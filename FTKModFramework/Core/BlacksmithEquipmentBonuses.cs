using System;

namespace FTKModFramework.Core
{
    /// <summary>One native-Blacksmith equipment action. Values are flat Armor points.</summary>
    public sealed class BlacksmithEquipmentBonuses
    {
        public readonly int SetHammerArmor;
        public readonly int OverhandArmorPenalty;
        public readonly int TemperArmor;

        public BlacksmithEquipmentBonuses(int setHammerArmor = 0, int overhandArmorPenalty = 0, int temperArmor = 0)
        {
            if ((setHammerArmor == 0 ? 0 : 1) + (overhandArmorPenalty == 0 ? 0 : 1) +
                (temperArmor == 0 ? 0 : 1) != 1 ||
                (setHammerArmor != 0 && (setHammerArmor < 2 || setHammerArmor > 5)) ||
                (overhandArmorPenalty != 0 && (overhandArmorPenalty < 2 || overhandArmorPenalty > 6)) ||
                (temperArmor != 0 && (temperArmor < 3 || temperArmor > 5)))
                throw new ArgumentOutOfRangeException("bonuses", "Exactly one bounded Blacksmith action is required.");
            SetHammerArmor = setHammerArmor;
            OverhandArmorPenalty = overhandArmorPenalty;
            TemperArmor = temperArmor;
        }

        internal bool SameAs(BlacksmithEquipmentBonuses other)
        {
            return other != null && SetHammerArmor == other.SetHammerArmor &&
                OverhandArmorPenalty == other.OverhandArmorPenalty && TemperArmor == other.TemperArmor;
        }
    }
}
