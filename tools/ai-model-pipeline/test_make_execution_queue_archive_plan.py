from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import archive_model_validation_case as archiver
import make_execution_queue_archive_plan as draft
import prepare_model_visual_review as review_template


PNG = b"\x89PNG\r\n\x1a\nfixture-png"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


class QueueArchivePlanFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        (root / "FTKModFramework").mkdir()
        (root / "tools").mkdir()
        self.package = root / "art-experiments/example"
        self.package.mkdir(parents=True)
        self.profile = {
            "key": "ftkmf_modeltest_example",
            "baseEnemy": "exampleA",
            "displayName": "Example",
            "combatProfile": "a" * 64,
            "renderers": [{"rendererPath": "body", "glbFile": "example.glb", "textureFile": "palette.png"}],
        }
        self.profile_path = self.package / "runtime-profile.json"
        write_json(self.profile_path, {"version": 1, "profiles": [self.profile]})
        self.model = self.package / "example.glb"
        self.palette = self.package / "palette.png"
        self.model.write_bytes(b"original-glb")
        self.palette.write_bytes(PNG)
        self.case_root = root / "scratch/case"
        frame_dir = self.case_root / "pass"
        frame_dir.mkdir(parents=True)
        frame = frame_dir / "0000.png"
        frame.write_bytes(PNG)
        raw_capture = self.case_root / "pass.json"
        write_json(raw_capture, {
            "ok": True, "error": None, "width": 2, "height": 2, "timingMode": "fixed-step",
            "frames": [{"celRelativeRendererPath": "body", "mesh": "ftkmf_glb_example.glb", "boneSignature": "bones"}],
        })
        write_json(self.case_root / "pass-result.json", {"ok": True})
        write_json(self.case_root / "pass-journal.json", {"events": []})
        self.case_path = self.case_root / "case-result.json"
        write_json(self.case_path, {
            "session": "session-1",
            "enemy": self.profile["key"],
            "profileSha256": "c" * 64,
            "status": "needs_visual_review",
            "initialRenderer": {"celRelativeRendererPath": "body", "mesh": "ftkmf_glb_example.glb", "boneSignature": "bones"},
            "selectedFrames": [{
                "path": "scratch/case/pass/0000.png", "sha256": sha(frame), "action": "pass", "index": 0,
            }],
            "actions": [{
                "action": "pass", "actionAccepted": True,
                "capture": {"rawCapture": {"path": str(raw_capture.relative_to(root))},
                            "boundary": {"completeCapture": True, "termination": "complete"}},
                "rawResult": {"path": "scratch/case/pass-result.json"},
                "journal": {"path": "scratch/case/pass-journal.json"},
            }],
        })
        self.review_path = self.case_root / "visual-review.json"
        write_json(self.review_path, {
            "session": "session-1",
            "reviewStatus": "reviewed_fixture",
            "binding": {"rendererPath": "body", "mesh": "ftkmf_glb_example.glb", "boneSignature": "bones"},
            "frames": [{"path": "scratch/case/pass/0000.png", "sha256": sha(frame), "action": "pass", "index": 0}],
        })
        write_json(root / "scratch/queue.json", {"routes": []})
        write_json(root / "scratch/stage.json", {"revisions": []})
        write_json(self.case_root / "stage-result.json", {"status": "binding_metadata_observed"})
        (self.case_root / "journal.jsonl").write_text('{"kind":"inventory","data":{}}\n')
        self.record_path = root / "scratch/example-run.json"
        write_json(self.record_path, {
            "status": "needs_visual_review",
            "needsManualVisualReview": True,
            "needsImmutableArchive": True,
            "session": "session-1",
            "caseResult": "scratch/case/case-result.json",
            "stageResult": "scratch/case/stage-result.json",
            "plan": {
                "profile": {
                    "key": self.profile["key"],
                    "document": {"path": "art-experiments/example/runtime-profile.json", "sha256": sha(self.profile_path)},
                },
                "catalog": {"path": "scratch/game/model-test-profiles.json", "sha256": "c" * 64},
                "sourceAssignments": [{
                    "sourceKind": "native_enemy_row", "nativeEnemy": "exampleA", "resourcePrefab": None,
                    "rendererPath": "body", "sourceRendererId": 123,
                }],
                "motionRendererPath": "body",
                "assets": [{"name": "example.glb", "sha256": sha(self.model)},
                           {"name": "palette.png", "sha256": sha(self.palette)}],
                "queue": {"path": "scratch/queue.json", "sha256": sha(root / "scratch/queue.json")},
                "stageReadiness": {"path": "scratch/stage.json", "sha256": sha(root / "scratch/stage.json")},
            },
        })
        self.plan_path = root / "art-experiments/example/live-validation-v1-plan.json"
        self.archive_output = "art-experiments/example/live-validation-v1"


class MakeExecutionQueueArchivePlanTests(unittest.TestCase):
    def test_creates_a_buildable_exact_archive_plan(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = QueueArchivePlanFixture(Path(temporary))
            template_path = fixture.root / "scratch/review-template.json"
            template = review_template.build_template(
                fixture.case_path, template_path, workspace_root=fixture.root
            )
            reviewed = json.loads(template_path.read_text())
            reviewed["reviewStatus"] = "reviewed_fixture"
            reviewed["frames"][0]["observation"] = "The original fixture frame is visible."
            write_json(template_path, reviewed)
            result = draft.build_plan(
                fixture.record_path, template_path, fixture.plan_path, fixture.archive_output,
                "V1", ["Fixture scope only."], workspace_root=fixture.root,
            )
            plan = json.loads(fixture.plan_path.read_text())
            archive = archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
        self.assertEqual(template["status"], "TEMPLATE_CREATED")
        self.assertEqual(result["status"], "PLAN_CREATED")
        self.assertEqual(plan["validation"]["nativeChassis"], "exampleA")
        self.assertEqual(plan["captures"]["pass"]["frameIdentity"]["celRelativeRendererPath"], "body")
        self.assertIn({"path": "scratch/case/journal.jsonl", "pinAs": "stageBindingJournal"}, plan["metadata"])
        self.assertEqual(archive["status"], "PASS")

    def test_rejects_an_asset_changed_after_the_runner_record(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = QueueArchivePlanFixture(Path(temporary))
            fixture.model.write_bytes(b"changed")
            with self.assertRaisesRegex(draft.PlanDraftError, "declared source asset changed"):
                draft.build_plan(
                    fixture.record_path, fixture.review_path, fixture.plan_path, fixture.archive_output,
                    "V1", ["Fixture scope only."], workspace_root=fixture.root,
                )

    def test_rejects_a_pending_review_template(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = QueueArchivePlanFixture(Path(temporary))
            pending = json.loads(fixture.review_path.read_text())
            pending["reviewStatus"] = "pending_manual_root_review"
            write_json(fixture.review_path, pending)
            with self.assertRaisesRegex(draft.PlanDraftError, "reviewed status"):
                draft.build_plan(
                    fixture.record_path, fixture.review_path, fixture.plan_path, fixture.archive_output,
                    "V1", ["Fixture scope only."], workspace_root=fixture.root,
                )


if __name__ == "__main__":
    unittest.main()
