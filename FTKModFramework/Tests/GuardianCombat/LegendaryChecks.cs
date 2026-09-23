using System;
using FTKModFramework.Core;

internal static class LegendaryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        GuardianLegendaryState state = new GuardianLegendaryState();
        check(!state.BeginGuard("paladin", "guard", "paladin", 10, -1), "legendary self Guard rejected");
        check(state.BeginGuard("paladin", "guard", "ally", 10, -1), "legendary Guard arms once");
        check(!state.BeginGuard("paladin", "guard", "ally", 10, -1), "duplicate Guard cannot cleanse twice");
        check(!state.ResolveMitigation("zero", "ally", 0, 0, new[] { "paladin" }), "zero damage never triggers Focus");
        check(!state.ResolveMitigation("one", "ally", 1, 1, new[] { "paladin" }), "unreduced rounding never triggers Focus");
        check(!state.ResolveMitigation("other", "other", 20, 10, new[] { "paladin" }), "only designated ally receives legendary reward");
        check(!state.ResolveMitigation("expired", "ally", 20, 10, new string[0]), "expired or incapacitated Guard cannot trigger");
        check(state.ResolveMitigation("hit", "ally", 20, 10, new[] { "paladin" }), "first actual mitigation restores Focus");
        check(!state.ResolveMitigation("hit", "ally", 20, 10, new[] { "paladin" }), "duplicate outcome cannot grant Focus again");
        check(!state.ResolveMitigation("hit2", "ally", 20, 10, new[] { "paladin" }), "later hit cannot grant Focus again");
        check(GuardianLegendaryState.FocusGain(3, 3) == 0 && GuardianLegendaryState.FocusGain(2, 3) == 1,
            "native Focus cap projection");
        state.BeginTurn("enemy");
        check(!state.ResolveMitigation("hit3", "ally", 20, 10, new[] { "paladin" }), "full Focus still consumed this Guard opportunity");
        state.BeginGuard("paladin", "guard2", "ally", 10, -1);
        state.BeginGuard("other", "guard3", "ally", 11, -1);
        check(state.ResolveMitigation("overlap", "ally", 20, 10, new[] { "other", "paladin" }), "overlapping Focus rewards produce one grant");
        check(!state.ResolveMitigation("overlap2", "ally", 20, 10, new[] { "paladin", "other" }), "overlap consumes both first-hit opportunities");
        state.BeginGuard("paladin", "guard4", "ally", 10, -1);
        state.ObserveEquipment("paladin", -1, -1);
        state.ObserveEquipment("paladin", 10, -1);
        check(!state.ResolveMitigation("swapped", "ally", 20, 10, new[] { "paladin" }), "unequip and re-equip does not rearm Watch");

        state.BeginGuard("paladin", "reckoningGuard", "ally", -1, 20);
        check(!state.IsCharged("paladin"), "Guard action alone cannot charge Reckoning");
        state.ResolveMitigation("charge", "ally", 20, 10, new[] { "paladin" });
        check(state.IsCharged("paladin"), "mitigation charges Reckoning");
        state.EndTurn("enemy");
        state.BeginTurn("paladin");
        check(state.IsCharged("paladin"), "charge survives into next owner turn");
        check(!state.BeginAttack("paladin", "area", false) && state.IsCharged("paladin"), "ineligible area attack cannot consume charge");
        check(state.BeginAttack("paladin", "miss", true) && !state.IsCharged("paladin"), "eligible attempt consumes charge before hit or miss");
        check(state.BeginAttack("paladin", "miss", true), "owner calculation and peer playback share charged attempt receipt");
        check(!state.BeginAttack("paladin", "second", true), "one charge cannot apply to another attack");
        state.ResolveMitigation("charge-again", "ally", 20, 10, new[] { "paladin" });
        check(!state.IsCharged("paladin"), "later hits from same Guard cannot recharge");
        state.BeginGuard("paladin", "freshGuard", "ally", -1, 20);
        state.ResolveMitigation("freshHit", "ally", 20, 10, new[] { "paladin" });
        state.BeginTurn("paladin");
        state.BeginGuard("paladin", "repeatGuard", "ally", -1, 20);
        state.ResolveMitigation("repeatedHit", "ally", 20, 10, new[] { "paladin" });
        state.EndTurn("paladin");
        check(!state.IsCharged("paladin"), "repeated Guard cannot extend existing charge beyond next owner turn");
        state.BeginGuard("paladin", "swapGuard", "ally", -1, 20);
        state.ResolveMitigation("swapHit", "ally", 20, 10, new[] { "paladin" });
        state.ObserveEquipment("paladin", -1, 21);
        check(!state.IsCharged("paladin"), "different Reckoning weapon invalidates charge");
        state.ObserveEquipment("paladin", -1, 20);
        state.ResolveMitigation("swapAgain", "ally", 20, 10, new[] { "paladin" });
        check(!state.IsCharged("paladin"), "swap back cannot resurrect pending charge or Guard perk");
        state.BeginGuard("paladin", "endGuard", "ally", -1, 20);
        state.ResolveMitigation("endHit", "ally", 20, 10, new[] { "paladin" });
        state.ResetActor("paladin");
        check(!state.IsCharged("paladin"), "battle exit or actor reset discards charge");
        check(!state.BeginAttack("paladin", "newBattle", true), "charge cannot carry to next encounter");

        for (int mask = 0; mask < 16; mask++)
        {
            int expected = (mask & 1) != 0 ? 1 : (mask & 2) != 0 ? 2 : (mask & 4) != 0 ? 3 : (mask & 8) != 0 ? 4 : 0;
            check(GuardianLegendaryState.CleanseChoice((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0) == expected,
                "cleanse deterministic priority combination " + mask);
        }
        GuardianEquipmentBonuses perks = GuardianEquipmentBonuses.Strongest(
            new GuardianEquipmentBonuses(guardFocusRestore: 1, guardCleanse: true),
            new GuardianEquipmentBonuses(guardFocusRestore: 1, guardReckoning: true));
        check(perks.GuardFocusRestore == 1 && perks.GuardReckoning && perks.GuardCleanse, "legendary capabilities aggregate without stacking");
        bool rejected = false;
        try { new GuardianEquipmentBonuses(guardFocusRestore: 2); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "Focus reward cannot exceed one");
        check(typeof(GuardianEquipmentBonuses).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(bool) }) != null,
            "existing compiled mods retain four-argument constructor ABI");
        string card = GuardianEquipmentDescription.Format(perks, "Paladin");
        check(card.Contains("restore 1 Focus") && card.Contains("+50%") && card.Contains("Stun, Daze, Curse, then Poison"), "native card text derives from all legendary capabilities");
    }
}
