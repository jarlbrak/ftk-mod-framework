using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        private static bool RegisteredGuardianClass(FTK_playerGameStart row, out int id)
        {
            id = -1;
            if (row == null || string.IsNullOrEmpty(row.m_ID)) return false;
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            id = db.GetIntFromID(row.m_ID);
            return id >= 0 && GuardianRuntime.IsGuardianClass(id) &&
                ContentRegistry.IsRegisteredSyntheticId(id, typeof(FTK_playerGameStartDB)) &&
                object.ReferenceEquals(db.GetEntryByInt(id), row);
        }

        private static bool RegisteredSetItem(FTK_itembase row, out int id)
        {
            id = -1;
            if (row == null || !ContentRegistry.TryGetSyntheticId(row.m_ID, out id,
                typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return false;
            FTK_itembase registered = row is FTK_weaponStats2
                ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id)
                : (FTK_itembase)Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id);
            return object.ReferenceEquals(row, registered);
        }

        /// <summary>Set the equipment-independent profile for an exact registered Guardian class.</summary>
        public static bool SetGuardianProfile(FTK_playerGameStart classRow, GuardianProfile profile)
        {
            int id;
            if (profile == null || !RegisteredGuardianClass(classRow, out id)) return false;
            return GuardianRuntime.RegisterProfile(id, profile);
        }

        /// <summary>Scope Smite profile scaling to one exact registered offensive action.</summary>
        public static bool SetGuardianSmiteAction(FTK_playerGameStart classRow, FTK_proficiencyTable action)
        {
            int classId, actionId;
            if (!RegisteredGuardianClass(classRow, out classId) || action == null || action.m_Harmless ||
                action.m_TargetFriendly || !ContentRegistry.TryGetSyntheticId(action.m_ID, out actionId,
                typeof(FTK_proficiencyTableDB)) ||
                !object.ReferenceEquals(Db<FTK_proficiencyTableDB>().GetEntryByInt(actionId), action)) return false;
            return GuardianRuntime.RegisterSmite(classId, actionId);
        }

        /// <summary>Register one exclusive armor family with exact custom rows and bounded role profiles.</summary>
        public static bool AddGuardianEquipmentSet(FTK_playerGameStart classRow, string key, GuardianEquipmentSet set)
        {
            int classId;
            if (!RegisteredGuardianClass(classRow, out classId) || string.IsNullOrEmpty(key) || key.Length > 80 ||
                set == null || set.CoreProficiencies.Length > 8) return false;
            FTK_itembase[] rows = { set.Head, set.Body, set.Feet, set.OneHand, set.Shield, set.TwoHand };
            int[] ids = new int[rows.Length];
            HashSet<int> distinct = new HashSet<int>();
            for (int i = 0; i < rows.Length; i++)
                if (!RegisteredSetItem(rows[i], out ids[i]) || !distinct.Add(ids[i])) return false;
            if (set.Head.m_ObjectType != FTK_itembase.ObjectType.helmet ||
                set.Body.m_ObjectType != FTK_itembase.ObjectType.armor ||
                set.Feet.m_ObjectType != FTK_itembase.ObjectType.boots ||
                !(set.OneHand is FTK_weaponStats2) || !(set.TwoHand is FTK_weaponStats2) ||
                set.Shield.m_ObjectType != FTK_itembase.ObjectType.shield) return false;
            int[] actions = new int[set.CoreProficiencies.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                FTK_proficiencyTable row = set.CoreProficiencies[i];
                if (!ContentRegistry.TryGetSyntheticId(row.m_ID, out actions[i], typeof(FTK_proficiencyTableDB)) ||
                    !object.ReferenceEquals(Db<FTK_proficiencyTableDB>().GetEntryByInt(actions[i]), row)) return false;
            }
            FTK_proficiencyTable[] sources = set.ArmorDamageSources;
            int[] sourceIds = new int[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                FTK_proficiencyTable source = sources[i];
                if (source.m_ProficiencyPrefab == null ||
                    source.m_ProficiencyPrefab.GetType() != typeof(ProficiencyArmor) ||
                    source.m_ProficiencyPrefab.m_Category != ProficiencyBase.Category.Armor ||
                    !(source.m_CustomValue <= -1) || float.IsInfinity(source.m_CustomValue) || !ContentRegistry.TryGetSyntheticId(source.m_ID, out sourceIds[i],
                    typeof(FTK_proficiencyTableDB)) ||
                    !object.ReferenceEquals(Db<FTK_proficiencyTableDB>().GetEntryByInt(sourceIds[i]), source)) return false;
            }
            return GuardianRuntime.RegisterSet(classId, new EquipmentSetState.Definition(key, ids[0], ids[1], ids[2],
                ids[3], ids[4], ids[5], set.Minor, set.Core, set.Completion), actions,
                sourceIds, set.ArmorDamageMultiplier);
        }
    }
}
