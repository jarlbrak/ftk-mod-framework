import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from local_inputs import integrity_files, skip_without_local_evidence


HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("verify_wildbloom", HERE / "verify_wildbloom_canonical_archive.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class WildbloomCanonicalArchiveTests(unittest.TestCase):
    @skip_without_local_evidence(*integrity_files(MODULE.DEFAULT))
    def test_current_archive(self):
        result = MODULE.verify()
        self.assertTrue(result["ok"])
        self.assertEqual(result["combat"]["enemyHp"], [72, 62])
        self.assertEqual(result["combat"]["heroHp"], [999, 962])
        self.assertFalse(result["ordinaryPlayerDeath"])

    def test_integrity_rejects_untracked_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive = Path(temporary) / "archive"
            archive.mkdir()
            (archive / "validation.json").write_text("{}")
            (archive / "extra").write_text("x")
            (archive / "integrity.json").write_text(json.dumps({"schema": "ftkmf.canonical-archive-integrity.v1", "files": {"validation.json": MODULE.sha256(archive / "validation.json")}}))
            with self.assertRaisesRegex(ValueError, "validation identity|full archive"):
                MODULE.verify(archive)


if __name__ == "__main__":
    unittest.main()
