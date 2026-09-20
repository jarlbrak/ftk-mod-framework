from __future__ import annotations

from pathlib import Path
import unittest

import verify_model_validation_archive as verifier
from local_inputs import lossless_archive_files, skip_without_local_evidence


ROOT = Path(__file__).resolve().parents[2]
CANONICAL_ARCHIVES = (
    "art-experiments/mireglass-croaker/live-validation-v1",
    "art-experiments/lichenfang-basey-wolf/live-validation-v1",
    "art-experiments/sablevine-basey-snake/live-validation-v1",
    "art-experiments/sablevine-basey-snake/live-validation-v2-scale055",
    "art-experiments/rivenquill-basey-cockatrice/live-validation-v1-boss",
    "art-experiments/rivenquill-basey-cockatrice/live-validation-v1-small",
)


@skip_without_local_evidence(*(path for archive in CANONICAL_ARCHIVES for path in lossless_archive_files(archive)))
class CurrentCanonicalArchiveTests(unittest.TestCase):
    def test_current_canonical_archives_have_intact_artifact_evidence(self) -> None:
        for relative in CANONICAL_ARCHIVES:
            with self.subTest(archive=relative):
                report = verifier.verify_archive(ROOT / relative)
                self.assertEqual(report["status"], "PASS")
                self.assertGreater(report["metadataMappings"], 0)


if __name__ == "__main__":
    unittest.main()
