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

    /// <summary>
    /// Spec #78 Phase 3 (amended FR-5/FR-6): makes the ConsumableDebuff passive trait LIVE ("Iron Belly"). It is a
    /// skip-original PREFIX on <c>EncounterSession.CombatPartyBuff</c>, the single consumable/orb choke point
    /// (<c>ConsumableBase.UseItemBuff</c> / <c>OrbItemBase.UsePartyBuffOrb</c> -> this RPC). For a target whose
    /// class resolves a ConsumableDebuff passive it strips the vanilla drink downsides from the applied prof array;
    /// buffs still land, and enemy attacks are untouched (they flow through DummyDamageInfo -> RespondToHit ->
    /// AddProfToDummy, never CombatPartyBuff, per ninum kb_7e48c334).
    /// <para>
    /// Shape rationale (grounded in the decompiled body): CombatPartyBuff applies the SAME <c>_prof</c> array to
    /// every target in <c>_group</c> (a plain per-target loop: <c>GetDummyByFID(_group[i]).AddProfToDummy(_prof,
    /// false, false)</c> plus <c>PlayAnim(Intro)</c> for every target after the first), and orb buffs pass a
    /// MULTI-target party group (OrbItemBase.UsePartyBuffOrb). Mutating the shared array in place would strip the
    /// downside from non-holders too. So we do NOT touch the array: if no target holds the passive we let the
    /// untouched vanilla loop run (return true, byte-identical parity); otherwise we reproduce that exact loop
    /// ourselves, handing ONLY holder targets a filtered COPY, and skip the original (return false).
    /// </para>
    /// Stateless (no once-per-combat gate: Iron Belly is always-on). The strip runs on every client from identical
    /// RPC args and synced state (class id + a fixed id set, no RNG), so it is deterministic; only the FR-6 feedback
    /// is master-gated, mirroring the Stonewall feedback pattern above.
    /// </summary>
    [HarmonyPatch(typeof(EncounterSession), "CombatPartyBuff")]
    internal static class EncounterSession_CombatPartyBuff_Patch
    {
        // The vanilla drink downsides Iron Belly negates. Source: the hardcoded ConsumableBase.OnUse set
        // (conMeadPoison / conMushroomPoison apply enPoison1; conRum applies enConfuse), the ONLY harmful ids any
        // consumable routes through CombatPartyBuff. Verified in ninum kb_7e48c334. Enemy attacks carry these same
        // ids down the DummyDamageInfo path, which this patch never touches.
        private static readonly FTK_proficiencyTable.ID[] HarmfulDrinkIds =
        {
            FTK_proficiencyTable.ID.enPoison1,
            FTK_proficiencyTable.ID.enConfuse,
        };

        // Skip-original prefix. Fast path preserves exact vanilla behaviour when no ConsumableDebuff holder is in the
        // group (including malformed args, or a holder scan that itself fails): return true and let the original run,
        // nothing applied yet. When a holder IS present we own the application and return false.
        private static bool Prefix(EncounterSession __instance, FTK_proficiencyTable.ID[] _prof, FTKPlayerID[] _group)
        {
            if (_prof == null || _group == null) return true;

            bool anyHolder;
            try { anyHolder = AnyConsumableDebuffHolder(__instance, _group); }
            catch (Exception e)
            {
                Plugin.Log.LogError("[passive] Iron Belly holder scan failed; running vanilla CombatPartyBuff. " + e);
                return true; // nothing applied yet, so the untouched vanilla loop is safe
            }
            if (!anyHolder) return true; // vanilla parity: byte-identical to the original

            // A holder is present: reproduce the vanilla per-target loop, isolating each target so a single failure
            // can neither abort the party's buff nor double-apply (the original never runs; we return false).
            bool isMaster = PhotonNetwork.isMasterClient;
            for (int i = 0; i < _group.Length; i++)
                ApplyBuffToTarget(__instance, _prof, _group[i], i, isMaster);
            return false;
        }

        // True if any target in the group resolves a ConsumableDebuff passive. This decides whether we hand the call
        // back to vanilla untouched, so it must run BEFORE we apply anything.
        private static bool AnyConsumableDebuffHolder(EncounterSession session, FTKPlayerID[] group)
        {
            for (int i = 0; i < group.Length; i++)
            {
                CharacterDummy dummy = session.GetDummyByFID(group[i]);
                PassiveTraitDef def;
                if (dummy != null && TryResolveHolder(dummy, out def)) return true;
            }
            return false;
        }

        // One target's slice of the vanilla loop with the Iron Belly strip folded in. Verbatim vanilla otherwise:
        // AddProfToDummy(_prof, false, false), then PlayAnim(Intro) for every target after the first. A holder gets a
        // harmful-id-stripped copy; a non-holder gets the shared array unchanged.
        private static void ApplyBuffToTarget(EncounterSession session, FTK_proficiencyTable.ID[] prof,
            FTKPlayerID fid, int index, bool isMaster)
        {
            try
            {
                CharacterDummy dummy = session.GetDummyByFID(fid);
                if (dummy == null) return;

                FTK_proficiencyTable.ID[] applied = prof;
                PassiveTraitDef def;
                if (TryResolveHolder(dummy, out def))
                    applied = StripHarmful(prof, dummy, def, isMaster);

                dummy.AddProfToDummy(applied, false, false);
                if (index != 0) dummy.PlayAnim(CharacterEventListener.CombatAnimTrigger.Intro);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[passive] Iron Belly per-target apply failed for one group member. " + e);
            }
        }

        // Resolve the ConsumableDebuff passive owned by this dummy's class, if any. Derives only from synced state
        // (m_CharacterClass), so it returns identically on every client.
        private static bool TryResolveHolder(CharacterDummy dummy, out PassiveTraitDef def)
        {
            def = null;
            CharacterOverworld cow = dummy.m_CharacterOverworld;
            if (cow == null || cow.m_CharacterStats == null) return false;
            int classId = (int)cow.m_CharacterStats.m_CharacterClass;
            return PassiveRegistry.TryResolve(classId, PassiveTrigger.ConsumableDebuff, out def);
        }

        // Return a copy of prof with the harmful drink ids removed. If none are present (e.g. a pure buff orb),
        // returns the original array unchanged (no allocation, no feedback). Every removed id emits one FR-6 veto,
        // master-only.
        private static FTK_proficiencyTable.ID[] StripHarmful(FTK_proficiencyTable.ID[] prof, CharacterDummy victim,
            PassiveTraitDef def, bool isMaster)
        {
            int harmful = 0;
            for (int i = 0; i < prof.Length; i++)
                if (IsHarmful(prof[i])) harmful++;
            if (harmful == 0) return prof;

            FTK_proficiencyTable.ID[] kept = new FTK_proficiencyTable.ID[prof.Length - harmful];
            int w = 0;
            for (int i = 0; i < prof.Length; i++)
            {
                if (IsHarmful(prof[i]))
                {
                    if (isMaster) EmitVeto(victim, def, prof[i]);
                    continue;
                }
                kept[w++] = prof[i];
            }
            return kept;
        }

        private static bool IsHarmful(FTK_proficiencyTable.ID id)
        {
            for (int i = 0; i < HarmfulDrinkIds.Length; i++)
                if (HarmfulDrinkIds[i] == id) return true;
            return false;
        }

        // FR-6 feedback for ONE vetoed id. Master-only: this RPC runs on every client, so emitting everywhere would
        // N-fold the HUD popup and the combat-log entry. All prose comes from the synthetic Localization templates
        // (def.Key + ":hud" / ":log"); a missing template falls back to DisplayName + "!" and warns rather than going
        // silent. No hard-coded English sentences.
        private static void EmitVeto(CharacterDummy victim, PassiveTraitDef def, FTK_proficiencyTable.ID vetoed)
        {
            string hud;
            if (!Localization.TryGetName(def.Key + ":hud", out hud) || string.IsNullOrEmpty(hud))
            {
                hud = def.DisplayName + "!";
                Plugin.Log.LogWarning("[passive] no ':hud' template for '" + def.Key + "'; using '" + hud + "'.");
            }
            victim.SpawnHudTextRPC(hud); // floating combat text (the named feedback surface)

            string log;
            if (!Localization.TryGetName(def.Key + ":log", out log) || string.IsNullOrEmpty(log))
            {
                log = def.DisplayName + "!";
                Plugin.Log.LogWarning("[passive] no ':log' template for '" + def.Key + "'; using '" + log + "'.");
            }
            EncounterSession es = EncounterSession.Instance;
            if (es != null) es.AddCombatEventToActiveLogEntry(log);

            CharacterOverworld cow = victim.m_CharacterOverworld;
            string name = (cow != null && cow.m_CharacterStats != null) ? cow.m_CharacterStats.m_CharacterName : "?";
            Plugin.Log.LogInfo("[passive] " + def.Key + " shrugged off " + vetoed + " for " + name + ".");
        }
    }
}
