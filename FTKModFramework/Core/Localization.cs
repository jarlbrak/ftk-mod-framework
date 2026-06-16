using System.Collections.Generic;
using GridEditor;
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
}
