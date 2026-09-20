from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import struct
import tempfile
import unittest
import zlib

import archive_player_model_validation as archiver
import audit_model_validation_gates as gates
import verify_model_validation_archive as verifier


def png_chunk(kind: bytes, content: bytes) -> bytes:
    return (
        struct.pack(">I", len(content))
        + kind
        + content
        + struct.pack(">I", zlib.crc32(kind + content) & 0xFFFFFFFF)
    )


PNG = (
    b"\x89PNG\r\n\x1a\n"
    + png_chunk(b"IHDR", struct.pack(">IIBBBBB", 2, 2, 8, 6, 0, 0, 0))
    + png_chunk(b"IDAT", zlib.compress((b"\x00" + b"\x60\xa0\x40\xff" * 2) * 2))
    + png_chunk(b"IEND", b"")
)


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


class PlayerArchivePlanFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        (root / "FTKModFramework").mkdir()
        self.asset_root = root / "art-experiments" / "player-example"
        self.capture_root = root / "scratch" / "preview"
        self.asset_root.mkdir(parents=True)
        self.capture_root.mkdir(parents=True)
        frames = self.capture_root / "idle"
        frames.mkdir()
        for index in range(2):
            (frames / f"{index:04d}.png").write_bytes(PNG)
        self.capture_path = self.capture_root / "idle.json"
        self.identity = {
            "ownerKind": "player-preview",
            "ownerInstanceId": 7,
            "celInstanceId": 8,
            "instanceId": 9,
            "celRelativeRendererPath": "body",
            "mesh": "ftkmf_glb_player-example.glb",
            "boneSignature": "player-bones",
            "active": True,
            "enabled": True,
            "isVisible": True,
        }
        write_json(self.capture_path, {
            "session": "player-session-1",
            "ok": True,
            "timingMode": "offline-fixed-step-gameplay",
            "requestedFps": 12,
            "frames": [{
                **self.identity,
                "animator": {"layers": [{"playing": [{"name": "standardIdle_handsDown"}]}]},
            }, {
                **self.identity,
                "animator": {"layers": [{"playing": [{"name": "standardIdle_handsDown"}]}]},
            }],
        })
        self.profile = {
            "key": "ftkmf_modeltest_player_example",
            "baseClass": "blacksmith",
            "displayName": "Player fixture",
            "skinset": "blacksmith_Female",
            "defaultSkinType": "Female",
            "renderers": [{"rendererPath": "body"}],
        }
        self.profile_path = self.asset_root / "runtime-profile.json"
        write_json(self.profile_path, {"profiles": [self.profile]})
        (self.asset_root / "player-example.glb").write_bytes(b"glTF player fixture")
        (self.asset_root / "palette.png").write_bytes(PNG)
        self.review_path = self.capture_root / "visual-review.json"
        write_json(self.review_path, {
            "session": "player-session-1",
            "reviewStatus": "reviewed_native_preview_fixture",
            "binding": {
                "ownerKind": "player-preview",
                "ownerInstanceId": 7,
                "celInstanceId": 8,
                "rendererPath": "body",
                "mesh": "ftkmf_glb_player-example.glb",
                "boneSignature": "player-bones",
            },
            "frames": [{
                "path": "scratch/preview/idle/0000.png",
                "sha256": sha256(PNG),
                "label": "native-preview-idle",
                "index": 0,
                "observation": "The player fixture stays visible on the native preview.",
            }],
        })
        self.plan_path = self.asset_root / "archive-plan.json"
        self.write_plan()

    def write_plan(self) -> None:
        write_json(self.plan_path, {
            "schema": "ftkmf.player-model-validation-archive-plan.v1",
            "output": "art-experiments/player-example/live-validation-v1",
            "session": "player-session-1",
            "visualReview": "scratch/preview/visual-review.json",
            "assetFiles": [
                "art-experiments/player-example/player-example.glb",
                "art-experiments/player-example/palette.png",
            ],
            "metadata": [{"path": "art-experiments/player-example/runtime-profile.json", "pinAs": "runtimeProfile"}],
            "captures": [{
                "label": "native-preview-idle",
                "rawCapture": "scratch/preview/idle.json",
                "scope": "Native player-preview idle capture.",
                "requiredClips": ["standardIdle_handsDown"],
                "frameIdentity": self.identity,
            }],
            "validation": {
                "status": "reviewed_native_preview_fixture",
                "revision": "V1",
                "displayName": "Player fixture",
                "profile": self.profile,
                "binding": {
                    "ownerKind": "player-preview",
                    "ownerInstanceId": 7,
                    "celInstanceId": 8,
                    "rendererPath": "body",
                    "mesh": "ftkmf_glb_player-example.glb",
                    "boneSignature": "player-bones",
                },
                "avatarOwners": {"preview": {"observedAvatars": 1, "ownerInstanceId": 7}},
                "limits": ["Player preview fixture only."],
            },
        })


class ArchivePlayerModelValidationTests(unittest.TestCase):
    def test_builds_a_player_archive_with_explicit_preview_idle_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = PlayerArchivePlanFixture(Path(temporary))
            report = archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
            archive = fixture.root / report["archive"]
            validation = json.loads((archive / "validation.json").read_text())
            integrity = verifier.verify_archive(archive)
            evidence = gates.gate_evidence(validation, None, player=True)
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(integrity["status"], "PASS")
        self.assertTrue(integrity["rootVisualReviewPinned"])
        self.assertTrue(evidence["idle"]["motionCaptureRecorded"])
        self.assertTrue(evidence["playerPreview"]["previewAvatarObserved"])

    def test_rejects_capture_when_the_required_idle_clip_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = PlayerArchivePlanFixture(Path(temporary))
            capture = json.loads(fixture.capture_path.read_text())
            capture["frames"][1]["animator"] = {"layers": []}
            write_json(fixture.capture_path, capture)
            with self.assertRaisesRegex(archiver.ArchiveBuildError, "lacks a required native clip"):
                archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=False)
            self.assertFalse((fixture.root / "art-experiments/player-example/live-validation-v1").exists())

    @unittest.skipUnless(shutil.which("ffmpeg") and shutil.which("ffprobe"),
                         "ffmpeg and ffprobe are required to render the presentation video")
    def test_renders_a_verified_presentation_video_when_enabled(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = PlayerArchivePlanFixture(Path(temporary))
            report = archiver.build_archive(fixture.plan_path, workspace_root=fixture.root, render_videos=True)
            integrity = verifier.verify_archive(fixture.root / report["archive"], check_video_metadata=True)
        self.assertEqual(integrity["videos"], 1)
        self.assertTrue(integrity["videoMetadataChecked"])


if __name__ == "__main__":
    unittest.main()
