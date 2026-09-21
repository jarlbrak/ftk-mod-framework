using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Encounter-local rules only. The integration owns authority, actor validation, and lifecycle
    // ordering: expire a guardian before resolving any damage after its turn or incapacitation.
    internal sealed class GuardianCombatState
    {
        private sealed class Guardian
        {
            internal string Target;
            internal bool Active;
            internal bool RescueUsed;
            internal readonly HashSet<string> HealedAttacks = new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, int> GuardHealHealth = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        internal sealed class DamageResult
        {
            internal readonly int Damage;
            internal readonly int RemainingHealth;
            internal readonly bool Guarded;
            internal readonly string Rescuer;

            internal DamageResult(int damage, int health, bool guarded, string rescuer)
            {
                Damage = damage;
                RemainingHealth = health;
                Guarded = guarded;
                Rescuer = rescuer;
            }
        }

        private readonly Dictionary<string, Guardian> guardians =
            new Dictionary<string, Guardian>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, DamageResult>> attacks =
            new Dictionary<string, Dictionary<string, DamageResult>>(StringComparer.Ordinal);

        // One native action per turn. The caller supplies a stable attack identity and resets receipts
        // at the next turn boundary, so repeated playback never halves twice or spends another rescue.
        internal void BeginTurn()
        {
            attacks.Clear();
        }

        internal bool TryResolveAttackDamage(string attackId, string targetId, int currentHealth, int damage,
            bool allowRescue, out DamageResult result)
        {
            result = null;
            if (!Valid(attackId) || !Valid(targetId)) return false;
            Dictionary<string, DamageResult> victims;
            if (attacks.TryGetValue(attackId, out victims) && victims.TryGetValue(targetId, out result)) return true;
            if (!TryResolveDirectDamage(targetId, currentHealth, damage, allowRescue, out result)) return false;
            if (victims == null)
            {
                victims = new Dictionary<string, DamageResult>(StringComparer.Ordinal);
                attacks.Add(attackId, victims);
            }
            victims.Add(targetId, result);
            return true;
        }

        internal bool TryGuard(string guardianId, string targetId, bool guardianCanAct, bool targetAlive)
        {
            if (!Valid(guardianId) || !Valid(targetId) || guardianId == targetId ||
                !guardianCanAct || !targetAlive) return false;
            Guardian guardian;
            if (!guardians.TryGetValue(guardianId, out guardian))
            {
                guardian = new Guardian();
                guardians.Add(guardianId, guardian);
            }
            guardian.Target = targetId;
            guardian.Active = true;
            return true;
        }

        internal string DesignatedAlly(string guardianId)
        {
            Guardian guardian;
            return Valid(guardianId) && guardians.TryGetValue(guardianId, out guardian) ? guardian.Target : null;
        }

        internal bool IsActive(string guardianId)
        {
            Guardian guardian;
            return Valid(guardianId) && guardians.TryGetValue(guardianId, out guardian) && guardian.Active;
        }

        internal bool RescueAvailable(string guardianId)
        {
            Guardian guardian;
            return Valid(guardianId) && (!guardians.TryGetValue(guardianId, out guardian) || !guardian.RescueUsed);
        }

        internal string[] ActiveGuardians(string targetId)
        {
            List<string> result = new List<string>();
            if (Valid(targetId))
                foreach (KeyValuePair<string, Guardian> pair in guardians)
                    if (pair.Value.Active && pair.Value.Target == targetId) result.Add(pair.Key);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        internal int ResolveGuardHealingHealth(string guardianId, string actionId, string targetId,
            int currentHealth, int maxHealth, int percent)
        {
            Guardian guardian;
            if (!Valid(guardianId) || !Valid(actionId) || !Valid(targetId) || percent < 0 || percent > 20 ||
                !guardians.TryGetValue(guardianId, out guardian) || !guardian.Active || guardian.Target != targetId ||
                currentHealth <= 0 || maxHealth < currentHealth) return currentHealth;
            int health;
            if (guardian.GuardHealHealth.TryGetValue(actionId, out health)) return health;
            int amount = GuardianEquipmentBonuses.HealAmount(currentHealth, maxHealth, percent);
            // Starting health is below 50: a positive novice perk must still restore one HP.
            if (percent > 0 && currentHealth < maxHealth) amount = Math.Max(1, amount);
            health = currentHealth + amount;
            guardian.GuardHealHealth.Add(actionId, health);
            return health;
        }

        // Turn start and incapacitation both remove protection without forgetting the chosen ally.
        internal void ExpireGuard(string guardianId)
        {
            Guardian guardian;
            if (Valid(guardianId) && guardians.TryGetValue(guardianId, out guardian)) guardian.Active = false;
        }

        // Call only for direct attacks, after vanilla mitigation. DoT must bypass this method.
        // Invalid input returns false and cannot consume a rescue charge.
        internal bool TryResolveDirectDamage(string targetId, int currentHealth, int damage, out DamageResult result)
        {
            return TryResolveDirectDamage(targetId, currentHealth, damage, true, out result);
        }

        internal bool TryResolveDirectDamage(string targetId, int currentHealth, int damage, bool allowRescue, out DamageResult result)
        {
            result = null;
            if (!Valid(targetId) || currentHealth <= 0 || damage < 0) return false;
            bool guarded = false;
            string rescuer = null;
            foreach (KeyValuePair<string, Guardian> pair in guardians)
            {
                Guardian guardian = pair.Value;
                if (!guardian.Active || guardian.Target != targetId) continue;
                guarded = true;
                if (!guardian.RescueUsed && (rescuer == null || StringComparer.Ordinal.Compare(pair.Key, rescuer) < 0))
                    rescuer = pair.Key;
            }
            int retained = guarded ? damage / 2 + damage % 2 : damage;
            if (allowRescue && rescuer != null && retained >= currentHealth)
            {
                guardians[rescuer].RescueUsed = true;
                result = new DamageResult(currentHealth - 1, 1, true, rescuer);
            }
            else result = new DamageResult(retained, Math.Max(0, currentHealth - retained), guarded, null);
            return true;
        }

        // The committed attack identity must be unique within this encounter. A qualifying hit at
        // full health still consumes that attack's healing opportunity, preventing later splash reuse.
        internal int ResolveFocusedHitHealing(string guardianId, string attackId, string targetId,
            int currentHealth, int maxHealth, bool guardianCanAct, bool focusSpent, bool landed)
        {
            return ResolveFocusedHitHealing(guardianId, attackId, targetId, currentHealth, maxHealth,
                guardianCanAct, focusSpent, landed, 0);
        }

        internal int ResolveFocusedHitHealing(string guardianId, string attackId, string targetId,
            int currentHealth, int maxHealth, bool guardianCanAct, bool focusSpent, bool landed, int bonusPercent)
        {
            Guardian guardian;
            if (!Valid(guardianId) || !Valid(attackId) || !Valid(targetId) || guardianId == targetId ||
                !guardianCanAct || !focusSpent || !landed || currentHealth <= 0 || maxHealth < currentHealth || bonusPercent < 0 || bonusPercent > 20 ||
                !guardians.TryGetValue(guardianId, out guardian) || guardian.Target != targetId) return 0;
            if (!guardian.HealedAttacks.Add(attackId)) return 0;
            return GuardianEquipmentBonuses.HealAmount(currentHealth, maxHealth, 8 + bonusPercent);
        }

        internal void ResetEncounter()
        {
            guardians.Clear();
            attacks.Clear();
        }

        // Native actor reset also runs on resurrection within the same combat. Keep the
        // encounter's charge, designation and replay receipts while ending active protection.
        internal void ResetActor(string guardianId)
        {
            ExpireGuard(guardianId);
        }

        internal void ResetGuardian(string guardianId)
        {
            if (Valid(guardianId)) guardians.Remove(guardianId);
        }

        private static bool Valid(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Trim().Length != 0;
        }
    }
}
