using System;

namespace FTKModFramework.Core
{
    /// <summary>The Perfect figure a combat action shows: the chance that every slot succeeds.
    /// Unity-free so Tests/Tweaks can check it. Display only; nothing here reaches a roll.</summary>
    internal static class PerfectChance
    {
        /// <summary>Statuses on the rolling character's combat dummy that decide unfocused slots
        /// before any skill roll in SlotControl.</summary>
        [Flags]
        internal enum SlotStatus
        {
            None = 0,
            Illuminated = 1,
            Darkness = 2,
            Shocked = 4,
        }

        internal static SlotStatus Statuses(bool illuminated, bool darkness, bool shocked)
        {
            SlotStatus statuses = SlotStatus.None;
            if (illuminated) statuses |= SlotStatus.Illuminated;
            if (darkness) statuses |= SlotStatus.Darkness;
            if (shocked) statuses |= SlotStatus.Shocked;
            return statuses;
        }

        /// <summary>CharacterStats.CalculateFullSkillChance: the per-slot skill value raised to the
        /// number of unfocused slots, rounded to hundredths as Mathf.Round does (to even), then
        /// clamped to [0, 1]. A focus-locked slot always succeeds, so it drops out of the power.</summary>
        internal static float Vanilla(float perSlotSkill, int slots, int spentFocus)
        {
            float chance = (float)Math.Pow(perSlotSkill, slots - spentFocus);
            chance = (float)Math.Round((double)(chance * 100f)) / 100f;
            if (chance < 0f) return 0f;
            return chance > 1f ? 1f : chance;
        }

        /// <summary>Applies the per-slot order that SlotControl.ComputeAttackSlotResults,
        /// ComputeFleeSlotResults and ComputeShieldTauntSlotResults share for the player: a slot
        /// below SpentFocus succeeds; otherwise Illuminated succeeds; otherwise Darkness fails;
        /// otherwise slot 0 fails when Shocked; only then does the skill roll. Distract, Royal
        /// Droll, encourage and critical strikes stay as vanilla shows them.</summary>
        internal static float WithStatuses(float vanillaChance, int slots, int spentFocus, SlotStatus statuses)
        {
            if (spentFocus >= slots) return vanillaChance;
            if ((statuses & SlotStatus.Illuminated) != 0) return 1f;
            if ((statuses & SlotStatus.Darkness) != 0) return 0f;
            if ((statuses & SlotStatus.Shocked) != 0 && spentFocus <= 0) return 0f;
            return vanillaChance;
        }

        /// <summary>The corrected Perfect figure from the per-slot skill value the roll uses.</summary>
        internal static float Full(float perSlotSkill, int slots, int spentFocus, SlotStatus statuses)
        {
            return WithStatuses(Vanilla(perSlotSkill, slots, spentFocus), slots, spentFocus, statuses);
        }
    }
}
