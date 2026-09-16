import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONTRACT = ROOT / "docs/evidence/kraken-production-adapter-design-v1/contract.json"
QUEUE = ROOT / "docs/model-validation-execution-queue.json"
PACKAGE = ROOT / "art-experiments/gloamfin-kraken"


class KrakenProductionAdapterContractCurrentTests(unittest.TestCase):
    def test_contract_and_supporting_evidence_hashes_are_current(self):
        contract = json.loads(CONTRACT.read_text())
        contract_sha = hashlib.sha256(CONTRACT.read_bytes()).hexdigest()
        candidates = json.loads((ROOT / "docs/model-skeleton-candidates.json").read_text())
        ownership = json.loads((ROOT / "docs/evidence/nonenemy-topology-ownership-v1/findings.json").read_text())
        self.assertEqual(candidates["resource_prefab_followup"]["adapter_contract"]["sha256"], contract_sha)
        kraken = next(
            row for row in ownership["classifications"]
            if row["topologyGroup"] == "6a28ac3cf4523c24"
        )
        self.assertEqual(kraken["adapterContract"]["sha256"], contract_sha)
        evidence = {
            "decompiledCharacterEventListenerSha256": ROOT / "scratch/CharacterEventListener.analysis.cs",
            "productionStateCoverageReadmeSha256": ROOT / "docs/evidence/kraken-production-state-coverage-v1/README.md",
            "offlineAdapterAuditSha256": ROOT / "tools/ai-model-pipeline/audit_kraken_adapter.py",
            "ownedUnityAdapterFixtureSha256": ROOT / "tools/ai-model-pipeline/runtime-test/KrakenOldAdapterFixture.cs",
            "dedicatedInternalAdapterSourceSha256": ROOT / "FTKModFramework/Core/LegacyKrakenResourceAdapter.cs",
            "dedicatedInternalAdapterTestSha256": ROOT / "tools/ai-model-pipeline/test_kraken_production_adapter_implementation.py",
        }
        for key, path in evidence.items():
            self.assertTrue(path.is_file(), path)
            self.assertEqual(contract["evidencePins"][key], hashlib.sha256(path.read_bytes()).hexdigest())

    def test_contract_pins_the_exact_rejected_generic_route(self):
        contract = json.loads(CONTRACT.read_text())
        route = contract["route"]
        self.assertEqual(route["topologyGroup"], "6a28ac3cf4523c24")
        self.assertEqual(route["routeKind"], "resourcePrefab")
        self.assertEqual(route["resourcePrefab"], "enkrakenhead")
        self.assertEqual(route["sourceRendererId"], 121260)
        self.assertFalse(route["genericProfileAllowed"])
        self.assertEqual(contract["status"], "implementation_complete_validation_pending")
        self.assertFalse(contract["implementationBoundary"]["currentPublicApiImplementsAdapter"])
        self.assertTrue(contract["implementationBoundary"]["dedicatedInternalAdapterImplemented"])
        self.assertFalse((PACKAGE / "runtime-profile.json").exists())

    def test_execution_queue_omits_the_canonical_route(self):
        queue = json.loads(QUEUE.read_text())
        routes = [row for row in queue["routes"] if row["topologyGroup"] == "6a28ac3cf4523c24"]
        self.assertEqual(routes, [])

    def test_mapping_and_guards_match_the_owned_fixture_contract(self):
        contract = json.loads(CONTRACT.read_text())
        self.assertEqual(
            contract["mapping"],
            {
                "Root_M/joint1": "Root_M/base/body",
                "Root_M/joint1/neck": "Root_M/base/body/neck",
                "Root_M/joint1/neck/head": "Root_M/base/body/neck/head",
                "Root_M/joint1/neck/head/topHead": "Root_M/base/body/neck/head/topHead",
            },
        )
        guards = contract["numericAndIdentityGuards"]
        self.assertEqual(guards["maximumMatrixError"], 0.00001)
        for name, value in guards.items():
            if name != "maximumMatrixError":
                self.assertIs(value, True, name)

    def test_contract_preserves_production_evidence_and_visual_backlog(self):
        gates = json.loads(CONTRACT.read_text())["acceptanceGates"]
        self.assertEqual([gate["name"] for gate in gates if gate["status"] == "fixture_proven"], ["owned_matrix_mechanics"])
        self.assertEqual([gate["name"] for gate in gates if gate["status"] == "pending"], ["appearance_and_main_composition"])
        self.assertEqual(
            {gate["name"] for gate in gates if gate["status"] == "production_observation_satisfied"},
            {"native_reachable_production_behavior", "native_attack_authority", "production_route_binding"},
        )
        self.assertEqual(
            next(gate for gate in gates if gate["name"] == "visual_motion_and_gameplay")["status"],
            "gameplay_satisfied_visual_and_progression_pending",
        )


if __name__ == "__main__":
    unittest.main()
