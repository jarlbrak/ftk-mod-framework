using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Grant ordinary rolling actions while a registered custom nonweapon equipment item is worn.
        /// Accepts armor, boots, helmets, shields, necklaces and trinkets. Backpack copies grant nothing.
        /// Additive and idempotent, up to sixteen actions per item. Invalid grants make no changes.
        /// Call after registration indexes are published, outside an open content batch.
        /// </summary>
        public static bool AttachItemProficiencies(FTK_items item, params string[] proficiencyIds)
        {
            int id;
            if (item == null || string.IsNullOrEmpty(item.m_ID) || proficiencyIds == null || proficiencyIds.Length == 0 || proficiencyIds.Length > 16 ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB)) ||
                !object.ReferenceEquals(Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id), item)) return false;
            switch (item.m_ObjectType)
            {
                case FTK_itembase.ObjectType.armor:
                case FTK_itembase.ObjectType.boots:
                case FTK_itembase.ObjectType.helmet:
                case FTK_itembase.ObjectType.shield:
                case FTK_itembase.ObjectType.necklace:
                case FTK_itembase.ObjectType.trinket: break;
                default: return false;
            }
            FTK_proficiencyTableDB proficiencies = Db<FTK_proficiencyTableDB>();
            HashSet<int> combined = new HashSet<int>(ItemProficiencyRegistry.Get(id));
            List<int> actions = new List<int>(ItemProficiencyRegistry.Get(id));
            foreach (string key in proficiencyIds)
            {
                if (string.IsNullOrEmpty(key)) return false;
                int proficiency = proficiencies.GetIntFromID(key);
                if (proficiency < 0 || proficiency == (int)FTK_proficiencyTable.ID.None ||
                    proficiencies.GetEntryByInt(proficiency) == null) return false;
                if (combined.Add(proficiency)) actions.Add(proficiency);
            }
            if (actions.Count > 16) return false;
            ItemProficiencyRegistry.Set(id, actions.ToArray());
            return true;
        }
    }
}
