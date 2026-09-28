// Spec #265 FR-2: the softlock signature predicates, the per-occurrence latch, the per-launch
// budget, the repeated discovery watch and the line shape. The Harmony hooks are live-gated.
using System;
using System.Text;
using FTKModFramework.Core.Diagnostics;

internal static class SignatureChecks
{
    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("softlock signature: " + message);
    }

    internal static int Run()
    {
        Kinds();
        LoadException();
        LootVote();
        StaleDungeon();
        CombatWithoutEncounter();
        Latch();
        Budget();
        Discovery();
        return _checks;
    }

    private static void Kinds()
    {
        string[] names = { "load-exception", "loot-vote", "poi-rediscovery", "stale-dungeon", "combat-without-encounter" };
        Array kinds = Enum.GetValues(typeof(SoftlockKind));
        Check(kinds.Length == names.Length, "five kinds");
        for (int i = 0; i < names.Length; i++)
        {
            var kind = (SoftlockKind)kinds.GetValue(i);
            Check(SoftlockSignature.KindName(kind) == names[i], "kind " + kind + " is named " + names[i]);
            Check(SoftlockSignature.Line(kind).ToString() == "SOFTLOCK-SIGNATURE kind=" + names[i], "each line starts with the stable prefix and kind");
        }
    }

    private static Exception Thrown()
    {
        try
        {
            object hex = null;
            return new Exception(hex.ToString());
        }
        catch (Exception e)
        {
            return e;
        }
    }

    private static void LoadException()
    {
        Check(SoftlockSignature.LoadException(null, "Dungeon", 3, 12, true, 1, 0) == null, "no exception, no line");

        Exception e = Thrown();
        string line = SoftlockSignature.LoadException(e, "AirShip", 3, 12, true, 2, 1);
        Check(line.StartsWith("SOFTLOCK-SIGNATURE kind=load-exception poi=AirShip hex=3,12 carrying=2 questMarkers=1 exception=System.NullReferenceException message=",
            StringComparison.Ordinal), "the POI type, hex and exception lead the line, got " + line);
        Check(line.Contains(" stack=") && line.Contains("Thrown"), "the original frames are kept");
        Check(line.IndexOf('\n') < 0 && line.IndexOf('\r') < 0, "one line");
        Check(SoftlockSignature.LoadException(e, "Town", 0, 0, false, 0, 0).Contains(" hex=- "), "a POI with no hex says so");

        var deep = new Exception("deep", null);
        string longStack = new StringBuilder().Insert(0, "  at Frame.Method () [0x00000] in <x>:0\n", 200).ToString();
        string flat = SoftlockSignature.Flatten(longStack);
        Check(flat.IndexOf('\n') < 0 && flat.Contains("|"), "line breaks become bars");
        string capped = new StuckTurnLine(SoftlockSignature.Prefix).Add("stack", flat, SoftlockSignature.MaxStackLength).ToString();
        Check(capped.Length <= StuckTurnLine.MaxLength && capped.EndsWith("...", StringComparison.Ordinal),
            "a long stack is capped at its own limit");
        Check(capped.Length > StuckTurnLine.MaxValueLength * 2, "the stack gets more room than an ordinary value");
        Check(SoftlockSignature.LoadException(deep, "Town", 1, 1, true, 0, 0).EndsWith(" stack=-", StringComparison.Ordinal), "an unthrown exception has no stack");
    }

    private static void LootVote()
    {
        Check(SoftlockSignature.LootVoteFault(false, 0, false, false) == null, "not a loot vote: nothing");
        Check(SoftlockSignature.LootVoteFault(true, 0, false, false) == "empty-queue", "an empty queue");
        Check(SoftlockSignature.LootVoteFault(true, 2, true, false) == "null-voter", "a Null first voter");
        Check(SoftlockSignature.LootVoteFault(true, 1, false, false) == "unknown-voter", "a voter with no character");
        Check(SoftlockSignature.LootVoteFault(true, 1, false, true) == null, "a normal loot vote");
        Check(SoftlockSignature.LootVoteFault(true, 3, false, true) == null, "a normal three-player loot vote");
    }

    private static void StaleDungeon()
    {
        Check(SoftlockSignature.StaleDungeonFault(false, false, false) == null, "not in a dungeon: nothing");
        Check(SoftlockSignature.StaleDungeonFault(false, true, false) == null, "a dungeon elsewhere does not matter");
        Check(SoftlockSignature.StaleDungeonFault(true, true, true) == null, "inside the entered dungeon");
        Check(SoftlockSignature.StaleDungeonFault(true, false, false) == "no-dungeon", "flagged with no dungeon");
        Check(SoftlockSignature.StaleDungeonFault(true, true, false) == "off-hex", "flagged away from the dungeon");
    }

    private static void CombatWithoutEncounter()
    {
        Check(!SoftlockSignature.CombatWithoutEncounter(false, false, false, false), "out of combat: nothing");
        Check(SoftlockSignature.CombatWithoutEncounter(true, false, false, false), "flagged with no encounter");
        Check(!SoftlockSignature.CombatWithoutEncounter(true, true, false, false), "the session is in combat");
        Check(!SoftlockSignature.CombatWithoutEncounter(true, false, true, false), "the master is in combat");
        Check(!SoftlockSignature.CombatWithoutEncounter(true, false, false, true), "an encounter has started");
    }

    private static void Latch()
    {
        var latch = new SoftlockLatch();
        Check(!latch.Observe(1, false), "a clear key reports nothing");
        Check(latch.Observe(1, true) && latch.Raised(1), "the first true reports");
        Check(!latch.Observe(1, true) && !latch.Observe(1, true), "a persisting condition reports once");
        Check(latch.Observe(2, true), "another key reports on its own");
        Check(!latch.Observe(1, false) && !latch.Raised(1) && latch.Raised(2), "a false check re-arms only that key");
        Check(latch.Observe(1, true), "a new occurrence reports again");
    }

    private static void Budget()
    {
        var budget = new SoftlockBudget();
        for (int i = 0; i < SoftlockBudget.MaxPerKind; i++)
            Check(budget.TryTake(SoftlockKind.StaleDungeon), "line " + (i + 1) + " is within the budget");
        Check(!budget.TryTake(SoftlockKind.StaleDungeon) && budget.Written(SoftlockKind.StaleDungeon) == SoftlockBudget.MaxPerKind,
            "the eleventh line of a kind is dropped");
        Check(budget.TryTake(SoftlockKind.LootVote) && budget.Written(SoftlockKind.LootVote) == 1, "each kind has its own budget");
        Check(!budget.TryTake((SoftlockKind)99), "an unknown kind is never written");
    }

    private static void Discovery()
    {
        var watch = new PoiDiscoveryWatch();
        Check(watch.Observe(true, 4, 7) == 0 && watch.Observe(true, 4, 8) == 0 && watch.Observe(true, 5, 1) == 0,
            "a loop that finds different places reports nothing");
        Check(watch.Observe(false, -1, -1) == 0 && watch.Tracked == 0, "an empty search ends the loop");

        Check(watch.Observe(true, 4, 7) == 0 && watch.Observe(true, 4, 7) == 0, "a second find of one hex is tolerated");
        Check(watch.Observe(true, 4, 7) == PoiDiscoveryWatch.RepeatThreshold, "the third find of one hex reports");
        Check(watch.Observe(true, 4, 7) == 0 && watch.Observe(true, 4, 7) == 0, "the same loop reports that hex once");
        Check(watch.Observe(true, 7, 4) == 0, "hex indices are not interchangeable");

        watch.Observe(false, -1, -1);
        Check(watch.Observe(true, 4, 7) == 0 && watch.Observe(true, 4, 7) == 0, "a new loop starts counting again");
        Check(watch.Observe(true, 4, 7) == 3, "and reports again at the threshold");

        watch.Observe(false, -1, -1);
        watch.Observe(true, 1, 1);
        watch.Observe(true, 2, 2);
        watch.Observe(true, 1, 1);
        watch.Observe(true, 2, 2);
        Check(watch.Observe(true, 1, 1) == 3, "alternating hexes are counted separately");
    }
}
