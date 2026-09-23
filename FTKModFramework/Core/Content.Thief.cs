using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
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

        /// <summary>Declare a registered physical weapon as a paired dagger or bow precision weapon.</summary>
        public static bool SetPrecisionWeapon(FTK_weaponStats2 weapon, string kind)
        {
            if (weapon == null || weapon._dmgtype != FTK_weaponStats2.DamageType.physical) return false;
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
