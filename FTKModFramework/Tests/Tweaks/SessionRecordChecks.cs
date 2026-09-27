// Game-free checks for the save-bound Session record (Spec #253 work items 1 and 2): the codec,
// per-ID resolution, the resume state inside TweakSessionLifecycle, the probe check per source,
// the save hook's write decision, the state dictionary entry points and the IL match rule. The
// Harmony hooks themselves need the game and are live-gated in work item 3.
using System;
using System.Collections.Generic;
using System.Text;
using FTKModFramework.Core;

internal static class SessionRecordChecks
{
    private static int _checks;
    private static readonly List<string> Logs = new List<string>();
    private static List<string> Warnings = new List<string>(), Infos = new List<string>(), Errors = new List<string>();

    private const string Via = "test";
    private const string ResumeVia = "uiStartGame.OnResumeGame";
    private const string ReadVia = "GameFlow.StateDataDeserialize";

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("session record: " + message);
    }

    internal static int Run()
    {
        Codec();
        Rejections();
        Resolution();
        ResumeSequence();
        IgnoredReads();
        Disarming();
        ResumeSources();
        ProbeBySource();
        WriteDecision();
        StateHooks();
        IlMatch();
        return _checks;
    }

    private static TweakDescriptor Descriptor(string id, TweakScope scope, bool? defaultOn = null)
    {
        return new TweakDescriptor(id, TweakCategory.Convenience, scope, "Title " + id, "Summary", "Evidence", null, defaultOn);
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

    /// <summary>session.on prefers On, session.off follows its Off default, local.on is a Local
    /// tweak preferring On. Extra Session IDs, if any, prefer On.</summary>
    private static TweakRegistry Registry(params string[] extraSessionOn)
    {
        Logs.Clear();
        var r = new TweakRegistry(Logs.Add);
        var store = new Store();
        r.Register(Descriptor("session.on", TweakScope.Session));
        r.Register(Descriptor("session.off", TweakScope.Session, false));
        r.Register(Descriptor("local.on", TweakScope.Local));
        store.Values["session.on"] = TweakPreference.On;
        store.Values["local.on"] = TweakPreference.On;
        foreach (string id in extraSessionOn)
        {
            r.Register(Descriptor(id, TweakScope.Session));
            store.Values[id] = TweakPreference.On;
        }
        r.Initialize(store);
        return r;
    }

    private static TweakSessionLifecycle Lifecycle(TweakRegistry r, Func<int> probe)
    {
        Warnings = new List<string>();
        Infos = new List<string>();
        Errors = new List<string>();
        return new TweakSessionLifecycle(r, Warnings.Add, Infos.Add, Errors.Add, probe);
    }

    private static TweakSessionLifecycle Lifecycle(TweakRegistry r)
    {
        return Lifecycle(r, () => TweakRegistry.InvalidHandle);
    }

    private static int Count(List<string> lines, string fragment)
    {
        int n = 0;
        foreach (string line in lines) if (line.Contains(fragment)) n++;
        return n;
    }

    private static int SessionCount(TweakRegistry r)
    {
        int n = 0;
        for (int h = 0; h < r.Count; h++) if (r.Get(h).Scope == TweakScope.Session) n++;
        return n;
    }

    // FR-1: every registered Session ID, sorted, with its effective state; Local tweaks never appear.
    private static void Codec()
    {
        TweakRegistry r = Registry();
        r.Capture(TweakSessionMode.SinglePlayer);
        r.Lock();
        string value = TweakSessionRecord.Encode(r);
        Check(value == "v1:-session.off,+session.on", "the record is sorted by ID with each state: " + value);
        Check(!value.Contains("local.on"), "Local tweaks are never recorded");

        TweakSessionRecord decoded = TweakSessionRecord.Decode(value);
        Check(decoded.Status == TweakSessionRecordStatus.Valid && decoded.Problem == null, "the encoded record decodes as valid");
        Check(decoded.Count == SessionCount(r), "the record lists every registered Session tweak");
        for (int h = 0; h < r.Count; h++)
        {
            TweakDescriptor d = r.Get(h);
            bool on;
            bool listed = decoded.TryGetState(d.Id, out on);
            if (d.Scope == TweakScope.Session) Check(listed && on == r.IsOn(h), "round trip keeps " + d.Id);
            else Check(!listed, "round trip leaves out " + d.Id);
        }

        // Sorting is ordinal and independent of registration order.
        TweakRegistry shuffled = Registry("a.first", "z.last", "session.mid");
        shuffled.Capture(TweakSessionMode.SinglePlayer);
        string sorted = TweakSessionRecord.Encode(shuffled);
        List<string> ids = TweakSessionRecord.Decode(sorted).Ids();
        var body = new StringBuilder(TweakSessionRecord.VersionPrefix);
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0) body.Append(',');
            body.Append(shuffled.IsOn(ids[i]) ? '+' : '-').Append(ids[i]);
        }
        Check(sorted == body.ToString() && sorted.StartsWith("v1:+a.first,", StringComparison.Ordinal) && sorted.EndsWith(",+z.last", StringComparison.Ordinal),
            "entries are in ordinal ID order: " + sorted);

        Check(TweakSessionRecord.Decode(null) == TweakSessionRecord.Absent && TweakSessionRecord.Absent.Status == TweakSessionRecordStatus.Absent,
            "a missing key is absent, not invalid");
        TweakSessionRecord unsorted = TweakSessionRecord.Decode("v1:+b.b,-a.a");
        bool state;
        Check(unsorted.Status == TweakSessionRecordStatus.Valid && unsorted.TryGetState("b.b", out state) && state
            && unsorted.TryGetState("a.a", out state) && !state, "decode does not require sorted input");
        string limit = "v1:+" + new string('a', TweakSessionRecord.MaxLength - 4);
        Check(limit.Length == TweakSessionRecord.MaxLength && TweakSessionRecord.Decode(limit).Status == TweakSessionRecordStatus.Valid,
            "a record of exactly the limit is accepted");
    }

    // FR-1: every bounded-parse rejection is invalid, never absent, and never partly applied.
    private static void Rejections()
    {
        string oversized = "v1:+" + new string('a', TweakSessionRecord.MaxLength - 3);
        var cases = new[]
        {
            new[] { oversized, "longer than" },
            new[] { "", "malformed" },
            new[] { "+a.b", "malformed" },
            new[] { "V1:+a.b", "malformed" },
            new[] { "v:+a.b", "malformed" },
            new[] { "v1+a.b", "malformed" },
            new[] { "v2:+a.b", "unknown version" },
            new[] { "v10:+a.b", "unknown version" },
            new[] { "v0:", "unknown version" },
            new[] { "v1:", "no entries" },
            new[] { "v1:a.b", "malformed entry 1" },
            new[] { "v1:+", "malformed entry 1" },
            new[] { "v1:*a.b", "malformed entry 1" },
            new[] { "v1: +a.b", "malformed entry 1" },
            new[] { "v1:+a.b,", "malformed entry 2" },
            new[] { "v1:,+a.b", "malformed entry 1" },
            new[] { "v1:+a.b,,-c.d", "malformed entry 2" },
            new[] { "v1:+a.b;-c.d", "invalid ID in entry 1" },
            new[] { "v1:+A.b", "invalid ID in entry 1" },
            new[] { "v1:+a..b", "invalid ID in entry 1" },
            new[] { "v1:+a.b,-c d", "invalid ID in entry 2" },
            new[] { "v1:+a.b,++c.d", "invalid ID in entry 2" },
            new[] { "v1:+a_b", "invalid ID in entry 1" },
            new[] { "v1:+a.b,-a.b", "duplicate ID 'a.b'" },
            new[] { "v1:+a.b,+c.d,+a.b", "duplicate ID 'a.b'" },
        };
        foreach (string[] c in cases)
        {
            TweakSessionRecord record = TweakSessionRecord.Decode(c[0]);
            string shown = c[0].Length > 40 ? c[0].Substring(0, 40) + "..." : c[0];
            Check(record.Status == TweakSessionRecordStatus.Invalid, "rejected as invalid: '" + shown + "'");
            Check(record.Problem != null && record.Problem.Contains(c[1]), "'" + shown + "' names its problem: " + record.Problem);
            Check(record.Count == 0, "an invalid record carries no entries: '" + shown + "'");
        }
    }

    // FR-1 and FR-4: per-ID fallback, ignored IDs and the source label, resolved without the lifecycle.
    private static void Resolution()
    {
        TweakRegistry r = Registry("session.new");
        int on, off, added, local;
        r.TryGetHandle("session.on", out on);
        r.TryGetHandle("session.off", out off);
        r.TryGetHandle("session.new", out added);
        r.TryGetHandle("local.on", out local);

        // A record saved before session.new existed, listing a retired ID and a Local ID.
        TweakSessionRecord record = TweakSessionRecord.Decode("v1:+local.on,+retired.id,+session.off,-session.on");
        TweakSessionResolution res = TweakSessionRecord.Resolve(r, record);
        Check(res.Source == TweakRegistry.SaveSource, "a valid record resolves as save even with fallbacks");
        Check(res.OnIds.Contains("session.off") && !res.OnIds.Contains("session.on"), "listed IDs take the record's state");
        Check(res.OnIds.Contains("session.new"), "a newly registered ID missing from the record follows the preference");
        Check(!res.OnIds.Contains("local.on") && !res.OnIds.Contains("retired.id"), "Local and unknown IDs are never turned on");
        Check(res.FromRecord == 2 && res.FromPreferences == SessionCount(r) - 2, "two from the record, the rest from preferences");
        Check(res.UnknownIds.Count == 1 && res.UnknownIds[0] == "retired.id", "the unknown ID is reported for logging");
        Check(res.LocalIds.Count == 1 && res.LocalIds[0] == "local.on", "the Local ID is reported for logging");

        // Fallback matches a SinglePlayer capture, faults included; a recorded state ignores faults.
        r.Fault(added, new InvalidOperationException("x"));
        r.Fault(off, new InvalidOperationException("x"));
        res = TweakSessionRecord.Resolve(r, record);
        Check(!res.OnIds.Contains("session.new"), "a faulted fallback ID is left out like a capture");
        Check(res.OnIds.Contains("session.off"), "a recorded On keeps the run's rules even after a local fault");

        TweakRegistry fresh = Registry("session.new");
        fresh.Capture(TweakSessionMode.SinglePlayer);
        foreach (TweakSessionRecord noRecord in new[] { TweakSessionRecord.Absent, null, TweakSessionRecord.Decode("v9:+x") })
        {
            TweakSessionResolution fallback = TweakSessionRecord.Resolve(fresh, noRecord);
            string expected = noRecord != null && noRecord.Status == TweakSessionRecordStatus.Invalid
                ? TweakRegistry.PreferencesInvalidSource : TweakRegistry.PreferencesLegacySource;
            Check(fallback.Source == expected, "no usable record resolves as " + expected);
            Check(fallback.FromRecord == 0 && fallback.FromPreferences == SessionCount(fresh)
                && fallback.UnknownIds.Count == 0 && fallback.LocalIds.Count == 0, "every ID falls back and nothing is ignored");
            for (int h = 0; h < fresh.Count; h++)
                if (fresh.Get(h).Scope == TweakScope.Session)
                    Check(fallback.OnIds.Contains(fresh.Get(h).Id) == fresh.IsOn(h), "the fallback equals a SinglePlayer capture for " + fresh.Get(h).Id);
        }

        Check(TweakSessionRecord.SourceFor(TweakSessionRecordStatus.Valid) == "save"
            && TweakSessionRecord.SourceFor(TweakSessionRecordStatus.Absent) == "preferences-legacy"
            && TweakSessionRecord.SourceFor(TweakSessionRecordStatus.Invalid) == "preferences-invalid", "the three source labels");
        foreach (string label in new[] { TweakRegistry.SaveSource, TweakRegistry.PreferencesLegacySource, TweakRegistry.PreferencesInvalidSource })
            Check(label != TweakRegistry.PreferencesSource && label != TweakRegistry.PendingSource, "the resume labels are distinct: " + label);

        var cold = new TweakRegistry(null);
        cold.Register(Descriptor("session.cold", TweakScope.Session));
        Check(TweakSessionRecord.Resolve(cold, record).OnIds.Count == 0, "an uninitialized registry resolves to nothing");
    }

    // FR-3: arm, capture, read and apply, a Photon clear, then the lock reapplies and disarms.
    private static void ResumeSequence()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        const string saved = "v1:+session.off,-session.on";

        Check(!l.ResumeArmed && l.ResumeRecord == null, "nothing is armed at first");
        l.ArmResume(ResumeVia);
        Check(l.ResumeArmed, "the resume trigger arms");
        Check(l.Capture(0, "GameLogic.CreateOfflineRoom") && r.IsOn("session.on") && r.SessionSource == TweakRegistry.PreferencesSource,
            "the resume's capture starts from preferences");
        Check(l.ReadRecord(saved, ReadVia), "an armed, unlocked read is accepted");
        Check(!r.IsOn("session.on") && r.IsOn("session.off") && r.IsOn("local.on") && r.SessionSource == TweakRegistry.SaveSource,
            "the record applies at once and leaves Local tweaks alone");
        Check(l.ResumeRecord != null && l.ResumeRecord.Status == TweakSessionRecordStatus.Valid, "the record is kept for the lock");
        Check(Count(Infos, "comes from save via " + ReadVia + " (2 from the save, 0 from preferences)") == 1, "the source is logged: no silent drift");

        // The asynchronous disconnect in solo offline clears the capture before the lock.
        Check(!l.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton") && r.SessionState == TweakSessionState.None,
            "the Photon clear empties the captured set");
        Check(l.ResumeArmed && l.ResumeRecord != null, "a Photon clear never disarms, so the record survives");
        Check(l.Lock(0, "uiStartGame.EnterFahrulRPC") && r.SessionState == TweakSessionState.Locked, "the run locks");
        Check(!r.IsOn("session.on") && r.IsOn("session.off") && r.SessionSource == TweakRegistry.SaveSource,
            "the lock's recapture reapplies the record");
        Check(!l.ResumeArmed && l.ResumeRecord == null, "the lock disarms");
        Check(Warnings.Count == 0 && Errors.Count == 0, "a resume with a Photon clear warns about nothing");
        Check(l.RecordToWrite() == saved, "the next save writes the restored set");

        // A preference change during the resumed run changes neither the run nor its record.
        int on;
        r.TryGetHandle("session.on", out on);
        r.Toggle(on);
        Check(!r.IsOn(on) && l.RecordToWrite() == saved, "the record reflects the run, not the preferences");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");

        // A record read before any capture is kept and applied by the next capture.
        r = Registry();
        l = Lifecycle(r);
        l.ArmResume(ResumeVia);
        Check(l.ReadRecord(saved, ReadVia) && r.SessionState == TweakSessionState.None && !r.IsOn("session.off"),
            "a read with no capture is stored, not applied");
        Check(l.Capture(0, "GameLogic.CreateOnlineRoom") && r.IsOn("session.off") && r.SessionSource == TweakRegistry.SaveSource,
            "the capture applies the stored record");
        Check(l.Capture(0, "GameLogic.CreateOfflineRoom") && r.IsOn("session.off") && r.SessionSource == TweakRegistry.SaveSource,
            "a second capture before the lock keeps the record");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(r.IsOn("session.off") && !l.ResumeArmed, "the lock keeps it and disarms");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // Local play resumes the same way.
        l.ArmResume(ResumeVia);
        l.Capture(2, "GameLogic.CreateOfflineRoom");
        l.ReadRecord(saved, ReadVia);
        l.Lock(2, "uiStartGame.EnterFahrulRPC");
        Check(r.IsOn("session.off") && r.SessionMode == TweakSessionMode.LocalMultiplayer && r.SessionSource == TweakRegistry.SaveSource,
            "a local-play resume restores the record");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // Online co-op keeps its pending set; publishing a host's record is Spec B.
        l.ArmResume(ResumeVia);
        l.Capture(1, "StartGameFE.GameConfig.CreateOnlineRoom");
        Check(l.ReadRecord(saved, ReadVia) && !r.IsOn("session.off") && r.SessionSource == TweakRegistry.PendingSource,
            "a co-op host never applies its save alone");
        l.Lock(1, "uiStartGame.EnterFahrulRPC");
        Check(!r.IsOn("session.off") && r.SessionSource == TweakRegistry.PendingSource && !l.ResumeArmed, "and locks pending");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // A new game without a resume is unaffected.
        Infos.Clear();
        Check(l.Capture(0, "GameLogic.CreateOfflineRoom") && l.Lock(0, "uiStartGame.EnterFahrulRPC"), "a new game captures and locks");
        Check(r.SessionSource == TweakRegistry.PreferencesSource && !l.ResumeArmed && Infos.Count == 0, "a new game uses preferences and logs no resume");
    }

    // FR-3: the deserializer is also a PunRPC, so reads while unarmed or locked must do nothing.
    private static void IgnoredReads()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        Check(!l.ReadRecord("v1:+session.off,-session.on", ReadVia), "a read while not armed is ignored");
        Check(r.IsOn("session.on") && !r.IsOn("session.off") && r.SessionSource == TweakRegistry.PreferencesSource && l.ResumeRecord == null,
            "the ignored read changes nothing");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");

        l.ArmResume(ResumeVia);
        Check(!l.ReadRecord("v1:+session.off,-session.on", ReadVia), "a read after the lock is ignored");
        Check(r.IsOn("session.on") && !r.IsOn("session.off") && r.SessionSource == TweakRegistry.PreferencesSource, "the locked run keeps its rules");
        Check(!l.ReadRecord("garbage", ReadVia) && Warnings.Count == 0, "an ignored invalid read does not warn");
        Check(Infos.Count == 0 && Errors.Count == 0, "ignored reads log nothing");
    }

    // FR-3: which clear triggers disarm. Scene reload, run end and a title activation mean no run.
    private static void Disarming()
    {
        foreach (TweakClearTrigger trigger in new[] { TweakClearTrigger.SceneReload, TweakClearTrigger.RunEnd, TweakClearTrigger.TitleActivation })
        {
            Check(TweakSessionLifecycle.DisarmsResume(trigger), trigger + " disarms");
            TweakRegistry r = Registry();
            TweakSessionLifecycle l = Lifecycle(r);
            l.ArmResume(ResumeVia);
            Check(!l.Clear(trigger, Via) && !l.ResumeArmed, trigger + " disarms even with nothing to clear");
            Check(!l.ReadRecord("v1:+session.off", ReadVia), "a read after " + trigger + " is ignored");
            l.ArmResume(ResumeVia);
            l.Capture(0, Via);
            l.ReadRecord("v1:+session.off", ReadVia);
            l.Clear(trigger, Via);
            Check(!l.ResumeArmed && l.ResumeRecord == null, trigger + " drops a stored record");
            l.Lock(0, Via);
            Check(!r.IsOn("session.off") && r.SessionSource == TweakRegistry.PreferencesSource, "the next run after " + trigger + " uses preferences");
        }
        foreach (TweakClearTrigger trigger in new[] { TweakClearTrigger.LeftRoom, TweakClearTrigger.Disconnected, TweakClearTrigger.JoinRoomFailed })
        {
            Check(!TweakSessionLifecycle.DisarmsResume(trigger), trigger + " never disarms");
            TweakRegistry r = Registry();
            TweakSessionLifecycle l = Lifecycle(r);
            l.ArmResume(ResumeVia);
            l.Capture(0, Via);
            l.ReadRecord("v1:+session.off", ReadVia);
            l.Clear(trigger, Via);
            Check(l.ResumeArmed && l.ResumeRecord != null, trigger + " keeps the armed record");
            l.Lock(0, Via);
            Check(r.IsOn("session.off") && r.SessionSource == TweakRegistry.SaveSource, "the lock after " + trigger + " restores the record");
        }

        // A lock with an uninitialized registry still disarms.
        var cold = new TweakRegistry(null);
        cold.Register(Descriptor("session.cold", TweakScope.Session));
        TweakSessionLifecycle coldLife = Lifecycle(cold);
        coldLife.ArmResume(ResumeVia);
        Check(!coldLife.ReadRecord("v1:+session.cold", ReadVia) && !coldLife.Lock(0, Via) && !coldLife.ResumeArmed,
            "an uninitialized registry ignores the read and the lock disarms");
    }

    // FR-1 and FR-4 through the lifecycle: legacy, invalid, and ignored IDs logged once each.
    private static void ResumeSources()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        l.ArmResume(ResumeVia);
        l.Capture(0, Via);
        Check(l.ReadRecord(null, ReadVia) && r.SessionSource == TweakRegistry.PreferencesLegacySource, "no record resumes as preferences-legacy");
        Check(r.IsOn("session.on") && !r.IsOn("session.off") && Warnings.Count == 0, "a legacy resume follows preferences without a warning");
        l.Lock(0, Via);
        Check(r.SessionSource == TweakRegistry.PreferencesLegacySource && l.RecordToWrite() == "v1:-session.off,+session.on",
            "the next save of a legacy resume writes a full record");
        l.Clear(TweakClearTrigger.SceneReload, Via);

        foreach (string bad in new[] { "v2:+session.off", "v1:+session.off,+session.off", "v1:+Session.off", "nonsense" })
        {
            l.ArmResume(ResumeVia);
            l.Capture(0, Via);
            Warnings.Clear();
            Check(l.ReadRecord(bad, ReadVia) && r.SessionSource == TweakRegistry.PreferencesInvalidSource, "'" + bad + "' resumes as preferences-invalid");
            Check(!r.IsOn("session.off") && r.IsOn("session.on"), "'" + bad + "' is not partly applied");
            Check(Warnings.Count == 1 && Warnings[0].Contains("unreadable"), "'" + bad + "' warns once");
            l.Lock(0, Via);
            Check(Warnings.Count == 1 && r.SessionSource == TweakRegistry.PreferencesInvalidSource, "the lock keeps the label without a second warning");
            l.Clear(TweakClearTrigger.SceneReload, Via);
        }

        // Ignored IDs log one line each, even when a Photon clear makes the lock reapply the record.
        Infos.Clear();
        l.ArmResume(ResumeVia);
        l.Capture(0, Via);
        l.ReadRecord("v1:+future.tweak,+local.on,+session.off", ReadVia);
        l.Clear(TweakClearTrigger.Disconnected, Via);
        l.Lock(0, Via);
        Check(Count(Infos, "'future.tweak', which this build does not register") == 1, "an unknown ID is logged once");
        Check(Count(Infos, "'local.on', which is a Local tweak") == 1, "a Local ID is logged once");
        Check(Count(Infos, "comes from save") == 2, "each application logs its source");
        Check(r.IsOn("session.off") && r.IsOn("session.on") && r.IsOn("local.on") && r.SessionSource == TweakRegistry.SaveSource,
            "known IDs apply and the missing one falls back to its preference");
        Check(l.RecordToWrite() == "v1:+session.off,+session.on", "unknown IDs are not carried forward");
    }

    // FR-3: the self-test probe check runs for every resume source and compares save to the record.
    private static void ProbeBySource()
    {
        foreach (bool preferOn in new[] { false, true })
        {
            TweakRegistry r = ProbeRegistry(preferOn);
            int probe = FrameworkTweaks.SessionProbe;
            string id = r.Get(probe).Id;
            TweakSessionLifecycle l = Lifecycle(r, () => FrameworkTweaks.SessionProbe);
            string pref = preferOn ? "on" : "off", saved = preferOn ? "off" : "on";

            ResumeWith(l, "v1:" + (preferOn ? "-" : "+") + id);
            Check(Count(Infos, "SELF-TEST PASS [session-lifecycle]: lock mode=SinglePlayer source=save probe=" + saved + " expected=" + saved) == 1,
                "save: the probe follows the record against a preference of " + pref);
            Check(Count(Infos, "SESSION-PROBE [session-lifecycle] resume-arm via=" + ResumeVia) == 1
                && Count(Infos, "SESSION-PROBE [session-lifecycle] record via=" + ReadVia) == 1, "the arm and the read are traced");

            ResumeWith(l, "v1:+other.id");
            Check(Count(Infos, "lock mode=SinglePlayer source=save probe=" + pref + " expected=" + pref) == 1,
                "save without the probe listed: the check expects the preference");

            ResumeWith(l, null);
            Check(Count(Infos, "lock mode=SinglePlayer source=preferences-legacy probe=" + pref + " expected=" + pref) == 1, "preferences-legacy is checked");

            ResumeWith(l, "v1:+" + id + ",+" + id);
            Check(Count(Infos, "lock mode=SinglePlayer source=preferences-invalid probe=" + pref + " expected=" + pref) == 1, "preferences-invalid is checked");
            Check(Errors.Count == 0, "every resume source passes with " + pref);
        }

        // The check detects a set that disagrees with the record.
        TweakRegistry tampered = ProbeRegistry(false);
        TweakSessionLifecycle t = Lifecycle(tampered, () => FrameworkTweaks.SessionProbe);
        t.ArmResume(ResumeVia);
        t.Capture(0, Via);
        t.ReadRecord("v1:+" + tampered.Get(FrameworkTweaks.SessionProbe).Id, ReadVia);
        tampered.SetSessionSet(new string[0], TweakRegistry.SaveSource);
        t.Lock(0, Via);
        Check(Errors.Count == 1 && Errors[0].StartsWith("SELF-TEST FAIL [session-lifecycle]: lock mode=SinglePlayer source=save probe=off expected=on", StringComparison.Ordinal),
            "a save-sourced set that differs from the record fails");

        // A save label with no armed record has nothing to compare against, so it is not checked.
        t.Clear(TweakClearTrigger.SceneReload, Via);
        Infos.Clear();
        t.Capture(0, Via);
        tampered.SetSessionSet(new string[0], TweakRegistry.SaveSource);
        t.Lock(0, Via);
        Check(Count(Infos, "SELF-TEST") == 0 && Errors.Count == 1, "a save label without a record is traced but not checked");
    }

    private static TweakRegistry ProbeRegistry(bool preferOn)
    {
        Logs.Clear();
        var r = new TweakRegistry(Logs.Add);
        FrameworkTweaks.RegisterAll(r, true);
        var store = new Store();
        store.Values[FrameworkTweaks.SessionProbeDescriptor.Id] = preferOn ? TweakPreference.On : TweakPreference.Off;
        r.Initialize(store);
        return r;
    }

    private static void ResumeWith(TweakSessionLifecycle l, string record)
    {
        l.ArmResume(ResumeVia);
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.ReadRecord(record, ReadVia);
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
    }

    // FR-2: the save hook writes only for a locked run with Session tweaks, so otherwise the save
    // is byte-identical to vanilla.
    private static void WriteDecision()
    {
        var cold = new TweakRegistry(null);
        cold.Register(Descriptor("session.cold", TweakScope.Session));
        Check(TweakSessionRecord.ValueToWrite(cold) == null && TweakSessionRecord.ValueToWrite(null) == null, "no value before initialization");

        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        Check(l.RecordToWrite() == null, "no value at the title screen");
        l.Capture(0, Via);
        Check(l.RecordToWrite() == null, "no value during setup, before the lock");
        l.Lock(0, Via);
        string value = l.RecordToWrite();
        Check(value == TweakSessionRecord.ValueToWrite(r) && value == "v1:-session.off,+session.on", "a locked run writes its set: " + value);
        l.Clear(TweakClearTrigger.RunEnd, Via);
        Check(l.RecordToWrite() == null, "no value after the run ends");

        l.Capture(1, Via);
        l.Lock(1, Via);
        Check(l.RecordToWrite() == "v1:-session.off,-session.on", "a locked co-op run records its all-off set");
        l.Clear(TweakClearTrigger.RunEnd, Via);

        Logs.Clear();
        var localOnly = new TweakRegistry(Logs.Add);
        localOnly.Register(Descriptor("local.only", TweakScope.Local));
        localOnly.Initialize(new Store());
        TweakSessionLifecycle localLife = Lifecycle(localOnly);
        localLife.Capture(0, Via);
        localLife.Lock(0, Via);
        Check(localLife.RecordToWrite() == null && Warnings.Count == 0, "no value when no Session tweak is registered");

        // A record that cannot fit is not written, and that is warned about once.
        var many = new List<string>();
        for (int i = 0; many.Count * 60 < TweakSessionRecord.MaxLength + 60; i++) many.Add("long." + i.ToString("D3") + "." + new string('x', 50));
        TweakRegistry big = Registry(many.ToArray());
        TweakSessionLifecycle bigLife = Lifecycle(big);
        bigLife.Capture(0, Via);
        Check(bigLife.RecordToWrite() == null && Warnings.Count == 0 && !TweakSessionRecord.Oversized(big), "setup never warns about size");
        bigLife.Lock(0, Via);
        Check(TweakSessionRecord.Oversized(big) && TweakSessionRecord.Encode(big) == null, "an oversized record is not encoded");
        Check(bigLife.RecordToWrite() == null && bigLife.RecordToWrite() == null, "an oversized record is not written");
        Check(Warnings.Count == 1 && Warnings[0].Contains("exceeds " + TweakSessionRecord.MaxLength), "the size is warned about once");
    }

    private static Dictionary<string, object> VanillaState()
    {
        return new Dictionary<string, object> { { "m_RoundCount", 7 }, { "m_Rules", new object() }, { "m_FindHerbRoundCoolDown", false } };
    }

    private static bool SameState(Dictionary<string, object> a, Dictionary<string, object> b)
    {
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, object> entry in a)
        {
            object other;
            if (!b.TryGetValue(entry.Key, out other) || !ReferenceEquals(entry.Value, other) && !Equals(entry.Value, other)) return false;
        }
        return true;
    }

    // Work item 2: the dictionary entry points the save transpiler and the load prefix call. The
    // GameFlow type check, the FullSerializer parse and the IL itself need the game and are live-gated.
    private static void StateHooks()
    {
        Check(TweakSessionRecord.FromStateValue(null) == TweakSessionRecord.Absent, "a missing or null value is absent");
        Check(TweakSessionRecord.FromStateValue("v1:+session.on").Status == TweakSessionRecordStatus.Valid, "a string value is decoded");
        TweakSessionRecord boxed = TweakSessionRecord.FromStateValue(1);
        Check(boxed.Status == TweakSessionRecordStatus.Invalid && boxed.Problem == "not a string", "a non-string value is invalid, not guessed at");

        // FR-2: with no locked run the dictionary is untouched, so FullSerializer writes vanilla's bytes.
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        Dictionary<string, object> state = VanillaState(), pristine = VanillaState();
        pristine["m_Rules"] = state["m_Rules"];
        Check(!l.WriteRecord(state) && SameState(state, pristine), "no record at the title screen");
        l.Capture(0, Via);
        Check(!l.WriteRecord(state) && SameState(state, pristine), "no record during setup");
        Check(!l.WriteRecord(null), "a null dictionary is ignored");
        l.Lock(0, Via);
        Check(l.WriteRecord(state) && state.Count == pristine.Count + 1 && (string)state[TweakSessionRecord.Key] == "v1:-session.off,+session.on",
            "a locked run adds exactly the record");
        state.Remove(TweakSessionRecord.Key);
        Check(SameState(state, pristine), "the record is the only change to the state");
        Check(l.WriteRecord(state) && l.WriteRecord(state) && state.Count == pristine.Count + 1, "a repeated write replaces the key");
        Dictionary<string, object> saved = state;
        l.Clear(TweakClearTrigger.RunEnd, Via);
        Dictionary<string, object> after = VanillaState();
        Check(!l.WriteRecord(after) && !after.ContainsKey(TweakSessionRecord.Key), "no record after the run ends");

        // FR-3: the load reads only while armed and unlocked, and parses nothing otherwise.
        Check(!l.WantsRecord && !l.ReadState(saved, ReadVia) && l.ResumeRecord == null, "an unarmed load, such as the PunRPC path, reads nothing");
        int on;
        r.TryGetHandle("session.on", out on);
        r.Toggle(on);
        l.ArmResume(ResumeVia);
        Check(l.WantsRecord, "an armed resume wants the record");
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        Check(!r.IsOn("session.on") && r.SessionSource == TweakRegistry.PreferencesSource, "the changed preference reaches the capture");
        Check(l.ReadState(saved, ReadVia) && r.IsOn("session.on") && r.SessionSource == TweakRegistry.SaveSource,
            "the saved state restores the run's set over the changed preference");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(!l.WantsRecord && !l.ReadState(saved, ReadVia), "a locked run reads nothing");
        l.Clear(TweakClearTrigger.RunEnd, Via);

        l.ArmResume(ResumeVia);
        l.Capture(0, Via);
        Check(l.ReadState(VanillaState(), ReadVia) && r.SessionSource == TweakRegistry.PreferencesLegacySource && !r.IsOn("session.on"),
            "a state without the key is legacy and follows current preferences");
        l.Lock(0, Via);
        Dictionary<string, object> next = VanillaState();
        Check(l.WriteRecord(next) && (string)next[TweakSessionRecord.Key] == "v1:-session.off,-session.on", "the next save of a legacy run writes a full record");
        l.Clear(TweakClearTrigger.RunEnd, Via);

        l.ArmResume(ResumeVia);
        l.Capture(0, Via);
        Dictionary<string, object> foreign = VanillaState();
        foreign[TweakSessionRecord.Key] = 42;
        int warnings = Warnings.Count;
        Check(l.ReadState(foreign, ReadVia) && r.SessionSource == TweakRegistry.PreferencesInvalidSource && Warnings.Count == warnings + 1,
            "a non-string record is invalid, warned once, and follows current preferences");
        Check(!l.ReadState(null, ReadVia) && r.SessionSource == TweakRegistry.PreferencesInvalidSource, "a null state is ignored");
        l.Clear(TweakClearTrigger.SceneReload, Via);
        Check(!l.WantsRecord, "a scene reload disarms");
    }

    // FR-2: the transpiler inserts its call only at a single match.
    private static void IlMatch()
    {
        int count;
        var none = new List<string> { "ldarg.0", "ret" };
        Check(TweakSessionRecord.SingleMatch(none, s => s == "call", out count) == -1 && count == 0, "no match is a mismatch");
        var one = new List<string> { "ldloc.0", "call", "stloc.5" };
        Check(TweakSessionRecord.SingleMatch(one, s => s == "call", out count) == 1 && count == 1, "a single match gives its index");
        var two = new List<string> { "call", "ldloc.0", "call" };
        Check(TweakSessionRecord.SingleMatch(two, s => s == "call", out count) == -1 && count == 2, "two matches are a mismatch, never the first");
        Check(TweakSessionRecord.SingleMatch<string>(null, s => true, out count) == -1 && count == 0, "a null list is a mismatch");
        Check(TweakSessionRecord.SingleMatch(one, null, out count) == -1 && count == 0, "a null predicate is a mismatch");
    }
}
