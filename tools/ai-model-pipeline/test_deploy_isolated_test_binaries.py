#!/usr/bin/env python3
"""Offline checks for the reversible isolated runtime-binary deployer."""
from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).with_name("deploy_isolated_test_binaries.py")


def load_module():
    spec = importlib.util.spec_from_file_location("isolated_binary_deployer", SCRIPT)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class IsolatedBinaryDeploymentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = load_module()

    def make_repo(self) -> tuple[Path, Path, Path, Path, Path, Path]:
        root = Path(tempfile.mkdtemp(prefix="ftk-binary-deployer-"))
        (root / "FTKModFramework").mkdir()
        (root / "tools").mkdir()
        game = root / "scratch" / "game"
        plugins = game / "BepInEx" / "plugins"
        plugins.mkdir(parents=True)
        old_framework = plugins / "FTKModFramework.dll"
        old_helper = plugins / "FtkRuntimeModelTest.dll"
        old_content = plugins / "FtkRuntimeModelTestContent.dll"
        old_framework.write_bytes(b"old-framework")
        old_helper.write_bytes(b"old-helper")
        old_content.write_bytes(b"old-content")
        staged = root / "staged"
        staged.mkdir()
        framework = staged / "FTKModFramework.dll"
        helper = staged / "FtkRuntimeModelTest.dll"
        content = staged / "FtkRuntimeModelTestContent.dll"
        framework.write_bytes(b"new-framework")
        helper.write_bytes(b"new-helper")
        content.write_bytes(b"new-content")
        return root, game, framework, helper, content, plugins

    def invoke(
        self,
        root: Path,
        game: Path,
        framework: Path,
        helper: Path,
        content: Path,
        execute: bool,
    ) -> dict:
        command = [
            sys.executable, str(SCRIPT), "--repo", str(root), "--game-root", str(game),
            "--framework", str(framework), "--helper", str(helper), "--content", str(content),
            "--label", "unit-binary-deploy",
        ]
        if execute:
            command.append("--execute")
        completed = subprocess.run(command, check=True, capture_output=True, text=True)
        return json.loads(completed.stdout)

    def test_dry_run_then_execute_pins_and_replaces_only_selected_binaries(self):
        root, game, framework, helper, content, plugins = self.make_repo()
        dry = self.invoke(root, game, framework, helper, content, execute=False)
        self.assertEqual(dry["status"], "PINNED_RUNTIME_BINARY_INPUTS_VERIFIED")
        self.assertFalse(dry["changes"]["framework"]["sameBytes"])
        self.assertFalse(dry["changes"]["helper"]["sameBytes"])
        self.assertFalse(dry["changes"]["content"]["sameBytes"])
        self.assertEqual((plugins / "FTKModFramework.dll").read_bytes(), b"old-framework")
        self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"old-helper")
        self.assertEqual((plugins / "FtkRuntimeModelTestContent.dll").read_bytes(), b"old-content")

        result = self.invoke(root, game, framework, helper, content, execute=True)
        self.assertEqual(result["status"], "VERIFIED_COMPLETE")
        self.assertEqual((plugins / "FTKModFramework.dll").read_bytes(), b"new-framework")
        self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"new-helper")
        self.assertEqual((plugins / "FtkRuntimeModelTestContent.dll").read_bytes(), b"new-content")
        backups = sorted((game / "deployment-backups").glob("unit-binary-deploy-*"))
        self.assertEqual(len(backups), 1)
        receipt = json.loads((backups[0] / "deployment.json").read_text())
        self.assertEqual(receipt["status"], "VERIFIED_COMPLETE")
        self.assertEqual(
            receipt["new"]["BepInEx/plugins/FTKModFramework.dll"]["sha256"],
            hashlib.sha256(b"new-framework").hexdigest(),
        )
        self.assertEqual(
            receipt["new"]["BepInEx/plugins/FtkRuntimeModelTest.dll"]["sha256"],
            hashlib.sha256(b"new-helper").hexdigest(),
        )
        self.assertEqual(
            receipt["new"]["BepInEx/plugins/FtkRuntimeModelTestContent.dll"]["sha256"],
            hashlib.sha256(b"new-content").hexdigest(),
        )

    def test_running_isolated_game_is_refused_before_any_write(self):
        root, game, _, _, _, plugins = self.make_repo()
        executable = str(game / "FTK.app" / "Contents" / "MacOS" / "FTK")
        with mock.patch.object(self.module.subprocess, "check_output", return_value=executable + " -screen-width 1\n"):
            with self.assertRaisesRegex(AssertionError, "Isolated FTK is running"):
                self.module.assert_game_stopped(game)
        self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"old-helper")


if __name__ == "__main__":
    unittest.main()
