using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Spec #78 Phase 2 (FR-3/FR-4/FR-6): makes the IncomingAttack passive trait LIVE. Three patches:
    /// <list type="bullet">
    /// <item><b>negate</b> (<see cref="DummyDamageInfo_ctor_Patch"/>): a master-side postfix on the damage-calc
    /// site that zeroes the first qualifying main-target hit each combat for a victim whose class resolves an
    /// IncomingAttack passive, mirroring the exact vanilla Protect/Shield precedent field set.</item>
    /// <item><b>gate reset</b> (<see cref="CharacterDummy_ResetForCombat_Patch"/>): clears a dummy's charges at
    /// combat start.</item>
    /// <item><b>gate clear</b> (<see cref="CharacterDummy_CombatFinished_Patch"/>): drops a dummy's fired-state at
    /// combat end.</item>
    /// </list>
    /// All three are registered idempotently by the single plugin <c>Harmony.PatchAll()</c> (Plugin.Awake); none
    /// carry a one-shot <c>_done</c> guard because each must run on every attack / every combat, and the only
    /// state they touch is the transient master-only <see cref="PassiveCombatGate"/>.
    /// </summary>
    [HarmonyPatch(typeof(DummyDamageInfo), MethodType.Constructor, new Type[] { typeof(AttackAttempt), typeof(bool) })]
    internal static class DummyDamageInfo_ctor_Patch
    {
        // A POSTFIX (never a return-false prefix): the vanilla ctor has already computed m_Damage / m_CritDamage /
        // m_NewHealth and applied any vanilla negate (Petrified/Protected/Shielded/Reflecting). We only mutate that
        // finished outcome. The decision runs ONLY on the master; the negated DummyDamageInfo is Photon-serialized
        // to every client (CharacterDummy.RespondToHit applies it there), so the outcome lands identically for all.
        private static void Postfix(DummyDamageInfo __instance, AttackAttempt _atk, bool _mainTarget)
        {
            try
            {
                if (!PhotonNetwork.isMasterClient) return; // master-authoritative decision (solo => true)
                if (!_mainTarget) return;                  // splash / secondary hits never negate, never consume

                CharacterDummy victim = _atk.m_DamagedDummy;
                if (victim == null) return;
                CharacterOverworld cow = victim.m_CharacterOverworld;
                if (cow == null || cow.m_CharacterStats == null) return; // enemies / uninitialised => vanilla

                int classId = (int)cow.m_CharacterStats.m_CharacterClass;
                PassiveTraitDef def;
                if (!PassiveRegistry.TryResolve(classId, PassiveTrigger.IncomingAttack, out def)) return; // vanilla

                // Qualifying hit = the vanilla ctor computed real damage. A 0-damage hit (including one an existing
                // vanilla negate already zeroed) is NOT qualifying: no negate, no consume, no feedback.
                int computed = __instance.m_Damage + __instance.m_CritDamage;
                if (computed <= 0) return;

                string identity = PassiveCombatGate.IdentityOf(victim);
                if (PassiveCombatGate.HasFired(identity, def.Key)) return; // charge already spent this combat => vanilla

                // Consume the once-per-combat charge, then negate with the EXACT vanilla Protect/Shield field set.
                PassiveCombatGate.MarkFired(identity, def.Key);
                __instance.m_Damage = 0;
                __instance.m_CritDamage = 0;
                __instance.m_NewHealth = _atk.m_VictimHealthStart;
                __instance.m_ProfAffect = false;

                EmitFeedback(victim, cow, def, computed);
            }
            catch (Exception e)
            {
                // Save-safe: a throw out of the damage-calc ctor would abort the attack for the whole party.
                // Swallow and leave the fully-computed vanilla outcome intact (zero behaviour change).
                Plugin.Log.LogError("[passive] IncomingAttack negate failed; leaving vanilla damage. " + e);
            }
        }

        // Feedback is emitted from the MASTER side (the only side that runs the decision). All strings come from the
        // synthetic Localization templates (def.Key + ":hud" / ":log"); a missing template falls back to
        // DisplayName + "!" and warns rather than going silent. No hard-coded English sentences here.
        private static void EmitFeedback(CharacterDummy victim, CharacterOverworld cow, PassiveTraitDef def, int damageZeroed)
        {
            string hud;
            if (!Localization.TryGetName(def.Key + ":hud", out hud) || string.IsNullOrEmpty(hud))
            {
                hud = def.DisplayName + "!";
                Plugin.Log.LogWarning("[passive] no ':hud' template for '" + def.Key + "'; using '" + hud + "'.");
            }
            victim.SpawnHudTextRPC(hud); // floating combat text (the named feedback surface), master-local

            string log;
            if (!Localization.TryGetName(def.Key + ":log", out log) || string.IsNullOrEmpty(log))
            {
                log = def.DisplayName + "!";
                Plugin.Log.LogWarning("[passive] no ':log' template for '" + def.Key + "'; using '" + log + "'.");
            }
            EncounterSession es = EncounterSession.Instance;
            if (es != null) es.AddCombatEventToActiveLogEntry(log);

            Plugin.Log.LogInfo("[passive] " + def.Key + " negated " + damageZeroed + " dmg to " +
                cow.m_CharacterStats.m_CharacterName + ".");
        }
    }

    // Combat START for one dummy (also the reset for the very first combat): clear its once-per-combat passive
    // charges so a fresh fight starts with every trait available.
    [HarmonyPatch(typeof(CharacterDummy), "ResetForCombat")]
    internal static class CharacterDummy_ResetForCombat_Patch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            PassiveCombatGate.ResetFor(__instance);
        }
    }

    // Combat END for one dummy: drop its fired-state so the transient gate never leaks across combats.
    [HarmonyPatch(typeof(CharacterDummy), "CombatFinished")]
    internal static class CharacterDummy_CombatFinished_Patch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            PassiveCombatGate.ResetFor(__instance);
        }
    }
}
