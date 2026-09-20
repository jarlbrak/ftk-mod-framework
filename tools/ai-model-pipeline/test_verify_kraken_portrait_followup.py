import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import verify_kraken_portrait_followup as verifier
from local_inputs import evidence_paths, skip_without_local_evidence


# Every test runs the verifier, which pins the built framework DLL and live
# capture files before it reaches the mutated field under test.
@skip_without_local_evidence(*evidence_paths(verifier.DEFAULT))
class KrakenPortraitFollowupTests(unittest.TestCase):
    def test_current_evidence(self):
        result = verifier.verify()
        self.assertTrue(result["ok"])
        self.assertEqual(result["activeHudImages"], 6)

    def test_rejects_unreviewed_pixels(self):
        data = json.loads(verifier.DEFAULT.read_text())
        changed = copy.deepcopy(data)
        changed["portrait"]["review"]["status"] = "pending"
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / "validation.json"
            candidate.write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "manual review"):
                verifier.verify(candidate)

    def test_rejects_framework_drift(self):
        data = json.loads(verifier.DEFAULT.read_text())
        changed = copy.deepcopy(data)
        changed["framework"]["sha256"] = "0" * 64
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / "validation.json"
            candidate.write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "framework hash mismatch"):
                verifier.verify(candidate)


if __name__ == "__main__":
    unittest.main()
