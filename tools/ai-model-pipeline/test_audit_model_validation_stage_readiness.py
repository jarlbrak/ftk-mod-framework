from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import audit_model_validation_stage_readiness as stage


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def profile(key: str = "ftkmf_modeltest_example") -> dict:
    return {
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


def revision(root: Path, profile_value: dict, *, route_kind: str = "direct_enemy") -> dict:
    document = root / "art-experiments" / "example" / "runtime-profile.json"
    document.parent.mkdir(parents=True, exist_ok=True)
    document.write_text(json.dumps({"version": 1, "profiles": [profile_value]}))
    for name in stage.declared_assets(profile_value):
        (document.parent / name).write_bytes(name.encode())
    return {
        "profileDocument": {"path": str(document.relative_to(root)), "sha256": sha(document)},
        "profile": profile_value,
        "routeKind": route_kind,
        "routeReferences": [{"topologyGroup": "group", "coverageRouteKind": "directEnemy"}],
    }


class StageReadinessTests(unittest.TestCase):
    def test_queue_revisions_deduplicates_one_profile_document_across_routes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value = profile()
            item = revision(root, value)
            queue = {
                "routes": [
                    {
                        "topologyGroup": "first",
                        "routeKind": "directEnemy",
                        "selectedValidationTargets": [],
                        "preflightedProfileCandidates": [{
                            "key": value["key"],
                            "routeKind": "direct_enemy",
                            "profileDocument": item["profileDocument"],
                        }],
                    },
                    {
                        "topologyGroup": "second",
                        "routeKind": "directEnemy",
                        "selectedValidationTargets": [],
                        "preflightedProfileCandidates": [{
                            "key": value["key"],
                            "routeKind": "direct_enemy",
                            "profileDocument": item["profileDocument"],
                        }],
                    },
                ]
            }
            revisions = stage.queue_revisions(root, queue)
            self.assertEqual(len(revisions), 1)
            self.assertEqual(len(revisions[0]["routeReferences"]), 2)

    def test_identical_catalog_profile_and_assets_need_no_stage(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value = profile()
            item = revision(root, value)
            models = root / "scratch" / "game" / stage.MODELS_RELATIVE
            models.mkdir(parents=True)
            for name in stage.declared_assets(value):
                source = (root / item["profileDocument"]["path"]).parent / name
                (models / name).write_bytes(source.read_bytes())
            result = stage.revision_report(
                root,
                root / "scratch" / "game",
                {"enemy": {value["key"]: value}, "player": {}},
                models,
                item,
            )
            self.assertEqual(result["catalogState"], "identical_profile_already_present")
            self.assertEqual(result["stageState"], "ready_without_catalog_or_asset_stage")

    def test_missing_catalog_profile_with_new_assets_is_appendable(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value = profile()
            item = revision(root, value)
            models = root / "scratch" / "game" / stage.MODELS_RELATIVE
            models.mkdir(parents=True)
            result = stage.revision_report(
                root,
                root / "scratch" / "game",
                {"enemy": {}, "player": {}},
                models,
                item,
            )
            self.assertEqual(result["catalogState"], "profile_append_required")
            self.assertEqual(result["stageState"], "append_or_asset_stage_available")
            self.assertEqual({asset["state"] for asset in result["assets"]}, {"new_asset_copy_required"})

    def test_changed_catalog_profile_requires_explicit_isolated_migration(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value = profile()
            item = revision(root, value)
            models = root / "scratch" / "game" / stage.MODELS_RELATIVE
            models.mkdir(parents=True)
            for name in stage.declared_assets(value):
                source = (root / item["profileDocument"]["path"]).parent / name
                (models / name).write_bytes(source.read_bytes())
            old = {**value, "visualScale": 0.5}
            result = stage.revision_report(
                root,
                root / "scratch" / "game",
                {"enemy": {value["key"]: old}, "player": {}},
                models,
                item,
            )
            self.assertEqual(result["catalogState"], "explicit_profile_migration_required")
            self.assertEqual(result["stageState"], "explicit_isolated_migration_required")

    def test_changed_profile_document_hash_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value = profile()
            item = revision(root, value)
            path = root / item["profileDocument"]["path"]
            path.write_text(json.dumps({"version": 1, "profiles": [value]}, indent=2))
            queue = {
                "routes": [{
                    "topologyGroup": "group",
                    "routeKind": "directEnemy",
                    "preflightedProfileCandidates": [{
                        "key": value["key"],
                        "routeKind": "direct_enemy",
                        "profileDocument": item["profileDocument"],
                    }],
                }]
            }
            with self.assertRaisesRegex(ValueError, "hash changed"):
                stage.queue_revisions(root, queue)

    def test_route_choices_keep_revisions_visible_without_auto_selection(self) -> None:
        queue = {
            "routes": [
                {
                    "topologyGroup": "ready",
                    "routeKind": "directEnemy",
                    "executionStatus": {"status": "profile_staging_candidate_available"},
                },
                {
                    "topologyGroup": "adapter",
                    "routeKind": "resourcePrefab",
                    "executionStatus": {"status": "adapter_or_retarget_design_required"},
                },
                {
                    "topologyGroup": "adapter-implementation",
                    "routeKind": "resourcePrefab",
                    "executionStatus": {"status": "adapter_implementation_required"},
                },
                {
                    "topologyGroup": "adapter-review",
                    "routeKind": "resourcePrefab",
                    "executionStatus": {"status": "adapter_visual_archive_review_required"},
                },
            ]
        }
        rows = [{
            "profileDocument": {"path": "art/example/runtime-profile.json"},
            "profile": {"key": "ftkmf_modeltest_example"},
            "catalogKind": "enemy",
            "catalogState": "identical_profile_already_present",
            "stageState": "ready_without_catalog_or_asset_stage",
            "routeReferences": [{"topologyGroup": "ready", "coverageRouteKind": "directEnemy"}],
        }]
        choices = stage.route_choices(queue, rows)
        ready = next(choice for choice in choices if choice["topologyGroup"] == "ready")
        adapter = next(choice for choice in choices if choice["topologyGroup"] == "adapter")
        implementation = next(
            choice for choice in choices if choice["topologyGroup"] == "adapter-implementation"
        )
        review = next(choice for choice in choices if choice["topologyGroup"] == "adapter-review")
        self.assertEqual(ready["nextStagingAction"], "stage_ready_revision_available")
        self.assertEqual(len(ready["candidateRevisions"]), 1)
        self.assertEqual(adapter["nextStagingAction"], "adapter_or_retarget_design_required")
        self.assertEqual(adapter["candidateRevisions"], [])
        self.assertEqual(implementation["nextStagingAction"], "adapter_implementation_required")
        self.assertEqual(implementation["candidateRevisions"], [])
        self.assertEqual(review["nextStagingAction"], "adapter_visual_archive_review_required")
        self.assertEqual(review["candidateRevisions"], [])

    def test_hash_pinned_selection_resolves_one_historical_revision(self) -> None:
        queue = {
            "routes": [{
                "topologyGroup": "group",
                "routeKind": "directEnemy",
                "executionStatus": {"status": "profile_staging_candidate_available"},
            }]
        }
        first = {
            "profileDocument": {"path": "one.json", "sha256": "1" * 64},
            "profile": {"key": "example"},
            "catalogKind": "enemy",
            "catalogState": "identical_profile_already_present",
            "stageState": "ready_without_catalog_or_asset_stage",
            "routeReferences": [{"topologyGroup": "group", "coverageRouteKind": "directEnemy"}],
        }
        second = {
            **first,
            "profileDocument": {"path": "two.json", "sha256": "2" * 64},
        }
        selections = {
            "schemaVersion": 1,
            "selections": [{
                "topologyGroup": "group",
                "routeKind": "directEnemy",
                "key": "example",
                "profileDocument": second["profileDocument"],
                "motionRendererPath": "body",
                "workflow": "passive_enemy_arrival",
                "arrivalLevel": 0,
                "arrivalRoom": 2,
                "reason": "Current reviewed revision.",
            }],
        }
        choice = stage.route_choices(queue, [first, second], selections)[0]
        self.assertEqual(choice["selectedRevision"]["profileDocument"], second["profileDocument"])
        self.assertEqual(choice["selectedMotionRendererPath"], "body")
        self.assertEqual(choice["selectedWorkflow"], "passive_enemy_arrival")
        self.assertEqual((choice["arrivalLevel"], choice["arrivalRoom"]), (0, 2))
        self.assertEqual(choice["selectionReason"], "Current reviewed revision.")
        self.assertEqual(len(choice["candidateRevisions"]), 2)


if __name__ == "__main__":
    unittest.main()
