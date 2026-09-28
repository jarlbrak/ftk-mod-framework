using System;

namespace FTKModFramework.Core
{
    /// <summary>The saved poison countdown of fix.poison-decay-resume (Spec #260 FR-1, FR-2).
    /// CharacterStats.m_PoisonTimeCounter is private, unsynced and unsaved, so a loaded character
    /// restarts it at 0 and suffers up to two extra poisoned end turns per level. The value lives
    /// under <see cref="Key"/> in the CharacterStats state dictionary as a plain string, so vanilla's
    /// serializer never meets a framework type: "v1:1" or "v1:2". Unity-free, so the save and load
    /// hooks stay thin and testable.</summary>
    internal static class PoisonCountdown
    {
        /// <summary>Contains a dot, so it cannot collide with a serialized C# field name.</summary>
        internal const string Key = "ftkmf.poison";
        internal const string VersionPrefix = "v1:";

        /// <summary>The only counters worth keeping. 0 is what a load restores anyway, and
        /// CharacterStats.EndTurnActionSequence resets the counter in the same end turn it reaches
        /// PoisonTimeRounds (3).</summary>
        internal const int MinCounter = 1;
        internal const int MaxCounter = 2;

        /// <summary>"v1:1" or "v1:2"; null for any other counter, so nothing is written.</summary>
        internal static string Encode(int counter)
        {
            return counter >= MinCounter && counter <= MaxCounter ? VersionPrefix + counter : null;
        }

        /// <summary>1 or 2 for a value <see cref="Encode"/> could have written, otherwise 0. The length
        /// is checked first, so an oversized or foreign value is rejected without further work.</summary>
        internal static int Decode(string value)
        {
            if (value == null || value.Length != VersionPrefix.Length + 1
                || !value.StartsWith(VersionPrefix, StringComparison.Ordinal)) return 0;
            int counter = value[VersionPrefix.Length] - '0';
            return counter >= MinCounter && counter <= MaxCounter ? counter : 0;
        }

        /// <summary>Decodes the value found under <see cref="Key"/> in a parsed state. The framework
        /// only ever writes a string, so any other type decodes as 0.</summary>
        internal static int FromStateValue(object value)
        {
            return Decode(value as string);
        }

        /// <summary>True when the serialized state could carry the key. The load hook checks this
        /// before parsing, so a state without it is never parsed a second time.</summary>
        internal static bool MayCarry(string content)
        {
            return content != null && content.IndexOf(Key, StringComparison.Ordinal) >= 0;
        }

        /// <summary>The save hook's decision (FR-1): the value to add under <see cref="Key"/>, or null
        /// to write nothing so the state matches vanilla. Only a locked run with the tweak on and a
        /// poisoned character mid-countdown has anything to keep.</summary>
        internal static string PoisonToWrite(bool locked, bool on, int level, int counter)
        {
            if (!locked || !on || level <= 0) return null;
            return Encode(counter);
        }

        /// <summary>The load hook's decision (FR-2) for one CharacterStats state that carried the
        /// key, after vanilla deserialized it: the counter to set, or 0 to keep vanilla's 0. The
        /// characters are created after the lock, so the run must already be locked.</summary>
        /// <param name="windowOpen">A resume's players are being created.</param>
        /// <param name="isSerialize">FTKNetworkObject.m_IsSerialize; vanilla read nothing otherwise.</param>
        /// <param name="recorded">The decoded value, 0 when it was unreadable.</param>
        /// <param name="level">m_PoisonLvl as vanilla just restored it.</param>
        internal static int CounterToRestore(bool windowOpen, bool locked, bool on, bool isSerialize, int recorded, int level)
        {
            if (!windowOpen || !locked || !on || !isSerialize) return 0;
            if (recorded < MinCounter || recorded > MaxCounter || level <= 0) return 0;
            return recorded;
        }
    }
}
