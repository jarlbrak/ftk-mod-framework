using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class GuardianReckoningPreviewPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            if (!_on) return;
            try { GuardianRuntime.ShowReckoningPreview(__instance, _button); }
            catch (Exception e) { Plugin.Log.LogError("[guardian] Reckoning preview failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_finishEngageAttack")]
    internal static class GuardianReckoningAttackPatch
    {
        private static void Prefix(AttackAttempt _aa, bool _consumable, ref float _dmgMod)
        {
            try { GuardianRuntime.PrepareReckoning(_aa, _consumable, ref _dmgMod); }
            catch (Exception e) { Plugin.Log.LogError("[guardian] Reckoning preparation failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "CheckUpdateAvatarAndPortrait")]
    internal static class GuardianLegendaryEquipmentPatch
    {
        private static void Postfix(CharacterOverworld __instance, FTK_itembase _itembase)
        {
            try
            {
                if (_itembase != null && _itembase.m_IsWeapon && __instance.m_CharacterStats != null &&
                    __instance.m_CharacterStats.m_IsInCombat)
                    GuardianRuntime.ObserveLegendaryEquipment(__instance.GetCombatDummy());
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] legendary equipment change failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(EncounterSession), "UpdateAttackTimeline")]
    internal static class GuardianLegendaryTurnEndPatch
    {
        private static void Prefix(bool _attacked, EncounterSessionMC.FightOrderEntry[] _foe)
        {
            try
            {
                if (_attacked && _foe != null && _foe.Length > 0 && EncounterSession.Instance != null)
                    GuardianRuntime.Legendary.EndTurn(GuardianRuntime.Identity(EncounterSession.Instance.GetDummyByFID(_foe[0].m_Pid)));
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] legendary turn expiry failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "FleeRPC")]
    internal static class GuardianLegendaryFleePatch
    {
        private static void Postfix(CharacterDummy __instance, bool _success)
        {
            if (_success) GuardianRuntime.EndLegendaryCombat(__instance);
        }
    }
}
