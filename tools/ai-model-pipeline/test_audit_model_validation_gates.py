#!/usr/bin/env python3
"""Focused regression coverage for the conservative validation-gate ledger."""
from __future__ import annotations

import unittest

import audit_model_validation_gates as gates


class GateEvidenceTests(unittest.TestCase):
    def test_common_archive_records_explicit_fields_without_inferring_idle_from_pass(self) -> None:
        content = {
            "binding": {
                "celRelativeRendererPath": "wolf01",
                "mesh": "ftkmf_glb_example.glb",
                "boneSignature": "abc",
            },
            "rootVisualReview": "review/example.json",
            "captures": [
                {"label": "pass", "complete": True},
                {"label": "attack", "complete": True},
                {"label": "nonlethal-hit", "complete": True},
                {"label": "kill-fixture", "complete": True},
            ],
            "ordinaryHit": {"beforeHp": 58, "afterHp": 50, "focus": False},
            "explicitKillFixture": True,
            "finalReady": {"ok": True, "level": 0, "room": 2},
        }
        observed = gates.gate_evidence(content, "wolfA")
        self.assertTrue(observed["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(observed["appearance"]["reviewRecorded"])
        self.assertFalse(observed["idle"]["motionCaptureRecorded"])
        self.assertTrue(observed["attack"]["motionCaptureRecorded"])
        self.assertTrue(observed["hit"]["motionCaptureRecorded"])
        self.assertTrue(observed["death"]["deathCaptureRecorded"])
        self.assertTrue(observed["death"]["fixtureActionRecorded"])
        self.assertTrue(observed["gameplay"]["ordinaryDamageRecorded"])
        self.assertTrue(observed["gameplay"]["readyRecorded"])

    def test_current_causal_motion_schema_records_an_explicit_hit(self) -> None:
        content = {
            "captures": [{
                "label": "attack",
                "complete": True,
                "causalMotion": {
                    "schema": "ftkmf.exercise-motion-evidence.v1",
                    "action": "attack",
                    "idle": {"sampleIndex": 0},
                    "hit": {"nativeAction": {"role": "victim"}, "sampleIndex": 23},
                },
            }],
        }
        observed = gates.gate_evidence(content, "plantA")
        self.assertTrue(observed["idle"]["motionCaptureRecorded"])
        self.assertTrue(observed["hit"]["motionCaptureRecorded"])
        self.assertEqual(observed["hit"]["captures"][0]["causalMotionKinds"], ["idle", "hit"])

    def test_source_specific_trial_never_contributes_a_sibling_source_capture(self) -> None:
        content = {
            "visualReview": {"status": "reviewed"},
            "trials": {
                "primary": {
                    "nativeChassis": "krakenTentacle",
                    "binding": {"rendererPath": "krakenTentacle", "mesh": "primary.glb"},
                    "captures": [
                        {"label": "attack", "complete": True},
                        {"label": "kill-fixture", "complete": False, "termination": "renderer_destroyed"},
                    ],
                    "ordinaryAttack": [{"beforeHp": 162, "afterHp": 154}],
                    "ready": {"ok": True, "level": 0, "room": 2},
                },
                "mirror": {
                    "nativeChassis": "krakenTentacleMirror",
                    "binding": {"rendererPath": "krakenTentacle", "mesh": "mirror.glb"},
                    "captures": [{"label": "nonlethal-hit", "complete": True}],
                    "ordinaryAttack": [{"beforeHp": 162, "afterHp": 152}],
                },
            },
        }
        observed = gates.gate_evidence(content, "krakenTentacle")
        units = {row["path"] for row in observed["selectedEvidenceUnits"]}
        self.assertIn("/trials/primary", units)
        self.assertNotIn("/trials/mirror", units)
        self.assertTrue(observed["appearance"]["archiveLevelReviewRecorded"])
        self.assertFalse(observed["appearance"]["sourceSpecificReviewRecorded"])
        self.assertTrue(observed["attack"]["motionCaptureRecorded"])
        self.assertFalse(observed["hit"]["motionCaptureRecorded"])
        self.assertTrue(observed["death"]["deathCaptureRecorded"])
        self.assertTrue(observed["gameplay"]["ordinaryDamageRecorded"])
        self.assertTrue(observed["gameplay"]["readyRecorded"])

    def test_player_owner_inventory_and_enemy_hp_pair_are_recorded(self) -> None:
        content = {
            "profile": {
                "key": "ftkmf_modeltest_player_example",
                "baseClass": "blacksmith",
                "skinset": "blacksmith_Female",
                "defaultSkinType": "Female",
                "renderers": [{"rendererPath": "playerBlacksmith"}],
            },
            "avatarOwners": {
                "overworld": {"customSmrs": 1, "leaseId": 7},
                "combat": {"customSmrs": 1, "leaseId": 7},
                "preview": {"status": "pending_native_character_creation_ui", "observedAvatars": 0},
            },
            "captures": {
                "nativeAttack": {"sampledClips": ["attack_blunt1H"]},
                "nativePass": {"sampledClips": ["damageLight_blunt1H"]},
            },
            "gameplay": {
                "ordinaryAttack": {"enemyHp": [72, 62]},
                "finalReady": {"ok": True, "level": 0, "room": 3},
            },
        }
        observed = gates.gate_evidence(content, None, player=True)
        self.assertTrue(observed["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(observed["attack"]["motionCaptureRecorded"])
        self.assertTrue(observed["hit"]["motionCaptureRecorded"])
        self.assertTrue(observed["gameplay"]["ordinaryDamageRecorded"])
        self.assertEqual(
            observed["gameplay"]["ordinaryDamageTransitions"],
            [{"path": "/gameplay/ordinaryAttack/enemyHp", "beforeHp": 72, "afterHp": 62, "scope": "source"}],
        )
        self.assertTrue(observed["gameplay"]["readyRecorded"])
        self.assertTrue(observed["playerPreview"]["previewStateRecorded"])
        self.assertFalse(observed["playerPreview"]["previewAvatarObserved"])

    def test_legacy_capture_kind_and_runtime_inventory_are_explicit_evidence(self) -> None:
        content = {
            "initialRenderer": {
                "celRelativeRendererPath": "enCrow",
                "mesh": "ftkmf_glb_raven.glb",
                "boneSignature": "abc",
            },
            "rootReview": {"path": "review.json", "sha256": "a" * 64},
            "captures": [
                {"kind": "pass"},
                {"kind": "hit"},
                {"action": "kill-fixture"},
            ],
            "normalPlayerAttack": {"hpBefore": 58, "hpAfter": 45},
            "nextReady": {"ok": True, "level": 0, "room": 2},
        }
        observed = gates.gate_evidence(content, "crowC")
        self.assertTrue(observed["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(observed["appearance"]["reviewRecorded"])
        self.assertTrue(observed["hit"]["motionCaptureRecorded"])
        self.assertTrue(observed["death"]["deathCaptureRecorded"])
        self.assertTrue(observed["gameplay"]["ordinaryDamageRecorded"])
        self.assertTrue(observed["gameplay"]["readyRecorded"])

    def test_legacy_native_clip_stems_and_strict_ready_snapshot_are_structured_evidence(self) -> None:
        content = {
            "rendererPath": "enWolf",
            "rendererId": 121,
            "ownerInstanceId": 77,
            "boneSignature": "abc",
            "captures": {
                "combat": {
                    "states": [{"clips": ["cidle_wolf", "attack1", "damageSmall", "deathHeavy"]}],
                },
            },
            "ordinaryAttack": {"beforeHp": 58, "afterHp": 50},
            "gameplay": {"explicit_KillSingle_fixture_hp": [50, 0]},
            "afterReadyState": {"strictReady": True, "level": 0, "room": 3},
        }
        observed = gates.gate_evidence(content, "wolfA")
        self.assertTrue(observed["runtimeBinding"]["bindingRecorded"])
        self.assertTrue(observed["idle"]["motionCaptureRecorded"])
        self.assertTrue(observed["attack"]["motionCaptureRecorded"])
        self.assertTrue(observed["hit"]["motionCaptureRecorded"])
        self.assertTrue(observed["death"]["deathCaptureRecorded"])
        self.assertTrue(observed["death"]["fixtureActionRecorded"])
        self.assertTrue(observed["gameplay"]["ordinaryDamageRecorded"])
        self.assertTrue(observed["gameplay"]["readyRecorded"])

    def test_indexed_native_chassis_filters_sibling_assignments_from_one_archive(self) -> None:
        assignments = [
            {"nativeEnemy": "krakenTentacle", "rendererPath": "krakenTentacle", "sourceRendererId": 121595},
            {"nativeEnemy": "krakenTentacleMirror", "rendererPath": "krakenTentacle", "sourceRendererId": 121595},
        ]
        filtered = gates.assignments_for_index_entry(
            assignments,
            {"native_chassis": "krakenTentacleMirror", "renderer_paths": ["krakenTentacle"]},
        )
        self.assertEqual(filtered, [assignments[1]])

    def test_compact_assignment_preserves_an_explicit_route_kind(self) -> None:
        self.assertEqual(
            gates.compact_assignment({"nativeEnemy": "wolfA", "rendererPath": "wolf01"})["sourceKind"],
            "native_enemy_row",
        )
        self.assertEqual(
            gates.compact_assignment({"nativeEnemy": "snakeJungleA", "resourcePrefab": "enbaseysnake", "rendererPath": "enBaseySnake"})["sourceKind"],
            "resource_prefab_override",
        )

    def test_unrecorded_core_evidence_requires_source_specific_records(self) -> None:
        observed = gates.gate_evidence({
            "binding": {"rendererPath": "wolf01", "mesh": "example.glb"},
            "rootVisualReview": {"status": "reviewed"},
            "captures": [
                {"label": "idle"}, {"label": "attack"}, {"label": "hit"}, {"label": "kill-fixture"},
            ],
            "explicitKillFixture": {"ok": True},
            "ordinaryHit": {"beforeHp": 58, "afterHp": 50},
            "finalReady": {"ok": True},
        }, "wolfA")
        self.assertEqual(gates.unrecorded_source_specific_core_evidence(observed, player=False), [])
        archive_only = {
            **observed,
            "runtimeBinding": {**observed["runtimeBinding"], "sourceSpecificBindingRecorded": False,
                               "archiveLevelBindingRecorded": True},
        }
        self.assertEqual(gates.unrecorded_source_specific_core_evidence(archive_only, player=False), ["runtimeBinding"])

    def test_passive_self_removal_can_mark_only_two_native_gates_not_applicable(self) -> None:
        content = {
            "nativeSuicideFixture": {"observed": True, "initialHealth": 58, "terminalHealth": 0},
            "sourceSpecificEvidenceApplicability": {
                "schema": "ftkmf.source-specific-evidence-applicability.v1",
                "nativeChassis": "monkeyC",
                "workflow": "passive_enemy_arrival",
                "requirements": {
                    "hitMotion": {
                        "status": "not_applicable_native_behavior",
                        "reasonCode": "native_self_removal_has_no_received_hit_phase",
                        "evidencePaths": ["/nativeSuicideFixture"],
                    },
                    "ordinaryDamage": {
                        "status": "not_applicable_native_behavior",
                        "reasonCode": "native_self_removal_precedes_hero_attack",
                        "evidencePaths": ["/nativeSuicideFixture"],
                    },
                },
            },
        }
        observed = gates.gate_evidence(content, "monkeyC")
        accepted = {row["requirement"] for row in observed["applicability"]["accepted"]}
        self.assertEqual(accepted, {"hitMotion", "ordinaryDamage"})
        missing = gates.unrecorded_source_specific_core_evidence(observed, player=False)
        self.assertNotIn("hitMotion", missing)
        self.assertNotIn("ordinaryDamage", missing)
        self.assertIn("runtimeBinding", missing)

    def test_native_not_applicable_rejects_wrong_chassis_or_missing_evidence(self) -> None:
        for chassis, paths in (("wolfA", ["/nativeSuicideFixture"]), ("monkeyC", ["/missing"])):
            content = {
                "nativeSuicideFixture": {"observed": True},
                "sourceSpecificEvidenceApplicability": {
                    "schema": "ftkmf.source-specific-evidence-applicability.v1",
                    "nativeChassis": chassis,
                    "workflow": "passive_enemy_arrival",
                    "requirements": {
                        "ordinaryDamage": {
                            "status": "not_applicable_native_behavior",
                            "reasonCode": "native_self_removal_precedes_hero_attack",
                            "evidencePaths": paths,
                        },
                    },
                },
            }
            observed = gates.gate_evidence(content, "monkeyC")
            self.assertEqual(observed["applicability"]["accepted"], [])
            self.assertIn("ordinaryDamage", gates.unrecorded_source_specific_core_evidence(observed, player=False))


if __name__ == "__main__":
    unittest.main()
