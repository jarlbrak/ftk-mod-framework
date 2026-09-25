using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(DamageCalculator), "_finishEngageAttack")]
    internal static class CombatProficiencyAttackPatch
    {
        private static void Prefix(ref AttackAttempt _aa, bool _consumable)
        {
            try { CombatProficiencyRuntime.Prepare(ref _aa, _consumable); }
            catch (Exception e)
            {
                if (CombatProficiencyRegistry.HasRandom((int)_aa.m_AttackProficiency)) _aa.m_ProfSuccess = false;
                Plugin.Log.LogError("[combat-proficiency] preparation failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_calcDamage")]
    internal static class CombatProficiencyVictimDamagePatch
    {
        private static void Prefix(AttackAttempt _atk, bool _itemAttack, ref float _dmgMultiplier)
        {
            try { CombatProficiencyRuntime.ApplyDamageBonus(_atk, _itemAttack, ref _dmgMultiplier); }
            catch (Exception e) { Plugin.Log.LogError("[combat-proficiency] victim damage failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class CombatProficiencyPreviewPatch
    {
        // Reckoning's existing preview follows this and includes the same conditional multiplier.
        [HarmonyPriority(Priority.First)]
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            if (!_on) return;
            try { CombatProficiencyRuntime.ShowPreview(__instance, _button); }
            catch (Exception e) { Plugin.Log.LogError("[combat-proficiency] preview failed: " + e); }
        }
    }
}
