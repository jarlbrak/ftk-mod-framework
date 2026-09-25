using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ItemProficiencyRuntime
    {
        private static readonly PlayerInventory.ContainerID[] Slots =
        {
            PlayerInventory.ContainerID.Trinket, PlayerInventory.ContainerID.Neck,
            PlayerInventory.ContainerID.LeftHand, PlayerInventory.ContainerID.RightHand,
            PlayerInventory.ContainerID.Foot, PlayerInventory.ContainerID.Body, PlayerInventory.ContainerID.Head
        };

        internal static int[] EquippedActions(CharacterOverworld owner)
        {
            List<int> actions = new List<int>();
            if (owner == null || owner.m_PlayerInventory == null) return actions.ToArray();
            foreach (PlayerInventory.ContainerID slot in Slots)
            {
                var container = owner.m_PlayerInventory.Get(slot);
                if (container == null || container.m_CountDictionary == null) continue;
                // Stable ordering avoids dictionary enumeration order affecting button layout.
                List<int> items = new List<int>();
                foreach (KeyValuePair<FTK_itembase.ID, int> item in container.m_CountDictionary)
                    if (item.Value > 0) items.Add((int)item.Key);
                items.Sort();
                foreach (int item in items)
                    foreach (int action in ItemProficiencyRegistry.Get(item))
                        if (!actions.Contains(action)) actions.Add(action);
            }
            return actions.ToArray();
        }

        internal static string Description(int item)
        {
            List<string> names = new List<string>();
            foreach (int action in ItemProficiencyRegistry.Get(item))
            {
                FTK_proficiencyTable row = FTK_proficiencyTableDB.Get((FTK_proficiencyTable.ID)action);
                if (row != null) names.Add(row.GetLocalizedDisplayName());
            }
            return names.Count == 0 ? string.Empty : "Actions while equipped:\n" + string.Join("\n", names.ToArray());
        }
    }
}
