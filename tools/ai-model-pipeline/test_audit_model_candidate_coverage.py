from __future__ import annotations

import unittest

import audit_model_candidate_coverage as audit


def group(name: str, ids: list[int], direct: int = 0, resource: int = 0, player: list[dict] | None = None) -> dict:
    pairs = []
    for renderer_id in ids:
        pairs.append({
            "sourceKind": "native_enemy_row",
            "nativeEnemy": f"enemy{renderer_id}",
            "resourcePrefab": None,
            "rendererPath": f"body{renderer_id}",
            "sourceRendererId": renderer_id,
        })
    return {
        "topologyGroup": name,
        "status": "direct_enemy_evidence_not_family_acceptance",
        "sourceRendererIds": ids,
        "exactSourcePairs": pairs,
        "directEnemyEvidence": [{"target": pair} for pair in pairs],
        "resourcePrefabEvidence": [],
        "nativeDirectEnemyPairs": direct,
        "nativeResourcePrefabPairs": resource,
        "exactSourcePairsWithIndexedOriginalEvidence": 1,
        "remainingExactSourcePairRepresentatives": [],
        "playerEvidence": player or [],
    }


def enemy(pointer: str, assignments: list[dict], missing: list[str]) -> dict:
    return {
        "indexPointer": pointer,
        "name": pointer,
        "status": "recorded",
        "archive": {"path": f"art/{pointer[1:]}.json"},
        "exactAssignments": assignments,
        "unrecordedSourceSpecificCoreEvidence": missing,
    }


class CandidateCoverageTests(unittest.TestCase):
    def test_single_exact_source_with_all_gates_is_a_route_representative(self) -> None:
        source = group("one", [10], direct=1)["exactSourcePairs"][0]
        report = audit.build_report(
            {"groups": [group("one", [10], direct=1)]},
            {"enemyRecords": [enemy("/enemy_models/0", [source], [])], "playerRecords": []},
            [],
        )
        item = report["groups"][0]
        direct = item["routes"]["directEnemy"]
        self.assertEqual(direct["status"], "canonical_representative_archive_recorded")
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 1)
        self.assertEqual(item["candidateCoverageStatus"], "canonical_route_representative_recorded")

    def test_multi_assignment_archive_cannot_credit_one_renderer_route(self) -> None:
        first = group("two", [10, 11], direct=2)["exactSourcePairs"]
        report = audit.build_report(
            {"groups": [group("two", [10, 11], direct=2)]},
            {"enemyRecords": [enemy("/enemy_models/1", first, [])], "playerRecords": []},
            [],
        )
        direct = report["groups"][0]["routes"]["directEnemy"]
        self.assertEqual(direct["singleExactSourceArchiveRecords"], 0)
        self.assertEqual(direct["completeExactSourceArchiveRecords"], 0)
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 0)
        self.assertEqual(direct["status"], "indexed_archive_needs_canonical_follow_up")

    def test_complete_multipart_renderer_set_for_one_identity_is_canonical(self) -> None:
        topology = group("multipart", [10, 11], direct=2)
        for pair in topology["exactSourcePairs"]:
            pair["nativeEnemy"] = "beholderA"
        report = audit.build_report(
            {"groups": [topology]},
            {"enemyRecords": [enemy("/enemy_models/2", topology["exactSourcePairs"], [])], "playerRecords": []},
            [],
        )
        direct = report["groups"][0]["routes"]["directEnemy"]
        record = direct["priorityRecord"]
        self.assertEqual(direct["singleExactSourceArchiveRecords"], 0)
        self.assertEqual(direct["completeExactSourceArchiveRecords"], 1)
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 1)
        self.assertTrue(record["exactSourceArchive"])
        self.assertEqual(
            record["exactSourceArchiveCheck"]["status"],
            "complete_exact_source_renderer_set",
        )

    def test_partial_multipart_renderer_set_is_not_canonical(self) -> None:
        topology = group("multipart", [10, 11], direct=2)
        for pair in topology["exactSourcePairs"]:
            pair["nativeEnemy"] = "beholderA"
        report = audit.build_report(
            {"groups": [topology]},
            {"enemyRecords": [enemy("/enemy_models/3", topology["exactSourcePairs"][:1], [])], "playerRecords": []},
            [],
        )
        record = report["groups"][0]["routes"]["directEnemy"]["priorityRecord"]
        self.assertFalse(record["exactSourceArchive"])
        self.assertEqual(
            record["exactSourceArchiveCheck"]["status"],
            "incomplete_or_extra_source_renderer_set",
        )
        self.assertEqual(len(record["exactSourceArchiveCheck"]["missingSourceAssignments"]), 1)

    def test_one_identity_spanning_topology_groups_is_not_canonical(self) -> None:
        first = group("first", [10], direct=1)
        second = group("second", [11], direct=1)
        first["exactSourcePairs"][0]["nativeEnemy"] = "multipartA"
        second["exactSourcePairs"][0]["nativeEnemy"] = "multipartA"
        assignments = first["exactSourcePairs"] + second["exactSourcePairs"]
        report = audit.build_report(
            {"groups": [first, second]},
            {"enemyRecords": [enemy("/enemy_models/4", assignments, [])], "playerRecords": []},
            [],
        )
        for topology in report["groups"]:
            record = topology["routes"]["directEnemy"]["priorityRecord"]
            self.assertFalse(record["exactSourceArchive"])
            self.assertEqual(
                record["exactSourceArchiveCheck"]["status"],
                "archive_assignments_extend_outside_topology_group",
            )

    def test_topology_scoped_archive_can_credit_one_part_of_cross_topology_identity(self) -> None:
        first = group("first", [10], direct=1)
        second = group("second", [11], direct=1)
        first["exactSourcePairs"][0]["nativeEnemy"] = "multipartA"
        second["exactSourcePairs"][0]["nativeEnemy"] = "multipartA"
        report = audit.build_report(
            {"groups": [first, second]},
            {"enemyRecords": [enemy("/enemy_models/5", first["exactSourcePairs"], [])], "playerRecords": []},
            [],
        )
        first_report = next(item for item in report["groups"] if item["topologyGroup"] == "first")
        record = first_report["routes"]["directEnemy"]["priorityRecord"]
        self.assertTrue(record["exactSourceArchive"])
        self.assertEqual(
            record["exactSourceArchiveCheck"]["sourceIdentityTopologyGroups"],
            ["first", "second"],
        )

    def test_player_evidence_stays_on_the_topology_group_that_names_it(self) -> None:
        player_evidence = [{"indexPointer": "/original_player_models/0"}]
        report = audit.build_report(
            {"groups": [group("player", [], player=player_evidence)]},
            {"enemyRecords": [], "playerRecords": [{
                "indexPointer": "/original_player_models/0",
                "name": "Player",
                "status": "recorded",
                "archive": {"path": "art/player.json"},
                "playerProfile": "player",
                "skinset": "Female",
                "rendererPaths": ["body"],
                "apparelPaths": [],
                "unrecordedSourceSpecificCoreEvidence": [],
            }]},
            [],
        )
        route = report["groups"][0]["routes"]["playerSkinset"]
        self.assertTrue(route["required"])
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        self.assertEqual(report["summary"]["unmappedPlayerArchives"], 0)

    def test_integrity_unverified_archive_cannot_credit_a_complete_route(self) -> None:
        source = group("one", [10], direct=1)["exactSourcePairs"][0]
        record = enemy("/enemy_models/0", [source], [])
        record["archive"]["actualSha256"] = "a" * 64
        report = audit.build_report(
            {"groups": [group("one", [10], direct=1)]},
            {"enemyRecords": [record], "playerRecords": []},
            [],
            {
                "art/enemy_models/0.json": {
                    "validation": "art/enemy_models/0.json",
                    "validationSha256": "a" * 64,
                    "status": "integrity_unverified",
                }
            },
            require_integrity=True,
        )
        direct = report["groups"][0]["routes"]["directEnemy"]
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 0)
        self.assertEqual(direct["integrityBlockedArchiveRecords"], 1)
        self.assertEqual(direct["priorityRecord"]["archiveIntegrity"]["status"], "integrity_unverified")

    def test_stale_integrity_hash_cannot_credit_a_complete_route(self) -> None:
        source = group("one", [10], direct=1)["exactSourcePairs"][0]
        record = enemy("/enemy_models/0", [source], [])
        record["archive"]["actualSha256"] = "a" * 64
        report = audit.build_report(
            {"groups": [group("one", [10], direct=1)]},
            {"enemyRecords": [record], "playerRecords": []},
            [],
            {
                "art/enemy_models/0.json": {
                    "validation": "art/enemy_models/0.json",
                    "validationSha256": "b" * 64,
                    "status": "artifact_integrity_verified",
                }
            },
            require_integrity=True,
        )
        direct = report["groups"][0]["routes"]["directEnemy"]
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 0)
        self.assertEqual(
            direct["priorityRecord"]["archiveIntegrity"]["status"],
            "integrity_stale_or_validation_hash_mismatch",
        )


if __name__ == "__main__":
    unittest.main()
