using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Diagnostic plugin for an isolated game copy only. Never distribute with the framework.
[BepInPlugin("com.ftkmf.lookup-benchmark", "FTK Lookup Benchmark", "0.1.0")]
[BepInDependency("com.ftkmf.runtime-model-test")]
public sealed class LookupBenchmark : BaseUnityPlugin
{
    private string root, lastCommand, lastError;
    private float nextPoll;

    private void Awake()
    {
        enabled = false;
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" ||
            Environment.GetEnvironmentVariable("FTK_PERF_BENCHMARK") != "1") return;
        try
        {
            root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
            string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
            DirectoryInfo directory = new DirectoryInfo(root);
            if (string.IsNullOrEmpty(requested) ||
                Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root ||
                directory.Parent == null || directory.Parent.Name != "scratch" ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Exact isolated scratch child root required, without a root symlink.");
            PluginInfo isolation;
            if (!Chainloader.PluginInfos.TryGetValue("com.ftkmf.runtime-model-test", out isolation) ||
                isolation.Instance == null || !isolation.Instance.enabled)
                throw new InvalidOperationException("Runtime model-test isolation plugin must be active.");
            enabled = true;
            Logger.LogInfo("Lookup benchmark ready: perf-lookup-command.json. Timed runs block the main thread and force GC before timing.");
        }
        catch (Exception error) { Logger.LogError("Lookup benchmark refused: " + error.Message); }
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.5f;
        try
        {
            string commandPath = Path.Combine(root, "perf-lookup-command.json");
            if (!File.Exists(commandPath)) return;
            string command;
            using (FileStream input = new FileStream(commandPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > 2048) throw new InvalidOperationException("Command exceeds 2048 bytes.");
                using (StreamReader reader = new StreamReader(input, Encoding.UTF8)) command = reader.ReadToEnd();
            }
            if (command == lastCommand) return;
            lastCommand = command;
            JObject request = JObject.Parse(command);
            JToken idToken = request["id"], passesToken = request["passes"];
            if (idToken == null || idToken.Type != JTokenType.String ||
                passesToken == null || passesToken.Type != JTokenType.Integer)
                throw new InvalidOperationException("Expected string id and integer passes.");
            string id = (string)idToken;
            int passes = (int)passesToken;
            if (!ValidId(id) || passes < 1 || passes > 1000)
                throw new InvalidOperationException("id must be 1..64 ASCII letters/digits/_/-; passes must be 1..1000.");
            string resultPath = Path.Combine(root, "perf-lookup-" + id + ".json");
            // Reserve before doing any work. Existing IDs are never replayed, even after a crash.
            using (FileStream reservation = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(reservation)) writer.Write("{\"status\":\"running\"}");
            JObject result;
            try { result = Measure(id, passes); }
            catch (Exception error)
            {
                result = new JObject { { "id", id }, { "status", "failed" }, { "error", error.ToString() } };
            }
            File.WriteAllText(resultPath, result.ToString());
            Logger.LogInfo("Lookup benchmark result: " + resultPath);
        }
        catch (Exception error)
        {
            if (lastError != error.Message) Logger.LogWarning("Lookup benchmark command rejected: " + error.Message);
            lastError = error.Message;
        }
    }

    private static bool ValidId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
        foreach (char c in id)
            if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
        return true;
    }

    private static JObject Measure(string id, int passes)
    {
        string[] names = Enum.GetNames(typeof(FTK_itembase.ID));
        if (names.Length == 0) throw new InvalidOperationException("Native item enum has no names.");
        Stopwatch firstLookup = Stopwatch.StartNew();
        FTK_itembase.ID firstValue = FTK_itembase.GetEnum(names[0]);
        firstLookup.Stop();
        if (firstValue != (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), names[0], true))
            throw new InvalidOperationException("First native lookup mismatch.");
        long expected = 0;
        foreach (string name in names)
        {
            FTK_itembase.ID vanilla = (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), name, true);
            FTK_itembase.ID actual = FTK_itembase.GetEnum(name);
            if (actual != vanilla) throw new InvalidOperationException("Native lookup mismatch for " + name);
            expected += (int)vanilla;
        }
        JObject identity = new JObject {
            { "framework", BuildIdentity("FTKModFramework") },
            { "probe", BuildIdentity("FTKPerfProbe") },
            { "game", BuildIdentity("Assembly-CSharp") },
            { "benchmark", BuildIdentity("FtkLookupBenchmark") }
        };
        Stopwatch clock = Stopwatch.StartNew();
        long warmupChecksum = Run(names, 1);
        clock.Stop();
        double warmupMs = clock.Elapsed.TotalMilliseconds;
        if (warmupChecksum != expected) throw new InvalidOperationException("Warmup checksum mismatch.");
        // Deliberate diagnostic perturbation. Never include these collections in the measured interval.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int beforeGc = GC.CollectionCount(0);
        clock.Reset();
        clock.Start();
        long checksum = Run(names, passes);
        clock.Stop();
        int gc0Delta = GC.CollectionCount(0) - beforeGc;
        if (checksum != expected * passes) throw new InvalidOperationException("Timed checksum mismatch.");
        return new JObject {
            { "id", id }, { "status", "complete" }, { "utc", DateTime.UtcNow.ToString("o") },
            { "names", names.Length }, { "passes", passes }, { "calls", (long)names.Length * passes },
            { "firstLookupMs", firstLookup.Elapsed.TotalMilliseconds },
            { "firstLookupNote", "First invocation by this trial; normal game startup or earlier trials may already have initialized the lookup." },
            { "warmupMs", warmupMs }, { "elapsedMs", clock.Elapsed.TotalMilliseconds },
            { "gc0Delta", gc0Delta }, { "checksum", checksum }, { "builds", identity },
            { "note", "Synchronous main-thread lookup microbenchmark; forced GC precedes timing. Not a gameplay frame-time measurement." }
        };
    }

    private static long Run(string[] names, int passes)
    {
        long checksum = 0;
        for (int pass = 0; pass < passes; pass++)
            for (int i = 0; i < names.Length; i++) checksum += (int)FTK_itembase.GetEnum(names[i]);
        return checksum;
    }

    private static JObject BuildIdentity(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != name) continue;
            using (FileStream input = File.OpenRead(assembly.Location))
            using (SHA256 sha = SHA256.Create())
                return new JObject {
                    { "assembly", assembly.FullName }, { "mvid", assembly.ManifestModule.ModuleVersionId.ToString() },
                    { "sha256", BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() }
                };
        }
        return new JObject { { "status", "not loaded" } };
    }
}
