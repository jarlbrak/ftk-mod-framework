using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static partial class GuardianRuntime
    {
        internal static readonly GuardianLegendaryState Legendary = new GuardianLegendaryState();
        private static readonly Dictionary<string, string> PendingGuardFocus = new Dictionary<string, string>(StringComparer.Ordinal);

        private static string CurrentAction(CharacterDummy actor)
        {
            return turnSerial.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Identity(actor);
        }

        private static GuardianEquipmentBonuses WeaponBonuses(CharacterDummy guardian)
        {
            GuardianEquipmentBonuses bonuses;
            return IsGuardian(guardian) && Equipment.TryGetValue((int)guardian.m_CharacterOverworld.m_WeaponID, out bonuses)
                ? bonuses : NoBonuses;
        }

        internal static void ObserveLegendaryEquipment(CharacterDummy guardian)
        {
            if (!IsGuardian(guardian)) return;
            GuardianEquipmentBonuses bonuses = WeaponBonuses(guardian);
            int item = (int)guardian.m_CharacterOverworld.m_WeaponID;
            Legendary.ObserveEquipment(Identity(guardian), bonuses.GuardFocusRestore > 0 ? item : -1,
                bonuses.GuardReckoning ? item : -1);
        }

        private static void ApplyLegendaryGuard(CharacterDummy guardian, CharacterDummy target)
        {
            GuardianEquipmentBonuses weapon = WeaponBonuses(guardian);
            int item = (int)guardian.m_CharacterOverworld.m_WeaponID;
            if (!Legendary.BeginGuard(Identity(guardian), CurrentAction(guardian), Identity(target),
                weapon.GuardFocusRestore > 0 ? item : -1, weapon.GuardReckoning ? item : -1)) return;
            if (EquippedBonuses(guardian).GuardCleanse) CleanseGuardTarget(target);
        }

        private static void CleanseGuardTarget(CharacterDummy target)
        {
            CharacterStats stats = target.m_CharacterOverworld.m_CharacterStats;
            int choice = GuardianLegendaryState.CleanseChoice(
                target.m_SufferingProficiencies.ContainsKey(ProficiencyBase.Category.Stunned),
                target.m_SufferingProficiencies.ContainsKey(ProficiencyBase.Category.Dazed),
                stats.m_ActiveCurses.Count > 0, stats.IsPoisoned);
            switch (choice)
            {
                case 1: target.RemoveSpecificProficiency(ProficiencyBase.Category.Stunned); break;
                case 2: target.RemoveSpecificProficiency(ProficiencyBase.Category.Dazed); break;
                case 3:
                    List<CharacterStats.CurseType> remaining = new List<CharacterStats.CurseType>(stats.m_ActiveCurses);
                    remaining.Sort();
                    remaining.RemoveAt(0);
                    // Apply the native curse snapshot locally on every peer. This also refreshes
                    // speed/stat penalties and FX without broadcasting a second Guard effect.
                    stats.BroadcastAllCursesRPC(remaining.ToArray(), stats.m_PermaCurses.ToArray());
                    break;
                case 4: stats.SetPoison(-stats.m_PoisonLvl, false, false); break;
            }
            if (choice != 0 && target.m_CharacterOverworld.IsOwner)
                target.SpawnHudTextRPC("Stand Firm", string.Empty);
        }

        private static void ResolveLegendaryMitigation(string attackId, CharacterDummy victim, int original, int reduced)
        {
            string target = Identity(victim);
            string[] active = State.ActiveGuardians(target);
            foreach (string id in active) ObserveLegendaryEquipment(Find(id));
            List<string> newlyCharged = new List<string>();
            foreach (string id in active) if (!Legendary.IsCharged(id)) newlyCharged.Add(id);
            if (Legendary.ResolveMitigation(attackId, target, original, reduced, active)) PendingGuardFocus[target] = attackId;
            foreach (string id in newlyCharged)
            {
                CharacterDummy guardian = Find(id);
                if (Legendary.IsCharged(id) && guardian != null && guardian.m_CharacterOverworld.IsOwner)
                    guardian.SpawnHudTextRPC("Reckoning ready", string.Empty);
            }
        }

        private static void ApplyGuardFocusAtImpact(CharacterDummy victim)
        {
            if (victim.m_DamageInfo == null || EncounterSession.Instance == null) return;
            CharacterDummy attacker = EncounterSession.Instance.GetDummyByFID(victim.m_DamageInfo.m_AttackerID);
            string receipt;
            if (!(attacker is EnemyDummy) || !PendingGuardFocus.TryGetValue(Identity(victim), out receipt) ||
                receipt != CurrentAction(attacker)) return;
            PendingGuardFocus.Remove(Identity(victim));
            if (!LivingAlly(victim) || !victim.m_CharacterOverworld.IsOwner) return;
            CharacterStats stats = victim.m_CharacterOverworld.m_CharacterStats;
            if (GuardianLegendaryState.FocusGain(stats.m_FocusPoints, stats.MaxFocus) > 0)
            {
                // UpdateFocusPoints has no native owner guard. One authoritative grant uses the
                // game's existing member synchronization; all peers have spent the same receipt.
                stats.UpdateFocusPoints(1, true);
                victim.SpawnHudTextRPC("Unbroken Watch +1 Focus", string.Empty);
            }
        }

        private static bool ReckoningAttack(CharacterDummy guardian, CharacterDummy victim,
            FTK_proficiencyTable.ID proficiency, Weapon.WeaponType weaponType, CharacterDummy.SpecialAttack special)
        {
            if (!IsGuardian(guardian) || !(victim is EnemyDummy) || weaponType != Weapon.WeaponType.blunt ||
                special == CharacterDummy.SpecialAttack.Justice || special == CharacterDummy.SpecialAttack.ItemAttack ||
                !WeaponBonuses(guardian).GuardReckoning) return false;
            if (proficiency == FTK_proficiencyTable.ID.None) return true;
            FTK_proficiencyTable row = FTK_proficiencyTableDB.Get(proficiency);
            // Read the authored target, not the failed-slot fallback or m_IsAOE name heuristic.
            return row != null && row.m_Target == CharacterDummy.TargetType.None && !row.m_Harmless &&
                !row.m_TargetFriendly && row.m_DmgMultiplier > 0;
        }

        internal static void PrepareReckoning(AttackAttempt attempt, bool consumable, ref float damageMultiplier)
        {
            CharacterDummy guardian = attempt.m_AttackingDummy;
            if (!IsGuardian(guardian)) return;
            ObserveLegendaryEquipment(guardian);
            if (consumable || attempt.m_Harmless || !CanAct(guardian) ||
                !guardian.m_CharacterOverworld.IsOwner) return;
            bool eligible = ReckoningAttack(guardian, attempt.m_DamagedDummy, attempt.m_AttackProficiency,
                attempt.m_WeaponType, attempt.m_SpecialAttack);
            if (Legendary.BeginAttack(Identity(guardian), CurrentAction(guardian), eligible)) damageMultiplier *= 1.5f;
        }

        internal static void ReplayReckoning(CharacterDummy guardian, DummyDamageInfo damage)
        {
            if (damage == null || !IsGuardian(guardian) || EncounterSession.Instance == null) return;
            ObserveLegendaryEquipment(guardian);
            bool eligible = ReckoningAttack(guardian, EncounterSession.Instance.GetDummyByFID(damage.m_VictimID),
                damage.m_Prof, damage.m_WeaponType, damage.m_SpecialAttack);
            if (Legendary.BeginAttack(Identity(guardian), CurrentAction(guardian), eligible) && guardian.m_CharacterOverworld.IsOwner)
                guardian.SpawnHudTextRPC("Reckoning", string.Empty);
        }

        internal static void ShowReckoningPreview(uiBattleStanceButtons owner, uiBattleButton button)
        {
            if (owner == null || owner.CombatCow == null || button == null || EncounterSession.Instance == null) return;
            CharacterDummy guardian = owner.CombatCow.GetCombatDummy();
            if (!IsGuardian(guardian)) return;
            ObserveLegendaryEquipment(guardian);
            if (!Legendary.IsCharged(Identity(guardian))) return;
            FTK_proficiencyTable.ID proficiency = FTK_proficiencyTable.ID.None;
            if (button.m_ButtonType == uiBattleButton.BattleButtonType.proficiency)
            {
                foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
                    if (entry.m_Button == button) proficiency = entry.m_Prof;
                if (proficiency == FTK_proficiencyTable.ID.None) return;
            }
            else if (button.m_ButtonType != uiBattleButton.BattleButtonType.attack) return;
            EnemyDummy enemy = EncounterSession.Instance.GetCurrentEnemy();
            if (guardian.m_EventListener == null || guardian.m_EventListener.m_Weapon == null ||
                !ReckoningAttack(guardian, enemy, proficiency, guardian.m_EventListener.m_Weapon.m_WeaponType,
                    guardian.m_SpecialAttack)) return;
            float multiplier = proficiency == FTK_proficiencyTable.ID.None ? 1f : FTK_proficiencyTableDB.Get(proficiency).m_DmgMultiplier;
            int damage = FTKUtil.RoundToInt(owner.CombatCow.m_CharacterStats.GetWeaponMaxDamage(enemy.m_EnemyCombat.m_RaceTypes) * multiplier * 1.5f);
            if (enemy.Frozen) damage = FTKUtil.RoundToInt(damage * GameFlow.Instance.m_FrozenDmgPercent);
            owner.m_InfoPanel.m_DamageValue.text = damage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Native description bounds already share the compact damage/accuracy panel. Keep
            // the charged indicator in the action title instead of adding another body line.
            owner.m_BattleActionDisplay.text += " + RECKONING";
        }

        internal static void EndLegendaryCombat(CharacterDummy actor)
        {
            // Native combat exit visits unused pooled dummies too. Their FID getter
            // dereferences an absent overworld character, and they own no Guardian state.
            if (actor == null || actor.m_CharacterOverworld == null) return;
            string identity = Identity(actor);
            Legendary.ResetActor(identity);
            PendingGuardFocus.Remove(identity);
        }
    }
}
