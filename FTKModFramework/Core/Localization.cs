using System.Collections.Generic;
using GridEditor;
using Google2u;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Gives custom content real display names.
    ///
    /// The game resolves names from Google2u text tables: FTK_itembase.GetLocalizedName() returns
    /// Localized&lt;TextItems&gt;("STR_" + m_ID), and FTK_proficiencyTable.GetLocalizedDisplayName()
    /// returns Localized&lt;TextMisc&gt;(m_DisplayName). Our custom string IDs aren't in those tables,
    /// so we postfix the lookups and substitute the name registered for that row's m_ID.
    /// </summary>
    public static class Localization
    {
        // keyed by the row's string m_ID
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        public static void SetName(string contentId, string displayName)
        {
            Names[contentId] = displayName;
        }

        public static bool TryGetName(string contentId, out string displayName)
        {
            if (contentId == null) { displayName = null; return false; }
            return Names.TryGetValue(contentId, out displayName);
        }

        // Class flavor/description text (the character-select panel reads it inline, not via a method).
        private static readonly Dictionary<string, string> ClassFlavors = new Dictionary<string, string>();

        public static void SetClassFlavor(string classId, string flavor)
        {
            ClassFlavors[classId] = flavor;
        }

        public static bool TryGetClassFlavor(string classId, out string flavor)
        {
            if (classId == null) { flavor = null; return false; }
            return ClassFlavors.TryGetValue(classId, out flavor);
        }

        // Proficiency description (the combat tooltip's effect line, normally derived from the category).
        private static readonly Dictionary<string, string> ProficiencyDescriptions = new Dictionary<string, string>();

        public static void SetProficiencyDescription(string proficiencyId, string description)
        {
            ProficiencyDescriptions[proficiencyId] = description;
        }

        public static bool TryGetProficiencyDescription(string proficiencyId, out string description)
        {
            if (proficiencyId == null) { description = null; return false; }
            return ProficiencyDescriptions.TryGetValue(proficiencyId, out description);
        }

        // Enemy description (the bestiary / inspect text). The display NAME reuses the shared Names map.
        private static readonly Dictionary<string, string> EnemyDescriptions = new Dictionary<string, string>();

        public static void SetEnemyDescription(string enemyId, string description)
        {
            EnemyDescriptions[enemyId] = description;
        }

        public static bool TryGetEnemyDescription(string enemyId, out string description)
        {
            if (enemyId == null) { description = null; return false; }
            return EnemyDescriptions.TryGetValue(enemyId, out description);
        }
    }

    [HarmonyPatch(typeof(FTK_itembase), "GetLocalizedName")]
    internal static class ItemName_Patch
    {
        private static void Postfix(FTK_itembase __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetLocalizedDisplayName")]
    internal static class ProficiencyName_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // The combat ability tooltip header uses DisplayTitle (not DisplayName); show our name there too.
    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetLocalizedDisplayTitle")]
    internal static class ProficiencyTitle_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // Override the tooltip's effect-description line for proficiencies whose category has no
    // built-in description (e.g. StealGold falls through to a "GetCategoryDescription #...#" placeholder).
    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetCategoryDescription")]
    internal static class ProficiencyCategoryDesc_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            // 1) explicit per-proficiency override wins
            string desc;
            if (Localization.TryGetProficiencyDescription(__instance.m_ID, out desc)) { __result = desc; return; }

            // 2) fill in the steal categories the game's switch forgot, using its own "Robbed" string.
            // (Our Thief Steal flips between StealGold and StealItem per outcome, so cover both so the
            // tooltip description stays correct whichever the last steal was.)
            if (__instance.m_ProficiencyPrefab != null &&
                (__instance.m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.StealGold ||
                 __instance.m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.StealItem))
            {
                __result = FTKHub.Localized<TextMisc>("STR_profRobbed");
            }
        }
    }

    [HarmonyPatch(typeof(FTK_playerGameStart), "GetDisplayName")]
    internal static class ClassName_Patch
    {
        private static void Postfix(FTK_playerGameStart __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // The class-select panel sets the flavor text inline (FTKHub.Localized<TextCharacters>(m_Flavor)),
    // so we override it after the fact for custom classes.
    [HarmonyPatch(typeof(uiSelectCharacterInfo), "ShowCharacterInfo")]
    internal static class ClassFlavor_Patch
    {
        private static void Postfix(uiSelectCharacterInfo __instance, FTK_playerGameStart.ID _characterType)
        {
            FTK_playerGameStart entry = FTK_playerGameStartDB.GetDB().GetEntry(_characterType);
            string flavor;
            if (entry != null && Localization.TryGetClassFlavor(entry.m_ID, out flavor) && __instance.m_ClassFlavor != null)
                __instance.m_ClassFlavor.text = flavor;
        }
    }

    // Enemy name shown in combat / the bestiary. GetEnemyDisplay() is wrapped in try/catch and otherwise
    // falls back to Localized<TextEnemy>("STR_"+m_ID) (a placeholder for our custom ids), so a postfix
    // cleanly substitutes the name we registered.
    [HarmonyPatch(typeof(FTK_enemyCombat), "GetEnemyDisplay")]
    internal static class EnemyName_Patch
    {
        private static void Postfix(FTK_enemyCombat __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    [HarmonyPatch(typeof(FTK_enemyCombat), "GetEnemyDescription")]
    internal static class EnemyDescription_Patch
    {
        private static void Postfix(FTK_enemyCombat __instance, ref string __result)
        {
            string desc;
            if (Localization.TryGetEnemyDescription(__instance.m_ID, out desc)) __result = desc;
        }
    }
}
