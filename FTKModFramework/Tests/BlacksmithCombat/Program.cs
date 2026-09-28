using System;
using System.Collections;
using FTKModFramework.Core;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { checks++; return; }
        throw new Exception("Expected bounded action rejection");
    }
    private static IEnumerator TargetWait(Func<bool> selected, Action commit, Action dispose)
    {
        try
        {
            while (!selected()) yield return null;
            commit();
        }
        finally { dispose(); }
    }
    private static void TargetCancellation()
    {
        BlacksmithCombatState state = new BlacksmithCombatState();
        state.BeginTurn("caster", "entry-1");
        bool selected = false;
        int committed = 0, disposed = 0, cleaned = 0, visible = 0;
        Action commit = delegate
        {
            committed++;
            if (state.TryCommit("caster", "entry-1", "temper")) state.Temper("caster", "ally", 4, null);
        };
        BlacksmithTargetWait wait = new BlacksmithTargetWait(
            TargetWait(delegate { return selected; }, commit, delegate { disposed++; }),
            delegate { visible++; }, delegate { cleaned++; });
        Check(wait.MoveNext() && visible == 1, "Opening native target iterator exposes cancellable picker without committing");
        Check(state.TemperAvailable("caster") && committed == 0, "Opening picker preserves encounter use");
        Check(wait.Cancel(), "Pending Temper selection can cancel");
        selected = true;
        Check(!wait.MoveNext() && committed == 0, "Canceled native iterator cannot resume its commit tail");
        Check(state.TemperAvailable("caster") && state.CurrentTurn("caster") == "entry-1", "Cancellation preserves use and scheduled action");
        Check(disposed == 1 && cleaned == 1, "Cancel releases native iterator and owned context once");
        Check(!wait.Cancel(), "Repeated cancellation is harmless");
        wait.Dispose();
        Check(disposed == 1 && cleaned == 1, "Unity stop/dispose after cancellation cannot repeat cleanup");
        selected = false;
        IEnumerator unrelated = TargetWait(delegate { return selected; }, delegate { committed++; }, delegate { });
        Check(unrelated.MoveNext(), "Independent native iterator still advances");
        BlacksmithTargetWait retry = new BlacksmithTargetWait(
            TargetWait(delegate { return selected; }, commit, delegate { disposed++; }),
            delegate { visible++; }, delegate { cleaned++; });
        Check(retry.MoveNext(), "Temper can reopen after cancellation");
        selected = true;
        Check(!retry.MoveNext() && !state.TemperAvailable("caster") && state.Armor("ally") == 4,
            "Accepted target reaches ordinary commit exactly once");
        Check(!retry.MoveNext() && !retry.Cancel() && committed == 1, "Completed target selection cannot recommit or cancel");
        Check(!unrelated.MoveNext() && committed == 2, "Stopping Temper did not stop unrelated native work");
        Check(disposed == 2 && cleaned == 2, "Successful native completion releases only its own context");
    }
    private static void Main()
    {
        TargetCancellation();
        Reject(delegate { new BlacksmithEquipmentBonuses(); });
        Reject(delegate { new BlacksmithEquipmentBonuses(2, 2); });
        Reject(delegate { new BlacksmithEquipmentBonuses(1); });
        Reject(delegate { new BlacksmithEquipmentBonuses(0, 7); });
        Reject(delegate { new BlacksmithEquipmentBonuses(0, 0, 6); });
        Reject(delegate { new BlacksmithEquipmentBonuses(-1); });
        for (int i = 2; i <= 5; i++) Check(new BlacksmithEquipmentBonuses(i).SetHammerArmor == i, "Valid Set Hammer range");
        BlacksmithCombatState s = new BlacksmithCombatState();
        s.BeginTurn("smith", "host-1");
        Check(s.TryCommit("smith", "host-1", "set"), "First committed action");
        Check(!s.TryCommit("smith", "host-1", "set"), "RPC replay cannot reapply");
        Check(!s.TryCommit("smith", null, "set"), "Missing host identity rejects");
        s.SetHammer("smith", 12, 5, false);
        Check(s.Armor("smith") == 0, "Blocked, dodged and zero HP outcomes grant nothing");
        s.SetHammer("smith", 12, 5, true);
        Check(s.Armor("smith") == 5, "Positive partial hit grants full protection");
        s.BeginTurn("smith", "host-1");
        Check(s.Armor("smith") == 5, "Repeated turn-start event does not expire current-turn effect");
        s.ObserveEquipment("smith", 12, false);
        Check(s.Armor("smith") == 0, "Removing any shield ends Set Hammer");
        s.SetHammer("smith", 12, 5, true);
        s.ObserveEquipment("smith", 13, true);
        Check(s.Armor("smith") == 0, "Changing granting hammer ends Set Hammer");
        s.Overhand("smith", 6);
        s.ObserveEquipment("smith", -1, false);
        s.ClearPositive("smith");
        Check(s.Armor("smith") == -6, "Swaps, positive dispel and death preserve committed penalty");
        s.Overhand("smith", 2);
        Check(s.Armor("smith") == -6, "Weaker copy never erases committed penalty");
        Check(s.Temper("helper", "smith", 4, "host-1"), "Ally Temper commits");
        Check(s.Armor("smith") == -2, "Temper offsets penalty additively");
        s.SetHammer("smith", 12, 5, true);
        Check(s.Armor("smith") == -1, "Positive bonuses use maximum, not sum");
        s.EndTurn("smith", "host-1");
        Check(s.Armor("smith") == -1, "Casting target's active turn does not consume a future turn");
        s.BeginTurn("smith", "host-2");
        Check(s.Armor("smith") == 4, "Next turn expires both personal strike effects");
        Check(!s.TryCommit("smith", "host-1", "overhand"), "Late previous-turn outcome cannot bind new scheduled turn");
        s.EndTurn("smith", "host-2");
        Check(s.Armor("smith") == 4, "Temper lasts through first future completed turn");
        s.EndTurn("smith", "host-2");
        Check(s.Armor("smith") == 4, "Duplicate completion cannot shorten duration");
        Check(s.Temper("weak", "smith", 3, null), "Weaker Temper spends caster use");
        s.BeginTurn("smith", "interrupted-entry"); s.EndTurn("smith", "interrupted-entry");
        Check(s.Armor("smith") == 0, "Skipped target turn counts and weaker Temper did not refresh");
        Check(!s.Temper("helper", "smith", 5, null), "Same caster cannot regain use through swaps");
        Check(s.Temper("smith", "smith", 3, "host-3"), "Self Temper allowed");
        s.ObserveEquipment("smith", -1, false);
        Check(s.Armor("smith") == 3, "Committed self Temper survives kit removal");
        s.EndTurn("smith", "host-3"); s.EndTurn("smith", "host-4");
        Check(s.Temper("equal", "smith", 3, null), "Equal Temper refreshes duration");
        s.EndTurn("smith", "host-5"); Check(s.Armor("smith") == 3, "Refreshed protection lasts first next turn");
        Check(s.Temper("strong", "smith", 5, null), "Stronger Temper replaces and refreshes");
        s.EndTurn("smith", "host-6"); Check(s.Armor("smith") == 5, "Stronger protection remains");
        s.ClearPositive("smith"); Check(s.Armor("smith") == 0, "Dispel or death clears protection");
        Check(!s.TemperAvailable("smith"), "Revive and dispel preserve encounter use");
        s.Overhand("smith", 4); s.EndActor("smith");
        Check(s.Armor("smith") == 0, "Combat exit clears all temporary Armor");
        Check(!s.TemperAvailable("smith"), "One actor exit cannot replenish same encounter budget");
        s.ResetEncounter(); Check(s.TemperAvailable("smith"), "Fresh combat replenishes use");
        s.BeginTurn("smith", "host-1");
        Check(s.TryCommit("smith", "host-1", "set"), "Fresh combat resets replay receipts");
        // Other providers remain independent because the state returns a delta, never rewrites their rows.
        s.Temper("helper", "smith", 5, null); s.Overhand("smith", 2);
        Check(7 + s.Armor("smith") == 10, "Native and third-party Armor coexist with feature delta");
        s.ClearPositive("smith"); Check(7 + s.Armor("smith") == 5, "Dispel removes owned positive Armor only");
        Console.WriteLine(checks + " Blacksmith combat checks passed.");
    }
}
