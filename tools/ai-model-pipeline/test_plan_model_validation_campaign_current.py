from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools/ai-model-pipeline/plan_model_validation_campaign.py"
SPEC = importlib.util.spec_from_file_location("plan_model_validation_campaign", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
campaign = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = campaign
SPEC.loader.exec_module(campaign)


class CurrentModelValidationCampaignTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.report = campaign.build_campaign(
            ROOT,
            ROOT / "docs/model-validation-execution-queue.json",
            ROOT / "scratch/model-validation-stage-readiness.json",
            ROOT / "scratch/mirewarden-game",
        )

    def test_every_backlog_route_has_one_exact_next_step(self) -> None:
        summary = self.report["summary"]
        self.assertEqual(summary["routes"], 0)
        self.assertEqual(summary["isolatedEnemyTrialsReady"], 0)
        self.assertEqual(summary["passiveArrivalWorkflowsRequired"], 0)
        self.assertEqual(summary["nativePlayerWorkflowsRequired"], 0)
        self.assertEqual(summary["adapterImplementationsRequired"], 0)
        self.assertEqual(summary["adapterValidationsRequired"], 0)
        self.assertEqual(summary["adapterVisualArchiveReviewsRequired"], 0)
        self.assertEqual(summary["unresolvedRoutePlans"], 0)
        self.assertEqual(summary["proposedEnemyOutputsAlreadyExist"], 0)
        self.assertEqual(summary["enemyTrialRecordsRequiringInspection"], 0)

    def test_completed_exact_renderer_topologies_are_no_longer_planned(self) -> None:
        self.assertFalse(any(
            route["topologyGroup"] in {
                "730458bde8737934",
                "e879df15fd3c4d59",
                "8f027145e71c9525",
                "b4ccd4e96e3a224b",
                "0db0dbdf202b8e11",
                "11742c19aa67e6d0",
                "2185298a0a369e67",
                "23c62612fd16b533",
                "5990c51ca004ebdb",
                "678c8066b33cf823",
                "6d40f2e6eba19a6b",
                "6fdb7ff148451731",
                "73739eaf6fd8f0e4",
                "73ef97795cc54f57",
                "791b63f3064c2f62",
                "896faa557db56261",
                "b65252b48fd9609d",
                "ba17c1398db51453",
                "fb84ec3e6e18f459",
                "f50b09e31a8484cf",
                "cace272650590c4f",
                "0f29e98795c302a5",
                "1329d6985dadecee",
                "853432a7c217ff04",
                "b0b93182a11758ed",
                "c01698c74bd54910",
                "02a6f31412bd52ab",
                "1322fec3354db550",
                "8c73c366b064726a",
                "963e53f60643b796",
                "a158ab62f9dd430f",
                "07f911c982da0402",
                "80d61d495b6a93be",
                "c3487d422b832d2e",
                "c3683bc2e807b15a",
                "e45711bff451ca73",
                "d1de8c46112a77d8",
                "1eda40629ee9aac9",
                "7b140c07befa33fd",
                "81f02cdbf3eefcb9",
            }
            for route in self.report["routes"]
        ))

    def test_no_ordinary_enemy_command_remains_after_bramblecoil(self) -> None:
        routes = [row for row in self.report["routes"] if row["status"] == "isolated_enemy_trial_ready"]
        self.assertEqual(routes, [])
        self.assertEqual(
            self.report["summary"]["proposedEnemyOutputsAlreadyExist"],
            sum(bool(route.get("proposedOutputExists")) for route in self.report["routes"]),
        )

    def test_kraken_head_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "e45711bff451ca73"
            for row in self.report["routes"]
        ))

    def test_mournglass_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "d1de8c46112a77d8"
            for row in self.report["routes"]
        ))

    def test_tidecrown_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "1eda40629ee9aac9"
            for row in self.report["routes"]
        ))

    def test_bramblecoil_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "7b140c07befa33fd"
            for row in self.report["routes"]
        ))

    def test_honeyback_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "07f911c982da0402"
            for row in self.report["routes"]
        ))

    def test_duneshade_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "80d61d495b6a93be"
            for row in self.report["routes"]
        ))

    def test_sargassum_primary_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "aae2ba644a16c223"
            for row in self.report["routes"]
        ))

    def test_ashfang_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "c3487d422b832d2e"
            for row in self.report["routes"]
        ))

    def test_moonreed_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "c3683bc2e807b15a"
            for row in self.report["routes"]
        ))

    def test_adapter_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "6a28ac3cf4523c24"
            for row in self.report["routes"]
        ))

    def test_mirewarden_and_gloamcap_routes_are_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "db2a524a5bc700ea"
            for row in self.report["routes"]
        ))

    def test_thistlewick_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "f50b09e31a8484cf"
            for row in self.report["routes"]
        ))

    def test_verdigrin_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "cace272650590c4f"
            for row in self.report["routes"]
        ))

    def test_sunspire_roc_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "0f29e98795c302a5"
            for row in self.report["routes"]
        ))

    def test_belladusk_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "1329d6985dadecee"
            for row in self.report["routes"]
        ))

    def test_bronzehollow_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "853432a7c217ff04"
            for row in self.report["routes"]
        ))

    def test_rustpetal_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "b0b93182a11758ed"
            for row in self.report["routes"]
        ))

    def test_player_routes_have_non_overwriting_current_plan_lifecycle(self) -> None:
        routes = [row for row in self.report["routes"] if row["routeKind"] == "playerSkinset"]
        self.assertEqual(routes, [])
        for route in routes:
            self.assertTrue(route["proposedPlayerPlan"].startswith("scratch/model-route-"))
            if route["proposedPlayerPlanExists"]:
                self.assertEqual(route["status"], "native_player_plan_current")
                self.assertIn("sha256", route["playerPlan"])
                self.assertNotIn("planCommand", route)
            else:
                self.assertEqual(route["status"], "native_player_workflow_required")
                self.assertIn("--output", route["planCommand"])

    def test_player_plan_paths_change_with_exact_plan_content(self) -> None:
        first = campaign.player_plan_output(ROOT, "0123456789abcdef", {"queue": {"sha256": "a" * 64}})
        same = campaign.player_plan_output(ROOT, "0123456789abcdef", {"queue": {"sha256": "a" * 64}})
        changed = campaign.player_plan_output(ROOT, "0123456789abcdef", {"queue": {"sha256": "b" * 64}})
        self.assertEqual(first, same)
        self.assertNotEqual(first, changed)
        self.assertRegex(first.name, r"^model-route-0123456789abcdef-playerskinset-plan-[0-9a-f]{12}\.json$")

    def test_tamarind_passive_route_is_complete(self) -> None:
        self.assertFalse(any(
            row["topologyGroup"] == "81f02cdbf3eefcb9"
            for row in self.report["routes"]
        ))

    def test_existing_enemy_trial_advances_without_becoming_acceptance(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "FTKModFramework").mkdir()
            (root / "tools").mkdir()
            (root / "scratch/case/pass").mkdir(parents=True)
            group = "0123456789abcdef"
            kind = "directEnemy"
            output = campaign.route_output(root, group, kind)
            expected_plan = {
                "profile": {"key": "ftkmf_modeltest_fixture", "document": {"path": "fixture.json", "sha256": "a" * 64}},
                "catalog": {"path": "catalog.json", "sha256": "b" * 64},
                "assets": [{"name": "fixture.glb", "sha256": "c" * 64}],
                "sourceAssignments": [{"rendererPath": "body", "sourceRendererId": 123}],
                "motionRendererPath": "body",
                "queue": {"path": "queue.json", "sha256": "d" * 64},
                "stageReadiness": {"path": "stage.json", "sha256": "e" * 64},
            }
            renderer = {"celRelativeRendererPath": "body", "mesh": "mesh", "boneSignature": "bones"}
            case_path = root / "scratch/case/case-result.json"
            case_path.write_text(json.dumps({
                "status": "needs_visual_review",
                "enemy": "ftkmf_modeltest_fixture",
                "session": "session-1",
                "profileSha256": "b" * 64,
                "initialRenderer": renderer,
            }))
            record_plan = dict(expected_plan)
            record_plan.update(topologyGroup=group, coverageRouteKind=kind)
            output.write_text(json.dumps({
                "status": "needs_visual_review",
                "needsManualVisualReview": True,
                "needsImmutableArchive": True,
                "session": "session-1",
                "initialRenderer": renderer,
                "caseResult": "scratch/case/case-result.json",
                "plan": record_plan,
            }))

            awaiting = campaign.existing_trial_state(root, output, expected_plan, group, kind)
            self.assertEqual(awaiting["status"], "enemy_trial_awaiting_visual_review")
            self.assertNotIn("--run", awaiting["reviewTemplateCommand"])
            advanced = campaign.advance_enemy_entry(
                {"liveCommand": ["python3", "--run"], "liveCommandText": "python3 --run"}, awaiting
            )
            self.assertNotIn("liveCommand", advanced)
            self.assertNotIn("liveCommandText", advanced)

            review_path = campaign.route_review_output(root, group, kind)
            review = {
                "session": "session-1",
                "reviewStatus": "pending_manual_root_review",
                "binding": {"rendererPath": "body", "mesh": "mesh", "boneSignature": "bones"},
                "frames": [{"path": "scratch/case/pass/0000.png"}],
            }
            review_path.write_text(json.dumps(review))
            pending = campaign.existing_trial_state(root, output, expected_plan, group, kind)
            self.assertEqual(pending["status"], "enemy_trial_manual_review_pending")

            review["reviewStatus"] = "reviewed_fixture"
            review_path.write_text(json.dumps(review))
            reviewed = campaign.existing_trial_state(root, output, expected_plan, group, kind)
            self.assertEqual(reviewed["status"], "enemy_trial_reviewed_archive_plan_required")

    def test_existing_enemy_trial_identity_mismatch_never_suggests_rerun(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "scratch").mkdir()
            output = campaign.route_output(root, "0123456789abcdef", "directEnemy")
            output.write_text(json.dumps({
                "status": "needs_visual_review",
                "plan": {"topologyGroup": "different", "coverageRouteKind": "directEnemy"},
            }))
            state = campaign.existing_trial_state(
                root, output, {"profile": {"key": "fixture"}}, "0123456789abcdef", "directEnemy"
            )
            self.assertEqual(state["status"], "enemy_trial_record_identity_mismatch")
            self.assertIn("do not overwrite or automatically rerun", state["nextAction"].lower())


if __name__ == "__main__":
    unittest.main()
