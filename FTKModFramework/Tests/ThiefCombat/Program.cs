using System;
using FTKModFramework.Core;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    private static void Main()
    {
        ThiefCombatState state = new ThiefCombatState();
        state.BeginEncounter(new[] { "enemy", "other" });
        state.BeginActorTurn("thief");
        Check(state.IsOpenFor("thief", "enemy"), "first enemy turn is open");
        ThiefCombatState.AttackCommit first = state.CommitPrecisionAttack("thief", "enemy");
        Check(first.EligibleForSneakAttack && first.BonusPercent == 35, "first precision attack has fixed bonus");
        Check(!state.CommitPrecisionAttack("thief", "enemy").EligibleForSneakAttack, "one precision entitlement per turn");
        state.TryPrepare("thief", true);
        Check(!state.CommitPrecisionAttack("thief", "enemy").EligibleForSneakAttack,
            "later eligible attack cannot reuse entitlement");
        Check(!state.HasPrepared("thief"), "later eligible commitment still consumes Prepared");

        state.BeginEnemyTurn("enemy");
        Check(!state.IsOpenFor("thief", "enemy"), "own attack does not create an opening");
        Check(state.RecordPositiveDirectDamage("ally-a", "enemy"), "ally damage adds contributor");
        Check(state.RecordPositiveDirectDamage("ally-b", "enemy"), "second ally contributor accumulates");
        Check(state.IsOpenFor("thief", "enemy"), "any other contributor opens target");
        state.BeginActorTurn("thief");
        Check(state.CommitPrecisionAttack("thief", "enemy").EligibleForSneakAttack, "new scheduled turn restores entitlement");
        state.BeginEnemyTurn("enemy");
        Check(!state.IsOpenFor("thief", "enemy"), "enemy turn closes every contributor opening");

        state.BeginEnemyTurn("summoned");
        Check(!state.IsOpenFor("thief", "summoned"), "summoned enemy has spent its initial opening");
        Check(state.RecordPositiveDirectDamage("ally-a", "summoned"), "ally damages summoned enemy");
        Check(state.IsOpenFor("thief", "summoned"), "ally opens summoned enemy after its first turn");
        state.ResetEnemy("summoned");
        Check(!state.IsOpenFor("thief", "summoned"), "revived enemy has spent its initial opening");
        Check(state.RecordPositiveDirectDamage("ally-b", "summoned"), "ally opens revived enemy");
        Check(state.IsOpenFor("thief", "summoned"), "revived enemy requires an ally opening");
        state.BeginEnemyTurn("summoned");
        Check(!state.IsOpenFor("thief", "summoned"), "revived enemy's next turn closes opening");

        state.BeginActorTurn("thief");
        Check(state.TryTwinFeint("thief", true, 1, true), "one failed paired check prepares next attack");
        Check(state.HasPrepared("thief"), "Twin Feint adds prepared state");
        Check(!state.TryTwinFeint("thief", true, 1, true), "Twin Feint cannot stack in one turn");
        Check(state.CommitPrecisionAttack("thief", "enemy").EligibleForSneakAttack, "Prepared opens a later target");
        Check(!state.HasPrepared("thief"), "precision commitment consumes Prepared");
        Check(state.TryTwinFeint("thief", true, 1, true) == false, "second twin feint stays spent after prepared consumption");

        state.BeginActorTurn("thief");
        Check(!state.TryTwinFeint("thief", true, 0, true), "perfect paired strike cannot Twin Feint");
        Check(!state.TryTwinFeint("thief", true, 2, true), "two missed checks cannot Twin Feint");
        Check(!state.TryTwinFeint("thief", false, 1, true), "one-handed or bow attack cannot Twin Feint");
        Check(!state.TryTwinFeint("thief", true, 1, false), "blocked or dodged near miss cannot Twin Feint");
        Check(state.TryPrepare("thief", true), "Feint or Draw Out can prepare");
        state.EndActorTurn("thief");
        Check(state.HasPrepared("thief"), "Prepared survives its producing turn");
        state.BeginActorTurn("thief");
        Check(state.HasPrepared("thief"), "Prepared survives to next scheduled turn");
        state.EndActorTurn("thief");
        Check(!state.HasPrepared("thief"), "Prepared expires after next scheduled turn");

        state.BeginActorTurn("thief");
        Check(state.TrySlipAway("thief"), "Slip Away arms once per combat and prepares");
        Check(!state.TrySlipAway("thief"), "Slip Away cannot rearm in one combat");
        Check(state.ResolveSlipAwayDamage("thief", false, 9) == 9, "non-enemy damage cannot consume Slip Away");
        Check(state.ResolveSlipAwayDamage("thief", true, 0) == 0, "zero post-defense damage cannot consume Slip Away");
        Check(state.ResolveSlipAwayDamage("thief", true, 9) == 5, "Slip Away halves odd post-defense damage upward");
        Check(state.ResolveSlipAwayDamage("thief", true, 9) == 9, "Slip Away only protects one enemy attack");
        state.BeginEncounter(new[] { "enemy" });
        state.BeginActorTurn("thief");
        Check(state.TrySlipAway("thief"), "fresh combat can arm Slip Away for turn-boundary expiry");
        state.BeginActorTurn("thief");
        Check(state.ResolveSlipAwayDamage("thief", true, 9) == 9, "next own turn expires unused Slip Away protection");
        Check(state.TryPrepare("thief", true), "preparation can be tested independently of protection");
        state.ClearPrepared("thief");
        Check(!state.HasPrepared("thief"), "weapon swap or non-precision attack clears Prepared");
        Check(state.TrySlipAway("thief") == false, "Prepared clear cannot restore the once-per-combat action");
        state.ExpireActor("thief");
        Check(!state.HasPrepared("thief"), "incapacity clears Prepared");
        Check(!state.SlipAwayAvailable("thief"), "incapacity does not restore spent Slip Away");

        state.BeginEncounter(new[] { "enemy" });
        state.BeginActorTurn("thief");
        ThiefCombatState.AttackCommit light = state.CommitPrecisionAttack("thief", "enemy", true, true);
        Check(light.LastLight && light.BonusPercent == 75, "full-health opening spends Last Light for 75 percent");
        state.BeginActorTurn("thief");
        Check(state.CommitPrecisionAttack("thief", "enemy", true, true).BonusPercent == 35,
            "Last Light cannot recharge on a later turn in the same combat");
        state.GrantEvasion("thief");
        Check(state.HasEvasion("thief"), "damaging artifact Sneak Attack grants timed Evasion");
        state.BeginActorTurn("thief");
        Check(!state.HasEvasion("thief"), "next scheduled turn expires Evasion");
        state.GrantEvasion("thief");
        state.ClearEvasion("thief");
        Check(!state.HasEvasion("thief"), "weapon swap clears Evasion");
        state.TrySlipAway("thief");
        Check(state.ResolveSlipAwayDamage("thief", true, 5, true) == 5,
            "Guard and Slip Away use one strongest reduction");
        Check(state.ResolveSlipAwayDamage("thief", true, 5) == 5,
            "Guarded hit still consumes Slip Away charge");

        state.BeginEncounter(new[] { "enemy" });
        state.BeginActorTurn("thief");
        Check(state.TrySlipAway("thief"), "new combat arms multi-hit Slip Away");
        Check(state.ResolveSlipAwayDamage("thief", true, 9, false, "attack-1") == 5,
            "first direct hit consumes charge and halves damage");
        Check(state.ResolveSlipAwayDamage("thief", true, 7, false, "attack-1") == 4,
            "second direct hit from the same attack remains protected");
        Check(state.ResolveSlipAwayDamage("thief", true, 7, false, "attack-2") == 7,
            "later attack is unprotected");

        state.BeginEncounter(new[] { "enemy" });
        Check(state.SlipAwayAvailable("thief"), "new encounter restores Slip Away");
        state.BeginActorTurn("thief");
        Check(!state.CommitPrecisionAttack("thief", "enemy", true, false).LastLight,
            "injured target does not spend Last Light");
        Console.WriteLine("PASS ThiefCombat: " + checks + " checks");
    }
}
