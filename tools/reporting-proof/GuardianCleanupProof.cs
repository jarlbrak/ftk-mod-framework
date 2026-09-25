using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FTKReportingProof
{
    // Fixed regression fixture only. It never adopts a character from the scene.
    internal static class GuardianCleanupProof
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object Run(string session, bool nativeReporting)
        {
            uiStartGame title = uiStartGame.Instance;
            string receipt = Path.Combine(Plugin.Root, "proof-steam-guard.receipt");
            if (!nativeReporting || !File.Exists(receipt) || File.ReadAllText(receipt) != session)
                throw new InvalidOperationException("Current isolated native-framework session required");
            if (!title || title.m_GameStarted || title.m_MapReady || title.m_IsResuming ||
                title.m_JoiningDirect || title.m_JoiningOnlineGame || title.m_IsResumingAutoSave ||
                title.m_ResumeGameInfo != null || title.m_CreateUIs.Count != 0 || uiStartGame.gRestartJoinGame ||
                PhotonNetwork.inRoom || PhotonNetwork.insideLobby ||
                PhotonNetwork.connectionState.ToString() != "Disconnected" || !String.IsNullOrEmpty(GameLogic.gAction) ||
                UnityEngine.Object.FindObjectsOfType<CharacterOverworld>().Length != 0)
                throw new InvalidOperationException("Disconnected pristine title required; no gameplay fixture is permitted");

            Type runtime = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "FTKModFramework") runtime = assembly.GetType("FTKModFramework.Core.GuardianRuntime", true);
            if (runtime == null) throw new InvalidOperationException("Framework runtime is not loaded");
            MethodInfo end = runtime.GetMethod("EndLegendaryCombat", Static);
            MethodInfo identity = runtime.GetMethod("Identity", Static, null, new[] { typeof(CharacterDummy) }, null);
            object legendary = runtime.GetField("Legendary", Static).GetValue(null);
            IDictionary guardians = (IDictionary)legendary.GetType().GetField("guardians", Instance).GetValue(legendary);
            IDictionary pending = (IDictionary)runtime.GetField("PendingGuardFocus", Static).GetValue(null);
            MethodInfo beginGuard = legendary.GetType().GetMethod("BeginGuard", Instance);
            if (end == null || identity == null || beginGuard == null) throw new InvalidOperationException("Guardian contract unavailable");
            bool patched = false;
            Patches patches = Harmony.GetPatchInfo(AccessTools.Method(typeof(CharacterDummy), "CombatFinished"));
            if (patches != null)
                foreach (Patch patch in patches.Prefixes)
                    if (patch.PatchMethod.DeclaringType.FullName == "FTKModFramework.Core.GuardianEndPatch") patched = true;
            if (!patched) throw new InvalidOperationException("Actual GuardianEndPatch is not installed on CombatFinished");

            List<object> checks = new List<object>();
            List<GameObject> owned = new List<GameObject>();
            List<string> ownedKeys = new List<string>();
            bool passed = true;
            int originalGuardians = guardians.Count, originalPending = pending.Count;
            try
            {
                CharacterDummy unbound = Create<CharacterDummy>(owned, "GuardianCleanup-Unbound");
                Check(checks, ref passed, "native-unbound-combat-finished", delegate
                {
                    Require(unbound.m_CharacterOverworld == null, "Fixture unexpectedly has a character");
                    unbound.m_CombatFinished = false;
                    unbound.CombatFinished();
                    Require(unbound.m_CombatFinished, "Native combat completion body did not run");
                });
                Check(checks, ref passed, "native-unbound-combat-finished-repeated", delegate
                {
                    unbound.m_CombatFinished = false;
                    unbound.CombatFinished();
                    Require(unbound.m_CombatFinished, "Repeated native combat completion did not run");
                });
                Check(checks, ref passed, "null-actor-cleanup", delegate { end.Invoke(null, new object[] { null }); });
                Check(checks, ref passed, "unbound-cleanup-preserves-existing-state", delegate
                {
                    Require(guardians.Count == originalGuardians && pending.Count == originalPending,
                        "Unbound cleanup altered existing legendary state");
                });

                CharacterDummy actor = Create<CharacterDummy>(owned, "GuardianCleanup-Actor");
                CharacterOverworld character = Create<CharacterOverworld>(owned, "GuardianCleanup-Character");
                actor.m_CharacterOverworld = character;
                character.m_FTKPlayerID = new FTKPlayerID { m_TurnIndex = 32001, m_PhotonID = 32002 };
                string actorKey = (string)identity.Invoke(null, new object[] { actor });
                string otherKey = "guardian-cleanup-proof:" + Guid.NewGuid().ToString("N");
                Require(!guardians.Contains(actorKey) && !pending.Contains(actorKey) &&
                    !guardians.Contains(otherKey) && !pending.Contains(otherKey), "Fixture identity collided with existing state");
                ownedKeys.Add(actorKey); ownedKeys.Add(otherKey);
                Require((bool)beginGuard.Invoke(legendary, new object[] { actorKey, "proof-actor", otherKey, 0, 0 }), "Could not seed actor state");
                Require((bool)beginGuard.Invoke(legendary, new object[] { otherKey, "proof-other", actorKey, 0, 0 }), "Could not seed other state");
                pending.Add(actorKey, "proof-actor"); pending.Add(otherKey, "proof-other");
                object otherState = guardians[otherKey];
                Check(checks, ref passed, "bound-cleanup-removes-only-matching-state", delegate
                {
                    end.Invoke(null, new object[] { actor });
                    Require(!guardians.Contains(actorKey) && !pending.Contains(actorKey), "Matching state survived cleanup");
                    Require(System.Object.ReferenceEquals(guardians[otherKey], otherState) && (string)pending[otherKey] == "proof-other",
                        "Other actor state changed");
                    end.Invoke(null, new object[] { actor });
                    Require(guardians.Count == originalGuardians + 1 && pending.Count == originalPending + 1,
                        "Repeated cleanup changed unrelated entries");
                });
                Check(checks, ref passed, "enemy-virtual-identity-preserved", delegate
                {
                    EnemyDummy enemy = Create<EnemyDummy>(owned, "GuardianCleanup-Enemy");
                    FTKPlayerID enemyId = new FTKPlayerID { m_TurnIndex = 32003, m_PhotonID = 32004 };
                    typeof(EnemyDummy).GetField("m_EnemyFID", Instance).SetValue(enemy, enemyId);
                    Require(enemy.m_CharacterOverworld == null, "Enemy fixture unexpectedly has a character");
                    Require((string)identity.Invoke(null, new object[] { enemy }) == "32003:32004", "Enemy FID override was bypassed");
                });
            }
            finally
            {
                foreach (string key in ownedKeys) { guardians.Remove(key); pending.Remove(key); }
                foreach (GameObject root in owned) if (root) UnityEngine.Object.DestroyImmediate(root);
            }
            Check(checks, ref passed, "owned-state-restored", delegate
            {
                Require(guardians.Count == originalGuardians && pending.Count == originalPending, "Fixture state was not restored");
                foreach (GameObject root in owned) Require(!root, "Owned fixture object survived cleanup");
            });
            return new { session, operation = "guardian-cleanup", passed, actualGuardianPrefixInstalled = patched,
                frameworkVersion = runtime.Assembly.GetName().Version.ToString(), checks,
                scope = "Owned inactive components at disconnected title; not a completed gameplay encounter" };
        }

        private static T Create<T>(List<GameObject> owned, string name) where T : Component
        {
            GameObject root = new GameObject(name);
            root.SetActive(false);
            owned.Add(root);
            T component = root.AddComponent<T>();
            Require(!root.activeInHierarchy, "Owned fixture must remain inactive to defer native Awake");
            return component;
        }

        private static void Check(List<object> checks, ref bool passed, string name, Action action)
        {
            try { action(); checks.Add(new { name, passed = true }); }
            catch (Exception error)
            {
                passed = false;
                while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                checks.Add(new { name, passed = false, error = error.ToString() });
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
