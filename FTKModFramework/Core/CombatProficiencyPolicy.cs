using System;

namespace FTKModFramework.Core
{
    internal static class CombatProficiencyPolicy
    {
        // A fixed integer mixer avoids process-dependent hashes and never advances native RNG.
        // The serialized timeline entry identifies one action; targets do not affect its branch.
        internal static int DebuffBranch(int seed, int encounter, int entry, int turnIndex, int photonId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, seed); hash = Mix(hash, encounter); hash = Mix(hash, entry);
                hash = Mix(hash, turnIndex); hash = Mix(hash, photonId);
                hash ^= hash >> 16; hash *= 0x7feb352du;
                hash ^= hash >> 15; hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (int)(hash & 1u);
            }
        }

        private static uint Mix(uint hash, int value)
        {
            unchecked { return (hash ^ (uint)value) * 16777619u; }
        }

        internal static bool ValidMultiplier(float multiplier)
        {
            return !float.IsNaN(multiplier) && !float.IsInfinity(multiplier) && multiplier > 1f && multiplier <= 16f;
        }

        internal static bool Qualifies(int source, int count, float value, int[] sources)
        {
            if (count <= 0 || !(value <= -1) || float.IsInfinity(value) || sources == null) return false;
            foreach (int candidate in sources) if (candidate == source) return true;
            return false;
        }
    }
}
