#!/usr/bin/env python3
"""Focused coverage for player-archive reconciliation in the original-model audit."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import audit_original_model_index as audit


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class PlayerArchiveAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.artifact = self.root / "art-experiments" / "hearthveil" / "live-validation-v1" / "validation.json"
        self.profile = {
            "key": "ftkmf_modeltest_player_hearthveil_blacksmith_female",
            "baseClass": "blacksmith",
            "skinset": "blacksmith_Female",
            "defaultSkinType": "Female",
            "renderers": [{"rendererPath": "playerBlacksmith"}],
            "apparel": [{"rendererPath": "armorGambesonF(Clone)"}],
        }
        write(self.artifact, {"status": "preview_pending", "profile": self.profile})
        self.mapping = self.root / "scratch" / "mapping.json"
        self.resources = self.root / "scratch" / "resources.json"
        write(self.mapping, {"enemies": []})
        write(self.resources, {"rows": []})

    def tearDown(self) -> None:
        self.temp.cleanup()

    def report(self, player_entries: list[dict]) -> dict:
        index = self.root / "docs" / "model-runtime-validation.json"
        write(index, {"enemy_models": [], "original_player_models": player_entries})
        return audit.audit(self.root, index, self.mapping, self.resources, self.root / "art-experiments")

    def test_indexed_player_archive_is_not_an_unresolved_enemy_artifact(self) -> None:
        entry = {
            "profile": self.profile["key"],
            "skinset": self.profile["skinset"],
            "evidence": {"archive": {"path": str(self.artifact.relative_to(self.root)), "sha256": digest(self.artifact)}},
        }
        report = self.report([entry])
        self.assertEqual(report["summary"]["directlyIndexedPlayerArtifacts"], 1)
        self.assertEqual(report["summary"]["unindexedPlayerArtifacts"], 0)
        self.assertEqual(report["summary"]["unresolvedArtifacts"], 0)
        self.assertEqual(report["summary"]["playerIndexEntriesWithoutDeclaredArchiveIdentity"], 0)

    def test_unindexed_player_archive_is_reported_separately(self) -> None:
        report = self.report([])
        self.assertEqual(report["summary"]["directlyIndexedPlayerArtifacts"], 0)
        self.assertEqual(report["summary"]["unindexedPlayerArtifacts"], 1)
        self.assertEqual(report["summary"]["unresolvedArtifacts"], 0)
        self.assertEqual(report["unindexedPlayerArtifacts"][0]["profile"], self.profile["key"])

    def test_nested_enemy_followup_path_is_an_explicit_index_reference(self) -> None:
        entry = {
            "evidence": {"path": "art-experiments/example/live-validation.json", "sha256": "a" * 64},
            "ordinary_lethal_followup": {
                "path": "art-experiments/example/live-validation-ordinary-lethal.json",
                "sha256": "b" * 64,
            },
        }
        self.assertEqual(audit.indexed_entry_paths(self.root, entry), {
            "art-experiments/example/live-validation.json",
            "art-experiments/example/live-validation-ordinary-lethal.json",
        })

    def test_explicit_nonruntime_fixture_is_not_an_unresolved_original_archive(self) -> None:
        fixture = self.root / "art-experiments" / "gloamfin" / "live-validation-fixture.json"
        write(fixture, {"status": "constructed_fixture_only", "scope": "not a runtime model trial"})
        index = self.root / "docs" / "model-runtime-validation.json"
        write(index, {
            "enemy_models": [],
            "original_player_models": [],
            "kraken_original_skin_trials": [{
                "evidence": {"path": str(fixture.relative_to(self.root)), "sha256": digest(fixture)},
            }],
        })
        report = audit.audit(self.root, index, self.mapping, self.resources, self.root / "art-experiments")
        self.assertEqual(report["summary"]["separatelyIndexedNonRuntimeArtifacts"], 1)
        self.assertEqual(report["summary"]["unresolvedArtifacts"], 0)
        self.assertEqual(report["separatelyIndexedNonRuntimeArtifacts"][0]["path"],
                         str(fixture.relative_to(self.root)))

    def test_archive_plan_matching_live_validation_glob_is_not_runtime_evidence(self) -> None:
        plan = self.root / "art-experiments" / "hearthveil" / "live-validation-v2-plan.json"
        write(plan, {
            "schema": "ftkmf.model-validation-archive-plan.v1",
            "output": "art-experiments/hearthveil/live-validation-v2",
        })
        report = self.report([])
        self.assertEqual(report["summary"]["discoveredOriginalEvidenceArtifacts"], 0)
        self.assertEqual(report["summary"]["unresolvedArtifacts"], 0)


if __name__ == "__main__":
    unittest.main()
