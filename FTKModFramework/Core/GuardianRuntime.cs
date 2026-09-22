using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class GuardianRuntime
    {
        internal const string ActionKey = "ftkmf_guard_ally";
        private static HashSet<int> Classes = new HashSet<int>();
        internal static GuardianCombatState State = new GuardianCombatState();
        internal static FTK_proficiencyTable.ID ActionId = FTK_proficiencyTable.ID.None;
        internal static bool Enabled { get { return Classes.Count > 0; } }
        private static long attackSerial;
        private static long turnSerial;
        private static readonly GuardianEquipmentBonuses NoBonuses = new GuardianEquipmentBonuses();
        private static Dictionary<int, GuardianEquipmentBonuses> Equipment = new Dictionary<int, GuardianEquipmentBonuses>();
        private static Dictionary<string, GuardianEquipmentBonuses> AttackEquipment =
            new Dictionary<string, GuardianEquipmentBonuses>(StringComparer.Ordinal);
        private static readonly PlayerInventory.ContainerID[] EquipmentSlots = new PlayerInventory.ContainerID[]
        {
            PlayerInventory.ContainerID.Belt, PlayerInventory.ContainerID.Trinket, PlayerInventory.ContainerID.Neck,
            PlayerInventory.ContainerID.LeftHand, PlayerInventory.ContainerID.RightHand, PlayerInventory.ContainerID.Foot,
            PlayerInventory.ContainerID.Body, PlayerInventory.ContainerID.Head
        };

        private sealed class Healing
        {
            internal string Attack;
            internal string Ally;
            internal int FocusBonusPercent;
            internal readonly HashSet<string> Victims = new HashSet<string>(StringComparer.Ordinal);
        }

        private static Dictionary<string, Healing> PendingHealing = new Dictionary<string, Healing>(StringComparer.Ordinal);
        private static Dictionary<string, string> PendingFeedback = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static int ReloadClassCount { get { return Classes.Count; } }
        internal static int ReloadEquipmentCount { get { return Equipment.Count; } }
        internal static bool ReloadTransientStateEmpty
        {
            get { return attackSerial == 0 && turnSerial == 0 && PendingHealing.Count == 0 &&
                PendingFeedback.Count == 0 && AttackEquipment.Count == 0 && State.ReloadIsEmpty; }
        }

        internal static Action SuspendForReload()
        {
            HashSet<int> classes = Classes;
            GuardianCombatState state = State;
            FTK_proficiencyTable.ID action = ActionId;
            Dictionary<int, GuardianEquipmentBonuses> equipment = Equipment;
            Dictionary<string, GuardianEquipmentBonuses> attackEquipment = AttackEquipment;
            Dictionary<string, Healing> healing = PendingHealing;
            Dictionary<string, string> feedback = PendingFeedback;
            long attacks = attackSerial, turns = turnSerial;
            Classes = new HashSet<int>(); State = new GuardianCombatState();
            ActionId = FTK_proficiencyTable.ID.None;
            Equipment = new Dictionary<int, GuardianEquipmentBonuses>();
            AttackEquipment = new Dictionary<string, GuardianEquipmentBonuses>(StringComparer.Ordinal);
            PendingHealing = new Dictionary<string, Healing>(StringComparer.Ordinal);
            PendingFeedback = new Dictionary<string, string>(StringComparer.Ordinal);
            attackSerial = turnSerial = 0;
            return delegate
            {
                Classes = classes; State = state; ActionId = action; Equipment = equipment;
                AttackEquipment = attackEquipment; PendingHealing = healing; PendingFeedback = feedback;
                attackSerial = attacks; turnSerial = turns;
            };
        }

        internal static void RegisterEquipment(int itemId, GuardianEquipmentBonuses bonuses)
        {
            if (!Equipment.ContainsKey(itemId)) Equipment.Add(itemId, bonuses);
        }

        internal static bool IsGuardianClass(int classId) { return Classes.Contains(classId); }

        internal static string EquipmentDescription(int itemId)
        {
            GuardianEquipmentBonuses bonuses;
            if (!Equipment.TryGetValue(itemId, out bonuses)) return string.Empty;
            List<string> names = new List<string>();
            foreach (int classId in Classes)
            {
                FTK_playerGameStart row = Content.Db<FTK_playerGameStartDB>().GetEntryByInt(classId);
                if (row != null) names.Add(row.GetDisplayName());
            }
            names.Sort(StringComparer.Ordinal);
            return GuardianEquipmentDescription.Format(bonuses, string.Join(", ", names.ToArray()));
        }

        private static GuardianEquipmentBonuses EquippedBonuses(CharacterDummy guardian)
        {
            if (!IsGuardian(guardian) || guardian.m_CharacterOverworld.m_PlayerInventory == null) return NoBonuses;
            GuardianEquipmentBonuses result = NoBonuses;
            foreach (PlayerInventory.ContainerID slot in EquipmentSlots)
                foreach (KeyValuePair<FTK_itembase.ID, int> equipped in guardian.m_CharacterOverworld.m_PlayerInventory.Get(slot).m_CountDictionary)
                {
                    GuardianEquipmentBonuses bonuses;
                    if (equipped.Value > 0 && Equipment.TryGetValue((int)equipped.Key, out bonuses))
                        result = GuardianEquipmentBonuses.Strongest(result, bonuses);
                }
            return result;
        }

        private static GuardianEquipmentBonuses ProtectionBonuses(string targetId)
        {
            GuardianEquipmentBonuses result;
            if (AttackEquipment.TryGetValue(targetId, out result)) return result;
            result = NoBonuses;
            foreach (string guardianId in State.ActiveGuardians(targetId))
            {
                CharacterDummy guardian = Find(guardianId);
                if (CanAct(guardian)) result = GuardianEquipmentBonuses.Strongest(result, EquippedBonuses(guardian));
            }
            AttackEquipment.Add(targetId, result);
            return result;
        }

        internal static bool RegisterClass(int classId)
        {
            if (Classes.Contains(classId)) return true;
            if (ActionId == FTK_proficiencyTable.ID.None)
            {
                GuardianProficiency behavior = (GuardianProficiency)BehaviorHost.Create(typeof(GuardianProficiency), ActionKey);
                if (behavior == null) return false;
                behavior.m_Category = ProficiencyBase.Category.None;
                Content.AddProficiency(Plugin.Guid, ActionKey, FTK_proficiencyTable.ID.taunt, "Guard",
                    delegate(FTK_proficiencyTable p)
                    {
                        p.m_ProficiencyPrefab = behavior;
                        p.m_TargetFriendly = true;
                        p.m_Target = CharacterDummy.TargetType.PickFriendly;
                        p.m_Harmless = true;
                        p.m_FullSlots = false;
                        p.m_DmgMultiplier = 0;
                        p.m_CustomValue = 0;
                        p.m_ChanceToAffect = 1;
                        p.m_GunShot = false;
                        p.m_Suicide = false;
                        p.m_RepeatCount = 0;
                    });
                int id = Content.Db<FTK_proficiencyTableDB>().GetIntFromID(ActionKey);
                if (id < 0) return false;
                ActionId = (FTK_proficiencyTable.ID)id;
                Localization.SetProficiencyDescription(ActionKey,
                    "Always protects another ally from half of direct attack damage until your next turn. " +
                    "Incapacitation ends protection. Once per combat, active Guard prevents a lethal hit, leaving 1 HP. " +
                    "Focused hits heal your chosen ally for 8% maximum HP plus equipment bonuses.");
            }
            Classes.Add(classId);
            return true;
        }

        internal static bool IsGuardian(CharacterDummy dummy)
        {
            return dummy != null && dummy.m_CharacterOverworld != null &&
                dummy.m_CharacterOverworld.m_CharacterStats != null &&
                Classes.Contains((int)dummy.m_CharacterOverworld.m_CharacterStats.m_CharacterClass);
        }

        internal static bool CanAct(CharacterDummy dummy)
        {
            return dummy != null && dummy.m_CharacterOverworld != null && dummy.m_IsAlive &&
                dummy.GetCurrentHealth() > 0 && !dummy.Stunned && !dummy.Petrified && !dummy.m_DidFlee &&
                dummy.m_CharacterOverworld.m_CharacterStats.m_IsInCombat;
        }

        internal static bool LivingAlly(CharacterDummy dummy)
        {
            return dummy != null && dummy.m_CharacterOverworld != null && dummy.m_IsAlive &&
                dummy.GetCurrentHealth() > 0 && !dummy.m_DidFlee &&
                dummy.m_CharacterOverworld.m_CharacterStats.m_IsInCombat;
        }

        internal static string Identity(CharacterDummy dummy)
        {
            if (dummy == null) return null;
            return Identity(dummy.FID);
        }

        private static string Identity(FTKPlayerID fid)
        {
            return fid.m_TurnIndex + ":" + fid.m_PhotonID;
        }

        internal static CharacterDummy Find(string identity)
        {
            if (identity == null || EncounterSession.Instance == null) return null;
            foreach (CharacterDummy dummy in EncounterSession.Instance.m_PlayerDummies.Values)
                if (Identity(dummy) == identity) return dummy;
            return null;
        }

        internal static bool HasTarget(CharacterDummy guardian)
        {
            if (!CanAct(guardian) || EncounterSession.Instance == null) return false;
            foreach (CharacterDummy target in EncounterSession.Instance.GetOtherCombatPlayerMembers(guardian))
                if (LivingAlly(target)) return true;
            return false;
        }

        internal static string StatusDescription(CharacterDummy guardian)
        {
            if (!IsGuardian(guardian)) return string.Empty;
            string id = Identity(guardian);
            CharacterDummy ally = Find(State.DesignatedAlly(id));
            string chosen = ally == null ? "None" : ally.m_CharacterOverworld.m_CharacterStats.m_CharacterName;
            return "Chosen ally: " + chosen + ". Protection: " +
                (State.IsActive(id) && CanAct(guardian) ? "active (50%)." : "inactive.") +
                " Divine Intervention: " + (State.RescueAvailable(id) ? "ready." : "spent this combat.");
        }

        internal static void ApplyGuard(CharacterDummy guardian, CharacterDummy target)
        {
            if (!IsGuardian(guardian) || EncounterSession.Instance == null) return;
            bool partyMember = EncounterSession.Instance.GetOtherCombatPlayerMembers(guardian).Contains(target);
            if (!State.TryGuard(Identity(guardian), Identity(target), CanAct(guardian), partyMember && LivingAlly(target))) return;
            if (target.m_DamageInfo != null)
            {
                int originalHealth = target.m_DamageInfo.m_NewHealth;
                int healedHealth = State.ResolveGuardHealingHealth(Identity(guardian),
                    turnSerial.ToString(System.Globalization.CultureInfo.InvariantCulture), Identity(target), originalHealth,
                    target.m_CharacterOverworld.m_CharacterStats.MaxHealth, EquippedBonuses(guardian).GuardHealPercent);
                // AddToDummy runs before RespondToHit applies this exact outcome's health. Use that
                // native all-peer application point instead of a heal which would be overwritten.
                target.m_DamageInfo.m_NewHealth = healedHealth;
                if (healedHealth > originalHealth) target.SpawnHudTextRPC("Guard +" + (healedHealth - originalHealth), string.Empty);
            }
            target.SpawnHudTextRPC("Guarded", string.Empty);
            EncounterSession.Instance.AddCombatEventToActiveLogEntry(guardian.GameLogID + " guards " + target.GameLogID);
        }

        internal static void Expire(CharacterDummy guardian)
        {
            if (guardian == null || guardian.m_CharacterOverworld == null) return;
            string identity = Identity(guardian);
            State.ExpireGuard(identity);
            PendingHealing.Remove(identity);
        }

        internal static void ResetActor(CharacterDummy dummy)
        {
            if (dummy == null || dummy.m_CharacterOverworld == null) return;
            string identity = Identity(dummy);
            State.ResetActor(identity);
            PendingHealing.Remove(identity);
            PendingFeedback.Remove(identity);
        }

        internal static void Reset(CharacterDummy dummy)
        {
            if (dummy == null || dummy.m_CharacterOverworld == null) return;
            string identity = Identity(dummy);
            State.ResetGuardian(identity);
            // This is a new native combat, so no previous attack's defensive snapshot applies.
            AttackEquipment.Clear();
            PendingHealing.Remove(identity);
            PendingFeedback.Remove(identity);
        }

        internal static void BeginTurn(CharacterDummy actor)
        {
            State.BeginTurn();
            turnSerial++;
            PendingFeedback.Clear();
            PendingHealing.Clear();
            AttackEquipment.Clear();
            Expire(actor);
        }

        internal static void RefreshEligibility()
        {
            if (EncounterSession.Instance == null) return;
            foreach (CharacterDummy guardian in EncounterSession.Instance.m_PlayerDummies.Values)
                if (!CanAct(guardian)) Expire(guardian);
        }

        internal static int ResolveDamage(CharacterDummy attacker, DummyDamageInfo result)
        {
            // Native attack calculation can run on the victim's owner. Replay this deterministic
            // transform on each peer's private copy of the received outcome, including rescue charges.
            if (!(attacker is EnemyDummy) || result == null ||
                result.m_AttackResponse == CharacterDummy.AttackResponse.Dodge ||
                result.m_AttackResponse == CharacterDummy.AttackResponse.BlackHole) return 0;
            CharacterDummy victim = EncounterSession.Instance.GetDummyByFID(result.m_VictimID);
            if (victim == null || victim.m_CharacterOverworld == null) return 0;
            GuardianEquipmentBonuses bonuses = ProtectionBonuses(Identity(victim));
            if (bonuses.WardDebuffs && result.m_ProfAffect && result.m_Prof != FTK_proficiencyTable.ID.None)
            {
                ProficiencyBase proficiency = ProficiencyManager.Instance.Get(result.m_Prof);
                if (proficiency != null && (proficiency.m_Category == ProficiencyBase.Category.Poison ||
                    proficiency.m_Category == ProficiencyBase.Category.Stunned || proficiency.m_Category == ProficiencyBase.Category.Dazed ||
                    proficiency.m_Category == ProficiencyBase.Category.Curse))
                {
                    result.m_ProfAffect = false;
                    result.m_ProfImmune = true;
                }
            }
            if (result.m_Damage <= 0) return 0;
            bool nativeRescue = result.m_AttackResponse == CharacterDummy.AttackResponse.ResistDeath ||
                result.m_AttackResponse == CharacterDummy.AttackResponse.ResistDeathSanctum;
            // Ordinary outcomes preserve their exact pre-hit HP. Lethal and native rescue outcomes
            // discard that field, so those require the synchronized pre-impact health snapshot.
            int health = result.m_NewHealth > 0 && !nativeRescue
                ? (int)Math.Min(int.MaxValue, (long)result.m_NewHealth + result.m_Damage)
                : victim.GetCurrentHealth();
            GuardianCombatState.DamageResult resolved;
            // Native rescue keeps precedence only when Guard alone cannot prevent lethal damage.
            string attackId = turnSerial.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Identity(attacker);
            if (!State.TryResolveAttackDamage(attackId, Identity(victim), health, result.m_Damage, !nativeRescue, out resolved) ||
                !resolved.Guarded) return 0;
            result.m_Damage = resolved.Damage;
            // m_Damage already includes critical damage; m_CritDamage is presentation metadata.
            result.m_CritDamage = Math.Min(result.m_CritDamage / 2, result.m_Damage);
            if (!nativeRescue || resolved.RemainingHealth > 0)
            {
                result.m_NewHealth = resolved.RemainingHealth;
                if (result.m_NewHealth > 0 && (result.m_AttackResponse == CharacterDummy.AttackResponse.Death || nativeRescue))
                    result.m_AttackResponse = CharacterDummy.AttackResponse.DamagedHeavy;
            }
            PendingFeedback[Identity(victim)] = resolved.Rescuer == null ? "Guard" : "Divine Intervention";
            return bonuses.RetaliationDamage;
        }

        internal static void QueueHealing(AttackAttempt attempt, DummyDamageInfo first, DummyDamageInfo second, DummyDamageInfo third)
        {
            CharacterDummy attacker = attempt.m_AttackingDummy;
            if (!IsGuardian(attacker)) return;
            string identity = Identity(attacker);
            PendingHealing.Remove(identity);
            // CalculateAttack runs on the acting player's owner, which sends the native attack outcome.
            if (!attacker.m_CharacterOverworld.IsOwner || !GuardianFocusedHit.EligibleAttempt(
                attempt.m_AttackFocused, attempt.m_SlotSuccessPercent, attempt.m_Harmless,
                attempt.m_CheatType == SlotControl.AttackCheatType.Miss) || !CanAct(attacker)) return;
            string ally = State.DesignatedAlly(identity);
            if (!LivingAlly(Find(ally))) return;
            Healing pending = new Healing();
            pending.Attack = (++attackSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            pending.Ally = ally;
            pending.FocusBonusPercent = EquippedBonuses(attacker).FocusHealBonusPercent;
            AddHealingVictim(pending, first);
            AddHealingVictim(pending, second);
            AddHealingVictim(pending, third);
            if (pending.Victims.Count > 0) PendingHealing[identity] = pending;
        }

        private static void AddHealingVictim(Healing pending, DummyDamageInfo damage)
        {
            // Qualification is established before playback, then confirmed at the actual impact.
            if (damage != null && GuardianFocusedHit.Landed(damage.m_Damage, damage.m_AttackResponse))
                pending.Victims.Add(Identity(damage.m_VictimID));
        }

        internal static void OnImpact(CharacterDummy victim)
        {
            if (victim == null || victim.m_DamageInfo == null) return;
            DummyDamageInfo damage = victim.m_DamageInfo;
            string victimId = Identity(victim);
            string feedback;
            if (PendingFeedback.TryGetValue(victimId, out feedback))
            {
                PendingFeedback.Remove(victimId);
                victim.SpawnHudTextRPC(feedback, string.Empty);
                EncounterSession.Instance.AddCombatEventToActiveLogEntry(feedback + " protected " + victim.GameLogID);
            }
            string attackerId = Identity(damage.m_AttackerID);
            Healing pending;
            if (!PendingHealing.TryGetValue(attackerId, out pending) || !pending.Victims.Contains(victimId)) return;
            PendingHealing.Remove(attackerId);
            CharacterDummy attacker = Find(attackerId);
            CharacterDummy ally = Find(pending.Ally);
            if (!CanAct(attacker) || !LivingAlly(ally) || !attacker.m_CharacterOverworld.IsOwner) return;
            CharacterStats stats = ally.m_CharacterOverworld.m_CharacterStats;
            int amount = State.ResolveFocusedHitHealing(attackerId, pending.Attack, pending.Ally,
                stats.m_HealthCurrent, stats.MaxHealth, true, true,
                GuardianFocusedHit.Landed(damage.m_Damage, damage.m_AttackResponse), pending.FocusBonusPercent);
            if (amount > 0) stats.GainSpecificHealth(amount, true, true);
        }
    }

    // Native AddProfToDummy applies this on every peer. Never call base: the shared cached
    // proficiency object must not own per-character Guard state or create vanilla status records.
    public sealed class GuardianProficiency : ProficiencyBase
    {
        public override bool IsImmune(CharacterDummy dummy) { return false; }
        public override bool IsIgnore(CharacterDummy dummy) { return false; }
        public override void AddToDummy(CharacterDummy dummy)
        {
            try { GuardianRuntime.ApplyGuard(GetAttacker(dummy), dummy); }
            catch (Exception e) { Plugin.Log.LogError("[guardian] Guard application failed: " + e); }
        }
    }
}
