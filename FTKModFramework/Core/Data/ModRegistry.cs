using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// One row in the <see cref="ModRegistry"/>: a single mod's identity and current enabled state.
    /// Description, author, and version are display metadata only and never affect identity or gating.
    /// </summary>
    internal sealed class ModEntry
    {
        /// <summary>Stable identity. For data mods this is the RAW <c>Manifest.ModGuid</c> (not sanitized);
        /// it is also the PlayerPrefs sub-key for data mods.</summary>
        public readonly string Key;

        /// <summary>Human-readable name for the UI from the mod manifest.</summary>
        public readonly string DisplayName;

        /// <summary>Optional mod version for the UI. Not used for gating.</summary>
        public readonly string Version;

        /// <summary>Optional player-facing explanation of the mod's content.</summary>
        public readonly string Description;

        /// <summary>Optional author credit shown beside the description.</summary>
        public readonly string Author;
        public readonly string FrameworkVersion;
        public readonly string CompatibilityReason;
        public bool FrameworkCompatible { get { return CompatibilityReason == null; } }

        /// <summary>Immutable enabled snapshot for this process. Pending changes never alter loaded state.</summary>
        public readonly bool Enabled;
        public bool? PendingEnabled;
        public bool IsManaged { get; private set; }
        public string PackageId { get; private set; }

        internal void MarkManaged(string packageId) { IsManaged = true; PackageId = packageId; }

        public ModEntry(string key, string displayName, string version, bool enabled, string description, string author, string frameworkVersion = null, bool requiresDeclaration = false)
        {
            Key = key;
            DisplayName = displayName;
            Version = version;
            Enabled = enabled;
            Description = description;
            Author = author;
            FrameworkVersion = frameworkVersion;
            CompatibilityReason = requiresDeclaration ? ModFrameworkCompatibility.Reason(frameworkVersion, Plugin.Version) : null;
        }
    }

    /// <summary>
    /// The single in-memory source of truth for which mods exist and whether each is enabled. It is a flat,
    /// ordered list of <see cref="ModEntry"/> rows, built EXACTLY ONCE per process during the
    /// <c>TableManager.Initialize</c> load path. <c>ContentLoader.Load</c> registers each discovered data
    /// mod (keyed <c>Manifest.ModGuid</c>) before gating its files.
    /// There is no second filesystem discovery pass: the registry only records what discovery already found.
    ///
    /// Ordering follows <c>ModDiscovery</c>'s <c>(modGuid, folder)</c> order. <see cref="Entries"/>
    /// returns ALL rows including disabled ones (the UI needs to show what it can re-enable).
    ///
    /// Data-mod rows read/write <c>PlayerPrefs</c> under <see cref="PrefKeyPrefix"/> + Key. A missing
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

        internal sealed class Snapshot
        {
            private readonly ModEntry[] entries;
            internal Snapshot() { entries = _entries.ToArray(); }
            internal void Restore()
            {
                _entries.Clear();
                _byKey.Clear();
                foreach (ModEntry entry in entries) { _entries.Add(entry); _byKey.Add(entry.Key, entry); }
            }
        }

        internal static Snapshot Capture() { return new Snapshot(); }

        /// <summary>All registered mods including disabled ones, in registration order. Read-only.</summary>
        public static ReadOnlyCollection<ModEntry> Entries
        {
            get { return _entries.AsReadOnly(); }
        }

        /// <summary>
        /// Register a mod row, returning the live entry. Idempotent on <paramref name="key"/>: a repeat call
        /// returns the EXISTING entry untouched (so a second TableManager.Initialize pass cannot duplicate or
        /// reset rows). On first registration the enabled state is seeded from PlayerPrefs, defaulting
        /// to <paramref name="defaultEnabled"/> when absent.
        /// </summary>
        public static ModEntry Register(string key, string displayName, string version, bool defaultEnabled,
            string description, string author, string frameworkVersion = null, bool requiresDeclaration = false)
        {
            ModEntry existing;
            if (_byKey.TryGetValue(key, out existing)) return existing; // idempotent: never re-seed.

            bool enabled = UnityEngine.PlayerPrefs.GetInt(PrefKeyPrefix + key, defaultEnabled ? 1 : 0) != 0;

            string name = (displayName == null || displayName.Trim().Length == 0) ? key : displayName;
            // Version is UI-only metadata and is not part of the gating contract.
            ModEntry entry = new ModEntry(key, name, version, enabled, description, author, frameworkVersion, requiresDeclaration);
            _entries.Add(entry);
            _byKey[key] = entry;
            return entry;
        }

        /// <summary>
        /// Registration path selection for a discovered mod. Only a folder inside the active generation's
        /// content root whose GUID the generation lock lists takes the managed path (enabled state from the
        /// lock, toggles routed through the marketplace). A manual copy of the same GUID, a managed-root folder
        /// the lock does not know, or discovery without an active generation all take the manual path. The
        /// separator suffix keeps a sibling directory sharing the root's prefix from counting as managed.
        /// </summary>
        internal static ModEntry RegisterDiscovered(ModManifest manifest, Marketplace.ManagedSnapshot managed, Marketplace.PackageDescriptor package)
        {
            if (package != null && managed != null &&
                manifest.FolderPath.StartsWith(managed.ContentRoot + System.IO.Path.DirectorySeparatorChar, System.StringComparison.Ordinal))
                return RegisterManaged(manifest, package);
            return Register(manifest.ModGuid, manifest.Name, manifest.Version, true,
                manifest.Description, manifest.Author, manifest.FrameworkVersion, true);
        }

        internal static ModEntry RegisterManaged(ModManifest manifest, Marketplace.PackageDescriptor package)
        {
            ModEntry existing;
            if (_byKey.TryGetValue(manifest.ModGuid, out existing)) return existing;
            ModEntry entry = new ModEntry(manifest.ModGuid, manifest.Name, manifest.Version,
                package.Enabled, package.Description ?? manifest.Description, package.Author ?? manifest.Author, manifest.FrameworkVersion, true);
            entry.MarkManaged(package.PackageId);
            _entries.Add(entry);
            _byKey[entry.Key] = entry;
            return entry;
        }

        /// <summary>
        /// Gating read used at load time. Returns the registered row's <c>Enabled</c> state, or TRUE for an
        /// unknown key (fail-open): a key the registry never saw must not be silently dropped.
        /// </summary>
        internal static string CompatibilityReasonFor(string key, string version)
        {
            ModEntry entry;
            return _byKey.TryGetValue(key, out entry) && entry.Version == version ? entry.CompatibilityReason : null;
        }

        public static bool IsEnabled(string key)
        {
            ModEntry entry;
            if (_byKey.TryGetValue(key, out entry)) return entry.Enabled && entry.FrameworkCompatible;
            return true; // fail-open (FR-3): unknown key is treated as enabled.
        }

        /// <summary>
        /// Set and persist a mod's enabled state to PlayerPrefs. Only PendingEnabled changes in memory. No live
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

            if (enabled && !entry.FrameworkCompatible)
            {
                Plugin.Log.LogWarning("Cannot enable '" + key + "': " + entry.CompatibilityReason);
                return;
            }
            if (entry.IsManaged)
            {
                Plugin.Log.LogWarning("Managed selections must be prepared through the marketplace confirmation flow.");
                return;
            }
            entry.PendingEnabled = enabled == entry.Enabled ? (bool?)null : enabled;

            UnityEngine.PlayerPrefs.SetInt(PrefKeyPrefix + key, enabled ? 1 : 0);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
