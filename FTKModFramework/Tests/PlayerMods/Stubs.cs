// Host adapters only. Tests compile the real discovery, registry, and behavior loader source.
using System;
using System.Collections.Generic;
namespace FTKModFramework
{
    internal sealed class BoolSetting { public bool Value; public BoolSetting(bool value) { Value = value; } }
    internal static class Plugin
    {
        public static bool SelfTestsEnabled;
        public static readonly BoolSetting EnableSampleContent = new BoolSetting(true);
        public static readonly BoolSetting EnableBehaviorLoading = new BoolSetting(true);
        public static readonly TestLog Log = new TestLog();
    }
    internal sealed class TestLog
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
    }
}
namespace UnityEngine
{
    internal static class PlayerPrefs
    {
        private static readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public static int GetInt(string key, int defaultValue) { int value; return Values.TryGetValue(key, out value) ? value : defaultValue; }
        public static void SetInt(string key, int value) { Values[key] = value; }
        public static void Save() { }
    }
}
namespace FTKModFramework.Core.Data
{
    internal static class SyntheticContentGenerator
    {
        public const string ReservedModGuid = "com.ftkmf.synthetic";
        public const string ReservedSubfolderName = "__ftkmf_synthetic__";
    }
}
namespace FTKModFramework.Core
{
    internal enum BehaviorKind { Proficiency }
    internal static class BehaviorRegistry
    {
        public static void Register(string guid, string name, Type type, BehaviorKind kind) { throw new Exception("Unexpected behavior registration"); }
    }
}
internal class ProficiencyBase { }
