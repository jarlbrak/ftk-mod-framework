using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(CharacterStats), "TallyCharacterMods")]
    internal static class ClassAffinityCharacterModsPatch
    {
        private static void Postfix(CharacterStats __instance)
        {
            Apply(__instance, false);
        }

        internal static void Apply(CharacterStats stats, bool defense)
        {
            try
            {
                if (stats == null || ClassAffinityRuntime.ReloadEquipmentCount == 0) return;
                List<FTK_characterModifier.ID> modifierRows = Reflect.GetField(stats, "m_CharacterMods") as List<FTK_characterModifier.ID>;
                if (modifierRows == null || modifierRows.Count == 0) return;
                List<int> modifierIds = new List<int>(modifierRows.Count);
                for (int i = 0; i < modifierRows.Count; i++) modifierIds.Add((int)modifierRows[i]);
                ItemClassAffinityTotal bonus = ClassAffinityRuntime.Aggregate((int)stats.m_CharacterClass, modifierIds);
                if (bonus == null) return;

                if (defense)
                {
                    if (bonus.Armor != 0) AddField(stats, "m_ModDefensePhysical", bonus.Armor);
                    if (bonus.Resistance != 0) AddField(stats, "m_ModDefenseMagic", bonus.Resistance);
                }
                else
                {
                    if (bonus.Vitality != 0f) AddField(stats, "m_ModVitality", bonus.Vitality);
                    if (bonus.Speed != 0f) AddField(stats, "_ModQuickness", bonus.Speed);
                    if (bonus.Reflect != 0) AddField(stats, "m_ReflectDamage", bonus.Reflect);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[class-affinity] stat tally failed safely: " + e.Message);
            }
        }

        private static void AddField(object instance, string fieldName, int amount)
        {
            object current = Reflect.GetField(instance, fieldName);
            if (current is int) Reflect.SetField(instance, fieldName, (int)current + amount);
        }

        private static void AddField(object instance, string fieldName, float amount)
        {
            object current = Reflect.GetField(instance, fieldName);
            if (current is float) Reflect.SetField(instance, fieldName, (float)current + amount);
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "TallyCharacterDefense")]
    internal static class ClassAffinityCharacterDefensePatch
    {
        private static void Postfix(CharacterStats __instance)
        {
            ClassAffinityCharacterModsPatch.Apply(__instance, true);
        }
    }

    [HarmonyPatch(typeof(uiItemDetail), "Show")]
    internal static class ClassAffinityItemUiPatch
    {
        private static void Postfix(uiItemDetail __instance, FTK_itembase.ID _itemID)
        {
            try
            {
                string description = ClassAffinityRuntime.Description((int)_itemID);
                if (description.Length == 0 || __instance.m_ArmorPanel == null ||
                    !__instance.m_ArmorPanel.gameObject.activeSelf || __instance.m_EquippableProperties == null) return;
                __instance.m_EquippableProperties.text = GuardianEquipmentDescription.Append(
                    __instance.m_EquippableProperties.text, description);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[class-affinity-item-ui] " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(uiWeaponDetail), "ShowWeapon")]
    internal static class ClassAffinityWeaponUiPatch
    {
        private static void Postfix(uiWeaponDetail __instance, FTK_itembase _itemInfo)
        {
            try
            {
                if (_itemInfo == null || __instance.m_WeaponStatDisplay == null) return;
                int itemId;
                if (!ContentRegistry.TryGetSyntheticId(_itemInfo.m_ID, out itemId,
                    typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return;
                string description = ClassAffinityRuntime.Description(itemId);
                if (description.Length == 0) return;
                __instance.m_WeaponStatDisplay.text = GuardianEquipmentDescription.Append(
                    __instance.m_WeaponStatDisplay.text, description);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[class-affinity-weapon-ui] " + e.Message); }
        }
    }
}
