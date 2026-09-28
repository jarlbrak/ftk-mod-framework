using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ClassAffinityRuntime
    {
        private sealed class Registration
        {
            internal readonly int ClassId;
            internal readonly FTK_playerGameStart ClassRow;
            internal readonly ItemClassAffinityBonuses Bonuses;

            internal Registration(int classId, FTK_playerGameStart classRow, ItemClassAffinityBonuses bonuses)
            {
                ClassId = classId;
                ClassRow = classRow;
                Bonuses = bonuses;
            }

            internal bool SameAs(Registration other)
            {
                return other != null && ClassId == other.ClassId && ClassRow.m_ID == other.ClassRow.m_ID &&
                    Bonuses.SameAs(other.Bonuses);
            }
        }

        private static Dictionary<int, Registration> Equipment = new Dictionary<int, Registration>();

        internal static int ReloadEquipmentCount { get { return Equipment.Count; } }

        internal static Action SuspendForReload()
        {
            Dictionary<int, Registration> previous = Equipment;
            Equipment = new Dictionary<int, Registration>();
            return delegate { Equipment = previous; };
        }

        internal static bool Register(int itemId, FTK_playerGameStart classRow, int classId,
            ItemClassAffinityBonuses bonuses)
        {
            if (itemId < 0 || classRow == null || classId < 0 || bonuses == null) return false;
            Registration candidate = new Registration(classId, classRow, bonuses);
            Registration previous;
            if (Equipment.TryGetValue(itemId, out previous)) return previous.SameAs(candidate);
            Equipment.Add(itemId, candidate);
            return true;
        }

        internal static string Description(int itemId)
        {
            Registration registration;
            if (!Equipment.TryGetValue(itemId, out registration)) return string.Empty;
            List<string> lines = new List<string>();
            if (registration.Bonuses.Armor > 0) lines.Add("+" + registration.Bonuses.Armor + " Armor");
            if (registration.Bonuses.Resistance > 0) lines.Add("+" + registration.Bonuses.Resistance + " Resistance");
            if (registration.Bonuses.Vitality > 0f) lines.Add("+" + StatPoints(registration.Bonuses.Vitality) + " Vitality");
            if (registration.Bonuses.Speed > 0f) lines.Add("+" + StatPoints(registration.Bonuses.Speed) + " Speed");
            if (registration.Bonuses.Reflect > 0) lines.Add("+" + registration.Bonuses.Reflect + " Reflect");
            if (lines.Count == 0) return string.Empty;
            // Localization may be unavailable during registration and can change while the game runs.
            string className = registration.ClassRow.GetDisplayName();
            if (string.IsNullOrEmpty(className)) className = registration.ClassRow.m_ID;
            if (!string.IsNullOrEmpty(className)) className = char.ToUpper(className[0]) + className.Substring(1);
            return className + " bonus:" + (lines.Count == 1 ? " " : "\n") + string.Join("\n", lines.ToArray());
        }

        internal static ItemClassAffinityTotal Aggregate(int classId, IEnumerable<int> modifierIds)
        {
            if (modifierIds == null || Equipment.Count == 0) return null;
            ItemClassAffinityTotal total = null;
            HashSet<int> seen = new HashSet<int>();
            foreach (int itemId in modifierIds)
            {
                if (!seen.Add(itemId)) continue;
                Registration registration;
                if (!Equipment.TryGetValue(itemId, out registration) || registration.ClassId != classId) continue;
                if (total == null) total = new ItemClassAffinityTotal();
                total.Armor += registration.Bonuses.Armor;
                total.Resistance += registration.Bonuses.Resistance;
                total.Vitality += registration.Bonuses.Vitality;
                total.Speed += registration.Bonuses.Speed;
                total.Reflect += registration.Bonuses.Reflect;
            }
            return total;
        }

        private static int StatPoints(float value)
        {
            return (int)Math.Round(value * 100f);
        }
    }
}
