from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

import jsonschema


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
import validate_custom_model_profile_route as direct_preflight
import validate_player_model_profile_route as player_preflight
import validate_resource_model_profile_route as resource_preflight
from local_inputs import require_local_inputs

SCRIPT = ROOT / "tools/ai-model-pipeline/plan_model_authoring_kit.py"
SPEC = importlib.util.spec_from_file_location("plan_model_authoring_kit", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
authoring = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = authoring
SPEC.loader.exec_module(authoring)


LOCAL_INPUTS = (
    "scratch/model-validation-stage-readiness.json",
    "scratch/mirewarden-game",
    "scratch/skeleton-inventory-reproducible.json",
    "scratch/blender-all-rigs/bridge-audit.json",
    "scratch/blender-apparel-rigs-v1/bridge-audit.json",
)


class CurrentModelAuthoringKitTests(unittest.TestCase):
    _context: dict | None = None

    @property
    def context(self) -> dict:
        # Loaded lazily so the output-path test still runs on a clone without scratch data.
        require_local_inputs(*LOCAL_INPUTS)
        if type(self)._context is None:
            type(self)._context = authoring.load_context(
                ROOT,
                ROOT / "docs/model-validation-execution-queue.json",
                ROOT / "scratch/model-validation-stage-readiness.json",
                ROOT / "scratch/mirewarden-game",
                ROOT / "scratch/skeleton-inventory-reproducible.json",
                ROOT / "scratch/blender-all-rigs/bridge-audit.json",
                [ROOT / "scratch/blender-apparel-rigs-v1/bridge-audit.json"],
            )
        return type(self)._context

    def test_output_is_confined_to_direct_scratch_json(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            (root / "scratch").mkdir()
            self.assertEqual(authoring.output_path(root, Path("scratch/kit.json")), root / "scratch/kit.json")
            with self.assertRaisesRegex(ValueError, "directly under"):
                authoring.output_path(root, Path("kit.json"))
            with self.assertRaisesRegex(ValueError, "directly under"):
                authoring.output_path(root, Path("scratch/nested/kit.json"))

    def test_every_backlog_route_resolves_to_exact_authoring_targets(self) -> None:
        routes = self.context["queue"]["routes"]
        self.assertEqual(routes, [])
        for route in routes:
            group = route["topologyGroup"]
            plan = authoring.build_route_kit(ROOT, self.context, group, route["routeKind"])
            self.assertEqual(plan["route"]["topologyGroup"], group)
            self.assertTrue(plan["targets"])
            for target in plan["targets"]:
                self.assertTrue(target["topologyFingerprint"].startswith(group))
                self.assertEqual(target["verifiedBlenderBridge"]["status"], "offline_blender_bridge_pass")
                self.assertTrue(target["existingLocalReference"]["identityVerifiedAgainstInventory"])
                self.assertIn("NEW_LABEL", target["freshReferenceOutput"])
                self.assertIn("--renderer-id", target["extractCommand"])
                self.assertTrue(any(value.endswith("create_blender_template.py") for value in target["scaffoldCommand"]))

    def test_catalog_covers_every_route_without_missing_authoring_targets(self) -> None:
        catalog = authoring.build_catalog(ROOT, self.context)
        self.assertEqual(catalog["summary"]["routes"], 0)
        self.assertEqual(catalog["summary"]["routesWithoutAuthoringTargets"], 0)
        self.assertEqual(catalog["summary"]["routesWithAllExistingLocalReferences"], 0)
        self.assertEqual(len(catalog["routes"]), 0)
        self.assertEqual(catalog["summary"]["rendererTargets"], 0)
        self.assertEqual(catalog["summary"]["companionRigTargets"], 0)
        self.assertEqual(catalog["summary"]["uniqueCompanionRendererTargets"], 0)
        self.assertEqual(catalog["summary"]["apparelRigTargets"], 0)
        self.assertEqual(catalog["summary"]["uniqueApparelRendererTargets"], 0)
        self.assertEqual(catalog["summary"]["allUniqueRigTargets"], 0)
        self.assertEqual(catalog["summary"]["routesWithAllRequiredLocalReferences"], 0)
        self.assertEqual(catalog["summary"]["verifiedRigBridgeProfiles"], 0)
        self.assertGreaterEqual(
            catalog["summary"]["allVerifiedRigBridgeProfiles"],
            catalog["summary"]["verifiedRigBridgeProfiles"],
        )
        self.assertEqual(catalog["summary"]["allVerifiedRigBridgeProfiles"], 0)

    def test_every_stageable_route_has_a_schema_valid_integration_starter(self) -> None:
        require_local_inputs(
            "scratch/enemy-rig-mapping-reproducible.json",
            "scratch/static-renderer-inventory.json",
            "scratch/resource-enemy-base-preflight.json",
            "scratch/rig-candidate-classification.json",
        )
        enemy_schema = json.loads((ROOT / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json").read_text())
        player_schema = json.loads((ROOT / "tools/ai-model-pipeline/runtime-test-content/player-profiles.schema.json").read_text())
        enemy_mapping = json.loads((ROOT / "scratch/enemy-rig-mapping-reproducible.json").read_text())
        static_inventory = json.loads((ROOT / "scratch/static-renderer-inventory.json").read_text())
        resource_rows = json.loads((ROOT / "scratch/resource-enemy-base-preflight.json").read_text())
        player_classification = json.loads((ROOT / "scratch/rig-candidate-classification.json").read_text())
        catalog = authoring.build_catalog(ROOT, self.context)
        for route in catalog["routes"]:
            integration = route["integration"]
            if route["route"]["nextStagingAction"] in authoring.ADAPTER_ACTIONS:
                self.assertIsNone(integration["profileTemplate"])
                self.assertIsNone(integration["commands"])
                continue
            schema = player_schema if route["route"]["routeKind"] == "playerSkinset" else enemy_schema
            jsonschema.validate(integration["profileTemplate"], schema)
            if route["route"]["routeKind"] == "directEnemy":
                semantic = direct_preflight.validate_document(
                    enemy_mapping, integration["profileTemplate"], None, static_inventory
                )
                self.assertEqual(semantic["status"], "PASS_EXACT_DIRECT_ROUTE_STATIC_PREFLIGHT")
            elif route["route"]["routeKind"] == "resourcePrefab":
                semantic = resource_preflight.validate_document(resource_rows, integration["profileTemplate"], None)
                self.assertEqual(semantic["status"], "PASS_EXACT_RESOURCE_PREFAB_ROUTE_STATIC_PREFLIGHT")
            else:
                semantic = player_preflight.validate_document(player_classification, integration["profileTemplate"], None)
                self.assertEqual(semantic["status"], "PASS_EXACT_PLAYER_SKINSET_ROUTE_STATIC_PREFLIGHT")
            profile = integration["profileTemplate"]["profiles"][0]
            expected_key = (
                "ftkmf_modeltest_player_replace_unique_key"
                if route["route"]["routeKind"] == "playerSkinset"
                else "ftkmf_modeltest_replace_with_unique_key"
            )
            self.assertEqual(profile["key"], expected_key)
            self.assertEqual(profile["displayName"], "Replace With Original Model Name")
            self.assertIn("--output", integration["commands"]["preflightCommand"])
            self.assertIn("--output", integration["commands"]["stageCommand"])
            for assignment in [*profile.get("renderers", []), *profile.get("apparel", [])]:
                self.assertTrue(assignment["glbFile"].startswith("original-model-"))
                if assignment.get("textureFile") is not None:
                    self.assertTrue(assignment["textureFile"].startswith("original-texture-"))
                for slot in assignment.get("materialSlots", []):
                    if slot.get("textureFile") is not None:
                        self.assertTrue(slot["textureFile"].startswith("original-texture-"))

            skinned_paths = {
                assignment["rendererPath"]
                for assignment in profile.get("renderers", [])
                if assignment.get("rendererKind", "SkinnedMeshRenderer") == "SkinnedMeshRenderer"
            }
            covered_paths = {
                target["rendererPath"]
                for target in [*route["targets"], *integration["companionRigTargets"]]
            }
            self.assertEqual(covered_paths, skinned_paths)
            for target in integration["companionRigTargets"]:
                self.assertEqual(target["rendererKind"], "SkinnedMeshRenderer")
                self.assertEqual(target["verifiedBlenderBridge"]["status"], "offline_blender_bridge_pass")
                self.assertTrue(target["existingLocalReference"]["identityVerifiedAgainstInventory"])
            apparel_paths = {assignment["rendererPath"] for assignment in profile.get("apparel", [])}
            self.assertEqual(
                {target["rendererPath"] for target in integration["apparelRigTargets"]},
                apparel_paths,
            )
            for target in integration["apparelRigTargets"]:
                self.assertTrue(target["conditionalApparel"])
                self.assertEqual(target["expectedNativeMeshName"], target["meshName"])
                self.assertEqual(target["verifiedBlenderBridge"]["status"], "offline_blender_bridge_pass")
                self.assertEqual(
                    target["verifiedBlenderBridge"]["audit"]["path"],
                    "scratch/blender-apparel-rigs-v1/bridge-audit.json",
                )
                self.assertTrue(target["existingLocalReference"]["identityVerifiedAgainstInventory"])

    def test_completed_player_route_is_absent_from_backlog_authoring(self) -> None:
        with self.assertRaisesRegex(ValueError, "expected one queue route"):
            authoring.build_route_kit(ROOT, self.context, "45c7a9b9fb730195", "playerSkinset")

    def test_canonical_adapter_route_is_absent_from_backlog_authoring(self) -> None:
        routes = self.context["queue"]["routes"]
        self.assertFalse(any(row["topologyGroup"] == "6a28ac3cf4523c24" for row in routes))
        self.assertFalse(any(row["topologyGroup"] in {"4082b6c4e777f922", "85c742f628ea6d37"} for row in routes))


if __name__ == "__main__":
    unittest.main()
