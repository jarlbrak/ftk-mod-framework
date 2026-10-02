using System;
using System.Collections.Generic;
using System.Globalization;

namespace FTKModFramework.Core
{
    internal static class GuardianSetDescription
    {
        internal static string Format(string key, string className, string[] armorNames, bool[] armorEquipped,
            string oneHandName, string shieldName, string twoHandName, bool oneHand, bool shield,
            bool twoHand, bool offhandEmpty, GuardianProfile neutral, GuardianProfile minor,
            GuardianProfile core, GuardianEquipmentBonuses completion, float armorPayoff, bool grantsSmite)
        {
            if (armorNames == null || armorEquipped == null || armorNames.Length != 3 || armorEquipped.Length != 3 ||
                neutral == null || minor == null || core == null || completion == null) return string.Empty;
            int count = 0;
            for (int i = 0; i < 3; i++) if (armorEquipped[i]) count++;
            bool complete = count == 3 && ((twoHand && offhandEmpty) || (oneHand && shield));
            string family = Title(key);
            List<string> lines = new List<string>();
            lines.Add("<b>" + family + " (" + className + ") " + count + "/3 armor</b>");
            if (count >= 2)
            {
                int first = lines.Count;
                AddProfile(lines, count == 3 ? core : minor, neutral, count == 3 && grantsSmite, false);
                if (lines.Count == first) lines.Add("<b>Active</b>");
                else lines[first] = "<b>Active:</b> " + lines[first];
            }
            if (count < 3)
            {
                lines.Add("<b>Next: " + (count < 2 ? "2" : "3") + " armor</b>");
                AddProfile(lines, count < 2 ? minor : core, count < 2 ? neutral : minor,
                    count == 2 && grantsSmite, true);
            }
            lines.Add("<b>Full set: " + (complete ? "active" : "locked") + "</b>");
            if (!complete)
            {
                if (count < 2) lines.Add("Requires 3 matching armor pieces");
                lines.Add(Arms(family, oneHandName, shieldName, twoHandName,
                    oneHand, shield, twoHand, offhandEmpty));
            }
            AddCompletion(lines, core, completion, armorPayoff);
            return string.Join("\n", lines.ToArray());
        }

        private static void AddProfile(List<string> lines, GuardianProfile profile,
            GuardianProfile baseline, bool grantsSmite, bool preview)
        {
            if (profile.PhysicalPercent != baseline.PhysicalPercent)
                lines.Add("Physical hits deal " + profile.PhysicalPercent + "% damage");
            List<string> offense = new List<string>();
            if (profile.SmitePercent != baseline.SmitePercent)
                offense.Add((grantsSmite ? (preview ? "Unlocks Smite " : "Smite unlocked ") : "Smite ") +
                    Change(profile.SmitePercent));
            else if (grantsSmite) offense.Add(preview ? "Unlocks Smite" : "Smite unlocked");
            if (profile.HealingPercent != baseline.HealingPercent)
                offense.Add("healing " + Change(profile.HealingPercent));
            if (offense.Count > 0) lines.Add(string.Join("; ", offense.ToArray()));
            int guardHeal = profile.Bonuses.GuardHealPercent * profile.HealingPercent;
            int baseGuardHeal = baseline.Bonuses.GuardHealPercent * baseline.HealingPercent;
            if (profile.GuardReductionPercent != baseline.GuardReductionPercent ||
                guardHeal != baseGuardHeal)
            {
                string guard = "Guard blocks " + profile.GuardReductionPercent + "%";
                if (guardHeal > 0) guard += "; heals " + Percent(guardHeal) + "% max HP";
                lines.Add(guard);
            }
            if (profile.Bonuses.RetaliationDamage != baseline.Bonuses.RetaliationDamage &&
                profile.Bonuses.RetaliationDamage > 0)
                lines.Add("Guard retaliates for " + profile.Bonuses.RetaliationDamage + " damage");
            if (profile.Bonuses.FocusHealBonusPercent != baseline.Bonuses.FocusHealBonusPercent &&
                profile.Bonuses.FocusHealBonusPercent > 0)
                lines.Add((profile.GuardSmiteHealing ? "Guard bond: next focused Smite heals ally " : "Focused hits heal ally ") + Percent((8 + profile.Bonuses.FocusHealBonusPercent) *
                    profile.HealingPercent) + "% max HP");
            else if (profile.GuardSmiteHealing && !baseline.GuardSmiteHealing)
                lines.Add("Guard bond: next focused Smite heals ally " +
                    Percent((8 + profile.Bonuses.FocusHealBonusPercent) * profile.HealingPercent) + "% max HP once");
            if (profile.GuardPhysicalBonusPercent > baseline.GuardPhysicalBonusPercent)
                lines.Add("Guard-reduced hit readies +" + profile.GuardPhysicalBonusPercent + "% next single-target physical hit");
            if (profile.Bonuses.WardDebuffs && !baseline.Bonuses.WardDebuffs)
                lines.Add("Guard wards direct-hit Poison, Stun, Daze, Curse");
            if (profile.Bonuses.GuardFocusRestore > baseline.Bonuses.GuardFocusRestore)
                lines.Add("Guard restores " + profile.Bonuses.GuardFocusRestore + " Focus");
            if (profile.Bonuses.GuardReckoning && !baseline.Bonuses.GuardReckoning)
                lines.Add("Guard readies Reckoning");
            if (profile.Bonuses.GuardCleanse && !baseline.Bonuses.GuardCleanse)
                lines.Add("Guard cleanses an ailment");
            if (preview && lines[lines.Count - 1].StartsWith("<b>Next:", StringComparison.Ordinal))
                lines.Add("Core role unlocked");
        }

        private static void AddCompletion(List<string> lines, GuardianProfile core,
            GuardianEquipmentBonuses completion, float armorPayoff)
        {
            if (completion.FocusHealBonusPercent > 0)
                lines.Add((core.GuardSmiteHealing ? "Guard bond: next focused Smite heals ally " : "Focused hits heal ally ") + Percent((8 + Math.Max(core.Bonuses.FocusHealBonusPercent,
                    completion.FocusHealBonusPercent)) * core.HealingPercent) + "% max HP");
            if (completion.WardDebuffs && !core.Bonuses.WardDebuffs)
                lines.Add("Guard wards direct-hit Poison, Stun, Daze, Curse");
            if (completion.RetaliationDamage > core.Bonuses.RetaliationDamage)
                lines.Add("Guard retaliates for " + completion.RetaliationDamage + " damage");
            if (armorPayoff > 1f)
                lines.Add("Direct physical hits +" + ((armorPayoff - 1f) * 100f).ToString("0.#",
                    CultureInfo.InvariantCulture) + "% vs set Armor debuff");
        }

        private static string Arms(string family, string oneHandName, string shieldName,
            string twoHandName, bool oneHand, bool shield, bool twoHand, bool offhandEmpty)
        {
            if ((twoHand && offhandEmpty) || (oneHand && shield)) return "Matching weapons equipped";
            if (twoHand && !offhandEmpty) return "Clear offhand for " + ShortName(twoHandName, family);
            if (oneHand && !shield) return "Equip " + ShortName(shieldName, family);
            if (shield && !oneHand) return "Equip " + ShortName(oneHandName, family);
            return "Equip " + ShortName(oneHandName, family) + " + " + ShortName(shieldName, family) +
                " or " + ShortName(twoHandName, family) + " (empty offhand)";
        }

        private static string Change(int percent)
        {
            int change = percent - 100;
            return (change >= 0 ? "+" : string.Empty) + change + "%";
        }

        private static string Percent(int hundredths)
        {
            return (hundredths / 100f).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Title(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Equipment";
            int separator = key.LastIndexOf(':');
            if (separator >= 0) key = key.Substring(separator + 1);
            string[] words = key.Replace('_', ' ').Replace('-', ' ').Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        private static string ShortName(string item, string family)
        {
            if (string.IsNullOrEmpty(item)) return "item";
            string prefix = family + " ";
            return item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? item.Substring(prefix.Length) : item;
        }
    }
}
