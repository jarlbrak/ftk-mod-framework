using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ThiefRuntime
    {
        internal enum WeaponKind { None, Paired, Bow, Pistol }
        internal enum ActionKind { None, Prepare, Pierce, Shot }
        internal enum ArtifactKind { None, BorrowedFortune, LastLight, LooseAndLeave }

        private sealed class PendingSneak
        {
            internal string TargetId;
            internal int TargetHealth;
            internal int WeaponId;
            internal int FocusSpent;
            internal bool Perfect;
            internal ArtifactKind Artifact;
            internal bool Prepared;
            internal string ProfileKey;
            internal int ProfileCount;
        }

        private sealed class PendingStrike
        {
            internal string AttackerId;
            internal float SlotSuccess;
        }

        private sealed class PendingWayfarer
        {
            internal string TargetId;
            internal int WeaponId;
        }

        private static HashSet<int> classes = new HashSet<int>();
        private static Dictionary<int, WeaponKind> weapons = new Dictionary<int, WeaponKind>();
        private static Dictionary<int, ActionKind> actions = new Dictionary<int, ActionKind>();
        private static Dictionary<int, ArtifactKind> artifacts = new Dictionary<int, ArtifactKind>();
        private static Dictionary<string, PendingSneak> pending = new Dictionary<string, PendingSneak>(StringComparer.Ordinal);
        private static Dictionary<string, PendingStrike> strikes = new Dictionary<string, PendingStrike>(StringComparer.Ordinal);
        private static Dictionary<string, PendingWayfarer> wayfarer = new Dictionary<string, PendingWayfarer>(StringComparer.Ordinal);
        private static Dictionary<string, int> observedWeapons = new Dictionary<string, int>(StringComparer.Ordinal);
        private static Dictionary<string, string> observedProfiles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static long attackSerial;
        private static ThiefCombatState state = new ThiefCombatState();
        private static ThiefEquipmentSets equipment = new ThiefEquipmentSets();
        internal const string SlipAwayKey = "ftkmf_thief_slip_away";
        internal const string EvasionSignalKey = "ftkmf_thief_evasion_signal";
        internal const string WayfarerPreviewKey = "ftkmf_thief_wayfarer_preview";
        internal const string WayfarerCoreKey = "ftkmf_thief_wayfarer_core";
        internal static FTK_proficiencyTable.ID SlipAwayId = FTK_proficiencyTable.ID.None;
        internal static FTK_proficiencyTable.ID EvasionSignalId = FTK_proficiencyTable.ID.None;
        internal static FTK_proficiencyTable.ID WayfarerPreviewId = FTK_proficiencyTable.ID.None;
        internal static FTK_proficiencyTable.ID WayfarerCoreId = FTK_proficiencyTable.ID.None;

        internal static bool Enabled { get { return classes.Count != 0; } }
        internal static int ReloadClassCount { get { return classes.Count; } }
        internal static int ReloadWeaponCount { get { return weapons.Count; } }
        internal static int ReloadActionCount { get { return actions.Count; } }
        internal static int ReloadArtifactCount { get { return artifacts.Count; } }
        internal static bool HasEquipmentRegistrations { get { return equipment.HasRegistrations; } }

        internal static Action SuspendForReload()
        {
            HashSet<int> oldClasses = classes;
            Dictionary<int, WeaponKind> oldWeapons = weapons;
            Dictionary<int, ActionKind> oldActions = actions;
            Dictionary<int, ArtifactKind> oldArtifacts = artifacts;
            Dictionary<string, PendingSneak> oldPending = pending;
            Dictionary<string, PendingStrike> oldStrikes = strikes;
            Dictionary<string, PendingWayfarer> oldWayfarer = wayfarer;
            Dictionary<string, int> oldObserved = observedWeapons;
            Dictionary<string, string> oldProfiles = observedProfiles;
            long oldAttackSerial = attackSerial;
            ThiefCombatState oldState = state;
            ThiefEquipmentSets oldEquipment = equipment;
            FTK_proficiencyTable.ID oldSlipAway = SlipAwayId;
            FTK_proficiencyTable.ID oldEvasionSignal = EvasionSignalId;
            FTK_proficiencyTable.ID oldWayfarerPreview = WayfarerPreviewId;
            FTK_proficiencyTable.ID oldWayfarerCore = WayfarerCoreId;
            classes = new HashSet<int>();
            weapons = new Dictionary<int, WeaponKind>();
            actions = new Dictionary<int, ActionKind>();
            artifacts = new Dictionary<int, ArtifactKind>();
            pending = new Dictionary<string, PendingSneak>(StringComparer.Ordinal);
            strikes = new Dictionary<string, PendingStrike>(StringComparer.Ordinal);
            wayfarer = new Dictionary<string, PendingWayfarer>(StringComparer.Ordinal);
            observedWeapons = new Dictionary<string, int>(StringComparer.Ordinal);
            observedProfiles = new Dictionary<string, string>(StringComparer.Ordinal);
            attackSerial = 0;
            state = new ThiefCombatState();
            equipment = new ThiefEquipmentSets();
            SlipAwayId = FTK_proficiencyTable.ID.None;
            EvasionSignalId = FTK_proficiencyTable.ID.None;
            WayfarerPreviewId = FTK_proficiencyTable.ID.None;
            WayfarerCoreId = FTK_proficiencyTable.ID.None;
            return delegate { classes = oldClasses; weapons = oldWeapons; actions = oldActions;
                artifacts = oldArtifacts; pending = oldPending; strikes = oldStrikes; wayfarer = oldWayfarer;
                observedWeapons = oldObserved;
                state = oldState; SlipAwayId = oldSlipAway; EvasionSignalId = oldEvasionSignal;
                attackSerial = oldAttackSerial; equipment = oldEquipment; observedProfiles = oldProfiles;
                WayfarerPreviewId = oldWayfarerPreview; WayfarerCoreId = oldWayfarerCore; };
        }

        internal static bool RegisterClass(int id)
        {
            if (SlipAwayId == FTK_proficiencyTable.ID.None)
            {
                ThiefSlipAwayProficiency behavior = (ThiefSlipAwayProficiency)BehaviorHost.Create(
                    typeof(ThiefSlipAwayProficiency), SlipAwayKey);
                if (behavior == null) return false;
                behavior.m_Category = ProficiencyBase.Category.None;
                Content.AddProficiency(Plugin.Guid, SlipAwayKey, FTK_proficiencyTable.ID.taunt, "Slip Away",
                    delegate(FTK_proficiencyTable p)
                    {
                        p.m_ProficiencyPrefab = behavior;
                        p.m_TargetFriendly = true;
                        p.m_Target = CharacterDummy.TargetType.None;
                        p.m_Harmless = true;
                        p.m_FullSlots = false;
                        p.m_DmgMultiplier = 0;
                        p.m_ChanceToAffect = 1;
                        p.m_RepeatCount = 0;
                    });
                int actionId = Content.Db<FTK_proficiencyTableDB>().GetIntFromID(SlipAwayKey);
                if (actionId < 0) return false;
                SlipAwayId = (FTK_proficiencyTable.ID)actionId;
                Localization.SetProficiencyDescription(SlipAwayKey,
                    "Once per combat. Spend your turn to prepare a strike and halve the next direct enemy attack before your next turn.");
            }
            if (EvasionSignalId == FTK_proficiencyTable.ID.None)
            {
                ThiefEvasionSignal behavior = (ThiefEvasionSignal)BehaviorHost.Create(
                    typeof(ThiefEvasionSignal), EvasionSignalKey);
                if (behavior == null) return false;
                behavior.m_Category = ProficiencyBase.Category.None;
                Content.AddProficiency(Plugin.Guid, EvasionSignalKey, FTK_proficiencyTable.ID.taunt,
                    "Unlost Road Evasion", delegate(FTK_proficiencyTable p)
                    {
                        p.m_ProficiencyPrefab = behavior;
                        p.m_TargetFriendly = true;
                        p.m_Target = CharacterDummy.TargetType.None;
                        p.m_Harmless = true;
                        p.m_FullSlots = false;
                        p.m_DmgMultiplier = 0;
                        p.m_ChanceToAffect = 1;
                        p.m_RepeatCount = 0;
                    });
                int signalId = Content.Db<FTK_proficiencyTableDB>().GetIntFromID(EvasionSignalKey);
                if (signalId < 0) return false;
                EvasionSignalId = (FTK_proficiencyTable.ID)signalId;
            }
            if (WayfarerPreviewId == FTK_proficiencyTable.ID.None)
                WayfarerPreviewId = RegisterRoleEvasion(WayfarerPreviewKey, 2);
            if (WayfarerCoreId == FTK_proficiencyTable.ID.None)
                WayfarerCoreId = RegisterRoleEvasion(WayfarerCoreKey, 4);
            if (WayfarerPreviewId == FTK_proficiencyTable.ID.None ||
                WayfarerCoreId == FTK_proficiencyTable.ID.None) return false;
            return classes.Add(id) || classes.Contains(id);
        }

        private static FTK_proficiencyTable.ID RegisterRoleEvasion(string key, int percent)
        {
            ThiefRoleEvasionSignal behavior = (ThiefRoleEvasionSignal)BehaviorHost.Create(
                typeof(ThiefRoleEvasionSignal), key);
            if (behavior == null) return FTK_proficiencyTable.ID.None;
            behavior.Percent = percent;
            behavior.m_Category = ProficiencyBase.Category.None;
            Content.AddProficiency(Plugin.Guid, key, FTK_proficiencyTable.ID.taunt,
                "Wayfarer Evasion", delegate(FTK_proficiencyTable p)
                {
                    p.m_ProficiencyPrefab = behavior;
                    p.m_TargetFriendly = true;
                    p.m_Target = CharacterDummy.TargetType.None;
                    p.m_Harmless = true;
                    p.m_FullSlots = false;
                    p.m_DmgMultiplier = 0;
                    p.m_ChanceToAffect = 1;
                    p.m_RepeatCount = 0;
                });
            int id = Content.Db<FTK_proficiencyTableDB>().GetIntFromID(key);
            return id < 0 ? FTK_proficiencyTable.ID.None : (FTK_proficiencyTable.ID)id;
        }

        internal static bool RegisterWeapon(int id, string kind)
        {
            WeaponKind parsed = kind == "paired" ? WeaponKind.Paired :
                kind == "bow" ? WeaponKind.Bow : kind == "pistol" ? WeaponKind.Pistol : WeaponKind.None;
            if (parsed == WeaponKind.None) return false;
            WeaponKind existing;
            if (weapons.TryGetValue(id, out existing)) return existing == parsed;
            weapons.Add(id, parsed);
            return true;
        }

        internal static bool RegisterAction(int id, string kind)
        {
            ActionKind parsed = kind == "prepare" ? ActionKind.Prepare :
                kind == "pierce" ? ActionKind.Pierce : kind == "shot" ? ActionKind.Shot : ActionKind.None;
            if (parsed == ActionKind.None) return false;
            ActionKind existing;
            if (actions.TryGetValue(id, out existing)) return existing == parsed;
            actions.Add(id, parsed);
            return true;
        }

        internal static bool RegisterArtifact(int id, string signature)
        {
            ArtifactKind parsed = signature == "borrowedFortune" ? ArtifactKind.BorrowedFortune :
                signature == "lastLight" ? ArtifactKind.LastLight :
                signature == "looseAndLeave" ? ArtifactKind.LooseAndLeave : ArtifactKind.None;
            WeaponKind weapon;
            if (parsed == ArtifactKind.None || !weapons.TryGetValue(id, out weapon) ||
                (parsed == ArtifactKind.LooseAndLeave ? weapon != WeaponKind.Bow && weapon != WeaponKind.Pistol :
                    weapon != WeaponKind.Paired)) return false;
            ArtifactKind existing;
            if (artifacts.TryGetValue(id, out existing)) return existing == parsed;
            artifacts.Add(id, parsed);
            return true;
        }

        internal static bool IsThief(CharacterDummy dummy)
        {
            return dummy != null && dummy.m_CharacterOverworld != null &&
                dummy.m_CharacterOverworld.m_CharacterStats != null &&
                classes.Contains((int)dummy.m_CharacterOverworld.m_CharacterStats.m_CharacterClass);
        }

        internal static bool IsThiefClass(int id) { return classes.Contains(id); }

        internal static bool RegisterArmor(int id, string key, ThiefEquipmentSets.Slot slot)
        { return equipment.AddArmor(id, key, slot); }

        internal static bool RegisterArmament(int id, string key)
        { return equipment.AddArmament(id, key); }

        private static ThiefEquipmentSets.Profile Profile(CharacterDummy actor)
        {
            if (!IsThief(actor) || actor.m_CharacterOverworld.m_PlayerInventory == null) return null;
            PlayerInventory inventory = actor.m_CharacterOverworld.m_PlayerInventory;
            return equipment.Evaluate(true,
                (int)inventory.Get(PlayerInventory.ContainerID.Head).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Body).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Foot).GetOne(),
                (int)actor.m_CharacterOverworld.m_WeaponID);
        }

        private static bool IsRole(ThiefEquipmentSets.Profile profile, string name)
        { return profile != null && profile.Key.EndsWith(":" + name, StringComparison.Ordinal); }

        internal static string EquipmentDescription(int itemId, CharacterOverworld wearer)
        {
            if (wearer == null || wearer.m_PlayerInventory == null)
                return equipment.Description(itemId, false, -1, -1, -1, -1);
            PlayerInventory inventory = wearer.m_PlayerInventory;
            bool thief = wearer.m_CharacterStats != null &&
                classes.Contains((int)wearer.m_CharacterStats.m_CharacterClass);
            return equipment.Description(itemId, thief,
                (int)inventory.Get(PlayerInventory.ContainerID.Head).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Body).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Foot).GetOne(),
                (int)wearer.m_WeaponID);
        }

        internal static string OwnedEquipmentDescription(CharacterOverworld wearer)
        {
            if (wearer == null || wearer.m_PlayerInventory == null || wearer.m_CharacterStats == null) return string.Empty;
            PlayerInventory inventory = wearer.m_PlayerInventory;
            return equipment.OwnedDescription(IsThiefClass((int)wearer.m_CharacterStats.m_CharacterClass),
                (int)inventory.Get(PlayerInventory.ContainerID.Head).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Body).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Foot).GetOne(),
                (int)wearer.m_WeaponID);
        }

        internal static string OwnedEquipmentLabel(CharacterOverworld wearer)
        {
            if (wearer == null || wearer.m_PlayerInventory == null || wearer.m_CharacterStats == null) return string.Empty;
            PlayerInventory inventory = wearer.m_PlayerInventory;
            return equipment.OwnedLabel(IsThiefClass((int)wearer.m_CharacterStats.m_CharacterClass),
                (int)inventory.Get(PlayerInventory.ContainerID.Head).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Body).GetOne(),
                (int)inventory.Get(PlayerInventory.ContainerID.Foot).GetOne(),
                (int)wearer.m_WeaponID);
        }

        private static WeaponKind CurrentWeapon(CharacterDummy actor)
        {
            if (actor == null || actor.m_CharacterOverworld == null) return WeaponKind.None;
            int id = (int)actor.m_CharacterOverworld.m_WeaponID;
            WeaponKind kind;
            if (weapons.TryGetValue(id, out kind)) return kind;
            return id == (int)FTK_itembase.ID.dualKnife || id == (int)FTK_itembase.ID.dualDagger
                ? WeaponKind.Paired : WeaponKind.None;
        }

        internal static void BeginEncounter(EncounterSession session)
        {
            if (!Enabled || session == null) return;
            List<string> enemies = new List<string>();
            foreach (EnemyDummy enemy in session.m_EnemyDummies.Values)
                if (enemy != null) enemies.Add(GuardianRuntime.Identity(enemy));
            state.BeginEncounter(enemies);
            pending.Clear();
            strikes.Clear();
            wayfarer.Clear();
            observedWeapons.Clear();
            observedProfiles.Clear();
            attackSerial = 0;
            foreach (CharacterDummy actor in session.m_PlayerDummies.Values) ObserveWeapon(actor);
        }

        internal static void BeginTurn(CharacterDummy actor)
        {
            if (!Enabled || actor == null) return;
            string id = GuardianRuntime.Identity(actor);
            if (actor is EnemyDummy) state.BeginEnemyTurn(id);
            else if (IsThief(actor))
            {
                pending.Remove(id);
                wayfarer.Remove(id);
                state.BeginActorTurn(id);
                // EngageBattle is the scheduled native turn boundary, including after reload.
                if (CurrentWeapon(actor) == WeaponKind.Pistol && actor.m_EventListener != null &&
                    actor.m_EventListener.m_Weapon != null)
                {
                    Weapon equipped = actor.m_EventListener.m_Weapon;
                    if (equipped.m_WeaponType == Weapon.WeaponType.firearm && equipped.m_AmmoCapacity > 0 &&
                        actor.m_CurrentAmmo < equipped.m_AmmoCapacity)
                        actor.SetCurrentAmmo(Math.Min(equipped.m_AmmoCapacity, actor.m_CurrentAmmo + 1));
                }
            }
        }

        internal static void ResetEnemy(CharacterDummy actor)
        {
            if (Enabled && actor is EnemyDummy) state.ResetEnemy(GuardianRuntime.Identity(actor));
        }

        internal static void EndTurn(CharacterDummy actor)
        {
            if (Enabled && IsThief(actor)) state.EndActorTurn(GuardianRuntime.Identity(actor));
        }

        internal static void Expire(CharacterDummy actor)
        {
            if (Enabled && IsThief(actor))
            {
                string id = GuardianRuntime.Identity(actor);
                state.ExpireActor(id);
                pending.Remove(id);
                wayfarer.Remove(id);
            }
        }

        internal static void ObserveWeapon(CharacterDummy actor)
        {
            if (!Enabled || !IsThief(actor)) return;
            string id = GuardianRuntime.Identity(actor);
            int weapon = (int)actor.m_CharacterOverworld.m_WeaponID;
            int previous;
            if (observedWeapons.TryGetValue(id, out previous) && previous != weapon)
            {
                state.ClearPrepared(id);
                state.ClearEvasion(id);
            }
            observedWeapons[id] = weapon;
            ThiefEquipmentSets.Profile profile = Profile(actor);
            string identity = profile == null ? string.Empty : profile.Key + ":" + profile.Count;
            string priorProfile;
            if (observedProfiles.TryGetValue(id, out priorProfile) && priorProfile != identity)
                state.ClearEvasion(id);
            observedProfiles[id] = identity;
        }

        internal static bool SlipAwayAvailable(CharacterDummy actor)
        {
            return IsThief(actor) && GuardianRuntime.CanAct(actor) &&
                state.SlipAwayAvailable(GuardianRuntime.Identity(actor));
        }

        internal static bool SlipAwayUsed(CharacterDummy actor)
        {
            return IsThief(actor) && state.SlipAwayUsed(GuardianRuntime.Identity(actor));
        }

        internal static void ApplySlipAway(CharacterDummy actor)
        {
            if (!IsThief(actor) || !GuardianRuntime.CanAct(actor)) return;
            if (state.TrySlipAway(GuardianRuntime.Identity(actor)))
                actor.SpawnHudTextRPC("Slip Away", string.Empty);
        }

        internal static string NextAttackReceipt(CharacterDummy attacker)
        {
            attackSerial++;
            return GuardianRuntime.Identity(attacker) + ":" +
                attackSerial.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static DummyDamageInfo ResolveIncoming(CharacterDummy attacker, DummyDamageInfo original, string attackId)
        {
            if (!Enabled || !(attacker is EnemyDummy) || original == null || original.m_Damage <= 0 ||
                EncounterSession.Instance == null || original.m_SpecialAttack == CharacterDummy.SpecialAttack.ItemAttack ||
                original.m_AttackResponse == CharacterDummy.AttackResponse.Dodge ||
                original.m_AttackResponse == CharacterDummy.AttackResponse.BlackHole)
                return original;
            CharacterDummy victim = EncounterSession.Instance.GetDummyByFID(original.m_VictimID);
            if (!IsThief(victim)) return original;
            string victimId = GuardianRuntime.Identity(victim);
            bool guarded = false;
            foreach (string guardianId in GuardianRuntime.State.ActiveGuardians(victimId))
                if (GuardianRuntime.CanAct(GuardianRuntime.Find(guardianId))) { guarded = true; break; }
            int reduced = state.ResolveSlipAwayDamage(victimId, true, original.m_Damage, guarded, attackId);
            if (reduced == original.m_Damage) return original;
            DummyDamageInfo copy = new DummyDamageInfo();
            copy.Set(original);
            copy.m_Damage = reduced;
            copy.m_CritDamage = Math.Min(copy.m_CritDamage, reduced);
            copy.m_NewHealth = Math.Max(original.m_NewHealth, Math.Max(0, victim.GetCurrentHealth() - reduced));
            if (copy.m_NewHealth > 0 && copy.m_AttackResponse == CharacterDummy.AttackResponse.Death)
                copy.m_AttackResponse = CharacterDummy.AttackResponse.DamagedHeavy;
            return copy;
        }

        internal static void ApplyEvasion(AttackAttempt attempt, DummyAttackProperties properties)
        {
            if (!Enabled || properties == null || !(attempt.m_AttackingDummy is EnemyDummy) ||
                !IsThief(attempt.m_DamagedDummy)) return;
            CharacterDummy victim = attempt.m_DamagedDummy;
            ObserveWeapon(victim);
            string id = GuardianRuntime.Identity(victim);
            ArtifactKind artifact;
            if (!artifacts.TryGetValue((int)victim.m_CharacterOverworld.m_WeaponID, out artifact) ||
                artifact != ArtifactKind.LooseAndLeave)
            {
                if (!IsRole(Profile(victim), "wayfarer")) { state.ClearEvasion(id); return; }
            }
            int earned = state.EvasionPercent(id);
            if (earned > 0) properties.m_EvadeRating = Math.Min(1f, properties.m_EvadeRating + earned / 100f);
        }

        internal static void GrantEvasion(CharacterDummy actor)
        {
            if (!IsThief(actor) || actor.m_CharacterOverworld == null ||
                EvasionSignalId == FTK_proficiencyTable.ID.None) return;
            ArtifactKind artifact;
            if (!artifacts.TryGetValue((int)actor.m_CharacterOverworld.m_WeaponID, out artifact) ||
                artifact != ArtifactKind.LooseAndLeave) return;
            state.GrantEvasion(GuardianRuntime.Identity(actor));
        }

        internal static void GrantRoleEvasion(CharacterDummy actor, int percent)
        {
            if (!IsThief(actor) || !IsRole(Profile(actor), "wayfarer")) return;
            ThiefEquipmentSets.Profile profile = Profile(actor);
            int cap = profile.Count == 3 ? 4 : 2;
            state.GrantEvasion(GuardianRuntime.Identity(actor), Math.Min(percent, cap));
        }

        internal static void OnImpact(CharacterDummy victim, int previousHealth)
        {
            if (!Enabled || victim == null || victim.m_DamageInfo == null || EncounterSession.Instance == null) return;
            DummyDamageInfo damage = victim.m_DamageInfo;
            CharacterDummy attacker = EncounterSession.Instance.GetDummyByFID(damage.m_AttackerID);
            if (attacker != null && victim is EnemyDummy)
            {
                string victimId = GuardianRuntime.Identity(victim);
                PendingStrike strike;
                float slotSuccess = float.NaN;
                if (strikes.TryGetValue(victimId, out strike))
                {
                    strikes.Remove(victimId);
                    if (strike.AttackerId == GuardianRuntime.Identity(attacker)) slotSuccess = strike.SlotSuccess;
                }
                if (previousHealth > victim.GetCurrentHealth())
                    RecordActualDamage(attacker, damage, victim, slotSuccess);
            }
            if (!IsThief(attacker)) return;
            string actorId = GuardianRuntime.Identity(attacker);
            PendingWayfarer walk;
            if (wayfarer.TryGetValue(actorId, out walk))
            {
                wayfarer.Remove(actorId);
                if (walk.TargetId == GuardianRuntime.Identity(victim) &&
                    walk.WeaponId == (int)attacker.m_CharacterOverworld.m_WeaponID &&
                    previousHealth > victim.GetCurrentHealth() && !damage.m_IsAOE &&
                    damage.m_SpecialAttack == CharacterDummy.SpecialAttack.None &&
                    damage.m_DamageType == FTK_weaponStats2.DamageType.physical &&
                    attacker.m_CharacterOverworld.IsOwner)
                {
                    ThiefEquipmentSets.Profile role = Profile(attacker);
                    if (IsRole(role, "wayfarer"))
                    {
                        FTK_proficiencyTable.ID signal = role.Count == 3 ? WayfarerCoreId : WayfarerPreviewId;
                        if (signal != FTK_proficiencyTable.ID.None)
                            attacker.RPCAllSelf("AddProfToDummy", new object[] {
                                new FTK_proficiencyTable.ID[] { signal }, false, false });
                    }
                }
            }
            PendingSneak receipt;
            if (!pending.TryGetValue(actorId, out receipt) || receipt.TargetId != GuardianRuntime.Identity(victim)) return;
            pending.Remove(actorId);
            if (!receipt.Perfect || previousHealth <= victim.GetCurrentHealth() ||
                victim.GetCurrentHealth() >= receipt.TargetHealth ||
                (int)attacker.m_CharacterOverworld.m_WeaponID != receipt.WeaponId) return;
            if (receipt.Artifact == ArtifactKind.BorrowedFortune && receipt.FocusSpent > 0 &&
                attacker.m_CharacterOverworld.IsOwner)
            {
                CharacterStats stats = attacker.m_CharacterOverworld.m_CharacterStats;
                if (stats.m_FocusPoints < stats.MaxFocus)
                {
                    stats.UpdateFocusPoints(1, true);
                    attacker.SpawnHudTextRPC("Borrowed Fortune: +1 Focus", string.Empty);
                }
            }
            else if (receipt.Prepared && receipt.FocusSpent > 0 && receipt.ProfileCount == 3 &&
                receipt.ProfileKey != null && receipt.ProfileKey.EndsWith(":locksmith", StringComparison.Ordinal) &&
                attacker.m_CharacterOverworld.IsOwner &&
                IsRole(Profile(attacker), "locksmith") && Profile(attacker).Count == 3)
            {
                CharacterStats stats = attacker.m_CharacterOverworld.m_CharacterStats;
                if (stats.m_FocusPoints < stats.MaxFocus && state.TryLocksmithRefund(actorId))
                {
                    stats.UpdateFocusPoints(1, true);
                    attacker.SpawnHudTextRPC("Locksmith: +1 Focus", string.Empty);
                }
            }
            if (receipt.Artifact == ArtifactKind.LooseAndLeave)
            {
                if (attacker.m_CharacterOverworld.IsOwner &&
                    EvasionSignalId != FTK_proficiencyTable.ID.None)
                {
                    attacker.RPCAllSelf("AddProfToDummy", new object[] {
                        new FTK_proficiencyTable.ID[] { EvasionSignalId }, false, false });
                    attacker.SpawnHudTextRPC("Loose and Leave: +8 Evasion", string.Empty);
                }
            }
        }

        internal static void PrepareAttack(AttackAttempt attack, bool consumable, ref float damageMultiplier)
        {
            if (!Enabled || consumable || !IsThief(attack.m_AttackingDummy) ||
                attack.m_AttackingDummy.m_CharacterOverworld == null ||
                !attack.m_AttackingDummy.m_CharacterOverworld.IsOwner) return;
            ObserveWeapon(attack.m_AttackingDummy);
            string actorId = GuardianRuntime.Identity(attack.m_AttackingDummy);
            // Each committed attack invalidates an earlier artifact receipt, including ineligible attacks.
            pending.Remove(actorId);
            wayfarer.Remove(actorId);
            ActionKind action;
            bool pistolShot = CurrentWeapon(attack.m_AttackingDummy) == WeaponKind.Pistol &&
                actions.TryGetValue((int)attack.m_AttackProficiency, out action) && action == ActionKind.Shot;
            bool authoredDirectAction = actions.TryGetValue((int)attack.m_AttackProficiency, out action) &&
                (action == ActionKind.Prepare || action == ActionKind.Pierce || action == ActionKind.Shot);
            bool basicPrecision = attack.m_AttackProficiency == FTK_proficiencyTable.ID.None &&
                CurrentWeapon(attack.m_AttackingDummy) != WeaponKind.None;
            if (attack.m_AttackingDummy.m_IsAttacking && !attack.m_Harmless &&
                attack.m_CheatType == SlotControl.AttackCheatType.None &&
                attack.m_DamagedDummy is EnemyDummy &&
                attack.m_DamageType == FTK_weaponStats2.DamageType.physical &&
                attack.m_SpecialAttack == CharacterDummy.SpecialAttack.None &&
                (basicPrecision || authoredDirectAction) && IsRole(Profile(attack.m_AttackingDummy), "wayfarer"))
                wayfarer[actorId] = new PendingWayfarer {
                    TargetId = GuardianRuntime.Identity(attack.m_DamagedDummy),
                    WeaponId = (int)attack.m_AttackingDummy.m_CharacterOverworld.m_WeaponID };
            if (attack.m_Harmless || attack.m_CheatType != SlotControl.AttackCheatType.None ||
                !(attack.m_DamagedDummy is EnemyDummy) ||
                attack.m_DamageType != FTK_weaponStats2.DamageType.physical ||
                CurrentWeapon(attack.m_AttackingDummy) == WeaponKind.None ||
                attack.m_SpecialAttack != CharacterDummy.SpecialAttack.None ||
                (attack.m_AttackProficiency != FTK_proficiencyTable.ID.None && !pistolShot))
            {
                state.ClearPrepared(actorId);
                return;
            }

            // The native damage calculation multiplies full success by current maximum weapon
            // damage, then applies crit, Frozen, and armor. Spend on a committed partial attempt too.
            string targetId = GuardianRuntime.Identity(attack.m_DamagedDummy);
            state.ObserveEnemy(targetId);
            int weaponId = (int)attack.m_AttackingDummy.m_CharacterOverworld.m_WeaponID;
            ArtifactKind artifact;
            if (!artifacts.TryGetValue(weaponId, out artifact)) artifact = ArtifactKind.None;
            EnemyDummy enemy = (EnemyDummy)attack.m_DamagedDummy;
            bool targetFull = enemy.GetCurrentHealth() == enemy.m_EnemyCombat.GetHealthTotal();
            ThiefEquipmentSets.Profile profile = Profile(attack.m_AttackingDummy);
            bool prepared = state.HasPrepared(actorId);
            ThiefCombatState.AttackCommit commit = state.CommitPrecisionAttack(actorId, targetId,
                artifact == ArtifactKind.LastLight, targetFull);
            if (commit.EligibleForSneakAttack)
                pending[actorId] = new PendingSneak { TargetId = targetId,
                    TargetHealth = enemy.GetCurrentHealth(), WeaponId = weaponId,
                    FocusSpent = attack.m_AttackFocused, Perfect = attack.m_SlotSuccessPercent == 1f,
                    Artifact = artifact, Prepared = prepared,
                    ProfileKey = profile == null ? null : profile.Key,
                    ProfileCount = profile == null ? 0 : profile.Count };
            if (commit.LastLight) attack.m_AttackingDummy.SpawnHudTextRPC("Last Light spent", string.Empty);
            if (commit.EligibleForSneakAttack && attack.m_SlotSuccessPercent == 1f)
            {
                int bonus = commit.BonusPercent;
                if (!commit.LastLight)
                {
                    if (IsRole(profile, "nightblade"))
                        bonus += (targetFull || !state.HasEnemyActed(targetId))
                            ? (profile.Count == 3 ? 10 : 5) : -(profile.Count == 3 ? 10 : 5);
                    if (IsRole(profile, "locksmith"))
                        bonus += profile.Count == 3 ? -10 : prepared ? 5 : -5;
                    if (IsRole(profile, "wayfarer")) bonus -= profile.Count == 3 ? 10 : 5;
                }
                damageMultiplier *= 1f + Math.Max(0, bonus) / 100f;
            }
        }

        internal static void RecordAttack(CharacterDummy attacker, DummyDamageInfo damage, float slotSuccess)
        {
            if (!Enabled || attacker == null || damage == null || float.IsNaN(slotSuccess) ||
                !IsThief(attacker) || attacker.m_CharacterOverworld == null ||
                !attacker.m_CharacterOverworld.IsOwner ||
                EncounterSession.Instance == null) return;
            CharacterDummy victim = EncounterSession.Instance.GetDummyByFID(damage.m_VictimID);
            if (victim is EnemyDummy)
                strikes[GuardianRuntime.Identity(victim)] = new PendingStrike {
                    AttackerId = GuardianRuntime.Identity(attacker), SlotSuccess = slotSuccess };
        }

        private static void RecordActualDamage(CharacterDummy attacker, DummyDamageInfo damage,
            CharacterDummy victim, float slotSuccess)
        {
            if (damage.m_Damage <= 0 ||
                EncounterSession.Instance == null || damage.m_IsAOE ||
                damage.m_SpecialAttack != CharacterDummy.SpecialAttack.None) return;
            if (!(victim is EnemyDummy) || attacker.m_CharacterOverworld == null || !attacker.m_IsAlive ||
                damage.m_DamageType == FTK_weaponStats2.DamageType.none) return;
            if (damage.m_Prof != FTK_proficiencyTable.ID.None)
            {
                FTK_proficiencyTable proficiency = FTK_proficiencyTableDB.Get(damage.m_Prof);
                if (proficiency == null || proficiency.m_Target != CharacterDummy.TargetType.None ||
                    proficiency.m_TargetFriendly || proficiency.m_RepeatCount > 0) return;
            }
            string actorId = GuardianRuntime.Identity(attacker);
            state.RecordPositiveDirectDamage(actorId, GuardianRuntime.Identity(victim));
            if (!IsThief(attacker) || !attacker.m_CharacterOverworld.IsOwner) return;
            ObserveWeapon(attacker);
            ActionKind action;
            if (actions.TryGetValue((int)damage.m_Prof, out action) && action == ActionKind.Prepare)
                state.TryPrepare(actorId, true);
            else if (damage.m_Prof == FTK_proficiencyTable.ID.None && CurrentWeapon(attacker) == WeaponKind.Paired)
            {
                FTK_weaponStats2 weapon = FTK_weaponStats2DB.Get(attacker.m_CharacterOverworld.m_WeaponID);
                if (weapon != null && weapon._slots > 0 &&
                    Math.Abs(slotSuccess * weapon._slots - (weapon._slots - 1)) < 0.01f)
                    state.TryTwinFeint(actorId, true, 1, true);
            }
        }
    }

    public sealed class ThiefSlipAwayProficiency : ProficiencyBase
    {
        public override bool IsImmune(CharacterDummy dummy) { return false; }
        public override bool IsIgnore(CharacterDummy dummy) { return false; }
        public override void AddToDummy(CharacterDummy dummy)
        {
            try { ThiefRuntime.ApplySlipAway(dummy); }
            catch (Exception e) { Plugin.Log.LogError("[thief] Slip Away application failed: " + e); }
        }
    }

    public sealed class ThiefEvasionSignal : ProficiencyBase
    {
        public override bool IsImmune(CharacterDummy dummy) { return false; }
        public override bool IsIgnore(CharacterDummy dummy) { return false; }
        public override void AddToDummy(CharacterDummy dummy)
        {
            try { ThiefRuntime.GrantEvasion(dummy); }
            catch (Exception e) { Plugin.Log.LogError("[thief] Evasion signal failed: " + e); }
        }
    }

    public sealed class ThiefRoleEvasionSignal : ProficiencyBase
    {
        public int Percent;
        public override bool IsImmune(CharacterDummy dummy) { return false; }
        public override bool IsIgnore(CharacterDummy dummy) { return false; }
        public override void AddToDummy(CharacterDummy dummy)
        {
            try { ThiefRuntime.GrantRoleEvasion(dummy, Percent); }
            catch (Exception e) { Plugin.Log.LogError("[thief] role Evasion failed: " + e); }
        }
    }
}
