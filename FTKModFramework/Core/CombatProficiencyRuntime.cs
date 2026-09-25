using System;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class CombatProficiencyRuntime
    {
        internal static void Prepare(ref AttackAttempt attempt, bool consumable)
        {
            CharacterDummy attacker = attempt.m_AttackingDummy;
            if (consumable || attacker == null || attacker.m_CharacterOverworld == null ||
                !attacker.m_CharacterOverworld.IsOwner || !(attempt.m_DamagedDummy is EnemyDummy)) return;
            int[] outcomes;
            if (CombatProficiencyRegistry.TryRandom((int)attempt.m_AttackProficiency, out outcomes))
            {
                // InitAttackTimeline receives the master's entry IDs on every peer. Its visual
                // copy changes only TTA spacing; unlike MC's initially recomputed list, these IDs
                // originate in that RPC. Read identity only and require the acting PID to match.
                EncounterSession encounter = EncounterSession.Instance;
                if (encounter == null || encounter.m_Random == null || encounter.m_FightOrderVisual == null ||
                    encounter.m_FightOrderVisual.Count == 0 || encounter.m_FightOrderVisual[0].m_EntryID < 0 ||
                    encounter.m_FightOrderVisual[0].m_Pid != attacker.FID)
                {
                    // A missing authoritative identity must not silently make the armor branch certain.
                    attempt.m_ProfSuccess = false;
                    Plugin.Log.LogWarning("[combat-proficiency] No matching synchronized timeline entry; debuff suppressed.");
                    return;
                }
                int branch = CombatProficiencyPolicy.DebuffBranch(encounter.m_Random.OriginalSeed,
                    encounter.m_EncounterIndex, encounter.m_FightOrderVisual[0].m_EntryID,
                    attacker.FID.m_TurnIndex, attacker.FID.m_PhotonID);
                attempt.m_AttackProficiency = (FTK_proficiencyTable.ID)outcomes[branch];
            }
            // Native construction subsequently applies misses, immunity, resistance, crits and death.
            // The selected proficiency and calculated damage travel in the existing outcome RPC.
        }

        internal static void ApplyDamageBonus(AttackAttempt attempt, bool consumable, ref float multiplier)
        {
            CharacterDummy attacker = attempt.m_AttackingDummy;
            if (consumable || attacker == null || attacker.m_CharacterOverworld == null ||
                !attacker.m_CharacterOverworld.IsOwner || !(attempt.m_DamagedDummy is EnemyDummy)) return;
            // Resolve each native victim separately, including secondary victims created by Justice.
            multiplier *= DamageBonus(attempt.m_AttackProficiency, attempt.m_DamagedDummy);
        }

        internal static float DamageBonus(FTK_proficiencyTable.ID action, CharacterDummy victim)
        {
            CombatProficiencyRegistry.ResistanceBonus bonus;
            CharacterDummy.ProficiencyRecord record;
            if (victim == null || !CombatProficiencyRegistry.TryBonus((int)action, out bonus) ||
                !victim.m_SufferingProficiencies.TryGetValue(ProficiencyBase.Category.Resist, out record) ||
                record == null || record.m_Proficiency == null) return 1f;
            return CombatProficiencyPolicy.Qualifies((int)record.m_Proficiency.m_ProficiencyID,
                record.m_Count, record.m_Proficiency.m_CustomValue, bonus.Sources) ? bonus.Multiplier : 1f;
        }

        internal static void ShowPreview(uiBattleStanceButtons owner, uiBattleButton button)
        {
            if (owner == null || owner.CombatCow == null || button == null || EncounterSession.Instance == null ||
                button.m_ButtonType != uiBattleButton.BattleButtonType.proficiency) return;
            foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
            {
                if (entry.m_Button != button) continue;
                EnemyDummy enemy = EncounterSession.Instance.GetCurrentEnemy();
                float bonus = DamageBonus(entry.m_Prof, enemy);
                if (bonus == 1f || enemy == null) return;
                FTK_proficiencyTable row = FTK_proficiencyTableDB.Get(entry.m_Prof);
                int damage = FTKUtil.RoundToInt(owner.CombatCow.m_CharacterStats.GetWeaponMaxDamage(enemy.m_EnemyCombat.m_RaceTypes) * row.m_DmgMultiplier * bonus);
                if (enemy.Frozen) damage = FTKUtil.RoundToInt(damage * GameFlow.Instance.m_FrozenDmgPercent);
                owner.m_InfoPanel.m_DamageValue.text = damage.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }
        }
    }
}
