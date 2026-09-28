using System;
using System.Collections.Generic;
using UnityEngine;
using GridEditor;

namespace FTKModFramework.Core
{
    internal enum BlacksmithActionKind { None, SetHammer, Overhand, Temper }

    internal static class BlacksmithRuntime
    {
        private static Dictionary<int, BlacksmithEquipmentBonuses> equipment = new Dictionary<int, BlacksmithEquipmentBonuses>();
        private static Dictionary<FTK_proficiencyTable.ID, int> actionSources = new Dictionary<FTK_proficiencyTable.ID, int>();
        private static Dictionary<int, FTK_proficiencyTable.ID> itemActions = new Dictionary<int, FTK_proficiencyTable.ID>();
        internal static BlacksmithCombatState State = new BlacksmithCombatState();
        private static bool encounterActive;
        private static EncounterSession encounterSession;
        private static int encounterIndex;
        internal static int ReloadEquipmentCount { get { return equipment.Count; } }
        internal static bool ReloadTransientStateEmpty { get { return !encounterActive; } }
        internal static bool IsRegistered(int id) { return equipment.ContainsKey(id); }
        internal static bool SameRegistration(int id, BlacksmithEquipmentBonuses bonus) { return equipment[id].SameAs(bonus); }
        internal static void Register(int id, BlacksmithEquipmentBonuses bonus, FTK_proficiencyTable.ID action)
        {
            equipment.Add(id, bonus); actionSources.Add(action, id); itemActions.Add(id, action);
        }
        internal static Action SuspendForReload()
        {
            Dictionary<int, BlacksmithEquipmentBonuses> old = equipment;
            Dictionary<FTK_proficiencyTable.ID, int> sources = actionSources;
            Dictionary<int, FTK_proficiencyTable.ID> actions = itemActions;
            equipment = new Dictionary<int, BlacksmithEquipmentBonuses>();
            actionSources = new Dictionary<FTK_proficiencyTable.ID, int>(); itemActions = new Dictionary<int, FTK_proficiencyTable.ID>();
            return delegate { equipment = old; actionSources = sources; itemActions = actions; };
        }
        internal static FTK_proficiencyTable.ID EnsureAction(string itemKey, BlacksmithEquipmentBonuses bonus)
        {
            bool temper = bonus.TemperArmor > 0, set = bonus.SetHammerArmor > 0;
            string name = temper ? "Temper" : set ? "Set Hammer" : "Overhand";
            string description = CombatDescription(temper ? BlacksmithActionKind.Temper : set ? BlacksmithActionKind.SetHammer : BlacksmithActionKind.Overhand,
                temper ? bonus.TemperArmor : set ? bonus.SetHammerArmor : bonus.OverhandArmorPenalty, true);
            // One row per granting item makes the native proficiency ID a deterministic source
            // identity, leaving the outcome's unused amount field for the host's timeline identity.
            return RegisterAction("ftkmf_blacksmith_" + itemKey, name, temper ? 0f : set ? .75f : 1.15f, temper, description, temper ? FTK_proficiencyTable.ID.protect : set ? FTK_proficiencyTable.ID.taunt : FTK_proficiencyTable.ID.heavyattack);
        }
        internal static string CombatDescription(BlacksmithActionKind kind, int armor, bool available)
        {
            // The native attack panel has room for one description line above damage and accuracy.
            // Detailed duration, equipment and stacking rules remain on item and Armor tooltips.
            if (kind == BlacksmithActionKind.SetHammer) return "75% damage. HP hit: +" + armor + " Armor until next turn.";
            if (kind == BlacksmithActionKind.Overhand) return "115% damage. -" + armor + " Armor until next turn.";
            return available ? "+" + armor + " Armor for 2 target turns. Once/combat.\nFull action. No roll or Focus." : "Temper used this combat.";
        }
        private static FTK_proficiencyTable.ID RegisterAction(string key, string name, float multiplier, bool friendly, string description, FTK_proficiencyTable.ID nativeIconSource)
        {
            FTK_proficiencyTableDB db = Content.Db<FTK_proficiencyTableDB>();
            int existing = db.GetIntFromID(key);
            if (existing >= 0) return (FTK_proficiencyTable.ID)existing;
            Content.AddProficiency(Plugin.Guid, key, FTK_proficiencyTable.ID.taunt, name, delegate(FTK_proficiencyTable p)
            {
                p.m_ProficiencyPrefab = null;
                // Borrow the native white-outline combat sprite; it remains game-owned.
                p.m_BattleButton = FTK_proficiencyTableDB.Get(nativeIconSource).m_BattleButton;
                if (p.m_BattleButton == null) throw new InvalidOperationException("Missing native combat icon: " + nativeIconSource);
                p.m_TargetFriendly = friendly;
                p.m_Target = friendly ? CharacterDummy.TargetType.PickFriendly : CharacterDummy.TargetType.None;
                p.m_Harmless = friendly; p.m_FullSlots = false;
                p.m_DmgMultiplier = multiplier; p.m_CustomValue = 0; p.m_ChanceToAffect = 1;
                p.m_GunShot = p.m_Suicide = p.m_IgnoresArmor = false; p.m_RepeatCount = 0;
                p.m_PerSlotSkillRoll = 0; p.m_SlotOverride = -1;
                p.m_DmgTypeOverride = FTK_weaponStats2.DamageType.physical;
                p.m_WpnTypeOverride = Weapon.WeaponType.none; p.m_HitEffectOverride = null;
            });
            Localization.SetProficiencyDescription(key, description);
            return (FTK_proficiencyTable.ID)db.GetIntFromID(key);
        }
        internal static bool IsAction(FTK_proficiencyTable.ID id) { return actionSources.ContainsKey(id); }
        internal static BlacksmithActionKind Kind(FTK_proficiencyTable.ID id)
        {
            int source;
            if (!actionSources.TryGetValue(id, out source)) return BlacksmithActionKind.None;
            BlacksmithEquipmentBonuses b = equipment[source];
            return b.SetHammerArmor > 0 ? BlacksmithActionKind.SetHammer : b.OverhandArmorPenalty > 0 ? BlacksmithActionKind.Overhand : BlacksmithActionKind.Temper;
        }
        internal static FTK_proficiencyTable.ID ActionFor(CharacterDummy actor, BlacksmithActionKind kind)
        {
            FTK_proficiencyTable.ID action;
            int item = GrantingItem(actor, kind);
            return itemActions.TryGetValue(item, out action) && Kind(action) == kind ? action : FTK_proficiencyTable.ID.None;
        }
        internal static bool IsBlacksmith(CharacterDummy actor)
        {
            return actor != null && actor.m_CharacterOverworld != null && actor.m_CharacterOverworld.m_CharacterStats != null &&
                actor.m_CharacterOverworld.m_CharacterStats.m_CharacterClass == FTK_playerGameStart.ID.blacksmith;
        }
        internal static string Identity(CharacterDummy actor) { return GuardianRuntime.Identity(actor); }
        internal static bool SameActor(FTKPlayerID left, FTKPlayerID right)
        {
            return left.m_TurnIndex == right.m_TurnIndex && left.m_PhotonID == right.m_PhotonID;
        }
        internal static string Entry(EncounterSessionMC.FightOrderEntry entry) { return entry.m_EntryID.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        internal static bool HasShield(CharacterDummy actor)
        {
            if (!IsBlacksmith(actor)) return false;
            FTK_itembase.ID id = actor.m_CharacterOverworld.m_ShieldID;
            FTK_itembase item = id == FTK_itembase.ID.None ? null : FTK_itembase.GetItemBase(id);
            return item != null && item.m_IsShield;
        }
        internal static BlacksmithEquipmentBonuses WeaponBonus(CharacterDummy actor)
        {
            BlacksmithEquipmentBonuses b;
            return IsBlacksmith(actor) && equipment.TryGetValue((int)actor.m_CharacterOverworld.m_WeaponID, out b) ? b : null;
        }
        internal static int TemperAmount(CharacterDummy actor)
        {
            if (!IsBlacksmith(actor) || actor.m_CharacterOverworld.m_PlayerInventory == null) return 0;
            int amount = 0;
            foreach (KeyValuePair<FTK_itembase.ID, int> item in actor.m_CharacterOverworld.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary)
            {
                BlacksmithEquipmentBonuses b;
                if (item.Value > 0 && equipment.TryGetValue((int)item.Key, out b)) amount = Math.Max(amount, b.TemperArmor);
            }
            return amount;
        }
        internal static int GrantingItem(CharacterDummy actor, BlacksmithActionKind kind)
        {
            if (!IsBlacksmith(actor) || actor.m_CharacterOverworld.m_PlayerInventory == null) return -1;
            if (kind != BlacksmithActionKind.Temper) return (int)actor.m_CharacterOverworld.m_WeaponID;
            int best = -1, amount = 0;
            foreach (KeyValuePair<FTK_itembase.ID, int> item in actor.m_CharacterOverworld.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary)
            {
                BlacksmithEquipmentBonuses b;
                if (item.Value > 0 && equipment.TryGetValue((int)item.Key, out b) &&
                    (b.TemperArmor > amount || (b.TemperArmor == amount && (int)item.Key < best)))
                { best = (int)item.Key; amount = b.TemperArmor; }
            }
            return best;
        }
        internal static void StampOutcome(AttackAttempt attack, DummyDamageInfo outcome)
        {
            if (outcome == null || !IsAction(attack.m_AttackProficiency)) return;
            // Our behavior-free private row identifies the exact source item. Native serialization
            // carries this otherwise unused int unchanged, without an amount HUD (flag stays false).
            int turn;
            outcome.m_ProficiencyAmount = int.TryParse(CurrentTurn(attack.m_AttackingDummy), out turn) ? turn : -1;
            outcome.m_ProfHasAmount = false;
        }
        internal static bool Available(CharacterDummy actor, FTK_proficiencyTable.ID action)
        {
            if (!encounterActive || !IsAction(action) || !GuardianRuntime.CanAct(actor) || !IsBlacksmith(actor) || CurrentTurn(actor) == null) return false;
            if (ActionFor(actor, Kind(action)) != action) return false;
            BlacksmithEquipmentBonuses b = WeaponBonus(actor);
            if (Kind(action) == BlacksmithActionKind.SetHammer) return b != null && b.SetHammerArmor > 0 && HasShield(actor);
            if (Kind(action) == BlacksmithActionKind.Overhand) return b != null && b.OverhandArmorPenalty > 0;
            return TemperAmount(actor) > 0 && State.TemperAvailable(Identity(actor));
        }
        internal static void BeginEncounter()
        {
            if (equipment.Count == 0) return;
            if (encounterActive && encounterSession == EncounterSession.Instance && encounterIndex == EncounterSession.Instance.m_EncounterIndex) return;
            State.ResetEncounter(); encounterActive = true;
            encounterSession = EncounterSession.Instance; encounterIndex = EncounterSession.Instance.m_EncounterIndex;
        }
        internal static string CurrentTurn(CharacterDummy actor)
        {
            return actor == null ? null : State.CurrentTurn(Identity(actor));
        }
        internal static void BeginTurn(CharacterDummy actor)
        {
            if (!encounterActive || actor == null || EncounterSession.Instance == null) return;
            List<EncounterSessionMC.FightOrderEntry> entries = EncounterSession.Instance.m_FightOrderVisual;
            if (entries == null || entries.Count == 0 || !SameActor(entries[0].m_Pid, actor.FID)) return;
            string id = Identity(actor), turn = Entry(entries[0]);
            State.BeginTurn(id, turn); RefreshHud(actor);
        }
        internal static void CompleteEntry(EncounterSessionMC.FightOrderEntry entry, bool skipped)
        {
            if (!encounterActive || entry == null) return;
            CharacterDummy actor = EncounterSession.Instance.GetDummyByFID(entry.m_Pid);
            if (actor == null) return;
            string id = Identity(actor), turn = Entry(entry);
            if (skipped) State.BeginTurn(id, turn);
            State.EndTurn(id, turn);
            RefreshHud(actor);
        }
        internal static void ObserveEquipment(CharacterDummy actor)
        {
            if (!encounterActive || actor == null || actor.m_CharacterOverworld == null) return;
            BlacksmithEquipmentBonuses b = WeaponBonus(actor);
            State.ObserveEquipment(Identity(actor), b == null || b.SetHammerArmor == 0 ? -1 : (int)actor.m_CharacterOverworld.m_WeaponID,
                HasShield(actor));
            if (!GuardianRuntime.LivingAlly(actor)) State.ClearPositive(Identity(actor));
        }
        internal static int Armor(CharacterDummy actor)
        {
            if (!encounterActive || actor == null || actor.m_CharacterOverworld == null) return 0;
            ObserveEquipment(actor); return State.Armor(Identity(actor));
        }
        internal static string Status(CharacterDummy actor)
        {
            if (!encounterActive || actor == null || actor.m_CharacterOverworld == null) return string.Empty;
            ObserveEquipment(actor); return State.Status(Identity(actor));
        }
        // This native RPC is the committed outcome on every peer, before any animation callback.
        // Its schedule identity came from the host; damage itself follows native owner authority.
        internal static void Commit(CharacterDummy actor, DummyDamageInfo result)
        {
            if (result == null || !IsAction(result.m_Prof) || !IsBlacksmith(actor)) return;
            int source = actionSources[result.m_Prof];
            BlacksmithEquipmentBonuses b = equipment[source];
            BlacksmithActionKind kind = Kind(result.m_Prof);
            string turn = result.m_ProficiencyAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (result.m_ProficiencyAmount < 0 || turn != CurrentTurn(actor) || !SameActor(result.m_AttackerID, actor.FID)) return;
            CharacterDummy target = EncounterSession.Instance.GetDummyByFID(result.m_VictimID);
            if (kind == BlacksmithActionKind.Temper && !GuardianRuntime.LivingAlly(target)) return;
            if (!State.TryCommit(Identity(actor), turn, ((int)result.m_Prof).ToString(System.Globalization.CultureInfo.InvariantCulture))) return;
            if (kind == BlacksmithActionKind.SetHammer)
                State.SetHammer(Identity(actor), source, b.SetHammerArmor,
                    result.m_Damage > 0 && result.m_AttackResponse != CharacterDummy.AttackResponse.Dodge && result.m_AttackResponse != CharacterDummy.AttackResponse.BlackHole);
            else if (kind == BlacksmithActionKind.Overhand) State.Overhand(Identity(actor), b.OverhandArmorPenalty);
            else State.Temper(Identity(actor), Identity(target), b.TemperArmor, CurrentTurn(target));
            CharacterDummy recipient = kind == BlacksmithActionKind.Temper ? target : actor;
            RefreshHud(recipient);
            string status = Status(recipient);
            if (status.Length > 0)
            {
                EncounterSession.Instance.AddCombatEventToActiveLogEntry(recipient.GameLogID + ": " + status);
                if (actor.m_CharacterOverworld.IsOwner) recipient.SpawnHudTextRPC(status, string.Empty);
            }
        }
        internal static void RefreshHud(CharacterDummy actor)
        {
            if (actor != null && actor.m_CharacterOverworld != null && actor.m_CharacterOverworld.m_UIPlayMainHud != null)
                actor.m_CharacterOverworld.m_UIPlayMainHud.UpdateHud();
        }
        internal static void ClearPositive(CharacterDummy actor)
        {
            if (encounterActive && actor != null && actor.m_CharacterOverworld != null) State.ClearPositive(Identity(actor));
        }
        internal static void EndCombat(CharacterDummy actor)
        {
            if (!encounterActive || actor == null) return;
            State.EndActor(Identity(actor));
            bool any = false;
            foreach (CharacterDummy p in EncounterSession.Instance.m_PlayerDummies.Values)
                if (p != actor && !p.m_CombatFinished) any = true;
            if (!any) { encounterActive = false; State.ResetEncounter(); }
        }
        internal static string Description(int itemId)
        {
            BlacksmithEquipmentBonuses b;
            if (!equipment.TryGetValue(itemId, out b)) return string.Empty;
            if (b.SetHammerArmor > 0) return "Blacksmith: Set Hammer, 75% damage.\nShield + HP hit: +" + b.SetHammerArmor + " Armor until next turn.";
            if (b.OverhandArmorPenalty > 0) return "Blacksmith: Overhand, 115% damage.\n-" + b.OverhandArmorPenalty + " Armor until next turn, hit or miss.";
            return "Blacksmith: Temper, +" + b.TemperArmor + " Armor (2 turns).\nAlly/self; full action, once/combat.\nNo roll or Focus.";
        }
    }
}
