import copy
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import verify_kraken_canonical_archive as verifier


class KrakenCanonicalArchiveTests(unittest.TestCase):
    def test_current_archive(self):
        result = verifier.verify()
        self.assertTrue(result["ok"])
        self.assertEqual(result["selectedPNGs"], 23)
        self.assertEqual(result["portrait"], "pass")

    def test_rejects_archive_file_drift(self):
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / "archive"
            shutil.copytree(verifier.DEFAULT, candidate)
            portrait = candidate / "portrait-followup/portrait.png"
            portrait.write_bytes(portrait.read_bytes() + b"drift")
            with self.assertRaisesRegex(ValueError, "archive file changed|SHA-256 mismatch"):
                verifier.verify(candidate)


if __name__ == "__main__":
    unittest.main()
