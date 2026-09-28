using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>What emptied the Session set. Scene reload, run end and a title-screen activation
    /// mean no run is going; the Photon callbacks can also fire while a run is still on screen.</summary>
    internal enum TweakClearTrigger
    {
        SceneReload,
        RunEnd,
        LeftRoom,
        Disconnected,
        JoinRoomFailed,
        /// <summary>A title-screen hot-reload activation committed. HotReloadBoundary.Check only
        /// admits one at a pristine title with no started run, so it is never a mid-run clear.</summary>
        TitleActivation,
    }

    /// <summary>Drives the registry's capture, lock and clear, and a resumed run's save record, from
    /// game lifecycle hooks and, when the self-test Session probe is registered, traces each step. Unity-free: the Harmony patches pass
    /// the raw GameLogic.GameMode value and a label naming the hook.</summary>
    internal sealed class TweakSessionLifecycle
    {
        internal const string ProbeTrace = "SESSION-PROBE [session-lifecycle]";
        internal const string ProbeTest = "[session-lifecycle]";

        private readonly TweakRegistry _registry;
        private readonly Action<string> _warn;
        private readonly Action<string> _info;
        private readonly Action<string> _error;
        private readonly Func<int> _probe;
        // Resume state (Spec #253 FR-3). Armed when a resume starts and disarmed at lock or when the
        // run is over. Photon callbacks never disarm: in solo offline an asynchronous disconnect can
        // clear the capture between the save's load and the lock, and the record must survive that.
        private bool _resumeArmed;
        private TweakSessionRecord _resumeRecord;
        private bool _resumeIgnoresLogged;
        private bool _oversizeWarned;

        internal TweakSessionLifecycle(TweakRegistry registry, Action<string> warn, Action<string> info,
            Action<string> error, Func<int> probeHandle)
        {
            _registry = registry;
            _warn = warn;
            _info = info;
            _error = error;
            _probe = probeHandle;
        }

        /// <summary>GameLogic.GameMode is SinglePlayer = 0, Multiplayer = 1, LocalMultiplayer = 2.
        /// Any other value, including -1 for "GameLogic is missing", fails closed as Multiplayer so
        /// every Session tweak stays off.</summary>
        internal static TweakSessionMode ModeFromGame(int gameMode)
        {
            switch (gameMode)
            {
                case 0: return TweakSessionMode.SinglePlayer;
                case 2: return TweakSessionMode.LocalMultiplayer;
                default: return TweakSessionMode.Multiplayer;
            }
        }

        /// <summary>At run start the captured set is kept only if it was captured for the mode the run
        /// actually has. A co-op client never passes a capture hook, and a disconnect callback can land
        /// between setup and start, so anything else is recaptured from the current mode.</summary>
        internal static bool NeedsCaptureAtLock(TweakSessionState state, TweakSessionMode? captured, TweakSessionMode current)
        {
            return state != TweakSessionState.Captured || captured != current;
        }

        /// <summary>A locked run cleared by a Photon callback may still be on screen, which points at a
        /// lifecycle bug. Scene reload, the run-end fade and a title-screen activation mean the run
        /// is over.</summary>
        internal static bool WarnOnClear(bool clearedLockedRun, TweakClearTrigger trigger)
        {
            return clearedLockedRun && trigger != TweakClearTrigger.SceneReload && trigger != TweakClearTrigger.RunEnd
                && trigger != TweakClearTrigger.TitleActivation;
        }

        /// <summary>Resume state is disarmed only by triggers that mean no run is going. The Photon
        /// callbacks can fire during a resume's setup, so they leave it armed.</summary>
        internal static bool DisarmsResume(TweakClearTrigger trigger)
        {
            return trigger == TweakClearTrigger.SceneReload || trigger == TweakClearTrigger.RunEnd
                || trigger == TweakClearTrigger.TitleActivation;
        }

        /// <summary>True from the start of a resume until its run locks or ends.</summary>
        internal bool ResumeArmed { get { return _resumeArmed; } }

        /// <summary>The record read during the armed resume, or null when none has been read.</summary>
        internal TweakSessionRecord ResumeRecord { get { return _resumeRecord; } }

        /// <summary>Run setup created its room. A later capture before the lock replaces this one, so
        /// a record read earlier in the same resume is applied again on top of it.</summary>
        internal bool Capture(int gameMode, string via)
        {
            bool captured = _registry.Capture(ModeFromGame(gameMode));
            if (!captured) return false;
            ApplyResume(via);
            Trace("capture", via);
            return true;
        }

        /// <summary>A saved run is being resumed. Forgets any record from an earlier resume.</summary>
        internal void ArmResume(string via)
        {
            _resumeArmed = true;
            _resumeRecord = null;
            _resumeIgnoresLogged = false;
            Trace("resume-arm", via);
        }

        /// <summary>True while a GameFlow state read would be used: a resume is armed and its run is
        /// not locked yet. The load hook checks this before parsing, so it parses at most once per
        /// resume and never for the PunRPC or co-op client paths into the same deserializer.</summary>
        internal bool WantsRecord
        {
            get { return _resumeArmed && _registry.IsInitialized && _registry.SessionState != TweakSessionState.Locked; }
        }

        /// <summary>The save's GameFlow state was deserialized; value is the record, or null when the
        /// key was absent. Returns false when the read is ignored: nothing is armed, which is how the
        /// same deserializer is reached as a PunRPC, or the run is already locked. Otherwise the record
        /// is kept for the lock and applied now if a capture exists.</summary>
        internal bool ReadRecord(string value, string via)
        {
            return Accept(TweakSessionRecord.Decode(value), via);
        }

        /// <summary>The load hook's entry: reads <see cref="TweakSessionRecord.Key"/> from a parsed
        /// GameFlow state dictionary, with the same rules as <see cref="ReadRecord"/>.</summary>
        internal bool ReadState(IDictionary<string, object> state, string via)
        {
            if (!WantsRecord || state == null) return false;
            object value;
            state.TryGetValue(TweakSessionRecord.Key, out value);
            return Accept(TweakSessionRecord.FromStateValue(value), via);
        }

        /// <summary>The save hook's entry: adds the record to a GameFlow state dictionary about to be
        /// serialized. Adds nothing, so the save matches vanilla, unless a locked run has a record.</summary>
        internal bool WriteRecord(IDictionary<string, object> state)
        {
            if (state == null) return false;
            string value = RecordToWrite();
            if (value == null) return false;
            state[TweakSessionRecord.Key] = value;
            return true;
        }

        private bool Accept(TweakSessionRecord record, string via)
        {
            if (!WantsRecord) return false;
            if (record.Status == TweakSessionRecordStatus.Invalid)
                Warn("Tweaks: the save's Session record is unreadable (" + record.Problem + "); the resumed run's Session tweaks follow the current preferences.");
            _resumeRecord = record;
            _resumeIgnoresLogged = false;
            ApplyResume(via);
            Trace("record", via);
            return true;
        }

        /// <summary>The value the save hook adds under TweakSessionRecord.Key, or null to add nothing.
        /// Warns once if a locked run's record would be too long to write.</summary>
        internal string RecordToWrite()
        {
            string value = TweakSessionRecord.ValueToWrite(_registry);
            if (value == null && !_oversizeWarned && TweakSessionRecord.Oversized(_registry))
            {
                _oversizeWarned = true;
                Warn("Tweaks: the Session record exceeds " + TweakSessionRecord.MaxLength + " characters and is not saved; a resume of this save will follow the current preferences.");
            }
            return value;
        }

        /// <summary>The run started on this machine. Reconciles the captured set with the run's mode,
        /// then fixes it until the next clear.</summary>
        internal bool Lock(int gameMode, string via)
        {
            if (!_registry.IsInitialized)
            {
                DisarmResume();
                return false;
            }
            TweakSessionMode mode = ModeFromGame(gameMode);
            if (_registry.SessionState == TweakSessionState.Locked)
            {
                Warn("Tweaks: a run started while the previous run's Session set was still locked; it was never cleared. Recapturing for this run.");
                _registry.Clear();
            }
            // A recapture here also reapplies an armed resume's record, such as after a Photon clear
            // between the save's load and the lock.
            if (NeedsCaptureAtLock(_registry.SessionState, _registry.SessionMode, mode))
                Capture(gameMode, via + " (at run start)");
            bool locked = _registry.Lock();
            if (locked)
            {
                Trace("lock", via);
                CheckLockedProbe();
            }
            DisarmResume();
            return locked;
        }

        /// <summary>Empties the Session set. Returns true when a locked run was cleared.</summary>
        internal bool Clear(TweakClearTrigger trigger, string via)
        {
            if (DisarmsResume(trigger)) DisarmResume();
            if (_registry.SessionState == TweakSessionState.None) return false;
            bool wasLocked = _registry.Clear();
            bool warn = WarnOnClear(wasLocked, trigger);
            if (warn)
                Warn("Tweaks: " + via + " cleared the Session set of a run that may still be going; its Session tweaks are now off. This points at a lifecycle bug.");
            Trace("clear", via + (wasLocked ? " locked=true" : " locked=false"));
            int probe = ProbeHandle();
            if (probe != TweakRegistry.InvalidHandle)
            {
                if (warn || _registry.IsOn(probe))
                    Error("SELF-TEST FAIL " + ProbeTest + ": clear via " + via + " while the run may continue=" + warn + ", probe on after clear=" + _registry.IsOn(probe));
                else
                    Info("SELF-TEST PASS " + ProbeTest + ": clear via " + via + " left the probe off");
            }
            return wasLocked;
        }

        private void CheckLockedProbe()
        {
            int probe = ProbeHandle();
            if (probe == TweakRegistry.InvalidHandle) return;
            string source = _registry.SessionSource;
            bool preferred = _registry.PreferredOn(probe) && !_registry.IsFaulted(probe);
            bool expected;
            if (source == TweakRegistry.PreferencesSource || source == TweakRegistry.PreferencesLegacySource
                || source == TweakRegistry.PreferencesInvalidSource) expected = preferred;
            else if (source == TweakRegistry.PendingSource) expected = false;
            else if (source == TweakRegistry.SaveSource && _resumeRecord != null
                && _resumeRecord.Status == TweakSessionRecordStatus.Valid)
            {
                bool recorded;
                expected = _resumeRecord.TryGetState(_registry.Get(probe).Id, out recorded) ? recorded : preferred;
            }
            else return;
            bool actual = _registry.IsOn(probe);
            string detail = "lock mode=" + _registry.SessionMode + " source=" + source + " probe=" + OnOff(actual) + " expected=" + OnOff(expected);
            if (actual == expected) Info("SELF-TEST PASS " + ProbeTest + ": " + detail);
            else Error("SELF-TEST FAIL " + ProbeTest + ": " + detail);
        }

        /// <summary>Applies the armed record to a captured solo or local run. Online co-op keeps its
        /// pending set: a host applying its save alone would split rules with its clients.</summary>
        private void ApplyResume(string via)
        {
            if (!_resumeArmed || _resumeRecord == null || _registry.SessionState != TweakSessionState.Captured) return;
            TweakSessionMode? mode = _registry.SessionMode;
            if (mode != TweakSessionMode.SinglePlayer && mode != TweakSessionMode.LocalMultiplayer) return;
            TweakSessionResolution resolution = TweakSessionRecord.Resolve(_registry, _resumeRecord);
            if (!_registry.SetSessionSet(resolution.OnIds, resolution.Source)) return;
            if (!_resumeIgnoresLogged)
            {
                _resumeIgnoresLogged = true;
                foreach (string id in resolution.UnknownIds)
                    Info("Tweaks: the save's Session record lists '" + id + "', which this build does not register; ignored.");
                foreach (string id in resolution.LocalIds)
                    Info("Tweaks: the save's Session record lists '" + id + "', which is a Local tweak; ignored.");
            }
            Info("Tweaks: the resumed run's Session set comes from " + resolution.Source + " via " + via + " ("
                + resolution.FromRecord + " from the save, " + resolution.FromPreferences + " from preferences).");
        }

        private void DisarmResume()
        {
            _resumeArmed = false;
            _resumeRecord = null;
            _resumeIgnoresLogged = false;
        }

        /// <summary>One probe trace line, written only while the self-test probe is registered. Session
        /// tweak patches call it too, so a run's log shows their shared-state changes beside its lifecycle.</summary>
        internal void Trace(string step, string via)
        {
            int probe = ProbeHandle();
            if (probe == TweakRegistry.InvalidHandle) return;
            Info(ProbeTrace + " " + step + " via=" + via + " state=" + _registry.SessionState
                + " mode=" + (_registry.SessionMode.HasValue ? _registry.SessionMode.Value.ToString() : "none")
                + " source=" + (_registry.SessionSource ?? "none") + " probe=" + OnOff(_registry.IsOn(probe)));
        }

        private int ProbeHandle()
        {
            if (_probe == null) return TweakRegistry.InvalidHandle;
            int handle = _probe();
            return _registry.Get(handle) != null ? handle : TweakRegistry.InvalidHandle;
        }

        private static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private void Warn(string message)
        {
            if (_warn != null) _warn(message);
        }

        private void Info(string message)
        {
            if (_info != null) _info(message);
        }

        private void Error(string message)
        {
            if (_error != null) _error(message);
        }
    }
}
