from __future__ import annotations

import copy
import tempfile
import unittest
from pathlib import Path

import validate_custom_model_profile_route as route


FINGERPRINT = "a" * 64
RIG = "b" * 64


def mapping() -> dict:
    return {
        "enemies": [{
            "enemy_id": "acidBlobB",
            "prefab_name": "enAcidMonsterB",
            "cel_path_id": 9,
            "weapon_animation_controller_candidates": [{"controller_path_id": 5931, "controller_name": "AcidBlobController"}],
            "renderers": [{
                "renderer_path_id": 121345,
                "renderer_name": "enAcidMonster",
                "ancestor_names": ["enAcidMonster", "enAcidMonsterB"],
                "character_event_listeners": [{"component_path_id": 9, "game_object": "enAcidMonsterB"}],
                "rig_profile_fingerprint": RIG,
                "combat_profile_fingerprint": FINGERPRINT,
                "animators": [{"controller_path_id": 5931}],
            }],
        }],
    }


def document() -> dict:
    return {"profiles": [{
        "key": "ftkmf_modeltest_mireglass_croaker_acidblob_b",
        "baseEnemy": "acidBlobB",
        "displayName": "Mireglass Croaker",
        "combatProfile": FINGERPRINT,
        "renderers": [{"rendererPath": "enAcidMonster", "glbFile": "mireglass.glb"}],
    }]}


def static_inventory() -> dict:
    return {
        "renderers": [{
            "nativeEnemyCandidates": ["acidBlobB"],
            "rendererPath": "Eye",
            "rendererId": 121347,
            "rendererKind": "MeshRenderer",
            "meshFilterCount": 1,
            "nativeMaterialSlotCount": 1,
            "strictStaticEligible": True,
        }],
    }


class RoutePreflightTests(unittest.TestCase):
    def test_exact_direct_route_is_reported(self) -> None:
        report = route.validate_document(mapping(), document())
        self.assertEqual(report["status"], "PASS_EXACT_DIRECT_ROUTE_STATIC_PREFLIGHT")
        target = report["profiles"][0]["renderers"][0]
        self.assertEqual(target["sourceRendererId"], 121345)
        self.assertEqual(target["combatProfile"], FINGERPRINT)

    def test_mismatched_combat_profile_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["combatProfile"] = "c" * 64
        with self.assertRaisesRegex(ValueError, "combat profile mismatch"):
            route.validate_document(mapping(), invalid)

    def test_unknown_renderer_path_is_rejected(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["renderers"][0]["rendererPath"] = "wrong"
        with self.assertRaisesRegex(ValueError, "unmapped renderer path"):
            route.validate_document(mapping(), invalid)

    def test_resource_override_is_rejected_from_direct_preflight(self) -> None:
        invalid = copy.deepcopy(document())
        invalid["profiles"][0]["resourcePrefab"] = "enacidblob"
        with self.assertRaisesRegex(ValueError, "resource-prefab"):
            route.validate_document(mapping(), invalid)

    def test_shared_palette_is_reported_once_for_multipart_profile(self) -> None:
        multi = copy.deepcopy(document())
        multi["profiles"][0]["renderers"].append({
            "rendererPath": "enAcidMonster",
            "glbFile": "second.glb",
            "textureFile": "shared.png",
        })
        # Give the synthetic native row a second uniquely addressable renderer
        # while preserving its exact controller/combat fingerprint.
        second = copy.deepcopy(mapping()["enemies"][0]["renderers"][0])
        second["renderer_path_id"] = 121346
        second["renderer_name"] = "enAcidMonsterEyes"
        second["ancestor_names"] = ["enAcidMonsterEyes", "enAcidMonsterB"]
        multi["profiles"][0]["renderers"][1]["rendererPath"] = "enAcidMonsterEyes"
        native = mapping()
        native["enemies"][0]["renderers"].append(second)
        # The profile combines one exact target per renderer, so use the
        # framework helper rather than hard-coding its opaque fingerprint.
        from prepare_runtime_profiles import combined
        multi["profiles"][0]["combatProfile"] = combined([
            {"rendererPath": "enAcidMonster", "sourceRendererId": 121345,
             "rigProfile": RIG, "combatProfile": FINGERPRINT, "controllerIds": [5931]},
            {"rendererPath": "enAcidMonsterEyes", "sourceRendererId": 121346,
             "rigProfile": RIG, "combatProfile": FINGERPRINT, "controllerIds": [5931]},
        ])
        with tempfile.TemporaryDirectory() as temporary:
            assets = Path(temporary)
            for name in ("mireglass.glb", "second.glb", "shared.png"):
                (assets / name).write_bytes(name.encode("ascii"))
            # The first assignment did not name a palette before this test.
            multi["profiles"][0]["renderers"][0]["textureFile"] = "shared.png"
            report = route.validate_document(native, multi, assets)
        self.assertEqual([row["file"] for row in report["profiles"][0]["declaredAssets"]],
                         ["mireglass.glb", "second.glb", "shared.png"])

    def test_exact_static_child_requires_and_reports_pinned_inventory(self) -> None:
        profile = copy.deepcopy(document())
        profile["profiles"][0]["renderers"].append({
            "rendererPath": "Eye",
            "rendererKind": "MeshRenderer",
            "glbFile": "eye.glb",
        })
        with self.assertRaisesRegex(ValueError, "pinned static renderer inventory"):
            route.validate_document(mapping(), profile)
        report = route.validate_document(mapping(), profile, static_inventory=static_inventory())
        self.assertEqual(
            [(row["rendererPath"], row["rendererKind"], row["sourceRendererId"])
             for row in report["profiles"][0]["renderers"]],
            [("Eye", "MeshRenderer", 121347), ("enAcidMonster", "SkinnedMeshRenderer", 121345)],
        )
        self.assertEqual(report["profiles"][0]["renderers"][0]["meshFilterCount"], 1)

    def test_unselected_noneligible_static_child_does_not_block_a_skinned_profile(self) -> None:
        inventory = static_inventory()
        inventory["renderers"].append({
            "nativeEnemyCandidates": ["acidBlobB"],
            "rendererPath": "DecorativeDoubleMaterial",
            "rendererId": 121348,
            "rendererKind": "MeshRenderer",
            "meshFilterCount": 1,
            "nativeMaterialSlotCount": 2,
            "strictStaticEligible": False,
        })
        report = route.validate_document(mapping(), document(), static_inventory=inventory)
        self.assertEqual(report["profiles"][0]["renderers"][0]["rendererPath"], "enAcidMonster")

    def test_static_only_profile_is_rejected_without_a_motion_renderer(self) -> None:
        profile = copy.deepcopy(document())
        profile["profiles"][0]["renderers"] = [{
            "rendererPath": "Eye",
            "rendererKind": "MeshRenderer",
            "glbFile": "eye.glb",
        }]
        with self.assertRaisesRegex(ValueError, "static-only profile lacks"):
            route.validate_document(mapping(), profile, static_inventory=static_inventory())

    def test_static_inventory_must_pin_the_mapping_source(self) -> None:
        native = mapping()
        native["source_sha256"] = "a" * 64
        valid = {"mapping": {"sourceAssetSha256": "a" * 64}, "source": {"sha256": "a" * 64}}
        route.validate_static_inventory_source(native, valid)
        invalid = copy.deepcopy(valid)
        invalid["source"]["sha256"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "does not pin"):
            route.validate_static_inventory_source(native, invalid)


if __name__ == "__main__":
    unittest.main()
