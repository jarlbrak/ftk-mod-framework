using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class Program
{
    private sealed class Inventory : ITownExchangeInventory
    {
        internal int Tokens;
        internal int Items;
        internal int EquippedCopies;
        internal bool FailDebit;
        internal bool FailGrant;
        internal bool FailRestore;
        internal bool UnexpectedDoubleDebit;
        internal int Notifications;
        internal Action OnDebit;
        public int TokenCount { get { return Tokens; } }
        public int ItemCount { get { return Items; } }
        public int EquippedItemCount { get { return EquippedCopies; } }
        public void RemoveToken()
        {
            if (FailDebit) return;
            Tokens -= UnexpectedDoubleDebit ? 2 : 1;
            if (OnDebit != null) OnDebit();
        }
        public void AddItem()
        {
            Items++;
            if (FailGrant) throw new InvalidOperationException("Injected delivery failure");
        }
        public void RestoreCounts(int tokens, int items)
        {
            if (FailRestore) return;
            Tokens = tokens;
            Items = items;
        }
        public void NotifyChanged() { Notifications++; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Main()
    {
        var transaction = new TownExchangeTransaction();
        var noToken = new Inventory();
        Check(!transaction.TryBuy("none", noToken) && noToken.Tokens == 0 && noToken.Items == 0, "zero tokens");

        var debitFailure = new Inventory { Tokens = 1, FailDebit = true };
        Check(!transaction.TryBuy("debit", debitFailure) && debitFailure.Tokens == 1 && debitFailure.Items == 0, "failed debit");

        var grantFailure = new Inventory { Tokens = 1, FailGrant = true };
        Check(!transaction.TryBuy("grant", grantFailure) && grantFailure.Tokens == 1 && grantFailure.Items == 0, "failed grant rollback");
        Check(grantFailure.Notifications == 1, "failed grant did not notify restored counts once");

        var doubleDebit = new Inventory { Tokens = 2, UnexpectedDoubleDebit = true };
        Check(!transaction.TryBuy("double-debit", doubleDebit) && doubleDebit.Tokens == 2 && doubleDebit.Items == 0, "unexpected double debit exact restore");

        var unrecoverable = new Inventory { Tokens = 1, FailGrant = true, FailRestore = true };
        var frozen = new TownExchangeTransaction();
        Check(!frozen.TryBuy("broken", unrecoverable) && frozen.RecoveryFailed, "recovery fault not surfaced");
        Check(!frozen.TryBuy("retry", unrecoverable), "failed recovery not frozen");

        var success = new Inventory { Tokens = 2 };
        bool nestedSucceeded = false;
        success.OnDebit = () => nestedSucceeded = transaction.TryBuy("nested", success);
        Check(transaction.TryBuy("once", success), "successful purchase");
        Check(success.Notifications == 1, "success should notify once after the batch");
        Check(!nestedSucceeded && success.Tokens == 1 && success.Items == 1, "reentry");
        Check(!transaction.TryBuy("once", success) && success.Tokens == 1 && success.Items == 1, "repeated confirmation nonce");
        Check(!transaction.TryBuy("new-confirmation", success) && success.Tokens == 1 && success.Items == 1, "owned item with fresh nonce");
        var equipped = new Inventory { Tokens = 1, EquippedCopies = 1 };
        Check(!transaction.TryBuy("equipped", equipped) && equipped.Tokens == 1 && equipped.Notifications == 0, "equipped copy must prevent debit");
        var backpack = new Inventory { Tokens = 1, Items = 1 };
        Check(!transaction.TryBuy("backpack", backpack) && backpack.Tokens == 1 && backpack.Notifications == 0, "backpack copy must prevent debit");
        Check(TownExchangeStockPolicy.Eligible(21, 21, 0), "owner class eligibility");
        Check(!TownExchangeStockPolicy.Eligible(21, 22, 0), "wrong class eligibility");
        Check(!TownExchangeStockPolicy.Eligible(21, 21, 1), "owned eligibility");

        var savedMerchantStock = new Dictionary<int, int> { { 101, 2 }, { 102, 3 } };
        var savedMerchantCurrent = new Dictionary<int, int> { { 101, 1 }, { 102, 2 } };
        var ownedEquipment = new Dictionary<int, int> { { 101, 1 } };
        Check(TownExchangeStockPolicy.RemoveExclusive(savedMerchantStock, id => id == 101) == 1, "saved maximum stock filtered");
        Check(TownExchangeStockPolicy.RemoveExclusive(savedMerchantCurrent, id => id == 101) == 1, "saved current stock filtered");
        Check(!savedMerchantStock.ContainsKey(101) && !savedMerchantCurrent.ContainsKey(101), "exclusive shop item remains");
        Check(savedMerchantStock[102] == 3 && savedMerchantCurrent[102] == 2, "ordinary merchant stock changed");
        Check(ownedEquipment[101] == 1, "owned equipment confiscated");

        var equippedCounts = new Dictionary<string, int> { { "Neck", 1 }, { "Trinket", 1 }, { "Head", 1 } };
        Func<string, int> equippedCount = slot => equippedCounts.ContainsKey(slot) ? equippedCounts[slot] : 0;
        Check(TownExchangeStockPolicy.OwnedCount("Neck", 0, equippedCount) == 3, "equipped slots omitted");
        Check(TownExchangeStockPolicy.OwnedCount("Trinket", 2, equippedCount) == 5, "backpack and equipped copies");
        Check(TownExchangeStockPolicy.OwnedCount("Accessory", 2, slot => slot == "LeftHand" ? 1 : 0) == 3,
            "other equipped slot omitted");

        Console.WriteLine("Town exchange transaction tests passed.");
    }
}
