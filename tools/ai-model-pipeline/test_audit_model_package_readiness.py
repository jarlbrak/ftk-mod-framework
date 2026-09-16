from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

import audit_model_package_readiness as audit


class PackageReadinessTests(unittest.TestCase):
    def test_archive_plan_is_not_a_local_runtime_validation_artifact(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = root / "art-experiments" / "example"
            package.mkdir(parents=True)
            plan = package / "live-validation-v2-plan.json"
            plan.write_text(json.dumps({
                "schema": "ftkmf.model-validation-archive-plan.v1",
                "output": "art-experiments/example/live-validation-v2",
            }))
            evidence = package / "live-validation-v1.json"
            evidence.write_text(json.dumps({"status": "reviewed"}))
            self.assertEqual(
                [row["path"] for row in audit.archive_refs(root, package)],
                ["art-experiments/example/live-validation-v1.json"],
            )

    def test_profile_kind_keeps_route_types_separate(self) -> None:
        self.assertEqual(audit.profile_kind({"profiles": [{"baseEnemy": "a"}]}), "direct")
        self.assertEqual(audit.profile_kind({"profiles": [{"baseEnemy": "a", "resourcePrefab": "res"}]}), "resource")
        self.assertEqual(audit.profile_kind({"profiles": [{"skinset": "player_Female"}]}), "player")
        with self.assertRaisesRegex(ValueError, "mixes route kinds"):
            audit.profile_kind({"profiles": [{"baseEnemy": "a"}, {"skinset": "player_Female"}]})

    def test_direct_entries_do_not_promote_diagnostic_or_static_records_to_original_evidence(self) -> None:
        report = {"profiles": [{
            "key": "ftkmf_modeltest_example",
            "baseEnemy": "exampleA",
            "declaredAssets": [{"file": "example.glb"}],
            "renderers": [
                {"rendererPath": "body", "sourceRendererId": 11, "rendererKind": "SkinnedMeshRenderer"},
                {"rendererPath": "eye", "sourceRendererId": 12, "rendererKind": "MeshRenderer",
                 "meshFilterCount": 1, "nativeMaterialSlotCount": 1},
            ],
        }]}
        matches = {
            ("exampleA", "", 11, "body"): [
                {"indexPointer": "/calibration_probes/0", "kind": "diagnostic"},
                {"indexPointer": "/enemy_models/0", "kind": "original"},
            ],
        }
        entry = audit.direct_entries(report, matches)[0]
        self.assertEqual(len(entry["indexedExactSourceEvidence"]), 2)
        self.assertEqual(entry["indexedExactOriginalEvidence"], [{"indexPointer": "/enemy_models/0", "kind": "original"}])
        static = next(item for item in entry["assignments"] if item["rendererKind"] == "MeshRenderer")
        self.assertEqual(static["indexedExactSourceEvidence"], [])
        self.assertEqual(static["strictStaticPreflight"], {"meshFilterCount": 1, "nativeMaterialSlotCount": 1})

    def test_resource_entries_require_original_index_kind(self) -> None:
        report = {"profiles": [{
            "key": "ftkmf_modeltest_resource",
            "baseEnemy": "wolfA",
            "resourcePrefab": "enbaseywolf",
            "renderer": {"rendererPath": "Wolfie", "sourceRendererId": 99},
            "declaredAssets": [],
        }]}
        matches = {("wolfA", "enbaseywolf", 99, "Wolfie"): [{"kind": "diagnostic"}]}
        entry = audit.resource_entries(report, matches)[0]
        self.assertTrue(entry["indexedExactSourceEvidence"])
        self.assertEqual(entry["indexedExactOriginalEvidence"], [])

    def test_direct_index_references_are_kept_even_without_a_route_join(self) -> None:
        index = {"enemy_models": [{
            "status": "recorded",
            "evidence": {"path": "art-experiments/example/live-validation.json", "sha256": "a" * 64},
            "ordinary_lethal_followup": {
                "path": "art-experiments/example/live-validation-ordinary-lethal.json",
                "sha256": "b" * 64,
            },
        }]}
        references = audit.runtime_index_artifact_references(index)
        self.assertEqual(references["art-experiments/example/live-validation.json"][0]["indexPointer"],
                         "/enemy_models/0")
        self.assertEqual(references["art-experiments/example/live-validation.json"][0]["status"], "recorded")
        followup = references["art-experiments/example/live-validation-ordinary-lethal.json"][0]
        self.assertEqual(followup["indexPointer"], "/enemy_models/0")
        self.assertEqual(followup["evidencePointer"], "/enemy_models/0/ordinary_lethal_followup")

    def test_markdown_uses_original_evidence_count_for_next_action(self) -> None:
        report = {
            "summary": {},
            "packages": [{
                "package": "art-experiments/example",
                "profileDocument": {"path": "art-experiments/example/runtime-profile.json"},
                "routeKind": "direct",
                "preflight": {"status": "pass"},
                "profiles": [{"indexedExactSourceEvidence": [{"kind": "diagnostic"}],
                              "indexedExactOriginalEvidence": []}],
                "localValidationArtifacts": [],
                "manifest": {"rawStatus": "OFFLINE_READY"},
            }],
        }
        rendered = audit.markdown(report)
        self.assertIn("1 / 0", rendered)
        self.assertIn("fresh isolated trial", rendered)

    def test_fresh_trial_queue_keeps_each_unvalidated_exact_profile_separate(self) -> None:
        packages = [{
            "package": "art-experiments/example",
            "profileDocument": {"path": "art-experiments/example/runtime-profile.json", "sha256": "a" * 64},
            "preflight": {"status": "pass"},
            "profiles": [
                {
                    "key": "ftkmf_modeltest_boss",
                    "routeKind": "resource_prefab_override",
                    "baseEnemy": "bossCockatrice",
                    "resourcePrefab": "enbaseycockatriceboss",
                    "assignments": [{"rendererPath": "enBaseyCockatrice", "sourceRendererId": 11,
                                     "rendererKind": "SkinnedMeshRenderer"}],
                    "indexedExactOriginalEvidence": [],
                },
                {
                    "key": "ftkmf_modeltest_small",
                    "routeKind": "resource_prefab_override",
                    "baseEnemy": "cockatriceC",
                    "resourcePrefab": "enbaseycockatricesmall",
                    "assignments": [{"rendererPath": "enBaseyCockatrice", "sourceRendererId": 12,
                                     "rendererKind": "SkinnedMeshRenderer"}],
                    "indexedExactOriginalEvidence": [{"kind": "original"}],
                },
            ],
        }, {
            "package": "art-experiments/failed",
            "profileDocument": {"path": "art-experiments/failed/runtime-profile.json"},
            "preflight": {"status": "failed"},
            "profiles": [{"key": "ignored", "indexedExactOriginalEvidence": []}],
        }]
        candidates = audit.fresh_trial_candidates(packages)
        self.assertEqual(len(candidates), 1)
        self.assertEqual(candidates[0]["key"], "ftkmf_modeltest_boss")
        self.assertEqual(candidates[0]["assignments"], [{
            "rendererPath": "enBaseyCockatrice", "sourceRendererId": 11,
            "rendererKind": "SkinnedMeshRenderer",
        }])


if __name__ == "__main__":
    unittest.main()
