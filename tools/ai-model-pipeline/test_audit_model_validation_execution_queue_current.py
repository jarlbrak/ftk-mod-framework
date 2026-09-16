"""Keep the generated execution queue tied to the current ledgers."""
from __future__ import annotations

import json
from pathlib import Path
import unittest

import audit_model_validation_execution_queue as queue


ROOT = Path(__file__).resolve().parents[2]


class CurrentExecutionQueueTests(unittest.TestCase):
    def report(self) -> dict:
        topology = ROOT / "docs/topology-coverage.json"
        coverage = ROOT / "docs/model-candidate-validation-coverage.json"
        readiness = ROOT / "docs/model-package-readiness.json"
        return queue.build_report(
            json.loads(topology.read_text()),
            json.loads(coverage.read_text()),
            json.loads(readiness.read_text()),
            [
                queue.input_reference(ROOT, topology),
                queue.input_reference(ROOT, coverage),
                queue.input_reference(ROOT, readiness),
            ],
            queue.discover_adapter_contracts(ROOT),
            queue.discover_adapter_campaigns(ROOT),
        )

    def test_generated_json_matches_current_inputs(self) -> None:
        report = self.report()
        generated = json.loads((ROOT / "docs/model-validation-execution-queue.json").read_text())
        self.assertEqual(generated, report)
        self.assertEqual(report["summary"]["backlogRoutes"], 0)
        self.assertEqual(report["summary"]["routesWithPreflightedProfileCandidates"], 0)
        self.assertEqual(report["summary"]["routesWithoutPreflightedProfileCandidates"], 0)
        self.assertEqual(report["summary"]["selectedValidationTargetsWithoutPreflightedProfile"], 0)
        self.assertEqual(report["summary"]["routesRequiringProfileAuthoring"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterOrRetargetDesign"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterImplementation"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterValidation"], 0)
        self.assertEqual(report["summary"]["routesRequiringAdapterVisualArchiveReview"], 0)

    def test_completed_exact_renderer_topologies_are_no_longer_queued(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] in {
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
                "aae2ba644a16c223",
                "c3487d422b832d2e",
                "c3683bc2e807b15a",
                "e45711bff451ca73",
                "d1de8c46112a77d8",
                "1eda40629ee9aac9",
                "7b140c07befa33fd",
                "81f02cdbf3eefcb9",
            }
            for item in self.report()["routes"]
        ))

    def test_tamarind_is_no_longer_queued_after_canonical_passive_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "81f02cdbf3eefcb9"
            for item in self.report()["routes"]
        ))

    def test_kraken_route_is_no_longer_queued_after_canonical_archive(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "6a28ac3cf4523c24"
            for item in self.report()["routes"]
        ))

    def test_copperveil_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "23c62612fd16b533"
            for item in self.report()["routes"]
        ))

    def test_reefstrider_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "5990c51ca004ebdb"
            for item in self.report()["routes"]
        ))

    def test_basilight_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "678c8066b33cf823"
            for item in self.report()["routes"]
        ))

    def test_mossglass_is_no_longer_queued_after_canonical_v5(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "6d40f2e6eba19a6b"
            for item in self.report()["routes"]
        ))

    def test_mirewarden_and_gloamcap_routes_are_no_longer_queued(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "db2a524a5bc700ea"
            for item in self.report()["routes"]
        ))

    def test_thistlewick_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "f50b09e31a8484cf"
            for item in self.report()["routes"]
        ))

    def test_verdigrin_is_no_longer_queued_after_canonical_v4(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "cace272650590c4f"
            for item in self.report()["routes"]
        ))

    def test_sunspire_roc_is_no_longer_queued_after_canonical_v4(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "0f29e98795c302a5"
            for item in self.report()["routes"]
        ))

    def test_belladusk_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "1329d6985dadecee"
            for item in self.report()["routes"]
        ))

    def test_bronzehollow_is_no_longer_queued_after_canonical_v4(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "853432a7c217ff04"
            for item in self.report()["routes"]
        ))

    def test_rustpetal_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "b0b93182a11758ed"
            for item in self.report()["routes"]
        ))

    def test_amberwake_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "c01698c74bd54910"
            for item in self.report()["routes"]
        ))

    def test_rimecrown_head_is_no_longer_queued_after_canonical_v4(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "02a6f31412bd52ab"
            for item in self.report()["routes"]
        ))

    def test_rimecrown_scarf_is_no_longer_queued_after_canonical_v5(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "1322fec3354db550"
            for item in self.report()["routes"]
        ))

    def test_rimecrown_base_is_no_longer_queued_after_canonical_v6(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "8c73c366b064726a"
            for item in self.report()["routes"]
        ))

    def test_rimecrown_hat_is_no_longer_queued_after_canonical_v7(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "963e53f60643b796"
            for item in self.report()["routes"]
        ))

    def test_rimecrown_middle_body_is_no_longer_queued_after_canonical_v8(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "a158ab62f9dd430f"
            for item in self.report()["routes"]
        ))

    def test_honeyback_bearb_is_no_longer_queued_after_canonical_v3(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "07f911c982da0402"
            for item in self.report()["routes"]
        ))

    def test_duneshade_desert_a_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "80d61d495b6a93be"
            for item in self.report()["routes"]
        ))

    def test_emberglass_is_no_longer_queued_after_canonical_v4(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "6fdb7ff148451731"
            for item in self.report()["routes"]
        ))

    def test_ashfang_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "c3487d422b832d2e"
            for item in self.report()["routes"]
        ))

    def test_moonreed_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "c3683bc2e807b15a"
            for item in self.report()["routes"]
        ))

    def test_kraken_head_is_no_longer_queued_after_canonical_v5(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "e45711bff451ca73"
            for item in self.report()["routes"]
        ))

    def test_mournglass_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "d1de8c46112a77d8"
            for item in self.report()["routes"]
        ))

    def test_tidecrown_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "1eda40629ee9aac9"
            for item in self.report()["routes"]
        ))

    def test_bramblecoil_is_no_longer_queued_after_canonical_v2(self) -> None:
        self.assertFalse(any(
            item["topologyGroup"] == "7b140c07befa33fd"
            for item in self.report()["routes"]
        ))

    def test_generated_markdown_matches_current_report(self) -> None:
        self.assertEqual(
            (ROOT / "docs/MODEL-VALIDATION-EXECUTION-QUEUE.md").read_text(),
            queue.markdown(self.report()),
        )


if __name__ == "__main__":
    unittest.main()
