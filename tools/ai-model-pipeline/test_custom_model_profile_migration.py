from __future__ import annotations

import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


TOOL_DIR = Path(__file__).resolve().parent
STAGE = TOOL_DIR / "stage_custom_model_profile.py"
DEPLOY = TOOL_DIR / "deploy_custom_model_stage.py"
SCHEMA = TOOL_DIR / "runtime-test-content/profiles.schema.json"


def profile(*, key: str = "ftkmf_modeltest_example", scale: float | None = None) -> dict:
    value = {
        "key": key,
        "baseEnemy": "exampleA",
        "displayName": "Example",
        "combatProfile": "a" * 64,
        "renderers": [{
            "rendererPath": "body",
            "glbFile": "example.glb",
            "textureFile": "example.png",
        }],
    }
    if scale is not None:
        value["visualScale"] = scale
    return value


class CustomModelProfileMigrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "FTKModFramework").mkdir()
        schema_dir = self.root / "tools/ai-model-pipeline/runtime-test-content"
        schema_dir.mkdir(parents=True)
        shutil.copy2(SCHEMA, schema_dir / "profiles.schema.json")
        self.game = self.root / "scratch/game"
        models = self.game / "BepInEx/plugins/FTKModFramework_content/models"
        models.mkdir(parents=True)
        self.old = profile(scale=0.5)
        (self.game / "model-test-profiles.json").write_text(
            json.dumps({"version": 1, "profiles": [self.old]}, indent=2) + "\n"
        )
        self.package = self.root / "art-experiments/example"
        self.package.mkdir(parents=True)
        self.new = profile(scale=1.0)
        (self.package / "runtime-profile.json").write_text(
            json.dumps({"version": 1, "profiles": [self.new]}, indent=2) + "\n"
        )
        for name in ("example.glb", "example.png"):
            contents = ("original-" + name).encode()
            (self.package / name).write_bytes(contents)
            (models / name).write_bytes(contents)
        self.stage = self.root / "scratch/example-stage"

    def command(self, script: Path, *extra: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(script), "--repo", str(self.root), *extra],
            text=True,
            capture_output=True,
            check=False,
        )

    def stage_args(self, *extra: str) -> list[str]:
        return [
            "--game-root", str(self.game),
            "--profile", str(self.package / "runtime-profile.json"),
            "--asset-dir", str(self.package),
            "--output", str(self.stage),
            *extra,
        ]

    def test_changed_existing_row_requires_explicit_migration_flag(self) -> None:
        result = self.command(STAGE, *self.stage_args())
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("replace-existing-profile", result.stderr + result.stdout)
        catalog = json.loads((self.game / "model-test-profiles.json").read_text())
        self.assertEqual(catalog["profiles"], [self.old])

    def test_explicit_migration_is_pinned_and_deployable(self) -> None:
        staged = self.command(STAGE, *self.stage_args("--replace-existing-profile"))
        self.assertEqual(staged.returncode, 0, staged.stderr)
        receipt = json.loads((self.stage / "receipt.json").read_text())
        self.assertEqual(receipt["appendedProfileKeys"], [])
        self.assertEqual(receipt["replacedProfileKeys"], [self.new["key"]])
        self.assertEqual(receipt["profileReplacements"][0]["index"], 0)
        self.assertTrue(receipt["preservedCatalogRowsOutsideReplacements"])
        self.assertEqual(json.loads((self.game / "model-test-profiles.json").read_text())["profiles"], [self.old])

        review = self.command(
            DEPLOY,
            "--game-root", str(self.game),
            "--stage", str(self.stage),
        )
        self.assertEqual(review.returncode, 0, review.stderr)
        review_payload = json.loads(review.stdout)
        self.assertEqual(review_payload["replacedProfileKeys"], [self.new["key"]])
        self.assertEqual(json.loads((self.game / "model-test-profiles.json").read_text())["profiles"], [self.old])

        deployed = self.command(
            DEPLOY,
            "--game-root", str(self.game),
            "--stage", str(self.stage),
            "--label", "example-migration",
            "--execute",
        )
        self.assertEqual(deployed.returncode, 0, deployed.stderr)
        self.assertEqual(json.loads((self.game / "model-test-profiles.json").read_text())["profiles"], [self.new])

    def test_absent_profile_keeps_the_append_transaction(self) -> None:
        added = profile(key="ftkmf_modeltest_added", scale=1.0)
        (self.package / "runtime-profile.json").write_text(
            json.dumps({"version": 1, "profiles": [added]}, indent=2) + "\n"
        )
        staged = self.command(STAGE, *self.stage_args())
        self.assertEqual(staged.returncode, 0, staged.stderr)
        receipt = json.loads((self.stage / "receipt.json").read_text())
        self.assertEqual(receipt["appendedProfileKeys"], [added["key"]])
        self.assertEqual(receipt["replacedProfileKeys"], [])
        deployed = self.command(
            DEPLOY,
            "--game-root", str(self.game),
            "--stage", str(self.stage),
            "--label", "example-append",
            "--execute",
        )
        self.assertEqual(deployed.returncode, 0, deployed.stderr)
        self.assertEqual(
            json.loads((self.game / "model-test-profiles.json").read_text())["profiles"],
            [self.old, added],
        )


if __name__ == "__main__":
    unittest.main()
