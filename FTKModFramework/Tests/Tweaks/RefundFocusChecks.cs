// Game-free checks for convenience.refund-movement-focus (Spec #264 FR-1 to FR-4): the descriptor,
// counting only real conversions, the min(count, action points) bound, every gate, the exact reversal
// through a recording setter seam, every clear trigger and the off path. The Harmony patches and the
// HUD are live-gated on #264.
using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class RefundFocusChecks
{
    private const string Id = "convenience.refund-movement-focus";

    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("refund focus: " + message);
    }

    internal static int Run()
    {
        Descriptor();
        Counting();
        Bound();
        Gates();
        Reversal();
        Clears();
        OffPath();
        return _checks;
    }

    private sealed class Store : ITweakPreferenceStore
    {
        internal readonly Dictionary<string, TweakPreference> Values = new Dictionary<string, TweakPreference>();
        public TweakPreference Read(string id)
        {
            TweakPreference value;
            return Values.TryGetValue(id, out value) ? value : TweakPreference.Default;
        }
        public void Write(string id, TweakPreference preference) { Values[id] = preference; }
    }

    /// <summary>Records every setter call in order, so a test can assert the exact reversal.</summary>
    private sealed class Recorder : IRefundFocusSetters
    {
        internal readonly List<string> Calls = new List<string>();
        internal RefundFocusLedger Ledger;
        internal long Key;
        internal int CountAtReset = -1;

        public void UpdateFocusPoints(int delta) { Calls.Add("UpdateFocusPoints(" + delta + ")"); }
        public void UpdatePlayerAction(int delta) { Calls.Add("UpdatePlayerAction(" + delta + ")"); }
        public void TrackResetList()
        {
            Calls.Add("TrackResetList");
            if (Ledger != null) CountAtReset = Ledger.Count(Key);
        }
        public void RefreshHud() { Calls.Add("RefreshHud"); }
    }

    /// <summary>The shipped registry with the tweak on in a locked solo run, and its handle by ID.</summary>
    private static TweakRegistry SoloRun(out int handle, bool on = true)
    {
        var r = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(r);
        var store = new Store();
        store.Values[Id] = on ? TweakPreference.On : TweakPreference.Off;
        r.Initialize(store);
        r.TryGetHandle(Id, out handle);
        r.Capture(TweakSessionMode.SinglePlayer);
        r.Lock();
        return r;
    }

    /// <summary>A state where every gate passes: one conversion, 3 of 5 focus, 4 action points,
    /// the owner's turn, tracking with only the start hex.</summary>
    private static RefundFocusState Open()
    {
        var s = new RefundFocusState();
        s.Count = 1;
        s.FocusPoints = 3;
        s.MaxFocus = 5;
        s.ActionPoints = 4;
        s.Owner = true;
        s.MyTurn = true;
        s.MovementCharacter = true;
        s.TrackerActive = true;
        s.FsmTracking = true;
        s.CommittedHexes = 1;
        return s;
    }

    private static void Descriptor()
    {
        TweakDescriptor d = FrameworkTweaks.RefundMovementFocusDescriptor;
        Check(d.Id == Id && d.Category == TweakCategory.Convenience && d.Scope == TweakScope.Session,
            "a Session Convenience tweak with the specified ID");
        Check(!d.DefaultOn && d.ExplicitDefault == null && d.BalanceNote == null, "off by default with no balance note");
        Check(d.Title == "Refund movement focus" && !string.IsNullOrEmpty(d.Summary), "the title and a summary");
        foreach (string member in new[] { "Movement.ConvertFocusToAction", "UpdateFocusPoints(-1)", "UpdatePlayerAction(1)",
            "IsOwner", "FTKPlayerID", "MaxFocus", "m_IsMyTurn", "m_CharacterOverworld", "TrackingMode.Movement",
            "\"Tracking\"", "m_HexList.Count <= 1", "m_LockedInput", "\"PickSneakHex\"", "EncounterSession.m_IsInCombat",
            "UpdateFocusPoints(1)", "UpdatePlayerAction(-1)", "TrackResetList", "UpdateHud", "CharacterOverworld.EndTurn",
            "StartEncounterSession_Actual", "SetInCombat(true)", "SetDeath" })
            Check(d.Evidence.Contains(member), "the evidence names " + member);
        Check(d.Summary.IndexOf('\u2014') < 0 && d.Evidence.IndexOf('\u2014') < 0, "no em dashes in the descriptor text");

        var r = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(r);
        int byId;
        Check(r.TryGetHandle(Id, out byId) && byId == FrameworkTweaks.RefundMovementFocus && r.Get(byId) == d,
            "registers cleanly and resolves by ID");
        Check(r.Initialize(new Store()) && !r.PreferredOn(byId), "a fresh install prefers it off");
        Check(r.Capture(TweakSessionMode.SinglePlayer) && r.Lock() && !r.IsOn(byId), "a fresh solo run leaves it off");
    }

    // FR-1: only a conversion vanilla's guard allowed, by the owner, where both points moved.
    private static void Counting()
    {
        int handle;
        TweakRegistry r = SoloRun(out handle);
        Check(r.IsOn(handle), "on in the solo run");

        RefundFocusConversion before = RefundFocus.Before(true, 3, 4);
        Check(before.Captured && before.Owner && before.FocusBefore == 3 && before.ActionBefore == 4, "the prefix captures the owner's guard");
        Check(FrameworkTweaks.RefundFocusCounts(r, handle, before, 2, 5), "a real conversion counts");
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, before, 3, 4), "nothing moved: not counted");
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, before, 2, 4), "focus spent but no action point: not counted");
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, before, 3, 5), "an action point without focus: not counted");

        Check(!RefundFocus.Before(true, 0, 4).Captured, "no focus: vanilla's guard fails, nothing captured");
        Check(!RefundFocus.Before(true, 3, 9).Captured, "9 action points: vanilla's guard fails, nothing captured");
        Check(!RefundFocus.Before(false, 3, 4).Captured, "a character this client does not own is never counted");
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, RefundFocus.Before(false, 3, 4), 2, 5), "not the owner: not counted");
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, new RefundFocusConversion(), 2, 5), "a skipped prefix never counts");
        Check(RefundFocus.Before(true, 1, 8).Captured, "the last focus at 8 action points is still a conversion");

        var ledger = new RefundFocusLedger(r);
        long a = RefundFocusLedger.Key(7, 0), b = RefundFocusLedger.Key(7, 1);
        ledger.Add(a);
        ledger.Add(a);
        ledger.Add(b);
        Check(ledger.Count(a) == 2 && ledger.Count(b) == 1, "counts are per character, keyed by PhotonID and TurnIndex");
        Check(ledger.Count(RefundFocusLedger.Key(8, 0)) == 0, "another PhotonID with the same TurnIndex is another character");
        Check(RefundFocusLedger.Key(1, 0) != RefundFocusLedger.Key(0, 1) && RefundFocusLedger.Key(-1, 0) != RefundFocusLedger.Key(0, -1),
            "keys do not collide across the two halves");
    }

    // FR-2: refundable = min(count, action points).
    private static void Bound()
    {
        Check(RefundFocus.Refundable(2, 5) == 2, "the count bounds it");
        Check(RefundFocus.Refundable(3, 1) == 1, "action points spent below the count bound it");
        Check(RefundFocus.Refundable(3, 0) == 0, "no action points, nothing refundable");
        Check(RefundFocus.Refundable(0, 9) == 0, "no conversions, nothing refundable");
        Check(RefundFocus.Refundable(-1, 3) == 0 && RefundFocus.Refundable(2, -1) == 0, "never negative");

        RefundFocusState s = Open();
        s.Count = 3;
        s.ActionPoints = 2;
        Check(RefundFocus.Pips(s) == 2, "pips follow the bound");
        s.FocusPoints = 4;
        Check(RefundFocus.Pips(s) == 1, "pips never exceed the empty pips MaxFocus leaves");
        s.Count = 1;
        s.FocusPoints = 0;
        s.ActionPoints = 9;
        Check(RefundFocus.Pips(s) == 1, "the lighthouse at the cap still leaves one refundable point per conversion");
    }

    // FR-2: each gate on its own blocks the refund and hides every pip.
    private static void Gates()
    {
        Check(RefundFocus.Check(Open()) == RefundFocusBlock.None && RefundFocus.Pips(Open()) == 1, "the open state refunds one point");
        Expect(s => s.Count = 0, RefundFocusBlock.NothingToRefund, "no conversion this turn");
        Expect(s => s.ActionPoints = 0, RefundFocusBlock.NoActionPoints, "the converted point was walked");
        Expect(s => s.FocusPoints = 5, RefundFocusBlock.FocusFull, "focus already at MaxFocus");
        Expect(s => { s.FocusPoints = 6; s.MaxFocus = 5; }, RefundFocusBlock.FocusFull, "focus above a lowered MaxFocus");
        Expect(s => s.Owner = false, RefundFocusBlock.NotOwner, "another client's character");
        Expect(s => s.MyTurn = false, RefundFocusBlock.NotMyTurn, "not this character's turn");
        Expect(s => s.MovementCharacter = false, RefundFocusBlock.NotMovementCharacter, "Movement tracks another character");
        Expect(s => s.TrackerActive = false, RefundFocusBlock.TrackerInactive, "a walk in progress: TrackingPathFinished set m_Mode None");
        Expect(s => s.FsmTracking = false, RefundFocusBlock.NotTracking, "the movement FSM left Tracking");
        Expect(s => s.CommittedHexes = 2, RefundFocusBlock.PathCommitted, "a committed path");
        Expect(s => s.CommittedHexes = int.MaxValue, RefundFocusBlock.PathCommitted, "an unreadable path list counts as committed");
        Expect(s => s.SneakPick = true, RefundFocusBlock.SneakPick, "the sneak destination pick");
        Expect(s => s.InCombat = true, RefundFocusBlock.InCombat, "combat or an encounter");
        Expect(s => { s.TrackerActive = false; s.FsmTracking = false; s.CommittedHexes = 3; }, RefundFocusBlock.TrackerInactive,
            "a walk along a committed path is blocked");
        Check(RefundFocus.Check(new RefundFocusState()) != RefundFocusBlock.None, "the default state blocks");
        var flags = new RefundFocusState();
        flags.Count = 1; flags.ActionPoints = 1; flags.MaxFocus = 1;
        Check(RefundFocus.Check(flags) == RefundFocusBlock.NotOwner, "with resources present, the first default flag already blocks");
    }

    private static void Expect(Action<RefundFocusStateBox> change, RefundFocusBlock block, string label)
    {
        var box = new RefundFocusStateBox { State = Open() };
        change(box);
        Check(RefundFocus.Check(box.State) == block, label + ": " + block + " expected, got " + RefundFocus.Check(box.State));
        Check(RefundFocus.Pips(box.State) == 0, label + ": no pip is shown");
        int handle;
        TweakRegistry r = SoloRun(out handle);
        var ledger = new RefundFocusLedger(r);
        long key = RefundFocusLedger.Key(1, 0);
        ledger.Add(key);
        var recorder = new Recorder();
        Check(FrameworkTweaks.RefundFocusApply(r, handle, ledger, key, box.State, recorder) == block
            && recorder.Calls.Count == 0 && ledger.Count(key) == 1, label + ": nothing is called and the count stays");
    }

    /// <summary>Lets a lambda change the struct in place.</summary>
    private sealed class RefundFocusStateBox
    {
        internal RefundFocusState State;
        internal int Count { set { State.Count = value; } }
        internal int ActionPoints { set { State.ActionPoints = value; } }
        internal int FocusPoints { set { State.FocusPoints = value; } }
        internal int MaxFocus { set { State.MaxFocus = value; } }
        internal bool Owner { set { State.Owner = value; } }
        internal bool MyTurn { set { State.MyTurn = value; } }
        internal bool MovementCharacter { set { State.MovementCharacter = value; } }
        internal bool TrackerActive { set { State.TrackerActive = value; } }
        internal bool FsmTracking { set { State.FsmTracking = value; } }
        internal int CommittedHexes { set { State.CommittedHexes = value; } }
        internal bool SneakPick { set { State.SneakPick = value; } }
        internal bool InCombat { set { State.InCombat = value; } }
    }

    // FR-3: exactly the two setters, the count, then the path reset and the HUD.
    private static void Reversal()
    {
        int handle;
        TweakRegistry r = SoloRun(out handle);
        var ledger = new RefundFocusLedger(r);
        long key = RefundFocusLedger.Key(3, 1);
        ledger.Add(key);
        ledger.Add(key);
        var recorder = new Recorder { Ledger = ledger, Key = key };
        RefundFocusState s = Open();
        s.Count = ledger.Count(key);
        Check(FrameworkTweaks.RefundFocusApply(r, handle, ledger, key, s, recorder) == RefundFocusBlock.None, "an open refund applies");
        Check(string.Join(",", recorder.Calls.ToArray()) == "UpdateFocusPoints(1),UpdatePlayerAction(-1),TrackResetList,RefreshHud",
            "exactly UpdateFocusPoints(1), UpdatePlayerAction(-1), TrackResetList and a HUD refresh, in that order: "
            + string.Join(",", recorder.Calls.ToArray()));
        Check(recorder.CountAtReset == 1 && ledger.Count(key) == 1, "the count drops by one before the path reset");

        s.Count = ledger.Count(key);
        recorder.Calls.Clear();
        Check(FrameworkTweaks.RefundFocusApply(r, handle, ledger, key, s, recorder) == RefundFocusBlock.None
            && ledger.Count(key) == 0 && ledger.Characters == 0, "the second refund empties the character's entry");
        s.Count = ledger.Count(key);
        recorder.Calls.Clear();
        Check(FrameworkTweaks.RefundFocusApply(r, handle, ledger, key, s, recorder) == RefundFocusBlock.NothingToRefund
            && recorder.Calls.Count == 0, "a third refund is refused without a call");
        Check(!ledger.Take(key), "taking from an empty ledger reports nothing taken");
    }

    // FR-4: every clear trigger, and no persistence across a Session set change.
    private static void Clears()
    {
        int handle;
        TweakRegistry r = SoloRun(out handle);
        var ledger = new RefundFocusLedger(r);
        long a = RefundFocusLedger.Key(1, 0), b = RefundFocusLedger.Key(1, 1);

        Action fill = () => { ledger.Add(a); ledger.Add(a); ledger.Add(b); };
        fill();
        Check(FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.EndTurn, a)
            && ledger.Count(a) == 0 && ledger.Count(b) == 1, "EndTurn clears that character only");
        Check(FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.Death, b) && ledger.Count(b) == 0, "SetDeath clears that character");
        fill();
        Check(FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.EnterCombat, a, false) && ledger.Count(a) == 2,
            "SetInCombat(false) clears nothing");
        Check(FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.EnterCombat, a, true)
            && ledger.Count(a) == 0 && ledger.Count(b) == 1, "SetInCombat(true) clears that character");
        fill();
        Check(FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.EncounterStart, 0L) && ledger.Characters == 0,
            "an encounter start clears every character");

        // Leaving the room, returning to the title and any other Session set change empty the ledger
        // through the registry's Session generation, not through a hook of their own.
        fill();
        int generation = r.SessionGeneration;
        Check(!r.Lock() && r.SessionGeneration == generation && ledger.Count(a) == 2, "a lock changes nothing");
        Check(r.Clear() && r.SessionGeneration != generation && ledger.Characters == 0, "a clear (left room, title, run end) empties it");
        fill();
        generation = r.SessionGeneration;
        Check(r.Capture(TweakSessionMode.SinglePlayer) && r.SessionGeneration != generation && ledger.Count(a) == 0,
            "a capture for the next run empties it");
        fill();
        generation = r.SessionGeneration;
        Check(r.SetSessionSet(new[] { Id }, "save") && r.SessionGeneration != generation && ledger.Characters == 0,
            "a replaced Session set (a save's record) empties it");
        fill();
        Check(r.Capture(TweakSessionMode.Multiplayer) && !r.IsOn(handle) && ledger.Characters == 0,
            "a co-op capture turns it off and empties it");

        // Scene reload through the real lifecycle.
        TweakRegistry run = SoloRun(out handle);
        var lifecycle = new TweakSessionLifecycle(run, null, null, null, () => TweakRegistry.InvalidHandle);
        var runLedger = new RefundFocusLedger(run);
        runLedger.Add(a);
        Check(lifecycle.Clear(TweakClearTrigger.LeftRoom, "uiStartGame.OnLeftRoom") && runLedger.Count(a) == 0, "OnLeftRoom empties it");
        lifecycle.Capture(0, "GameLogic.CreateOfflineRoom");
        lifecycle.Lock(0, "uiStartGame.EnterFahrulRPC");
        runLedger.Add(a);
        Check(lifecycle.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish") && runLedger.Count(a) == 0,
            "the return to the title empties it");
    }

    // Off, before initialization, an invalid handle and a Session fault.
    private static void OffPath()
    {
        int handle;
        TweakRegistry off = SoloRun(out handle, false);
        NeverActs(off, handle, "off in the run");
        NeverActs(off, TweakRegistry.InvalidHandle, "an invalid handle");
        var uninitialized = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(uninitialized);
        NeverActs(uninitialized, FrameworkTweaks.RefundMovementFocus, "before initialization");

        var outside = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(outside);
        var store = new Store();
        store.Values[Id] = TweakPreference.On;
        outside.Initialize(store);
        outside.TryGetHandle(Id, out handle);
        NeverActs(outside, handle, "on but outside a run");

        TweakRegistry run = SoloRun(out handle);
        run.Fault(handle, new InvalidOperationException("refund"));
        Check(run.IsOn(handle), "a Session fault leaves the current run's rules alone");
        run.Clear();
        Check(run.Capture(TweakSessionMode.SinglePlayer) && !run.IsOn(handle), "the faulted tweak is left out of the next run");
        NeverActs(run, handle, "faulted, next run");
    }

    private static void NeverActs(TweakRegistry r, int handle, string label)
    {
        var ledger = new RefundFocusLedger(r);
        long key = RefundFocusLedger.Key(1, 0);
        ledger.Add(key);
        Check(!FrameworkTweaks.RefundFocusCounts(r, handle, RefundFocus.Before(true, 3, 4), 2, 5), label + ": nothing is counted");
        Check(FrameworkTweaks.RefundFocusGate(r, handle, Open()) == RefundFocusBlock.Off, label + ": the gate reports Off");
        Check(FrameworkTweaks.RefundFocusPips(r, handle, Open()) == 0, label + ": no pip is shown");
        var recorder = new Recorder();
        Check(FrameworkTweaks.RefundFocusApply(r, handle, ledger, key, Open(), recorder) == RefundFocusBlock.Off
            && recorder.Calls.Count == 0, label + ": no setter is called");
        Check(!FrameworkTweaks.RefundFocusClear(r, handle, ledger, RefundFocusClear.EncounterStart, key) && ledger.Count(key) == 1,
            label + ": a clear hook does nothing");
    }
}
