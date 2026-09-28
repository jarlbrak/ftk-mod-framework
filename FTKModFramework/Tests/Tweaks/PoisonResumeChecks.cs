// Game-free checks for fix.poison-decay-resume (Spec #260 FR-1, FR-2): the descriptor, the
// bounded codec, the save decision, the restore decision and the resume players window inside
// TweakSessionLifecycle. The Harmony hooks run against stand-ins in Tests/SessionRecordHooks; the
// save and resume in the game are live-gated on #260.
using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using FTKModFramework.Core.UI;

internal static class PoisonResumeChecks
{
    private static int _checks;
    private static List<string> Warnings = new List<string>(), Infos = new List<string>(), Errors = new List<string>();

    private const string Via = "CharacterStats.StateDataDeserializeDone";
    private const string CloseVia = "uiStartGame.AllCowsCreated";

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("poison resume: " + message);
    }

    internal static int Run()
    {
        Descriptor();
        Codec();
        WriteDecision();
        RestoreDecision();
        Window();
        ApplyOnce();
        Discards();
        SharedPhotonId();
        Triggers();
        Trace();
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

    /// <summary>The shipped registry, as a player's game builds it.</summary>
    private static TweakRegistry Registry(bool selfTests = false, bool probeOn = false)
    {
        var r = new TweakRegistry(Warnings.Add);
        FrameworkTweaks.RegisterAll(r, selfTests);
        var store = new Store();
        if (selfTests) store.Values[FrameworkTweaks.SessionProbeDescriptor.Id] = probeOn ? TweakPreference.On : TweakPreference.Off;
        r.Initialize(store);
        return r;
    }

    private static TweakSessionLifecycle Lifecycle(TweakRegistry r, Func<int> probe = null)
    {
        Warnings = new List<string>();
        Infos = new List<string>();
        Errors = new List<string>();
        return new TweakSessionLifecycle(r, Warnings.Add, Infos.Add, Errors.Add, probe ?? (() => TweakRegistry.InvalidHandle));
    }

    private static int Count(List<string> lines, string fragment)
    {
        int n = 0;
        foreach (string line in lines) if (line.Contains(fragment)) n++;
        return n;
    }

    /// <summary>A solo resume up to the lock: arm, capture, lock. The window stays open.</summary>
    private static void ResumeToLock(TweakSessionLifecycle l)
    {
        l.ArmResume("uiStartGame.OnResumeGame");
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
    }

    // The user's 2026-09-27 decision: a Session Fix, explicitly on, with a balance note.
    private static void Descriptor()
    {
        TweakDescriptor d = FrameworkTweaks.PoisonDecayResumeDescriptor;
        Check(d.Id == "fix.poison-decay-resume" && d.Category == TweakCategory.Fix && d.Scope == TweakScope.Session,
            "the descriptor is a Session Fix with the specified ID");
        Check(d.ExplicitDefault == true && d.DefaultOn, "the default is an explicit On");
        Check(d.BalanceNote == "Poison no longer lasts extra turns after loading a save.", "the balance note is the specified text");
        Check(d.Evidence.Contains("m_PoisonTimeCounter") && d.Evidence.Contains("EnterFahrulRPC")
            && d.Evidence.Contains("StateDataDeserializeDone") && d.Evidence.Contains("AllCowsCreated"),
            "the evidence names the counter, the lock and both load hooks");

        TweakRegistry r = Registry();
        int handle = FrameworkTweaks.PoisonDecayResume;
        Check(handle != TweakRegistry.InvalidHandle && r.Get(handle) == d && r.PreferredOn(handle), "RegisterAll registers it, preferred on by default");
        Check(!r.IsOn(handle), "a Session tweak is off outside a run");
        r.Capture(TweakSessionMode.SinglePlayer);
        Check(r.IsOn(handle), "a solo capture turns it on");
        r.Clear();
        r.Capture(TweakSessionMode.LocalMultiplayer);
        Check(r.IsOn(handle), "a local multiplayer capture turns it on");
        r.Clear();
        r.Capture(TweakSessionMode.Multiplayer);
        Check(!r.IsOn(handle), "online co-op keeps it off until Spec B");
        r.Clear();

        bool balanceShown = false;
        foreach (List<ModsPanelTweaks.Item> page in ModsPanelTweaks.Pages(r, ModsPanelTweaks.PageBudget))
            foreach (ModsPanelTweaks.Item item in page)
                if (item.Row != null && item.Row.Handle == handle)
                    foreach (ModsPanelTweaks.Line line in item.Row.Lines)
                        if (line.Text == "Balance: " + d.BalanceNote) balanceShown = true;
        Check(balanceShown, "the Tweaks tab shows the balance note");
    }

    // FR-1 codec: "v1:" and 1 or 2, nothing else in either direction.
    private static void Codec()
    {
        Check(PoisonCountdown.Key == "ftkmf.poison", "the key is ftkmf.poison");
        Check(PoisonCountdown.Encode(1) == "v1:1" && PoisonCountdown.Encode(2) == "v1:2", "1 and 2 encode");
        foreach (int counter in new[] { int.MinValue, -1, 0, 3, 4, 10, int.MaxValue })
            Check(PoisonCountdown.Encode(counter) == null, "no other counter encodes: " + counter);
        Check(PoisonCountdown.Decode("v1:1") == 1 && PoisonCountdown.Decode("v1:2") == 2, "1 and 2 decode");
        Check(PoisonCountdown.Decode(PoisonCountdown.Encode(1)) == 1 && PoisonCountdown.Decode(PoisonCountdown.Encode(2)) == 2, "the codec round-trips");
        string[] rejected = { null, "", "v1:", "v1:0", "v1:3", "v1:9", "v1:-1", "v1:12", "v1:01", "v1:1 ", " v1:1", "v1:x",
            "v2:1", "V1:1", "v11", "1", "2", "v1:١", new string('v', 5000), "v1:" + new string('1', 5000) };
        foreach (string value in rejected)
            Check(PoisonCountdown.Decode(value) == 0, "rejected: " + (value == null ? "<null>" : value.Length > 20 ? value.Substring(0, 20) + "..." : value));
        Check(PoisonCountdown.FromStateValue("v1:2") == 2 && PoisonCountdown.FromStateValue(2) == 0
            && PoisonCountdown.FromStateValue(null) == 0 && PoisonCountdown.FromStateValue(new object()) == 0,
            "only a string value decodes");
        Check(PoisonCountdown.MayCarry("m_PoisonLvl=2;ftkmf.poison=v1:2") && !PoisonCountdown.MayCarry("m_PoisonLvl=2")
            && !PoisonCountdown.MayCarry(null) && !PoisonCountdown.MayCarry(string.Empty) && !PoisonCountdown.MayCarry("FTKMF.POISON"),
            "the pre-parse check is an ordinal search for the key");
    }

    // FR-1: only a locked run with the tweak on and a poisoned character mid-countdown writes.
    private static void WriteDecision()
    {
        int written = 0;
        foreach (bool locked in new[] { false, true })
            foreach (bool on in new[] { false, true })
                foreach (int level in new[] { -1, 0, 1, 3 })
                    foreach (int counter in new[] { -1, 0, 1, 2, 3 })
                    {
                        string value = PoisonCountdown.PoisonToWrite(locked, on, level, counter);
                        bool expected = locked && on && level > 0 && (counter == 1 || counter == 2);
                        Check(expected ? value == "v1:" + counter : value == null,
                            "write decision locked=" + locked + " on=" + on + " level=" + level + " counter=" + counter + ": " + value);
                        if (value != null) written++;
                    }
        Check(written == 4, "exactly the two levels times two counters write");
    }

    // FR-2: the restore decision fails closed on each condition alone.
    private static void RestoreDecision()
    {
        Check(PoisonCountdown.CounterToRestore(true, true, true, true, 2, 2) == 2
            && PoisonCountdown.CounterToRestore(true, true, true, true, 1, 1) == 1, "every condition met restores the recorded value");
        Check(PoisonCountdown.CounterToRestore(false, true, true, true, 2, 2) == 0, "the window closed discards");
        Check(PoisonCountdown.CounterToRestore(true, false, true, true, 2, 2) == 0, "a run not locked discards");
        Check(PoisonCountdown.CounterToRestore(true, true, false, true, 2, 2) == 0, "the tweak off discards");
        Check(PoisonCountdown.CounterToRestore(true, true, true, false, 2, 2) == 0, "an unserialized object discards");
        foreach (int recorded in new[] { -1, 0, 3, 7 })
            Check(PoisonCountdown.CounterToRestore(true, true, true, true, recorded, 2) == 0, "an out-of-range value discards: " + recorded);
        foreach (int level in new[] { -1, 0 })
            Check(PoisonCountdown.CounterToRestore(true, true, true, true, 2, level) == 0, "a cured character discards: " + level);
    }

    // The window opens on arm and survives the lock; #253's armed state does not.
    private static void Window()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        var stats = new object();
        Check(!l.ResumePlayersOpen, "no window at the title screen");
        Check(!l.StashPoison(stats, "v1:2") && l.PoisonPending == 0, "nothing is stashed without a resume");
        Check(l.TakePoison(stats, true, true, 2, Via) == 0 && l.PoisonDiscarded == 0, "an unstashed instance is not counted");

        l.ArmResume("uiStartGame.OnResumeGame");
        Check(l.ResumePlayersOpen && l.ResumeArmed, "arming opens the window");
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        Check(l.ResumePlayersOpen && !l.ResumeArmed && !l.WantsRecord, "the lock disarms the record read and leaves the window open");
        Check(!l.StashPoison(null, "v1:2") && !l.StashPoison(stats, null), "a null instance or an absent key stashes nothing");
        Check(l.StashPoison(stats, "v1:2") && l.PoisonPending == 1, "a saved value is stashed in the window");
        l.CloseResumePlayers(CloseVia);
        Check(!l.ResumePlayersOpen && l.PoisonPending == 0 && l.PoisonDiscarded == 1, "closing discards what was never applied");
        Check(Count(Infos, "restored 0, discarded 1") == 1, "the close logs the resume's counts once");
        l.CloseResumePlayers(CloseVia);
        Check(Count(Infos, "discarded") == 1, "a second close logs nothing");
        Check(!l.StashPoison(stats, "v1:2"), "nothing is stashed after the close");

        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
        l.ArmResume("uiStartGame.OnResumeGame");
        Check(l.ResumePlayersOpen && l.PoisonDiscarded == 0 && l.PoisonRestored == 0 && l.PoisonPending == 0, "a new resume starts from zero");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");
    }

    // Arm, stash, lock, then the apply after vanilla restored the level: once.
    private static void ApplyOnce()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        var stats = new object();
        ResumeToLock(l);
        bool on = r.IsOn(FrameworkTweaks.PoisonDecayResume);
        Check(on, "the resumed solo run has the tweak on");
        Check(l.StashPoison(stats, "v1:2"), "the load prefix stashes the value");
        Check(l.TakePoison(stats, on, true, 2, Via) == 2, "the apply returns the saved counter");
        Check(l.TakePoison(stats, on, true, 2, Via) == 0 && l.PoisonRestored == 1 && l.PoisonDiscarded == 0, "it applies once");
        l.CloseResumePlayers(CloseVia);
        Check(Count(Infos, "resume poison countdowns via uiStartGame.AllCowsCreated: restored 1, discarded 0.") == 1,
            "the close reports one restored");
        Check(Warnings.Count == 0 && Errors.Count == 0, "a clean resume warns about nothing");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
    }

    private static void Discards()
    {
        TweakRegistry r = Registry();
        int handle = FrameworkTweaks.PoisonDecayResume;
        r.Toggle(handle);
        TweakSessionLifecycle l = Lifecycle(r);
        var off = new object();
        ResumeToLock(l);
        Check(!r.IsOn(handle), "the player turned the tweak off");
        l.StashPoison(off, "v1:2");
        Check(l.TakePoison(off, r.IsOn(handle), true, 2, Via) == 0 && l.PoisonDiscarded == 1, "the tweak off discards");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
        r.Toggle(handle);

        ResumeToLock(l);
        var cured = new object();
        l.StashPoison(cured, "v1:2");
        Check(l.TakePoison(cured, true, true, 0, Via) == 0 && l.PoisonDiscarded == 1, "a character cured by the load (level 0) discards");
        var invalid = new object();
        l.StashPoison(invalid, "v1:3");
        Check(l.TakePoison(invalid, true, true, 2, Via) == 0 && l.PoisonDiscarded == 2, "an unreadable value discards");
        var twice = new object();
        l.StashPoison(twice, "v1:1");
        l.StashPoison(twice, "v1:2");
        Check(l.PoisonPending == 1 && l.PoisonDiscarded == 3, "a second state for the same instance replaces the first");
        Check(l.TakePoison(twice, true, true, 1, Via) == 2, "the later state wins");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");

        // Before the lock the run's set is not final, so a stash taken then is discarded.
        l.ArmResume("uiStartGame.OnResumeGame");
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        var early = new object();
        l.StashPoison(early, "v1:2");
        Check(l.TakePoison(early, true, true, 2, Via) == 0 && l.PoisonDiscarded == 1, "a run not yet locked discards");
        l.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");

        // Not armed: a locked run without a resume, such as a PunRPC into the same deserializer.
        l.Capture(0, "GameLogic.CreateOfflineRoom");
        l.Lock(0, "uiStartGame.EnterFahrulRPC");
        var rpc = new object();
        Check(!l.StashPoison(rpc, "v1:2") && l.TakePoison(rpc, true, true, 2, Via) == 0, "a locked run that never resumed stashes nothing");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");

        // After the window: a locked resumed run whose characters all exist.
        ResumeToLock(l);
        l.CloseResumePlayers(CloseVia);
        Check(!l.StashPoison(rpc, "v1:2"), "a state read after AllCowsCreated stashes nothing");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
    }

    // Local multiplayer: every character has the loader's PhotonID, so only the instance tells
    // them apart. The stash never looks at anything else.
    private static void SharedPhotonId()
    {
        TweakRegistry r = Registry();
        TweakSessionLifecycle l = Lifecycle(r);
        l.ArmResume("uiStartGame.OnResumeGame");
        l.Capture(2, "GameLogic.CreateOfflineRoom");
        l.Lock(2, "uiStartGame.EnterFahrulRPC");
        bool on = r.IsOn(FrameworkTweaks.PoisonDecayResume);
        var first = new SameId(1);
        var second = new SameId(1);
        Check(first.Equals(second) && first.GetHashCode() == second.GetHashCode(), "the stand-ins compare equal, as a PhotonID key would");
        l.StashPoison(first, "v1:1");
        l.StashPoison(second, "v1:2");
        Check(l.PoisonPending == 2, "two instances with one PhotonID stash separately");
        Check(l.TakePoison(second, on, true, 3, Via) == 2 && l.TakePoison(first, on, true, 1, Via) == 1,
            "each gets its own value, in any order");
        l.CloseResumePlayers(CloseVia);
        Check(Count(Infos, "restored 2, discarded 0") == 1, "local multiplayer restores both");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
    }

    /// <summary>Equal by PhotonID, as a key built from the ID would be.</summary>
    private sealed class SameId
    {
        private readonly int _photonId;
        internal SameId(int photonId) { _photonId = photonId; }
        public override bool Equals(object obj) { SameId other = obj as SameId; return other != null && other._photonId == _photonId; }
        public override int GetHashCode() { return _photonId; }
    }

    // Photon callbacks can fire during a resume's setup and never close the window; a scene
    // reload, the run end and a title-screen activation mean no run is going and close it.
    private static void Triggers()
    {
        foreach (TweakClearTrigger trigger in Enum.GetValues(typeof(TweakClearTrigger)))
        {
            TweakRegistry r = Registry();
            TweakSessionLifecycle l = Lifecycle(r);
            ResumeToLock(l);
            var stats = new object();
            l.StashPoison(stats, "v1:2");
            l.Clear(trigger, "trigger " + trigger);
            bool closes = trigger == TweakClearTrigger.SceneReload || trigger == TweakClearTrigger.RunEnd
                || trigger == TweakClearTrigger.TitleActivation;
            Check(TweakSessionLifecycle.DisarmsResume(trigger) == closes, "the window follows the disarm rule: " + trigger);
            Check(l.ResumePlayersOpen == !closes && l.PoisonPending == (closes ? 0 : 1), "the window " + (closes ? "closes" : "stays open") + " on " + trigger);
            Check(Count(Infos, "resume poison countdowns via trigger " + trigger + ": restored 0, discarded 1.") == (closes ? 1 : 0),
                "a closing clear logs the discard: " + trigger);
        }
    }

    // The probe trace records each restore and the close.
    private static void Trace()
    {
        TweakRegistry r = Registry(true, true);
        TweakSessionLifecycle l = Lifecycle(r, () => FrameworkTweaks.SessionProbe);
        ResumeToLock(l);
        var stats = new object();
        l.StashPoison(stats, "v1:1");
        l.TakePoison(stats, r.IsOn(FrameworkTweaks.PoisonDecayResume), true, 1, Via);
        l.CloseResumePlayers(CloseVia);
        Check(Count(Infos, TweakSessionLifecycle.ProbeTrace + " poison-restore via=" + Via + " counter=1") == 1, "a restore is traced");
        Check(Count(Infos, TweakSessionLifecycle.ProbeTrace + " resume-players-close via=" + CloseVia) == 1, "the close is traced");
        Check(Errors.Count == 0, "the probe records no failure");
        l.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
    }
}
