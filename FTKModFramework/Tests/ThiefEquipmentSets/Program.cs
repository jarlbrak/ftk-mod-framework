using System;
using FTKModFramework.Core;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new Exception(name); }
    private static void Main()
    {
        ThiefEquipmentSets sets = new ThiefEquipmentSets();
        string lockKey = "guild:locksmith", nightKey = "guild:nightblade";
        Check(sets.AddArmor(10, lockKey, ThiefEquipmentSets.Slot.Head), "head registered");
        Check(sets.AddArmor(11, lockKey, ThiefEquipmentSets.Slot.Body), "body registered");
        Check(sets.AddArmor(12, lockKey, ThiefEquipmentSets.Slot.Feet), "feet registered");
        Check(sets.AddArmor(20, nightKey, ThiefEquipmentSets.Slot.Feet), "other family registered");
        Check(sets.AddArmor(21, nightKey, ThiefEquipmentSets.Slot.Head), "other head registered");
        Check(sets.AddArmor(22, nightKey, ThiefEquipmentSets.Slot.Body), "other body registered");
        Check(sets.AddArmament(30, lockKey), "matching armament registered");
        Check(sets.Evaluate(true, 10, -1, -1, -1) == null, "one armor has no role");
        ThiefEquipmentSets.Profile two = sets.Evaluate(true, 10, 11, -1, 30);
        Check(two != null && two.Count == 2 && !two.MatchingArmament, "two armor preview, weapon cannot fill third");
        Check(sets.Evaluate(true, 10, 11, 20, 30).Count == 2, "mixed armor cannot complete core");
        Check(sets.Evaluate(true, 10, 11, 12, -1).Count == 3, "three visible armor core without weapon");
        Check(sets.Evaluate(true, 10, 11, 12, 30).MatchingArmament, "exact matching weapon completion");
        Check(!sets.Evaluate(true, 10, 11, 12, 31).MatchingArmament, "artifact or unrelated weapon does not match");
        Check(sets.Evaluate(false, 10, 11, 12, 30) == null, "other class has no profile");
        Check(sets.Evaluate(true, -1, 11, 12, 30).Count == 2, "removing head resets to preview");
        Check(sets.Evaluate(true, -1, -1, 12, 30) == null, "unequip clears profile");
        Check(sets.AddArmor(10, lockKey, ThiefEquipmentSets.Slot.Head), "repeat declaration is idempotent");
        Check(!sets.AddArmor(10, nightKey, ThiefEquipmentSets.Slot.Head), "conflicting declaration rejected");
        Check(sets.AddArmor(40, "other:locksmith", ThiefEquipmentSets.Slot.Head), "other mod has distinct key");
        Check(sets.Evaluate(true, 40, 11, 12, 30).Count == 2, "other mod cannot complete first family");
        string preview = sets.Description(10, true, 10, 11, -1, -1);
        Check(preview.Contains("2/3 visible armor") && preview.Contains("Prepared Sneak +5") &&
            preview.Contains("other Sneak -5") && preview.Contains("Next: 3"),
            "preview card exposes count, benefit, cost, and next threshold");
        string core = sets.Description(30, true, 10, 11, 12, 30);
        Check(core.Contains("refund 1 spent Focus") && core.Contains("Sneak -10") &&
            core.Contains("Matching weapon equipped; no extra bonus"),
            "core weapon card exposes role and no extra completion power");
        string other = sets.Description(10, false, 10, 11, 12, 30);
        Check(other.Contains("Thief only") && other.Contains("At 2:") && other.Contains("At 3:"),
            "other class card shows role tradeoffs without advertising active role");
        Check(sets.Description(99, true, 10, 11, 12, 30).Length == 0,
            "unregistered artifact has no set section");
        string inspectedOther = sets.Description(10, true, 21, 22, 20, 30);
        Check(inspectedOther.Contains("Locksmith  0/3") &&
            inspectedOther.Contains("Active: Nightblade 3/3") &&
            inspectedOther.Contains("opener +10 points; other Sneak -10 points") &&
            inspectedOther.Contains("At 2: Prepared Sneak +5 points"),
            "inspected family progression and worn role tradeoff are distinct");
        string owned = sets.OwnedDescription(true, 21, 22, 20, 30);
        Check(owned.Contains("Thief armor: Nightblade 3/3") &&
            owned.Contains("opener +10 points; other Sneak -10 points") &&
            owned.Contains("Matching weapon optional; no extra bonus"),
            "owned class section reports actual worn role and cost");
        Check(sets.OwnedLabel(true, 21, 22, 20, 30) == "Nightblade 3/3",
            "owned class label follows worn family");
        Check(sets.OwnedDescription(true, 10, -1, -1, 30).Contains("1/3; no role active"),
            "one visible piece reports next threshold without a role");
        Check(sets.OwnedDescription(false, 21, 22, 20, 30).Length == 0,
            "non-Thief has no owned role section");
        Console.WriteLine("PASS ThiefEquipmentSets: " + checks + " checks");
    }
}
