using System;

namespace FTKModFramework.Core
{
    /// <summary>Bounded Guardian-only combat rules. Percent values use 100 as the native baseline.</summary>
    public sealed class GuardianProfile
    {
        public int PhysicalPercent { get; private set; }
        public int SmitePercent { get; private set; }
        public int HealingPercent { get; private set; }
        public int GuardReductionPercent { get; private set; }
        public GuardianEquipmentBonuses Bonuses { get; private set; }

        public GuardianProfile(int physicalPercent, int smitePercent, int healingPercent,
            int guardReductionPercent, GuardianEquipmentBonuses bonuses)
        {
            if (physicalPercent < 25 || physicalPercent > 200 || smitePercent < 25 || smitePercent > 200 ||
                healingPercent < 25 || healingPercent > 200 || guardReductionPercent < 0 || guardReductionPercent > 50)
                throw new ArgumentOutOfRangeException("Guardian profile percentages are outside their bounds.");
            PhysicalPercent = physicalPercent;
            SmitePercent = smitePercent;
            HealingPercent = healingPercent;
            GuardReductionPercent = guardReductionPercent;
            Bonuses = bonuses ?? new GuardianEquipmentBonuses();
        }

        internal static int Scale(int value, int percent)
        {
            if (value <= 0) return value;
            return (int)Math.Min(int.MaxValue, ((long)value * percent + 50) / 100);
        }
    }
}
