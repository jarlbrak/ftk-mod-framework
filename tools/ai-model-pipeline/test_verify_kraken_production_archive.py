from pathlib import Path
import shutil
import tempfile
import unittest

import verify_kraken_production_archive as archive
from local_inputs import integrity_files, skip_without_local_evidence


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/evidence/kraken-production-adapter-v1"


@skip_without_local_evidence(*integrity_files(SOURCE))
class KrakenProductionArchiveTests(unittest.TestCase):
    def test_committed_archive_reproduces_the_verified_campaign(self) -> None:
        result = archive.verify_archive(SOURCE)
        self.assertEqual(
            result["status"],
            "production_observation_campaign_satisfied_archive_verified",
        )
        self.assertEqual(result["reportCount"], 2)
        self.assertEqual(len(result["sessions"]), 2)
        self.assertEqual(len(set(result["sessions"])), 2)

    def test_changed_compressed_report_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            copied = Path(temporary) / "archive"
            shutil.copytree(SOURCE, copied)
            report = copied / "native-combat-death.json.gz"
            report.write_bytes(report.read_bytes() + b"changed")
            with self.assertRaisesRegex(ValueError, "Integrity file digest mismatch"):
                archive.verify_archive(copied)


if __name__ == "__main__":
    unittest.main()
