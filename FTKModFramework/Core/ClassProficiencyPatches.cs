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
                List<int> actions = new List<int>(ClassProficiencyRegistry.Get(classId));
                foreach (int action in ItemProficiencyRuntime.EquippedActions(__instance.CombatCow))
                    if (!actions.Contains(action)) actions.Add(action);
                foreach (int id in actions)
                {
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
}
