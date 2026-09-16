"""Keep the generated package-readiness ledger tied to current profile documents."""
from __future__ import annotations

import json
from pathlib import Path
import unittest

import audit_model_package_readiness as readiness


ROOT = Path(__file__).resolve().parents[2]


class CurrentPackageReadinessTests(unittest.TestCase):
    def report(self) -> dict:
        mapping = ROOT / "scratch/enemy-rig-mapping-reproducible.json"
        resources = ROOT / "scratch/resource-enemy-base-preflight.json"
        classification = ROOT / "scratch/rig-candidate-classification.json"
        static_inventory = ROOT / "scratch/static-renderer-inventory.json"
        index = ROOT / "docs/model-runtime-validation.json"
        ownership = ROOT / "docs/evidence/nonenemy-topology-ownership-v1/findings.json"
        return readiness.build_report(
            ROOT,
            ROOT / "art-experiments",
            json.loads(mapping.read_text()),
            json.loads(resources.read_text()),
            json.loads(classification.read_text()),
            json.loads(static_inventory.read_text()),
            json.loads(index.read_text()),
            json.loads(ownership.read_text()),
            [mapping, resources, classification, static_inventory, index, ownership],
        )

    def test_generated_json_matches_current_inputs(self) -> None:
        report = self.report()
        generated = json.loads((ROOT / "docs/model-package-readiness.json").read_text())
        self.assertEqual(generated, report)
        self.assertEqual(report["summary"]["profileDocuments"], 55)
        self.assertEqual(report["summary"]["preflightFailedDocuments"], 0)
        self.assertEqual(report["summary"]["validatedProfileEntries"], 92)

    def test_legacy_profiles_are_exactly_preflighted(self) -> None:
        report = self.report()
        expected = {
            "art-experiments/cinderwing-bat/runtime-profile.json": ("batA", "enBat01", 121104),
            "art-experiments/mirewarden-ftk/runtime-profile.json": ("trollCaveA", "enTroll01", 121153),
            "art-experiments/bronzehollow-sentinel/runtime-profile.json": ("deathknightA", "deathKnight", 121217),
            "art-experiments/amberwake-dragon/runtime-profile.json": ("dragonFrost", "enDragon", 121561),
            "art-experiments/ashfang-wolf/runtime-profile.json": ("wolfA", "wolf01", 121142),
            "art-experiments/moonreed-sylph/runtime-profile.json": ("fairyA", "enFairy01", 121395),
            "art-experiments/mournglass-wraith/runtime-profile.json": ("chaosBeast", "enChaosBeast", 121008),
            "art-experiments/tamarind-trickster/runtime-profile.json": ("monkeyC", "enMonkeyBasey", 121301),
        }
        actual = {}
        for package in report["packages"]:
            document = package["profileDocument"]["path"]
            if document not in expected:
                continue
            self.assertEqual(package["preflight"]["status"], "pass")
            profile = package["profiles"][0]
            assignment = profile["assignments"][0]
            actual[document] = (profile["baseEnemy"], assignment["rendererPath"], assignment["sourceRendererId"])
        self.assertEqual(actual, expected)

    def test_generated_markdown_matches_current_report(self) -> None:
        self.assertEqual(
            (ROOT / "docs/MODEL-PACKAGE-READINESS.md").read_text(),
            readiness.markdown(self.report()),
        )


if __name__ == "__main__":
    unittest.main()
