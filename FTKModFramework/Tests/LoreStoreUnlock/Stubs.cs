using System.Collections.Generic;
using GridEditor;

namespace GridEditor
{
    public class FTK_dlc
    {
        public enum ID { None = -1, Owned, Unowned, Free }
        public bool Owned;
        public string m_LoreStoreLockedDescription = "STR_DLCLostCiv";
        public bool IsPurchased() { return Owned; }
    }
    public static class FTK_dlcDB
    {
        public static FTK_dlc Get(FTK_dlc.ID id)
        {
            if (id == FTK_dlc.ID.None) return null;
            var dlc = new FTK_dlc { Owned = id == FTK_dlc.ID.Owned };
            if (id == FTK_dlc.ID.Free) dlc.m_LoreStoreLockedDescription = "STR_DLC_Free";
            return dlc;
        }
    }
    public class FTK_loreItem
    {
        public string m_ID;
        public bool m_Ignore;
        public FTK_dlc.ID m_DLC = FTK_dlc.ID.None;
        public bool m_IsCheckCloud;
        public FTK_loreCategory.ID m_Category;
        public int m_UnlockID;
        // Stands in for the STAT_LU_<entry> row in player.db.
        public bool StoredPurchase;
        public bool Revealed;
    }
    public class FTK_loreCategory { public enum ID { None = -1, classes, items } }
    public class FTK_playerGameStart { public enum ID { None = -1, blacksmith, hunter } }
    public class FTK_playerGameStartDB
    {
        public readonly List<FTK_loreItem> Classes = new List<FTK_loreItem>();
        public bool IsUnlock(FTK_playerGameStart.ID id, bool bypass)
        {
            foreach (FTK_loreItem item in Classes)
                if (item.m_Category == FTK_loreCategory.ID.classes && item.m_UnlockID == (int)id) return bypass ? Program.Purchased(item) : false;
            return true;
        }
        public bool IsReveal(FTK_playerGameStart.ID id, bool bypass)
        {
            foreach (FTK_loreItem item in Classes)
                if (item.m_Category == FTK_loreCategory.ID.classes && item.m_UnlockID == (int)id) return bypass && item.Revealed;
            return true;
        }
    }
}

public class LorePersistence
{
    public static readonly LorePersistence Instance = new LorePersistence();
    public readonly HashSet<string> CloudAvailable = new HashSet<string>();

    // Mirrors the vanilla accessor: unowned DLC is false before the stored row is consulted.
    public bool IsPurchased(FTK_loreItem item)
    {
        if (item.m_DLC != FTK_dlc.ID.None && !FTK_dlcDB.Get(item.m_DLC).IsPurchased()) return false;
        return item.StoredPurchase;
    }

    public bool IsCloudAvailable(FTK_loreItem item)
    {
        if (item.m_ID == "Faulty") throw new System.InvalidOperationException("cloud state unavailable");
        return CloudAvailable.Contains(item.m_ID);
    }
}

namespace FTKModFramework.Core
{
    internal static class Plugin { internal static readonly Logger Log = new Logger(); }
    internal class Logger { internal int Warnings; internal void LogWarning(string text) { Warnings++; } }
}
