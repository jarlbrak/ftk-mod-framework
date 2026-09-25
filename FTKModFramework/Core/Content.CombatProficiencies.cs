using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Choose equally between two compatible armor/resistance debuffs using synchronized combat
        /// action identity. One outcome must be the registered source row itself. No native RNG is consumed.
        /// Returns false without changing registration when any row or compatibility check fails.
        /// </summary>
        public static bool SetRandomDebuffOutcomes(FTK_proficiencyTable row, params FTK_proficiencyTable[] outcomes)
        {
            int id;
            if (!RegisteredProficiency(row, out id) || outcomes == null || outcomes.Length != 2 ||
                CombatProficiencyRegistry.HasBonus(id)) return false;
            int[] ids = new int[2];
            bool self = false, armor = false, resist = false;
            for (int i = 0; i < outcomes.Length; i++)
            {
                if (!RegisteredProficiency(outcomes[i], out ids[i]) || !NativeDefenseDebuff(outcomes[i]) ||
                    !CompatibleDebuff(row, outcomes[i])) return false;
                if (ids[i] == id) self = true;
                armor |= outcomes[i].m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.Armor;
                resist |= outcomes[i].m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.Resist;
            }
            if (!self || !armor || !resist || ids[0] == ids[1]) return false;
            CombatProficiencyRegistry.SetRandom(id, ids);
            return true;
        }

        /// <summary>
        /// Multiply a single-target magic action's damage only while its victim has an active negative
        /// native resistance status originating from one of the supplied exact custom rows. The status
        /// is not consumed. Native resistance, Focus, critical and death resolution remain authoritative.
        /// </summary>
        public static bool SetResistanceDebuffDamageBonus(FTK_proficiencyTable row,
            FTK_proficiencyTable[] sources, float multiplier)
        {
            int id;
            if (!RegisteredProficiency(row, out id) || CombatProficiencyRegistry.HasRandom(id) ||
                sources == null || sources.Length == 0 || sources.Length > 16 ||
                !CombatProficiencyPolicy.ValidMultiplier(multiplier) || row.m_Harmless || row.m_TargetFriendly ||
                row.m_Target != CharacterDummy.TargetType.None || row.m_DmgTypeOverride != FTK_weaponStats2.DamageType.magic ||
                !(row.m_DmgMultiplier > 0) || float.IsInfinity(row.m_DmgMultiplier)) return false;
            int[] ids = new int[sources.Length];
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < sources.Length; i++)
                if (!RegisteredProficiency(sources[i], out ids[i]) || !seen.Add(ids[i]) ||
                    !NativeDefenseDebuff(sources[i]) || sources[i].m_ProficiencyPrefab.m_Category != ProficiencyBase.Category.Resist)
                    return false;
            CombatProficiencyRegistry.SetBonus(id, ids, multiplier);
            return true;
        }

        private static bool RegisteredProficiency(FTK_proficiencyTable row, out int id)
        {
            id = -1;
            return row != null && ContentRegistry.TryGetSyntheticId(row.m_ID, out id, typeof(FTK_proficiencyTableDB)) &&
                object.ReferenceEquals(Db<FTK_proficiencyTableDB>().GetEntryByInt(id), row);
        }

        private static bool NativeDefenseDebuff(FTK_proficiencyTable row)
        {
            ProficiencyBase behavior = row.m_ProficiencyPrefab;
            if (behavior == null || !(row.m_CustomValue <= -1) || float.IsInfinity(row.m_CustomValue) || row.m_RepeatCount <= 0 ||
                !(row.m_Quickness > 0) || float.IsInfinity(row.m_Quickness) || row.m_TargetFriendly ||
                !(row.m_DmgMultiplier > 0) || float.IsInfinity(row.m_DmgMultiplier) ||
                float.IsNaN(row.m_ChanceToAffect) || row.m_ChanceToAffect < 0 || row.m_ChanceToAffect > 1 ||
                row.m_Target != CharacterDummy.TargetType.None || row.m_Harmless) return false;
            return (behavior.GetType() == typeof(ProficiencyArmor) && behavior.m_Category == ProficiencyBase.Category.Armor) ||
                (behavior.GetType() == typeof(ProficiencyResist) && behavior.m_Category == ProficiencyBase.Category.Resist);
        }

        private static bool CompatibleDebuff(FTK_proficiencyTable a, FTK_proficiencyTable b)
        {
            if (!NativeDefenseDebuff(a)) return false;
            // Target, rolls, damage and duration are established before the branch is selected.
            return a.m_Target == b.m_Target && a.m_TargetFriendly == b.m_TargetFriendly &&
                a.m_Harmless == b.m_Harmless && a.m_DmgMultiplier == b.m_DmgMultiplier &&
                a.m_DmgTypeOverride == b.m_DmgTypeOverride && a.m_WpnTypeOverride == b.m_WpnTypeOverride &&
                a.m_FullSlots == b.m_FullSlots && a.m_SlotOverride == b.m_SlotOverride &&
                a.m_PerSlotSkillRoll == b.m_PerSlotSkillRoll && a.m_IgnoresArmor == b.m_IgnoresArmor &&
                a.m_ChanceToAffect == b.m_ChanceToAffect && a.m_CustomValue == b.m_CustomValue &&
                a.m_Quickness == b.m_Quickness && a.m_RepeatCount == b.m_RepeatCount &&
                a.m_GunShot == b.m_GunShot && a.m_Suicide == b.m_Suicide &&
                a.m_DamagePerAttack == b.m_DamagePerAttack && a.m_BoatDamage == b.m_BoatDamage &&
                a.m_ChaosOption == b.m_ChaosOption &&
                a.m_ProficiencyPrefab.m_IsEndOnTurn == b.m_ProficiencyPrefab.m_IsEndOnTurn;
        }
    }
}
