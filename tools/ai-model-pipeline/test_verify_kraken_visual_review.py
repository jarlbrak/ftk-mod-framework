import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import verify_kraken_visual_review as verifier


class KrakenVisualReviewTests(unittest.TestCase):
    def test_current_review_and_raw_artifacts(self):
        result = verifier.verify()
        self.assertTrue(result["ok"])
        self.assertEqual(result["captureCount"], 3)
        self.assertEqual(result["selectedFrameCount"], 22)
        self.assertEqual(result["portrait"], "fail")

    def test_rejects_changed_capture_hash(self):
        data = json.loads(verifier.DEFAULT_REVIEW.read_text())
        changed = copy.deepcopy(data)
        changed["captures"][0]["sha256"] = "0" * 64
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "review.json"
            path.write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "capture hash mismatch"):
                verifier.verify(path)

    def test_rejects_claim_that_failed_portrait_passed(self):
        data = json.loads(verifier.DEFAULT_REVIEW.read_text())
        changed = copy.deepcopy(data)
        changed["review"]["portrait"]["status"] = "pass"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "review.json"
            path.write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "pre-fix portrait result changed"):
                verifier.verify(path)


if __name__ == "__main__":
    unittest.main()
