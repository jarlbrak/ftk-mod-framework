// Game-free checks for fix.stuck-skip-turn-popup (Spec #265 FR-4): the descriptor, every episode
// trigger, recovery at most once, pause, abandonment, the turn check, the once-only continuation
// guard, a normal skip turn, and the off path. The Harmony hooks and the driver are live-gated.
using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class SkipTurnChecks
{
    private const string Id = "fix.stuck-skip-turn-popup";

    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("stuck skip turn: " + message);
    }

    internal static int Run()
    {
        Descriptor();
        NormalSkipTurn();
        ForceHideTrigger();
        RootAndHost();
        Timeout();
        Abandonment();
        TurnCheck();
        Guard();
        InterruptedFlow();
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

    private static TweakRegistry Registry(out int handle, TweakPreference preference = TweakPreference.Default)
    {
        var r = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(r);
        var store = new Store();
        store.Values[Id] = preference;
        r.Initialize(store);
        r.TryGetHandle(Id, out handle);
        return r;
    }

    private static readonly long Owner = RefundFocusLedger.Key(1, 0);
    private static readonly long Other = RefundFocusLedger.Key(1, 1);

    /// <summary>Polls every 1/60 s of scaled time from start (exclusive) to end (inclusive) with the
    /// panel shown, and returns the first trigger and its time.</summary>
    private static SkipTurnTrigger Frames(SkipTurnEpisode e, float start, float end, out float at)
    {
        at = -1f;
        const float step = 1f / 60f;
        for (float t = start + step; t <= end + 0.0001f; t += step)
        {
            SkipTurnTrigger trigger = e.Poll(true, true, t);
            if (trigger != SkipTurnTrigger.None)
            {
                at = t;
                return trigger;
            }
        }
        return SkipTurnTrigger.None;
    }

    private static void Descriptor()
    {
        TweakDescriptor d = FrameworkTweaks.StuckSkipTurnPopupDescriptor;
        Check(d.Id == Id && d.Category == TweakCategory.Fix && d.Scope == TweakScope.Local, "a Local Fix with the specified ID");
        Check(d.DefaultOn && d.ExplicitDefault == null && d.BalanceNote == null, "on by default as a Fix, with no balance note");
        Check(d.Title == "Unstick the skip-turn popup" && d.Summary.Contains("skip-turn popup"), "the title and summary");
        foreach (string member in new[] { "SkipTurnUI.Show", "uiMovementSlots.ShowActionPanelRPC", "InitializeSkipTurn",
            "ForceHide", "RemoveSkipTurn", "SlotDisplayExpandSkipTurn", "m_ActionSlotDisplayTimeout", "Time.deltaTime",
            "SkipTurnUI.Close(GetCurrentCOW().m_FTKPlayerID.IsLocal())", "m_IsFading", "StopAllCoroutines", "m_Root",
            "10 s of Time.time", "Initialize action roll", "m_ContinueFSM", "WaitClients.None" })
            Check(d.Evidence.Contains(member), "the evidence names " + member);
        Check(d.Summary.IndexOf('—') < 0 && d.Evidence.IndexOf('—') < 0, "no em dashes in the descriptor text");

        int handle;
        TweakRegistry r = Registry(out handle);
        Check(handle == FrameworkTweaks.StuckSkipTurnPopup && r.Get(handle) == d, "registers cleanly and resolves by ID");
        Check(r.PreferredOn(handle) && r.IsOn(handle), "a fresh install has it on, outside a run too, as a Local tweak");
    }

    // A skip turn that closes on its own is never touched, and its continuation goes through once.
    private static void NormalSkipTurn()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var e = new SkipTurnEpisode();
        var guard = new ContinueOnceGuard();
        object continuation = new object();

        Check(e.State == SkipTurnEpisodeState.Idle && e.Poll(true, true, 0f) == SkipTurnTrigger.None, "idle does nothing");
        e.Arm(Owner, 100f);
        Check(e.State == SkipTurnEpisodeState.Open && e.Player == Owner, "InitializeSkipTurn arms it for the character");
        float at;
        Check(Frames(e, 100f, 104.8f, out at) == SkipTurnTrigger.None, "vanilla's 4.8 s display triggers nothing");
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, continuation), "vanilla's close continues");
        e.Closed();
        Check(e.State == SkipTurnEpisodeState.Idle, "the close ends the episode");
        Check(Frames(e, 104.8f, 130f, out at) == SkipTurnTrigger.None, "nothing fires after a normal close");
        Check(e.Poll(false, false, 131f) == SkipTurnTrigger.None, "the fade hiding the panel afterwards is not a trigger");

        object next = new object();
        e.Arm(Owner, 200f);
        e.Closed();
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, next), "the next skip turn's new continuation continues");
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, false, next) == false,
            "a remote close without a continue stays without one");
    }

    private static void ForceHideTrigger()
    {
        var e = new SkipTurnEpisode();
        e.ForceHide(true);
        Check(e.State == SkipTurnEpisodeState.Idle, "ForceHide with no episode does nothing");

        e.Arm(Owner, 0f);
        e.ForceHide(false);
        Check(e.State == SkipTurnEpisodeState.Open, "ForceHide with the root already hidden stops no coroutine");
        e.ForceHide(true);
        Check(e.State == SkipTurnEpisodeState.Pending && e.Trigger == SkipTurnTrigger.ForceHide, "ForceHide on a shown root is recorded");
        Check(e.Poll(false, true, 0.1f) == SkipTurnTrigger.ForceHide, "the next frame recovers");
        Check(e.State == SkipTurnEpisodeState.Idle, "recovery ends the episode");
        Check(e.Poll(false, true, 0.2f) == SkipTurnTrigger.None && e.Poll(false, true, 50f) == SkipTurnTrigger.None,
            "recovery happens once");

        e.Arm(Owner, 0f);
        e.ForceHide(true);
        e.Closed();
        Check(e.Poll(false, true, 0.1f) == SkipTurnTrigger.None, "a close before the next frame cancels the recovery");
    }

    private static void RootAndHost()
    {
        var e = new SkipTurnEpisode();
        e.Arm(Owner, 0f);
        Check(e.Poll(true, true, 1f) == SkipTurnTrigger.None, "a shown root on an active host waits");
        Check(e.Poll(false, true, 1.1f) == SkipTurnTrigger.RootHidden && e.State == SkipTurnEpisodeState.Idle,
            "a root hidden before the close recovers once");

        e.Arm(Owner, 0f);
        Check(e.Poll(true, false, 1f) == SkipTurnTrigger.None && e.State == SkipTurnEpisodeState.Pending
            && e.Trigger == SkipTurnTrigger.HostInactive, "an inactive host is recorded but cannot run Close yet");
        Check(e.Poll(false, false, 30f) == SkipTurnTrigger.None, "still inactive, still waiting");
        Check(e.Poll(true, true, 31f) == SkipTurnTrigger.HostInactive, "recovery runs once the host is active again");
        Check(e.Poll(true, true, 32f) == SkipTurnTrigger.None, "and only once");

        e.Arm(Owner, 0f);
        e.ForceHide(true);
        Check(e.Poll(false, false, 0.1f) == SkipTurnTrigger.None && e.Trigger == SkipTurnTrigger.ForceHide,
            "a ForceHide recovery also waits for an active host and keeps its reason");
        Check(e.Poll(false, true, 5f) == SkipTurnTrigger.ForceHide, "then recovers");
    }

    private static void Timeout()
    {
        var e = new SkipTurnEpisode();
        e.Arm(Owner, 50f);
        float at;
        Check(Frames(e, 50f, 59.9f, out at) == SkipTurnTrigger.None, "9.9 s of scaled time is not enough");
        Check(Frames(e, 59.9f, 61f, out at) == SkipTurnTrigger.Timeout && at >= 60f - 0.0001f && at < 60.1f,
            "the fallback fires at 10 s, got " + at);
        Check(e.State == SkipTurnEpisodeState.Idle, "the fallback recovers once");

        // A pause freezes Time.time, as it freezes the coroutine's own clock.
        e.Arm(Owner, 10f);
        bool fired = false;
        for (int i = 0; i < 100000; i++) fired |= e.Poll(true, true, 15f) != SkipTurnTrigger.None;
        Check(!fired && e.State == SkipTurnEpisodeState.Open, "a paused game never triggers, however many frames pass");
        Check(e.Poll(true, true, 19.99f) == SkipTurnTrigger.None && e.Poll(true, true, 20f) == SkipTurnTrigger.Timeout,
            "only scaled time counts toward the fallback");
    }

    private static void Abandonment()
    {
        var e = new SkipTurnEpisode();
        Check(!e.Abandon(), "nothing to abandon when idle");
        e.Arm(Owner, 0f);
        Check(e.Abandon() && e.State == SkipTurnEpisodeState.Idle, "a new popup or an action roll drops an open episode");
        Check(e.Poll(false, false, 100f) == SkipTurnTrigger.None, "an abandoned episode never recovers");

        e.Arm(Owner, 0f);
        e.ForceHide(true);
        Check(e.Abandon() && e.Poll(false, true, 1f) == SkipTurnTrigger.None, "a pending recovery is dropped too");

        e.Arm(Owner, 0f);
        e.Abandon();
        e.Arm(Other, 3f);
        Check(e.Player == Other && e.State == SkipTurnEpisodeState.Open, "the replacing popup arms its own episode");
        Check(e.Poll(true, true, 12.9f) == SkipTurnTrigger.None && e.Poll(true, true, 13f) == SkipTurnTrigger.Timeout,
            "its clock starts at its own arm");
    }

    private static void TurnCheck()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        Check(FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, SkipTurnTrigger.ForceHide, Owner, Owner),
            "the armed character still holds the turn: close");
        foreach (SkipTurnTrigger t in new[] { SkipTurnTrigger.RootHidden, SkipTurnTrigger.HostInactive, SkipTurnTrigger.Timeout })
            Check(FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, t, Owner, Owner), t + " closes");
        Check(!FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, SkipTurnTrigger.Timeout, Owner, Other),
            "the turn has moved on: no close");
        Check(!FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, SkipTurnTrigger.Timeout, Owner, long.MinValue),
            "no current character: no close");
        Check(!FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, SkipTurnTrigger.None, Owner, Owner), "no trigger: no close");
    }

    private static void Guard()
    {
        var guard = new ContinueOnceGuard();
        object a = new object(), b = new object();
        Check(guard.Allow(a), "the first close of a continuation is allowed");
        Check(!guard.Allow(a) && !guard.Allow(a), "a repeat of the same continuation is refused");
        Check(guard.Allow(b), "a new continuation is allowed");
        Check(!guard.Allow(b), "and refused on its repeat");
        Check(guard.Allow(null) && guard.Allow(null), "no continuation: nothing to guard");
        Check(!guard.Allow(b), "a null close does not reset the guard");
    }

    // The interrupted popup: our recovery continues once, and vanilla's later close cannot again.
    private static void InterruptedFlow()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var e = new SkipTurnEpisode();
        var guard = new ContinueOnceGuard();
        object continuation = new object();

        e.Arm(Owner, 0f);
        e.ForceHide(true);
        SkipTurnTrigger trigger = e.Poll(false, true, 0.02f);
        Check(FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, trigger, e.Player, Owner), "the recovery closes");
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, continuation), "the recovery's Close(true) continues");
        e.Closed();
        Check(!FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, continuation),
            "a late vanilla Close(true) with the same continuation runs as Close(false)");

        // The fallback fired while the coroutine was only slow.
        object slow = new object();
        e.Arm(Owner, 0f);
        float at;
        Check(Frames(e, 0f, 11f, out at) == SkipTurnTrigger.Timeout, "the fallback fires");
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, slow), "its close continues once");
        Check(!FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, slow), "the coroutine's own close does not");
    }

    private static void OffPath()
    {
        int handle;
        TweakRegistry off = Registry(out handle, TweakPreference.Off);
        NeverActs(off, handle, "off");
        NeverActs(off, TweakRegistry.InvalidHandle, "an invalid handle");

        var uninitialized = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(uninitialized);
        NeverActs(uninitialized, FrameworkTweaks.StuckSkipTurnPopup, "before initialization");

        TweakRegistry faulted = Registry(out handle);
        faulted.Fault(handle, new InvalidOperationException("skip turn"));
        Check(!faulted.IsOn(handle), "a Local fault turns it off for the process");
        NeverActs(faulted, handle, "faulted");
    }

    private static void NeverActs(TweakRegistry r, int handle, string label)
    {
        var guard = new ContinueOnceGuard();
        object continuation = new object();
        guard.Allow(continuation);
        Check(FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, true, continuation), label + ": vanilla's continue stands");
        Check(!FrameworkTweaks.SkipTurnCloseContinues(r, handle, guard, false, continuation), label + ": vanilla's no-continue stands");
        Check(!FrameworkTweaks.SkipTurnRecoveryCloses(r, handle, SkipTurnTrigger.Timeout, Owner, Owner), label + ": no recovery close");
    }
}
