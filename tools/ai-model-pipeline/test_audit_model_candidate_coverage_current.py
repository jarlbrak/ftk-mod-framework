"""Keep generated candidate validation coverage tied to its current inputs."""
from __future__ import annotations

import json
from pathlib import Path
import unittest

import audit_model_candidate_coverage as coverage


ROOT = Path(__file__).resolve().parents[2]


class CurrentCandidateCoverageTests(unittest.TestCase):
    def report(self) -> dict:
        topology = ROOT / "docs/topology-coverage.json"
        gates = ROOT / "docs/model-validation-gates.json"
        integrity = ROOT / "docs/model-validation-archive-integrity.json"
        return coverage.build_report(
            json.loads(topology.read_text()),
            json.loads(gates.read_text()),
            [
                coverage.input_reference(ROOT, topology),
                coverage.input_reference(ROOT, gates),
                coverage.input_reference(ROOT, integrity),
            ],
            coverage.integrity_index(json.loads(integrity.read_text())),
            require_integrity=True,
        )

    def test_generated_json_matches_current_inputs(self) -> None:
        report = self.report()
        generated = json.loads((ROOT / "docs/model-candidate-validation-coverage.json").read_text())
        self.assertEqual(generated, report)
        self.assertEqual(report["summary"]["unmappedEnemyAssignments"], 0)
        self.assertEqual(report["summary"]["ambiguousEnemyAssignments"], 0)
        self.assertEqual(report["summary"]["unmappedPlayerArchives"], 0)
        self.assertEqual(report["summary"]["unmatchedPlayerEvidence"], 0)

    def test_generated_markdown_matches_current_report(self) -> None:
        self.assertEqual(
            (ROOT / "docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md").read_text(),
            coverage.markdown(self.report()),
        )

    def test_amberwake_dragonfrost_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "c01698c74bd54910"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/116")
        self.assertEqual(record["archive"]["path"], "art-experiments/amberwake-dragon/live-validation-v3/validation.json")
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])

    def test_tamarind_passive_route_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "81f02cdbf3eefcb9"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/50")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/tamarind-trickster/live-validation-v2/validation.json",
        )
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_snowmanb_head_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "02a6f31412bd52ab"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/117")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/rimecrown-sentinel/live-validation-v4-head/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snowmanB",
            "resourcePrefab": None,
            "rendererPath": "SnowMan_Geo/enSnowmanHead",
            "sourceRendererId": 121415,
        }])

    def test_rimecrown_snowmanb_scarf_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "1322fec3354db550"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/118")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/rimecrown-sentinel/live-validation-v5-scarf/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snowmanB",
            "resourcePrefab": None,
            "rendererPath": "SnowMan_Geo/enSnowmanScarf",
            "sourceRendererId": 121639,
        }])

    def test_rimecrown_snowmanb_base_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "8c73c366b064726a"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/119")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/rimecrown-sentinel/live-validation-v6-base/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snowmanB",
            "resourcePrefab": None,
            "rendererPath": "SnowMan_Geo/enSnowmanBase",
            "sourceRendererId": 121557,
        }])

    def test_rimecrown_snowmanb_hat_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "963e53f60643b796"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/120")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/rimecrown-sentinel/live-validation-v7-hat/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snowmanB",
            "resourcePrefab": None,
            "rendererPath": "SnowMan_Geo/enSnowmanHat",
            "sourceRendererId": 121405,
        }])

    def test_rimecrown_snowmanb_middle_body_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "a158ab62f9dd430f"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/121")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/rimecrown-sentinel/live-validation-v8-middle-body/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snowmanB",
            "resourcePrefab": None,
            "rendererPath": "SnowMan_Geo/enSnowmanmiddleBody",
            "sourceRendererId": 121697,
        }])

    def test_honeyback_bearb_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "07f911c982da0402"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/122")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/honeyback-portrait-v2/live-validation-v3-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "bearB",
            "resourcePrefab": None,
            "rendererPath": "enBear01",
            "sourceRendererId": 121467,
        }])

    def test_duneshade_desert_a_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "80d61d495b6a93be"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/123")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/duneshade-desert-asp/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snakeDesertA",
            "resourcePrefab": None,
            "rendererPath": "enDesertSnakeA",
            "sourceRendererId": 121552,
        }])

    def test_sargassum_primary_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "aae2ba644a16c223"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/124")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "krakenTentacle",
            "resourcePrefab": None,
            "rendererPath": "krakenTentacle",
            "sourceRendererId": 121595,
        }])

    def test_ashfang_wolfa_is_one_exact_canonical_direct_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "c3487d422b832d2e"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 2)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 9)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/125")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/ashfang-wolf/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "wolfA",
            "resourcePrefab": None,
            "rendererPath": "wolf01",
            "sourceRendererId": 121142,
        }])

    def test_moonreed_fairya_is_one_exact_canonical_direct_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "c3683bc2e807b15a"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 3)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/126")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/moonreed-sylph/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "fairyA",
            "resourcePrefab": None,
            "rendererPath": "enFairy01",
            "sourceRendererId": 121395,
        }])

    def test_abyssal_crown_kraken_head_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "e45711bff451ca73"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 0)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/127")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/abyssal-kraken/live-validation-head-v5-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "krakenHead",
            "resourcePrefab": None,
            "rendererPath": "kraken2",
            "sourceRendererId": 121035,
        }])

    def test_mournglass_chaos_beast_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "d1de8c46112a77d8"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 0)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/128")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/mournglass-wraith/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "chaosBeast",
            "resourcePrefab": None,
            "rendererPath": "enChaosBeast",
            "sourceRendererId": 121008,
        }])

    def test_thistlewick_scourgeg_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "f50b09e31a8484cf"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/68")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "scourgeG",
            "resourcePrefab": None,
            "rendererPath": "enScourgeLeprechaun",
            "sourceRendererId": 121222,
        }])

    def test_verdigrin_mimica_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "cace272650590c4f"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/111")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "mimicA",
            "resourcePrefab": None,
            "rendererPath": "mimic01",
            "sourceRendererId": 121192,
        }])

    def test_sunspire_rocA_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "0f29e98795c302a5"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/112")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "rocA",
            "resourcePrefab": None,
            "rendererPath": "enRoc01",
            "sourceRendererId": 121238,
        }])

    def test_belladusk_plantE_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "1329d6985dadecee"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/113")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "plantE",
            "resourcePrefab": None,
            "rendererPath": "enJungleNibbler_A",
            "sourceRendererId": 121530,
        }])

    def test_bronzewake_armor_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "11742c19aa67e6d0"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/95")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "bossGladiator",
            "resourcePrefab": None,
            "rendererPath": "armorBossGladiator",
            "sourceRendererId": 121522,
        }])

    def test_bronzewake_body_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "73ef97795cc54f57"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/103")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "bossGladiator",
            "resourcePrefab": None,
            "rendererPath": "enBossGladiator",
            "sourceRendererId": 121272,
        }])

    def test_bronzewake_boots_are_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "b65252b48fd9609d"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/106")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "bossGladiator",
            "resourcePrefab": None,
            "rendererPath": "bootsBossGladiator",
            "sourceRendererId": 121661,
        }])

    def test_bronzewake_hair_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "fb84ec3e6e18f459"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/108")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "bossGladiator",
            "resourcePrefab": None,
            "rendererPath": "hairBottomBossGladiator",
            "sourceRendererId": 121500,
        }])

    def test_mirewarden_and_gloamcap_are_separate_canonical_routes(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "db2a524a5bc700ea"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        direct = group["routes"]["directEnemy"]
        self.assertEqual(direct["canonicalRepresentativeArchiveRecords"], 1)
        record = direct["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/109")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "trollCaveA",
            "resourcePrefab": None,
            "rendererPath": "enTroll01",
            "sourceRendererId": 121153,
        }])
        resource = group["routes"]["resourcePrefab"]
        self.assertEqual(resource["canonicalRepresentativeArchiveRecords"], 1)
        resource_record = resource["priorityRecord"]
        self.assertEqual(resource_record["indexPointer"], "/enemy_models/110")
        self.assertTrue(resource_record["singleExactSourceArchive"])
        self.assertTrue(resource_record["exactSourceArchive"])
        self.assertTrue(resource_record["canonicalExactSourceEvidence"])
        self.assertEqual(resource_record["allArchiveAssignments"], [{
            "sourceKind": "resource_prefab_override",
            "nativeEnemy": "impA",
            "resourcePrefab": "enbaseyimp",
            "rendererPath": "enBaseyImp",
            "sourceRendererId": 121117,
        }])

    def test_duskquill_crowc_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "ba17c1398db51453"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/107")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "crowC",
            "resourcePrefab": None,
            "rendererPath": "enCrow",
            "sourceRendererId": 120964,
        }])

    def test_tideglass_crabb_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "791b63f3064c2f62"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/104")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "crabB",
            "resourcePrefab": None,
            "rendererPath": "enCrabWizard",
            "sourceRendererId": 121411,
        }])

    def test_cinderwing_bata_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "896faa557db56261"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/105")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "batA",
            "resourcePrefab": None,
            "rendererPath": "enBat01",
            "sourceRendererId": 121104,
        }])

    def test_saffronspine_puffera_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "2185298a0a369e67"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/96")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "pufferA",
            "resourcePrefab": None,
            "rendererPath": "enBlowFishA",
            "sourceRendererId": 121509,
        }])

    def test_copperveil_spiderb_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "23c62612fd16b533"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 2)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/97")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "spiderB",
            "resourcePrefab": None,
            "rendererPath": "enSpiderB",
            "sourceRendererId": 121386,
        }])

    def test_reefstrider_fisha01_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "5990c51ca004ebdb"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 4)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/98")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "fishA01",
            "resourcePrefab": None,
            "rendererPath": "enFishA",
            "sourceRendererId": 121695,
        }])

    def test_basilight_cockatricec_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "678c8066b33cf823"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 3)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 1)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/99")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "cockatriceC",
            "resourcePrefab": None,
            "rendererPath": "enChicken",
            "sourceRendererId": 121484,
        }])

    def test_mossglass_cubea_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "6d40f2e6eba19a6b"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 6)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/100")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "cubeA",
            "resourcePrefab": None,
            "rendererPath": "enJellyCube",
            "sourceRendererId": 121012,
        }])

    def test_emberglass_beea_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "6fdb7ff148451731"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 2)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/101")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["canonicalSingleSourceEvidence"])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "beeA",
            "resourcePrefab": None,
            "rendererPath": "Monster Bee",
            "sourceRendererId": 121062,
        }])

    def test_vesper_beholdera_complete_multipart_source_is_canonical(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "73739eaf6fd8f0e4"
        )
        self.assertEqual(
            group["candidateCoverageStatus"],
            "canonical_route_representative_recorded_exact_source_pairs_remain",
        )
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        self.assertEqual(route["completeExactSourceArchiveRecords"], 4)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/102")
        self.assertFalse(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(
            record["exactSourceArchiveCheck"]["status"],
            "complete_exact_source_renderer_set",
        )
        self.assertEqual(
            record["exactSourceArchiveCheck"]["expectedSourceAssignments"],
            record["allArchiveAssignments"],
        )


    def test_tidecrown_sea_king_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "1eda40629ee9aac9"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 0)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/129")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/tidecrown-sea-king/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "seaKing",
            "resourcePrefab": None,
            "rendererPath": "enSeaKing",
            "sourceRendererId": 121357,
        }])

    def test_gloamfin_kraken_is_one_exact_canonical_resource_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "6a28ac3cf4523c24"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 0)
        route = group["routes"]["resourcePrefab"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/131")
        self.assertEqual(record["archive"]["path"], "art-experiments/gloamfin-kraken/live-validation-v2-canonical/validation.json")
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "resource_prefab_override",
            "nativeEnemy": "krakenHead",
            "resourcePrefab": "enkrakenhead",
            "rendererPath": "krakenHead",
            "sourceRendererId": 121260,
        }])

    def test_bramblecoil_jungle_snake_is_one_exact_canonical_representative(self) -> None:
        group = next(
            row for row in self.report()["groups"]
            if row["topologyGroup"] == "7b140c07befa33fd"
        )
        self.assertEqual(group["candidateCoverageStatus"], "canonical_route_representative_recorded")
        self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
        self.assertEqual(group["remainingExactSourcePairRepresentativeCount"], 0)
        route = group["routes"]["directEnemy"]
        self.assertEqual(route["canonicalRepresentativeArchiveRecords"], 1)
        record = route["priorityRecord"]
        self.assertEqual(record["indexPointer"], "/enemy_models/130")
        self.assertEqual(
            record["archive"]["path"],
            "art-experiments/bramblecoil-jungle-snake/live-validation-v2-canonical/validation.json",
        )
        self.assertTrue(record["singleExactSourceArchive"])
        self.assertTrue(record["exactSourceArchive"])
        self.assertTrue(record["canonicalExactSourceEvidence"])
        self.assertEqual(record["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertEqual(record["allArchiveAssignments"], [{
            "sourceKind": "native_enemy_row",
            "nativeEnemy": "snakeJungleC",
            "resourcePrefab": None,
            "rendererPath": "enJungleSnakeC",
            "sourceRendererId": 121424,
        }])


if __name__ == "__main__":
    unittest.main()
