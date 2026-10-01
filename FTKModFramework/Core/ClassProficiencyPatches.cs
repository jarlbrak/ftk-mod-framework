using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiBattleStanceButtons), "CreateWeaponProficiencyButtons")]
    internal static class ClassProficiencyButtonPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance, bool _needsReload)
        {
            try
            {
                if (__instance.CombatCow == null || __instance.CombatCow.m_CharacterStats == null) return;
                int classId = (int)__instance.CombatCow.m_CharacterStats.m_CharacterClass;
                List<int> actions = new List<int>(ClassProficiencyRegistry.Get(classId,
                    (int)__instance.CombatCow.m_WeaponID));
                foreach (int action in ItemProficiencyRuntime.EquippedActions(__instance.CombatCow))
                    if (!actions.Contains(action)) actions.Add(action);
                foreach (int id in actions)
                {
                    if (!ClassWeaponProficiencyEligibility.Allows(__instance.CombatCow, id)) continue;
                    FTK_proficiencyTable.ID proficiency = (FTK_proficiencyTable.ID)id;
                    bool present = false;
                    foreach (uiBattleStanceButtons.ProfValues existing in __instance.m_Proficiencies)
                        if (existing.m_Prof == proficiency) { present = true; break; }
                    if (present) continue;
                    FTK_proficiencyTable row = FTK_proficiencyTableDB.Get(proficiency);
                    if (row == null) continue;
                    uiBattleButton button = UnityEngine.Object.Instantiate(__instance.m_ProficiencyButtonMaster);
                    try
                    {
                        button.transform.SetParent(__instance.m_ProficiencyButtonMaster.transform.parent, false);
                        button.m_Owner = __instance;
                        button.m_ButtonType = uiBattleButton.BattleButtonType.proficiency;
                        button.gameObject.GetComponent<Image>().sprite = row.m_BattleButton;
                        button.SetCanUse(!row.m_GunShot || !_needsReload);
                        uiBattleStanceButtons.ProfValues entry = new uiBattleStanceButtons.ProfValues();
                        entry.m_Prof = proficiency;
                        entry.m_Button = button;
                        button.gameObject.SetActive(true);
                        __instance.m_Proficiencies.Add(entry);
                    }
                    catch
                    {
                        UnityEngine.Object.Destroy(button.gameObject);
                        throw;
                    }
                }
                // The ordinary button type follows AttackProficiency and native slot rolls.
                // Native CreateWeaponProficiencyButtons destroys these entries on its next refresh.
            }
            catch (Exception e) { Plugin.Log.LogError("[class-proficiency] action button failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "AttackProficiency")]
    internal static class ClassWeaponProficiencyCommitPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            if (__instance == null || _button == null) return true;
            if (__instance.m_Focusing) return true;
            foreach (uiBattleStanceButtons.ProfValues entry in __instance.m_Proficiencies)
            {
                if (entry.m_Button != _button) continue;
                if (ClassWeaponProficiencyEligibility.Allows(__instance.CombatCow, (int)entry.m_Prof))
                    return true;
                if (__instance.CombatCow != null && __instance.CombatCow.m_CharacterStats != null)
                    __instance.CombatCow.m_CharacterStats.ResetSpentFocus(true);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SlotControl), "ComputeAttackSlotResults", new Type[] {
        typeof(CharacterOverworld), typeof(bool), typeof(FTK_proficiencyTable.ID) })]
    internal static class ClassWeaponProficiencyRollPatch
    {
        private static bool Prefix(CharacterOverworld _cow, FTK_proficiencyTable.ID _prof)
        {
            return ClassWeaponProficiencyEligibility.Allows(_cow, (int)_prof);
        }
    }
}
