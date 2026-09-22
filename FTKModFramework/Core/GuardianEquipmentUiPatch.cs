using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiItemDetail), "Show")]
    internal static class GuardianEquipmentUiPatch
    {
        private static void Postfix(uiItemDetail __instance, FTK_itembase.ID _itemID)
        {
            try
            {
                string description = GuardianRuntime.EquipmentDescription((int)_itemID);
                if (description.Length == 0 || __instance.m_ArmorPanel == null ||
                    !__instance.m_ArmorPanel.gameObject.activeSelf || __instance.m_EquippableProperties == null) return;
                // Native Show rebuilds this text for every armor/shield/boot/helmet selection.
                __instance.m_EquippableProperties.text = GuardianEquipmentDescription.Append(
                    __instance.m_EquippableProperties.text, description);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[guardian-item-ui] " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(uiWeaponDetail), "ShowWeapon")]
    internal static class GuardianWeaponUiPatch
    {
        private static void Postfix(uiWeaponDetail __instance, FTK_itembase _itemInfo)
        {
            try
            {
                if (_itemInfo == null || __instance.m_WeaponStatDisplay == null) return;
                int id;
                if (!ContentRegistry.TryGetSyntheticId(_itemInfo.m_ID, out id, typeof(FTK_weaponStats2DB))) return;
                string description = GuardianRuntime.EquipmentDescription(id);
                if (description.Length == 0) return;
                // Weapon fronts have their own native stats text even without a modifier row.
                __instance.m_WeaponStatDisplay.text = GuardianEquipmentDescription.Append(
                    __instance.m_WeaponStatDisplay.text, description);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[guardian-weapon-ui] " + e.Message); }
        }
    }
}
