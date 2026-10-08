using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Register exact visible Thief armor in a caller-namespaced set.</summary>
        public static bool SetThiefVisibleArmor(FTK_items item, string setKey, string slot)
        {
            int id;
            if (item == null || string.IsNullOrEmpty(setKey) || setKey.IndexOf(':') < 1 ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB)) ||
                !object.ReferenceEquals(Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id), item)) return false;
            ThiefEquipmentSets.Slot parsed;
            if (slot == "head") parsed = ThiefEquipmentSets.Slot.Head;
            else if (slot == "body") parsed = ThiefEquipmentSets.Slot.Body;
            else if (slot == "feet") parsed = ThiefEquipmentSets.Slot.Feet;
            else return false;
            return ThiefRuntime.RegisterArmor(id, setKey, parsed);
        }

        /// <summary>Record an optional exact matching weapon; it does not unlock armor effects.</summary>
        public static bool SetThiefArmament(FTK_weaponStats2 weapon, string setKey)
        {
            int id;
            if (weapon == null || string.IsNullOrEmpty(setKey) || setKey.IndexOf(':') < 1 ||
                !ContentRegistry.TryGetSyntheticId(weapon.m_ID, out id, typeof(FTK_weaponStats2DB)) ||
                !object.ReferenceEquals(Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id), weapon)) return false;
            return ThiefRuntime.RegisterArmament(id, setKey);
        }

        /// <summary>Give an exact registered class the Thief's Opportunist combat rules.</summary>
        public static bool AddOpportunist(FTK_playerGameStart classRow)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID)) return false;
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            int id = db.GetIntFromID(classRow.m_ID);
            if (id < 0 || !ContentRegistry.IsRegisteredSyntheticId(id, typeof(FTK_playerGameStartDB)) ||
                !object.ReferenceEquals(db.GetEntryByInt(id), classRow)) return false;
            return ThiefRuntime.RegisterClass(id);
        }

        /// <summary>Declare an exact paired, bow, or native firearm precision weapon.</summary>
        public static bool SetPrecisionWeapon(FTK_weaponStats2 weapon, string kind)
        {
            if (weapon == null || weapon._dmgtype != FTK_weaponStats2.DamageType.physical) return false;
            if (kind == "pistol")
            {
                if (weapon.m_ObjectSlot != FTK_itembase.ObjectSlot.twoHands || weapon.m_Prefab == null) return false;
                Weapon component = weapon.m_Prefab.GetComponentInChildren<Weapon>(true);
                if (component == null || component.m_WeaponType != Weapon.WeaponType.firearm ||
                    component.m_AmmoCapacity < 1) return false;
            }
            int id;
            if (!ContentRegistry.TryGetSyntheticId(weapon.m_ID, out id, typeof(FTK_weaponStats2DB)) ||
                !object.ReferenceEquals(Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id), weapon)) return false;
            return ThiefRuntime.RegisterWeapon(id, kind);
        }

        /// <summary>Declare a registered damage action that prepares or pierces for an Opportunist.</summary>
        public static bool SetPrecisionAction(FTK_proficiencyTable action, string kind)
        {
            if (action == null || action.m_Harmless || action.m_TargetFriendly ||
                action.m_Target != CharacterDummy.TargetType.None || action.m_RepeatCount != 0) return false;
            int id;
            if (!ContentRegistry.TryGetSyntheticId(action.m_ID, out id, typeof(FTK_proficiencyTableDB)) ||
                !object.ReferenceEquals(Db<FTK_proficiencyTableDB>().GetEntry((FTK_proficiencyTable.ID)id), action)) return false;
            if (!ThiefRuntime.RegisterAction(id, kind)) return false;
            // The clone's native template can carry a debuff behavior. These authored actions
            // intentionally only use native damage and penetration calculation.
            action.m_ProficiencyPrefab = null;
            return true;
        }

        /// <summary>Bind one of the Thief's artifact signatures to an exact custom weapon row.</summary>
        public static bool SetThiefArtifact(FTK_weaponStats2 weapon, string signature)
        {
            if (weapon == null || weapon._dmgtype != FTK_weaponStats2.DamageType.physical) return false;
            int id;
            if (!ContentRegistry.TryGetSyntheticId(weapon.m_ID, out id, typeof(FTK_weaponStats2DB)) ||
                !object.ReferenceEquals(Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id), weapon)) return false;
            return ThiefRuntime.RegisterArtifact(id, signature);
        }
    }
}
