from __future__ import annotations

import json
from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[2]
TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR / "runtime-test"))
import run_execution_queue_route as runner
from local_inputs import skip_without_local_inputs


QUEUE_PATH = ROOT / "docs/model-validation-execution-queue.json"
STAGE_PATH = ROOT / "scratch/model-validation-stage-readiness.json"
GAME = ROOT / "scratch/mirewarden-game"


@skip_without_local_inputs(STAGE_PATH, GAME)
class CurrentExecutionQueueRouteRunnerTests(unittest.TestCase):
    def test_every_stage_ready_route_has_one_pinned_selected_revision(self) -> None:
        stage = json.loads(STAGE_PATH.read_text())
        choices = [
            runner.as_dict(raw) for raw in stage["routeChoices"]
            if runner.as_dict(raw).get("nextStagingAction") == "stage_ready_revision_available"
        ]
        self.assertEqual(len(choices), stage["summary"]["routesWithStageReadyRevision"])
        self.assertEqual(len(choices), stage["summary"]["stageReadyRoutesWithSelectedRevision"])
        for choice in choices:
            selected = runner.as_dict(choice.get("selectedRevision"))
            self.assertTrue(selected.get("key"), choice)
            self.assertTrue(runner.as_dict(selected.get("profileDocument")).get("sha256"), choice)

    def test_every_stage_ready_enemy_revision_has_a_current_exact_dry_plan(self) -> None:
        queue = json.loads(QUEUE_PATH.read_text())
        stage = json.loads(STAGE_PATH.read_text())
        plans = []
        routes = set()
        player_routes = set()
        profile_revisions = set()
        for raw_choice in stage["routeChoices"]:
            choice = runner.as_dict(raw_choice)
            if choice.get("nextStagingAction") != "stage_ready_revision_available":
                continue
            route = runner.selected_route(queue, choice["topologyGroup"], choice["routeKind"])
            ready = [runner.as_dict(item) for item in runner.as_list(choice.get("candidateRevisions"))
                     if runner.as_dict(item).get("stageState") == "ready_without_catalog_or_asset_stage"]
            self.assertTrue(ready)
            if route["routeKind"] == "playerSkinset":
                player_routes.add((route["topologyGroup"], route["routeKind"]))
                for raw_candidate in ready:
                    document = runner.as_dict(raw_candidate["profileDocument"])["path"]
                    candidate = runner.choose_profile(choice, document)
                    profile_revisions.add((document, candidate["key"]))
                    plan = runner.validate_stage_ledger(
                        ROOT, QUEUE_PATH, queue, STAGE_PATH, stage, GAME, route, candidate
                    )
                    self.assertEqual(plan["profile"]["routeKind"], "player_skinset_avatar")
                    self.assertEqual(plan["playerProfileEvidence"]["profile"], candidate["key"])
                    plans.append(plan)
                continue
            self.assertIn(route["routeKind"], ("directEnemy", "resourcePrefab"))
            routes.add((route["topologyGroup"], route["routeKind"]))
            for raw_candidate in ready:
                document = runner.as_dict(raw_candidate["profileDocument"])["path"]
                candidate = runner.choose_profile(choice, document)
                profile_revisions.add((document, candidate["key"]))
                revision = runner.revision_row(stage, document, candidate["key"])
                targets = runner.source_targets(route, runner.as_dict(revision["profile"]))
                eligible = runner.eligible_motion_renderers(targets, runner.as_dict(revision["profile"]))
                motion_renderer = eligible[0] if len(eligible) > 1 else None
                plans.append(runner.validate_stage_ledger(
                    ROOT, QUEUE_PATH, queue, STAGE_PATH, stage, GAME, route, candidate, motion_renderer
                ))
        self.assertEqual(player_routes, set())
        self.assertEqual(len(routes) + len(player_routes), stage["summary"]["routesWithStageReadyRevision"])
        self.assertEqual(len(profile_revisions), stage["summary"]["readyWithoutCatalogOrAssetStage"])
        self.assertTrue(all(plan["catalog"]["sha256"] for plan in plans))


if __name__ == "__main__":
    unittest.main()
