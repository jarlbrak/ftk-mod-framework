using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static partial class GuardianRuntime
    {
        private static Dictionary<string, string> GuardRoles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<int, GuardianProfile> Profiles = new Dictionary<int, GuardianProfile>();
        private static Dictionary<int, int> SmiteActions = new Dictionary<int, int>();
        private static Dictionary<int, List<EquipmentSetState.Definition>> Sets =
            new Dictionary<int, List<EquipmentSetState.Definition>>();
        private sealed class ArmorPayoff
        {
            internal readonly int[] Sources;
            internal readonly float Multiplier;
            internal ArmorPayoff(int[] sources, float multiplier)
            { Sources = (int[])sources.Clone(); Multiplier = multiplier; }
        }
        private static Dictionary<int, Dictionary<string, ArmorPayoff>> ArmorPayoffs =
            new Dictionary<int, Dictionary<string, ArmorPayoff>>();
        private static Dictionary<int, Dictionary<string, int[]>> SetActions =
            new Dictionary<int, Dictionary<string, int[]>>();

        internal static bool RegisterProfile(int classId, GuardianProfile profile)
        {
            GuardianProfile existing;
            if (Profiles.TryGetValue(classId, out existing)) return object.ReferenceEquals(existing, profile);
            Profiles.Add(classId, profile);
            return true;
        }

        internal static bool RegisterSmite(int classId, int actionId)
        {
            int existing;
            if (SmiteActions.TryGetValue(classId, out existing)) return existing == actionId;
            SmiteActions.Add(classId, actionId);
            return true;
        }

        internal static bool RegisterSet(int classId, EquipmentSetState.Definition set, int[] actions,
            int[] armorSources, float armorMultiplier)
        {
            List<EquipmentSetState.Definition> definitions;
            if (!Sets.TryGetValue(classId, out definitions))
            {
                definitions = new List<EquipmentSetState.Definition>(); Sets.Add(classId, definitions);
                SetActions.Add(classId, new Dictionary<string, int[]>(StringComparer.Ordinal));
                ArmorPayoffs.Add(classId, new Dictionary<string, ArmorPayoff>(StringComparer.Ordinal));
            }
            if (SetActions[classId].ContainsKey(set.Key)) return false;
            foreach (EquipmentSetState.Definition existing in definitions)
                if (existing.Head == set.Head || existing.Body == set.Body || existing.Feet == set.Feet) return false;
            definitions.Add(set);
            SetActions[classId].Add(set.Key, (int[])actions.Clone());
            if (armorSources.Length > 0) ArmorPayoffs[classId].Add(set.Key,
                new ArmorPayoff(armorSources, armorMultiplier));
            return true;
        }

        internal static string SetEquipmentDescription(int itemId, CharacterOverworld wearer)
        {
            List<int> classes = new List<int>(Sets.Keys);
            classes.Sort();
            foreach (int classId in classes)
            foreach (EquipmentSetState.Definition set in Sets[classId])
            {
                if (itemId != set.Head && itemId != set.Body && itemId != set.Feet &&
                    itemId != set.OneHand && itemId != set.Shield && itemId != set.TwoHand) continue;
                bool currentClass = wearer != null && wearer.m_CharacterStats != null &&
                    (int)wearer.m_CharacterStats.m_CharacterClass == classId;
                PlayerInventory inventory = currentClass ? wearer.m_PlayerInventory : null;
                int head = inventory == null ? -1 : Slot(inventory, PlayerInventory.ContainerID.Head);
                int body = inventory == null ? -1 : Slot(inventory, PlayerInventory.ContainerID.Body);
                int feet = inventory == null ? -1 : Slot(inventory, PlayerInventory.ContainerID.Foot);
                int right = inventory == null ? -1 : Slot(inventory, PlayerInventory.ContainerID.RightHand);
                int left = inventory == null ? -1 : Slot(inventory, PlayerInventory.ContainerID.LeftHand);
                FTK_playerGameStart classRow = Content.Db<FTK_playerGameStartDB>().GetEntryByInt(classId);
                string className = classRow == null ? "Guardian" : classRow.GetDisplayName();
                ArmorPayoff payoff;
                Dictionary<string, ArmorPayoff> payoffs;
                float multiplier = ArmorPayoffs.TryGetValue(classId, out payoffs) &&
                    payoffs.TryGetValue(set.Key, out payoff) ? payoff.Multiplier : 1f;
                GuardianProfile neutral;
                if (!Profiles.TryGetValue(classId, out neutral)) return string.Empty;
                int smite;
                int[] actions = SetActions[classId][set.Key];
                bool grantsSmite = SmiteActions.TryGetValue(classId, out smite) &&
                    Array.IndexOf(actions, smite) >= 0;
                return GuardianSetDescription.Format(set.Key, className,
                    new[] { ItemName(set.Head), ItemName(set.Body), ItemName(set.Feet) },
                    new[] { head == set.Head, body == set.Body, feet == set.Feet },
                    ItemName(set.OneHand), ItemName(set.Shield), ItemName(set.TwoHand),
                    right == set.OneHand, left == set.Shield, right == set.TwoHand,
                    left < 0, neutral, set.Minor, set.Core, set.Completion, multiplier, grantsSmite);
            }
            return string.Empty;
        }

        private static string ItemName(int id)
        {
            FTK_itembase row = FTK_itembase.GetItemBase((FTK_itembase.ID)id);
            return row == null ? "Unknown item" : row.GetLocalizedName();
        }

        private static int ClassId(CharacterDummy guardian)
        {
            return (int)guardian.m_CharacterOverworld.m_CharacterStats.m_CharacterClass;
        }

        private static int Slot(PlayerInventory inventory, PlayerInventory.ContainerID slot)
        {
            foreach (KeyValuePair<FTK_itembase.ID, int> pair in inventory.Get(slot).m_CountDictionary)
                if (pair.Value > 0) return (int)pair.Key;
            return -1;
        }

        private static EquipmentSetState.Result ActiveSet(CharacterDummy guardian)
        {
            if (!IsGuardian(guardian) || guardian.m_CharacterOverworld.m_PlayerInventory == null) return null;
            List<EquipmentSetState.Definition> definitions;
            if (!Sets.TryGetValue(ClassId(guardian), out definitions)) return null;
            PlayerInventory inventory = guardian.m_CharacterOverworld.m_PlayerInventory;
            return EquipmentSetState.Evaluate(definitions, Slot(inventory, PlayerInventory.ContainerID.Head),
                Slot(inventory, PlayerInventory.ContainerID.Body), Slot(inventory, PlayerInventory.ContainerID.Foot),
                Slot(inventory, PlayerInventory.ContainerID.RightHand), Slot(inventory, PlayerInventory.ContainerID.LeftHand));
        }

        private static GuardianProfile ActiveProfile(CharacterDummy guardian)
        {
            GuardianProfile profile;
            if (!IsGuardian(guardian) || !Profiles.TryGetValue(ClassId(guardian), out profile)) return null;
            EquipmentSetState.Result result = ActiveSet(guardian);
            return result == null ? profile : result.ArmorCount == 3 ? result.Set.Core : result.Set.Minor;
        }

        internal static int GuardReductionPercent(CharacterDummy guardian)
        {
            GuardianProfile profile = ActiveProfile(guardian);
            return profile == null ? 50 : profile.GuardReductionPercent;
        }

        private static GuardianEquipmentBonuses ProfileBonuses(CharacterDummy guardian)
        {
            GuardianProfile profile = ActiveProfile(guardian);
            if (profile == null) return NoBonuses;
            GuardianEquipmentBonuses bonuses = profile.Bonuses;
            EquipmentSetState.Result result = ActiveSet(guardian);
            if (result != null && result.ArmamentComplete)
                bonuses = GuardianEquipmentBonuses.Strongest(bonuses, result.Set.Completion);
            return bonuses;
        }

        private static string RoleKey(CharacterDummy guardian)
        {
            EquipmentSetState.Result active = ActiveSet(guardian);
            return active == null ? string.Empty : active.Set.Key + ":" + active.ArmorCount + ":" + active.ArmamentComplete;
        }

        internal static void RecordGuardRole(CharacterDummy guardian)
        {
            if (IsGuardian(guardian)) GuardRoles[Identity(guardian)] = RoleKey(guardian);
        }

        internal static void ExpireIfRoleChanged(CharacterDummy guardian)
        {
            if (!IsGuardian(guardian)) return;
            string id = Identity(guardian);
            string previous;
            if (GuardRoles.TryGetValue(id, out previous) && previous != RoleKey(guardian))
            {
                State.ExpireGuard(id);
                PendingHealing.Remove(id);
                GuardRoles.Remove(id);
            }
        }

        internal static void ForgetGuardRole(CharacterDummy guardian)
        {
            if (guardian != null && guardian.m_CharacterOverworld != null) GuardRoles.Remove(Identity(guardian));
        }

        internal static bool HasSetAction(CharacterDummy guardian, int actionId)
        {
            EquipmentSetState.Result result = ActiveSet(guardian);
            if (result == null || result.ArmorCount < 3) return false;
            int[] actions = SetActions[ClassId(guardian)][result.Set.Key];
            foreach (int action in actions) if (action == actionId) return true;
            return false;
        }

        internal static int[] ActiveActions(CharacterDummy guardian)
        {
            EquipmentSetState.Result result = ActiveSet(guardian);
            return result == null || result.ArmorCount < 3 ? new int[0] :
                (int[])SetActions[ClassId(guardian)][result.Set.Key].Clone();
        }

        internal static void PrepareProfileAttack(AttackAttempt attempt, bool consumable, ref float damageMultiplier)
        {
            CharacterDummy actor = attempt.m_AttackingDummy;
            GuardianProfile profile = ActiveProfile(actor);
            if (profile == null || consumable || attempt.m_Harmless || !CanAct(actor)) return;
            int smite;
            if (SmiteActions.TryGetValue(ClassId(actor), out smite) &&
                (int)attempt.m_AttackProficiency == smite)
                damageMultiplier *= profile.SmitePercent / 100f;
            else if (attempt.m_DamageType == FTK_weaponStats2.DamageType.physical)
            {
                damageMultiplier *= profile.PhysicalPercent / 100f;
                if (actor.m_CharacterOverworld.IsOwner && attempt.m_DamagedDummy is EnemyDummy &&
                    attempt.m_SpecialAttack != CharacterDummy.SpecialAttack.ItemAttack &&
                    attempt.m_SpecialAttack != CharacterDummy.SpecialAttack.Justice)
                {
                    FTK_proficiencyTable row = attempt.m_AttackProficiency == FTK_proficiencyTable.ID.None ?
                        null : FTK_proficiencyTableDB.Get(attempt.m_AttackProficiency);
                    if (row == null || (!row.m_Harmless && !row.m_TargetFriendly &&
                        row.m_Target == CharacterDummy.TargetType.None))
                        damageMultiplier *= ArmorPayoffMultiplier(actor, attempt.m_DamagedDummy);
                }
            }
        }

        private static float ArmorPayoffMultiplier(CharacterDummy guardian, CharacterDummy victim)
        {
            EquipmentSetState.Result set = ActiveSet(guardian);
            if (set == null || !set.ArmamentComplete || victim == null) return 1f;
            ArmorPayoff payoff;
            Dictionary<string, ArmorPayoff> payoffs;
            if (!ArmorPayoffs.TryGetValue(ClassId(guardian), out payoffs) ||
                !payoffs.TryGetValue(set.Set.Key, out payoff)) return 1f;
            CharacterDummy.ProficiencyRecord record;
            if (!victim.m_SufferingProficiencies.TryGetValue(ProficiencyBase.Category.Armor, out record) ||
                record == null || record.m_Proficiency == null) return 1f;
            return CombatProficiencyPolicy.Qualifies((int)record.m_Proficiency.m_ProficiencyID,
                record.m_Count, record.m_Proficiency.m_CustomValue, payoff.Sources)
                ? payoff.Multiplier : 1f;
        }

        internal static bool HasProfile(CharacterDummy guardian)
        {
            return ActiveProfile(guardian) != null;
        }

        internal static bool IsSmite(CharacterDummy guardian, FTK_proficiencyTable.ID action)
        {
            int registered;
            return IsGuardian(guardian) && SmiteActions.TryGetValue(ClassId(guardian), out registered) &&
                registered == (int)action;
        }

        internal static int PreviewPercent(CharacterDummy guardian, FTK_proficiencyTable.ID action,
            FTK_weaponStats2.DamageType damageType)
        {
            GuardianProfile profile = ActiveProfile(guardian);
            if (profile == null) return 100;
            int smite;
            if (SmiteActions.TryGetValue(ClassId(guardian), out smite) && (int)action == smite)
                return profile.SmitePercent;
            return damageType == FTK_weaponStats2.DamageType.physical ? profile.PhysicalPercent : 100;
        }

        internal static float PreviewArmorPayoff(CharacterDummy guardian, FTK_proficiencyTable.ID action,
            FTK_weaponStats2.DamageType damageType)
        {
            if (guardian == null || EncounterSession.Instance == null ||
                damageType != FTK_weaponStats2.DamageType.physical) return 1f;
            FTK_proficiencyTable row = action == FTK_proficiencyTable.ID.None ? null : FTK_proficiencyTableDB.Get(action);
            if (row != null && (row.m_Harmless || row.m_TargetFriendly ||
                row.m_Target != CharacterDummy.TargetType.None)) return 1f;
            return ArmorPayoffMultiplier(guardian, EncounterSession.Instance.GetCurrentEnemy());
        }

        private static Action SuspendProfilesForReload()
        {
            Dictionary<string, string> guardRoles = GuardRoles;
            Dictionary<int, GuardianProfile> profiles = Profiles;
            Dictionary<int, int> smite = SmiteActions;
            Dictionary<int, List<EquipmentSetState.Definition>> sets = Sets;
            Dictionary<int, Dictionary<string, int[]>> actions = SetActions;
            Dictionary<int, Dictionary<string, ArmorPayoff>> payoffs = ArmorPayoffs;
            GuardRoles = new Dictionary<string, string>(StringComparer.Ordinal);
            Profiles = new Dictionary<int, GuardianProfile>();
            SmiteActions = new Dictionary<int, int>();
            Sets = new Dictionary<int, List<EquipmentSetState.Definition>>();
            SetActions = new Dictionary<int, Dictionary<string, int[]>>();
            ArmorPayoffs = new Dictionary<int, Dictionary<string, ArmorPayoff>>();
            return delegate { GuardRoles = guardRoles; Profiles = profiles; SmiteActions = smite; Sets = sets; SetActions = actions; ArmorPayoffs = payoffs; };
        }
    }
}
