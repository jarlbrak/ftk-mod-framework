using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class PlayerRaceSelection
    {
        // Native choices retain their order; custom identities have a stable order independent of load order.
        internal static int Next(int current, bool backwards, IList<int> native, IList<int> custom)
        {
            List<int> choices = new List<int>(native);
            List<int> sorted = new List<int>(custom);
            sorted.Sort();
            foreach (int id in sorted) if (!choices.Contains(id)) choices.Add(id);
            if (choices.Count == 0) return current;
            int index = choices.IndexOf(current);
            if (index < 0) return backwards ? choices[choices.Count - 1] : choices[0];
            return choices[(index + (backwards ? choices.Count - 1 : 1)) % choices.Count];
        }

        internal static int UnprobedId(string modGuid, string key)
        {
            uint hash = 2166136261u;
            foreach (char c in modGuid + ":" + key) { hash ^= (byte)c; hash *= 16777619u; }
            return 0x40000000 + (int)(hash & 0x1FFFFFFF);
        }
    }
}
