using System;
using Google2u;
using GridEditor;
using HarmonyLib;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    /// <summary>Perfect chance fix (fix.perfect-chance), status half. CalculateFullSkillChance is
    /// display only: its callers are the flee and shieldtaunt branches of
    /// uiBattleStanceButtons.DisplayBattleActionInfo and FTK_proficiencyTable.GetBattleButtonInfo,
    /// which only that method calls. The rolls in SlotControl never read it. The postfix applies the
    /// statuses on the character's combat dummy, which GetCombatDummy returns only in combat.</summary>
    [HarmonyPatch(typeof(CharacterStats), "CalculateFullSkillChance")]
    internal static class PerfectChanceStatusPatch
    {
        private static void Postfix(CharacterStats __instance, int _slots, ref float __result)
        {
            int handle = FrameworkTweaks.PerfectChanceFix;
            if (!Tweaks.IsOn(handle)) return;
            float chance;
            try
            {
                if (__instance == null || __instance.m_CharacterOverworld == null) return;
                CharacterDummy dummy = __instance.m_CharacterOverworld.GetCombatDummy();
                if (dummy == null) return;
                chance = FrameworkTweaks.PerfectChanceDisplay(Tweaks.Registry, handle, __result, _slots,
                    __instance.SpentFocus, PerfectChance.Statuses(dummy.Illuminated, dummy.Darkness, dummy.Shocked));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            __result = chance;
        }
    }

    /// <summary>Perfect chance fix, Taunt half. The shieldtaunt branch of DisplayBattleActionInfo
    /// asks CalculateFullSkillChance for vitality with a 0f slot modifier, while
    /// SlotControl.ComputeShieldTauntSlotResults rolls each unfocused slot with
    /// m_SkillRoll[vitality].Roll(taunt m_PerSlotSkillRoll). The postfix rebuilds only the Perfect
    /// line, through vanilla's FTKUI.GetPerfectDescriptionFormatted with the same flavor key, so the
    /// STR_perfectDisplay format is kept and only the number changes. Priority.Last places it after
    /// the Guardian, Guardian Reckoning, Thief and combat proficiency postfixes on this method; none
    /// of those touches the shieldtaunt button, so the order only guarantees the last word.</summary>
    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class PerfectChanceTauntPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            int handle = FrameworkTweaks.PerfectChanceFix;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                if (!_on || _button == null || _button.m_ButtonType != uiBattleButton.BattleButtonType.shieldtaunt) return;
                if (__instance == null || __instance.m_InfoPanel == null) return;
                Text[] descriptions = __instance.m_InfoPanel.m_Description;
                if (descriptions == null || descriptions.Length < 2 || descriptions[1] == null) return;
                CharacterOverworld cow = __instance.CombatCow;
                if (cow == null || cow.m_CharacterStats == null || GameFlow.Instance == null) return;
                CharacterDummy dummy = cow.GetCombatDummy();
                FTK_proficiencyTable taunt = FTK_proficiencyTableDB.GetDB().GetEntry(FTK_proficiencyTable.ID.taunt);
                if (dummy == null || taunt == null) return;

                CharacterStats stats = cow.m_CharacterStats;
                float perSlot = stats.GetSkillValue(FTK_weaponStats2.SkillType.vitality, true, taunt.m_PerSlotSkillRoll);
                float chance;
                if (!FrameworkTweaks.TauntPerfectChance(Tweaks.Registry, handle, perSlot, GameFlow.Instance.m_DefaultSlots,
                        stats.SpentFocus, PerfectChance.Statuses(dummy.Illuminated, dummy.Darkness, dummy.Shocked), out chance))
                    return;
                string line = FTKUI.GetPerfectDescriptionFormatted(chance, FTKHub.Localized<TextMenu>("STR_battleButtonsDrawAttention"));
                descriptions[1].text = line;
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }
}
