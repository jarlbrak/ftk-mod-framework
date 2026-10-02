using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Encounter-local opportunities. Action and mitigation receipts make owner calculation and
    // peer playback agree without deriving shared outcomes from client-local UI state.
    internal sealed class GuardianThemeState
    {
        private sealed class Guardian
        {
            internal string GuardAction;
            internal string Target;
            internal string Role;
            internal int Weapon;
            internal bool Bond;
            internal bool BondTurnStarted;
            internal bool ChargeArmed;
            internal bool Charged;
            internal bool ChargeTurnStarted;
            internal int ChargePercent;
        }

        private readonly Dictionary<string, Guardian> guardians = new Dictionary<string, Guardian>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> mercyAttempts = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> physicalAttempts = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly HashSet<string> mitigations = new HashSet<string>(StringComparer.Ordinal);

        internal bool BeginGuard(string guardianId, string actionId, string targetId, string role, int weapon,
            bool mercy, int physicalBonusPercent)
        {
            if (string.IsNullOrEmpty(guardianId) || string.IsNullOrEmpty(actionId) ||
                string.IsNullOrEmpty(targetId) || guardianId == targetId || physicalBonusPercent < 0 || physicalBonusPercent > 50)
                return false;
            Guardian guardian;
            if (!guardians.TryGetValue(guardianId, out guardian))
            {
                guardian = new Guardian();
                guardians.Add(guardianId, guardian);
            }
            if (guardian.GuardAction == actionId) return false;
            guardian.GuardAction = actionId;
            guardian.Target = targetId;
            guardian.Role = role;
            guardian.Weapon = weapon;
            guardian.Bond = mercy;
            guardian.BondTurnStarted = false;
            guardian.ChargeArmed = physicalBonusPercent > 0;
            guardian.ChargePercent = physicalBonusPercent;
            return true;
        }

        internal void ObserveEquipment(string guardianId, string role, int weapon)
        {
            Guardian guardian;
            if (guardianId != null && guardians.TryGetValue(guardianId, out guardian) &&
                (guardian.Role != role || guardian.Weapon != weapon)) ResetActor(guardianId);
        }

        internal void LoseTarget(string targetId)
        {
            foreach (Guardian guardian in guardians.Values)
                if (guardian.Target == targetId) guardian.Bond = false;
        }

        internal void ResolveMitigation(string attackId, string targetId, int original, int reduced, string[] active)
        {
            if (string.IsNullOrEmpty(attackId) || string.IsNullOrEmpty(targetId) || original <= 0 ||
                reduced >= original || reduced < 0 || active == null || !mitigations.Add(attackId + "|" + targetId)) return;
            foreach (string id in active)
            {
                Guardian guardian;
                if (id == null || !guardians.TryGetValue(id, out guardian) || guardian.Target != targetId ||
                    !guardian.ChargeArmed) continue;
                guardian.ChargeArmed = false;
                if (!guardian.Charged)
                {
                    guardian.Charged = true;
                    guardian.ChargeTurnStarted = false;
                }
            }
        }

        internal bool BeginMercyAttack(string guardianId, string actionId, bool eligible)
        {
            if (!eligible || string.IsNullOrEmpty(guardianId) || string.IsNullOrEmpty(actionId)) return false;
            string receipt = guardianId + "|" + actionId;
            bool spent;
            if (mercyAttempts.TryGetValue(receipt, out spent)) return spent;
            Guardian guardian;
            spent = guardians.TryGetValue(guardianId, out guardian) && guardian.Bond;
            mercyAttempts.Add(receipt, spent);
            if (spent) guardian.Bond = false;
            return spent;
        }

        internal bool MercyReceipt(string guardianId, string actionId)
        {
            bool spent;
            return guardianId != null && actionId != null &&
                mercyAttempts.TryGetValue(guardianId + "|" + actionId, out spent) && spent;
        }

        internal int BeginPhysicalAttack(string guardianId, string actionId, bool eligible)
        {
            if (!eligible || string.IsNullOrEmpty(guardianId) || string.IsNullOrEmpty(actionId)) return 0;
            string receipt = guardianId + "|" + actionId;
            bool spent;
            Guardian guardian;
            if (physicalAttempts.TryGetValue(receipt, out spent))
                return spent && guardians.TryGetValue(guardianId, out guardian) ? guardian.ChargePercent : 0;
            spent = guardians.TryGetValue(guardianId, out guardian) && guardian.Charged;
            physicalAttempts.Add(receipt, spent);
            if (spent) guardian.Charged = false;
            return spent ? guardian.ChargePercent : 0;
        }

        internal int ChargePercent(string guardianId)
        {
            Guardian guardian;
            return guardianId != null && guardians.TryGetValue(guardianId, out guardian) && guardian.Charged
                ? guardian.ChargePercent : 0;
        }

        internal static int StrongestAttackBonus(bool reckoning, int physicalBonusPercent)
        {
            return Math.Max(reckoning ? 50 : 0, physicalBonusPercent);
        }

        internal bool HasBond(string guardianId)
        {
            Guardian guardian;
            return guardianId != null && guardians.TryGetValue(guardianId, out guardian) && guardian.Bond;
        }

        internal void BeginTurn(string actorId)
        {
            mercyAttempts.Clear();
            physicalAttempts.Clear();
            mitigations.Clear();
            Guardian guardian;
            if (actorId == null || !guardians.TryGetValue(actorId, out guardian)) return;
            if (guardian.Bond) guardian.BondTurnStarted = true;
            if (guardian.Charged) guardian.ChargeTurnStarted = true;
        }

        internal void EndTurn(string actorId)
        {
            Guardian guardian;
            if (actorId == null || !guardians.TryGetValue(actorId, out guardian)) return;
            if (guardian.BondTurnStarted) guardian.Bond = false;
            if (guardian.ChargeTurnStarted) guardian.Charged = false;
        }

        internal void ResetActor(string actorId)
        {
            if (actorId != null) guardians.Remove(actorId);
            if (guardians.Count == 0)
            {
                mercyAttempts.Clear();
                physicalAttempts.Clear();
                mitigations.Clear();
            }
        }

        internal bool IsEmpty { get { return guardians.Count == 0 && mercyAttempts.Count == 0 &&
            physicalAttempts.Count == 0 && mitigations.Count == 0; } }
    }
}
