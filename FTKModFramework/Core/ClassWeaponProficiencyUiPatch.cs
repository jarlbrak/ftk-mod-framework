using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiWeaponDetail), "ShowWeapon")]
    internal static class ClassWeaponProficiencyUiPatch
    {
        private static void Postfix(uiWeaponDetail __instance, FTK_itembase _itemInfo)
        {
            try { Apply(__instance, _itemInfo); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[class-weapon-proficiency] weapon description failed: " + e.Message);
            }
        }

        internal static void Apply(uiWeaponDetail detail, FTK_itembase item)
        {
            if (detail == null || detail.m_WeaponStatDisplay == null || item == null ||
                string.IsNullOrEmpty(item.m_ID)) return;
            int weaponId = Content.Db<FTK_weaponStats2DB>().GetIntFromID(item.m_ID);
            if (weaponId < 0) return;
            string[] lines = ClassWeaponProficiencyDescription.Lines(weaponId);
            if (lines.Length == 0 || VisualParams.Instance == null ||
                VisualParams.Instance.m_ColorTints == null) return;
            var colors = VisualParams.Instance.m_ColorTints.m_CharacterModTypeColor;
            if (colors == null || !colors.ContainsKey(ModType.SkillOrImmunity)) return;
            for (int i = 0; i < lines.Length; i++)
                lines[i] = FTKUI.GetKeyInfoRichText(colors[ModType.SkillOrImmunity], false, lines[i]);
            // Native ShowWeapon rebuilds the front before the enclosing item card measures its set section.
            detail.m_WeaponStatDisplay.text = GuardianEquipmentDescription.Append(
                detail.m_WeaponStatDisplay.text, string.Join("\n", lines));
        }
    }
}
