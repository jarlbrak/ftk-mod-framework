from __future__ import annotations

from pathlib import Path
import unittest

import verify_model_validation_archive as verifier


ROOT = Path(__file__).resolve().parents[2]
PLAYER_ARCHIVES = (
    "art-experiments/hearthveil-blacksmith/live-validation-v2",
    "art-experiments/wildbloom-herbalist/live-validation-v1",
    "art-experiments/tideglass-fishsmith/live-validation-v2",
)


class CurrentPlayerArchiveTests(unittest.TestCase):
    def test_representative_player_archives_have_intact_artifact_evidence(self) -> None:
        for relative in PLAYER_ARCHIVES:
            with self.subTest(archive=relative):
                report = verifier.verify_archive(ROOT / relative)
                self.assertEqual(report["status"], "PASS")
                self.assertGreater(report["metadataMappings"], 0)


if __name__ == "__main__":
    unittest.main()
