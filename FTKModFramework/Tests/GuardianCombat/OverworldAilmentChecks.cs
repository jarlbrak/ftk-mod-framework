using System;
using System.Reflection;
using FTKModFramework.Core;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string method) { }
    }
}

internal class ProficiencyBase
{
    internal enum Category { None, Poison, Curse, Fire, Stun }
}

internal class CharacterStats
{
    internal object m_CharacterOverworld;
    internal int m_CharacterClass;
    internal bool m_IsInCombat;
}

namespace FTKModFramework
{
    internal static class Plugin
    {
        internal static readonly TestLog Log = new TestLog();
    }
    internal sealed class TestLog
    {
        internal void LogWarning(string message) { throw new Exception(message); }
    }
}

internal static class OverworldAilmentChecks
{
    private static bool Query(CharacterStats stats, ProficiencyBase.Category category, bool vanilla)
    {
        MethodInfo patch = typeof(CharacterStats_OverworldAilmentImmunity_Patch).GetMethod(
            "Postfix", BindingFlags.NonPublic | BindingFlags.Static);
        object[] args = { stats, category, vanilla };
        patch.Invoke(null, args);
        return (bool)args[2];
    }

    internal static void Run(Action<bool, string> check)
    {
        CharacterStats paladin = new CharacterStats { m_CharacterOverworld = new object(), m_CharacterClass = 8123 };
        CharacterStats other = new CharacterStats { m_CharacterOverworld = new object(), m_CharacterClass = 8124 };
        check(!Query(paladin, ProficiencyBase.Category.Poison, false), "Unregistered class keeps native poison result");
        check(!OverworldAilmentImmunity.RegisterClass(8123, " "), "Display name is required");
        OverworldAilmentImmunity.RegisterClass(8123, "Cleansing March");
        OverworldAilmentImmunity.RegisterClass(8123, "Other name");
        check(OverworldAilmentImmunity.ReloadClassCount == 1, "Class capability registration is idempotent");
        check(OverworldAilmentImmunity.DisplayName(8123) == "Cleansing March", "First display name wins");
        check(Query(paladin, ProficiencyBase.Category.Poison, false) &&
            Query(paladin, ProficiencyBase.Category.Curse, false), "Registered class rejects both overworld ailments");
        check(!Query(other, ProficiencyBase.Category.Poison, false), "Other class keeps native result");
        check(!Query(paladin, ProficiencyBase.Category.Fire, false) &&
            !Query(paladin, ProficiencyBase.Category.Stun, false), "Unrelated categories keep native result");
        paladin.m_IsInCombat = true;
        check(!Query(paladin, ProficiencyBase.Category.Poison, false) &&
            !Query(paladin, ProficiencyBase.Category.Curse, false), "Combat ailments keep native result");
        paladin.m_IsInCombat = false;
        check(Query(paladin, ProficiencyBase.Category.Poison, true), "Native immunity is never removed");
        paladin.m_CharacterOverworld = null;
        check(!Query(paladin, ProficiencyBase.Category.Poison, false), "Missing owner keeps native result");
        paladin.m_CharacterOverworld = new object();
        Action restore = OverworldAilmentImmunity.SuspendForReload();
        check(!Query(paladin, ProficiencyBase.Category.Poison, false), "Removed generation loses capability");
        OverworldAilmentImmunity.RegisterClass(8124, "Other");
        restore();
        check(Query(paladin, ProficiencyBase.Category.Poison, false) &&
            !Query(other, ProficiencyBase.Category.Poison, false), "Rollback restores old class binding only");
        OverworldAilmentImmunity.SuspendForReload();
    }
}
