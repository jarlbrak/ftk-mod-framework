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

    [HarmonyPatch(typeof(FTK_playerGameStart), "GetEnum")]
    internal static class ClassGetEnum_Patch
    {
        private static bool Prefix(string _id, ref FTK_playerGameStart.ID __result)
        {
            int v;
            if (ContentRegistry.TryGetSyntheticId(_id, out v, typeof(FTK_playerGameStartDB)))
            {
                __result = (FTK_playerGameStart.ID)v;
                return false;
            }
            return true;
        }
    }

    // Load-bearing for enemies: the overworld spawn picker calls FTK_enemyCombat.GetEnum(row.m_ID) on our
    // (non-numeric) string id while building the candidate pool, and DROPS the enemy if it returns None.
    // (The Photon path resolves the decimal-string form via vanilla Enum.Parse, so this is only for the
    // picker's human-id lookup — but without it the custom enemy never reaches a fight.)
    [HarmonyPatch(typeof(FTK_enemyCombat), "GetEnum")]
    internal static class EnemyGetEnum_Patch
    {
        private static bool Prefix(string _id, ref FTK_enemyCombat.ID __result)
        {
            int v;
            if (ContentRegistry.TryGetSyntheticId(_id, out v, typeof(FTK_enemyCombatDB)))
            {
                __result = (FTK_enemyCombat.ID)v;
                return false;
            }
            return true;
        }
    }
}
