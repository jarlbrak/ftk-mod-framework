using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Grant ordinary rolling combat actions to an exact registered custom class, independent
        /// of its equipped weapon. Actions retain native slot, Focus, targeting and damage rules.
        /// Repeated grants are additive and idempotent. Rejects the whole grant if any ID is missing.
        /// Call after registration indexes are published, outside an open content batch.
        /// </summary>
        public static bool AttachClassProficiencies(FTK_playerGameStart classRow, params string[] proficiencyIds)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID) ||
                proficiencyIds == null || proficiencyIds.Length == 0 || proficiencyIds.Length > 16) return false;
            FTK_playerGameStartDB classes = Db<FTK_playerGameStartDB>();
            int classId = classes.GetIntFromID(classRow.m_ID);
            if (classId < 0 || !ContentRegistry.IsRegisteredSyntheticId(classId, typeof(FTK_playerGameStartDB)) ||
                !object.ReferenceEquals(classes.GetEntryByInt(classId), classRow)) return false;

            FTK_proficiencyTableDB proficiencies = Db<FTK_proficiencyTableDB>();
            int[] resolved = new int[proficiencyIds.Length];
            for (int i = 0; i < proficiencyIds.Length; i++)
            {
                if (string.IsNullOrEmpty(proficiencyIds[i])) return false;
                int id = proficiencies.GetIntFromID(proficiencyIds[i]);
                if (id < 0 || id == (int)FTK_proficiencyTable.ID.None || proficiencies.GetEntryByInt(id) == null)
                    return false;
                resolved[i] = id;
            }
            System.Collections.Generic.HashSet<int> combined = new System.Collections.Generic.HashSet<int>(ClassProficiencyRegistry.Get(classId));
            foreach (int id in resolved) combined.Add(id);
            if (combined.Count > 16) return false;
            ClassProficiencyRegistry.Attach(classId, resolved);
            return true;
        }

        /// <summary>
        /// Grant custom rolling actions to an exact registered class only while one of the
        /// named weapons is equipped. Native weapon actions and unconditional class grants
        /// are unaffected. Resolution is atomic across every weapon and action in this call.
        /// </summary>
        public static bool AttachClassWeaponProficiencies(FTK_playerGameStart classRow,
            string[] weaponIds, params string[] proficiencyIds)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID) ||
                weaponIds == null || weaponIds.Length == 0 || weaponIds.Length > 128 ||
                proficiencyIds == null || proficiencyIds.Length == 0 || proficiencyIds.Length > 16) return false;
            FTK_playerGameStartDB classes = Db<FTK_playerGameStartDB>();
            int classId = classes.GetIntFromID(classRow.m_ID);
            if (classId < 0 || !ContentRegistry.IsRegisteredSyntheticId(classId, typeof(FTK_playerGameStartDB)) ||
                !object.ReferenceEquals(classes.GetEntryByInt(classId), classRow)) return false;

            FTK_weaponStats2DB weapons = Db<FTK_weaponStats2DB>();
            int[] resolvedWeapons = new int[weaponIds.Length];
            HashSet<int> seenWeapons = new HashSet<int>();
            for (int i = 0; i < weaponIds.Length; i++)
            {
                if (string.IsNullOrEmpty(weaponIds[i]) || weaponIds[i].Trim().Length == 0) return false;
                int id = weapons.GetIntFromID(weaponIds[i]);
                if (id < 0 || weapons.GetEntryByInt(id) == null || !seenWeapons.Add(id)) return false;
                resolvedWeapons[i] = id;
            }

            FTK_proficiencyTableDB proficiencies = Db<FTK_proficiencyTableDB>();
            int[] resolvedActions = new int[proficiencyIds.Length];
            HashSet<int> seenActions = new HashSet<int>();
            for (int i = 0; i < proficiencyIds.Length; i++)
            {
                if (string.IsNullOrEmpty(proficiencyIds[i]) || proficiencyIds[i].Trim().Length == 0) return false;
                int id = proficiencies.GetIntFromID(proficiencyIds[i]);
                if (id < 0 || id == (int)FTK_proficiencyTable.ID.None ||
                    proficiencies.GetEntryByInt(id) == null ||
                    !ContentRegistry.IsRegisteredSyntheticId(id, typeof(FTK_proficiencyTableDB)) ||
                    !seenActions.Add(id)) return false;
                resolvedActions[i] = id;
            }
            return ClassProficiencyRegistry.AttachWeapon(classId, resolvedWeapons, resolvedActions);
        }
    }
}
