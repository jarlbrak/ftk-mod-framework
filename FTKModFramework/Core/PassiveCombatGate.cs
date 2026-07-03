using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Master-only, transient once-per-combat ledger for class-innate passives (spec #78 FR-4). It records which
    /// (character, passive) pairs have already fired THIS combat so a trait negates at most once per fight. It is
    /// NEVER serialized and NEVER crosses Photon: the negate DECISION runs only on the master (see
    /// <see cref="DummyDamageInfo_ctor_Patch"/>), and the resulting DummyDamageInfo carries the outcome to every
    /// client, so this ledger existing only on the master is correct and desync-free. Keyed by a stable
    /// per-character identity (the dummy's FTKPlayerID) plus the passive Key.
    /// </summary>
    internal static class PassiveCombatGate
    {
        // character identity -> set of passive Keys that have already fired this combat.
        private static readonly Dictionary<string, HashSet<string>> Fired =
            new Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Stable per-character identity for the gate: the dummy's FTKPlayerID (turn index + photon id). It is
        /// distinct per party member (and per enemy), present on every dummy, and never networked, so its exact
        /// textual form is a master-local detail. Returns null if no dummy is supplied.
        /// </summary>
        internal static string IdentityOf(CharacterDummy dummy)
        {
            if (dummy == null) return null;
            FTKPlayerID fid = dummy.FID;
            return fid.m_TurnIndex + ":" + fid.m_PhotonID;
        }

        /// <summary>True if <paramref name="passiveKey"/> has already fired for <paramref name="identity"/> this combat.</summary>
        internal static bool HasFired(string identity, string passiveKey)
        {
            if (identity == null) return false;
            HashSet<string> keys;
            return Fired.TryGetValue(identity, out keys) && keys.Contains(passiveKey);
        }

        /// <summary>Record that <paramref name="passiveKey"/> fired for <paramref name="identity"/> this combat.</summary>
        internal static void MarkFired(string identity, string passiveKey)
        {
            if (identity == null) return;
            HashSet<string> keys;
            if (!Fired.TryGetValue(identity, out keys))
            {
                keys = new HashSet<string>();
                Fired[identity] = keys;
            }
            keys.Add(passiveKey);
        }

        /// <summary>
        /// Drop all fired-state for one character. Called on BOTH combat START (ResetForCombat) and combat END
        /// (CombatFinished), so each fight begins with every charge available and nothing leaks across combats.
        /// </summary>
        internal static void ResetFor(CharacterDummy dummy)
        {
            string identity = IdentityOf(dummy);
            if (identity != null) Fired.Remove(identity);
        }
    }
}
