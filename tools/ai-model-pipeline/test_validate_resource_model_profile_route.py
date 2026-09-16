from __future__ import annotations

import copy
import unittest

import validate_resource_model_profile_route as route
from prepare_resource_profiles import resource_combat_profile


RIG = "b" * 64


def resource_row() -> dict:
    return {
        "resource_load_path": "enbaseywolf",
        "game_object_id": 19201,
        "cel_ids": [137372],
        "renderer_id": 120975,
        "renderer_path": "Wolfie",
        "rig_profile_fingerprint": RIG,
        "base_enemy_id": "wolfA",
        "weapon_controller": {
            "controller_path_id": 6007,
            "controller_asset_file": "resources.assets",
            "controller_name": "wolfController",
            "weapon_holder_name": "WEAPON_HOLDER",
            "serialized_pointer_offset": 584,
        },
        "root_components": ["Transform", "Animator", "CharacterEventListener"],
        "has_Root_M": True,
        "weapon_holder_present": True,
        "new_missing_vs_base": [],
    }


def preflight() -> dict:
    return {"rows": [resource_row()]}


def document() -> dict:
    row = resource_row()
    return {"profiles": [{
        "key": "ftkmf_modeltest_mirewolf_resource",
        "baseEnemy": "wolfA",
        "resourcePrefab": "enbaseywolf",
        "displayName": "Mire Wolf",
        "combatProfile": resource_combat_profile(row),
        "renderers": [{"rendererPath": "Wolfie", "glbFile": "mirewolf.glb"}],
    }]}


class ResourceRoutePreflightTests(unittest.TestCase):
    def test_resource_combat_fingerprint_is_stable(self) -> None:
        self.assertEqual(
            resource_combat_profile(resource_row()),
            "9ffccf6b581c8557e7bc03d2ad53a77af15f5334afaa860156f0fb210c834140",
        )

    def test_exact_resource_route_is_reported(self) -> None:
        report = route.validate_document(preflight(), document())
        self.assertEqual(report["status"], "PASS_EXACT_RESOURCE_PREFAB_ROUTE_STATIC_PREFLIGHT")
        target = report["profiles"][0]
        self.assertEqual(target["resourcePrefab"], "enbaseywolf")
        self.assertEqual(target["renderer"]["sourceRendererId"], 120975)

    def test_mismatched_base_enemy_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["baseEnemy"] = "wolfB"
        with self.assertRaisesRegex(ValueError, "base enemy mismatch"):
            route.validate_document(preflight(), invalid)

    def test_wrong_renderer_path_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["renderers"][0]["rendererPath"] = "wrong"
        with self.assertRaisesRegex(ValueError, "renderer path mismatch"):
            route.validate_document(preflight(), invalid)

    def test_mismatched_combat_profile_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["combatProfile"] = "c" * 64
        with self.assertRaisesRegex(ValueError, "combat profile mismatch"):
            route.validate_document(preflight(), invalid)

    def test_direct_profile_is_rejected_from_resource_preflight(self) -> None:
        invalid = copy.deepcopy(document())
        del invalid["profiles"][0]["resourcePrefab"]
        with self.assertRaisesRegex(ValueError, "direct-enemy profile"):
            route.validate_document(preflight(), invalid)

    def test_new_binding_gap_is_rejected(self) -> None:
        invalid = preflight()
        invalid["rows"][0]["new_missing_vs_base"] = [123]
        with self.assertRaisesRegex(ValueError, "binding gaps"):
            route.validate_document(invalid, document())


if __name__ == "__main__":
    unittest.main()
