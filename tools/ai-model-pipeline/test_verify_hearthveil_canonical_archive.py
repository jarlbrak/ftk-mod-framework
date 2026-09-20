import gzip
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import verify_hearthveil_canonical_archive as verifier
from local_inputs import integrity_files, skip_without_local_evidence


@skip_without_local_evidence(*integrity_files(verifier.DEFAULT))
class HearthveilCanonicalArchiveTests(unittest.TestCase):
    def copy_archive(self, directory: str) -> Path:
        candidate = Path(directory) / "archive"
        shutil.copytree(verifier.DEFAULT, candidate)
        return candidate

    def test_current_archive(self):
        result = verifier.verify()
        self.assertTrue(result["ok"])
        self.assertEqual(result["topologyGroups"], ["4082b6c4e777f922", "85c742f628ea6d37"])
        self.assertFalse(result["ordinaryPlayerDeath"])

    def test_rejects_archive_file_drift(self):
        with tempfile.TemporaryDirectory() as directory:
            candidate = self.copy_archive(directory)
            frame = candidate / "selected/idle-body-0012.png"
            frame.write_bytes(frame.read_bytes() + b"drift")
            with self.assertRaisesRegex(ValueError, "archive file changed"):
                verifier.verify(candidate)

    def test_rejects_death_fixture_reclassified_as_ordinary(self):
        with tempfile.TemporaryDirectory() as directory:
            candidate = self.copy_archive(directory)
            path = candidate / "validation.json"
            value = json.loads(path.read_text())
            value["explicitDeathFixture"]["ordinaryPlayerDeath"] = True
            path.write_text(json.dumps(value, indent=2) + "\n")
            integrity = json.loads((candidate / "integrity.json").read_text())
            integrity["files"]["validation.json"] = verifier.sha256(path)
            (candidate / "integrity.json").write_text(json.dumps(integrity, indent=2) + "\n")
            with self.assertRaisesRegex(ValueError, "death fixture boundary changed"):
                verifier.verify(candidate)

    def test_rejects_capture_identity_drift_with_resealed_integrity(self):
        with tempfile.TemporaryDirectory() as directory:
            candidate = self.copy_archive(directory)
            path = candidate / "metadata/death-hair.json.gz"
            with gzip.open(path, "rt") as stream:
                value = json.load(stream)
            value["frames"][7]["mesh"] = "wrong.glb"
            with gzip.open(path, "wt") as stream:
                json.dump(value, stream)
            integrity = json.loads((candidate / "integrity.json").read_text())
            integrity["files"]["metadata/death-hair.json.gz"] = verifier.sha256(path)
            (candidate / "integrity.json").write_text(json.dumps(integrity, indent=2) + "\n")
            with self.assertRaisesRegex(ValueError, "mesh or bone signature changed"):
                verifier.verify(candidate)


if __name__ == "__main__":
    unittest.main()
