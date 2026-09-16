"""Keep the generated archive-integrity hashes tied to current validation bytes."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import unittest

import audit_model_validation_archive_integrity as auditor


ROOT = Path(__file__).resolve().parents[2]


class CurrentArchiveIntegrityAuditTests(unittest.TestCase):
    def test_hearthveil_canonical_archive_is_verified(self) -> None:
        report = auditor.build_report(ROOT, ROOT / "art-experiments")
        record = next(row for row in report["records"] if row["archive"].endswith(
            "hearthveil-blacksmith/live-validation-v3-canonical"))
        self.assertEqual(record["status"], "artifact_integrity_verified")
        self.assertEqual(record["layout"], "canonical-player-route")

    def test_every_indexed_validation_hash_matches_current_bytes(self) -> None:
        report = json.loads((ROOT / "docs/model-validation-archive-integrity.json").read_text())
        records = report.get("records", [])
        self.assertEqual(report.get("summary", {}).get("archives"), len(records))
        for record in records:
            validation = ROOT / record["validation"]
            self.assertTrue(validation.is_file(), validation)
            self.assertEqual(
                hashlib.sha256(validation.read_bytes()).hexdigest(),
                record["validationSha256"],
                validation,
            )


if __name__ == "__main__":
    unittest.main()
