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

    def fake_processes(self, rows, cwds=None):
        """Replace the live process table so no test depends on what this machine runs."""
        cwds = cwds or {}
        return mock.patch.multiple(
            self.module,
            running_processes=mock.Mock(return_value=rows),
            process_cwd=mock.Mock(side_effect=lambda pid: cwds.get(pid)),
        )

    def run_main(self, root: Path, game: Path, helper: Path, execute: bool) -> None:
        argv = [
            str(SCRIPT), "--repo", str(root), "--game-root", str(game),
            "--helper", str(helper), "--label", "unit-binary-deploy",
        ]
        if execute:
            argv.append("--execute")
        with mock.patch.object(sys, "argv", argv), mock.patch("builtins.print"):
            self.module.main()

    def test_running_isolated_game_is_refused_before_any_write(self):
        root, game, _, _, _, plugins = self.make_repo()
        executable = str(game / "FTK.app" / "Contents" / "MacOS" / "FTK")
        with mock.patch.object(self.module.subprocess, "check_output", return_value="  4242 " + executable + "\n"):
            with self.assertRaisesRegex(AssertionError, "Isolated FTK is running.*PID 4242 "):
                self.module.assert_game_stopped(game)
        self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"old-helper")

    def test_process_table_parsing_keeps_paths_with_spaces(self):
        output = "    1 /sbin/launchd\n  812 /Games/My Copy/Paladin Control.app/Contents/MacOS/FTK\nbad row\n"
        with mock.patch.object(self.module.subprocess, "check_output", return_value=output) as check_output:
            rows = self.module.running_processes()
        self.assertEqual(check_output.call_args.args[0], ["ps", "-axo", "pid=,comm="])
        self.assertEqual(rows, [(1, "/sbin/launchd"), (812, "/Games/My Copy/Paladin Control.app/Contents/MacOS/FTK")])

    def test_renamed_app_bundle_inside_copy_is_refused_with_pid_and_path(self):
        _, game, _, _, _, _ = self.make_repo()
        for bundle in ("PaladinControl.app", "PaladinGear.app"):
            executable = str(game / bundle / "Contents" / "MacOS" / "FTK")
            with self.fake_processes([(1, "/sbin/launchd"), (5150, executable)]):
                with self.assertRaises(AssertionError) as raised:
                    self.module.assert_game_stopped(game)
            self.assertIn("PID 5150 " + executable, str(raised.exception))

    def test_symlinked_launch_path_is_matched_on_the_resolved_root(self):
        root, game, _, _, _, _ = self.make_repo()
        alias = root / "alias-to-game"
        alias.symlink_to(game, target_is_directory=True)
        executable = str(alias / "PaladinControl.app" / "Contents" / "MacOS" / "FTK")
        with self.fake_processes([(77, executable)]):
            with self.assertRaisesRegex(AssertionError, "PID 77 "):
                self.module.assert_game_stopped(game)

    def test_relative_launch_path_is_resolved_against_the_process_directory(self):
        _, game, _, _, _, _ = self.make_repo()
        rows = [(31, "PaladinGear.app/Contents/MacOS/FTK"), (32, "sleep")]
        with self.fake_processes(rows, cwds={31: game}):
            with self.assertRaisesRegex(AssertionError, "PID 31 "):
                self.module.assert_game_stopped(game)
            self.module.process_cwd.assert_called_once_with(31)

    def test_processes_outside_the_copy_do_not_block(self):
        root, game, _, _, _, _ = self.make_repo()
        sibling = root / "scratch" / "game2"
        rows = [
            (10, str(sibling / "PaladinGear.app" / "Contents" / "MacOS" / "FTK")),
            (11, "/Applications/FTK.app/Contents/MacOS/FTK"),
            (12, "PaladinGear.app/Contents/MacOS/FTK"),
            (13, "Contents/MacOS/Exited"),
        ]
        with self.fake_processes(rows, cwds={12: sibling}):
            self.module.assert_game_stopped(game)

    def test_dry_run_and_execute_refuse_a_running_renamed_copy_before_any_write(self):
        root, game, _, helper, _, plugins = self.make_repo()
        executable = str(game / "PaladinControl.app" / "Contents" / "MacOS" / "FTK")
        for execute in (False, True):
            with self.fake_processes([(900, executable)]):
                with self.assertRaisesRegex(AssertionError, "PID 900 "):
                    self.run_main(root, game, helper, execute)
            self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"old-helper")
            self.assertFalse((game / "deployment-backups").exists())

        with self.fake_processes([]):
            self.run_main(root, game, helper, execute=True)
        self.assertEqual((plugins / "FtkRuntimeModelTest.dll").read_bytes(), b"new-helper")


if __name__ == "__main__":
    unittest.main()
