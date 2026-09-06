// Host adapters only. Tests compile the real discovery, registry, and behavior loader source.
using System;
using System.Collections.Generic;
namespace FTKModFramework
{
    internal sealed class Setting<T> { public T Value; public Setting(T value) { Value = value; } }
    internal static class Plugin
    {
        public const string Guid = "com.ftkmf.framework";
        public const string Version = "0.1.0";
        public static string DataContentRootPath = System.IO.Path.GetTempPath();
        public static bool SelfTestsEnabled;
        public static readonly Setting<bool> EnableDataContent = new Setting<bool>(true);
        public static readonly Setting<bool> EnableCampaignEngine = new Setting<bool>(true);
        public static readonly Setting<bool> ForceCustomEnemy = new Setting<bool>(false);
        public static readonly Setting<bool> ForceCustomEncounter = new Setting<bool>(false);
        public static readonly Setting<bool> DiagnosticsEnableGate = new Setting<bool>(false);
        public static readonly Setting<int> SyntheticContentCount = new Setting<int>(0);
        public static readonly Setting<string> SyntheticContentKind = new Setting<string>("weapon");
        public static readonly Setting<string> SyntheticContentTemplate = new Setting<string>("bladeDagger");
        public static readonly Setting<int> SyntheticCampaignStages = new Setting<int>(20);
        public static readonly Setting<int> SyntheticCampaignQuestsPerStage = new Setting<int>(25);
        public static readonly Setting<bool> EnableSampleContent = new Setting<bool>(true);
        public static readonly Setting<bool> EnableBehaviorLoading = new Setting<bool>(true);
        public static readonly TestLog Log = new TestLog();
    }
    internal sealed class TestLog
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
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

namespace BepInEx { internal static class Paths { public static string GameRootPath; } }
namespace GridEditor { internal class TableManager { } }
