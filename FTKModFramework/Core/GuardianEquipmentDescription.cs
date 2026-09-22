using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class GuardianEquipmentDescription
    {
        // The native class label overflows horizontally rather than wrapping. Keep each rule
        // on two short lines; the exact live panel fit remains a separate visual check.
        internal const string ClassRules =
            "Guard: -50% ally direct damage\n" +
            "until next turn or incapacity.\n" +
            "Focused hit: heal chosen ally\n" +
            "8% max HP, once per attack.\n" +
            "Active Guard: lethal direct hit\n" +
            "leaves 1 HP, once per combat.";

        internal static string Format(GuardianEquipmentBonuses bonuses, string classNames)
        {
            if (bonuses == null) return string.Empty;
            List<string> lines = new List<string>();
            if (bonuses.GuardHealPercent > 0)
                lines.Add("Guard heals " + bonuses.GuardHealPercent + "% ally max HP (min 1).");
            if (bonuses.FocusHealBonusPercent > 0)
                lines.Add("Focused-hit healing: +" + bonuses.FocusHealBonusPercent + "% ally max HP.");
            if (bonuses.WardDebuffs)
                lines.Add("Guard blocks direct-attack\nPoison, Stun, Daze and Curse.");
            if (bonuses.RetaliationDamage > 0)
                lines.Add("Guard retaliates for " + bonuses.RetaliationDamage + " damage\nonce per damaging direct attack\nfrom an enemy.");
            if (bonuses.GuardFocusRestore > 0)
                lines.Add("First reduced hit per Guard:\nrestore 1 Focus to guarded ally.");
            if (bonuses.GuardReckoning)
                lines.Add("Guarded hit readies Reckoning:\n+50% next single-target hammer hit.\nSpent on attempt; expires next turn end.");
            if (bonuses.GuardCleanse)
                lines.Add("Guard removes one ally condition:\nStun, Daze, Curse, then Poison.");
            if (lines.Count == 0) return string.Empty;
            return (string.IsNullOrEmpty(classNames) ? "Guard users" : classNames) + " only:\n" + string.Join("\n", lines.ToArray());
        }

        internal static string Append(string nativeText, string description)
        {
            if (string.IsNullOrEmpty(description)) return nativeText;
            string original = (nativeText ?? string.Empty).TrimEnd('\r', '\n');
            if (original == description || original.EndsWith("\n" + description, System.StringComparison.Ordinal)) return original;
            return original.Length == 0 ? description : original + "\n" + description;
        }
    }
}
