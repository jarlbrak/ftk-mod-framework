#!/usr/bin/env python3
"""Offline checks for the custom-model stage deployer's running-copy guard."""
from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).with_name("deploy_custom_model_stage.py")


def load_module():
    spec = importlib.util.spec_from_file_location("custom_model_stage_deployer", SCRIPT)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RunningCopyGuardTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = load_module()

    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.game = self.root / "scratch" / "game"
        self.game.mkdir(parents=True)
        self.stage = self.root / "scratch" / "stage"
        self.stage.mkdir()

    def fake_processes(self, rows, cwds=None):
        """Replace the live process table so no test depends on what this machine runs."""
        cwds = cwds or {}
        return mock.patch.multiple(
            self.module,
            running_processes=mock.Mock(return_value=rows),
            process_cwd=mock.Mock(side_effect=lambda pid: cwds.get(pid)),
        )

    def test_process_table_parsing_keeps_paths_with_spaces(self):
        output = "    1 /sbin/launchd\n  812 /Games/My Copy/Paladin Control.app/Contents/MacOS/FTK\nbad row\n"
        with mock.patch.object(self.module.subprocess, "check_output", return_value=output) as check_output:
            rows = self.module.running_processes()
        self.assertEqual(check_output.call_args.args[0], ["ps", "-axo", "pid=,comm="])
        self.assertEqual(rows, [(1, "/sbin/launchd"), (812, "/Games/My Copy/Paladin Control.app/Contents/MacOS/FTK")])

    def test_original_bundle_name_is_still_refused(self):
        executable = str(self.game / "FTK.app" / "Contents" / "MacOS" / "FTK")
        with mock.patch.object(self.module.subprocess, "check_output", return_value="  4242 " + executable + "\n"):
            with self.assertRaisesRegex(AssertionError, "Isolated FTK is running.*PID 4242 "):
                self.module.assert_game_stopped(self.game)

    def test_renamed_app_bundle_inside_copy_is_refused_with_pid_and_path(self):
        for bundle in ("PaladinControl.app", "PaladinGear.app"):
            executable = str(self.game / bundle / "Contents" / "MacOS" / "FTK")
            with self.fake_processes([(1, "/sbin/launchd"), (5150, executable)]):
                with self.assertRaises(AssertionError) as raised:
                    self.module.assert_game_stopped(self.game)
            self.assertIn("no files were changed", str(raised.exception))
            self.assertIn("PID 5150 " + executable, str(raised.exception))

    def test_symlinked_launch_path_is_matched_on_the_resolved_root(self):
        alias = self.root / "alias-to-game"
        alias.symlink_to(self.game, target_is_directory=True)
        executable = str(alias / "PaladinControl.app" / "Contents" / "MacOS" / "FTK")
        with self.fake_processes([(77, executable)]):
            with self.assertRaisesRegex(AssertionError, "PID 77 "):
                self.module.assert_game_stopped(self.game)

    def test_relative_launch_path_is_resolved_against_the_process_directory(self):
        rows = [(31, "PaladinGear.app/Contents/MacOS/FTK"), (32, "sleep")]
        with self.fake_processes(rows, cwds={31: self.game}):
            with self.assertRaisesRegex(AssertionError, "PID 31 "):
                self.module.assert_game_stopped(self.game)
            self.module.process_cwd.assert_called_once_with(31)

    def test_processes_outside_the_copy_do_not_block(self):
        sibling = self.root / "scratch" / "game2"
        rows = [
            (10, str(sibling / "PaladinGear.app" / "Contents" / "MacOS" / "FTK")),
            (11, "/Applications/FTK.app/Contents/MacOS/FTK"),
            (12, "PaladinGear.app/Contents/MacOS/FTK"),
            (13, "Contents/MacOS/Exited"),
        ]
        with self.fake_processes(rows, cwds={12: sibling}):
            self.module.assert_game_stopped(self.game)

    def test_dry_run_and_execute_refuse_a_running_renamed_copy_before_any_read_or_write(self):
        executable = str(self.game / "PaladinControl.app" / "Contents" / "MacOS" / "FTK")
        for execute in (False, True):
            argv = [
                str(SCRIPT), "--repo", str(self.root), "--game-root", str(self.game),
                "--stage", str(self.stage), "--label", "unit-stage-deploy",
            ]
            if execute:
                argv.append("--execute")
            # The stage is empty, so reaching the receipt read would fail differently.
            with self.fake_processes([(900, executable)]), mock.patch.object(sys, "argv", argv):
                with self.assertRaisesRegex(AssertionError, "PID 900 "):
                    self.module.main()
            self.assertFalse((self.game / "deployment-backups").exists())


if __name__ == "__main__":
    unittest.main()
