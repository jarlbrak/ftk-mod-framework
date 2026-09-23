using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Bind Guardian-only perks to an exact registered custom equipment row.</summary>
        public static bool SetGuardianEquipment(FTK_itembase item, GuardianEquipmentBonuses bonuses)
        {
            int id;
            if (item == null || bonuses == null || !ContentRegistry.TryGetSyntheticId(item.m_ID, out id,
                typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return false;
            FTK_itembase registered = item is FTK_weaponStats2
                ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id)
                : (FTK_itembase)Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id);
            if (!object.ReferenceEquals(item, registered)) return false;
            if ((bonuses.GuardFocusRestore > 0 || bonuses.GuardReckoning) && !(item is FTK_weaponStats2)) return false;
            GuardianRuntime.RegisterEquipment(id, bonuses);
            return true;
        }

        /// <summary>
        /// Give a registered class the equipment-independent Guard action, focused-hit ally healing,
        /// and one active-Guard lethal rescue per combat. Repeated registration is idempotent.
        /// Guard halves direct damage, expires on the guardian's turn or incapacitation, and targets
        /// another ally only. Designation persists for healing after protection expires.
        /// </summary>
        public static bool AddGuardian(FTK_playerGameStart classRow)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID)) return false;
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            int id = db.GetIntFromID(classRow.m_ID);
            if (id < 0 || !ContentRegistry.IsRegisteredSyntheticId(id, typeof(FTK_playerGameStartDB)) ||
                !object.ReferenceEquals(db.GetEntryByInt(id), classRow)) return false;
            return GuardianRuntime.RegisterClass(id);
        }
    }
}
