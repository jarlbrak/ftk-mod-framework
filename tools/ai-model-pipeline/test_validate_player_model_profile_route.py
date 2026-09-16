from __future__ import annotations

import copy
import tempfile
import unittest
from pathlib import Path

import validate_player_model_profile_route as route


def classification() -> dict:
    return {
        "skinset_rows": [{
            "skinset_id": "blacksmith_Fish",
            "avatar_prefab_name": "Player_FishPerson",
            "avatar_cel_path_id": 139636,
            "avatar_asset_file": "resources.assets",
        }],
        "renderers": [
            {
                "positive_role": "player_skinset_avatar",
                "skinset_avatar_references": ["blacksmith_Fish"],
                "ancestor_names": ["playerFIsh", "Player_FishPerson"],
                "ancestor_component_evidence": [{"component_class": "CharacterEventListener", "component_path_id": 139636}],
                "renderer_path_id": 121366,
                "renderer_name": "playerFIsh",
                "rig_profile_fingerprint": "a" * 64,
                "joint_count": 25,
            },
            {
                "positive_role": "player_skinset_avatar",
                "skinset_avatar_references": ["blacksmith_Fish"],
                "ancestor_names": ["hairTop", "Player_FishPerson"],
                "ancestor_component_evidence": [{"component_class": "CharacterEventListener", "component_path_id": 139636}],
                "renderer_path_id": 121490,
                "renderer_name": "hairTop",
                "rig_profile_fingerprint": "b" * 64,
                "joint_count": 6,
            },
            {
                "positive_role": "player_skinset_avatar",
                "skinset_avatar_references": ["blacksmith_Fish"],
                "ancestor_names": ["hairBottom", "Player_FishPerson"],
                "ancestor_component_evidence": [{"component_class": "CharacterEventListener", "component_path_id": 139636}],
                "renderer_path_id": 121521,
                "renderer_name": "hairBottom",
                "rig_profile_fingerprint": "c" * 64,
                "joint_count": 7,
            },
        ],
    }


def document() -> dict:
    return {"profiles": [{
        "key": "ftkmf_modeltest_player_tideglass_fishsmith",
        "baseClass": "blacksmith",
        "displayName": "Tideglass Fishsmith",
        "skinset": "blacksmith_Fish",
        "defaultSkinType": "Fish",
        "renderers": [
            {"rendererPath": "playerFIsh", "glbFile": "body.glb", "textureFile": "palette.png"},
            {"rendererPath": "hairTop", "glbFile": "top.glb", "textureFile": "palette.png"},
            {"rendererPath": "hairBottom", "glbFile": "bottom.glb", "textureFile": "palette.png"},
        ],
    }]}


class PlayerRoutePreflightTests(unittest.TestCase):
    def test_exact_skinset_route_reports_all_native_renderers(self) -> None:
        report = route.validate_document(classification(), document())
        self.assertEqual(report["status"], "PASS_EXACT_PLAYER_SKINSET_ROUTE_STATIC_PREFLIGHT")
        profile = report["profiles"][0]
        self.assertEqual(profile["avatar"], {"prefab": "Player_FishPerson", "celPathId": 139636, "assetFile": "resources.assets"})
        self.assertEqual([row["rendererPath"] for row in profile["renderers"]], ["hairBottom", "hairTop", "playerFIsh"])
        self.assertEqual(next(row for row in profile["renderers"] if row["rendererPath"] == "playerFIsh")["sourceRendererId"], 121366)

    def test_shared_palette_is_pinned_once(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            assets = Path(temporary)
            for name in ("body.glb", "top.glb", "bottom.glb", "palette.png"):
                (assets / name).write_bytes(name.encode())
            report = route.validate_document(classification(), document(), assets)
        self.assertEqual([row["file"] for row in report["profiles"][0]["declaredAssets"]],
                         ["body.glb", "bottom.glb", "palette.png", "top.glb"])

    def test_missing_required_body_or_hair_renderer_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["renderers"].pop()
        with self.assertRaisesRegex(ValueError, "do not exactly match native skinset"):
            route.validate_document(classification(), invalid)

    def test_unknown_or_extra_player_renderer_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["renderers"][0]["rendererPath"] = "wrong"
        with self.assertRaisesRegex(ValueError, "do not exactly match native skinset"):
            route.validate_document(classification(), invalid)

    def test_skinset_without_classified_avatar_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["skinset"] = "blacksmith_Female"
        with self.assertRaisesRegex(ValueError, "unknown classified player skinset"):
            route.validate_document(classification(), invalid)

    def test_conditional_apparel_cannot_overlap_body_renderer(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["apparel"] = [{
            "rendererPath": "hairBottom",
            "expectedNativeMeshName": "armorBlacksmith",
            "glbFile": "armor.glb",
        }]
        with self.assertRaisesRegex(ValueError, "overlapping conditional apparel"):
            route.validate_document(classification(), invalid)


if __name__ == "__main__":
    unittest.main()
