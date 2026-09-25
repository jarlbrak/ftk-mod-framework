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
    }
}
