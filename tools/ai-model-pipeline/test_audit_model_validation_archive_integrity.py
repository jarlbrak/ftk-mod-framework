from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

import audit_model_validation_archive_integrity as audit


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


class ArchiveIntegrityAuditTests(unittest.TestCase):
    def test_reports_verified_and_unverified_archives_without_behavior_claims(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "FTKModFramework").mkdir()
            experiments = root / "art-experiments"
            write_json(experiments / "valid" / "live-validation-v1" / "validation.json", {
                "losslessMappings": [],
                "videos": [],
            })
            write_json(experiments / "invalid" / "live-validation-v1" / "validation.json", {
                "status": "legacy_without_integrity_layout",
            })
            report = audit.build_report(root, experiments)
        self.assertEqual(report["summary"]["archives"], 2)
        self.assertEqual(report["summary"]["artifactIntegrityVerified"], 1)
        self.assertEqual(report["summary"]["integrityUnverified"], 1)
        for record in report["records"]:
            self.assertRegex(str(record.get("validationSha256")), r"^[0-9a-f]{64}$")
        self.assertIn("does not accept binding", audit.markdown(report))

    def test_refuses_to_overwrite_generated_output_without_explicit_flag(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "report.json"
            audit.write_output(path, "first\n", overwrite=False)
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                audit.write_output(path, "second\n", overwrite=False)


if __name__ == "__main__":
    unittest.main()
