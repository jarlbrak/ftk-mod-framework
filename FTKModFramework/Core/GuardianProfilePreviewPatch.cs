using System;
using System.Globalization;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class GuardianProfilePreviewPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            if (!_on || __instance.CombatCow == null || _button == null ||
                __instance.m_InfoPanel == null || __instance.m_InfoPanel.m_DamageValue == null ||
                EncounterSession.Instance == null) return;
            try
            {
                CharacterDummy guardian = __instance.CombatCow.GetCombatDummy();
                if (!GuardianRuntime.HasProfile(guardian) ||
                    __instance.m_InfoPanel.m_DMGRoot == null ||
                    !__instance.m_InfoPanel.m_DMGRoot.activeSelf) return;
                FTK_proficiencyTable.ID action = FTK_proficiencyTable.ID.None;
                FTK_proficiencyTable row = null;
                if (_button.m_ButtonType == uiBattleButton.BattleButtonType.proficiency)
                {
                    bool found = false;
                    foreach (uiBattleStanceButtons.ProfValues entry in __instance.m_Proficiencies)
                        if (entry.m_Button == _button) { action = entry.m_Prof; found = true; break; }
                    if (!found || action == GuardianRuntime.ActionId) return;
                    row = FTK_proficiencyTableDB.Get(action);
                    if (row == null || row.m_Harmless) return;
                }
                else if (_button.m_ButtonType != uiBattleButton.BattleButtonType.attack) return;
                EnemyDummy enemy = EncounterSession.Instance.GetCurrentEnemy();
                FTK_weaponStats2 weapon = FTK_weaponStats2DB.Get(__instance.CombatCow.m_WeaponID);
                if (enemy == null || weapon == null) return;
                FTK_weaponStats2.DamageType type = (FTK_weaponStats2.DamageType)GuardianPreviewMath.EffectiveType(
                    (int)weapon._dmgtype,
                    row == null ? (int)FTK_weaponStats2.DamageType.none : (int)row.m_DmgTypeOverride,
                    (int)FTK_weaponStats2.DamageType.none);
                int percent = GuardianRuntime.PreviewPercent(guardian, action, type);
                float armor = GuardianRuntime.PreviewArmorPayoff(guardian, action, type);
                float resistance = CombatProficiencyRuntime.DamageBonus(action, enemy);
                float reckoning = __instance.m_BattleActionDisplay != null &&
                    __instance.m_BattleActionDisplay.text.EndsWith(" + RECKONING", StringComparison.Ordinal) ? 1.5f : 1f;
                int max = __instance.CombatCow.m_CharacterStats.GetWeaponMaxDamage(enemy.m_EnemyCombat.m_RaceTypes);
                int displayed = GuardianPreviewMath.Calculate(max, row == null ? 1f : row.m_DmgMultiplier,
                    resistance, reckoning, percent, armor,
                    enemy.Frozen ? GameFlow.Instance.m_FrozenDmgPercent : 1f);
                __instance.m_InfoPanel.m_DamageValue.text = displayed.ToString(CultureInfo.InvariantCulture);
                if (GuardianRuntime.IsSmite(guardian, action) &&
                    __instance.m_InfoPanel.m_Description != null &&
                    __instance.m_InfoPanel.m_Description.Length > 1 &&
                    __instance.m_InfoPanel.m_Description[1] != null)
                {
                    string description;
                    if (row != null && Localization.TryGetProficiencyDescription(row.m_ID, out description) &&
                        !string.IsNullOrEmpty(description))
                        __instance.m_InfoPanel.m_Description[1].text = description;
                }
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] profile preview failed: " + e); }
        }
    }
}
