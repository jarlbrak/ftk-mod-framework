"""Focused source-route coverage for the topology audit."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

import audit_topology_coverage as audit


class TopologyCoverageTests(unittest.TestCase):
    def write(self, root: Path, name: str, value: object) -> Path:
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value))
        return path

    def test_resource_override_and_explicit_player_route_are_not_unresolved(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original = self.write(root, "art/validation.json", {
                "profile": {"baseEnemy": "snake", "resourcePrefab": "oldsnake", "renderers": [{"rendererPath": "oldBody"}]},
            })
            player = self.write(root, "art/player.json", {
                "profile": {"key": "player_key", "skinset": "Female", "renderers": [{"rendererPath": "body"}], "apparel": []},
            })
            candidates = {"candidate_families": [
                {"topology_fingerprint": "resource", "joint_count": 3, "renderer_count": 1, "renderer_path_ids": [10], "mesh_names": ["oldBody"], "controller_names": ["old"]},
                {"topology_fingerprint": "player", "joint_count": 4, "renderer_count": 1, "renderer_path_ids": [20], "mesh_names": ["body"], "controller_names": ["player"]},
            ]}
            mapping = {"enemies": [{"enemy_id": "snake", "prefab_name": "native", "weapon_animation_controller_candidates": [{"controller_path_id": 3, "controller_name": "ctrl"}], "renderers": []}]}
            resources = {"rows": [{"base_enemy_id": "snake", "resource_load_path": "oldsnake", "renderer_id": 10, "renderer_path": "oldBody", "rig_profile_fingerprint": "rig", "weapon_controller": {"controller_path_id": 3, "controller_name": "ctrl"}}]}
            index = {
                "calibration_probes": [],
                "enemy_models": [{"name": "old", "evidence": {"path": str(original.relative_to(root))}}],
                "original_player_models": [{"name": "player", "status": "partial", "evidence": {"archive": {"path": str(player.relative_to(root))}}}],
            }
            ownership = {"classifications": [{"topologyGroup": "player", "category": "player_avatar", "status": "player_avatar_no_enemy_row", "summary": "player-only", "profileKeys": ["player_key"], "rendererPaths": ["body"], "remainingChecks": ["preview"]}]}
            inputs = [self.write(root, "inputs/candidates.json", candidates), self.write(root, "inputs/mapping.json", mapping), self.write(root, "inputs/index.json", index), self.write(root, "inputs/resources.json", resources), self.write(root, "inputs/catalog.json", {"profiles": []}), self.write(root, "inputs/ownership.json", ownership)]
            plan = audit.build_plan(root, candidates, mapping, index, resources, {"profiles": []}, ownership, inputs)
            resource_group, player_group = plan["groups"]
            self.assertEqual(resource_group["nativeDirectEnemyPairs"], 0)
            self.assertEqual(resource_group["nativeResourcePrefabPairs"], 1)
            self.assertEqual(resource_group["exactSourcePairsWithIndexedOriginalEvidence"], 1)
            self.assertEqual(resource_group["exactSourcePairsWithIndexedEvidence"], 1)
            self.assertEqual(player_group["status"], "player_avatar_no_enemy_row")
            self.assertEqual(len(player_group["playerEvidence"]), 1)
            self.assertEqual(plan["summary"]["unresolvedOwnershipGroups"], 0)

    def test_unknown_zero_pair_group_stays_explicitly_unresolved(self) -> None:
        candidates = {"candidate_families": [{"topology_fingerprint": "unknown", "joint_count": 1, "renderer_count": 1, "renderer_path_ids": [99], "mesh_names": ["unknown"], "controller_names": []}]}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = [self.write(root, name, value) for name, value in [
                ("candidates.json", candidates), ("mapping.json", {"enemies": []}), ("index.json", {"calibration_probes": [], "enemy_models": [], "original_player_models": []}), ("resources.json", {"rows": []}), ("catalog.json", {"profiles": []}), ("ownership.json", {"classifications": []}),
            ]]
            plan = audit.build_plan(root, candidates, {"enemies": []}, {"calibration_probes": [], "enemy_models": [], "original_player_models": []}, {"rows": []}, {"profiles": []}, {"classifications": []}, inputs)
            self.assertEqual(plan["groups"][0]["status"], "unresolved_ownership_no_native_enemy_row")
            self.assertEqual(plan["summary"]["unresolvedOwnershipGroups"], 1)

    def test_calibration_only_evidence_does_not_count_as_an_original_model(self) -> None:
        candidates = {"candidate_families": [{
            "topology_fingerprint": "direct", "joint_count": 1, "renderer_count": 1,
            "renderer_path_ids": [10], "mesh_names": ["body"], "controller_names": ["ctrl"],
        }]}
        mapping = {"enemies": [{
            "enemy_id": "snake", "prefab_name": "native",
            "weapon_animation_controller_candidates": [{"controller_path_id": 3, "controller_name": "ctrl"}],
            "renderers": [{"ancestor_names": ["body", "native"], "renderer_path_id": 10, "rig_profile_fingerprint": "rig"}],
        }]}
        index = {"calibration_probes": [{
            "native_chassis": "snake", "rendererPaths": ["body"], "sourceRendererIds": [10],
        }], "enemy_models": [], "original_player_models": []}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = [self.write(root, name, value) for name, value in [
                ("candidates.json", candidates), ("mapping.json", mapping), ("index.json", index),
                ("resources.json", {"rows": []}), ("catalog.json", {"profiles": []}),
                ("ownership.json", {"classifications": []}),
            ]]
            plan = audit.build_plan(root, candidates, mapping, index, {"rows": []}, {"profiles": []},
                                    {"classifications": []}, inputs)
            group = plan["groups"][0]
            self.assertEqual(group["exactSourcePairsWithIndexedEvidence"], 1)
            self.assertEqual(group["exactSourcePairsWithIndexedOriginalEvidence"], 0)
            self.assertEqual(plan["summary"]["groupsWithAnyIndexedOriginalDirectEnemyEvidence"], 0)


if __name__ == "__main__":
    unittest.main()
