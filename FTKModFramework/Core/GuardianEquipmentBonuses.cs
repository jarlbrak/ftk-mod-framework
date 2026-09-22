using System;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Equipment perks for classes registered with AddGuardian. Multiple equipped items use the
    /// strongest value for each perk. These never increase Guard's 50 percent damage reduction.
    /// </summary>
    public sealed class GuardianEquipmentBonuses
    {
        public int GuardHealPercent { get; private set; }
        public int FocusHealBonusPercent { get; private set; }
        public int RetaliationDamage { get; private set; }
        /// <summary>Active Guard blocks direct-attack Poison, Stunned, Dazed, and Curse proficiencies.</summary>
        public bool WardDebuffs { get; private set; }
        public int GuardFocusRestore { get; private set; }
        public bool GuardReckoning { get; private set; }
        public bool GuardCleanse { get; private set; }

        // Preserve the constructor token used by previously compiled behavior mods.
        public GuardianEquipmentBonuses(int guardHealPercent, int focusHealBonusPercent,
            int retaliationDamage, bool wardDebuffs)
            : this(guardHealPercent, focusHealBonusPercent, retaliationDamage, wardDebuffs, 0, false, false) { }

        public GuardianEquipmentBonuses(int guardHealPercent = 0, int focusHealBonusPercent = 0,
            int retaliationDamage = 0, bool wardDebuffs = false, int guardFocusRestore = 0,
            bool guardReckoning = false, bool guardCleanse = false)
        {
            if (guardHealPercent < 0 || guardHealPercent > 20 || focusHealBonusPercent < 0 ||
                focusHealBonusPercent > 20 || retaliationDamage < 0 || retaliationDamage > 20)
                throw new ArgumentOutOfRangeException("Guardian equipment values must be between 0 and 20.");
            if (guardFocusRestore < 0 || guardFocusRestore > 1)
                throw new ArgumentOutOfRangeException("guardFocusRestore", "Guard restores at most one Focus.");
            GuardHealPercent = guardHealPercent;
            FocusHealBonusPercent = focusHealBonusPercent;
            RetaliationDamage = retaliationDamage;
            WardDebuffs = wardDebuffs;
            GuardFocusRestore = guardFocusRestore;
            GuardReckoning = guardReckoning;
            GuardCleanse = guardCleanse;
        }

        internal static GuardianEquipmentBonuses Strongest(GuardianEquipmentBonuses first, GuardianEquipmentBonuses second)
        {
            if (first == null) return second;
            if (second == null) return first;
            return new GuardianEquipmentBonuses(Math.Max(first.GuardHealPercent, second.GuardHealPercent),
                Math.Max(first.FocusHealBonusPercent, second.FocusHealBonusPercent),
                Math.Max(first.RetaliationDamage, second.RetaliationDamage), first.WardDebuffs || second.WardDebuffs,
                Math.Max(first.GuardFocusRestore, second.GuardFocusRestore),
                first.GuardReckoning || second.GuardReckoning, first.GuardCleanse || second.GuardCleanse);
        }

        internal static int HealAmount(int currentHealth, int maxHealth, int percent)
        {
            if (currentHealth <= 0 || maxHealth < currentHealth || percent < 0 || percent > 100) return 0;
            return Math.Min((int)((long)maxHealth * percent / 100), maxHealth - currentHealth);
        }

        internal static int AddRetaliation(int nativeHealthMod, params int[] targetBonuses)
        {
            int amount = 0;
            foreach (int value in targetBonuses) if (value >= 0 && value <= 20) amount = Math.Max(amount, value);
            return (int)Math.Min(int.MaxValue, (long)nativeHealthMod + amount);
        }
    }
}
