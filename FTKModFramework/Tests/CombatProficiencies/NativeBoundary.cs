// Minimal named native boundary. The production build binds the installed Unity/game assemblies.
using System;
using System.Collections.Generic;

namespace GridEditor
{
    public class FTK_itembase
    {
        public enum ID { None = -1 }
        public enum ObjectType { armor, boots, helmet, shield, necklace, trinket, tool, weapon }
        public string m_ID;
        public ObjectType m_ObjectType;
    }
    public class FTK_items : FTK_itembase { }
    public class FTK_itemsDB : Rows<FTK_items> { public FTK_items GetEntry(FTK_itembase.ID id) { return GetEntryByInt((int)id); } }
    public class FTK_weaponStats2 { public enum DamageType { none, physical, magic } }
    public class FTK_playerGameStart { public string m_ID; }
    public class FTK_playerGameStartDB : Rows<FTK_playerGameStart> { }
    public class FTK_proficiencyTableDB : Rows<FTK_proficiencyTable>
    { public static FTK_proficiencyTable Get(FTK_proficiencyTable.ID id) { return FTKModFramework.Core.Content.Proficiencies.GetEntryByInt((int)id); } }
    public class Rows<T> where T : class
    {
        public readonly Dictionary<int, T> Values = new Dictionary<int, T>();
        public readonly Dictionary<string, int> Ids = new Dictionary<string, int>();
        public int GetIntFromID(string id) { int value; return Ids.TryGetValue(id, out value) ? value : -1; }
        public T GetEntryByInt(int id) { T value; return Values.TryGetValue(id, out value) ? value : null; }
    }
    public class FTK_proficiencyTable
    {
        public enum ID { None = -1 }
        public string m_ID;
        public ProficiencyBase m_ProficiencyPrefab;
        public float m_CustomValue, m_Quickness = .5f, m_DmgMultiplier = .75f, m_PerSlotSkillRoll, m_ChanceToAffect = 1;
        public int m_RepeatCount = 1, m_SlotOverride = 3, m_DamagePerAttack, m_BoatDamage;
        public bool m_Harmless, m_TargetFriendly, m_FullSlots, m_IgnoresArmor, m_GunShot, m_Suicide, m_ChaosOption;
        public CharacterDummy.TargetType m_Target;
        public FTK_weaponStats2.DamageType m_DmgTypeOverride;
        public int m_WpnTypeOverride;
        public string GetLocalizedDisplayName() { return m_ID; }
    }
}
public class ProficiencyBase
{
    public enum Category { Armor, Resist }
    public Category m_Category;
    public bool m_IsEndOnTurn;
    public float m_CustomValue;
    public GridEditor.FTK_proficiencyTable.ID m_ProficiencyID;
}
public class ProficiencyArmor : ProficiencyBase { }
public class ProficiencyResist : ProficiencyBase { }
public struct FTKPlayerID
{
    public int m_TurnIndex, m_PhotonID;
    public static bool operator ==(FTKPlayerID a, FTKPlayerID b) { return a.Equals(b); }
    public static bool operator !=(FTKPlayerID a, FTKPlayerID b) { return !a.Equals(b); }
    public override bool Equals(object other) { return other is FTKPlayerID && ((FTKPlayerID)other).m_TurnIndex == m_TurnIndex && ((FTKPlayerID)other).m_PhotonID == m_PhotonID; }
    public override int GetHashCode() { return m_TurnIndex ^ m_PhotonID; }
}
public class CharacterDummy
{
    public enum TargetType { None, Splash }
    public sealed class ProficiencyRecord { public int m_Count; public ProficiencyBase m_Proficiency; }
    public FTKPlayerID FID;
    public CharacterOverworld m_CharacterOverworld;
    public Dictionary<ProficiencyBase.Category, ProficiencyRecord> m_SufferingProficiencies = new Dictionary<ProficiencyBase.Category, ProficiencyRecord>();
}
public class EnemyDummy : CharacterDummy { public EnemyCombat m_EnemyCombat = new EnemyCombat(); public bool Frozen; }
public class EnemyCombat { public object m_RaceTypes; }
public class CharacterOverworld { public bool IsOwner = true; public CharacterStats m_CharacterStats = new CharacterStats(); public PlayerInventory m_PlayerInventory = new PlayerInventory(); }
public class PlayerInventory
{
    public enum ContainerID { Trinket, Neck, LeftHand, RightHand, Foot, Body, Head, Belt, Backpack }
    public class Container { public Dictionary<GridEditor.FTK_itembase.ID, int> m_CountDictionary = new Dictionary<GridEditor.FTK_itembase.ID, int>(); }
    private Dictionary<ContainerID, Container> slots = new Dictionary<ContainerID, Container>();
    public Container Get(ContainerID id) { Container value; if (!slots.TryGetValue(id, out value)) { value = new Container(); slots.Add(id, value); } return value; }
}
public class CharacterStats { public int Damage = 31; public int GetWeaponMaxDamage(object race) { return Damage; } }
public struct AttackAttempt { public CharacterDummy m_AttackingDummy, m_DamagedDummy; public GridEditor.FTK_proficiencyTable.ID m_AttackProficiency; public bool m_ProfSuccess; }
public class EncounterSessionMC { public class FightOrderEntry { public int m_EntryID; public FTKPlayerID m_Pid; } }
public class FTKRandom { public int OriginalSeed; }
public class EncounterSession
{
    public static EncounterSession Instance;
    public FTKRandom m_Random;
    public int m_EncounterIndex;
    public List<EncounterSessionMC.FightOrderEntry> m_FightOrderVisual = new List<EncounterSessionMC.FightOrderEntry>();
    public EnemyDummy Enemy;
    public EnemyDummy GetCurrentEnemy() { return Enemy; }
}
public class uiBattleButton { public enum BattleButtonType { proficiency, attack } public BattleButtonType m_ButtonType; }
public class TextStub { public string text; }
public class InfoPanelStub { public TextStub m_DamageValue = new TextStub(); }
public class uiBattleStanceButtons
{
    public struct ProfValues { public GridEditor.FTK_proficiencyTable.ID m_Prof; public uiBattleButton m_Button; }
    public CharacterOverworld CombatCow;
    public List<ProfValues> m_Proficiencies = new List<ProfValues>();
    public InfoPanelStub m_InfoPanel = new InfoPanelStub();
}
public class GameFlow { public static GameFlow Instance = new GameFlow(); public float m_FrozenDmgPercent = 1.25f; }
public static class FTKUtil { public static int RoundToInt(float value) { return (int)Math.Round(value); } }
namespace FTKModFramework.Core
{
    internal static class Localization
    {
        internal static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>();
        internal static bool TryGetProficiencyDescription(string id, out string description)
        {
            description = null;
            return id != null && Descriptions.TryGetValue(id, out description);
        }
    }
    public static partial class Content
    {
        public static GridEditor.FTK_playerGameStartDB Classes = new GridEditor.FTK_playerGameStartDB();
        public static GridEditor.FTK_proficiencyTableDB Proficiencies = new GridEditor.FTK_proficiencyTableDB();
        public static GridEditor.FTK_itemsDB Items = new GridEditor.FTK_itemsDB();
        public static T Db<T>() { return (T)(object)(typeof(T) == typeof(GridEditor.FTK_playerGameStartDB) ? (object)Classes : typeof(T) == typeof(GridEditor.FTK_itemsDB) ? (object)Items : Proficiencies); }
    }
    internal static class ContentRegistry
    {
        internal static HashSet<int> Custom = new HashSet<int>();
        internal static bool IsRegisteredSyntheticId(int id, Type type) { return Custom.Contains(id); }
        internal static bool TryGetSyntheticId(string name, out int id, params Type[] types)
        { id = types[0] == typeof(GridEditor.FTK_itemsDB) ? Content.Items.GetIntFromID(name) : Content.Proficiencies.GetIntFromID(name); return Custom.Contains(id); }
    }
    internal static class Plugin { internal static LogStub Log = new LogStub(); }
    internal class LogStub { internal void LogWarning(string message) { } }
}
