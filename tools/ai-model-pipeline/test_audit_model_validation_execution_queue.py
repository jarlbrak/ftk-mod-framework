from __future__ import annotations

import unittest

import audit_model_validation_execution_queue as queue


def source(
    renderer_id: int,
    *,
    native_enemy: str = "enemy",
    resource_prefab: str | None = None,
    source_kind: str = "native_enemy_row",
) -> dict:
    return {
        "sourceKind": source_kind,
        "nativeEnemy": native_enemy,
        "resourcePrefab": resource_prefab,
        "rendererPath": f"body{renderer_id}",
        "sourceRendererId": renderer_id,
    }


def profile(
    key: str,
    assignment: dict,
    *,
    route_kind: str = "direct_enemy",
    skinset: str | None = None,
) -> dict:
    return {
        "key": key,
        "routeKind": route_kind,
        "baseEnemy": assignment.get("nativeEnemy"),
        "resourcePrefab": assignment.get("resourcePrefab"),
        "skinset": skinset,
        "assignments": [{
            "rendererPath": assignment.get("rendererPath"),
            "sourceRendererId": assignment.get("sourceRendererId"),
            "rendererKind": "SkinnedMeshRenderer",
        }],
    }


def readiness(*profiles: dict) -> dict:
    return {
        "packages": [{
            "package": "art/example",
            "preflight": {"status": "pass", "result": "passed"},
            "profileDocument": {"path": "art/example/runtime-profile.json"},
            "profiles": list(profiles),
        }]
    }


def topology_group(
    name: str,
    ids: list[int],
    *,
    remaining: list[dict] | None = None,
    player_evidence: list[dict] | None = None,
    ownership: dict | None = None,
) -> dict:
    result = {
        "topologyGroup": name,
        "sourceRendererIds": ids,
        "remainingExactSourcePairRepresentatives": remaining or [],
        "playerEvidence": player_evidence or [],
    }
    if ownership is not None:
        result["ownershipClassification"] = ownership
    return result


class ExecutionQueueTests(unittest.TestCase):
    def test_priority_enemy_record_selects_only_its_exact_assignment(self) -> None:
        first = source(10, native_enemy="first")
        second = source(11, native_enemy="second")
        report = queue.build_report(
            {"groups": [topology_group("g", [10, 11])]},
            {
                "backlog": [{
                    "topologyGroup": "g",
                    "routeKind": "directEnemy",
                    "routeStatus": "indexed_archive_needs_canonical_follow_up",
                    "priorityRecord": {"indexPointer": "/enemy_models/1"},
                }],
                "groups": [{
                    "topologyGroup": "g",
                    "enemyArchiveRecords": [{
                        "indexPointer": "/enemy_models/1",
                        "assignmentsInThisTopologyGroup": [second],
                    }],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(
                profile("first-profile", first),
                profile("second-profile", second),
            ),
            [],
        )
        route = report["routes"][0]
        self.assertEqual(route["selectionBasis"], "priority_archive_record_exact_source_assignment")
        self.assertEqual(route["selectedValidationTargets"][0]["sourceRendererId"], 11)
        self.assertEqual(
            [candidate["key"] for candidate in route["preflightedProfileCandidates"]],
            ["second-profile"],
        )
        self.assertEqual(route["validationTargetsWithoutPreflightedProfile"], [])

    def test_unindexed_route_uses_remaining_exact_representative(self) -> None:
        target = source(
            20,
            native_enemy="resource-enemy",
            resource_prefab="resourcePrefab",
            source_kind="resource_prefab_override",
        )
        report = queue.build_report(
            {"groups": [topology_group("resource", [20], remaining=[target])]},
            {
                "backlog": [{
                    "topologyGroup": "resource",
                    "routeKind": "resourcePrefab",
                    "routeStatus": "no_indexed_original_archive",
                    "priorityRecord": None,
                }],
                "groups": [{
                    "topologyGroup": "resource",
                    "enemyArchiveRecords": [],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(profile("resource-profile", target, route_kind="resource_prefab_override")),
            [],
        )
        route = report["routes"][0]
        self.assertEqual(route["selectionBasis"], "unindexed_remaining_topology_representative")
        self.assertEqual(route["selectedValidationTargets"][0]["sourceRendererId"], 20)
        self.assertEqual(route["preflightedProfileCandidates"][0]["key"], "resource-profile")

    def test_priority_player_record_does_not_pull_other_group_profiles(self) -> None:
        blacksmith = {
            "indexPointer": "/original_player_models/0",
            "profile": "blacksmith-profile",
            "skinset": "blacksmith_Female",
            "rendererPaths": ["body10"],
        }
        herbalist = {
            "indexPointer": "/original_player_models/1",
            "profile": "herbalist-profile",
            "skinset": "herbalist_Female",
            "rendererPaths": ["body11"],
        }
        blacksmith_assignment = source(10)
        herbalist_assignment = source(11)
        report = queue.build_report(
            {"groups": [topology_group("player", [10, 11], player_evidence=[blacksmith, herbalist])]},
            {
                "backlog": [{
                    "topologyGroup": "player",
                    "routeKind": "playerSkinset",
                    "routeStatus": "indexed_archive_needs_canonical_follow_up",
                    "priorityRecord": {"indexPointer": "/original_player_models/0"},
                }],
                "groups": [{
                    "topologyGroup": "player",
                    "enemyArchiveRecords": [],
                    "playerArchiveRecords": [blacksmith, herbalist],
                }],
            },
            readiness(
                profile(
                    "blacksmith-profile",
                    blacksmith_assignment,
                    route_kind="player_skinset_avatar",
                    skinset="blacksmith_Female",
                ),
                profile(
                    "herbalist-profile",
                    herbalist_assignment,
                    route_kind="player_skinset_avatar",
                    skinset="herbalist_Female",
                ),
            ),
            [],
        )
        route = report["routes"][0]
        self.assertEqual(route["selectionBasis"], "priority_archive_record_exact_player_profile")
        self.assertEqual(route["selectedValidationTargets"][0]["profile"], "blacksmith-profile")
        self.assertEqual(
            [candidate["key"] for candidate in route["preflightedProfileCandidates"]],
            ["blacksmith-profile"],
        )

    def test_missing_exact_profile_is_exposed_as_a_planning_gap(self) -> None:
        target = source(30, native_enemy="missing")
        report = queue.build_report(
            {"groups": [topology_group("missing", [30])]},
            {
                "backlog": [{
                    "topologyGroup": "missing",
                    "routeKind": "directEnemy",
                    "routeStatus": "indexed_archive_needs_canonical_follow_up",
                    "priorityRecord": {"indexPointer": "/enemy_models/3"},
                }],
                "groups": [{
                    "topologyGroup": "missing",
                    "enemyArchiveRecords": [{
                        "indexPointer": "/enemy_models/3",
                        "assignmentsInThisTopologyGroup": [target],
                    }],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(),
            [],
        )
        route = report["routes"][0]
        self.assertEqual(route["preflightedProfileCandidates"], [])
        self.assertEqual(route["validationTargetsWithoutPreflightedProfile"][0]["sourceRendererId"], 30)
        self.assertEqual(route["executionStatus"]["status"], "profile_authoring_required")
        self.assertEqual(report["summary"]["routesWithoutPreflightedProfileCandidates"], 1)
        self.assertEqual(report["summary"]["selectedValidationTargetsWithoutPreflightedProfile"], 1)

    def test_explicit_resource_controller_incompatibility_requires_an_adapter(self) -> None:
        target = source(
            50,
            native_enemy="krakenHead",
            resource_prefab="enkrakenhead",
            source_kind="resource_prefab_override",
        )
        report = queue.build_report(
            {
                "groups": [topology_group(
                    "resource",
                    [50],
                    remaining=[target],
                    ownership={"status": "resolved_resource_prefab_not_native_enemy_row_controller_incompatible"},
                )]
            },
            {
                "backlog": [{
                    "topologyGroup": "resource",
                    "routeKind": "resourcePrefab",
                    "routeStatus": "no_indexed_original_archive",
                    "priorityRecord": None,
                }],
                "groups": [{
                    "topologyGroup": "resource",
                    "enemyArchiveRecords": [],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(),
            [],
        )
        route = report["routes"][0]
        self.assertEqual(route["executionStatus"]["status"], "adapter_or_retarget_design_required")
        self.assertEqual(
            report["summary"]["routesRequiringAdapterOrRetargetDesign"],
            1,
        )
        self.assertIn("Do not stage", route["nextAction"])

    def test_pinned_design_advances_the_route_to_implementation_required(self) -> None:
        target = source(
            50,
            native_enemy="krakenHead",
            resource_prefab="enkrakenhead",
            source_kind="resource_prefab_override",
        )
        contract = {
            "document": {"path": "docs/evidence/adapter/contract.json", "sha256": "abc"},
            "contract": {
                "schema": "ftkmf.kraken-production-adapter-contract.v1",
                "status": "design_complete_implementation_pending",
                "route": {"topologyGroup": "resource", "routeKind": "resourcePrefab"},
            },
        }
        report = queue.build_report(
            {
                "groups": [topology_group(
                    "resource",
                    [50],
                    remaining=[target],
                    ownership={"status": "resolved_resource_prefab_not_native_enemy_row_controller_incompatible"},
                )]
            },
            {
                "backlog": [{
                    "topologyGroup": "resource",
                    "routeKind": "resourcePrefab",
                    "routeStatus": "no_indexed_original_archive",
                    "priorityRecord": None,
                }],
                "groups": [{
                    "topologyGroup": "resource",
                    "enemyArchiveRecords": [],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(),
            [],
            [contract],
        )
        route = report["routes"][0]
        self.assertEqual(route["executionStatus"]["status"], "adapter_implementation_required")
        self.assertEqual(route["executionStatus"]["adapterContract"], contract["document"])
        self.assertEqual(report["summary"]["routesRequiringAdapterOrRetargetDesign"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterImplementation"], 1)
        self.assertIn("Implement and review", route["nextAction"])

    def test_completed_adapter_campaign_advances_only_to_visual_archive_review(self) -> None:
        target = source(
            50,
            native_enemy="krakenHead",
            resource_prefab="enkrakenhead",
            source_kind="resource_prefab_override",
        )
        target["rendererPath"] = "krakenHead"
        contract = {
            "document": {"path": "docs/evidence/adapter/contract.json", "sha256": "abc"},
            "contract": {
                "schema": "ftkmf.kraken-production-adapter-contract.v1",
                "status": "implementation_complete_validation_pending",
                "route": {
                    "topologyGroup": "resource",
                    "routeKind": "resourcePrefab",
                    "nativeEnemy": "krakenHead",
                    "resourcePrefab": "enkrakenhead",
                    "rendererPath": "krakenHead",
                    "sourceRendererId": 50,
                },
            },
        }
        campaign = {
            "document": {"path": "docs/evidence/adapter-live/validation.json", "sha256": "def"},
            "integrity": {"path": "docs/evidence/adapter-live/integrity.json", "sha256": "ghi"},
            "status": "production_observation_campaign_satisfied",
            "routeKind": "resourcePrefab",
            "route": {
                "topologyGroup": "resource",
                "sourceKind": "resource_prefab_override",
                "nativeEnemy": "krakenHead",
                "resourcePrefab": "enkrakenhead",
                "rendererPath": "krakenHead",
                "sourceRendererId": 50,
            },
        }
        report = queue.build_report(
            {
                "groups": [topology_group(
                    "resource",
                    [50],
                    remaining=[target],
                    ownership={"status": "resolved_resource_prefab_not_native_enemy_row_controller_incompatible"},
                )]
            },
            {
                "backlog": [{
                    "topologyGroup": "resource",
                    "routeKind": "resourcePrefab",
                    "routeStatus": "no_indexed_original_archive",
                    "priorityRecord": None,
                }],
                "groups": [{
                    "topologyGroup": "resource",
                    "enemyArchiveRecords": [],
                    "playerArchiveRecords": [],
                }],
            },
            readiness(),
            [],
            [contract],
            [campaign],
        )
        route = report["routes"][0]
        self.assertEqual(route["executionStatus"]["status"], "adapter_visual_archive_review_required")
        self.assertEqual(route["executionStatus"]["adapterCampaign"], campaign["document"])
        self.assertEqual(route["executionStatus"]["adapterCampaignIntegrity"], campaign["integrity"])
        self.assertEqual(report["summary"]["routesRequiringAdapterValidation"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterVisualArchiveReview"], 1)
        self.assertIn("Do not repeat", route["nextAction"])

    def test_adapter_campaign_must_match_the_contract_source_pair(self) -> None:
        contract = {
            "route": {
                "topologyGroup": "resource",
                "routeKind": "resourcePrefab",
                "nativeEnemy": "krakenHead",
                "resourcePrefab": "enkrakenhead",
                "rendererPath": "krakenHead",
                "sourceRendererId": 50,
            }
        }
        campaign = {
            "routeKind": "resourcePrefab",
            "route": {
                "topologyGroup": "resource",
                "nativeEnemy": "krakenHead",
                "resourcePrefab": "enkrakenhead",
                "rendererPath": "wrong",
                "sourceRendererId": 50,
            },
        }
        with self.assertRaisesRegex(ValueError, "rendererPath"):
            queue.matching_adapter_campaign("resource", "resourcePrefab", contract, [campaign])

    def test_priority_pointer_must_resolve_to_a_detailed_record(self) -> None:
        with self.assertRaisesRegex(ValueError, "no detailed enemy archive record"):
            queue.build_report(
                {"groups": [topology_group("broken", [40])]},
                {
                    "backlog": [{
                        "topologyGroup": "broken",
                        "routeKind": "directEnemy",
                        "priorityRecord": {"indexPointer": "/enemy_models/missing"},
                    }],
                    "groups": [{
                        "topologyGroup": "broken",
                        "enemyArchiveRecords": [],
                        "playerArchiveRecords": [],
                    }],
                },
                readiness(),
                [],
            )


if __name__ == "__main__":
    unittest.main()
