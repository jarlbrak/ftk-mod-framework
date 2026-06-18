using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Deterministic synthetic-ID allocator.
    ///
    /// FTK content tables key rows by an FTK_*.ID enum (an int). Vanilla values are small
    /// (e.g. items start at 100000). To add content we mint NEW ints, but those ints get
    /// written into save files AND must match across every Photon client in a co-op game.
    /// So the mapping (modGuid + contentKey) -> int must be identical on every machine,
    /// regardless of mod load order. We achieve that with a pure hash (FNV-1a) into a high
    /// band, far above any vanilla enum value, with deterministic linear probing on the
    /// (astronomically rare) collision.
    /// </summary>
    public static class IdAllocator
    {
        // 0x40000000 = 1,073,741,824. Well above every vanilla FTK_*.ID value, still a
        // positive int, leaving a ~536M-wide band for custom content.
        private const int Band = 0x40000000;
        private const int Span = 0x1FFFFFFF;

        private static readonly Dictionary<string, int> KeyToInt = new Dictionary<string, int>();
        private static readonly Dictionary<int, string> IntToKey = new Dictionary<int, string>();

        /// <summary>Stable int for a given (modGuid, contentKey). Idempotent.</summary>
        public static int Allocate(string modGuid, string contentKey)
        {
            string key = modGuid + ":" + contentKey;
            int existing;
            if (KeyToInt.TryGetValue(key, out existing)) return existing;

            uint h = Fnv1a(key);
            int candidate = Band + (int)(h & (uint)Span);
            while (IntToKey.ContainsKey(candidate))
                candidate = Band + (((candidate - Band) + 1) & Span);

            KeyToInt[key] = candidate;
            IntToKey[candidate] = key;
            return candidate;
        }

        /// <summary>True if an int was minted by this framework (i.e. is in the custom band).</summary>
        public static bool IsCustom(int id)
        {
            return id >= Band;
        }

        /// <summary>
        /// Count of distinct high-band synthetic ids minted so far. Every id this allocator hands out is
        /// in the custom band (see <see cref="Allocate"/>), so this is exactly the high-band row count.
        /// Positional class ids are EXCLUDED by construction: classes register through
        /// <see cref="ContentRegistry.Register"/> with an explicit id == array index and never call
        /// <see cref="Allocate"/>, so they never enter this map. Read by the scale-budget save-size proxy.
        /// </summary>
        public static int CustomIdCount
        {
            get { return KeyToInt.Count; }
        }

        private static uint Fnv1a(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= (byte)s[i];
                h *= 16777619u;
            }
            return h;
        }
    }
}
