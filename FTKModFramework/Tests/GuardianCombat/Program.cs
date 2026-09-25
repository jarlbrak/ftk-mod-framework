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

    private static GuardianCombatState.DamageResult Hit(GuardianCombatState state, string target, int hp, int damage)
    {
        GuardianCombatState.DamageResult result;
        Check(state.TryResolveDirectDamage(target, hp, damage, out result), "valid damage input");
        return result;
    }

    private static void Main(string[] args)
    {
        GuardianCombatState state = new GuardianCombatState();
        Check(state.RescueAvailable("z") && !state.IsActive("z"), "initial status ready but inactive");
        Check(state.TryGuard("z", "ally", true, true), "guard starts");
        Check(state.IsActive("z"), "active guard status");
        Check(Hit(state, "ally", 100, 11).Damage == 6, "odd damage rounds up");
        Check(Hit(state, "ally", 100, 0).Damage == 0, "zero damage");
        Check(!state.TryGuard("z", "z", true, true), "self guard rejected");
        Check(!state.TryGuard("z", "other", false, true), "incapacitated guardian rejected");
        Check(!state.TryGuard("z", "other", true, false), "dead target rejected");
        Check(!state.TryGuard(null, "other", true, true), "missing identity rejected");
        Check(state.DesignatedAlly("z") == "ally", "invalid action preserves designation");
        Check(Hit(state, "ally", 100, 10).Guarded, "invalid action preserves protection");

        state.ExpireGuard("z");
        Check(!state.IsActive("z") && state.RescueAvailable("z"), "expiry preserves rescue readiness");
        Check(!Hit(state, "ally", 100, 10).Guarded, "turn or incapacitation expiry");
        Check(state.DesignatedAlly("z") == "ally", "designation survives expiry");
        Check(state.ResolveFocusedHitHealing("z", "a1", "ally", 20, 101, true, true, true) == 8, "healing uses recipient max and floors");
        Check(state.ResolveFocusedHitHealing("z", "a1", "ally", 20, 101, true, true, true) == 0, "multi-hit dedup");
        Check(state.ResolveFocusedHitHealing("z", "a2", "ally", 99, 101, true, true, true) == 2, "heal capped at deficit");
        Check(state.ResolveFocusedHitHealing("z", "a3", "ally", 101, 101, true, true, true) == 0, "full health");
        Check(state.ResolveFocusedHitHealing("z", "a3", "ally", 10, 101, true, true, true) == 0, "full health consumes opportunity");
        Check(state.ResolveFocusedHitHealing("z", "a4", "ally", 10, 101, true, true, false) == 0, "miss does not heal");
        Check(state.ResolveFocusedHitHealing("z", "a4", "ally", 10, 101, true, false, true) == 0, "unfocused does not heal");
        Check(state.ResolveFocusedHitHealing("z", "a4", "ally", 0, 101, true, true, true) == 0, "cannot resurrect");
        Check(state.ResolveFocusedHitHealing("z", "a4", "other", 10, 101, true, true, true) == 0, "only designated ally");
        Check(state.ResolveFocusedHitHealing("z", "a4", "ally", 10, 101, false, true, true) == 0, "incapacitated cannot heal");
        Check(state.ResolveFocusedHitHealing("z", "a4", "ally", 10, 101, true, true, true) == 8, "invalid healing does not consume opportunity");

        state.TryGuard("z", "ally", true, true);
        state.TryGuard("a", "ally", true, true);
        Check(Hit(state, "ally", 100, 10).Damage == 5, "guards never stack");
        GuardianCombatState.DamageResult result = Hit(state, "ally", 5, 10);
        Check(result.Rescuer == "a" && result.RemainingHealth == 1 && result.Damage == 4, "ordinal rescue at exact lethal threshold");
        Check(!state.RescueAvailable("a") && state.RescueAvailable("z"), "status tracks only consumed rescue");
        Check(Hit(state, "ally", 5, 10).Rescuer == "z", "only one rescue consumed");
        Check(Hit(state, "ally", 5, 10).RemainingHealth == 0, "charges exhausted");
        state.TryGuard("z", "other", true, true);
        Check(state.DesignatedAlly("z") == "other", "switch designation");
        state.ExpireGuard("a");
        Check(!Hit(state, "ally", 100, 10).Guarded, "switch removes old guard");
        Check(Hit(state, "other", 5, 10).Rescuer == null, "recasting does not recharge rescue");
        Check(state.ResolveFocusedHitHealing("z", "a1", "other", 10, 100, true, true, true) == 0, "switch cannot reuse attack heal");

        state.ResetEncounter();
        Check(state.RescueAvailable("z") && !state.IsActive("z"), "reset restores initial status");
        Check(state.RescueAvailable("a"), "encounter reset restores spent rescue after actor reset");
        Check(state.DesignatedAlly("z") == null, "reset clears designation");
        Check(!Hit(state, "other", 100, 10).Guarded, "reset clears active guard");
        state.TryGuard("z", "ally", true, true);
        GuardianCombatState.DamageResult invalid;
        Check(!state.TryResolveDirectDamage("ally", 0, 20, out invalid), "dead target damage rejected");
        Check(!state.TryResolveDirectDamage("ally", 5, -1, out invalid), "negative damage rejected");
        Check(Hit(state, "ally", 1, int.MaxValue).Rescuer == "z", "invalid damage preserves charge and max damage does not overflow");
        Check(state.ResolveFocusedHitHealing("z", "a1", "ally", 1, int.MaxValue, true, true, true) == 171798691, "healing arithmetic does not overflow and reset clears dedup");
        state.ResetEncounter();
        state.TryGuard("a", "ally", true, true);
        state.ExpireGuard("a");
        Check(Hit(state, "ally", 5, 20).Rescuer == null, "inactive guard cannot rescue");
        state.TryGuard("a", "ally", true, true);
        Check(Hit(state, "ally", 5, 20).Rescuer == "a", "inactive hit did not consume rescue");
        state.ResetEncounter();
        state.TryGuard("a", "ally", true, true);
        Check(state.TryResolveDirectDamage("ally", 5, 20, false, out result) && result.Rescuer == null &&
            result.Damage == 10, "native rescue suppresses custom charge but retains reduction");
        Check(Hit(state, "ally", 5, 20).Rescuer == "a", "native rescue preserves custom charge");
        state.TryGuard("z", "other", true, true);
        state.ResetActor("a");
        Check(!state.IsActive("a") && state.DesignatedAlly("a") == "ally", "revive actor reset expires guard but preserves designation");
        Check(!state.RescueAvailable("a"), "revive actor reset preserves spent rescue");
        state.TryGuard("a", "ally", true, true);
        Check(Hit(state, "ally", 5, 20).Rescuer == null, "revive then reGuard cannot recharge rescue");
        state.ResetActor("a");
        Check(!state.RescueAvailable("a"), "repeated actor reset preserves spent rescue");
        state.ResetGuardian("z");
        Check(state.DesignatedAlly("z") == null, "per guardian start resets own state");
        Check(Hit(state, "ally", 5, 20).Rescuer == null, "other guardian reset never recharges existing guardian");
        state.ResetGuardian("a");
        Check(state.RescueAvailable("a") && state.DesignatedAlly("a") == null, "new native combat initialization restores own rescue and clears designation");
        state.TryGuard("a", "ally", true, true);
        Check(Hit(state, "ally", 5, 20).Rescuer == "a", "new native combat can spend fresh rescue");
        state.ResetEncounter();
        Check(state.RescueAvailable("a"), "full encounter reset restores rescue after revive trial");
        state.TryGuard("a", "ally", true, true);
        state.TryGuard("z", "ally", true, true);
        Check(Hit(state, "ally", 5, 20).Rescuer == "a", "rescuer independent of registration order");
        state.ResetEncounter();
        state.TryGuard("a", "ally", true, true);
        state.TryGuard("z", "ally", true, true);
        Check(state.TryResolveAttackDamage("turn1", "ally", 5, 20, true, out result) && result.Rescuer == "a", "first playback rescues");
        Check(state.TryResolveAttackDamage("turn1", "ally", 1, 20, true, out result) && result.Rescuer == "a" &&
            result.Damage == 4, "duplicate playback preserves outcome with changed live HP");
        Check(state.TryResolveAttackDamage("turn1", "ally", 0, 4, true, out result) && result.Rescuer == "a", "duplicate playback never transforms reduced damage again");
        Check(state.TryResolveAttackDamage("turn2", "ally", 5, 20, true, out result) && result.Rescuer == "z", "duplicate consumed no additional charge");
        state.BeginTurn();
        Check(state.TryResolveAttackDamage("turn1", "ally", 5, 20, true, out result) && result.Rescuer == null, "new turn clears receipts without recharging rescue");
        state.ResetEncounter();
        state.TryGuard("z", "ally", true, true);
        state.TryGuard("a", "ally", true, true);
        Check(string.Join(",", state.ActiveGuardians("ally")) == "a,z", "equipment source order is stable");
        state.ExpireGuard("a");
        Check(string.Join(",", state.ActiveGuardians("ally")) == "z", "expired guardian supplies no defensive equipment perk");
        state.TryGuard("z", "other", true, true);
        Check(state.ActiveGuardians("ally").Length == 0 && state.ActiveGuardians("other").Length == 1, "switch moves gear protection");
        Check(state.ResolveGuardHealingHealth("z", "g1", "other", 30, 101, 8) == 38, "guard heal floors recipient maximum percent");
        Check(state.ResolveGuardHealingHealth("z", "g1", "other", 38, 101, 8) == 38, "repeated impact cannot heal twice");
        Check(state.ResolveGuardHealingHealth("z", "g1", "other", 30, 101, 8) == 38, "replayed guard outcome restores same final health");
        Check(state.ResolveGuardHealingHealth("z", "g2", "other", 99, 101, 8) == 101, "guard heal caps at maximum");
        Check(state.ResolveGuardHealingHealth("z", "g3", "other", 0, 101, 8) == 0, "guard heal cannot resurrect");
        Check(state.ResolveGuardHealingHealth("z", "g3", "other", 30, 101, 21) == 30, "invalid guard perk cannot mutate health or receipt");
        Check(state.ResolveGuardHealingHealth("z", "g3", "other", 30, 101, 2) == 32, "invalid guard heal did not consume receipt");
        Check(state.ResolveGuardHealingHealth("z", "novice", "other", 35, 37, 2) == 36, "positive novice guard heal restores at least one HP");
        Check(state.ResolveGuardHealingHealth("z", "full", "other", 37, 37, 2) == 37, "minimum heal never exceeds full health");
        Check(state.ResolveGuardHealingHealth("z", "zero", "other", 35, 37, 0) == 35, "missing perk never receives minimum healing");
        state.ExpireGuard("z");
        Check(state.ResolveGuardHealingHealth("z", "g4", "other", 30, 101, 8) == 30, "expired guard cannot cast-heal");
        Check(state.ResolveFocusedHitHealing("z", "mercy", "other", 30, 101, true, true, true, 4) == 12, "Mercy hammer adds four percentage points to focused healing");
        Check(state.ResolveFocusedHitHealing("z", "mercy", "other", 30, 101, true, true, true, 4) == 0, "enhanced focused heal still deduplicates");
        var merged = GuardianEquipmentBonuses.Strongest(new GuardianEquipmentBonuses(8, 4, 4), new GuardianEquipmentBonuses(3, 2, 4, true));
        Check(merged.GuardHealPercent == 8 && merged.FocusHealBonusPercent == 4 && merged.RetaliationDamage == 4 && merged.WardDebuffs, "equipped perk values use strongest and ward union");
        Check(GuardianEquipmentBonuses.AddRetaliation(3, 4, 4, 4) == 7, "AoE retaliation once and native reflection preserved");
        Check(GuardianEquipmentBonuses.AddRetaliation(-10, 4, 0, 4) == -6, "native lifedrain offset preserved");
        Check(GuardianEquipmentBonuses.AddRetaliation(int.MaxValue, 4) == int.MaxValue, "retaliation addition cannot overflow");
        Check(GuardianEquipmentBonuses.AddRetaliation(7, 0, 0, 0) == 7, "no eligible guard leaves native retaliation unchanged");
        bool rejected = false;
        try { new GuardianEquipmentBonuses(0, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "negative equipment perks rejected");
        rejected = false;
        try { new GuardianEquipmentBonuses(0, 0, 21); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "oversized equipment perks rejected");
        string guardText = GuardianEquipmentDescription.Format(new GuardianEquipmentBonuses(2), "Paladin");
        Check(guardText == "Paladin only:\nGuard heals 2% ally max HP (min 1).", "guard perk card uses registered value and class qualifier");
        Check(GuardianEquipmentDescription.Format(new GuardianEquipmentBonuses(0,4), "Paladin").Contains("+4% ally max HP"), "Mercy weapon card shows bonus percentage without needing modifier row");
        string wardText = GuardianEquipmentDescription.Format(new GuardianEquipmentBonuses(0,0,0,true), "Paladin");
        Check(wardText.Contains("direct-attack") && wardText.Contains("Poison, Stun, Daze and Curse"), "ward card names exact bounded direct debuffs");
        string retaliationText = GuardianEquipmentDescription.Format(new GuardianEquipmentBonuses(0,0,4), "Paladin");
        Check(retaliationText.Contains("4 damage") && retaliationText.Contains("once per damaging direct attack\nfrom an enemy."), "retaliation card states damage requirement and once-per-attack limit");
        Check(GuardianEquipmentDescription.Format(new GuardianEquipmentBonuses(), "Paladin") == "", "empty bonuses do not alter vanilla card");
        string card = GuardianEquipmentDescription.Append("<color=red>-2 Speed</color>\n", guardText);
        Check(card.StartsWith("<color=red>-2 Speed</color>\nPaladin only:"), "native rich stat text preserved");
        Check(GuardianEquipmentDescription.Append(card, guardText) == card, "same card append is idempotent");
        Check(GuardianEquipmentDescription.Append("native", "") == "native", "no perk leaves native text untouched");
        Check(GuardianFocusedHit.EligibleAttempt(1, 0.5f, false, false), "partially successful focused direct attack qualifies");
        Check(!GuardianFocusedHit.EligibleAttempt(0, 1, false, false) && !GuardianFocusedHit.EligibleAttempt(1, 0, false, false), "unfocused and zero-slot misses excluded");
        Check(!GuardianFocusedHit.EligibleAttempt(1, 1, true, false) && !GuardianFocusedHit.EligibleAttempt(1, 1, false, true), "harmless Guard and forced misses excluded");
        Check(GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.Block), "fully armor absorbed hit qualifies");
        Check(GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.MagicBlock), "fully resistance absorbed hit qualifies");
        Check(!GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.Dodge) && !GuardianFocusedHit.Landed(7, CharacterDummy.AttackResponse.Dodge), "dodge cannot heal even malformed positive damage");
        Check(!GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.HarmlessAttack) && !GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.None), "harmless and unclassified outcomes excluded");
        Check(GuardianFocusedHit.Landed(7, CharacterDummy.AttackResponse.Damaged) && GuardianFocusedHit.Landed(7, CharacterDummy.AttackResponse.Death), "ordinary and lethal damage hits preserved");
        var blockedState = new GuardianCombatState();blockedState.TryGuard("g", "ally", true, true);
        int blockedHeal = blockedState.ResolveFocusedHitHealing("g", "blocked-attack", "ally", 20, 100, true, true,
            GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.Block));
        Check(blockedHeal == 8, "armor absorbed landed hit heals eight percent");
        Check(blockedState.ResolveFocusedHitHealing("g", "blocked-attack", "ally", 28, 100, true, true,
            GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.MagicBlock)) == 0, "second absorbed splash impact cannot repeat healing");
        // One committed splash attack can contain misses, dodges, and absorbed hits.
        // Each guardian gets one heal, even after active protection expires.
        foreach (int bonus in new[] { 2, 4 })
        {
            var splash = new GuardianCombatState();
            splash.TryGuard("first", "ally", true, true);
            splash.TryGuard("second", "ally", true, true);
            splash.ExpireGuard("first");
            string attack = "mercy-splash";
            Check(splash.ResolveFocusedHitHealing("first", attack, "ally", 30, 39, true,
                GuardianFocusedHit.EligibleAttempt(1, 0, false, false),
                GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.Block), bonus) == 0,
                "Mercy zero-slot Block cannot heal");
            Check(splash.ResolveFocusedHitHealing("first", attack, "ally", 30, 39, true, true,
                GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.Dodge), bonus) == 0,
                "Mercy dodged splash target cannot heal or consume later hit");
            int expected = bonus == 2 ? 3 : 4;
            int healed = splash.ResolveFocusedHitHealing("first", attack, "ally", 30, 39, true,
                GuardianFocusedHit.EligibleAttempt(3, 1, false, false),
                GuardianFocusedHit.Landed(0, CharacterDummy.AttackResponse.MagicBlock), bonus);
            Check(healed == expected, "Mercy later absorbed hit heals after Guard expiry without multiplying Focus");
            Check(splash.ResolveFocusedHitHealing("first", attack, "ally", 30 + healed, 39, true, true,
                GuardianFocusedHit.Landed(10, CharacterDummy.AttackResponse.Damaged), bonus) == 0,
                "Mercy later damaging splash target cannot repeat healing");
            Check(splash.ResolveFocusedHitHealing("second", attack, "ally", 38, 39, true, true, true, bonus) == 1,
                "second guardian has independent capped healing for same attack identity");
            Check(splash.ResolveFocusedHitHealing("second", attack, "ally", 30, 39, true, true, true, bonus) == 0,
                "capped Mercy healing consumes the attack opportunity");
        }
        LegendaryChecks.Run(Check);
        OverworldAilmentChecks.Run(Check);
        Console.WriteLine("PASS GuardianCombat: " + checks + " checks");
        if (args.Length == 2 && args[0] == "--assembly") NativeSignatures.Verify(args[1]);
        else if (args.Length != 0) throw new Exception("Usage: GuardianCombat [--assembly PATH]");
        else Console.WriteLine("Native signature checks not requested; use --assembly PATH.");
    }
}
