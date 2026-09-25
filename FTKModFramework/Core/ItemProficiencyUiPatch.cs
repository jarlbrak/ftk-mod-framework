using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiItemDetail), "Show")]
    internal static class ItemProficiencyUiPatch
    {
        private static void Postfix(uiItemDetail __instance, FTK_itembase.ID _itemID)
        {
            try
            {
                string description = ItemProficiencyRuntime.Description((int)_itemID);
                if (description.Length == 0 || __instance.m_ArmorPanel == null ||
                    !__instance.m_ArmorPanel.gameObject.activeSelf || __instance.m_EquippableProperties == null) return;
                // Native Show rebuilds this label for trinkets and other supported equipment.
                __instance.m_EquippableProperties.text = GuardianEquipmentDescription.Append(
                    __instance.m_EquippableProperties.text, description);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[item-proficiency] item description failed: " + e.Message); }
        }
    }
}
