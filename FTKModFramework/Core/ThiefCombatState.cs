using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Pure, encounter-local rules for a Thief's opening, Prepared, Twin Feint, and Slip Away state.
    /// Native integration owns combat identities, attack receipts, damage application, and synchronization.
    /// This type has no Unity or Photon dependency so the rule contract can be exercised directly.
    /// </summary>
    internal sealed class ThiefCombatState
    {
        internal struct AttackCommit
        {
            internal readonly bool EligibleForSneakAttack;
            internal readonly int BonusPercent;
            internal readonly bool LastLight;

            internal AttackCommit(bool eligible, bool lastLight)
            {
                EligibleForSneakAttack = eligible;
                LastLight = eligible && lastLight;
                BonusPercent = eligible ? (lastLight ? 75 : 35) : 0;
            }
        }

        private sealed class Enemy
        {
            internal bool BeganTurn;
            internal readonly HashSet<string> Contributors = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class Actor
        {
            internal int Turn;
            internal bool SneakAttemptUsed;
            internal bool TwinFeintUsed;
            internal bool Prepared;
            internal int PreparedExpiresAfterTurn;
            internal bool SlipAwayUsed;
            internal bool SlipAwayArmed;
            internal string SlipAwayAttack;
            internal bool LastLightUsed;
            internal bool EvasionArmed;
        }

        private readonly Dictionary<string, Enemy> enemies = new Dictionary<string, Enemy>(StringComparer.Ordinal);
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>(StringComparer.Ordinal);

        internal void BeginEncounter(IEnumerable<string> enemyIds)
        {
            enemies.Clear();
            actors.Clear();
            if (enemyIds == null) return;
            foreach (string id in enemyIds)
                if (!string.IsNullOrEmpty(id) && !enemies.ContainsKey(id)) enemies.Add(id, new Enemy());
        }

        internal void BeginActorTurn(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor == null) return;
            actor.Turn++;
            actor.SneakAttemptUsed = false;
            actor.TwinFeintUsed = false;
            // Slip Away protects only through the enemy response before this scheduled turn.
            // Prepared deliberately survives this boundary and expires at this turn's end.
            actor.SlipAwayArmed = false;
            actor.SlipAwayAttack = null;
            actor.EvasionArmed = false;
        }

        internal void EndActorTurn(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor == null) return;
            if (actor.Prepared && actor.PreparedExpiresAfterTurn <= actor.Turn) actor.Prepared = false;
        }

        internal void BeginEnemyTurn(string enemyId)
        {
            Enemy enemy;
            if (string.IsNullOrEmpty(enemyId)) return;
            enemy = EnsureEnemy(enemyId);
            enemy.BeganTurn = true;
            enemy.Contributors.Clear();
        }

        internal void ResetEnemy(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            enemies[enemyId] = new Enemy { BeganTurn = true };
        }

        internal bool RecordPositiveDirectDamage(string actorId, string enemyId)
        {
            Enemy enemy;
            if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(enemyId)) return false;
            enemy = EnsureEnemy(enemyId);
            return enemy.Contributors.Add(actorId);
        }

        internal void ObserveEnemy(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId)) EnsureEnemy(enemyId);
        }

        internal bool IsOpenFor(string actorId, string enemyId)
        {
            Enemy enemy;
            Actor actor = GetActor(actorId);
            if (actor == null || string.IsNullOrEmpty(enemyId) || !enemies.TryGetValue(enemyId, out enemy)) return false;
            if (!enemy.BeganTurn || actor.Prepared) return true;
            foreach (string contributor in enemy.Contributors)
                if (contributor != actorId) return true;
            return false;
        }

        /// <summary>
        /// Capture and spend this actor's once-per-turn Sneak Attack entitlement at attack commitment.
        /// A partial, dodged, or fully mitigated eligible attempt remains spent, matching the player-facing rule.
        /// </summary>
        internal AttackCommit CommitPrecisionAttack(string actorId, string enemyId)
        {
            return CommitPrecisionAttack(actorId, enemyId, false, false);
        }

        internal AttackCommit CommitPrecisionAttack(string actorId, string enemyId, bool lastLightEquipped, bool targetFullHealth)
        {
            Actor actor = GetActor(actorId);
            if (actor == null) return new AttackCommit(false, false);
            if (actor.SneakAttemptUsed)
            {
                actor.Prepared = false;
                return new AttackCommit(false, false);
            }
            bool eligible = IsOpenFor(actorId, enemyId);
            if (!eligible) return new AttackCommit(false, false);
            bool lastLight = lastLightEquipped && targetFullHealth && !actor.LastLightUsed;
            if (lastLight) actor.LastLightUsed = true;
            actor.SneakAttemptUsed = true;
            if (actor.Prepared) actor.Prepared = false;
            return new AttackCommit(true, lastLight);
        }

        /// <summary>
        /// A paired-dagger basic attack that misses exactly one check and causes positive direct damage
        /// grants one Prepared token. It cannot enhance the attack that produced it.
        /// </summary>
        internal bool TryTwinFeint(string actorId, bool pairedBasicStrike, int failedChecks, bool positiveDamage)
        {
            Actor actor = GetActor(actorId);
            if (actor == null || !pairedBasicStrike || failedChecks != 1 || !positiveDamage || actor.TwinFeintUsed)
                return false;
            actor.TwinFeintUsed = true;
            GrantPrepared(actor);
            return true;
        }

        internal bool TryPrepare(string actorId, bool positiveDamage)
        {
            Actor actor = GetActor(actorId);
            if (actor == null || !positiveDamage) return false;
            GrantPrepared(actor);
            return true;
        }

        internal bool TrySlipAway(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor == null || actor.SlipAwayUsed) return false;
            actor.SlipAwayUsed = true;
            actor.SlipAwayArmed = true;
            actor.SlipAwayAttack = null;
            GrantPrepared(actor);
            return true;
        }

        /// <summary>Apply Slip Away after native personal defenses. Zero damage does not spend it.</summary>
        internal int ResolveSlipAwayDamage(string actorId, bool directEnemyAttack, int defendedDamage)
        {
            return ResolveSlipAwayDamage(actorId, directEnemyAttack, defendedDamage, false);
        }

        internal int ResolveSlipAwayDamage(string actorId, bool directEnemyAttack, int defendedDamage, bool alreadyHalved)
        {
            return ResolveSlipAwayDamage(actorId, directEnemyAttack, defendedDamage, alreadyHalved, null);
        }

        internal int ResolveSlipAwayDamage(string actorId, bool directEnemyAttack, int defendedDamage,
            bool alreadyHalved, string attackId)
        {
            Actor actor = GetActor(actorId);
            if (actor == null || !directEnemyAttack || defendedDamage <= 0) return defendedDamage;
            bool sameAttack = !string.IsNullOrEmpty(attackId) && actor.SlipAwayAttack == attackId;
            if (!actor.SlipAwayArmed && !sameAttack) return defendedDamage;
            actor.SlipAwayArmed = false;
            if (!sameAttack) actor.SlipAwayAttack = attackId;
            return alreadyHalved ? defendedDamage : (int)(((long)defendedDamage + 1L) / 2L);
        }

        internal void ExpireActor(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor == null) return;
            actor.Prepared = false;
            actor.SlipAwayArmed = false;
            actor.SlipAwayAttack = null;
            actor.EvasionArmed = false;
        }

        /// <summary>
        /// A weapon swap or a non-precision weapon attack discards preparation. It deliberately
        /// leaves Slip Away alone because its next-attack deadline is independent of the weapon.
        /// </summary>
        internal void ClearPrepared(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor != null) actor.Prepared = false;
        }

        internal bool HasPrepared(string actorId)
        {
            Actor actor = GetActor(actorId);
            return actor != null && actor.Prepared;
        }

        internal bool SlipAwayAvailable(string actorId)
        {
            Actor actor = GetActor(actorId);
            return actor != null && !actor.SlipAwayUsed;
        }

        internal void GrantEvasion(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor != null) actor.EvasionArmed = true;
        }

        internal bool HasEvasion(string actorId)
        {
            Actor actor = GetActor(actorId);
            return actor != null && actor.EvasionArmed;
        }

        internal void ClearEvasion(string actorId)
        {
            Actor actor = GetActor(actorId);
            if (actor != null) actor.EvasionArmed = false;
        }

        private Actor GetActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            Actor actor;
            if (!actors.TryGetValue(actorId, out actor))
            {
                actor = new Actor();
                actors.Add(actorId, actor);
            }
            return actor;
        }

        private Enemy EnsureEnemy(string enemyId)
        {
            Enemy enemy;
            if (!enemies.TryGetValue(enemyId, out enemy))
            {
                // An enemy discovered after the genuine encounter roster has spent its initial opening.
                enemy = new Enemy { BeganTurn = true };
                enemies.Add(enemyId, enemy);
            }
            return enemy;
        }

        private static void GrantPrepared(Actor actor)
        {
            actor.Prepared = true;
            actor.PreparedExpiresAfterTurn = actor.Turn + 1;
        }
    }
}
