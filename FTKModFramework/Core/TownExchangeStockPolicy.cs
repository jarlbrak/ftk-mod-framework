using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class TownExchangeStockPolicy
    {
        internal static bool Eligible(int ownerClass, int buyerClass, int ownedCount)
        {
            return ownerClass >= 0 && ownerClass == buyerClass && ownedCount == 0;
        }

        internal static int OwnedCount(string slot, int backpackCount, Func<string, int> equippedCount)
        {
            string[] slots = { "Head", "Body", "Foot", "RightHand", "LeftHand", "Trinket", "Neck" };
            int owned = backpackCount;
            if (equippedCount != null)
                for (int i = 0; i < slots.Length; i++) owned += equippedCount(slots[i]);
            return owned;
        }

        internal static int RemoveExclusive<TKey>(Dictionary<TKey, int> stock, Predicate<TKey> exclusive)
        {
            if (stock == null || exclusive == null) return 0;
            var remove = new List<TKey>();
            foreach (TKey key in stock.Keys)
                if (exclusive(key)) remove.Add(key);
            foreach (TKey key in remove) stock.Remove(key);
            return remove.Count;
        }
    }
}
