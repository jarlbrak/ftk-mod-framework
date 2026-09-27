using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal sealed class MemoryStore : ITweakPreferenceStore
{
    internal readonly Dictionary<string, TweakPreference> Values = new Dictionary<string, TweakPreference>();
    internal int Writes;
    internal bool ThrowOnRead;

    public TweakPreference Read(string id)
    {
        if (ThrowOnRead) throw new InvalidOperationException("config unreadable");
        TweakPreference value;
        return Values.TryGetValue(id, out value) ? value : TweakPreference.Default;
    }

    public void Write(string id, TweakPreference preference)
    {
        Writes++;
        Values[id] = preference;
    }
}

internal static class Program
{
    private static int _checks;
    private static readonly List<string> Logs = new List<string>();

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    private static TweakRegistry NewRegistry()
    {
        Logs.Clear();
        return new TweakRegistry(Logs.Add);
    }

    private static TweakDescriptor Local(string id, TweakCategory category = TweakCategory.Convenience, bool? defaultOn = null)
    {
        return new TweakDescriptor(id, category, TweakScope.Local, "Title " + id, "Summary", "Evidence", null, defaultOn);
    }

    private static TweakDescriptor Session(string id, TweakCategory category = TweakCategory.Convenience, bool? defaultOn = null)
    {
        return new TweakDescriptor(id, category, TweakScope.Session, "Title " + id, "Summary", "Evidence", null, defaultOn);
    }

    private static void Main()
    {
        Validation();
        Defaults();
        Ordering();
        PreferenceResolution();
        PreferencesThroughRegistry();
        ModeMatrix();
        Lifecycle();
        Faults();
        UnknownHandlesAndPreInitialization();
        NonAllocating();
        Facade();
        SkipIntro();
        LifecycleDecisions();
        LifecycleHooks();
        SessionProbe();
        Console.WriteLine("Tweaks: " + _checks + " checks passed (registry, preferences, session lifecycle, faults, skip intro, lifecycle hooks, session probe).");
    }

    // FR-1: IDs, duplicates, balance-note defaults, and a freeze once initialized.
    private static void Validation()
    {
        TweakRegistry r = NewRegistry();
        Check(r.Register(Local("convenience.skip-intro")) == 0, "a valid ID gets the first handle");
        Check(r.Register(Local("fix.a1-b2.c")) == 1, "digits, hyphens and several segments are valid");
        Check(r.Register(Local("single")) == 2, "a single segment is valid");
        Check(Logs.Count == 0, "valid registrations do not log");

        string[] malformed = { null, "", "Convenience.skip", "a..b", ".a", "a.", "a b", "a_b", "café", "a/b", "." };
        foreach (string id in malformed)
        {
            int before = Logs.Count;
            Check(r.Register(Local(id)) == TweakRegistry.InvalidHandle, "malformed ID is rejected: " + id);
            Check(Logs.Count == before + 1, "a malformed ID logs exactly once: " + id);
        }
        Check(r.Count == 3, "malformed IDs are not registered");

        Logs.Clear();
        var duplicate = new TweakDescriptor("convenience.skip-intro", TweakCategory.Fix, TweakScope.Session, "Other", "", "");
        Check(r.Register(duplicate) == TweakRegistry.InvalidHandle, "a duplicate ID is rejected");
        Check(Logs.Count == 1 && Logs[0].Contains("duplicate"), "a duplicate logs once");
        Check(r.Get(0).Title == "Title convenience.skip-intro" && r.Get(0).Scope == TweakScope.Local, "the first registration is kept");
        int handle;
        Check(r.TryGetHandle("convenience.skip-intro", out handle) && handle == 0, "the ID still maps to the first handle");

        Logs.Clear();
        var noteNoDefault = new TweakDescriptor("fix.balance", TweakCategory.Fix, TweakScope.Session, "Balance", "", "", "Changes damage");
        Check(r.Register(noteNoDefault) == TweakRegistry.InvalidHandle && Logs.Count == 1, "a Fix with a balance note and no explicit default is rejected once");
        var noteOn = new TweakDescriptor("fix.balance-on", TweakCategory.Fix, TweakScope.Session, "Balance on", "", "", "Changes damage", true);
        var noteOff = new TweakDescriptor("fix.balance-off", TweakCategory.Fix, TweakScope.Session, "Balance off", "", "", "Changes damage", false);
        Check(r.Register(noteOn) >= 0 && r.Register(noteOff) >= 0, "a Fix with a balance note and an explicit default is accepted");
        var noteConvenience = new TweakDescriptor("convenience.noted", TweakCategory.Convenience, TweakScope.Local, "Noted", "", "", "Note");
        Check(r.Register(noteConvenience) >= 0, "the explicit-default rule applies only to Fix tweaks");

        Logs.Clear();
        Check(r.Register(null) == TweakRegistry.InvalidHandle && Logs.Count == 1, "a null descriptor is rejected once");
        var badCategory = new TweakDescriptor("bad.category", (TweakCategory)9, TweakScope.Local, "Bad", "", "");
        var badScope = new TweakDescriptor("bad.scope", TweakCategory.Fix, (TweakScope)9, "Bad", "", "");
        var noTitle = new TweakDescriptor("bad.title", TweakCategory.Fix, TweakScope.Local, "", "", "");
        Check(r.Register(badCategory) == TweakRegistry.InvalidHandle, "an undefined category is rejected");
        Check(r.Register(badScope) == TweakRegistry.InvalidHandle, "an undefined scope is rejected");
        Check(r.Register(noTitle) == TweakRegistry.InvalidHandle, "a missing title is rejected");
        Check(Logs.Count == 4, "each rejection logs once");

        int registered = r.Count;
        Check(r.Initialize(new MemoryStore()), "initialization succeeds");
        Logs.Clear();
        Check(r.Register(Local("late.tweak")) == TweakRegistry.InvalidHandle && Logs.Count == 1, "registration after initialization is rejected once");
        Check(r.Count == registered, "a late registration is not added");
    }

    // FR-1: Fix defaults on, the rest default off, and explicit defaults win.
    private static void Defaults()
    {
        Check(Local("f", TweakCategory.Fix).DefaultOn, "Fix defaults on");
        Check(!Local("i", TweakCategory.Information).DefaultOn, "Information defaults off");
        Check(!Local("c", TweakCategory.Convenience).DefaultOn, "Convenience defaults off");
        Check(!Local("f", TweakCategory.Fix, false).DefaultOn, "an explicit off overrides the Fix default");
        Check(Local("c", TweakCategory.Convenience, true).DefaultOn, "an explicit on overrides the Convenience default");
        Check(!Local("c").ExplicitDefault.HasValue && Local("c", defaultOn: false).ExplicitDefault == false, "explicitness is observable");
    }

    private static void Ordering()
    {
        TweakRegistry r = NewRegistry();
        int zeta = r.Register(new TweakDescriptor("c.zeta", TweakCategory.Convenience, TweakScope.Local, "Zeta", "", ""));
        int info = r.Register(new TweakDescriptor("i.alpha", TweakCategory.Information, TweakScope.Local, "Alpha", "", ""));
        int fixB = r.Register(new TweakDescriptor("f.bravo", TweakCategory.Fix, TweakScope.Session, "bravo", "", ""));
        int alpha = r.Register(new TweakDescriptor("c.alpha", TweakCategory.Convenience, TweakScope.Local, "alpha", "", ""));
        int fixA = r.Register(new TweakDescriptor("f.alpha", TweakCategory.Fix, TweakScope.Local, "Alpha", "", ""));
        IList<int> order = r.DisplayOrder;
        Check(order.Count == 5, "every descriptor is in the display order");
        Check(order[0] == fixA && order[1] == fixB && order[2] == info && order[3] == alpha && order[4] == zeta,
            "display order is category, then title ignoring case");
    }

    // FR-2: only exact On and Off are choices; Default follows the current default.
    private static void PreferenceResolution()
    {
        TweakPreference seven = (TweakPreference)Enum.Parse(typeof(TweakPreference), "7", true);
        TweakPreference combined = (TweakPreference)Enum.Parse(typeof(TweakPreference), "On, Off", true);
        TweakPreference lower = (TweakPreference)Enum.Parse(typeof(TweakPreference), "on", true);
        Check((int)seven == 7 && (int)combined == 3, "Enum.Parse accepts the values BepInEx would store");
        TweakPreference[] notChoices = { seven, combined, (TweakPreference)(-1), (TweakPreference)7, TweakPreference.Default };
        foreach (TweakPreference value in notChoices)
        {
            Check(!TweakPreferences.IsExplicit(value), "not explicit: " + (int)value);
            Check(TweakPreferences.Normalize(value) == TweakPreference.Default, "normalizes to Default: " + (int)value);
            Check(TweakPreferences.Resolve(value, true) && !TweakPreferences.Resolve(value, false), "follows the default: " + (int)value);
        }
        Check(lower == TweakPreference.On && TweakPreferences.IsExplicit(lower), "a case-insensitive On is a choice");
        Check(TweakPreferences.Resolve(TweakPreference.On, false) && !TweakPreferences.Resolve(TweakPreference.Off, true), "explicit choices override the default");

        // Toggle flips the effective value and stores Default when it lands on the default.
        Check(TweakPreferences.Toggle(TweakPreference.Default, false) == TweakPreference.On, "default-off row toggles to On");
        Check(TweakPreferences.Toggle(TweakPreference.On, false) == TweakPreference.Default, "and back to Default");
        Check(TweakPreferences.Toggle(TweakPreference.Default, true) == TweakPreference.Off, "default-on row toggles to Off");
        Check(TweakPreferences.Toggle(TweakPreference.Off, true) == TweakPreference.Default, "and back to Default");
        Check(TweakPreferences.Toggle(TweakPreference.On, true) == TweakPreference.Off, "a redundant On flips to Off");
        Check(TweakPreferences.Toggle(TweakPreference.Off, false) == TweakPreference.On, "a redundant Off flips to On");
        Check(TweakPreferences.Toggle((TweakPreference)7, false) == TweakPreference.On, "an unparsed value toggles from the default");
        Check(TweakPreferences.Toggle(combined, true) == TweakPreference.Off, "a combined value toggles from the default");
    }

    private static void PreferencesThroughRegistry()
    {
        // A fresh install resolves every tweak to its default.
        var store = new MemoryStore();
        TweakRegistry r = NewRegistry();
        int fix = r.Register(Local("fix.one", TweakCategory.Fix));
        int info = r.Register(Local("info.one", TweakCategory.Information));
        int conv = r.Register(Local("convenience.one"));
        Check(r.Initialize(store), "initialize with an empty store");
        Check(r.IsOn(fix) && !r.IsOn(info) && !r.IsOn(conv), "a fresh install follows every default");
        Check(r.Preference(fix) == TweakPreference.Default && store.Writes == 0, "a fresh install stores nothing");

        // Toggling saves immediately, takes effect for Local tweaks at once, and toggling twice returns to Default.
        Check(r.Toggle(conv) && store.Values["convenience.one"] == TweakPreference.On && r.IsOn(conv), "toggle saves On and applies");
        Check(r.Toggle(conv) && store.Values["convenience.one"] == TweakPreference.Default && !r.IsOn(conv), "toggle twice returns to Default");
        Check(r.Toggle(fix) && store.Values["fix.one"] == TweakPreference.Off && !r.IsOn(fix) && !r.PreferredOn(fix), "a default-on row toggles to Off");

        // Unparsed stored values resolve to Default at load.
        store.Values["info.one"] = (TweakPreference)7;
        store.Values["convenience.one"] = (TweakPreference)3;
        TweakRegistry restarted = NewRegistry();
        restarted.Register(Local("fix.one", TweakCategory.Fix));
        restarted.Register(Local("info.one", TweakCategory.Information));
        restarted.Register(Local("convenience.one"));
        restarted.Initialize(store);
        Check(!restarted.IsOn(fix), "an explicit choice survives a restart");
        Check(restarted.Preference(info) == TweakPreference.Default && !restarted.IsOn(info), "a stored 7 resolves to Default");
        Check(restarted.Preference(conv) == TweakPreference.Default && !restarted.IsOn(conv), "a stored 'On, Off' resolves to Default");

        // A later build changes the defaults: explicit choices hold, Default rows follow.
        TweakRegistry changed = NewRegistry();
        changed.Register(Local("fix.one", TweakCategory.Fix, false));
        changed.Register(Local("info.one", TweakCategory.Information, true));
        changed.Register(Local("convenience.one", TweakCategory.Convenience, true));
        changed.Initialize(store);
        Check(changed.IsOn(info) && changed.IsOn(conv), "Default rows follow the new default");
        Check(changed.Toggle(fix) && store.Values["fix.one"] == TweakPreference.On && changed.IsOn(fix), "toggling against a new default stores the explicit value");
        TweakRegistry flippedBack = NewRegistry();
        flippedBack.Register(Local("fix.one", TweakCategory.Fix, true));
        flippedBack.Initialize(store);
        Check(flippedBack.IsOn(fix) && flippedBack.Preference(fix) == TweakPreference.On, "an explicit On survives a restart and another default change");
        TweakRegistry offDefault = NewRegistry();
        offDefault.Register(Local("fix.one", TweakCategory.Fix, false));
        offDefault.Initialize(store);
        Check(offDefault.IsOn(fix), "an explicit On that now differs from the default still holds");

        // Reset returns every row to Default.
        store.Values["info.one"] = TweakPreference.Off;
        Check(changed.Toggle(conv) && changed.ResetAll(), "reset succeeds");
        Check(store.Values["fix.one"] == TweakPreference.Default && store.Values["info.one"] == TweakPreference.Default
            && store.Values["convenience.one"] == TweakPreference.Default, "reset stores Default for every row");
        Check(changed.IsOn(fix) == false && changed.IsOn(info) && changed.IsOn(conv), "reset rows follow their defaults");

        // A retired key in the store is neither read nor rewritten.
        store.Values["retired.tweak"] = TweakPreference.On;
        int writes = store.Writes;
        TweakRegistry retired = NewRegistry();
        retired.Register(Local("fix.one", TweakCategory.Fix));
        retired.Initialize(store);
        Check(store.Values["retired.tweak"] == TweakPreference.On && store.Writes == writes && !retired.IsOn("retired.tweak"), "retired keys are left alone and ignored");
    }

    private static TweakRegistry SessionRegistry(MemoryStore store, out int sessionOn, out int sessionOff, out int local)
    {
        TweakRegistry r = NewRegistry();
        sessionOn = r.Register(Session("session.on"));
        sessionOff = r.Register(Session("session.off", TweakCategory.Fix, false));
        local = r.Register(Local("local.on"));
        store.Values["session.on"] = TweakPreference.On;
        store.Values["local.on"] = TweakPreference.On;
        r.Initialize(store);
        return r;
    }

    // FR-3: Session tweaks by game mode; Local tweaks always follow preferences.
    private static void ModeMatrix()
    {
        var store = new MemoryStore();
        int on, off, local;
        TweakRegistry r = SessionRegistry(store, out on, out off, out local);
        Check(r.SessionState == TweakSessionState.None && !r.IsOn(on) && r.IsOn(local), "no run: Session off, Local follows preference");

        TweakSessionMode[] shared = { TweakSessionMode.SinglePlayer, TweakSessionMode.LocalMultiplayer };
        foreach (TweakSessionMode mode in shared)
        {
            Check(r.Capture(mode), "capture " + mode);
            Check(r.IsOn(on) && !r.IsOn(off) && r.IsOn(local), mode + ": Session follows the player's preferences");
            Check(r.SessionMode == mode && r.SessionSource == TweakRegistry.PreferencesSource, mode + ": mode and source recorded");
            r.Clear();
        }

        Check(r.Capture(TweakSessionMode.Multiplayer), "capture Multiplayer");
        Check(!r.IsOn(on) && !r.IsOn(off) && r.IsOn(local), "Multiplayer: Session off until a source, Local unaffected");
        Check(r.SessionSource == TweakRegistry.PendingSource, "Multiplayer: source is pending");
        Check(r.SetSessionSet(new[] { "session.off", "local.on", "unknown.id" }, "host"), "a host set is accepted before lock");
        Check(!r.IsOn(on) && r.IsOn(off) && r.SessionSource == "host", "the supplied set replaces the Session set exactly");
        Check(r.IsOn(local), "a supplied set never touches Local tweaks");
        r.Clear();

        Check(r.Capture((TweakSessionMode)9) && !r.IsOn(on), "an unknown mode fails closed like Multiplayer");
        r.Clear();
        Check(!r.SetSessionSet(new[] { "session.on" }, "save") && !r.IsOn(on), "a set cannot be supplied without a captured run");
    }

    // FR-3: capture, lock and clear transitions, including the mid-run clear signal.
    private static void Lifecycle()
    {
        var store = new MemoryStore();
        int on, off, local;
        TweakRegistry r = SessionRegistry(store, out on, out off, out local);

        Check(!r.Lock() && r.SessionState == TweakSessionState.None, "lock without a capture is ignored");
        Check(!r.Clear(), "a clear with no run does not signal");

        r.Capture(TweakSessionMode.SinglePlayer);
        Check(r.SessionState == TweakSessionState.Captured, "capture enters Captured");
        Check(r.Toggle(off) && r.Capture(TweakSessionMode.SinglePlayer) && r.IsOn(off), "recapture before lock picks up new preferences");
        Check(r.Toggle(off) && r.IsOn(off), "a preference change after capture does not change the captured set");
        Check(!r.Clear(), "clearing a captured but unstarted run does not signal");
        Check(r.SessionState == TweakSessionState.None && r.SessionMode == null && r.SessionSource == null && !r.IsOn(on), "clear empties the Session state");

        r.Capture(TweakSessionMode.SinglePlayer);
        Check(r.Lock() && r.SessionState == TweakSessionState.Locked, "lock enters Locked");
        Check(!r.Lock(), "a second lock is ignored");
        Check(r.Toggle(on) && !r.PreferredOn(on) && r.IsOn(on), "a Session preference change mid-run does not affect the run");
        Check(r.Toggle(local) && !r.IsOn(local), "a Local preference change applies at once");
        Check(!r.Capture(TweakSessionMode.Multiplayer) && r.SessionMode == TweakSessionMode.SinglePlayer && r.IsOn(on), "capture after lock is ignored");
        Check(!r.SetSessionSet(new string[0], "host") && r.IsOn(on), "a set cannot replace a locked run");
        Check(r.Clear(), "clearing a locked run signals so the caller can warn");
        Check(r.SessionState == TweakSessionState.None && !r.IsOn(on), "the locked run is cleared");
        Check(r.Capture(TweakSessionMode.SinglePlayer) && !r.IsOn(on), "the next capture uses the changed preference");
    }

    // FR-4: both fault paths, logged once, surviving clears.
    private static void Faults()
    {
        var store = new MemoryStore();
        int on, off, local;
        TweakRegistry r = SessionRegistry(store, out on, out off, out local);
        var error = new InvalidOperationException("boom");

        Logs.Clear();
        r.Fault(local, error);
        r.Fault(local, error);
        Check(!r.IsOn(local) && r.IsFaulted(local), "a faulted Local tweak is off");
        Check(Logs.Count == 1 && Logs[0].Contains("local.on") && Logs[0].Contains("boom"), "a Local fault logs once with the exception");
        Check(r.PreferredOn(local), "the player's preference is untouched");
        r.Toggle(local);
        r.Toggle(local);
        Check(!r.IsOn(local), "toggling does not revive a faulted Local tweak");
        r.Capture(TweakSessionMode.SinglePlayer);
        r.Lock();
        r.Clear();
        Check(!r.IsOn(local) && r.IsFaulted(local), "a Local fault survives clears");

        Logs.Clear();
        r.Capture(TweakSessionMode.SinglePlayer);
        r.Lock();
        r.Fault(on, error);
        r.Fault(on, error);
        Check(r.IsOn(on) && r.IsFaulted(on), "a Session fault does not change the locked run");
        Check(Logs.Count == 1 && Logs[0].Contains("session.on"), "a Session fault logs once");
        Check(r.Clear() && r.IsFaulted(on), "a Session fault survives the clear");
        Check(r.Capture(TweakSessionMode.SinglePlayer) && !r.IsOn(on) && r.PreferredOn(on), "the faulted Session tweak is left out of the next capture");
        r.Clear();
        Check(r.IsFaulted(on), "the flag survives repeated clears");

        // A fault during setup leaves the captured set alone until the next capture.
        store.Values["session.off"] = TweakPreference.On;
        TweakRegistry setup = SessionRegistry(store, out on, out off, out local);
        setup.Capture(TweakSessionMode.LocalMultiplayer);
        setup.Fault(off, error);
        Check(setup.IsOn(off), "a Session fault never changes the current captured set");
        Check(setup.Capture(TweakSessionMode.LocalMultiplayer) && !setup.IsOn(off), "a recapture excludes it");

        // A supplied set is applied as given so every player shares the same rules.
        Check(setup.SetSessionSet(new[] { "session.off" }, "host") && setup.IsOn(off), "a supplied set includes a locally faulted tweak");
    }

    private static void UnknownHandlesAndPreInitialization()
    {
        var store = new MemoryStore();
        TweakRegistry r = NewRegistry();
        int fix = r.Register(Local("fix.pre", TweakCategory.Fix));
        int session = r.Register(Session("session.pre", TweakCategory.Fix));
        Check(!r.IsInitialized && !r.IsOn(fix) && !r.IsOn("fix.pre"), "IsOn is false before initialization, even for a default-on Fix");
        Check(!r.Capture(TweakSessionMode.SinglePlayer) && !r.IsOn(session), "capture is ignored before initialization");
        Check(!r.Toggle(fix) && !r.ResetAll() && store.Writes == 0, "preferences cannot change before initialization");
        Logs.Clear();
        r.Fault(fix, new Exception("early"));
        Check(Logs.Count == 0 && !r.IsFaulted(fix), "a fault before initialization is ignored");
        Check(!r.Clear(), "a clear before initialization is harmless");

        var broken = new MemoryStore { ThrowOnRead = true };
        Logs.Clear();
        Check(!r.Initialize(broken) && !r.IsInitialized && Logs.Count == 1, "a failed initialization logs and leaves the registry uninitialized");
        Check(!r.IsOn(fix), "IsOn stays false after a failed initialization");
        Check(!r.Initialize(null) && !r.IsInitialized, "a null store fails initialization");

        Check(r.Initialize(store) && r.IsOn(fix), "a later successful initialization takes effect");
        int[] unknown = { -1, 2, 99, int.MaxValue, int.MinValue };
        Logs.Clear();
        foreach (int handle in unknown)
        {
            Check(!r.IsOn(handle), "unknown handle is off: " + handle);
            Check(!r.Toggle(handle) && !r.IsFaulted(handle) && !r.PreferredOn(handle), "unknown handle is inert: " + handle);
            Check(r.Get(handle) == null && r.Preference(handle) == TweakPreference.Default, "unknown handle has no descriptor: " + handle);
            r.Fault(handle, new Exception("x"));
        }
        Check(Logs.Count == 0, "faults on unknown handles are ignored silently");
        Check(!r.IsOn("unknown.id") && !r.IsOn((string)null) && !r.IsOn(""), "unknown IDs are off");
    }

    // NFR-1: IsOn must not allocate on either overload.
    private static void NonAllocating()
    {
        TweakRegistry r = NewRegistry();
        int handle = r.Register(Local("fix.hot", TweakCategory.Fix));
        r.Initialize(new MemoryStore());
        bool sink = false;
        for (int i = 0; i < 1000; i++) sink ^= r.IsOn(handle) ^ r.IsOn("fix.hot") ^ r.IsOn(99);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) sink ^= r.IsOn(handle) ^ r.IsOn("fix.hot") ^ r.IsOn(99);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "IsOn allocated " + allocated + " bytes (sink " + sink + ")");
    }

    private static void Facade()
    {
        var messages = new List<string>();
        Tweaks.Warn = messages.Add;
        int handle = Tweaks.Register(Local("fix.facade", TweakCategory.Fix));
        Check(handle == 0 && !Tweaks.IsOn(handle) && !Tweaks.IsOn("fix.facade"), "the facade is off before initialization");
        Check(Tweaks.Register(Local("fix.facade")) == TweakRegistry.InvalidHandle && messages.Count == 1, "the facade logs through the injected logger");
        Check(Tweaks.Registry.Initialize(new MemoryStore()) && Tweaks.IsOn(handle), "the facade reflects the shared registry");
        Tweaks.Fault(handle, new Exception("facade"));
        Check(!Tweaks.IsOn(handle) && messages.Count == 2, "the facade forwards faults");
    }

    // FR-6: the Skip intro descriptor, its registration, and the GetAnyButton gate the patch applies.
    private static void SkipIntro()
    {
        TweakDescriptor d = FrameworkTweaks.SkipIntroDescriptor;
        Check(d.Id == "convenience.skip-intro" && d.Category == TweakCategory.Convenience && d.Scope == TweakScope.Local,
            "Skip intro is a Local Convenience tweak with the specified ID");
        Check(!d.DefaultOn && d.BalanceNote == null, "Skip intro is off by default and has no balance note");
        Check(d.Evidence.Contains("SplashScreen.GetAnyButton") && d.Evidence.Contains("SplashScreen.DisplayScene"),
            "the evidence names the verified methods");

        var store = new MemoryStore();
        TweakRegistry r = NewRegistry();
        FrameworkTweaks.RegisterAll(r);
        int handle = FrameworkTweaks.SkipIntro;
        Check(handle != TweakRegistry.InvalidHandle && Logs.Count == 0, "the framework descriptors register cleanly");
        Check(r.Get(handle) == d, "the handle resolves to the Skip intro descriptor");

        bool[] vanilla = { false, true };
        foreach (bool pressed in vanilla)
            Check(FrameworkTweaks.SkipIntroAnyButton(r, handle, pressed) == pressed, "before initialization the vanilla result passes through: " + pressed);

        Check(r.Initialize(store) && !r.IsOn(handle), "a fresh install leaves Skip intro off");
        foreach (bool pressed in vanilla)
            Check(FrameworkTweaks.SkipIntroAnyButton(r, handle, pressed) == pressed, "off: the vanilla result passes through: " + pressed);

        Check(r.Toggle(handle) && store.Values["convenience.skip-intro"] == TweakPreference.On, "turning it on stores On");
        foreach (bool pressed in vanilla)
            Check(FrameworkTweaks.SkipIntroAnyButton(r, handle, pressed), "on: every call reports a press: " + pressed);

        r.Fault(handle, new InvalidOperationException("splash"));
        foreach (bool pressed in vanilla)
            Check(FrameworkTweaks.SkipIntroAnyButton(r, handle, pressed) == pressed, "faulted: the vanilla result passes through: " + pressed);

        TweakRegistry restarted = NewRegistry();
        FrameworkTweaks.RegisterAll(restarted);
        Check(restarted.Initialize(store) && FrameworkTweaks.SkipIntroAnyButton(restarted, FrameworkTweaks.SkipIntro, false),
            "the stored On applies from the first call after a restart");
        Check(!FrameworkTweaks.SkipIntroAnyButton(restarted, TweakRegistry.InvalidHandle, false), "an unregistered handle leaves vanilla untouched");
    }

    private static List<string> Warnings = new List<string>(), Infos = new List<string>(), Errors = new List<string>();

    private static TweakSessionLifecycle NewLifecycle(TweakRegistry r, Func<int> probe)
    {
        Warnings = new List<string>();
        Infos = new List<string>();
        Errors = new List<string>();
        return new TweakSessionLifecycle(r, Warnings.Add, Infos.Add, Errors.Add, probe);
    }

    private static int Count(List<string> lines, string fragment)
    {
        int n = 0;
        foreach (string line in lines) if (line.Contains(fragment)) n++;
        return n;
    }

    // FR-3 (work item 2b): the pure decisions behind the lifecycle hooks.
    private static void LifecycleDecisions()
    {
        Check(TweakSessionLifecycle.ModeFromGame(0) == TweakSessionMode.SinglePlayer, "GameMode 0 is SinglePlayer");
        Check(TweakSessionLifecycle.ModeFromGame(1) == TweakSessionMode.Multiplayer, "GameMode 1 is Multiplayer");
        Check(TweakSessionLifecycle.ModeFromGame(2) == TweakSessionMode.LocalMultiplayer, "GameMode 2 is LocalMultiplayer");
        foreach (int unknown in new[] { -1, 3, 9, int.MaxValue, int.MinValue })
            Check(TweakSessionLifecycle.ModeFromGame(unknown) == TweakSessionMode.Multiplayer, "an unknown GameMode fails closed as Multiplayer: " + unknown);
        foreach (TweakSessionMode mode in new[] { TweakSessionMode.SinglePlayer, TweakSessionMode.Multiplayer, TweakSessionMode.LocalMultiplayer })
            Check(TweakSessionLifecycle.ModeFromGame((int)mode) == mode, "the mirror enum round-trips " + mode);

        var sp = TweakSessionMode.SinglePlayer;
        var mp = TweakSessionMode.Multiplayer;
        Check(!TweakSessionLifecycle.NeedsCaptureAtLock(TweakSessionState.Captured, sp, sp), "a capture for the run's mode is kept");
        Check(TweakSessionLifecycle.NeedsCaptureAtLock(TweakSessionState.Captured, sp, mp), "a capture for another mode is replaced");
        Check(TweakSessionLifecycle.NeedsCaptureAtLock(TweakSessionState.None, null, mp), "a client that never captured captures at run start");
        Check(TweakSessionLifecycle.NeedsCaptureAtLock(TweakSessionState.None, null, sp), "a capture lost to a clear is redone at run start");
        Check(TweakSessionLifecycle.NeedsCaptureAtLock(TweakSessionState.Locked, sp, sp), "a stale locked set is never reused");

        var triggers = new[] { TweakClearTrigger.SceneReload, TweakClearTrigger.RunEnd, TweakClearTrigger.LeftRoom,
            TweakClearTrigger.Disconnected, TweakClearTrigger.JoinRoomFailed };
        foreach (TweakClearTrigger trigger in triggers)
            Check(!TweakSessionLifecycle.WarnOnClear(false, trigger), "clearing an unstarted run never warns: " + trigger);
        Check(!TweakSessionLifecycle.WarnOnClear(true, TweakClearTrigger.SceneReload), "a scene reload ends the run: no warning");
        Check(!TweakSessionLifecycle.WarnOnClear(true, TweakClearTrigger.RunEnd), "the run-end fade ends the run: no warning");
        Check(TweakSessionLifecycle.WarnOnClear(true, TweakClearTrigger.LeftRoom), "leaving the room mid-run warns");
        Check(TweakSessionLifecycle.WarnOnClear(true, TweakClearTrigger.Disconnected), "a disconnect mid-run warns");
        Check(TweakSessionLifecycle.WarnOnClear(true, TweakClearTrigger.JoinRoomFailed), "a join failure mid-run warns");
    }

    // FR-3 (work item 2b): capture, lock and clear as the hooks drive them, without the probe.
    private static void LifecycleHooks()
    {
        var store = new MemoryStore();
        int on, off, local;
        TweakRegistry r = SessionRegistry(store, out on, out off, out local);
        TweakSessionLifecycle l = NewLifecycle(r, () => TweakRegistry.InvalidHandle);

        // Solo: capture at room creation, lock at run start, clear at the run-end fade, then the
        // Photon callbacks and the scene reload that follow find nothing to clear.
        Check(l.Capture(0, "GameLogic.CreateOnlineRoom") && r.IsOn(on), "solo capture applies preferences");
        Check(l.Lock(0, "uiStartGame.EnterFahrulRPC") && r.SessionState == TweakSessionState.Locked && r.IsOn(on), "solo lock keeps the set");
        Check(l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish") && !r.IsOn(on), "the run-end fade clears a locked run");
        Check(!l.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton"), "the disconnect after the fade has nothing to clear");
        Check(!l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton"), "the scene reload has nothing to clear");
        Check(Warnings.Count == 0 && Infos.Count == 0 && Errors.Count == 0, "a normal run logs nothing without the probe");

        // Local multiplayer in an offline room.
        Check(l.Capture(2, "GameLogic.CreateOfflineRoom") && r.IsOn(on) && r.SessionMode == TweakSessionMode.LocalMultiplayer, "local play applies preferences");
        Check(l.Lock(2, "uiStartGame.EnterFahrulRPC") && r.IsOn(on), "local play locks");
        Check(l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton") && Warnings.Count == 0, "a scene reload ends a locked run without a warning");

        // Online co-op host: captured when GameConfig creates the room; everything Session is off.
        Check(l.Capture(1, "StartGameFE.GameConfig.CreateOnlineRoom") && !r.IsOn(on) && r.SessionSource == TweakRegistry.PendingSource, "the co-op host captures Multiplayer");
        Check(l.Lock(1, "uiStartGame.EnterFahrulRPC") && !r.IsOn(on) && !r.IsOn(off) && r.IsOn(local), "the co-op host locks with Session off, Local unaffected");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");

        // Co-op client: no capture hook runs; the lock captures Multiplayer itself.
        Check(r.SessionState == TweakSessionState.None && l.Lock(1, "uiStartGame.EnterFahrulRPC"), "a client with no capture still locks");
        Check(!r.IsOn(on) && r.SessionMode == TweakSessionMode.Multiplayer && r.SessionState == TweakSessionState.Locked, "the client's Session tweaks are off");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // A solo setup abandoned without a clear, then joining co-op: the stale capture is replaced.
        l.Capture(0, "GameLogic.CreateOnlineRoom");
        Check(r.IsOn(on) && l.Lock(1, "uiStartGame.EnterFahrulRPC") && !r.IsOn(on) && r.SessionMode == TweakSessionMode.Multiplayer,
            "a capture for solo is not carried into a co-op run");
        Check(Warnings.Count == 0, "a mode mismatch before the lock is corrected silently");

        // A locked solo run that was never cleared, then a co-op run starts: one warning, then off.
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(r.IsOn(on) && l.Lock(1, "uiStartGame.EnterFahrulRPC") && !r.IsOn(on) && r.SessionState == TweakSessionState.Locked,
            "a stale locked solo set never reaches a co-op run");
        Check(Warnings.Count == 1 && Warnings[0].Contains("never cleared"), "the stale lock is warned about once");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // A disconnect callback between setup and start clears the capture; the lock redoes it.
        Warnings.Clear();
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        Check(!l.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton") && Warnings.Count == 0, "clearing a setup capture never warns");
        Check(l.Lock(0, "uiStartGame.EnterFahrulRPC") && r.IsOn(on) && r.SessionMode == TweakSessionMode.SinglePlayer, "the lock recaptures the solo set");

        // A Photon callback clearing a locked run that is still on screen warns once.
        Check(l.Clear(TweakClearTrigger.LeftRoom, "uiStartGame.OnLeftRoom") && !r.IsOn(on), "a mid-run clear still clears");
        Check(Warnings.Count == 1 && Warnings[0].Contains("uiStartGame.OnLeftRoom") && Warnings[0].Contains("may still be going"), "a mid-run clear logs one warning");
        Check(!l.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton") && Warnings.Count == 1, "the callbacks after it do not warn again");

        // Uninitialized registry: nothing captures, locks or logs.
        TweakRegistry cold = NewRegistry();
        int coldHandle = cold.Register(Session("session.cold"));
        TweakSessionLifecycle coldLife = NewLifecycle(cold, () => coldHandle);
        Check(!coldLife.Capture(0, "x") && !coldLife.Lock(0, "x") && !coldLife.Clear(TweakClearTrigger.LeftRoom, "x"), "an uninitialized registry ignores the hooks");
        Check(Warnings.Count == 0 && Infos.Count == 0 && Errors.Count == 0 && !cold.IsOn(coldHandle), "and logs nothing");

        Check(Tweaks.Session != null, "the facade exposes the lifecycle");
    }

    // FR-6: the self-test Session probe registers only for self-tests and traces each step.
    private static void SessionProbe()
    {
        TweakDescriptor d = FrameworkTweaks.SessionProbeDescriptor;
        Check(d.Id == "probe.session-lifecycle" && d.Scope == TweakScope.Session && d.Category == TweakCategory.Convenience,
            "the probe is a Session Convenience descriptor with the specified ID");
        Check(!d.DefaultOn && d.BalanceNote == null, "the probe is off by default");
        Check(d.Evidence.Contains("EnterFahrulRPC") && d.Evidence.Contains("InitializeSingleton"), "the probe names its hooks");

        TweakRegistry players = NewRegistry();
        FrameworkTweaks.RegisterAll(players);
        Check(FrameworkTweaks.SessionProbe == TweakRegistry.InvalidHandle && players.Count == 1, "without self-tests the probe is not registered");
        FrameworkTweaks.RegisterAll(NewRegistry(), false);
        Check(FrameworkTweaks.SessionProbe == TweakRegistry.InvalidHandle, "an explicit false leaves it out too");

        var store = new MemoryStore();
        TweakRegistry r = NewRegistry();
        FrameworkTweaks.RegisterAll(r, true);
        int probe = FrameworkTweaks.SessionProbe;
        Check(probe != TweakRegistry.InvalidHandle && r.Count == 2 && Logs.Count == 0, "self-tests register the probe cleanly");
        store.Values["probe.session-lifecycle"] = TweakPreference.On;
        r.Initialize(store);
        TweakSessionLifecycle l = NewLifecycle(r, () => FrameworkTweaks.SessionProbe);
        Check(!r.IsOn(probe), "the probe is off outside a run even when chosen");

        string trace = TweakSessionLifecycle.ProbeTrace;
        l.Capture(0, "GameLogic.CreateOnlineRoom");
        Check(Infos.Count == 1 && Infos[0].StartsWith(trace + " capture via=GameLogic.CreateOnlineRoom", StringComparison.Ordinal)
            && Infos[0].Contains("mode=SinglePlayer") && Infos[0].Contains("source=preferences") && Infos[0].Contains("probe=on"),
            "capture is traced with mode, source and the probe value: " + (Infos.Count > 0 ? Infos[0] : "<none>"));
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(Count(Infos, trace + " lock via=uiStartGame.EnterFahrulRPC") == 1, "lock is traced");
        Check(Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=SinglePlayer source=preferences probe=on expected=on") == 1,
            "solo lock passes with the probe on");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
        Check(Count(Infos, trace + " clear via=GameLogic.RestartFadeOutFinish locked=true") == 1, "clear is traced");
        Check(Count(Infos, "SELF-TEST PASS [session-lifecycle]: clear via GameLogic.RestartFadeOutFinish") == 1 && Errors.Count == 0,
            "the run-end clear passes");
        int before = Infos.Count;
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");
        Check(Infos.Count == before, "a clear with nothing to clear is not traced");

        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(Count(Infos, "capture via=GameLogic.CreateOfflineRoom") == 1 && Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=SinglePlayer") == 2,
            "solo offline traces and passes");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        l.Capture(2, "GameLogic.CreateOnlineRoom");
        l.Lock(2, "uiStartGame.EnterFahrulRPC");
        Check(Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=LocalMultiplayer source=preferences probe=on expected=on") == 1,
            "local multiplayer passes with the probe on");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        l.Capture(1, "StartGameFE.GameConfig.CreateOnlineRoom");
        string last = Infos[Infos.Count - 1];
        Check(last.Contains("mode=Multiplayer") && last.Contains("source=pending") && last.Contains("probe=off"),
            "the co-op capture shows the probe resolving off");
        l.Lock(1, "uiStartGame.EnterFahrulRPC");
        Check(Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=Multiplayer source=pending probe=off expected=off") == 1,
            "online co-op passes with the probe off");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        l.Lock(1, "uiStartGame.EnterFahrulRPC");
        Check(Count(Infos, "capture via=uiStartGame.EnterFahrulRPC (at run start)") == 1
            && Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=Multiplayer source=pending probe=off expected=off") == 2,
            "a co-op client traces its run-start capture and passes off");
        Check(Errors.Count == 0 && Warnings.Count == 0, "the four modes log no failure or warning");

        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");
        l.Capture(0, "GameLogic.CreateOnlineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        l.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton");
        Check(Warnings.Count == 1 && Errors.Count == 1
            && Errors[0].StartsWith("SELF-TEST FAIL [session-lifecycle]: clear via uiStartGame.OnDisconnectedFromPhoton", StringComparison.Ordinal),
            "a mid-run clear is a probe failure as well as a warning");

        Errors.Clear();
        r.Fault(probe, new InvalidOperationException("probe"));
        l.Capture(0, "GameLogic.CreateOnlineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(!r.IsOn(probe) && Errors.Count == 0 && Count(Infos, "lock mode=SinglePlayer source=preferences probe=off expected=off") == 1,
            "a faulted probe is left out of the next run and the check expects that");
    }
}
