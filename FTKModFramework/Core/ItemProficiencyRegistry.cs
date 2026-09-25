using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class ItemProficiencyRegistry
    {
        private static Dictionary<int, int[]> items = new Dictionary<int, int[]>();
        internal static int Count { get { return items.Count; } }
        internal static void Set(int item, int[] actions) { items[item] = (int[])actions.Clone(); }
        internal static int[] Get(int item)
        {
            int[] actions;
            return items.TryGetValue(item, out actions) ? (int[])actions.Clone() : new int[0];
        }
        internal static Action SuspendForReload()
        {
            Dictionary<int, int[]> previous = items;
            items = new Dictionary<int, int[]>();
            return delegate { items = previous; };
        }
    }
}
