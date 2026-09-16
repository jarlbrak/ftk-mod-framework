import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "FTKModFramework/Core/LegacyKrakenResourceAdapter.cs"
PLUGIN = ROOT / "FTKModFramework/Plugin.cs"


class KrakenProductionAdapterImplementationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = SOURCE.read_text()

    def test_route_binding_is_exact_and_dedicated(self):
        self.assertIn(
            '[HarmonyPatch(typeof(EnemyDummy), "InitEnemyDummyForCombat", new Type[] { typeof(bool), typeof(bool), typeof(bool) })]',
            self.source,
        )
        self.assertNotIn('[HarmonyPatch(typeof(EnemyDummy), "InitEnemyDummyForCombat")]', self.source)
        self.assertIn('ResourcePrefabPath = "enkrakenhead"', self.source)
        self.assertIn('enemyRow.m_EnemyAsset != resource', self.source)
        self.assertIn('target.m_Dummy != dummy', self.source)
        self.assertIn('lease.Configure(dummy, target, resource, enemyRow);', self.source)
        self.assertIn('ControllerName = "krakenHeadController"', self.source)
        self.assertNotIn("EnemyVisualPatch", self.source)

    def test_sampler_cannot_dispatch_native_controller_behaviours(self):
        self.assertIn("ClipCount * BankCount", self.source)
        self.assertIn("DirectorUpdateMode.Manual", self.source)
        self.assertIn("_animator.runtimeAnimatorController = null", self.source)
        self.assertIn("_animator.fireEvents = false", self.source)
        self.assertIn("_animator.applyRootMotion = false", self.source)
        self.assertNotIn("AnimatorControllerPlayable", self.source)
        self.assertNotIn("AddComponent<CharacterEventListener>", self.source)

    def test_adapter_keeps_root_and_renderer_contract_immutable(self):
        self.assertIn("Parent-to-child order makes each readback unambiguous and never writes Root_M.", self.source)
        self.assertIn("Shared Root_M was modified.", self.source)
        self.assertIn("Target inverse bind pose changed.", self.source)
        self.assertIn("Target renderer palette order changed.", self.source)
        self.assertIn("MatrixTolerance = 0.00001f", self.source)
        self.assertIn("private EnemyDummy _owner;", self.source)
        self.assertIn("private FTK_enemyCombat _enemyRow;", self.source)
        self.assertIn("private void ValidateOwnerInvariants()", self.source)
        for invariant in (
            "_owner.m_EventListener != _target",
            "_target.m_Dummy != _owner",
            "_owner.m_EnemyCombat != _enemyRow",
            "_enemyRow.m_EnemyAsset != _resource",
        ):
            self.assertIn(invariant, self.source)
        apply_frame = self.source.index("private void ApplyFrame()")
        self.assertLess(
            self.source.index("ValidateOwnerInvariants();", apply_frame),
            self.source.index("ValidateTargetInvariants();", apply_frame),
        )
        commit = self.source.index("private void Commit(")
        self.assertLess(
            self.source.index("ValidateOwnerInvariants();", commit),
            self.source.index("LocalTrs[] rollback", commit),
        )

    def test_failure_rolls_back_and_cleanup_orders_graph_before_hierarchy(self):
        self.assertIn("Restore(rollback);", self.source)
        self.assertIn("Adapter rollback did not restore every target.", self.source)
        self.assertIn('Near(ReadLocal(_targets[i]).Matrix(), rollback[i].Matrix(), "rollback local readback");', self.source)
        self.assertIn("private void OnDestroy()", self.source)
        graph_destroy = self.source.index("if (_graph.IsValid()) _graph.Destroy();")
        hierarchy_destroy = self.source.index("UnityEngine.Object.Destroy(_root)")
        self.assertLess(graph_destroy, hierarchy_destroy)
        self.assertIn("internal static void PruneDestroyedOwners()", self.source)
        self.assertIn("LegacyKrakenResourceAdapterLease.PruneDestroyedOwners();", PLUGIN.read_text())

    def test_transition_policy_keeps_duplicate_clock_banks_and_rejects_unreviewed_appearance_pair(self):
        self.assertIn("SetInput(0, current);", self.source)
        self.assertIn("SetInput(ClipCount, next.Value);", self.source)
        self.assertIn("Native current/next raw weights do not sum to one.", self.source)
        self.assertIn("Two appearance contributors require a separately approved policy.", self.source)
        contracts = re.findall(
            r'new NativeStateContract\("([^"]+)", "([^"]+)", (true|false)\)',
            self.source,
        )
        self.assertEqual(
            contracts,
            [
                ("Base Layer.IDLE", "krakenIdle", "true"),
                ("Base Layer.INTRO", "krakenIdle", "true"),
                ("Base Layer.DEFEND", "krakenDamage", "false"),
                ("Base Layer.ATTACK", "krakenAttack", "false"),
                ("Base Layer.VICTORY", "krakenDisappear", "false"),
                ("Base Layer.PASSIVE VICTORY", "krakenDisappear", "false"),
                ("Base Layer.DAMAGEDHEAVY", "krakenDamage", "false"),
                ("Base Layer.DEATH", "krakenDisappear", "false"),
                ("Base Layer.ATTACKCRIT", "krakenAttack", "false"),
                ("Base Layer.ATTACKPROF", "krakenAttack", "false"),
                ("Base Layer.DODGE", "krakenDamage", "false"),
                ("Base Layer.DAMAGED", "krakenDamage", "false"),
                ("Base Layer.DAMAGED STUN", "krakenDamage", "false"),
                ("Base Layer.OverworldAppear", "kraken_appear", "false"),
                ("Base Layer.DEATHLIGHT", "krakenDisappear", "false"),
            ],
        )
        self.assertIn("FindStateContract(state.fullPathHash)", self.source)
        self.assertIn("state is not certified for the Kraken adapter.", self.source)
        self.assertIn("clip.name != contract.clipName", self.source)
        self.assertIn("state.speed != 1f || state.speedMultiplier != 1f || state.length != clip.length", self.source)
        self.assertIn("state.loop != contract.loop || clip.isLooping != contract.loop", self.source)

    def test_controller_clip_inventory_accepts_only_same_object_state_references(self):
        method = re.search(
            r"private static Dictionary<string, AnimationClip> ReadExactClips\(RuntimeAnimatorController controller\)"
            r"(?P<body>.*?)\n        private void CaptureTargetTopology\(\)",
            self.source,
            flags=re.DOTALL,
        )
        self.assertIsNotNone(method)
        body = method.group("body")
        self.assertIn("found.Length != StateContracts.Length", body)
        self.assertNotIn("found.Length != ClipCount", body)
        self.assertIn("Array.IndexOf(ClipNames, clip.name) < 0", body)
        self.assertRegex(
            body,
            r"(?s)clips\.TryGetValue\(clip\.name, out existing\).*?"
            r"!ReferenceEquals\(existing, clip\).*?continue;.*?clips\.Add\(clip\.name, clip\);",
        )
        self.assertIn("if (clips.Count != ClipCount)", body)

    def test_partial_sampler_construction_releases_owned_graph_and_root(self):
        self.assertRegex(
            self.source,
            r"(?s)try\s*\{\s*Create\(source, avatar\);\s*\}\s*catch \(Exception createFailure\).*?Dispose\(\);",
            "ClipSampler construction must dispose partial ownership before rethrowing.",
        )
        self.assertIn("Owned sampler construction failed and cleanup was incomplete:", self.source)

    def test_appearance_conversion_excludes_the_branched_native_jaw(self):
        match = re.search(
            r"private static readonly string\[\] OldMappedPaths\s*=\s*\{(?P<paths>.*?)\};",
            self.source,
            flags=re.DOTALL,
        )
        self.assertIsNotNone(match)
        paths = re.findall(r'"([^"]+)"', match.group("paths"))
        self.assertEqual(
            paths,
            [
                "Root_M/joint1",
                "Root_M/joint1/neck",
                "Root_M/joint1/neck/head",
                "Root_M/joint1/neck/head/topHead",
            ],
        )
        self.assertIn("SnapshotModels(_oldSampler, OldMappedPaths)", self.source)
        self.assertNotIn("SnapshotModels(_oldSampler, OldPaths)", self.source)
        self.assertIn("native appearance jaw preservation", self.source)


if __name__ == "__main__":
    unittest.main()
