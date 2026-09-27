using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace FTKModFramework.Core
{
    /// <summary>Tweak preferences in the [Tweaks] section of the framework config, one entry per
    /// registered tweak ID. Deliberately thin: normalization, defaults and toggling stay in the
    /// tested TweakRegistry. A value BepInEx cannot parse is logged by BepInEx and read as Default,
    /// so nothing is logged here. Keys for retired IDs are never bound, so they are left as they are.</summary>
    internal sealed class TweakConfigStore : ITweakPreferenceStore
    {
        internal const string Section = "Tweaks";

        private readonly ConfigFile _config;
        private readonly TweakRegistry _registry;
        private readonly Dictionary<string, ConfigEntry<TweakPreference>> _entries =
            new Dictionary<string, ConfigEntry<TweakPreference>>(StringComparer.Ordinal);

        /// <param name="registry">Supplies each tweak's title and summary for the config comment.</param>
        internal TweakConfigStore(ConfigFile config, TweakRegistry registry)
        {
            _config = config;
            _registry = registry;
        }

        public TweakPreference Read(string id)
        {
            return Entry(id).Value;
        }

        /// <summary>Saves immediately, matching the framework toggles in the Mods panel.</summary>
        public void Write(string id, TweakPreference preference)
        {
            Entry(id).Value = preference;
            _config.Save();
        }

        private ConfigEntry<TweakPreference> Entry(string id)
        {
            ConfigEntry<TweakPreference> entry;
            if (!_entries.TryGetValue(id, out entry))
            {
                entry = _config.Bind(Section, id, TweakPreference.Default, Describe(id));
                _entries.Add(id, entry);
            }
            return entry;
        }

        private string Describe(string id)
        {
            int handle;
            TweakDescriptor descriptor = _registry != null && _registry.TryGetHandle(id, out handle)
                ? _registry.Get(handle) : null;
            string follows = "Default follows the framework's current default for this tweak";
            if (descriptor == null) return follows + ". On and Off are explicit choices.";
            return descriptor.Title + ". " + descriptor.Summary + " " + follows
                + " (" + (descriptor.DefaultOn ? "On" : "Off") + "). On and Off are explicit choices.";
        }
    }
}
