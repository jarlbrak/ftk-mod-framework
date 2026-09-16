from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools/ai-model-pipeline/scaffold_model_package.py"
SPEC = importlib.util.spec_from_file_location("scaffold_model_package", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
scaffold = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = scaffold
SPEC.loader.exec_module(scaffold)


class CurrentModelPackageScaffoldTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.catalog_path = ROOT / "scratch/model-authoring-kit-catalog-v54.json"
        cls.catalog = json.loads(cls.catalog_path.read_text())

    def test_current_catalog_builds_complete_player_package_metadata(self) -> None:
        route = scaffold.select_route(self.catalog, "45c7a9b9fb730195", "playerSkinset")
        destination = ROOT / "art-experiments/scaffold-current-test-unused"
        profile, plan, readme = scaffold.build_package(
            ROOT,
            self.catalog_path,
            self.catalog,
            route,
            destination,
            "scaffold-current-test-unused",
            "ftkmf_modeltest_scaffold_current_test_unused",
            "Scaffold Current Test Unused",
            "scaffold-current-test-unused",
        )
        record = profile["profiles"][0]
        self.assertEqual(record["key"], "ftkmf_modeltest_scaffold_current_test_unused")
        self.assertEqual(record["displayName"], "Scaffold Current Test Unused")
        self.assertEqual(
            {row["glbFile"] for row in record["renderers"]},
            {
                "scaffold-current-test-unused-model-01.glb",
                "scaffold-current-test-unused-model-02.glb",
                "scaffold-current-test-unused-model-03.glb",
            },
        )
        self.assertEqual(
            {row["textureFile"] for row in record["renderers"]},
            {
                "scaffold-current-test-unused-texture-01.png",
            },
        )
        self.assertEqual(plan["authoringTargets"]["primaryCount"], 1)
        self.assertEqual(plan["authoringTargets"]["companionCount"], 2)
        self.assertEqual(plan["authoringTargets"]["apparelCount"], 0)
        self.assertEqual(len(plan["authoringTargets"]["rows"]), 3)
        self.assertNotIn("PACKAGE_DIR", json.dumps(plan["commands"]))
        self.assertNotIn("NEW_LABEL", json.dumps(plan["commands"]))
        self.assertIn("scaffold-current-test-unused", readme)
        profile_text = json.dumps(profile, indent=2, allow_nan=False) + "\n"
        self.assertEqual(plan["package"]["runtimeProfile"]["sha256"], scaffold.sha256_text(profile_text))

    def test_player_scaffold_includes_conditional_apparel_rigs(self) -> None:
        route = scaffold.select_route(self.catalog, "45c7a9b9fb730195", "playerSkinset")
        profile, plan, readme = scaffold.build_package(
            ROOT,
            self.catalog_path,
            self.catalog,
            route,
            ROOT / "art-experiments/player-scaffold-current-test-unused",
            "player-scaffold-current-test-unused",
            "ftkmf_modeltest_player_scaffold_current_test_unused",
            "Player Scaffold Current Test Unused",
            "player-scaffold-current-test-unused",
        )
        self.assertEqual(plan["authoringTargets"]["primaryCount"], 1)
        self.assertEqual(plan["authoringTargets"]["companionCount"], 2)
        self.assertEqual(plan["authoringTargets"]["apparelCount"], 0)
        self.assertEqual(len(plan["authoringTargets"]["rows"]), 3)
        self.assertEqual(profile["profiles"][0].get("apparel", []), [])

    def test_write_creates_metadata_only_and_refuses_overwrite(self) -> None:
        route = scaffold.select_route(self.catalog, "45c7a9b9fb730195", "playerSkinset")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "art-experiments").mkdir()
            destination = scaffold.package_destination(root, "metadata-only")
            profile, plan, readme = scaffold.build_package(
                ROOT,
                self.catalog_path,
                self.catalog,
                route,
                destination,
                "metadata-only",
                "ftkmf_modeltest_metadata_only_fixture",
                "Metadata Only Fixture",
                "metadata-only-fixture",
            )
            scaffold.write_package(destination, profile, plan, readme)
            self.assertEqual(
                {path.name for path in destination.iterdir()},
                {"README.md", "authoring-plan.json", "runtime-profile.json"},
            )
            self.assertFalse(any(path.suffix in {".glb", ".png"} for path in destination.iterdir()))
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                scaffold.write_package(destination, profile, plan, readme)

    def test_canonical_adapter_route_is_absent_and_existing_identity_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "expected one exact authoring route"):
            scaffold.select_route(self.catalog, "6a28ac3cf4523c24", "resourcePrefab")
        keys, displays, assets = scaffold.current_identities(ROOT, self.catalog)
        self.assertIn("ftkmf_modeltest_player_hearthveil_blacksmith_female", keys)
        with self.assertRaisesRegex(ValueError, "key already exists"):
            scaffold.validate_identity(
                "ftkmf_modeltest_player_hearthveil_blacksmith_female",
                "Unique Scaffold Display Fixture",
                "unique-scaffold-fixture",
                keys,
                displays,
                assets,
            )

    def test_destination_and_identity_syntax_are_strict(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "art-experiments").mkdir()
            self.assertEqual(
                scaffold.package_destination(root, "valid-package"),
                (root / "art-experiments/valid-package").resolve(),
            )
            with self.assertRaisesRegex(ValueError, "package slug"):
                scaffold.package_destination(root, "../escape")
        with self.assertRaisesRegex(ValueError, "key must start"):
            scaffold.validate_identity("bad", "Valid", "valid-prefix", set(), set(), set())
        with self.assertRaisesRegex(ValueError, "asset prefix collides"):
            scaffold.validate_identity(
                "ftkmf_modeltest_valid_fixture",
                "Valid Fixture",
                "taken",
                set(),
                set(),
                {"taken-model-01.glb"},
            )


if __name__ == "__main__":
    unittest.main()
