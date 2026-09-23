using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using GridEditor;

internal static class Program
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private static FTK_weaponStats2 Weapon(string id, int min, int max, bool common, FTK_weaponStats2.SkillType stat = FTK_weaponStats2.SkillType.Vitality)
    {
        return new FTK_weaponStats2 { m_ID = id, m_MinLevel = min, m_MaxLevel = max,
            m_ItemRarity = common ? FTK_itemRarityLevel.ID.common : FTK_itemRarityLevel.ID.rare, _skilltest = stat };
    }

    private static void Main()
    {
        CharacterOverworld player = new CharacterOverworld();
        FTK_itembase.ID result = FTK_itembase.ID.None;
        GameLogic.Instance.Tier.m_ItemLevel = 1; // Native reward requests tier + 1.
        FTK_weaponStats2DB.Database.m_Array = new[] { Weapon("First", 1, 2, true), Weapon("Second", 2, 3, true) };
        Check(TailoredWeaponPatch.Prefix(player, ref result), "vanilla classes retain native behavior");
        Check(UnityEngine.Random.Calls == 0, "vanilla bypass consumes no additional randomness");

        ContentRegistry.Custom = true;
        Check(!TailoredWeaponPatch.Prefix(player, ref result) && result == FTK_itembase.ID.Second,
            "empty native pool uses common weapons with inclusive tier bounds");
        Check(UnityEngine.Random.Calls == 1 && UnityEngine.Random.LastCount == 2,
            "fallback draws once from the complete matching pool");

        FTK_weaponStats2DB.Database.m_Array = new[] { Weapon("First", 1, 2, true), Weapon("Second", 2, 3, false) };
        Check(TailoredWeaponPatch.Prefix(player, ref result) && UnityEngine.Random.Calls == 1,
            "eligible non-common reward preserves native selection and RNG");

        FTK_itemsDB.Database.Locked.Add(FTK_itembase.ID.Second);
        Check(!TailoredWeaponPatch.Prefix(player, ref result) && result == FTK_itembase.ID.First,
            "locked non-common weapon does not suppress the common fallback");
        FTK_weaponStats2DB.Database.m_Array = new[] {
            Weapon("First", 1, 2, true), Weapon("Second", 1, 2, true),
            Weapon("WrongStat", 1, 2, true, FTK_weaponStats2.SkillType.Strength),
            Weapon("TooEarly", 3, 4, true), Weapon("TooLate", 0, 1, true) };
        Check(!TailoredWeaponPatch.Prefix(player, ref result) && UnityEngine.Random.LastCount == 1 && result == FTK_itembase.ID.First,
            "fallback excludes locked, wrong-stat and out-of-tier weapons");

        FTK_itemsDB.Database.Locked.Add(FTK_itembase.ID.First);
        int calls = UnityEngine.Random.Calls;
        Check(TailoredWeaponPatch.Prefix(player, ref result) && UnityEngine.Random.Calls == calls,
            "no eligible fallback leaves the native path untouched without inventing a reward");
        Check(TailoredWeaponPatch.Prefix(null, ref result), "missing character preserves native preconditions");
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute { internal HarmonyPatch(Type type, string method) { } }
}
namespace UnityEngine
{
    internal static class Random
    {
        internal static int Calls, LastCount;
        internal static int Range(int min, int max) { Calls++; LastCount = max - min; return max - 1; }
    }
}
namespace GridEditor
{
    internal class FTK_itembase
    {
        internal enum ID { None, First, Second, WrongStat, TooEarly, TooLate }
        internal static ID GetEnum(string id) { return (ID)Enum.Parse(typeof(ID), id); }
    }
    internal sealed class FTK_weaponStats2
    {
        internal enum SkillType { Vitality, Strength }
        internal string m_ID;
        internal int m_MinLevel, m_MaxLevel;
        internal FTK_itemRarityLevel.ID m_ItemRarity;
        internal SkillType _skilltest;
    }
    internal static class FTK_itemRarityLevel { internal enum ID { common, rare } }
    internal sealed class FTK_weaponStats2DB
    {
        internal static readonly FTK_weaponStats2DB Database = new FTK_weaponStats2DB();
        internal FTK_weaponStats2[] m_Array;
        internal static FTK_weaponStats2DB GetDB() { return Database; }
    }
    internal sealed class FTK_itemsDB
    {
        internal static readonly FTK_itemsDB Database = new FTK_itemsDB();
        internal readonly HashSet<FTK_itembase.ID> Locked = new HashSet<FTK_itembase.ID>();
        internal static FTK_itemsDB GetDB() { return Database; }
        internal bool IsItemUnlocked(FTK_itembase.ID id) { return !Locked.Contains(id); }
    }
    internal sealed class FTK_playerGameStart
    {
        internal string m_ID = "class";
        internal FTK_weaponStats2.SkillType m_PrimaryWeaponStat = FTK_weaponStats2.SkillType.Vitality;
    }
    internal sealed class FTK_playerGameStartDB { }
}
internal sealed class CharacterOverworld
{
    internal FTK_playerGameStart GetDBEntry() { return new FTK_playerGameStart(); }
}
internal sealed class GameLogic
{
    internal static readonly GameLogic Instance = new GameLogic();
    internal readonly ProgressionTier Tier = new ProgressionTier();
    internal GameLogic GetGameDef() { return this; }
    internal GameLogic GetGameStage() { return this; }
    internal ProgressionTier GetCurrentProgressionTierDB() { return Tier; }
}
internal sealed class ProgressionTier { internal int m_ItemLevel; }
namespace FTKModFramework.Core
{
    internal static class ContentRegistry
    {
        internal static bool Custom;
        internal static bool TryGetSyntheticId(string key, out int id, params Type[] types) { id = 1; return Custom; }
    }
    internal static class Plugin { internal static readonly Logger Log = new Logger(); }
    internal sealed class Logger { internal void LogWarning(string message) { throw new Exception(message); } }
}
