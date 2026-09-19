from __future__ import annotations

import json
from pathlib import Path
import subprocess
import unittest

from local_inputs import skip_without_local_inputs


ROOT = Path(__file__).resolve().parents[2]


@skip_without_local_inputs(
    "scratch/model-venv/bin/python",
    "scratch/model-authoring-kit-catalog-v53.json",
)
class CurrentModelAuthoringReferenceTests(unittest.TestCase):
    def test_every_catalog_reference_matches_current_native_decode(self) -> None:
        result = subprocess.run(
            [
                str(ROOT / "scratch/model-venv/bin/python"),
                str(ROOT / "tools/ai-model-pipeline/verify_model_authoring_references.py"),
                "--root", str(ROOT),
                "--catalog", "scratch/model-authoring-kit-catalog-v53.json",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
            check=False,
            timeout=120,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(result.stdout)
        self.assertEqual(report["summary"], {"rendererTargets": 9, "verified": 9, "failed": 0})
        self.assertTrue(all(row["status"] == "PASS" and not row["failures"] for row in report["results"]))


if __name__ == "__main__":
    unittest.main()
