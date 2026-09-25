// Registration dependencies are deliberately unusable: this harness exercises only lookup code.
using System;

namespace GridEditor
{
    public class FTK_itembase { public enum ID { None = -1, First = 1, Alias = 1, Second = 2, Case = 3, CASE = 4 } }
    public class FTK_itemsDB { }
    public class FTK_weaponStats2DB { }
    public class FTK_proficiencyTable { public enum ID { None } }
    public class FTK_proficiencyTableDB { }
    public class FTK_playerGameStart { public enum ID { None } }
    public class FTK_playerGameStartDB { }
    public class FTK_enemyCombat { public enum ID { None } }
    public class FTK_enemyCombatDB { }
    public class FTK_miniEncounter { public enum ID { None } }
    public class FTK_miniEncounterDB { }
    public class GEDataArrayBase
    {
        public void AddEntry(string id) { throw new NotSupportedException(); }
    }
    public class TableManager
    {
        public GEDataArrayBase Get(Type type) { throw new NotSupportedException(); }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type target, string method) { }
    }
}

namespace FTKModFramework.Core
{
    internal class RegisteredRowRestoration
    {
        internal Array Restore(Array rows) { throw new NotSupportedException(); }
        internal void Record(int index, object row) { throw new NotSupportedException(); }
    }
    internal static class Reflect
    {
        internal static object GetField(object target, string name) { throw new NotSupportedException(); }
        internal static void SetField(object target, string name, object value) { throw new NotSupportedException(); }
        internal static void Invoke(object target, string name) { throw new NotSupportedException(); }
        internal static void CopyFields(object source, object target, string excluded) { throw new NotSupportedException(); }
    }
    internal static class IdAllocator
    {
        internal static int Allocate(string guid, string id) { throw new NotSupportedException(); }
    }
    internal static class DbLookupPatcher
    {
        internal static void EnsurePatched(Type type) { throw new NotSupportedException(); }
    }
    internal static class Plugin
    {
        internal static class Log
        {
            internal static void LogInfo(string message) { throw new NotSupportedException(); }
        }
    }
}
