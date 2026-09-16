from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import archive_model_validation_case as archiver
import verify_model_validation_archive as verifier


PNG = b"\x89PNG\r\n\x1a\nfixture-png"


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


class ArchivePlanFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        (root / "FTKModFramework").mkdir()
        self.case_root = root / "scratch" / "case"
        self.asset_root = root / "art-experiments" / "example"
        self.case_root.mkdir(parents=True)
        self.asset_root.mkdir(parents=True)
        frames = self.case_root / "pass"
        frames.mkdir()
        for index in range(2):
            (frames / f"{index:04d}.png").write_bytes(PNG)
        self.raw_capture = self.case_root / "pass.json"
        write_json(self.raw_capture, {
            "ok": True,
            "error": None,
            "width": 2,
            "height": 2,
            "timingMode": "fixed-step",
            "frames": [
                {"celRelativeRendererPath": "body", "mesh": "ftkmf_glb_example.glb", "boneSignature": "bones"},
                {"celRelativeRendererPath": "body", "mesh": "ftkmf_glb_example.glb", "boneSignature": "bones"},
            ],
        })
        write_json(self.case_root / "pass-result.json", {"ok": True})
        write_json(self.case_root / "pass-journal.json", {"events": []})
        self.case_path = self.case_root / "case-result.json"
        write_json(self.case_path, {
            "session": "session-1",
            "enemy": "ftkmf_modeltest_example",
            "profileSha256": "a" * 64,
            "finalReady": {"strictReady": {"ok": True, "level": 0, "room": 2, "buttonCount": 1}},
            "actions": [{
                "action": "pass",
                "actionAccepted": True,
                "capture": {
                    "rawCapture": {"path": str(self.raw_capture.relative_to(root))},
                    "boundary": {"completeCapture": True, "termination": "complete"},
                },
                "rawResult": {"path": str((self.case_root / "pass-result.json").relative_to(root))},
                "journal": {"path": str((self.case_root / "pass-journal.json").relative_to(root))},
                "motionEvidence": {"attack": {"nativeTrigger": {"trigger": "Attack"}}},
            }],
        })
        self.review_path = self.case_root / "visual-review.json"
        binding = {
            "rendererPath": "body",
            "mesh": "ftkmf_glb_example.glb",
            "boneSignature": "bones",
        }
        write_json(self.review_path, {
            "session": "session-1",
            "reviewStatus": "reviewed_fixture",
            "binding": binding,
            "frames": [{
                "path": "scratch/case/pass/0000.png",
                "sha256": sha256(PNG),
                "action": "pass",
                "index": 0,
                "observation": "The fixture remains visible.",
            }],
        })
        self.profile_path = self.asset_root / "runtime-profile.json"
        write_json(self.profile_path, {"profiles": [{"key": "ftkmf_modeltest_example", "baseEnemy": "example"}]})
        write_json(self.asset_root / "manifest.json", {"originalAssets": {}})
        (self.asset_root / "example.glb").write_bytes(b"glTF fixture")
        (self.asset_root / "palette.png").write_bytes(PNG)
        self.plan_path = self.asset_root / "archive-plan.json"
        self.write_plan()

    def write_plan(self, output: str = "art-experiments/example/live-validation-v1") -> None:
        write_json(self.plan_path, {
            "schema": "ftkmf.model-validation-archive-plan.v1",
            "output": output,
            "caseResult": str(self.case_path.relative_to(self.root)),
            "visualReview": str(self.review_path.relative_to(self.root)),
            "assetFiles": [
                "art-experiments/example/example.glb",
                "art-experiments/example/palette.png",
            ],
            "metadata": [
                {"path": "art-experiments/example/runtime-profile.json", "pinAs": "runtimeProfile"},
                {"path": "art-experiments/example/manifest.json", "pinAs": "assetManifest"},
            ],
            "captures": {
                "pass": {
                    "label": "idle-and-native-attack",
                    "scope": "Fixture idle and native attack observation.",
                    "frameIdentity": {
                        "celRelativeRendererPath": "body",
                        "mesh": "ftkmf_glb_example.glb",
                        "boneSignature": "bones",
                    },
                },
            },
            "validation": {
                "status": "reviewed_fixture",
                "revision": "V1",
                "displayName": "Fixture model",
                "sourceKind": "native_enemy_row",
                "nativeChassis": "example",
                "binding": {
                    "rendererPath": "body",
                    "mesh": "ftkmf_glb_example.glb",
                    "boneSignature": "bones",
                },
                "limits": ["Fixture only."],
            },
        })


class ArchiveModelValidationCaseTests(unittest.TestCase):
    def test_builds_a_generic_integrity_checked_archive(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchivePlanFixture(Path(temporary))
            report = archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
            archive = fixture.root / report["archive"]
            validation = json.loads((archive / "validation.json").read_text())
            integrity = verifier.verify_archive(archive)
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(validation["profile"]["key"], "ftkmf_modeltest_example")
        self.assertEqual(validation["captures"][0]["label"], "idle-and-native-attack")
        self.assertEqual(integrity["status"], "PASS")
        self.assertEqual(integrity["selectedPNGs"], 1)
        self.assertTrue(integrity["rootVisualReviewPinned"])

    def test_refuses_to_overwrite_a_completed_archive(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchivePlanFixture(Path(temporary))
            archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
            with self.assertRaisesRegex(archiver.ArchiveBuildError, "will not be overwritten"):
                archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)

    def test_rejects_a_changed_runtime_frame_identity_before_output(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchivePlanFixture(Path(temporary))
            raw = json.loads(fixture.raw_capture.read_text())
            raw["frames"][1]["mesh"] = "unexpected.glb"
            write_json(fixture.raw_capture, raw)
            with self.assertRaisesRegex(archiver.ArchiveBuildError, "changed 'mesh'"):
                archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
            self.assertFalse((fixture.root / "art-experiments/example/live-validation-v1").exists())

    def test_rejects_a_metadata_pin_that_would_replace_validation_status(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchivePlanFixture(Path(temporary))
            plan = json.loads(fixture.plan_path.read_text())
            plan["metadata"][0]["pinAs"] = "status"
            write_json(fixture.plan_path, plan)
            with self.assertRaisesRegex(archiver.ArchiveBuildError, "invalid or reserved"):
                archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)

    def test_bounded_attack_attempts_receive_distinct_archive_keys_and_select_the_damage_hit(self) -> None:
        actions = archiver.action_map({"actions": [
            {"action": "attack", "attempt": 1, "actionAccepted": True,
             "hpOutcome": {"status": "no_hp_loss_unclassified"}},
            {"action": "attack", "attempt": 2, "actionAccepted": True,
             "hpOutcome": {"status": "nonlethal_hp_loss", "beforeHp": 9, "afterHp": 4}},
            {"action": "pass", "actionAccepted": True},
        ]})
        self.assertEqual(sorted(actions), ["attack-attempt-1", "attack-attempt-2", "pass"])
        key, action = archiver.ordinary_attack(actions) or (None, None)
        self.assertEqual(key, "attack-attempt-2")
        self.assertEqual(action["hpOutcome"]["afterHp"], 4)

    def test_repeated_action_without_attempt_is_rejected(self) -> None:
        with self.assertRaisesRegex(archiver.ArchiveBuildError, "positive integer attempt"):
            archiver.action_map({"actions": [
                {"action": "attack", "actionAccepted": True},
                {"action": "attack", "actionAccepted": True},
            ]})

    def test_action_window_hp_precedes_later_ready_state_drift(self) -> None:
        fid = {"turnIndex": 0, "photonId": -1}
        def state(hp: int) -> dict[str, object]:
            return {"combat": {"enemies": [{
                "type": "ftkmf_modeltest_example", "fid": fid, "hp": hp,
            }]}}
        outcome = archiver.action_window_hp_outcome({
            "actionResult": {"result": {"target": fid}},
            "before": state(58),
            "after": state(53),
            "hpOutcome": {
                "status": "nonlethal_hp_loss", "beforeHp": 58, "afterHp": 45,
                "nativeOutcome": {"attackResponse": "Damaged", "damage": 5},
            },
        }, "ftkmf_modeltest_example")
        self.assertEqual((outcome["beforeHp"], outcome["afterHp"]), (58, 53))
        self.assertEqual(outcome["laterReadyObservedHp"], 45)
        self.assertEqual(outcome["nativeOutcome"]["damage"], 5)


if __name__ == "__main__":
    unittest.main()
