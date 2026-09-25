using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class CombatProficiencyRegistry
    {
        internal sealed class ResistanceBonus
        {
            internal readonly int[] Sources;
            internal readonly float Multiplier;
            internal ResistanceBonus(int[] sources, float multiplier)
            { Sources = (int[])sources.Clone(); Multiplier = multiplier; }
        }

        private static Dictionary<int, int[]> random = new Dictionary<int, int[]>();
        private static Dictionary<int, ResistanceBonus> bonuses = new Dictionary<int, ResistanceBonus>();
        internal static int Count { get { return random.Count + bonuses.Count; } }
        internal static bool HasRandom(int id) { return random.ContainsKey(id); }
        internal static bool HasBonus(int id) { return bonuses.ContainsKey(id); }
        internal static void SetRandom(int id, int[] outcomes) { random[id] = (int[])outcomes.Clone(); }
        internal static void SetBonus(int id, int[] sources, float multiplier)
        { bonuses[id] = new ResistanceBonus(sources, multiplier); }
        internal static bool TryRandom(int id, out int[] outcomes) { return random.TryGetValue(id, out outcomes); }
        internal static bool TryBonus(int id, out ResistanceBonus bonus) { return bonuses.TryGetValue(id, out bonus); }

        internal static Action SuspendForReload()
        {
            Dictionary<int, int[]> oldRandom = random;
            Dictionary<int, ResistanceBonus> oldBonuses = bonuses;
            random = new Dictionary<int, int[]>(); bonuses = new Dictionary<int, ResistanceBonus>();
            return delegate { random = oldRandom; bonuses = oldBonuses; };
        }
    }
}
