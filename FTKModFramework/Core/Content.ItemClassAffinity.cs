using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Register bounded additive bonuses for one exact equipment item when worn by the given
        /// class. The item's ordinary modifiers continue to apply to every class.
        /// </summary>
        public static bool SetItemClassAffinity(string modGuid, FTK_itembase item,
            FTK_playerGameStart classRow, ItemClassAffinityBonuses bonuses)
        {
            int itemId;
            if (string.IsNullOrEmpty(modGuid) || item == null || classRow == null || bonuses == null ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out itemId, typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return false;

            FTK_itembase registeredItem = item is FTK_weaponStats2
                ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)itemId)
                : (FTK_itembase)Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)itemId);
            if (!object.ReferenceEquals(item, registeredItem)) return false;

            FTK_playerGameStartDB classDb = Db<FTK_playerGameStartDB>();
            int classId = classDb.GetIntFromID(classRow.m_ID);
            if (classId < 0 || !object.ReferenceEquals(classDb.GetEntryByInt(classId), classRow)) return false;

            // Native equipment resolves a modifier row using the item's numeric ID. Ensure a row
            // exists even when class affinity is the item's only authored modifier.
            if (SetItemModifiers(modGuid, item, null) == null) return false;
            return ClassAffinityRuntime.Register(itemId, classRow, classId, bonuses);
        }
    }
}
