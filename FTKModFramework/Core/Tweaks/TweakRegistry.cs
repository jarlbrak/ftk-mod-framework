using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FTKModFramework.Core
{
    /// <summary>Pure mirror of GameLogic.GameMode, with the same names and values. The game hosts
    /// solo play in a closed Photon room, so room membership cannot tell co-op from solo; the
    /// game mode can.</summary>
    internal enum TweakSessionMode
    {
        SinglePlayer = 0,
        Multiplayer = 1,
        LocalMultiplayer = 2,
    }

    internal enum TweakSessionState
    {
        /// <summary>No run is being set up. Session tweaks are off.</summary>
        None,
        /// <summary>Run setup captured a Session set. It may still be replaced before the run starts.</summary>
        Captured,
        /// <summary>The run started. The Session set is fixed until the next clear.</summary>
        Locked,
    }

    /// <summary>Descriptors, player preferences, fault flags and the effective set that tweak
    /// patches consult at call time. Registration happens once at load; Initialize then freezes
    /// the registry so handles index fixed-size arrays and IsOn never allocates.</summary>
    internal sealed class TweakRegistry
    {
        internal const int InvalidHandle = -1;
        internal const string PreferencesSource = "preferences";
        /// <summary>Online co-op before a host set arrives: every Session tweak is off.</summary>
        internal const string PendingSource = "pending";

        private readonly Action<string> _warn;
        private readonly List<TweakDescriptor> _descriptors = new List<TweakDescriptor>();
        private readonly Dictionary<string, int> _handles = new Dictionary<string, int>(StringComparer.Ordinal);
        private ReadOnlyCollection<int> _displayOrder = new ReadOnlyCollection<int>(new int[0]);
        private ITweakPreferenceStore _store;
        private bool _initialized;
        private TweakPreference[] _preferences;
        private bool[] _faulted;
        private bool[] _session;
        private bool[] _effective;
        private TweakSessionState _state;
        private TweakSessionMode? _mode;
        private string _source;

        internal TweakRegistry(Action<string> warn)
        {
            _warn = warn;
        }

        internal bool IsInitialized { get { return _initialized; } }
        internal int Count { get { return _descriptors.Count; } }
        /// <summary>Handles ordered by category, then title, for display.</summary>
        internal ReadOnlyCollection<int> DisplayOrder { get { return _displayOrder; } }
        internal TweakSessionState SessionState { get { return _state; } }
        internal TweakSessionMode? SessionMode { get { return _mode; } }
        /// <summary>Where the current Session set came from, or null when there is none.</summary>
        internal string SessionSource { get { return _source; } }

        internal TweakDescriptor Get(int handle)
        {
            return Valid(handle) ? _descriptors[handle] : null;
        }

        internal bool TryGetHandle(string id, out int handle)
        {
            handle = InvalidHandle;
            return id != null && _handles.TryGetValue(id, out handle);
        }

        /// <summary>Returns a handle, or InvalidHandle after logging why the descriptor was
        /// rejected. A rejected duplicate leaves the first registration in place.</summary>
        internal int Register(TweakDescriptor descriptor)
        {
            if (descriptor == null) return Reject("a null descriptor");
            string id = descriptor.Id;
            if (_initialized) return Reject("'" + id + "' after initialization");
            if (!IsValidId(id)) return Reject("'" + id + "': IDs are lowercase dot-separated segments of a-z, 0-9 and '-'");
            if (_handles.ContainsKey(id)) return Reject("duplicate ID '" + id + "'; the first registration is kept");
            if (!IsDefined(descriptor.Category) || !IsDefined(descriptor.Scope))
                return Reject("'" + id + "': unknown category or scope");
            if (string.IsNullOrEmpty(descriptor.Title)) return Reject("'" + id + "': a title is required");
            if (descriptor.Category == TweakCategory.Fix && !string.IsNullOrEmpty(descriptor.BalanceNote)
                && !descriptor.ExplicitDefault.HasValue)
                return Reject("'" + id + "': a Fix with a balance note must set its default explicitly");

            int handle = _descriptors.Count;
            _descriptors.Add(descriptor);
            _handles.Add(id, handle);
            RebuildDisplayOrder();
            return handle;
        }

        internal static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool segmentStart = true;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '.')
                {
                    if (segmentStart) return false;
                    segmentStart = true;
                    continue;
                }
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')) return false;
                segmentStart = false;
            }
            return !segmentStart;
        }

        /// <summary>Reads every preference and freezes registration. On failure the registry
        /// stays uninitialized, so every tweak reports off and the game runs vanilla.</summary>
        internal bool Initialize(ITweakPreferenceStore store)
        {
            if (_initialized) return true;
            if (store == null)
            {
                Warn("Tweaks: initialization failed: no preference store.");
                return false;
            }
            int count = _descriptors.Count;
            var preferences = new TweakPreference[count];
            try
            {
                for (int i = 0; i < count; i++) preferences[i] = TweakPreferences.Normalize(store.Read(_descriptors[i].Id));
            }
            catch (Exception e)
            {
                Warn("Tweaks: initialization failed; every tweak stays off: " + e);
                return false;
            }
            _store = store;
            _preferences = preferences;
            _faulted = new bool[count];
            _session = new bool[count];
            _effective = new bool[count];
            _initialized = true;
            RefreshAll();
            return true;
        }

        /// <summary>The only question a tweak patch asks. False before initialization and for
        /// unknown handles. Must not allocate: patches may sit on per-frame or per-hit paths.</summary>
        internal bool IsOn(int handle)
        {
            bool[] effective = _effective;
            return effective != null && (uint)handle < (uint)effective.Length && effective[handle];
        }

        internal bool IsOn(string id)
        {
            int handle;
            return TryGetHandle(id, out handle) && IsOn(handle);
        }

        /// <summary>The stored choice, normalized. Default when uninitialized or unknown.</summary>
        internal TweakPreference Preference(int handle)
        {
            return _initialized && Valid(handle) ? _preferences[handle] : TweakPreference.Default;
        }

        /// <summary>What the player chose, before faults and session rules apply.</summary>
        internal bool PreferredOn(int handle)
        {
            return _initialized && Valid(handle)
                && TweakPreferences.Resolve(_preferences[handle], _descriptors[handle].DefaultOn);
        }

        internal bool IsFaulted(int handle)
        {
            return _initialized && Valid(handle) && _faulted[handle];
        }

        /// <summary>Flips the player's choice and saves it. A Session change reaches only the
        /// next capture; the current run keeps its captured set.</summary>
        internal bool Toggle(int handle)
        {
            if (!_initialized || !Valid(handle)) return false;
            return SetPreference(handle, TweakPreferences.Toggle(_preferences[handle], _descriptors[handle].DefaultOn));
        }

        internal bool ResetAll()
        {
            if (!_initialized) return false;
            bool ok = true;
            for (int i = 0; i < _descriptors.Count; i++) ok &= SetPreference(i, TweakPreference.Default);
            return ok;
        }

        /// <summary>Run setup. Solo and local play share one machine, so the player's preferences
        /// are the shared rules, minus Session tweaks that have faulted. Online co-op, and any mode
        /// this build does not know, keeps every Session tweak off until a source is supplied.
        /// Ignored once the run is locked, until a clear.</summary>
        internal bool Capture(TweakSessionMode mode)
        {
            if (!_initialized || _state == TweakSessionState.Locked) return false;
            bool fromPreferences = mode == TweakSessionMode.SinglePlayer || mode == TweakSessionMode.LocalMultiplayer;
            for (int i = 0; i < _descriptors.Count; i++)
            {
                _session[i] = fromPreferences && _descriptors[i].Scope == TweakScope.Session && !_faulted[i]
                    && TweakPreferences.Resolve(_preferences[i], _descriptors[i].DefaultOn);
            }
            _state = TweakSessionState.Captured;
            _mode = mode;
            _source = fromPreferences ? PreferencesSource : PendingSource;
            RefreshAll();
            return true;
        }

        /// <summary>Replaces the captured Session set with a complete one, such as the host's set
        /// or a save's record. IDs not listed are off; unknown and Local IDs are ignored. The set
        /// is applied as given, faults included, so every player in the run shares the same rules;
        /// a faulting patch still falls back to vanilla per call. Only valid between capture and
        /// lock.</summary>
        internal bool SetSessionSet(IEnumerable<string> onIds, string sourceLabel)
        {
            if (!_initialized || _state != TweakSessionState.Captured || onIds == null) return false;
            Array.Clear(_session, 0, _session.Length);
            foreach (string id in onIds)
            {
                int handle;
                if (TryGetHandle(id, out handle) && _descriptors[handle].Scope == TweakScope.Session)
                    _session[handle] = true;
            }
            _source = string.IsNullOrEmpty(sourceLabel) ? "unlabeled" : sourceLabel;
            RefreshAll();
            return true;
        }

        /// <summary>Run start. Fixes the captured set until the next clear.</summary>
        internal bool Lock()
        {
            if (_state != TweakSessionState.Captured) return false;
            _state = TweakSessionState.Locked;
            return true;
        }

        /// <summary>Empties the Session state. Returns true when a locked run was cleared, which the
        /// caller should warn about if the run is still going, since that points at a lifecycle bug.
        /// Fault flags are kept.</summary>
        internal bool Clear()
        {
            bool wasLocked = _state == TweakSessionState.Locked;
            _state = TweakSessionState.None;
            _mode = null;
            _source = null;
            if (_initialized)
            {
                Array.Clear(_session, 0, _session.Length);
                RefreshAll();
            }
            return wasLocked;
        }

        /// <summary>Called by a tweak patch that caught its own exception. A Local tweak turns off
        /// for the rest of the process. A Session tweak never changes the current run, because that
        /// would split rules between players; it is left out of the next capture instead. Logged
        /// once per tweak.</summary>
        internal void Fault(int handle, Exception exception)
        {
            if (!_initialized || !Valid(handle) || _faulted[handle]) return;
            _faulted[handle] = true;
            TweakDescriptor descriptor = _descriptors[handle];
            string consequence = descriptor.Scope == TweakScope.Local
                ? "it stays off until the game restarts"
                : "the current run keeps its rules and the tweak is left out of the next run";
            Warn("Tweaks: '" + descriptor.Id + "' faulted; " + consequence + ": " + exception);
            if (descriptor.Scope == TweakScope.Local) Refresh(handle);
        }

        private bool SetPreference(int handle, TweakPreference preference)
        {
            try
            {
                _store.Write(_descriptors[handle].Id, preference);
            }
            catch (Exception e)
            {
                Warn("Tweaks: could not save the preference for '" + _descriptors[handle].Id + "': " + e);
                return false;
            }
            _preferences[handle] = preference;
            Refresh(handle);
            return true;
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _descriptors.Count; i++) Refresh(i);
        }

        private void Refresh(int handle)
        {
            TweakDescriptor descriptor = _descriptors[handle];
            _effective[handle] = descriptor.Scope == TweakScope.Local
                ? !_faulted[handle] && TweakPreferences.Resolve(_preferences[handle], descriptor.DefaultOn)
                : _session[handle];
        }

        private bool Valid(int handle)
        {
            return (uint)handle < (uint)_descriptors.Count;
        }

        private void RebuildDisplayOrder()
        {
            var order = new List<int>(_descriptors.Count);
            for (int i = 0; i < _descriptors.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                TweakDescriptor x = _descriptors[a], y = _descriptors[b];
                int byCategory = ((int)x.Category).CompareTo((int)y.Category);
                if (byCategory != 0) return byCategory;
                int byTitle = StringComparer.OrdinalIgnoreCase.Compare(x.Title, y.Title);
                return byTitle != 0 ? byTitle : string.CompareOrdinal(x.Id, y.Id);
            });
            _displayOrder = new ReadOnlyCollection<int>(order);
        }

        private int Reject(string what)
        {
            Warn("Tweaks: rejected " + what + ".");
            return InvalidHandle;
        }

        private void Warn(string message)
        {
            if (_warn != null) _warn(message);
        }

        private static bool IsDefined(TweakCategory category)
        {
            return category == TweakCategory.Fix || category == TweakCategory.Information
                || category == TweakCategory.Convenience;
        }

        private static bool IsDefined(TweakScope scope)
        {
            return scope == TweakScope.Local || scope == TweakScope.Session;
        }
    }
}
