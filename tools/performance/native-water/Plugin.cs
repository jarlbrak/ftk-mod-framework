using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using Newtonsoft.Json;
using UnityEngine;

namespace FtkNativeWater
{
    [BepInPlugin("com.ftkmf.native-water-tool", "Isolated native water experiment", "0.1.0")]
    [BepInDependency("com.ftkmf.runtime-model-test")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private sealed class Command { public string id { get; set; } public string action { get; set; } }
        private string root, libraryPath, nativeHash, pluginHash, gameHash, lastRequest;
        private float nextPoll;
        private bool supported;
        private void Awake()
        {
            enabled = false;
            if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" || Environment.GetEnvironmentVariable("FTK_NATIVE_WATER") != "1") return;
            try
            {
                root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
                string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
                DirectoryInfo directory = new DirectoryInfo(root);
                if (string.IsNullOrEmpty(requested) || Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root ||
                    directory.Parent == null || directory.Parent.Name != "scratch" || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("An exact isolated scratch child root without a root symlink is required.");
                PluginInfo helper;
                if (!Chainloader.PluginInfos.TryGetValue("com.ftkmf.runtime-model-test", out helper) || helper.Instance == null || !helper.Instance.enabled)
                    throw new InvalidOperationException("Active runtime model-test isolation helper required.");
                if (Application.platform != RuntimePlatform.OSXPlayer || IntPtr.Size != 8 || Type.GetType("Mono.Runtime") == null || !Application.unityVersion.StartsWith("2017.2.", StringComparison.Ordinal))
                    throw new PlatformNotSupportedException("Only the macOS x86_64 Unity 2017.2 Mono player is supported.");
                string pluginPath = Path.GetFullPath(typeof(Plugin).Assembly.Location);
                DirectoryInfo pluginDirectory = new DirectoryInfo(Path.GetDirectoryName(pluginPath));
                if (!pluginPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || (File.GetAttributes(pluginPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Managed experiment must be installed inside the isolated game root.");
                for (DirectoryInfo parent = pluginDirectory; parent != null && parent.FullName != root; parent = parent.Parent)
                    if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Symlinked plugin directories are unsupported.");
                libraryPath = Path.Combine(pluginDirectory.FullName, "libftk_water_native.dylib");
                pluginHash = Hash(pluginPath);
                gameHash = Hash(typeof(WaterDistort).Assembly.Location);
                if (gameHash != "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8")
                    throw new InvalidOperationException("Unreviewed game assembly; this experiment is pinned to its inspected method bodies.");
                NativeWaterBatch.Clear();
                string commandPath = Path.Combine(root, "native-water-command.json");
                if (File.Exists(commandPath)) lastRequest = File.ReadAllText(commandPath);
                supported = true;
                enabled = true;
                Logger.LogInfo("Native water experiment ready, OFF. Send selftest before on using native-water-command.json.");
            }
            catch (Exception error) { Logger.LogError("Native water experiment refused: " + error.Message); }
        }
        private void Update()
        {
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.5f;
            string path = Path.Combine(root, "native-water-command.json");
            if (!File.Exists(path)) return;
            string body = File.ReadAllText(path);
            if (body == lastRequest) return;
            lastRequest = body;
            Command command;
            try { command = JsonConvert.DeserializeObject<Command>(body); }
            catch (Exception error) { Logger.LogWarning("Invalid native-water command: " + error.Message); return; }
            if (command == null || !ValidId(command.id)) { Logger.LogWarning("Command id must contain 1-80 ASCII letters, digits, underscores or hyphens."); return; }
            string output = Path.Combine(root, "native-water-" + command.id + ".json");
            if (File.Exists(output)) { Logger.LogWarning("Command id already used; existing evidence retained: " + command.id); return; }
            var result = new Dictionary<string, object>();
            try
            {
                if (command.action == "selftest")
                {
                    NativeWaterBatch.Clear();
                    string observed = Hash(libraryPath);
                    if (nativeHash != null && nativeHash != observed) throw new InvalidOperationException("Companion changed after loading; restart the game.");
                    NativeWaterInterop.Load(libraryPath);
                    if (observed != Hash(libraryPath)) throw new InvalidOperationException("Companion changed while loading; restart the game.");
                    nativeHash = observed;
                    result["normals"] = NativeWaterInterop.SelfTest();
                    result["perlin"] = NativeWaterInterop.PerlinSelfTest();
                    result["water"] = NativeWaterInterop.WaterSelfTest();
                }
                else if (command.action == "on") NativeWaterBatch.SetEnabled(true);
                else if (command.action == "off") NativeWaterBatch.Clear();
                else if (command.action != "inventory") throw new InvalidOperationException("Supported actions: selftest, on, off, inventory.");
            }
            catch (Exception error)
            {
                NativeWaterBatch.Clear();
                result["error"] = error.GetType().Name + ": " + error.Message;
            }
            result["id"] = command.id; result["action"] = command.action;
            result["supported"] = supported; result["selfTestsPassed"] = NativeWaterInterop.Ready;
            result["experiment"] = NativeWaterBatch.Inventory();
            result["nativeSha256"] = nativeHash; result["pluginSha256"] = pluginHash; result["gameAssemblySha256"] = gameHash;
            result["utc"] = DateTime.UtcNow.ToString("o"); result["unityVersion"] = Application.unityVersion;
            result["runtimeVersion"] = Environment.Version.ToString(); result["pointerBytes"] = IntPtr.Size;
            result["operatingSystem"] = SystemInfo.operatingSystem;
            File.WriteAllText(output, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        private static bool ValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80) return false;
            foreach (char ch in value) if (!(ch >= 'a' && ch <= 'z') && !(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9') && ch != '-' && ch != '_') return false;
            return true;
        }
        private static string Hash(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private void OnDisable() { NativeWaterBatch.Clear(); }
        private void OnDestroy() { NativeWaterBatch.Clear(); }
    }
}
