using System;
using FTKModFramework.Core;

internal static class ThemeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        GuardianProfile old = new GuardianProfile(100, 100, 100, 50, null);
        check(!old.GuardSmiteHealing && old.GuardPhysicalBonusPercent == 0,
            "old profile constructor preserves legacy themes");
        check(typeof(GuardianProfile).GetConstructor(new[] { typeof(int), typeof(int), typeof(int),
            typeof(int), typeof(GuardianEquipmentBonuses) }) != null, "old profile ABI remains");
        bool rejected = false;
        try { new GuardianProfile(100, 100, 100, 50, null, false, 51); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "physical charge is bounded");
        check(GuardianThemeState.StrongestAttackBonus(true, 50) == 50 &&
            GuardianThemeState.StrongestAttackBonus(false, 50) == 50 &&
            GuardianThemeState.StrongestAttackBonus(true, 0) == 50,
            "Kingsfall and Censure use the strongest charge, never a product");

        GuardianThemeState mercy = new GuardianThemeState();
        check(mercy.BeginGuard("m", "guard1", "ally", "mercy", 1, true, 0), "Mercy Guard arms bond");
        check(!mercy.BeginGuard("m", "guard1", "ally", "mercy", 1, true, 0), "duplicate Guard does not rearm");
        mercy.BeginTurn("enemy");
        check(mercy.HasBond("m"), "enemy turn preserves Mercy bond");
        mercy.BeginTurn("m");
        check(mercy.HasBond("m"), "bond survives to guardian next turn");
        check(!mercy.BeginMercyAttack("m", "unfocused", false) && mercy.HasBond("m"),
            "unfocused or wrong attack cannot spend bond");
        check(mercy.BeginMercyAttack("m", "smite", true) && !mercy.HasBond("m"),
            "focused Smite attempt spends bond before hit result");
        check(mercy.MercyReceipt("m", "smite") && mercy.BeginMercyAttack("m", "smite", true),
            "duplicate attempt reuses Mercy receipt");
        check(!mercy.BeginMercyAttack("m", "second", true), "one Guard cannot heal twice");
        mercy.BeginGuard("m", "guard2", "ally", "mercy", 1, true, 0);
        mercy.EndTurn("m");
        check(mercy.HasBond("m"), "fresh Guard on own turn survives to next own turn");
        mercy.BeginTurn("m");
        mercy.EndTurn("m");
        check(!mercy.HasBond("m"), "unused bond expires next own turn end");
        mercy.BeginGuard("m", "guard3", "ally", "mercy", 1, true, 0);
        mercy.LoseTarget("ally");
        check(!mercy.HasBond("m"), "target loss clears bond");
        mercy.BeginGuard("m", "guard4", "ally", "mercy", 1, true, 0);
        mercy.ObserveEquipment("m", "other", 1);
        check(!mercy.HasBond("m"), "role change clears bond");
        mercy.BeginGuard("m", "guard5", "ally", "mercy", 1, true, 0);
        mercy.ObserveEquipment("m", "mercy", -1);
        check(!mercy.HasBond("m"), "weapon unequip clears bond");
        mercy.BeginGuard("m", "guard6", "ally", "mercy", 1, true, 0);
        mercy.ObserveEquipment("m", "mercy", 2);
        check(!mercy.HasBond("m"), "weapon replacement clears bond");

        GuardianThemeState censure = new GuardianThemeState();
        censure.BeginGuard("c", "guard1", "ally", "censure", 2, false, 50);
        check(censure.ChargePercent("c") == 0, "Guard alone cannot charge Censure");
        censure.ResolveMitigation("zero", "ally", 0, 0, new[] { "c" });
        censure.ResolveMitigation("round", "ally", 1, 1, new[] { "c" });
        censure.ResolveMitigation("dodge", "ally", 0, 0, new[] { "c" });
        censure.ResolveMitigation("other", "other", 20, 10, new[] { "c" });
        check(censure.ChargePercent("c") == 0, "zero, dodge, no reduction, wrong target cannot charge");
        censure.ResolveMitigation("hit", "ally", 20, 10, new[] { "c" });
        check(censure.ChargePercent("c") == 50, "actual mitigation arms charge");
        censure.ResolveMitigation("hit", "ally", 20, 10, new[] { "c" });
        censure.BeginTurn("c");
        check(censure.BeginPhysicalAttack("c", "aoe", false) == 0 && censure.ChargePercent("c") == 50,
            "area attack cannot spend charge");
        check(censure.BeginPhysicalAttack("c", "miss", true) == 50 && censure.ChargePercent("c") == 0,
            "eligible physical miss consumes charge");
        check(censure.BeginPhysicalAttack("c", "miss", true) == 50 &&
            censure.BeginPhysicalAttack("c", "later", true) == 0, "peer replay matches one charged attempt");
        censure.ResolveMitigation("later-hit", "ally", 20, 10, new[] { "c" });
        check(censure.ChargePercent("c") == 0, "one Guard cannot recharge Censure");
        censure.BeginGuard("c", "guard2", "ally", "censure", 2, false, 50);
        censure.ResolveMitigation("fresh", "ally", 20, 10, new[] { "c" });
        censure.EndTurn("c");
        check(censure.ChargePercent("c") == 50, "new Guard survives current own turn");
        censure.BeginTurn("c");
        censure.EndTurn("c");
        check(censure.ChargePercent("c") == 0, "charge expires next own turn end");
        censure.BeginGuard("c", "guard3", "ally", "censure", 2, false, 50);
        censure.ResolveMitigation("fresh2", "ally", 20, 10, new[] { "c" });
        censure.ObserveEquipment("c", "censure", 3);
        check(censure.ChargePercent("c") == 0, "weapon swap clears charge");
        censure.BeginGuard("c", "guard4", "ally", "censure", 2, false, 50);
        censure.ResolveMitigation("fresh3", "ally", 20, 10, new[] { "c" });
        censure.ObserveEquipment("c", "censure", -1);
        check(censure.ChargePercent("c") == 0, "weapon-to-empty clears charge");
        censure.ResetActor("c");
        check(!censure.HasBond("c") && censure.ChargePercent("c") == 0,
            "encounter reset clears opportunities");
    }
}
