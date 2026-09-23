using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Encounter-local receipts. The integration supplies the synchronized native turn identity,
    // equipped item identities and the active Guard snapshot before any victim is incapacitated.
    internal sealed class GuardianLegendaryState
    {
        private sealed class Guardian
        {
            internal string GuardAction;
            internal string Target;
            internal int FocusItem;
            internal int ReckoningItem;
            internal bool FocusArmed;
            internal bool ReckoningArmed;
            internal bool Charged;
            internal bool ChargeTurnStarted;
        }

        private readonly Dictionary<string, Guardian> guardians = new Dictionary<string, Guardian>(StringComparer.Ordinal);
        private readonly HashSet<string> mitigations = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> attempts = new Dictionary<string, bool>(StringComparer.Ordinal);

        internal bool BeginGuard(string guardianId, string actionId, string targetId, int focusItem, int reckoningItem)
        {
            if (string.IsNullOrEmpty(guardianId) || string.IsNullOrEmpty(actionId) ||
                string.IsNullOrEmpty(targetId) || guardianId == targetId) return false;
            Guardian guardian;
            if (!guardians.TryGetValue(guardianId, out guardian))
            {
                guardian = new Guardian();
                guardians.Add(guardianId, guardian);
            }
            if (guardian.GuardAction == actionId) return false;
            ObserveEquipment(guardianId, focusItem, reckoningItem);
            guardian.GuardAction = actionId;
            guardian.Target = targetId;
            guardian.FocusItem = focusItem;
            guardian.ReckoningItem = reckoningItem;
            guardian.FocusArmed = focusItem >= 0;
            guardian.ReckoningArmed = reckoningItem >= 0;
            return true;
        }

        internal void ObserveEquipment(string guardianId, int focusItem, int reckoningItem)
        {
            Guardian guardian;
            if (guardianId == null || !guardians.TryGetValue(guardianId, out guardian)) return;
            if (guardian.FocusItem != focusItem) guardian.FocusArmed = false;
            if (guardian.ReckoningItem != reckoningItem)
            {
                guardian.ReckoningArmed = false;
                guardian.Charged = false;
            }
        }

        // Every qualifying guardian spends its first-hit opportunity, but one guarded outcome
        // restores at most one Focus. Ordering of callers and dictionary iteration cannot stack it.
        internal bool ResolveMitigation(string attackId, string targetId, int originalDamage,
            int guardedDamage, string[] activeGuardians)
        {
            if (string.IsNullOrEmpty(attackId) || string.IsNullOrEmpty(targetId) ||
                originalDamage <= 0 || guardedDamage < 0 || guardedDamage >= originalDamage ||
                activeGuardians == null || !mitigations.Add(attackId + "|" + targetId)) return false;
            bool focus = false;
            foreach (string id in activeGuardians)
            {
                Guardian guardian;
                if (id == null || !guardians.TryGetValue(id, out guardian) || guardian.Target != targetId) continue;
                if (guardian.FocusArmed)
                {
                    focus = true;
                    guardian.FocusArmed = false;
                }
                if (guardian.ReckoningArmed)
                {
                    guardian.ReckoningArmed = false;
                    if (!guardian.Charged)
                    {
                        guardian.Charged = true;
                        guardian.ChargeTurnStarted = false;
                    }
                }
            }
            return focus;
        }

        internal bool IsCharged(string guardianId)
        {
            Guardian guardian;
            return guardianId != null && guardians.TryGetValue(guardianId, out guardian) && guardian.Charged;
        }

        internal bool BeginAttack(string guardianId, string actionId, bool eligible)
        {
            if (!eligible || string.IsNullOrEmpty(guardianId) || string.IsNullOrEmpty(actionId)) return false;
            string receipt = guardianId + "|" + actionId;
            bool charged;
            if (attempts.TryGetValue(receipt, out charged)) return charged;
            Guardian guardian;
            charged = guardians.TryGetValue(guardianId, out guardian) && guardian.Charged;
            attempts.Add(receipt, charged);
            if (charged) guardian.Charged = false;
            return charged;
        }

        internal void BeginTurn(string actorId)
        {
            mitigations.Clear();
            attempts.Clear();
            Guardian guardian;
            if (actorId != null && guardians.TryGetValue(actorId, out guardian) && guardian.Charged)
                guardian.ChargeTurnStarted = true;
        }

        internal void EndTurn(string actorId)
        {
            Guardian guardian;
            if (actorId != null && guardians.TryGetValue(actorId, out guardian) && guardian.ChargeTurnStarted)
                guardian.Charged = false;
        }

        internal void ResetActor(string actorId)
        {
            if (actorId != null) guardians.Remove(actorId);
        }

        internal static int FocusGain(int current, int maximum)
        {
            return current >= 0 && maximum > current ? 1 : 0;
        }

        // Curse is one removable active curse, not a permanent campaign penalty. Poison is one
        // stacked condition. The integration uses native removal/recalculation APIs for each.
        internal static int CleanseChoice(bool stunned, bool dazed, bool cursed, bool poisoned)
        {
            return stunned ? 1 : dazed ? 2 : cursed ? 3 : poisoned ? 4 : 0;
        }
    }
}
