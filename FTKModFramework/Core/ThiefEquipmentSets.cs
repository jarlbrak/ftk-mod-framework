using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Exact equipped IDs select one visible armor role. Weapons and accessories cannot fill armor slots.
    internal sealed class ThiefEquipmentSets
    {
        internal enum Slot { Head, Body, Feet }
        internal sealed class Profile
        {
            internal readonly string Key;
            internal readonly int Count;
            internal readonly bool MatchingArmament;
            internal Profile(string key, int count, bool matchingArmament)
            { Key = key; Count = count; MatchingArmament = matchingArmament; }
        }
        private sealed class Piece
        {
            internal readonly string Key;
            internal readonly Slot Slot;
            internal Piece(string key, Slot slot) { Key = key; Slot = slot; }
        }
        private readonly Dictionary<int, Piece> armor = new Dictionary<int, Piece>();
        private readonly Dictionary<int, string> arms = new Dictionary<int, string>();
        internal bool HasRegistrations { get { return armor.Count != 0 || arms.Count != 0; } }

        internal static bool ValidKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.IndexOf(':') > 0 &&
                (key.EndsWith(":locksmith", StringComparison.Ordinal) ||
                 key.EndsWith(":nightblade", StringComparison.Ordinal) ||
                 key.EndsWith(":wayfarer", StringComparison.Ordinal));
        }

        internal bool AddArmor(int id, string key, Slot slot)
        {
            if (id < 0 || !ValidKey(key)) return false;
            Piece old;
            if (armor.TryGetValue(id, out old)) return old.Key == key && old.Slot == slot;
            armor.Add(id, new Piece(key, slot));
            return true;
        }

        internal bool AddArmament(int id, string key)
        {
            if (id < 0 || !ValidKey(key)) return false;
            string old;
            if (arms.TryGetValue(id, out old)) return old == key;
            arms.Add(id, key);
            return true;
        }

        internal Profile Evaluate(bool thief, int head, int body, int feet, int weapon)
        {
            if (!thief) return null;
            Piece h, b, f;
            armor.TryGetValue(head, out h);
            armor.TryGetValue(body, out b);
            armor.TryGetValue(feet, out f);
            string[] keys = { h == null || h.Slot != Slot.Head ? null : h.Key,
                b == null || b.Slot != Slot.Body ? null : b.Key,
                f == null || f.Slot != Slot.Feet ? null : f.Key };
            string selected = null;
            int best = 1;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null) continue;
                int count = 0;
                for (int j = 0; j < keys.Length; j++) if (keys[j] == keys[i]) count++;
                if (count > best || (count == best && StringComparer.Ordinal.Compare(keys[i], selected) < 0))
                { best = count; selected = keys[i]; }
            }
            if (selected == null) return null;
            string armKey;
            return new Profile(selected, best, best == 3 && arms.TryGetValue(weapon, out armKey) && armKey == selected);
        }

        internal string Description(int itemId, bool thief, int head, int body, int feet, int weapon)
        {
            Piece item;
            string key;
            bool isArmor = armor.TryGetValue(itemId, out item);
            if (isArmor) key = item.Key;
            else if (!arms.TryGetValue(itemId, out key)) return string.Empty;
            int count = 0;
            Piece equipped;
            if (armor.TryGetValue(head, out equipped) && equipped.Slot == Slot.Head && equipped.Key == key) count++;
            if (armor.TryGetValue(body, out equipped) && equipped.Slot == Slot.Body && equipped.Key == key) count++;
            if (armor.TryGetValue(feet, out equipped) && equipped.Slot == Slot.Feet && equipped.Key == key) count++;
            string family = key.Substring(key.LastIndexOf(':') + 1);
            string heading = char.ToUpperInvariant(family[0]) + family.Substring(1);
            string status = count + "/3 visible armor";
            string threshold = count < 2 ? "Next: 2 armor for preview" :
                count == 2 ? "Next: 3 armor for core" : "Core complete";
            string preview = RoleEffect(family, 2);
            string core = RoleEffect(family, 3);
            if (!thief) return heading + "  Thief only\nAt 2: " + preview +
                "\nAt 3: " + core + "\nOrdinary item stats and attacks still work";
            Profile active = Evaluate(true, head, body, feet, weapon);
            string otherRole = string.Empty;
            if (active != null && active.Key != key)
            {
                string activeFamily = active.Key.Substring(active.Key.LastIndexOf(':') + 1);
                otherRole = "Active: " + char.ToUpperInvariant(activeFamily[0]) + activeFamily.Substring(1) +
                    " " + active.Count + "/3 - " + RoleEffect(activeFamily, active.Count) + "\n";
            }
            string arm = count == 3 && arms.ContainsKey(weapon) && arms[weapon] == key
                ? "Matching weapon equipped; no extra bonus" :
                "Matching weapon optional; no extra bonus";
            return heading + "  " + status + "\n" +
                otherRole + (count == 3 ? core : count == 2 ? preview : "Inspected family inactive") + "\n" +
                (count == 0 || count == 1 ? "At 2: " + preview : threshold) + "\n" +
                (count == 2 ? "At 3: " + core + "\n" : string.Empty) + arm +
                (isArmor ? "\nCharm and artifact do not count" : "\nOrdinary attacks work for any class");
        }

        private static string RoleEffect(string family, int count)
        {
            bool core = count == 3;
            if (family == "locksmith") return core
                ? "Prepared perfect hit: refund 1 spent Focus once/combat; Sneak -10 points"
                : "Prepared Sneak +5 points; other Sneak -5 points";
            if (family == "nightblade") return core
                ? "Full-health or unacted opener +10 points; other Sneak -10 points"
                : "Full-health or unacted opener +5 points; other Sneak -5 points";
            return core
                ? "Direct hit: +4 Evasion to next turn; Sneak -10 points"
                : "Direct hit: +2 Evasion to next turn; Sneak -5 points";
        }

        internal string OwnedDescription(bool thief, int head, int body, int feet, int weapon)
        {
            if (!thief) return string.Empty;
            Profile active = Evaluate(true, head, body, feet, weapon);
            if (active == null)
            {
                Piece h, b, f;
                armor.TryGetValue(head, out h); armor.TryGetValue(body, out b); armor.TryGetValue(feet, out f);
                string one = h != null && h.Slot == Slot.Head ? h.Key :
                    b != null && b.Slot == Slot.Body ? b.Key :
                    f != null && f.Slot == Slot.Feet ? f.Key : null;
                return one == null ? "Thief armor: 0/3; no role active\nNext: 2 matching head/body/feet for preview" :
                    "Thief armor: 1/3; no role active\nNext: 2 matching head/body/feet for preview";
            }
            string family = active.Key.Substring(active.Key.LastIndexOf(':') + 1);
            string heading = char.ToUpperInvariant(family[0]) + family.Substring(1);
            return "Thief armor: " + heading + " " + active.Count + "/3\n" +
                RoleEffect(family, active.Count) + "\n" +
                (active.Count == 2 ? "Next: 3 matching armor for core" : "Core complete") + "\n" +
                (active.MatchingArmament ? "Matching weapon equipped; no extra bonus" :
                    "Matching weapon optional; no extra bonus") +
                "\nOnly visible armor counts; charm and artifact do not";
        }

        internal string OwnedLabel(bool thief, int head, int body, int feet, int weapon)
        {
            Profile active = Evaluate(thief, head, body, feet, weapon);
            if (active == null)
            {
                if (!thief) return string.Empty;
                Piece h, b, f;
                armor.TryGetValue(head, out h); armor.TryGetValue(body, out b); armor.TryGetValue(feet, out f);
                int count = (h != null && h.Slot == Slot.Head ? 1 : 0) +
                    (b != null && b.Slot == Slot.Body ? 1 : 0) +
                    (f != null && f.Slot == Slot.Feet ? 1 : 0);
                return count < 2 ? "No role " + count + "/3" : "No role (" + count + " mixed)";
            }
            string family = active.Key.Substring(active.Key.LastIndexOf(':') + 1);
            return char.ToUpperInvariant(family[0]) + family.Substring(1) + " " + active.Count + "/3";
        }
    }
}
