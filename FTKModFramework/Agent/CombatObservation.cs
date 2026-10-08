using System;
using System.Collections;

namespace FTKModFramework.Agent
{
    internal static class CombatObservation
    {
        internal static bool IsActive(bool clientActive, bool masterActive, string clientEncounterType,
            IDictionary enemies, IDictionary dummies, object timelineHead)
        {
            if (clientActive) return true;
            // Native reveal battles initialize the timeline and dummies without setting the client flag.
            // Ready also sets the master flag, so require the actual revealed enemy population.
            if (!masterActive || clientEncounterType != "Enemy" || enemies == null || enemies.Count == 0 ||
                dummies == null || timelineHead == null || !dummies.Contains(timelineHead) || dummies[timelineHead] == null) return false;
            foreach (DictionaryEntry enemy in enemies)
                if (enemy.Value == null || !dummies.Contains(enemy.Key) ||
                    !ReferenceEquals(enemy.Value, dummies[enemy.Key])) return false;
            return true;
        }
    }
}
