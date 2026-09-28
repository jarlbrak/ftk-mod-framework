using System;
using System.Collections.Generic;
using System.Text;
using FTKModFramework.Core.Diagnostics;

// Spec #265 FR-1: when the stuck-turn watchdog and the host acknowledgement watch write a
// snapshot, how often, and what the line looks like.
internal static class Program
{
    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    // My overworld turn with the button off and nothing explaining it.
    private static StuckTurnPoll Stuck(int endTurns = 3, int turn = 1)
    {
        return new StuckTurnPoll { InRun = true, MyTurn = true, OverworldGate = true, EndTurnCount = endTurns, TurnKey = turn };
    }

    // Polls every half second from start (exclusive) to end (inclusive) and returns the first
    // trigger and its time, or None.
    private static StuckTurnTrigger Run(StuckTurnWatch watch, StuckTurnPoll poll, float start, float end, out float at)
    {
        at = -1f;
        for (float t = start + 0.5f; t <= end + 0.001f; t += 0.5f)
        {
            StuckTurnTrigger trigger = watch.Observe(poll, t);
            if (trigger != StuckTurnTrigger.None)
            {
                at = t;
                return trigger;
            }
        }
        return StuckTurnTrigger.None;
    }

    private static void Main()
    {
        Thresholds();
        Idle();
        Suppression();
        EpisodeEnd();
        RateLimit();
        StepClamp();
        HostWatch();
        Line();
        NoAllocation();
        int signatures = SignatureChecks.Run();
        Console.WriteLine("PASS: " + _checks + " stuck-turn checks, " + signatures + " softlock signature checks.");
    }

    private static void Thresholds()
    {
        float at;
        var watch = new StuckTurnWatch();
        Check(watch.Observe(Stuck(), 0f) == StuckTurnTrigger.None && watch.InEpisode, "the first stuck poll starts an episode");
        Check(Run(watch, Stuck(), 0f, 19.5f, out at) == StuckTurnTrigger.None, "19.5 s unexplained writes nothing");
        Check(Run(watch, Stuck(), 19.5f, 30f, out at) == StuckTurnTrigger.Unexplained && Math.Abs(at - 20f) < 0.01f,
            "the unexplained threshold fires at 20 s, got " + at);

        StuckTurnPoll shop = Stuck();
        shop.PanelShowing = true;
        watch = new StuckTurnWatch();
        watch.Observe(shop, 0f);
        Check(Run(watch, shop, 0f, 89.5f, out at) == StuckTurnTrigger.None, "shopping for 89.5 s writes nothing");
        Check(watch.UnexplainedElapsed == 0f, "panel time never counts as unexplained");
        Check(Run(watch, shop, 89.5f, 100f, out at) == StuckTurnTrigger.Panel && Math.Abs(at - 90f) < 0.01f,
            "the panel threshold fires at 90 s, got " + at);

        // Panel time and unexplained time are separate: 60 s in a shop, then 19 s with no panel.
        watch = new StuckTurnWatch();
        watch.Observe(shop, 0f);
        Check(Run(watch, shop, 0f, 60f, out at) == StuckTurnTrigger.None, "60 s in a shop writes nothing");
        Check(Run(watch, Stuck(), 60f, 79f, out at) == StuckTurnTrigger.None, "shop time does not shorten the 20 s threshold");
        Check(Run(watch, Stuck(), 79f, 81f, out at) == StuckTurnTrigger.Unexplained, "20 s without a panel still fires");
    }

    private static void Idle()
    {
        float at;
        StuckTurnPoll[] idle =
        {
            new StuckTurnPoll(),
            new StuckTurnPoll { InRun = true, OverworldGate = true, EndTurnCount = 3, TurnKey = 1 },
            new StuckTurnPoll { InRun = true, MyTurn = true, EndTurnCount = 3, TurnKey = 1 },
            new StuckTurnPoll { InRun = true, MyTurn = true, OverworldGate = true, Interactable = true, EndTurnCount = 3, TurnKey = 1 },
        };
        string[] names = { "outside a run", "another player's turn", "combat (no overworld gate)", "an interactable button" };
        for (int i = 0; i < idle.Length; i++)
        {
            var watch = new StuckTurnWatch();
            watch.Observe(idle[i], 0f);
            Check(!watch.InEpisode && Run(watch, idle[i], 0f, 200f, out at) == StuckTurnTrigger.None, names[i] + " never writes");
        }
    }

    private static void Suppression()
    {
        float at;
        // The encounter menu and the help focus are the two live false positives from the v1.6.0
        // preview: a Fight/Sneak menu left open, and the Timeline tutorial at a turn start.
        string[] names = { "Options open", "chat focused", "game aborted", "encounter menu open", "tutorial or encyclopedia focused" };
        for (int i = 0; i < names.Length; i++)
        {
            StuckTurnPoll suppressed = Stuck();
            suppressed.OptionsOpen = i == 0;
            suppressed.ChatFocused = i == 1;
            suppressed.GameAborted = i == 2;
            suppressed.EncounterMenuOpen = i == 3;
            suppressed.HelpFocused = i == 4;
            var watch = new StuckTurnWatch();
            watch.Observe(suppressed, 0f);
            Check(Run(watch, suppressed, 0f, 300f, out at) == StuckTurnTrigger.None, names[i] + " suppresses the snapshot");
            Check(watch.InEpisode && watch.UnexplainedElapsed == 0f, names[i] + " pauses the clock without ending the episode");

            // Suppressed time does not count: 15 s stuck, 60 s suppressed, then 5 s more fires.
            watch = new StuckTurnWatch();
            watch.Observe(Stuck(), 0f);
            Check(Run(watch, Stuck(), 0f, 15f, out at) == StuckTurnTrigger.None, "15 s stuck writes nothing");
            Check(Run(watch, suppressed, 15f, 75f, out at) == StuckTurnTrigger.None, names[i] + " for 60 s writes nothing");
            Check(Run(watch, Stuck(), 75f, 79.5f, out at) == StuckTurnTrigger.None, "the clock resumed where it paused");
            Check(Run(watch, Stuck(), 79.5f, 80f, out at) == StuckTurnTrigger.Unexplained, names[i] + " resumes the count after it ends");

            // A suppression also pauses the panel clock.
            StuckTurnPoll behindPanel = suppressed;
            behindPanel.PanelShowing = true;
            watch = new StuckTurnWatch();
            watch.Observe(behindPanel, 0f);
            Check(Run(watch, behindPanel, 0f, 300f, out at) == StuckTurnTrigger.None && watch.PanelElapsed == 0f,
                names[i] + " with a panel writes nothing");
        }
    }

    private static void EpisodeEnd()
    {
        float at;
        // The button becoming interactable ends the episode and resets the clock.
        var watch = new StuckTurnWatch();
        watch.Observe(Stuck(), 0f);
        Run(watch, Stuck(), 0f, 19f, out at);
        StuckTurnPoll ready = Stuck();
        ready.Interactable = true;
        Check(watch.Observe(ready, 19.5f) == StuckTurnTrigger.None && !watch.InEpisode, "an interactable button ends the episode");
        watch.Observe(Stuck(), 20f);
        Check(Run(watch, Stuck(), 20f, 39.5f, out at) == StuckTurnTrigger.None, "a new episode starts its own 20 s");
        Check(Run(watch, Stuck(), 39.5f, 40f, out at) == StuckTurnTrigger.Unexplained, "the new episode fires on its own clock");

        // An end-turn count change ends the episode without the button ever turning on.
        watch = new StuckTurnWatch();
        watch.Observe(Stuck(3), 0f);
        Run(watch, Stuck(3), 0f, 19f, out at);
        Check(Run(watch, Stuck(4), 19f, 38.5f, out at) == StuckTurnTrigger.None, "a new end-turn count restarts the clock");
        Check(Run(watch, Stuck(4), 38.5f, 39.5f, out at) == StuckTurnTrigger.Unexplained, "and the next turn can fire");

        // A client never sees m_EndTurnCount move; the turn index change ends its episode.
        watch = new StuckTurnWatch();
        watch.Observe(Stuck(0, 1), 0f);
        Run(watch, Stuck(0, 1), 0f, 19f, out at);
        Check(Run(watch, Stuck(0, 2), 19f, 38.5f, out at) == StuckTurnTrigger.None, "a new turn index restarts the clock");
    }

    private static void RateLimit()
    {
        float at;
        var watch = new StuckTurnWatch();
        watch.Observe(Stuck(), 0f);
        Check(Run(watch, Stuck(), 0f, 25f, out at) == StuckTurnTrigger.Unexplained, "the episode fires once");
        Check(Run(watch, Stuck(), 25f, 600f, out at) == StuckTurnTrigger.None, "and never again in the same episode");
        StuckTurnPoll shop = Stuck();
        shop.PanelShowing = true;
        Check(Run(watch, shop, 600f, 800f, out at) == StuckTurnTrigger.None, "a panel threshold after it does not fire again");
        Check(watch.Snapshots == 1, "one snapshot counted");

        // Across episodes the watch stops after MaxSnapshots.
        watch = new StuckTurnWatch();
        float t = 0f;
        int fired = 0;
        for (int episode = 0; episode < StuckTurnWatch.MaxSnapshots + 5; episode++)
        {
            watch.Observe(Stuck(episode), t);
            if (Run(watch, Stuck(episode), t, t + 21f, out at) != StuckTurnTrigger.None) fired++;
            t += 21f;
        }
        Check(fired == StuckTurnWatch.MaxSnapshots && watch.Snapshots == StuckTurnWatch.MaxSnapshots,
            "at most " + StuckTurnWatch.MaxSnapshots + " snapshots per watch, got " + fired);
    }

    private static void StepClamp()
    {
        // A 60 s gap between polls (a load, a hitch, a suspended app) counts one second.
        var watch = new StuckTurnWatch();
        watch.Observe(Stuck(), 0f);
        Check(watch.Observe(Stuck(), 60f) == StuckTurnTrigger.None && Math.Abs(watch.UnexplainedElapsed - 1f) < 0.001f,
            "a long gap is clamped to one second");
        Check(watch.Observe(Stuck(), 59f) == StuckTurnTrigger.None && Math.Abs(watch.UnexplainedElapsed - 1f) < 0.001f,
            "a clock that goes backwards adds nothing");
    }

    private static void HostWatch()
    {
        var host = new HostAckWatch();
        object wait = new object();
        Check(!host.Observe(true, true, wait, "Sync Board", 1, 0f), "a new pending wait starts an episode");
        bool fired = false;
        float at = -1f;
        for (float t = 0.5f; t <= 30f && !fired; t += 0.5f)
        {
            fired = host.Observe(true, true, wait, "Sync Board", 1, t);
            if (fired) at = t;
        }
        Check(fired && Math.Abs(at - 15f) < 0.01f, "a wait pending 15 s fires, got " + at);
        bool again = false;
        for (float t = 30.5f; t <= 200f; t += 0.5f) again |= host.Observe(true, true, wait, "Sync Board", 1, t);
        Check(!again && host.Snapshots == 1, "once per wait");

        // Not master, not in a run, no wait, or nothing pending: never.
        var idle = new HostAckWatch();
        bool any = false;
        for (float t = 0f; t <= 60f; t += 0.5f)
        {
            any |= idle.Observe(true, false, wait, "Sync Board", 1, t);
            any |= idle.Observe(false, true, wait, "Sync Board", 1, t);
            any |= idle.Observe(true, true, null, null, 0, t);
            any |= idle.Observe(true, true, wait, "Sync Board", 0, t);
        }
        Check(!any, "only a master in a run with a pending wait can fire");

        // A barrier that completes and a new one: each wait gets its own 15 s.
        host = new HostAckWatch();
        host.Observe(true, true, wait, "BeginTurnFinished", 2, 0f);
        for (float t = 0.5f; t <= 14f; t += 0.5f) host.Observe(true, true, wait, "BeginTurnFinished", 2, t);
        object next = new object();
        Check(!host.Observe(true, true, next, "Sync Board", 2, 14.5f), "a new wait object restarts the clock");
        Check(host.Elapsed == 0f, "the clock restarted");
        host.Observe(true, true, next, "Sync Board", 0, 15f);
        Check(!host.Observe(true, true, next, "Sync Board", 1, 15.5f) && host.Elapsed == 0f, "an emptied list ends the episode");
        Check(!host.Observe(true, true, next, "BeginTurn3", 1, 16f) && host.Elapsed == 0f, "a new wait ID on the same object restarts");

        RemoteOnly();
    }

    // The master's own entry is a local wait (its turn start waits on local UI such as a
    // tutorial), so only remote IDs count. Live v1.6.0 preview: SinglePlayer, master 1,
    // acks=BeginTurnFinished:[1] behind the Timeline tutorial wrote host-ack-15s.
    private static void RemoteOnly()
    {
        const int local = 1;
        Check(HostAckWatch.RemotePending(null, local) == 0, "no list, nothing pending");
        Check(HostAckWatch.RemotePending(new List<int>(), local) == 0, "an empty list, nothing pending");
        Check(HostAckWatch.RemotePending(new List<int> { local }, local) == 0, "the master's own entry is not remote");
        Check(HostAckWatch.RemotePending(new List<int> { local, 2 }, local) == 1, "a client beside the master counts once");
        Check(HostAckWatch.RemotePending(new List<int> { 2, 3 }, local) == 2, "every client counts");
        Check(HostAckWatch.RemotePending(new List<int> { local }, int.MinValue) == 1, "no known local ID counts every entry");

        // Single player: the only entry is the master's own, so the watch never fires.
        object wait = new object();
        var host = new HostAckWatch();
        List<int> solo = new List<int> { local };
        bool any = false;
        for (float t = 0f; t <= 120f; t += 0.5f)
            any |= host.Observe(true, true, wait, "BeginTurnFinished", HostAckWatch.RemotePending(solo, local), t);
        Check(!any && host.Snapshots == 0, "a single-player turn start behind a tutorial never fires");

        // Co-op: the master's own tutorial plus a silent client fires on the client's 15 s.
        host = new HostAckWatch();
        List<int> coop = new List<int> { local, 2 };
        float at = -1f;
        for (float t = 0f; t <= 30f && at < 0f; t += 0.5f)
            if (host.Observe(true, true, wait, "BeginTurnFinished", HostAckWatch.RemotePending(coop, local), t)) at = t;
        Check(Math.Abs(at - 15f) < 0.01f, "a client pending 15 s beside the master's entry fires, got " + at);

        // The client acknowledges while the master's tutorial is still open: the episode ends.
        host = new HostAckWatch();
        for (float t = 0f; t <= 10f; t += 0.5f)
            host.Observe(true, true, wait, "BeginTurnFinished", HostAckWatch.RemotePending(coop, local), t);
        coop.Remove(2);
        any = false;
        for (float t = 10.5f; t <= 60f; t += 0.5f)
            any |= host.Observe(true, true, wait, "BeginTurnFinished", HostAckWatch.RemotePending(coop, local), t);
        Check(!any && host.Snapshots == 0, "only the master's entry left ends the episode");
    }

    private static void Line()
    {
        string line = new StuckTurnLine().Add("reason", "unexplained-20s").Add("waited", 20.04f).Add("button", false)
            .Add("turn", 2).Add("movement", "Choose Path").Add("focus", null).ToString();
        Check(line == "STUCK-TURN reason=unexplained-20s waited=20.0s button=0 turn=2 movement=Choose_Path focus=-",
            "stable key=value format, got " + line);

        string hostile = "a\nb\tc dé中" + new string('x', 400);
        var big = new StuckTurnLine();
        for (int i = 0; i < 40; i++) big.Add("k" + i, hostile);
        string capped = big.ToString();
        Check(capped.StartsWith(StuckTurnLine.Prefix + " ", StringComparison.Ordinal), "the prefix leads");
        Check(capped.Length <= StuckTurnLine.MaxLength && Encoding.UTF8.GetByteCount(capped) <= 2048,
            "the line stays under 2 KB, got " + Encoding.UTF8.GetByteCount(capped));
        Check(capped.EndsWith(" truncated=1", StringComparison.Ordinal), "a capped line says so");
        Check(capped.IndexOf('\n') < 0 && capped.IndexOf('\t') < 0 && Encoding.UTF8.GetByteCount(capped) == capped.Length,
            "one line of printable ASCII");
        Check(capped.Contains("k0=a_b_c_d__xxx") && capped.Contains("xxx..."), "values are sanitized and each is capped");
    }

    private static void NoAllocation()
    {
        var watch = new StuckTurnWatch();
        var host = new HostAckWatch();
        object wait = new object();
        StuckTurnPoll idle = new StuckTurnPoll { InRun = true, MyTurn = true, OverworldGate = true, Interactable = true };
        StuckTurnPoll stuck = Stuck();
        List<int> pending = new List<int> { 1, 2 };
        float t = 0f;
        for (int i = 0; i < 1000; i++)
        {
            watch.Observe(idle, t);
            host.Observe(true, true, wait, "Sync Board", HostAckWatch.RemotePending(pending, 1), t);
            t += 0.5f;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++)
        {
            watch.Observe((i & 1) == 0 ? idle : stuck, t);
            host.Observe(true, (i & 2) == 0, wait, "Sync Board", HostAckWatch.RemotePending(pending, 1), t);
            t += 0.5f;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "polling allocated " + allocated + " bytes");
    }
}
