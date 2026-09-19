from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py"
QUEUE = ROOT / "docs/model-validation-execution-queue.json"
STAGE = ROOT / "scratch/model-validation-stage-readiness.json"
GAME = ROOT / "scratch/mirewarden-game"

sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline/runtime-test"))
import plan_execution_queue_player_route as player_planner
from local_inputs import skip_without_local_inputs


class CurrentPlayerQueuePlanTests(unittest.TestCase):
    def test_output_path_is_confined_to_direct_scratch_json(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            (root / "scratch").mkdir()
            self.assertEqual(
                player_planner.plan_output(root, Path("scratch/player-plan.json")),
                root / "scratch/player-plan.json",
            )
            with self.assertRaisesRegex(ValueError, "directly under"):
                player_planner.plan_output(root, Path("player-plan.json"))
            with self.assertRaisesRegex(ValueError, "directly under"):
                player_planner.plan_output(root, Path("scratch/nested/player-plan.json"))

    @skip_without_local_inputs(STAGE, GAME)
    def test_every_stage_ready_player_route_has_a_current_preview_plan(self) -> None:
        queue = json.loads(QUEUE.read_text())
        stage = json.loads(STAGE.read_text())
        groups = []
        for route in queue["routes"]:
            if route["routeKind"] != "playerSkinset":
                continue
            choice = next(choice for choice in stage["routeChoices"]
                          if choice["topologyGroup"] == route["topologyGroup"]
                          and choice["routeKind"] == "playerSkinset")
            if choice["nextStagingAction"] != "stage_ready_revision_available":
                continue
            result = subprocess.run(
                [
                    sys.executable, str(SCRIPT), "--root", str(ROOT), "--queue", str(QUEUE),
                    "--stage-readiness", str(STAGE), "--game-root", str(GAME),
                    "--topology-group", route["topologyGroup"],
                ],
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            plan = json.loads(result.stdout)
            target = route["selectedValidationTargets"][0]
            self.assertEqual(plan["profile"]["key"], target["profile"])
            self.assertEqual(plan["playerProfileEvidence"], target)
            self.assertEqual(plan["catalog"]["path"], "scratch/mirewarden-game/model-test-player-profiles.json")
            groups.append(route["topologyGroup"])
        self.assertEqual(groups, [])


if __name__ == "__main__":
    unittest.main()
