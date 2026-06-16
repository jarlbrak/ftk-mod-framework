using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Many game systems convert a row's string m_ID back to its enum via the static GetEnum
    /// helpers (which Enum.Parse and return None for anything not in the vanilla enum). The town
    /// market, inventory, equipping, etc. all rely on this. These prefixes make GetEnum resolve our
    /// custom string IDs to their synthetic ints (and skip the original's "not found" warning).
    /// </summary>
    [HarmonyPatch(typeof(FTK_itembase), "GetEnum")]
    internal static class ItemGetEnum_Patch
    {
        private static bool Prefix(string _id, ref FTK_itembase.ID __result)
        {
            int v;
            if (ContentRegistry.TryGetSyntheticId(_id, out v, typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB)))
            {
                __result = (FTK_itembase.ID)v;
                return false; // resolved; skip the vanilla Enum.Parse + warning
            }
            return true; // not ours; run the original
        }
    }

    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetEnum")]
    internal static class ProficiencyGetEnum_Patch
    {
        private static bool Prefix(string _id, ref FTK_proficiencyTable.ID __result)
        {
            int v;
            if (ContentRegistry.TryGetSyntheticId(_id, out v, typeof(FTK_proficiencyTableDB)))
            {
                __result = (FTK_proficiencyTable.ID)v;
                return false;
            }
            return true;
        }
    }
}
