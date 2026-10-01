using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal interface ITownExchangeInventory
    {
        int TokenCount { get; }
        int ItemCount { get; }
        int EquippedItemCount { get; }
        void RemoveToken();
        void AddItem();
        void RestoreCounts(int tokenCount, int itemCount);
        void NotifyChanged();
    }

    /// <summary>A guarded local exchange. Network delivery is disabled until it can use one authoritative operation.</summary>
    internal sealed class TownExchangeTransaction
    {
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        private bool busy;
        internal bool RecoveryFailed { get; private set; }

        internal bool TryBuy(string nonce, ITownExchangeInventory inventory)
        {
            if (String.IsNullOrEmpty(nonce) || inventory == null || busy || RecoveryFailed || completed.Contains(nonce)) return false;
            int tokens = inventory.TokenCount;
            int items = inventory.ItemCount;
            if (tokens < 1 || items > 0 || inventory.EquippedItemCount > 0) return false;
            busy = true;
            try
            {
                inventory.RemoveToken();
                if (inventory.TokenCount != tokens - 1) throw new InvalidOperationException("Token debit failed.");
                if (inventory.ItemCount != items || inventory.EquippedItemCount > 0)
                    throw new InvalidOperationException("Item ownership changed during token debit.");
                inventory.AddItem();
                if (inventory.ItemCount != items + 1) throw new InvalidOperationException("Item delivery failed.");
                inventory.NotifyChanged();
                completed.Add(nonce);
                return true;
            }
            catch
            {
                try
                {
                    inventory.RestoreCounts(tokens, items);
                    if (inventory.TokenCount != tokens || inventory.ItemCount != items)
                        throw new InvalidOperationException("Town exchange recovery did not restore the exact counts.");
                    inventory.NotifyChanged();
                }
                catch { RecoveryFailed = true; return false; }
                return false;
            }
            finally { busy = false; }
        }

        internal void ClearCompleted() { completed.Clear(); RecoveryFailed = false; }
    }
}
