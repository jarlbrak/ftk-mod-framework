#!/usr/bin/env python3
"""Keep the generated validation-gate ledger tied to the current evidence index."""
from __future__ import annotations

import json
from pathlib import Path
import unittest

import audit_model_validation_gates as gates
from local_inputs import skip_without_local_inputs


ROOT = Path(__file__).resolve().parents[2]
LOCAL_INPUTS = (
    "scratch/enemy-rig-mapping-reproducible.json",
    "scratch/resource-enemy-base-preflight.json",
)


@skip_without_local_inputs(*LOCAL_INPUTS)
class CurrentValidationGateLedgerTests(unittest.TestCase):
    def test_generated_json_matches_current_inputs(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        generated = json.loads((ROOT / "docs/model-validation-gates.json").read_text())
        self.assertEqual(generated, report)
        self.assertEqual(report["summary"]["unresolvedIndexRecords"], 0)
        self.assertEqual(report["summary"]["archiveHashMismatches"], 0)

    def test_generated_markdown_matches_current_report(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        self.assertEqual((ROOT / "docs/MODEL-VALIDATION-GATES.md").read_text(), gates.render_markdown(report))

    def test_amberwake_v3_records_fixture_assisted_ordinary_hit_without_ordinary_lethal_credit(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        amberwake = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/116")
        gate = amberwake["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 675, "afterHp": 664, "scope": "source",
        }])
        self.assertEqual(amberwake["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_v4_head_records_source_scoped_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        rimecrown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/117")
        gate = rimecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 45, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(rimecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_v5_scarf_records_source_scoped_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        rimecrown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/118")
        gate = rimecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 45, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(rimecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_v6_base_records_source_scoped_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        rimecrown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/119")
        gate = rimecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 48, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(rimecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_v7_hat_records_source_scoped_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        rimecrown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/120")
        gate = rimecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 45, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(rimecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_rimecrown_v8_middle_body_records_source_scoped_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        rimecrown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/121")
        gate = rimecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 50, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(rimecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_honeyback_v3_records_exact_bearb_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        honeyback = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/122")
        gate = honeyback["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 72, "afterHp": 64, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(honeyback["unrecordedSourceSpecificCoreEvidence"], [])

    def test_duneshade_v2_records_exact_desert_a_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        duneshade = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/123")
        gate = duneshade["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 48, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(duneshade["unrecordedSourceSpecificCoreEvidence"], [])

    def test_sargassum_v2_records_exact_primary_tentacle_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        sargassum = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/124")
        gate = sargassum["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 162, "afterHp": 154, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(sargassum["unrecordedSourceSpecificCoreEvidence"], [])

    def test_ashfang_v2_records_exact_wolfa_motion_ragdoll_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        ashfang = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/125")
        gate = ashfang["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 50, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(ashfang["unrecordedSourceSpecificCoreEvidence"], [])

    def test_moonreed_v2_records_exact_fairya_motion_animated_death_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        moonreed = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/126")
        gate = moonreed["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 55, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(moonreed["unrecordedSourceSpecificCoreEvidence"], [])

    def test_abyssal_crown_v5_records_exact_kraken_head_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        crown = next(row for row in report["enemyRecords"] if row["indexPointer"] == "/enemy_models/127")
        gate = crown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 324, "afterHp": 316, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(crown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_mournglass_v2_records_exact_chaos_beast_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        mournglass = next(
            row for row in report["enemyRecords"]
            if row["indexPointer"] == "/enemy_models/128"
        )
        gate = mournglass["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 58, "afterHp": 53, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(mournglass["unrecordedSourceSpecificCoreEvidence"], [])

    def test_tidecrown_v2_records_exact_sea_king_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        tidecrown = next(
            row for row in report["enemyRecords"]
            if row["indexPointer"] == "/enemy_models/129"
        )
        gate = tidecrown["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 720, "afterHp": 719, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(tidecrown["unrecordedSourceSpecificCoreEvidence"], [])

    def test_bramblecoil_v2_records_exact_jungle_snake_motion_and_gameplay(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        bramblecoil = next(
            row for row in report["enemyRecords"]
            if row["indexPointer"] == "/enemy_models/130"
        )
        gate = bramblecoil["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertEqual(gate["gameplay"]["ordinaryDamageTransitions"], [{
            "path": "/ordinaryHit", "beforeHp": 86, "afterHp": 85, "scope": "source",
        }])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(bramblecoil["unrecordedSourceSpecificCoreEvidence"], [])

    def test_tamarind_v2_records_exact_passive_route_and_native_applicability(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        tamarind = next(
            row for row in report["enemyRecords"]
            if row["indexPointer"] == "/enemy_models/50"
        )
        gate = tamarind["gates"]
        self.assertTrue(gate["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(gate["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(gate["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertFalse(gate["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(gate["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertFalse(gate["gameplay"]["sourceSpecificOrdinaryDamageRecorded"])
        self.assertTrue(gate["gameplay"]["sourceSpecificReadyRecorded"])
        self.assertEqual(
            {row["requirement"] for row in gate["applicability"]["accepted"]},
            {"hitMotion", "ordinaryDamage"},
        )
        self.assertEqual(tamarind["unrecordedSourceSpecificCoreEvidence"], [])

    def test_wildbloom_canonical_route_has_every_player_shape(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        self.assertEqual(report["summary"]["indexedPlayerRecords"], 7)
        wildbloom = next(row for row in report["playerRecords"]
                         if row["name"] == "Wildbloom Herbalist canonical player route")
        gateset = wildbloom["gates"]
        self.assertTrue(gateset["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(gateset["appearance"]["reviewRecorded"])
        self.assertTrue(gateset["idle"]["motionCaptureRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewStateRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewAvatarObserved"])
        self.assertTrue(gateset["attack"]["motionCaptureRecorded"])
        self.assertTrue(gateset["hit"]["motionCaptureRecorded"])
        self.assertTrue(gateset["death"]["deathCaptureRecorded"])
        self.assertTrue(gateset["death"]["fixtureActionRecorded"])
        self.assertTrue(gateset["gameplay"]["ordinaryDamageRecorded"])
        self.assertTrue(gateset["gameplay"]["readyRecorded"])
        self.assertEqual(wildbloom["unrecordedSourceSpecificCoreEvidence"], [])

    def test_tideglass_v1_stays_a_preview_idle_and_art_revision_pending_record(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        tideglass = next(row for row in report["playerRecords"]
                         if row["playerProfile"] == "ftkmf_modeltest_player_tideglass_fishsmith"
                         and row["status"] == "native_character_creation_preview_and_idle_observed_art_revision_pending")
        self.assertEqual(tideglass["status"], "native_character_creation_preview_and_idle_observed_art_revision_pending")
        self.assertEqual(tideglass["rendererPaths"], ["playerFIsh", "hairTop", "hairBottom"])
        gateset = tideglass["gates"]
        self.assertTrue(gateset["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(gateset["appearance"]["reviewRecorded"])
        self.assertTrue(gateset["idle"]["motionCaptureRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewStateRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewAvatarObserved"])
        self.assertFalse(gateset["attack"]["motionCaptureRecorded"])
        self.assertFalse(gateset["hit"]["motionCaptureRecorded"])
        self.assertFalse(gateset["death"]["deathCaptureRecorded"])
        self.assertFalse(gateset["gameplay"]["ordinaryDamageRecorded"])
        self.assertFalse(gateset["gameplay"]["readyRecorded"])

    def test_gloamfin_kraken_canonical_route_has_every_source_specific_shape(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        kraken = next(row for row in report["enemyRecords"]
                      if row["archive"]["path"] == "art-experiments/gloamfin-kraken/live-validation-v2-canonical/validation.json")
        self.assertEqual(kraken["indexPointer"], "/enemy_models/131")
        self.assertEqual(kraken["unrecordedSourceSpecificCoreEvidence"], [])
        self.assertTrue(kraken["gates"]["runtimeBinding"]["sourceSpecificBindingRecorded"])
        self.assertTrue(kraken["gates"]["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(kraken["gates"]["idle"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(kraken["gates"]["attack"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(kraken["gates"]["hit"]["sourceSpecificMotionCaptureRecorded"])
        self.assertTrue(kraken["gates"]["death"]["sourceSpecificDeathCaptureRecorded"])
        self.assertTrue(kraken["gates"]["death"]["sourceSpecificFixtureActionRecorded"])
        self.assertTrue(kraken["gates"]["gameplay"]["sourceSpecificOrdinaryDamageRecorded"])
        self.assertTrue(kraken["gates"]["gameplay"]["sourceSpecificReadyRecorded"])

    def test_tideglass_v2_is_a_separately_pinned_sampled_preview_acceptance(self) -> None:
        report = gates.audit(
            ROOT,
            ROOT / "docs/model-runtime-validation.json",
            ROOT / "scratch/enemy-rig-mapping-reproducible.json",
            ROOT / "scratch/resource-enemy-base-preflight.json",
        )
        tideglass = next(row for row in report["playerRecords"]
                         if row["playerProfile"] == "ftkmf_modeltest_player_tideglass_fishsmith"
                         and row["status"] == "native_character_creation_preview_and_idle_observed_v2_preview_art_accepted")
        self.assertEqual(tideglass["indexPointer"], "/original_player_models/4")
        self.assertEqual(tideglass["rendererPaths"], ["playerFIsh", "hairTop", "hairBottom"])
        gateset = tideglass["gates"]
        self.assertTrue(gateset["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(gateset["appearance"]["reviewRecorded"])
        self.assertTrue(gateset["idle"]["motionCaptureRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewStateRecorded"])
        self.assertTrue(gateset["playerPreview"]["previewAvatarObserved"])
        self.assertFalse(gateset["attack"]["motionCaptureRecorded"])
        self.assertFalse(gateset["hit"]["motionCaptureRecorded"])
        self.assertFalse(gateset["death"]["deathCaptureRecorded"])
        self.assertFalse(gateset["gameplay"]["ordinaryDamageRecorded"])
        self.assertFalse(gateset["gameplay"]["readyRecorded"])


if __name__ == "__main__":
    unittest.main()
