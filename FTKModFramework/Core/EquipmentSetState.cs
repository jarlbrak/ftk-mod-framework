using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Pure evaluation of a native inventory snapshot. Accessories never select a family.
    internal static class EquipmentSetState
    {
        internal sealed class Definition
        {
            internal readonly string Key;
            internal readonly int Head, Body, Feet, OneHand, Shield, TwoHand;
            internal readonly GuardianProfile Minor, Core;
            internal readonly GuardianEquipmentBonuses Completion;
            internal Definition(string key, int head, int body, int feet, int oneHand, int shield, int twoHand,
                GuardianProfile minor, GuardianProfile core, GuardianEquipmentBonuses completion)
            {
                Key = key; Head = head; Body = body; Feet = feet; OneHand = oneHand; Shield = shield; TwoHand = twoHand;
                Minor = minor; Core = core; Completion = completion;
            }
        }
        internal sealed class Result
        {
            internal readonly Definition Set;
            internal readonly int ArmorCount;
            internal readonly bool ArmamentComplete;
            internal Result(Definition set, int armorCount, bool complete)
            { Set = set; ArmorCount = armorCount; ArmamentComplete = complete; }
        }
        internal static Result Evaluate(IList<Definition> sets, int head, int body, int feet, int right, int left)
        {
            Result selected = null;
            foreach (Definition set in sets)
            {
                int count = (head == set.Head ? 1 : 0) + (body == set.Body ? 1 : 0) + (feet == set.Feet ? 1 : 0);
                if (count < 2) continue;
                bool complete = count == 3 && ((right == set.TwoHand && left < 0) ||
                    (right == set.OneHand && left == set.Shield));
                if (selected == null || count > selected.ArmorCount ||
                    (count == selected.ArmorCount && StringComparer.Ordinal.Compare(set.Key, selected.Set.Key) < 0))
                    selected = new Result(set, count, complete);
            }
            return selected;
        }
    }
}
