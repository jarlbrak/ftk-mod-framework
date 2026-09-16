from __future__ import annotations

import hashlib
import json
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


TOOL_DIR = Path(__file__).resolve().parent
RUNNER_PATH = TOOL_DIR / "runtime-test" / "run_execution_queue_route.py"
sys.path.insert(0, str(RUNNER_PATH.parent))
import run_execution_queue_route as runner


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class ExecutionQueueRouteRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "FTKModFramework").mkdir()
        (self.root / "tools").mkdir()
        self.game = self.root / "scratch" / "game"
        self.models = self.game / runner.MODELS_RELATIVE
        self.models.mkdir(parents=True)
        self.package = self.root / "art-experiments" / "example"
        self.package.mkdir(parents=True)
        self.profile = {
            "key": "ftkmf_modeltest_example",
            "baseEnemy": "exampleA",
            "displayName": "Example",
            "combatProfile": "a" * 64,
            "renderers": [{
                "rendererPath": "body",
                "glbFile": "example.glb",
                "textureFile": "example.png",
            }],
        }
        self.document = self.package / "runtime-profile.json"
        self.document.write_text(json.dumps({"version": 1, "profiles": [self.profile]}, indent=2) + "\n")
        self.assets = {"example.glb": b"model", "example.png": b"texture"}
        for name, contents in self.assets.items():
            (self.package / name).write_bytes(contents)
            (self.models / name).write_bytes(contents)
        self.catalog = self.game / "model-test-profiles.json"
        self.catalog.write_text(json.dumps({"version": 1, "profiles": [self.profile]}, indent=2) + "\n")
        self.document_ref = {
            "path": str(self.document.relative_to(self.root)),
            "sha256": sha(self.document),
        }
        self.queue_path = self.root / "docs" / "queue.json"
        self.queue_path.parent.mkdir()
        self.queue = {
            "routes": [{
                "topologyGroup": "group",
                "routeKind": "directEnemy",
                "selectedValidationTargets": [{
                    "targetType": "source_assignment",
                    "sourceKind": "native_enemy_row",
                    "nativeEnemy": "exampleA",
                    "resourcePrefab": None,
                    "rendererPath": "body",
                    "sourceRendererId": 123,
                }],
                "preflightedProfileCandidates": [{
                    "key": self.profile["key"],
                    "routeKind": "direct_enemy",
                    "profileDocument": self.document_ref,
                }],
            }],
        }
        self.queue_path.write_text(json.dumps(self.queue, indent=2) + "\n")
        self.stage_path = self.root / "scratch" / "stage-readiness.json"
        assets = [{
            "name": name,
            "sourceSha256": hashlib.sha256(contents).hexdigest(),
            "targetSha256": hashlib.sha256(contents).hexdigest(),
            "state": "identical_asset_already_present",
        } for name, contents in sorted(self.assets.items())]
        self.stage = {
            "isolatedGameRoot": "scratch/game",
            "queue": {"input": {"path": "docs/queue.json", "sha256": sha(self.queue_path)}},
            "catalogInputs": [{
                "kind": "enemy",
                "path": "scratch/game/model-test-profiles.json",
                "sha256": sha(self.catalog),
            }],
            "revisions": [{
                "profileDocument": self.document_ref,
                "profile": self.profile,
                "routeKind": "direct_enemy",
                "catalogKind": "enemy",
                "assets": assets,
                "stageState": "ready_without_catalog_or_asset_stage",
            }],
            "routeChoices": [{
                "topologyGroup": "group",
                "routeKind": "directEnemy",
                "nextStagingAction": "stage_ready_revision_available",
                "candidateRevisions": [{
                    "profileDocument": self.document_ref,
                    "key": self.profile["key"],
                    "catalogKind": "enemy",
                    "stageState": "ready_without_catalog_or_asset_stage",
                }],
            }],
        }
        self.stage_path.write_text(json.dumps(self.stage, indent=2) + "\n")

    def plan(self) -> dict:
        route = runner.selected_route(self.queue, "group", "directEnemy")
        choice = runner.selected_choice(self.stage, "group", "directEnemy")
        candidate = runner.choose_profile(choice, None)
        return runner.validate_stage_ledger(
            self.root, self.queue_path, self.queue, self.stage_path, self.stage, self.game, route, candidate
        )

    def test_stage_ready_direct_route_has_an_exact_plan(self) -> None:
        plan = self.plan()
        self.assertEqual(plan["profile"]["routeKind"], "direct_enemy")
        self.assertEqual(plan["sourceAssignments"][0]["nativeEnemy"], "exampleA")
        self.assertEqual(plan["motionRendererPath"], "body")
        self.assertEqual(plan["catalog"]["sha256"], sha(self.catalog))
        self.assertEqual({asset["name"] for asset in plan["assets"]}, set(self.assets))

    def test_passive_arrival_workflow_keeps_exact_ready_slot(self) -> None:
        plan = self.plan()
        choice = {
            "selectedWorkflow": "passive_enemy_arrival",
            "arrivalLevel": 0,
            "arrivalRoom": 2,
        }
        self.assertIs(runner.apply_selected_workflow(plan, choice), plan)
        self.assertEqual(plan["workflow"]["kind"], "passive_enemy_arrival")
        self.assertEqual((plan["workflow"]["level"], plan["workflow"]["room"]), (0, 2))

    def test_passive_arrival_rejects_missing_or_unknown_workflow_contract(self) -> None:
        with self.assertRaisesRegex(ValueError, "requires nonnegative"):
            runner.apply_selected_workflow({}, {"selectedWorkflow": "passive_enemy_arrival"})
        with self.assertRaisesRegex(ValueError, "unsupported"):
            runner.apply_selected_workflow({}, {"selectedWorkflow": "unknown"})

    def test_stale_catalog_is_rejected_before_any_launch(self) -> None:
        self.catalog.write_text(json.dumps({"version": 1, "profiles": []}, indent=2) + "\n")
        with self.assertRaisesRegex(ValueError, "catalog changed"):
            self.plan()

    def test_multiple_stage_ready_revisions_require_an_explicit_document(self) -> None:
        choice = {
            "nextStagingAction": "stage_ready_revision_available",
            "candidateRevisions": [
                {"stageState": "ready_without_catalog_or_asset_stage", "profileDocument": {"path": "one.json"}},
                {"stageState": "ready_without_catalog_or_asset_stage", "profileDocument": {"path": "two.json"}},
            ],
        }
        with self.assertRaisesRegex(ValueError, "multiple stage-ready"):
            runner.choose_profile(choice, None)
        self.assertEqual(runner.choose_profile(choice, "two.json")["profileDocument"]["path"], "two.json")

    def test_pinned_stage_selection_resolves_multiple_revisions(self) -> None:
        second = {
            "key": "current",
            "stageState": "ready_without_catalog_or_asset_stage",
            "profileDocument": {"path": "two.json"},
        }
        choice = {
            "nextStagingAction": "stage_ready_revision_available",
            "selectedRevision": second,
            "candidateRevisions": [
                {
                    "key": "old",
                    "stageState": "ready_without_catalog_or_asset_stage",
                    "profileDocument": {"path": "one.json"},
                },
                second,
            ],
        }
        self.assertEqual(runner.choose_profile(choice, None), second)

    def test_changed_profile_selection_ledger_is_rejected(self) -> None:
        selections = self.root / "docs" / "selections.json"
        selections.write_text("{}\n")
        self.stage["profileSelections"] = {
            "input": {"path": "docs/selections.json", "sha256": sha(selections)}
        }
        selections.write_text('{"changed":true}\n')
        with self.assertRaisesRegex(ValueError, "stale for the current profile selections"):
            self.plan()

    def test_multiple_selected_skinned_parts_require_an_explicit_motion_renderer(self) -> None:
        profile = {"renderers": [
            {"rendererPath": "body"},
            {"rendererPath": "eye"},
        ]}
        targets = [{"rendererPath": "body"}, {"rendererPath": "eye"}]
        with self.assertRaisesRegex(ValueError, "multiple selected SkinnedMeshRenderer"):
            runner.choose_motion_renderer(targets, profile, None)
        self.assertEqual(runner.choose_motion_renderer(targets, profile, "eye"), "eye")

    def test_foreign_ftk_process_blocks_a_live_launch(self) -> None:
        with mock.patch.object(runner, "active_ftk_processes", return_value=[(44, "/other/FTK.app/Contents/MacOS/FTK")]):
            with self.assertRaisesRegex(RuntimeError, "another FTK session"):
                runner.assert_fresh_launch_environment(self.game, 8788)

    def test_symlinked_inputs_are_rejected_before_planning_or_launch(self) -> None:
        linked_document = self.root / "art-experiments" / "linked-profile.json"
        linked_document.symlink_to(self.document)
        with self.assertRaisesRegex(ValueError, "must not be a symlink"):
            runner.repository_file(self.root, linked_document, "profile")
        linked_game = self.root / "scratch" / "linked-game"
        linked_game.symlink_to(self.game, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "must not be a symlink"):
            runner.isolated_game(self.root, linked_game)

    def test_runner_record_must_be_a_new_json_file_directly_under_scratch(self) -> None:
        output = runner.runner_record_path(self.root, Path("scratch/example-run.json"))
        self.assertEqual(output, (self.root / "scratch/example-run.json").resolve())
        with self.assertRaisesRegex(ValueError, "directly under repository scratch"):
            runner.runner_record_path(self.root, self.root / "outside.json")

    def test_runner_stops_the_owned_process_group(self) -> None:
        process = mock.Mock(pid=77)
        process.wait.return_value = 0
        handle = mock.Mock()
        with mock.patch.object(runner.os, "killpg") as killpg:
            runner.stop_owned_game(process, handle)
        killpg.assert_called_once_with(77, signal.SIGTERM)
        process.wait.assert_called_once_with(timeout=5)
        handle.close.assert_called_once_with()

    def test_only_completed_exercises_request_review_and_archive(self) -> None:
        self.assertEqual(runner.review_requirements("needs_visual_review"), {
            "needsManualVisualReview": True,
            "needsImmutableArchive": True,
        })
        for status in ("stopped", "runner_error", None):
            self.assertEqual(runner.review_requirements(status), {
                "needsManualVisualReview": False,
                "needsImmutableArchive": False,
            })

    def test_cli_dry_plan_uses_the_coverage_to_profile_route_mapping(self) -> None:
        result = subprocess.run(
            [
                sys.executable, str(RUNNER_PATH), "--root", str(self.root), "--queue", str(self.queue_path),
                "--stage-readiness", str(self.stage_path), "--game-root", str(self.game),
                "--topology-group", "group", "--route-kind", "directEnemy",
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["profile"]["key"], self.profile["key"])

    def test_cli_dry_plan_records_the_opt_in_attack_skill_fixture(self) -> None:
        result = subprocess.run(
            [
                sys.executable, str(RUNNER_PATH), "--root", str(self.root), "--queue", str(self.queue_path),
                "--stage-readiness", str(self.stage_path), "--game-root", str(self.game),
                "--topology-group", "group", "--route-kind", "directEnemy",
                "--cap-equipped-attack-skill",
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(json.loads(result.stdout)["partyFixture"]["capEquippedAttackSkill"])

    def test_cli_dry_plan_records_the_opt_in_native_damage_fixture(self) -> None:
        result = subprocess.run(
            [
                sys.executable, str(RUNNER_PATH), "--root", str(self.root), "--queue", str(self.queue_path),
                "--stage-readiness", str(self.stage_path), "--game-root", str(self.game),
                "--topology-group", "group", "--route-kind", "directEnemy",
                "--minimum-native-weapon-max-damage", "30",
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        fixture = json.loads(result.stdout)["partyFixture"]
        self.assertEqual(fixture["minimumNativeWeaponMaxDamage"], 30)
        self.assertIn("does not guarantee actual damage", fixture["boundary"])

    def test_cli_rejects_an_unbounded_native_damage_fixture(self) -> None:
        result = subprocess.run(
            [
                sys.executable, str(RUNNER_PATH), "--root", str(self.root), "--queue", str(self.queue_path),
                "--stage-readiness", str(self.stage_path), "--game-root", str(self.game),
                "--topology-group", "group", "--route-kind", "directEnemy",
                "--minimum-native-weapon-max-damage", "101",
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("must be in 1..100", result.stderr)


if __name__ == "__main__":
    unittest.main()
