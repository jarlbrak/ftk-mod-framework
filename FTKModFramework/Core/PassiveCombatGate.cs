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

        // character identity -> feedback armed by a negate but not yet shown. See ArmFeedback for why the
        // FR-6 surfaces cannot be emitted at the moment the negate decision is made.
        private static readonly Dictionary<string, PendingFeedback> Pending =
            new Dictionary<string, PendingFeedback>();

        /// <summary>One negate's worth of FR-6 feedback, waiting for the impact frame that it describes.</summary>
        internal sealed class PendingFeedback
        {
            internal PassiveTraitDef Def;
            internal int DamageZeroed;
        }

        /// <summary>
        /// Stable per-character identity for the gate: the dummy's FTKPlayerID (turn index + photon id). It is
        /// distinct per party member (and per enemy), present on every dummy, and never networked, so its exact
        /// textual form is a master-local detail. Returns null if no dummy is supplied.
        /// </summary>
        internal static string IdentityOf(CharacterDummy dummy)
        {
            if (dummy == null) return null;
            // A COW-less dummy (an enemy dummy, or one not yet initialised) has no player identity: dummy.FID
            // dereferences m_CharacterOverworld.m_FTKPlayerID, so a null COW would NPE. Guard it here so the
            // unguarded ResetForCombat/CombatFinished lifecycle postfixes (which call ResetFor -> IdentityOf on
            // EVERY dummy, enemies included) can never throw. Mirrors the negate path's cow==null guard; a null
            // identity makes ResetFor/HasFired/MarkFired clean no-ops.
            if (dummy.m_CharacterOverworld == null) return null;
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
        /// Record that a negate just happened for <paramref name="identity"/> and that its FR-6 surfaces still owe
        /// the player a showing. The negate DECISION runs in the DummyDamageInfo ctor, which the decompile places
        /// in DamageCalculator._finishEngageAttack BEFORE _playAttackSequence: the whole attack animation (windup,
        /// projectile travel, impact) has not started yet. A hud popup emitted there is displayed and expired by
        /// CharacterDummy.DisplayNextHud (a flat WaitForSeconds(1f)) long before the blow it describes visibly
        /// lands, so it reads as a missing popup. We therefore ARM here and let the impact-frame consumer show it.
        /// <para>
        /// At most one entry per character per combat (an IncomingAttack negate is a once-per-combat charge gated by HasFired
        /// above), so a later re-arm can only follow a reset. If the armed attack never reaches its impact frame,
        /// the entry is dropped by ResetFor at combat start/end and is never shown, which is the correct outcome.
        /// </para>
        /// </summary>
        internal static void ArmFeedback(string identity, PassiveTraitDef def, int damageZeroed)
        {
            if (identity == null || def == null) return;
            PendingFeedback pending = new PendingFeedback();
            pending.Def = def;
            pending.DamageZeroed = damageZeroed;
            Pending[identity] = pending;
        }

        /// <summary>
        /// Take the feedback armed for <paramref name="identity"/>, if any, and clear it so it shows exactly once.
        /// Returns null when this character has no negate awaiting a showing (the overwhelmingly common case: every
        /// ordinary hit on every character).
        /// </summary>
        internal static PendingFeedback TryConsumeFeedback(string identity)
        {
            if (identity == null) return null;
            PendingFeedback pending;
            if (!Pending.TryGetValue(identity, out pending)) return null;
            Pending.Remove(identity);
            return pending;
        }

        /// <summary>
        /// Drop all fired-state for one character. Called on BOTH combat START (ResetForCombat) and combat END
        /// (CombatFinished), so each fight begins with every charge available and nothing leaks across combats.
        /// Also drops any armed-but-unshown feedback, so a negate whose attack never reached its impact frame can
        /// never surface a stale popup in a later fight.
        /// </summary>
        internal static void ResetFor(CharacterDummy dummy)
        {
            string identity = IdentityOf(dummy);
            if (identity == null) return;
            Fired.Remove(identity);
            Pending.Remove(identity);
        }
    }
}
