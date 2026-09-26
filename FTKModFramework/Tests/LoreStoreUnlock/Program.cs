using System;
using GridEditor;
using FTKModFramework.Core;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }

    // Runs the vanilla accessor followed by the framework postfix, as Harmony would in game.
    internal static bool Purchased(FTK_loreItem item)
    {
        bool result = LorePersistence.Instance.IsPurchased(item);
        LorePersistence_LoreStoreUnlock_Patch.Postfix(LorePersistence.Instance, item, ref result);
        return result;
    }

    private static bool Revealed(FTK_playerGameStartDB db, FTK_playerGameStart.ID id, bool bypass)
    {
        bool result = db.IsReveal(id, bypass);
        FTK_playerGameStartDB_LoreStoreUnlock_Patch.Postfix(db, id, bypass, ref result);
        return result;
    }

    private static void Main()
    {
        LorePersistence lore = LorePersistence.Instance;
        var locked = new FTK_loreItem { m_ID = "Busker" };
        var bought = new FTK_loreItem { m_ID = "Herbalist", StoredPurchase = true };
        var ignored = new FTK_loreItem { m_ID = "Retired", m_Ignore = true };
        var ownedDlc = new FTK_loreItem { m_ID = "Dynasty", m_DLC = FTK_dlc.ID.Owned };
        var unownedDlc = new FTK_loreItem { m_ID = "Skin", m_DLC = FTK_dlc.ID.Unowned };
        var freeDlc = new FTK_loreItem { m_ID = "HelmetNinja", m_DLC = FTK_dlc.ID.Free };
        var cloudOff = new FTK_loreItem { m_ID = "Winter", m_IsCheckCloud = true };
        var cloudOn = new FTK_loreItem { m_ID = "Harvest", m_IsCheckCloud = true };
        var faulty = new FTK_loreItem { m_ID = "Faulty", m_IsCheckCloud = true };
        lore.CloudAvailable.Add(cloudOn.m_ID);
        var classes = new FTK_playerGameStartDB();
        var hunter = new FTK_loreItem { m_ID = "Hunter", m_Category = FTK_loreCategory.ID.classes, m_UnlockID = (int)FTK_playerGameStart.ID.hunter };
        classes.Classes.Add(hunter);

        // Without the mod every answer is the player's stored purchase state.
        Check(!Purchased(locked) && Purchased(bought) && !Purchased(faulty), "inactive overlay keeps vanilla purchases");
        Check(!Revealed(classes, FTK_playerGameStart.ID.hunter, true), "inactive overlay keeps an unrevealed class hidden");

        LoreStoreUnlock.UnlockAll();
        LoreStoreUnlock.UnlockAll();
        Check(Purchased(locked) && Purchased(bought), "every listed entry reads as purchased");
        Check(Purchased(ownedDlc), "owned DLC entry unlocks");
        Check(!Purchased(unownedDlc), "unowned paid DLC entry stays locked");
        Check(!LoreStoreUnlock.Covers(lore, unownedDlc), "paid DLC is never covered");
        Check(Purchased(freeDlc), "unclaimed free DLC entry unlocks");
        Check(!Purchased(ignored), "an ignored row the store never lists stays locked");
        Check(!Purchased(cloudOff) && Purchased(cloudOn), "a cloud promotion follows its current availability");
        Check(!Purchased(faulty) && !Purchased(faulty) && Plugin.Log.Warnings == 1, "a faulting query warns once and keeps the vanilla answer");
        Check(!LoreStoreUnlock.Covers(lore, null), "a missing entry is not covered");

        Check(Revealed(classes, FTK_playerGameStart.ID.hunter, true), "a purchased class is visible at character creation");
        Check(!Revealed(classes, FTK_playerGameStart.ID.hunter, false), "shared reveal bits keep genuine party progress");
        Check(Revealed(classes, FTK_playerGameStart.ID.blacksmith, true), "a class without a lore entry keeps its vanilla answer");
        hunter.m_DLC = FTK_dlc.ID.Unowned;
        Check(!Revealed(classes, FTK_playerGameStart.ID.hunter, true), "an unowned paid DLC class stays hidden");
        hunter.m_DLC = FTK_dlc.ID.Free;
        Check(Revealed(classes, FTK_playerGameStart.ID.hunter, true), "a free DLC class is visible");

        foreach (FTK_loreItem item in new[] { locked, ignored, ownedDlc, unownedDlc, freeDlc, cloudOff, cloudOn, faulty, hunter })
            Check(!item.StoredPurchase, "the overlay writes no stored purchase for " + item.m_ID);
        Check(bought.StoredPurchase, "a real purchase is preserved");

        Console.WriteLine("PASS: " + _checks + " Lore Store unlock checks.");
    }
}
