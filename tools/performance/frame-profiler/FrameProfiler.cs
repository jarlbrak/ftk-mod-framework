using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Profiling;

// Developer-only instrument. Never ship in the framework or change rendering settings here.
[BepInPlugin("com.ftkmf.frame-profiler", "FTK Frame Profiler", "0.1.0")]
[BepInDependency("com.ftkmf.runtime-model-test")]
public sealed class FrameProfiler : BaseUnityPlugin
{
    private string root, previous, resultPath;
    private float nextPoll, started, maxSeconds;
    private int count, warmup;
    private float[] frames;
    private bool[] focused;
    private bool focusStart, previousFocus, backgroundStart;
    private int focusChangedCount, focusedFrames;
    private long[,] elapsed;
    private int[,] blocks;
    private Recorder[] recorders;
    private bool[] originalEnabled;
    private string[] names;
    private JObject metadata;
    private bool resultReserved, cleanupFailed;
    private int gc0Start, gc1Start, gc2Start;
    private long managedBytesStart;
    private ResourceSnapshot resourcesStart;
    private long captureWallStart;
    private const string TimerOwner = "com.ftkmf.frame-profiler.timers";
    private Harmony timerHarmony;
    private string[] managedNames = new string[0];
    private long[,] managedTicks;
    private long[,] managedCalls;
    private static readonly Dictionary<MethodBase, int> TimedMethods = new Dictionary<MethodBase, int>();
    private static long[] PendingTicks, PendingCalls;
    private static bool timingManaged;

    private static void TimerPrefix(MethodBase __originalMethod, out long __state)
    {
        __state = timingManaged ? Stopwatch.GetTimestamp() : 0;
    }

    private static void TimerPostfix(MethodBase __originalMethod, long __state)
    {
        if (__state == 0 || !timingManaged) return;
        long elapsedTicks = Stopwatch.GetTimestamp() - __state;
        int index;
        if (TimedMethods.TryGetValue(__originalMethod, out index))
        {
            PendingTicks[index] += elapsedTicks;
            PendingCalls[index]++;
        }
    }

    private static Dictionary<string, MethodInfo> CallbackMethods()
    {
        Dictionary<string, MethodInfo> result = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        HashSet<string> ambiguous = new HashSet<string>(StringComparer.Ordinal);
        string[] callbacks = { "Update", "LateUpdate", "FixedUpdate", "OnRenderImage", "OnWillRenderObject", "OnPreCull", "OnPreRender", "OnPostRender" };
        foreach (MonoBehaviour behavior in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (!ActiveSceneBehavior(behavior)) continue;
            Type type = behavior.GetType();
            if (type == typeof(FrameProfiler)) continue;
            for (; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (Array.IndexOf(callbacks, method.Name) >= 0 && !method.IsAbstract && !method.ContainsGenericParameters)
                    {
                        string key = type.FullName + "." + method.Name;
                        MethodInfo existing;
                        if (ambiguous.Contains(key)) continue;
                        if (result.TryGetValue(key, out existing) && existing != method)
                        {
                            ambiguous.Add(key);
                            result.Remove(key);
                            continue;
                        }
                        result[key] = method;
                    }
        }
        return result;
    }

    private static bool ActiveSceneBehavior(MonoBehaviour behavior)
    {
        return behavior != null && behavior.enabled && behavior.gameObject.activeInHierarchy &&
            behavior.gameObject.scene.IsValid() && behavior.gameObject.scene.isLoaded;
    }

    private void Awake()
    {
        enabled = false;
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" ||
            Environment.GetEnvironmentVariable("FTK_FRAME_PROFILE") != "1") return;
        try
        {
            root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
            string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
            DirectoryInfo directory = new DirectoryInfo(root);
            if (string.IsNullOrEmpty(requested) ||
                Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root ||
                directory.Parent == null || directory.Parent.Name != "scratch" ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Exact isolated scratch child root required without root symlink.");
            PluginInfo isolation;
            if (!Chainloader.PluginInfos.TryGetValue("com.ftkmf.runtime-model-test", out isolation) ||
                isolation.Instance == null || !isolation.Instance.enabled)
                throw new InvalidOperationException("Runtime model-test isolation plugin must be active.");
            enabled = true;
            Logger.LogInfo("Frame profiler ready: frame-profile-command.json");
        }
        catch (Exception error) { Logger.LogError("Frame profiler refused: " + error.Message); }
    }

    private void Update()
    {
        if (frames != null)
        {
            try
            {
                // Never poll files, format values, allocate rows, or log while timing.
                if (warmup > 0)
                {
                    warmup--;
                    if (warmup == 0)
                    {
                        resourcesStart = ResourceSnapshot.Read();
                        captureWallStart = Stopwatch.GetTimestamp();
                        gc0Start = GC.CollectionCount(0);
                        gc1Start = GC.CollectionCount(1);
                        gc2Start = GC.CollectionCount(2);
                        managedBytesStart = GC.GetTotalMemory(false);
                        focusStart = previousFocus = Application.isFocused;
                        backgroundStart = Application.runInBackground;
                        focusChangedCount = focusedFrames = 0;
                        started = Time.realtimeSinceStartup;
                        timingManaged = true;
                    }
                    return;
                }
                frames[count] = Time.unscaledDeltaTime;
                bool focus = Application.isFocused;
                focused[count] = focus;
                if (focus != previousFocus) focusChangedCount++;
                if (focus) focusedFrames++;
                previousFocus = focus;
                for (int i = 0; i < recorders.Length; i++)
                {
                    elapsed[count, i] = recorders[i].elapsedNanoseconds;
                    blocks[count, i] = recorders[i].sampleBlockCount;
                }
                for (int i = 0; i < managedNames.Length; i++)
                {
                    managedTicks[count, i] = PendingTicks[i];
                    managedCalls[count, i] = PendingCalls[i];
                    PendingTicks[i] = 0;
                    PendingCalls[i] = 0;
                }
                count++;
                if (count >= frames.Length || Time.realtimeSinceStartup - started >= maxSeconds) Finish();
            }
            catch (Exception error) { Fail(error); }
            return;
        }
        if (Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.5f;
        resultReserved = false;
        try
        {
            string path = Path.Combine(root, "frame-profile-command.json");
            if (!File.Exists(path)) return;
            string command;
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > 16384) throw new InvalidOperationException("Command exceeds 16 KiB.");
                using (StreamReader reader = new StreamReader(stream)) command = reader.ReadToEnd();
            }
            if (command == previous) return;
            previous = command;
            resultReserved = false;
            metadata = null;
            JObject request = JObject.Parse(command);
            string id = (string)request["id"];
            if (string.IsNullOrEmpty(id) || id.Length > 64) throw new InvalidOperationException("Invalid id.");
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '-' && c != '_') throw new InvalidOperationException("Invalid id.");
            resultPath = Path.Combine(root, "frame-profile-" + id + ".json");
            using (FileStream reservation = new FileStream(resultPath, FileMode.CreateNew)) { }
            resultReserved = true;
            metadata = Inventory();
            metadata["id"] = id;
            string action = (string)request["action"];
            if (action == "inventory") { metadata["status"] = "complete"; File.WriteAllText(resultPath, metadata.ToString()); }
            else if (action == "capture") StartCapture(request);
            else throw new InvalidOperationException("Expected inventory or capture.");
        }
        catch (Exception error) { Fail(error); }
    }

    private void StartCapture(JObject request)
    {
        if (timerHarmony != null || cleanupFailed) throw new InvalidOperationException("Previous capture cleanup failed; restart the isolated player.");
        int capacity = request["frames"] == null ? 600 : (int)request["frames"];
        maxSeconds = request["seconds"] == null ? 60 : (float)request["seconds"];
        if (capacity < 1 || capacity > 10000 || float.IsNaN(maxSeconds) || maxSeconds < 1 || maxSeconds > 300)
            throw new InvalidOperationException("frames 1..10000 and seconds 1..300 required.");
        JArray selected = request["samplers"] as JArray;
        if (selected == null || selected.Count > 128) throw new InvalidOperationException("samplers array required, at most 128.");
        names = new string[selected.Count];
        recorders = new Recorder[selected.Count];
        originalEnabled = new bool[selected.Count];
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < selected.Count; i++)
        {
            names[i] = (string)selected[i];
            if (string.IsNullOrEmpty(names[i]) || !unique.Add(names[i])) throw new InvalidOperationException("Invalid or duplicate sampler.");
            recorders[i] = Recorder.Get(names[i]);
            if (!recorders[i].isValid) throw new InvalidOperationException("Unavailable sampler: " + names[i]);
            originalEnabled[i] = recorders[i].enabled;
        }
        JArray requestedManaged = request["managedMethods"] as JArray;
        if (requestedManaged != null && requestedManaged.Count > 256) throw new InvalidOperationException("At most 256 managed methods.");
        managedNames = new string[requestedManaged == null ? 0 : requestedManaged.Count];
        PendingTicks = new long[managedNames.Length];
        PendingCalls = new long[managedNames.Length];
        managedTicks = new long[capacity, managedNames.Length];
        managedCalls = new long[capacity, managedNames.Length];
        Dictionary<string, MethodInfo> available = CallbackMethods();
        List<MethodInfo> methods = new List<MethodInfo>();
        HashSet<string> uniqueManaged = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < managedNames.Length; i++)
        {
            managedNames[i] = (string)requestedManaged[i];
            MethodInfo method;
            if (string.IsNullOrEmpty(managedNames[i]) || !uniqueManaged.Add(managedNames[i]) || !available.TryGetValue(managedNames[i], out method))
                throw new InvalidOperationException("Unavailable or duplicate managed callback: " + managedNames[i]);
            methods.Add(method);
        }
        if (methods.Count > 0)
        {
            timerHarmony = new Harmony(TimerOwner);
            for (int i = 0; i < methods.Count; i++)
            {
                TimedMethods.Add(methods[i], i);
                timerHarmony.Patch(methods[i], new HarmonyMethod(typeof(FrameProfiler).GetMethod("TimerPrefix", BindingFlags.Static | BindingFlags.NonPublic)),
                    new HarmonyMethod(typeof(FrameProfiler).GetMethod("TimerPostfix", BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }
        elapsed = new long[capacity, names.Length];
        blocks = new int[capacity, names.Length];
        frames = new float[capacity];
        focused = new bool[capacity];
        count = 0;
        warmup = 120;
        metadata["managedMethods"] = new JArray(managedNames);
        metadata["stopwatchFrequency"] = Stopwatch.Frequency;
        metadata["selectedSamplers"] = new JArray(names);
        metadata["status"] = "warming";
        metadata["warmupFrames"] = warmup;
        metadata["maxFrames"] = capacity;
        metadata["maxSeconds"] = maxSeconds;
        File.WriteAllText(resultPath, metadata.ToString());
        foreach (Recorder recorder in recorders) recorder.enabled = true;
    }

    private Exception Restore()
    {
        timingManaged = false;
        Exception firstError = null;
        if (timerHarmony != null)
        {
            try { timerHarmony.UnpatchSelf(); timerHarmony = null; }
            catch (Exception error) { firstError = error; }
        }
        TimedMethods.Clear();
        if (recorders != null)
        {
            for (int i = 0; i < recorders.Length; i++)
            {
                try
                {
                    if (recorders[i] != null && recorders[i].isValid) recorders[i].enabled = originalEnabled[i];
                }
                catch (Exception error) { if (firstError == null) firstError = error; }
            }
        }
        recorders = null;
        if (firstError != null) cleanupFailed = true;
        return firstError;
    }

    private void ReleaseBuffers()
    {
        frames = null;
        focused = null;
        elapsed = null;
        blocks = null;
        managedTicks = null;
        managedCalls = null;
        PendingTicks = null;
        PendingCalls = null;
        originalEnabled = null;
        resourcesStart = null;
    }

    private void Fail(Exception error)
    {
        Exception cleanupError = Restore();
        ReleaseBuffers();
        Logger.LogError("Frame profiler failed: " + error);
        if (cleanupError != null) Logger.LogError("Frame profiler cleanup failed; restart the isolated player: " + cleanupError);
        if (!resultReserved) return;
        try
        {
            if (metadata == null) metadata = new JObject();
            metadata["status"] = "failed";
            metadata["error"] = error.ToString();
            if (cleanupError != null) metadata["cleanupError"] = cleanupError.ToString();
            File.WriteAllText(resultPath, metadata.ToString());
        }
        catch (Exception writeError) { Logger.LogError("Frame profiler failure report could not be written: " + writeError); }
    }

    private void OnDisable()
    {
        if (frames != null) Fail(new OperationCanceledException("Profiler disabled during capture."));
        else
        {
            Exception error = Restore();
            ReleaseBuffers();
            if (error != null) Logger.LogError("Frame profiler cleanup failed: " + error);
        }
    }

    private void Finish()
    {
        timingManaged = false;
        try
        {
            // Boundary-only observations, before unpatching and serialization allocate.
            long captureWallEnd = Stopwatch.GetTimestamp();
            int gc0End = GC.CollectionCount(0), gc1End = GC.CollectionCount(1), gc2End = GC.CollectionCount(2);
            long managedBytesEnd = GC.GetTotalMemory(false);
            ResourceSnapshot resourcesEnd = ResourceSnapshot.Read();
            metadata["resources"] = ResourceSnapshot.Interval(resourcesStart, resourcesEnd, captureWallStart, captureWallEnd);
            metadata["focusStart"] = focusStart;
            metadata["focusEnd"] = previousFocus;
            metadata["focusChangedCount"] = focusChangedCount;
            metadata["focusedFrames"] = focusedFrames;
            metadata["unfocusedFrames"] = count - focusedFrames;
            metadata["runInBackgroundStart"] = backgroundStart;
            metadata["runInBackgroundEnd"] = Application.runInBackground;
            metadata["focusSemantics"] = "Application.isFocused sampled once at each profiler Update and at capture start. Transitions between samples may be missed. No application settings changed.";
            metadata["gcCollectionsStart"] = new JArray(gc0Start, gc1Start, gc2Start);
            metadata["gcCollectionsEnd"] = new JArray(gc0End, gc1End, gc2End);
            metadata["gcCollectionsDelta"] = new JArray(gc0End - gc0Start, gc1End - gc1Start, gc2End - gc2Start);
            metadata["managedHeapBytesStart"] = managedBytesStart;
            metadata["managedHeapBytesEnd"] = managedBytesEnd;
            metadata["managedHeapBytesDelta"] = managedBytesEnd - managedBytesStart;
            metadata["gcSemantics"] = "CollectionCount generations 0/1/2 and GetTotalMemory(false) at end of warmup and after last sample, before cleanup/output. No forced collection. Heap delta is not allocated bytes; Mono generation counts may alias.";
            Exception cleanupError = Restore();
            if (cleanupError != null) throw new InvalidOperationException("Capture cleanup failed.", cleanupError);
            string csv = Path.ChangeExtension(resultPath, ".csv");
            using (StreamWriter writer = new StreamWriter(csv))
            {
                writer.Write("sample,frame_ms,is_focused");
                foreach (string name in names) writer.Write(",\"" + name.Replace("\"", "\"\"") + " ns\",\"" + name.Replace("\"", "\"\"") + " blocks\"");
                foreach (string name in managedNames) writer.Write(",\"" + name.Replace("\"", "\"\"") + " ticks\",\"" + name.Replace("\"", "\"\"") + " calls\"");
                writer.WriteLine();
                for (int row = 0; row < count; row++)
                {
                    writer.Write(row.ToString(CultureInfo.InvariantCulture));
                    writer.Write("," + (frames[row] * 1000.0).ToString("R", CultureInfo.InvariantCulture));
                    writer.Write(focused[row] ? ",1" : ",0");
                    for (int column = 0; column < names.Length; column++)
                        writer.Write("," + elapsed[row, column].ToString(CultureInfo.InvariantCulture) + "," + blocks[row, column].ToString(CultureInfo.InvariantCulture));
                    for (int column = 0; column < managedNames.Length; column++)
                        writer.Write("," + managedTicks[row, column].ToString(CultureInfo.InvariantCulture) + "," + managedCalls[row, column].ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine();
                }
            }
            JArray managedTotals = new JArray();
            for (int i = 0; i < managedNames.Length; i++)
            {
                long ticks = 0, calls = 0;
                for (int row = 0; row < count; row++) { ticks += managedTicks[row, i]; calls += managedCalls[row, i]; }
                managedTotals.Add(new JObject { { "method", managedNames[i] }, { "inclusiveMilliseconds", ticks * 1000.0 / Stopwatch.Frequency }, { "calls", calls } });
            }
            metadata["managedTotals"] = managedTotals;
            metadata["managedSemantics"] = "Inclusive Stopwatch ticks from temporary Harmony prefix/postfix. Overlapping callbacks must not be summed. Timing/patched-call overhead is included. Calls throwing exceptions may be omitted. Per-frame boundaries depend on profiler Update order; aggregate totals are diagnostic only.";
            metadata["status"] = "complete";
            metadata["capturedFrames"] = count;
            metadata["csv"] = Path.GetFileName(csv);
            metadata["recorderSemantics"] = "Read at Update. Native recorder aggregation and frame alignment are engine-defined; nested samplers overlap and must not be summed. Zero does not prove no work. Not GPU timings.";
            File.WriteAllText(resultPath, metadata.ToString());
            Logger.LogInfo("Frame capture complete: " + resultPath);
        }
        catch (Exception error) { Fail(error); }
        finally { ReleaseBuffers(); }
    }

    private static JObject Inventory()
    {
        List<string> samplers = new List<string>();
        Sampler.GetNames(samplers);
        samplers.Sort(StringComparer.Ordinal);
        JArray cameras = new JArray();
        foreach (Camera camera in UnityEngine.Object.FindObjectsOfType<Camera>())
        {
            if (!camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded) continue;
            JArray components = new JArray();
            foreach (Component component in camera.GetComponents<Component>())
            {
                if (component == null) continue;
                Behaviour behavior = component as Behaviour;
                JObject fields = new JObject();
                foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    Type fieldType = field.FieldType;
                    if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(Color) ||
                        fieldType == typeof(Vector2) || fieldType == typeof(Vector3) || fieldType == typeof(Vector4))
                    {
                        object value = field.GetValue(component);
                        fields[field.Name] = fieldType.IsPrimitive ? new JValue(value) : new JValue(value.ToString());
                    }
                }
                components.Add(new JObject { { "type", component.GetType().FullName }, { "enabled", behavior == null ? (JToken)new JValue((object)null) : new JValue(behavior.enabled) }, { "publicFields", fields } });
            }
            cameras.Add(new JObject { { "name", camera.name }, { "scene", camera.gameObject.scene.name }, { "enabled", camera.enabled }, { "active", camera.gameObject.activeInHierarchy },
                { "main", camera == Camera.main }, { "depth", camera.depth }, { "renderingPath", camera.renderingPath.ToString() },
                { "actualRenderingPath", camera.actualRenderingPath.ToString() }, { "pixelRect", camera.pixelRect.ToString() },
                { "targetTexture", camera.targetTexture == null ? null : camera.targetTexture.name }, { "components", components } });
        }
        Dictionary<string, int> activeTypes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (MonoBehaviour behavior in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (!ActiveSceneBehavior(behavior)) continue;
            string key = behavior.GetType().FullName;
            int value;
            activeTypes.TryGetValue(key, out value);
            activeTypes[key] = value + 1;
        }
        JObject typeCounts = new JObject();
        List<string> types = new List<string>(activeTypes.Keys);
        types.Sort(StringComparer.Ordinal);
        foreach (string type in types) typeCounts[type] = activeTypes[type];
        List<string> callbacks = new List<string>(CallbackMethods().Keys);
        callbacks.Sort(StringComparer.Ordinal);
        JArray binaries = new JArray();
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string path;
            try { path = assembly.Location; } catch (NotSupportedException) { continue; }
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) binaries.Add(new JObject { { "name", Path.GetFileName(path) }, { "sha256", BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() } });
        }
        return new JObject { { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }, { "unity", Application.unityVersion },
            { "resources", ResourceSnapshot.Read().Json() }, { "resourceSemantics", ResourceSnapshot.Semantics },
            { "isFocused", Application.isFocused }, { "runInBackground", Application.runInBackground },
            { "width", Screen.width }, { "height", Screen.height }, { "fullscreen", Screen.fullScreen }, { "qualityLevel", QualitySettings.GetQualityLevel() },
            { "vSyncCount", QualitySettings.vSyncCount }, { "targetFrameRate", Application.targetFrameRate },
            { "activeMonoBehaviourCounts", typeCounts }, { "managedCallbacks", new JArray(callbacks) }, { "samplers", new JArray(samplers) }, { "cameras", cameras }, { "loadedBinaryHashes", binaries } };
    }
}
