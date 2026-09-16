from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import verify_model_validation_archive as verifier


PNG = b"\x89PNG\r\n\x1a\nfixture-png"


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


class ArchiveFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.metadata_raw = b'{"schema":"fixture"}\n'
        self.metadata = root / "metadata" / "art-experiments" / "example" / "manifest.json.gz"
        self.metadata.parent.mkdir(parents=True)
        self.metadata.write_bytes(gzip.compress(self.metadata_raw, mtime=0))
        self.selected = root / "selected" / "pass-0000.png"
        self.selected.parent.mkdir(parents=True)
        self.selected.write_bytes(PNG)
        self.write_validation()

    def write_validation(self, *, mapping_source: str = "art-experiments/example/manifest.json") -> None:
        source_hash = sha256(self.metadata_raw)
        png_hash = sha256(PNG)
        (self.root / "source-image-pins.json").write_text(json.dumps({"scratch/case/pass/0000.png": png_hash}))
        (self.root / "capture-image-pins.json").write_text(json.dumps({"scratch/case/pass/0000.png": png_hash}))
        (self.root / "asset-pins.json").write_text(json.dumps({"example.glb": "a" * 64}))
        validation = {
            "losslessMappings": [{
                "source": mapping_source,
                "sourceSha256": source_hash,
                "archive": str(self.metadata.relative_to(self.root)),
                "archiveSha256": sha256(self.metadata.read_bytes()),
                "encoding": "gzip-lossless",
                "bytes": len(self.metadata_raw),
            }],
            "sourceImagePins": "source-image-pins.json",
            "captureImagePins": "capture-image-pins.json",
            "assetPins": "asset-pins.json",
            "sourceImageCount": 1,
            "captureImageCount": 1,
            "assetHashes": {"example.glb": "a" * 64},
            "selectedPNGs": [{
                "source": "scratch/case/pass/0000.png",
                "archive": str(self.selected.relative_to(self.root)),
                "sha256": sha256(PNG),
            }],
        }
        (self.root / "validation.json").write_text(json.dumps(validation))


class VerifyArchiveTests(unittest.TestCase):
    def test_valid_archive_reports_integrity_counts(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(report["metadataMappings"], 1)
        self.assertEqual(report["sourceImagePins"], 1)
        self.assertEqual(report["captureImagePins"], 1)
        self.assertEqual(report["selectedPNGs"], 1)
        self.assertFalse(report["videoMetadataChecked"])

    def test_rejects_tampered_lossless_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            fixture.metadata.write_bytes(gzip.compress(b"tampered", mtime=0))
            with self.assertRaisesRegex(verifier.ArchiveIntegrityError, "SHA-256 mismatch"):
                verifier.verify_archive(fixture.root)

    def test_rejects_selected_copy_that_differs_from_pinned_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            changed = PNG + b"changed"
            fixture.selected.write_bytes(changed)
            validation = json.loads((fixture.root / "validation.json").read_text())
            validation["selectedPNGs"][0]["sha256"] = sha256(changed)
            (fixture.root / "validation.json").write_text(json.dumps(validation))
            with self.assertRaisesRegex(verifier.ArchiveIntegrityError, "differs from its pinned source"):
                verifier.verify_archive(fixture.root)

    def test_rejects_game_payload_in_lossless_mapping(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            fixture.write_validation(mapping_source="scratch/game/Assembly-CSharp.dll")
            with self.assertRaisesRegex(verifier.ArchiveIntegrityError, "forbidden game payload suffix"):
                verifier.verify_archive(fixture.root)

    def test_reads_a_hash_pinned_legacy_integrity_sidecar(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            validation_path = fixture.root / "validation.json"
            validation = json.loads(validation_path.read_text())
            integrity = {
                "validation": {"path": "validation.json", "sha256": None},
                "metadata": validation.pop("losslessMappings"),
                "sourceImages": {"count": 1, "pins": validation.pop("sourceImagePins")},
                "selected": validation.pop("selectedPNGs"),
                "videos": [],
            }
            validation.pop("captureImagePins")
            validation.pop("captureImageCount")
            validation.pop("assetPins")
            validation_path.write_text(json.dumps(validation))
            integrity["validation"]["sha256"] = verifier.sha256_file(validation_path)
            (fixture.root / "integrity.json").write_text(json.dumps(integrity))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["layout"], "legacy-integrity-sidecar")
        self.assertEqual(report["selectedPNGs"], 1)

    def test_rejects_legacy_sidecar_with_a_stale_validation_pin(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            validation_path = fixture.root / "validation.json"
            validation = json.loads(validation_path.read_text())
            integrity = {
                "validation": {"path": "validation.json", "sha256": "b" * 64},
                "metadata": validation.pop("losslessMappings"),
                "sourceImages": {"count": 1, "pins": validation.pop("sourceImagePins")},
            }
            validation.pop("captureImagePins")
            validation.pop("captureImageCount")
            validation_path.write_text(json.dumps(validation))
            (fixture.root / "integrity.json").write_text(json.dumps(integrity))
            with self.assertRaisesRegex(verifier.ArchiveIntegrityError, "does not pin the current validation"):
                verifier.verify_archive(fixture.root)

    def test_reads_source_image_records_that_preserve_copied_pngs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            copied = fixture.root / "source-images" / "scratch" / "case" / "pass" / "0000.png"
            copied.parent.mkdir(parents=True)
            copied.write_bytes(PNG)
            pins_path = fixture.root / "source-image-pins.json"
            pins_path.write_text(json.dumps({
                "scratch/case/pass/0000.png": {
                    "sha256": sha256(PNG),
                    "bytes": len(PNG),
                    "archive": str(copied.relative_to(fixture.root)),
                },
            }))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["sourceImagePins"], 1)

    def test_rejects_a_tampered_pinned_root_visual_review(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            review = fixture.root / "visual-review.json"
            review.write_text(json.dumps({"reviewed": True}))
            validation = json.loads((fixture.root / "validation.json").read_text())
            validation["rootVisualReview"] = "visual-review.json"
            validation["rootVisualReviewPin"] = {
                "path": "visual-review.json",
                "sha256": sha256(review.read_bytes()),
                "bytes": review.stat().st_size,
            }
            (fixture.root / "validation.json").write_text(json.dumps(validation))
            review.write_text(json.dumps({"reviewed": False}))
            with self.assertRaisesRegex(verifier.ArchiveIntegrityError, "rootVisualReviewPin has a SHA-256 mismatch"):
                verifier.verify_archive(fixture.root)

    def test_ignores_a_legacy_asset_pin_provenance_pointer(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            (fixture.root / "asset-pins.json").write_text(json.dumps({
                "example.glb": "a" * 64,
                "source": "art-experiments/example/manifest.json",
            }))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["assetPins"], 1)

    def test_reads_a_source_image_record_without_an_archived_copy(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            (fixture.root / "source-image-pins.json").write_text(json.dumps({
                "scratch/case/pass/0000.png": {"sha256": sha256(PNG), "bytes": len(PNG)},
            }))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["sourceImagePins"], 1)

    def test_normalizes_a_legacy_singular_presentation_video(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = ArchiveFixture(Path(temporary))
            validation = json.loads((fixture.root / "validation.json").read_text())
            validation["video"] = {"path": "presentation.mp4", "sha256": sha256(b"video")}
            (fixture.root / "presentation.mp4").write_bytes(b"video")
            (fixture.root / "validation.json").write_text(json.dumps(validation))
            report = verifier.verify_archive(fixture.root)
        self.assertEqual(report["videos"], 1)


if __name__ == "__main__":
    unittest.main()
