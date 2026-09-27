namespace FTKModFramework.Core
{
    /// <summary>Persistence for player choices, keyed by tweak ID. The runtime binds this to the
    /// [Tweaks] config section; tests use an in-memory store. Keys for retired IDs are never read,
    /// so they stay in the file untouched.</summary>
    internal interface ITweakPreferenceStore
    {
        TweakPreference Read(string id);
        void Write(string id, TweakPreference preference);
    }

    /// <summary>Pure preference resolution shared by the registry and the Tweaks tab.</summary>
    internal static class TweakPreferences
    {
        /// <summary>Only the exact On and Off values are choices. BepInEx parses config values with
        /// case-insensitive Enum.Parse, which also accepts numbers and comma-combined names such
        /// as "7" or "On, Off"; those must not be read as a choice.</summary>
        internal static bool IsExplicit(TweakPreference stored)
        {
            return stored == TweakPreference.On || stored == TweakPreference.Off;
        }

        internal static TweakPreference Normalize(TweakPreference stored)
        {
            return IsExplicit(stored) ? stored : TweakPreference.Default;
        }

        internal static bool Resolve(TweakPreference stored, bool defaultOn)
        {
            if (stored == TweakPreference.On) return true;
            if (stored == TweakPreference.Off) return false;
            return defaultOn;
        }

        /// <summary>The value to store after flipping the effective state. Landing on the default
        /// stores Default, so toggling twice always returns a row to following its default.</summary>
        internal static TweakPreference Toggle(TweakPreference stored, bool defaultOn)
        {
            bool next = !Resolve(stored, defaultOn);
            if (next == defaultOn) return TweakPreference.Default;
            return next ? TweakPreference.On : TweakPreference.Off;
        }
    }
}
