using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class Program
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }

    private static void Main()
    {
        GuardianProfile neutral = new GuardianProfile(75, 100, 100, 50, new GuardianEquipmentBonuses());
        GuardianProfile mercy = new GuardianProfile(65, 160, 150, 35,
            new GuardianEquipmentBonuses(guardHealPercent: 4));
        GuardianProfile verdict = new GuardianProfile(60, 50, 75, 50, new GuardianEquipmentBonuses());
        List<EquipmentSetState.Definition> sets = new List<EquipmentSetState.Definition>();
        sets.Add(new EquipmentSetState.Definition("mercy", 1, 2, 3, 4, 5, 6, neutral, mercy,
            new GuardianEquipmentBonuses(focusHealBonusPercent: 2)));
        sets.Add(new EquipmentSetState.Definition("verdict", 11, 12, 13, 14, 15, 16, neutral, verdict,
            new GuardianEquipmentBonuses(wardDebuffs: true)));
        Check(EquipmentSetState.Evaluate(sets, 1, -1, -1, 4, 5) == null, "one armor never activates");
        EquipmentSetState.Result result = EquipmentSetState.Evaluate(sets, 1, 2, -1, 14, 15);
        Check(result.Set.Key == "mercy" && result.ArmorCount == 2 && !result.ArmamentComplete,
            "two armor selects only minor role");
        result = EquipmentSetState.Evaluate(sets, 1, 2, 3, 14, 15);
        Check(result.Set.Key == "mercy" && result.ArmorCount == 3 && !result.ArmamentComplete,
            "foreign arms retain core without completion");
        Check(EquipmentSetState.Evaluate(sets, 1, 2, 3, 4, 5).ArmamentComplete,
            "matching one hand and shield complete");
        Check(EquipmentSetState.Evaluate(sets, 1, 2, 3, 6, -1).ArmamentComplete,
            "matching two hand and empty offhand complete");
        Check(!EquipmentSetState.Evaluate(sets, 1, 2, 3, 6, 5).ArmamentComplete,
            "two hand with occupied offhand cannot complete");
        Check(EquipmentSetState.Evaluate(sets, 1, 12, 3, 4, 5).Set.Key == "mercy",
            "mixed armor selects sole two piece family");
        Check(GuardianProfile.Scale(11, 65) == 7 && GuardianProfile.Scale(11, 150) == 17,
            "profile scaling is rounded and bounded");
        Check(GuardianProfile.Scale(int.MaxValue, 200) == int.MaxValue, "scaling never overflows");
        Check(CombatProficiencyPolicy.Qualifies(73, 2, -2f, new[] { 73, 74 }),
            "exact active Armor source qualifies");
        Check(!CombatProficiencyPolicy.Qualifies(75, 2, -2f, new[] { 73, 74 }) &&
            !CombatProficiencyPolicy.Qualifies(73, 0, -2f, new[] { 73 }) &&
            !CombatProficiencyPolicy.Qualifies(73, 2, -0.5f, new[] { 73 }),
            "foreign expired and nonnegative Armor never qualify");
        Check(GuardianPreviewMath.Calculate(5, 0.5f, 1f, 1f, 150, 1f, 1f) == 4,
            "preview rounds combined product once; rounded native card would incorrectly show five");
        Check(GuardianPreviewMath.Calculate(5, 0.5f, 1f, 1f, 150, 1f, 1.5f) == 6,
            "Frozen rounds after the combined damage product");
        Check(GuardianPreviewMath.Calculate(5, 0.5f, 1.2f, 1.5f, 125, 1.2f, 1f) == 7,
            "resistance, Reckoning, profile and Armor multipliers each apply once");
        Check(GuardianPreviewMath.EffectiveType(2, 1, 0) == 1 &&
            GuardianPreviewMath.EffectiveType(2, 0, 0) == 2,
            "physical proficiency override replaces magic weapon damage type");
        string[] mercyNames = { "Mercy Helm", "Mercy Armor", "Mercy Boots" };
        GuardianEquipmentBonuses mercyFinish = new GuardianEquipmentBonuses(focusHealBonusPercent: 2);
        string card = GuardianSetDescription.Format("com.ftkmf.paladin:mercy", "Paladin",
            mercyNames, new[] { true, true, false }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            true, false, false, true, neutral, neutral, mercy, mercyFinish, 1f, true);
        Check(card.Contains("<b>Mercy (Paladin) 2/3 armor</b>") &&
            card.Contains("<b>Next: 3 armor</b>") && card.Contains("Physical hits deal 65% damage") &&
            card.Contains("Unlocks Smite +60%; healing +50%") &&
            card.Contains("Guard blocks 35%; heals 6% max HP") &&
            card.Contains("<b>Full set: locked</b>") && card.Contains("Equip Shield") &&
            card.Contains("Focused hits heal ally 15% max HP") &&
            !card.Contains("Phys ") && !card.Contains("com.ftkmf") &&
            card.Split('\n').Length <= 10,
            "two armor card shows next role, costs and locked matching armament concisely");
        card = GuardianSetDescription.Format("mercy", "Paladin", mercyNames,
            new[] { true, true, false }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            true, true, false, false, neutral, neutral, mercy, mercyFinish, 1f, true);
        Check(card.Contains("Matching weapons equipped") && card.Contains("Full set: locked"),
            "matching arms alone do not mark incomplete armor as complete");
        GuardianProfile mercyMinor = new GuardianProfile(75, 100, 112, 50, new GuardianEquipmentBonuses());
        card = GuardianSetDescription.Format("mercy", "Paladin", mercyNames,
            new[] { true, true, false }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            false, false, false, true, neutral, mercyMinor, mercy, mercyFinish, 1f, true);
        Check(card.Contains("<b>Active:</b> healing +12%") &&
            card.Contains("<b>Next: 3 armor</b>") && card.Contains("healing +50%"),
            "active minor benefit and next core tradeoff are distinct");
        card = GuardianSetDescription.Format("mercy", "Paladin", mercyNames,
            new[] { true, true, true }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            false, false, true, true, neutral, mercyMinor, mercy, mercyFinish, 1f, true);
        Check(card.Contains("<b>Full set: active</b>") && card.Contains("Focused hits heal ally 15% max HP") &&
            !card.Contains("Equip ") && !card.Contains("Next:"),
            "matching two-hand set marks completion and omits irrelevant next threshold");
        card = GuardianSetDescription.Format("mercy", "Paladin", mercyNames,
            new[] { true, true, true }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            false, false, true, false, neutral, mercyMinor, mercy, mercyFinish, 1f, true);
        Check(card.Contains("<b>Full set: locked</b>") && card.Contains("Clear offhand"),
            "occupied offhand prevents two-hand completion");
        card = GuardianSetDescription.Format("mercy", "Paladin", mercyNames,
            new[] { true, true, true }, "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer",
            false, false, true, true, neutral, mercyMinor, mercy, mercyFinish, 1f, false);
        Check(card.Contains("Smite +60%") && !card.Contains("unlocked") && !card.Contains("Unlocks"),
            "Mercy strengthens a base class skill without claiming to unlock it");
        GuardianProfile verdictMinor = new GuardianProfile(75, 100, 100, 50,
            new GuardianEquipmentBonuses(retaliationDamage: 1));
        card = GuardianSetDescription.Format("verdict", "Paladin",
            new[] { "Verdict Helm", "Verdict Armor", "Verdict Boots" },
            new[] { true, true, true }, "Verdict Hammer", "Verdict Shield", "Verdict Great Hammer",
            true, true, false, false, neutral, verdictMinor, verdict,
            new GuardianEquipmentBonuses(wardDebuffs: true), 1f, false);
        Check(card.Contains("Smite -50%; healing -25%") &&
            card.Contains("Guard wards direct-hit Poison, Stun, Daze, Curse") &&
            card.Contains("<b>Full set: active</b>"),
            "Verdict costs and exact four-debuff ward are visible");
        GuardianProfile censure = new GuardianProfile(125, 50, 50, 25, new GuardianEquipmentBonuses());
        card = GuardianSetDescription.Format("censure", "Paladin",
            new[] { "Censure Helm", "Censure Armor", "Censure Boots" },
            new[] { false, false, false }, "Censure Hammer", "Censure Shield", "Censure Great Hammer",
            false, false, false, true, neutral, neutral, censure,
            new GuardianEquipmentBonuses(), 1.2f, false);
        Check(card.Contains("<b>Next: 2 armor</b>") && !card.Contains("Next: 3") &&
            card.Contains("Direct physical hits +20% vs set Armor debuff") &&
            card.Contains("<b>Full set: locked</b>"),
            "empty set previews only next threshold and its eventual own-Armor payoff");
        Check(GuardianSetDescription.Format("mercy", "Paladin", mercyNames, new[] { true, true, true },
            "Mercy Hammer", "Mercy Shield", "Mercy Great Hammer", false, false, false, true,
            null, mercyMinor, mercy, mercyFinish, 1f, true) == string.Empty,
            "unregistered neutral profile does not fabricate set benefits");
        Console.WriteLine("Equipment set profiles passed");
    }
}
