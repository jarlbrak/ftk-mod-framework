using System;
using System.Collections.Generic;
using System.Text;

namespace FTKModFramework.Core
{
    internal enum TweakSessionRecordStatus
    {
        /// <summary>The save has no record: it predates the feature or vanilla re-saved it.</summary>
        Absent,
        /// <summary>A record is present but unusable. Treated as no record, with its own source label.</summary>
        Invalid,
        Valid,
    }

    /// <summary>The run's Session set as stored in a save under the GameFlow state key
    /// <see cref="Key"/> (Spec #253 FR-1). The value is a plain string so vanilla's serializer never
    /// sees a framework type: "v1:" followed by "+id" or "-id" for every registered Session tweak,
    /// comma-separated and sorted by ID. Unity-free, so the save hooks stay thin and testable.</summary>
    internal sealed class TweakSessionRecord
    {
        /// <summary>Contains a dot, so it cannot collide with a serialized C# field name.</summary>
        internal const string Key = "ftkmf.session";
        internal const string VersionPrefix = "v1:";
        internal const int MaxLength = 1024;

        internal static readonly TweakSessionRecord Absent =
            new TweakSessionRecord(TweakSessionRecordStatus.Absent, null, null);

        private readonly Dictionary<string, bool> _states;

        private TweakSessionRecord(TweakSessionRecordStatus status, Dictionary<string, bool> states, string problem)
        {
            Status = status;
            _states = states ?? new Dictionary<string, bool>(StringComparer.Ordinal);
            Problem = problem;
        }

        internal TweakSessionRecordStatus Status { get; private set; }
        /// <summary>Why an invalid record was rejected; null otherwise.</summary>
        internal string Problem { get; private set; }
        internal int Count { get { return _states.Count; } }

        internal bool TryGetState(string id, out bool on)
        {
            on = false;
            return id != null && _states.TryGetValue(id, out on);
        }

        /// <summary>Recorded IDs in ordinal order, so logging is deterministic.</summary>
        internal List<string> Ids()
        {
            var ids = new List<string>(_states.Keys);
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>Null means the key was absent. Any other value is either a complete valid record or
        /// invalid as a whole; a partly readable record is never applied, because silently dropping one
        /// entry would change the run's rules.</summary>
        internal static TweakSessionRecord Decode(string value)
        {
            if (value == null) return Absent;
            if (value.Length > MaxLength) return Invalid("longer than " + MaxLength + " characters");
            if (!value.StartsWith(VersionPrefix, StringComparison.Ordinal))
                return Invalid(HasVersionPrefix(value) ? "unknown version prefix" : "malformed: no version prefix");
            if (value.Length == VersionPrefix.Length) return Invalid("malformed: no entries");

            var states = new Dictionary<string, bool>(StringComparer.Ordinal);
            string[] entries = value.Substring(VersionPrefix.Length).Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (entry.Length < 2 || (entry[0] != '+' && entry[0] != '-'))
                    return Invalid("malformed entry " + (i + 1));
                string id = entry.Substring(1);
                if (!TweakRegistry.IsValidId(id)) return Invalid("invalid ID in entry " + (i + 1));
                if (states.ContainsKey(id)) return Invalid("duplicate ID '" + id + "'");
                states.Add(id, entry[0] == '+');
            }
            return new TweakSessionRecord(TweakSessionRecordStatus.Valid, states, null);
        }

        /// <summary>Decodes the value found under <see cref="Key"/> in a parsed GameFlow state. The
        /// framework only ever writes a string; any other type means something else wrote the key, so
        /// the record is invalid rather than guessed at.</summary>
        internal static TweakSessionRecord FromStateValue(object value)
        {
            if (value == null) return Absent;
            string text = value as string;
            return text != null ? Decode(text) : Invalid("not a string");
        }

        /// <summary>The index of the only item that matches, or -1 when none or several do; count
        /// reports how many matched. The save transpiler inserts its call only at a single match, as
        /// SaveNamespace.RewritePaths asserts, so drifted IL gets no record instead of a wrong one.</summary>
        internal static int SingleMatch<T>(IList<T> items, Predicate<T> match, out int count)
        {
            count = 0;
            int index = -1;
            if (items == null || match == null) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (!match(items[i])) continue;
                count++;
                index = i;
            }
            return count == 1 ? index : -1;
        }

        /// <summary>Every registered Session tweak with its effective state, or null when none is
        /// registered or the value would exceed <see cref="MaxLength"/>. Local tweaks are never recorded.</summary>
        internal static string Encode(TweakRegistry registry)
        {
            string value = Build(registry);
            return value != null && value.Length <= MaxLength ? value : null;
        }

        /// <summary>The save hook's decision (FR-2): the value to add under <see cref="Key"/>, or null to
        /// write nothing so the save stays byte-identical to vanilla. Only a locked run has rules to
        /// keep; at the title screen or during setup the set is not final.</summary>
        internal static string ValueToWrite(TweakRegistry registry)
        {
            return IsLocked(registry) ? Encode(registry) : null;
        }

        /// <summary>True when a locked run has a record that does not fit, which the caller should warn
        /// about: the next resume of that save would fall back to preferences.</summary>
        internal static bool Oversized(TweakRegistry registry)
        {
            string value = IsLocked(registry) ? Build(registry) : null;
            return value != null && value.Length > MaxLength;
        }

        /// <summary>The run's Session set for a resume (FR-1, FR-4). Each registered Session tweak uses
        /// the record's state when listed, faults included, so the run keeps the rules it was saved
        /// with. Anything else resolves as a SinglePlayer capture does: the player's preference, minus
        /// faulted tweaks. A null record counts as absent.</summary>
        internal static TweakSessionResolution Resolve(TweakRegistry registry, TweakSessionRecord record)
        {
            if (record == null) record = Absent;
            var resolution = new TweakSessionResolution(SourceFor(record.Status));
            if (registry == null || !registry.IsInitialized) return resolution;
            bool valid = record.Status == TweakSessionRecordStatus.Valid;
            for (int handle = 0; handle < registry.Count; handle++)
            {
                TweakDescriptor descriptor = registry.Get(handle);
                if (descriptor.Scope != TweakScope.Session) continue;
                bool on;
                if (valid && record.TryGetState(descriptor.Id, out on)) resolution.FromRecord++;
                else
                {
                    on = registry.PreferredOn(handle) && !registry.IsFaulted(handle);
                    resolution.FromPreferences++;
                }
                if (on) resolution.OnIds.Add(descriptor.Id);
            }
            if (valid)
            {
                foreach (string id in record.Ids())
                {
                    int handle;
                    if (!registry.TryGetHandle(id, out handle)) resolution.UnknownIds.Add(id);
                    else if (registry.Get(handle).Scope != TweakScope.Session) resolution.LocalIds.Add(id);
                }
            }
            return resolution;
        }

        internal static string SourceFor(TweakSessionRecordStatus status)
        {
            switch (status)
            {
                case TweakSessionRecordStatus.Valid: return TweakRegistry.SaveSource;
                case TweakSessionRecordStatus.Invalid: return TweakRegistry.PreferencesInvalidSource;
                default: return TweakRegistry.PreferencesLegacySource;
            }
        }

        private static bool IsLocked(TweakRegistry registry)
        {
            return registry != null && registry.IsInitialized && registry.SessionState == TweakSessionState.Locked;
        }

        private static string Build(TweakRegistry registry)
        {
            if (registry == null || !registry.IsInitialized) return null;
            var ids = new List<string>();
            for (int handle = 0; handle < registry.Count; handle++)
            {
                TweakDescriptor descriptor = registry.Get(handle);
                if (descriptor.Scope == TweakScope.Session) ids.Add(descriptor.Id);
            }
            if (ids.Count == 0) return null;
            ids.Sort(StringComparer.Ordinal);
            var value = new StringBuilder(VersionPrefix);
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) value.Append(',');
                value.Append(registry.IsOn(ids[i]) ? '+' : '-').Append(ids[i]);
            }
            return value.ToString();
        }

        private static TweakSessionRecord Invalid(string problem)
        {
            return new TweakSessionRecord(TweakSessionRecordStatus.Invalid, null, problem);
        }

        /// <summary>"v" and one or more digits, then ':'. Tells a record from a later format apart from
        /// garbage in the warning; both are rejected the same way.</summary>
        private static bool HasVersionPrefix(string value)
        {
            if (value.Length < 3 || value[0] != 'v') return false;
            int i = 1;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9') i++;
            return i > 1 && i < value.Length && value[i] == ':';
        }
    }

    /// <summary>A resolved resume set: the complete list of Session IDs that are on, the source
    /// label, and what the record listed that this build ignores.</summary>
    internal sealed class TweakSessionResolution
    {
        internal TweakSessionResolution(string source)
        {
            Source = source;
            OnIds = new List<string>();
            UnknownIds = new List<string>();
            LocalIds = new List<string>();
        }

        internal string Source { get; private set; }
        internal List<string> OnIds { get; private set; }
        /// <summary>Registered Session tweaks that took the record's state.</summary>
        internal int FromRecord { get; set; }
        /// <summary>Registered Session tweaks that the record did not list, or all of them without a valid record.</summary>
        internal int FromPreferences { get; set; }
        /// <summary>Recorded IDs this build does not register, such as tweaks from a newer framework.</summary>
        internal List<string> UnknownIds { get; private set; }
        /// <summary>Recorded IDs that this build registers as Local; a record never governs them.</summary>
        internal List<string> LocalIds { get; private set; }
    }
}
