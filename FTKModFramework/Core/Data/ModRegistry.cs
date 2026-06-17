using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// One row in the <see cref="ModRegistry"/>: a single mod's identity and current enabled state.
    /// Deliberately flat (four fields). No descriptions, categories, dependencies, or load-order data:
    /// item #18's UI reads exactly these fields. <see cref="Version"/> is carried for the UI only and
    /// is not part of the gating contract.
    /// </summary>
    internal sealed class ModEntry
    {
        /// <summary>Stable identity. For data mods this is the RAW <c>Manifest.ModGuid</c> (not sanitized);
        /// for the bundled demo it is <c>Plugin.Guid</c>. It is also the PlayerPrefs sub-key for data mods.</summary>
        public readonly string Key;

        /// <summary>Human-readable name for the (future) UI. Data mods use <c>Manifest.Name</c>; the demo
        /// uses a fixed literal.</summary>
        public readonly string DisplayName;

        /// <summary>True for the one bundled-demo row (its enabled state is backed by EnableSampleContent,
        /// not PlayerPrefs). Exactly one entry has this set.</summary>
        public readonly bool IsBundledDemo;

        /// <summary>Optional mod version for the UI (data mods only). Null for the demo. Not used for gating.</summary>
        public readonly string Version;

        /// <summary>Current enabled state. Mutated only by <see cref="ModRegistry.SetEnabled"/>, which also
        /// persists it. Read by <see cref="ModRegistry.IsEnabled"/> for load-time gating.</summary>
        public bool Enabled;

        public ModEntry(string key, string displayName, bool isBundledDemo, string version, bool enabled)
        {
            Key = key;
            DisplayName = displayName;
            IsBundledDemo = isBundledDemo;
            Version = version;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// The single in-memory source of truth for which mods exist and whether each is enabled. It is a flat,
    /// ordered list of <see cref="ModEntry"/> rows, built EXACTLY ONCE per process during the
    /// <c>TableManager.Initialize</c> load path, off two registration sites that are deliberately NOT
    /// unified:
    ///   1. <c>Plugin</c>'s postfix registers the one bundled-demo row (keyed <c>Plugin.Guid</c>).
    ///   2. <c>ContentLoader.CollectEntries</c> registers each discovered data mod (keyed
    ///      <c>Manifest.ModGuid</c>) before gating its files.
    /// There is no second filesystem discovery pass: the registry only records what discovery already found.
    ///
    /// Ordering is deterministic: the demo registers first (it runs before the data loader in the postfix),
    /// then data mods append in <c>ModDiscovery</c>'s existing <c>(modGuid, folder)</c> order. <see cref="Entries"/>
    /// returns ALL rows including disabled ones (the UI needs to show what it can re-enable).
    ///
    /// Persistence is split by row kind (FR-2): the demo row reads/writes <c>Plugin.EnableSampleContent</c>;
    /// data-mod rows read/write <c>PlayerPrefs</c> under <see cref="PrefKeyPrefix"/> + Key. A missing
    /// PlayerPrefs key defaults to the caller-supplied <c>defaultEnabled</c>.
    ///
    /// Built once on the main thread and read on the main thread (NFR-4): no locks, no concurrent collections.
    /// <see cref="IsEnabled"/> fails OPEN (unknown key -&gt; true) so a gating read can never silently drop
    /// content that was never registered. Gating changes WHICH mods load, never the id-minting order WITHIN
    /// the surviving set (NFR-3): the registry holds no id state.
    /// </summary>
    internal static class ModRegistry
    {
        /// <summary>PlayerPrefs sub-key prefix for data-mod enabled state. The full key is this + the mod's Key.</summary>
        public const string PrefKeyPrefix = "ftkmf_mod_enabled:";

        // Ordered list IS the public view; the dictionary is just an index for idempotent Register/lookup.
        // Both built and read on the main thread, so plain (non-concurrent) collections are correct (NFR-4).
        private static readonly List<ModEntry> _entries = new List<ModEntry>();
        private static readonly Dictionary<string, ModEntry> _byKey =
            new Dictionary<string, ModEntry>(System.StringComparer.Ordinal);

        /// <summary>All registered mods including disabled ones, in registration order (demo first). Read-only.</summary>
        public static ReadOnlyCollection<ModEntry> Entries
        {
            get { return _entries.AsReadOnly(); }
        }

        /// <summary>
        /// Register a mod row, returning the live entry. Idempotent on <paramref name="key"/>: a repeat call
        /// returns the EXISTING entry untouched (so a second TableManager.Initialize pass cannot duplicate or
        /// reset rows). On first registration the enabled state is seeded from the backing store: the demo row
        /// reads <c>Plugin.EnableSampleContent.Value</c>; a data-mod row reads its PlayerPrefs key, defaulting
        /// to <paramref name="defaultEnabled"/> when absent.
        /// </summary>
        public static ModEntry Register(string key, string displayName, bool isBundledDemo, bool defaultEnabled)
        {
            ModEntry existing;
            if (_byKey.TryGetValue(key, out existing)) return existing; // idempotent: never re-seed.

            bool enabled = isBundledDemo
                ? Plugin.EnableSampleContent.Value
                : (UnityEngine.PlayerPrefs.GetInt(PrefKeyPrefix + key, defaultEnabled ? 1 : 0) != 0);

            string name = (displayName == null || displayName.Trim().Length == 0) ? key : displayName;
            // Version is UI-only metadata; the demo has none. Register does not receive it (the spec's row is
            // Key/DisplayName/IsBundledDemo/Enabled), so it stays null here.
            ModEntry entry = new ModEntry(key, name, isBundledDemo, null, enabled);
            _entries.Add(entry);
            _byKey[key] = entry;
            return entry;
        }

        /// <summary>
        /// Gating read used at load time. Returns the registered row's <c>Enabled</c> state, or TRUE for an
        /// unknown key (fail-open): a key the registry never saw must not be silently dropped.
        /// </summary>
        public static bool IsEnabled(string key)
        {
            ModEntry entry;
            if (_byKey.TryGetValue(key, out entry)) return entry.Enabled;
            return true; // fail-open (FR-3): unknown key is treated as enabled.
        }

        /// <summary>
        /// Set and PERSIST a mod's enabled state. The demo row writes <c>Plugin.EnableSampleContent.Value</c>;
        /// a data-mod row writes its PlayerPrefs key (then Save). Updates the in-memory row too. No live
        /// re-inject: a change takes effect on the next load. A no-op (with a warning) for an unknown key,
        /// since there is nothing to persist against.
        /// </summary>
        public static void SetEnabled(string key, bool enabled)
        {
            ModEntry entry;
            if (!_byKey.TryGetValue(key, out entry))
            {
                Plugin.Log.LogWarning("ModRegistry.SetEnabled: unknown mod key '" + key + "' (ignored).");
                return;
            }

            entry.Enabled = enabled;

            if (entry.IsBundledDemo)
            {
                Plugin.EnableSampleContent.Value = enabled;
            }
            else
            {
                UnityEngine.PlayerPrefs.SetInt(PrefKeyPrefix + key, enabled ? 1 : 0);
                UnityEngine.PlayerPrefs.Save();
            }
        }
    }
}
