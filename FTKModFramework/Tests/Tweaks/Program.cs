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
        Console.WriteLine("Tweaks: " + _checks + " checks passed (registry, preferences, session lifecycle, faults).");
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
}
