using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Lore Store purchases live in player.db as STAT_LU_<entry> rows, and every statistic write is
    // committed and mirrored to Steam at once. The overlay therefore only answers purchase queries
    // in memory and never calls a lore or statistic setter. Removing the mod restores the player's
    // real purchases because nothing was ever written.
    internal static class LoreStoreUnlock
    {
        private static bool all;
        private static bool warned;

        internal static bool All { get { return all; } }

        internal static void UnlockAll() { all = true; }

        // The game's DLC table marks packs anyone can claim at no cost with this Lore Store lock
        // message. Paid packs carry their own message and are never unlocked by the overlay.
        internal const string FreeDlcLockedDescription = "STR_DLC_Free";

        // Covers exactly the entries uiLoreStore.Show can list: ignored rows, paid DLC the player
        // does not own, and cloud promotions that are not currently offered keep their vanilla answer.
        internal static bool Covers(LorePersistence persistence, FTK_loreItem item)
        {
            if (!all || item == null || item.m_Ignore) return false;
            if (item.m_DLC != FTK_dlc.ID.None)
            {
                FTK_dlc dlc = FTK_dlcDB.Get(item.m_DLC);
                if (dlc == null || (!dlc.IsPurchased() && dlc.m_LoreStoreLockedDescription != FreeDlcLockedDescription)) return false;
            }
            return !item.m_IsCheckCloud || (persistence != null && persistence.IsCloudAvailable(item));
        }

        internal static void Warn(Exception e)
        {
            if (warned) return;
            warned = true;
            Plugin.Log.LogWarning("[lore-store-unlock] vanilla answer kept: " + e.Message);
        }
    }

    [HarmonyPatch(typeof(LorePersistence), "IsPurchased")]
    internal static class LorePersistence_LoreStoreUnlock_Patch
    {
        internal static void Postfix(LorePersistence __instance, FTK_loreItem __0, ref bool __result)
        {
            if (__result || !LoreStoreUnlock.All) return;
            try { if (LoreStoreUnlock.Covers(__instance, __0)) __result = true; }
            catch (Exception e) { LoreStoreUnlock.Warn(e); }
        }
    }

    // Character creation skips an unrevealed class before it asks whether the class is purchased.
    // Only the local query is widened. The shared reveal bits suppress reveal-granting encounters
    // and must keep reporting the party's genuine progress.
    [HarmonyPatch(typeof(FTK_playerGameStartDB), "IsReveal")]
    internal static class FTK_playerGameStartDB_LoreStoreUnlock_Patch
    {
        internal static void Postfix(FTK_playerGameStartDB __instance, FTK_playerGameStart.ID __0, bool __1, ref bool __result)
        {
            if (__result || !__1 || !LoreStoreUnlock.All) return;
            try { if (__instance.IsUnlock(__0, true)) __result = true; }
            catch (Exception e) { LoreStoreUnlock.Warn(e); }
        }
    }
}
