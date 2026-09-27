using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEngine.Profiling;

// Capture boundaries and explicit inventory only. Never call from the sampled-frame loop.
internal sealed class ResourceSnapshot
{
    private readonly Dictionary<string, string> unavailable = new Dictionary<string, string>();
    internal long? CpuTicks;
    internal long CpuObservedAt;
    private long observedStart, observedEnd;
    private int? processId;
    private long? workingSet, unityAllocated, unityReserved, unityUnused, monoHeap, monoUsed, managedHeap;

    internal static ResourceSnapshot Read()
    {
        ResourceSnapshot value = new ResourceSnapshot();
        value.observedStart = Stopwatch.GetTimestamp();
        value.unityAllocated = value.Counter("unityAllocatedBytes", Profiler.GetTotalAllocatedMemoryLong, false);
        value.unityReserved = value.Counter("unityReservedBytes", Profiler.GetTotalReservedMemoryLong, false);
        value.unityUnused = value.Counter("unityUnusedReservedBytes", Profiler.GetTotalUnusedReservedMemoryLong, false);
        value.monoHeap = value.Counter("unityMonoHeapBytes", Profiler.GetMonoHeapSizeLong, false);
        value.monoUsed = value.Counter("unityMonoUsedBytes", Profiler.GetMonoUsedSizeLong, false);
        value.managedHeap = value.Counter("managedHeapBytes", delegate { return GC.GetTotalMemory(false); }, false);
        try
        {
            using (Process process = Process.GetCurrentProcess())
            {
                value.processId = process.Id;
                value.workingSet = value.Counter("processWorkingSetBytes", delegate { return process.WorkingSet64; }, true);
                value.CpuTicks = value.Counter("processCpuTimeTicks", delegate { return process.TotalProcessorTime.Ticks; }, true);
                value.CpuObservedAt = Stopwatch.GetTimestamp();
            }
        }
        catch (Exception error)
        {
            string reason = Describe(error);
            if (!value.CpuTicks.HasValue) value.unavailable["processCpuTimeTicks"] = reason;
            if (!value.workingSet.HasValue) value.unavailable["processWorkingSetBytes"] = reason;
        }
        value.observedEnd = Stopwatch.GetTimestamp();
        return value;
    }

    private long? Counter(string name, Func<long> read, bool zeroIsUnavailable)
    {
        try
        {
            long result = read();
            if (result < 0 || (result == 0 && zeroIsUnavailable))
            {
                unavailable[name] = "Runtime returned a nonpositive or invalid value; not treated as measured zero usage.";
                return null;
            }
            return result;
        }
        catch (Exception error) { unavailable[name] = Describe(error); return null; }
    }

    private static string Describe(Exception error) { return error.GetType().Name + ": " + error.Message; }
    private static JValue Number(long? value) { return new JValue(value.HasValue ? (object)value.Value : null); }
    internal JObject Json()
    {
        JObject errors = new JObject();
        foreach (KeyValuePair<string, string> item in unavailable) errors[item.Key] = item.Value;
        return new JObject {
            { "processId", new JValue(processId.HasValue ? (object)processId.Value : null) },
            { "processCpuTimeTicks", Number(CpuTicks) },
            { "processWorkingSetBytes", Number(workingSet) },
            { "unityAllocatedBytes", Number(unityAllocated) },
            { "unityReservedBytes", Number(unityReserved) },
            { "unityUnusedReservedBytes", Number(unityUnused) },
            { "unityMonoHeapBytes", Number(monoHeap) },
            { "unityMonoUsedBytes", Number(monoUsed) },
            { "managedHeapBytes", Number(managedHeap) },
            { "observationMilliseconds", (observedEnd - observedStart) * 1000.0 / Stopwatch.Frequency },
            { "unavailable", errors }
        };
    }

    internal static JObject Interval(ResourceSnapshot start, ResourceSnapshot end, long begin, long finish)
    {
        double wall = (finish - begin) / (double)Stopwatch.Frequency;
        double? cpuSeconds = null, percent = null, cpuWallSeconds = null;
        string reason = null;
        if (start != null && end != null && start.CpuTicks.HasValue && end.CpuTicks.HasValue)
        {
            long delta = end.CpuTicks.Value - start.CpuTicks.Value;
            double cpuWall = (end.CpuObservedAt - start.CpuObservedAt) / (double)Stopwatch.Frequency;
            if (delta >= 0 && cpuWall > 0)
            {
                cpuSeconds = delta / (double)TimeSpan.TicksPerSecond;
                cpuWallSeconds = cpuWall;
                percent = 100.0 * cpuSeconds.Value / cpuWall;
            }
            else reason = "CPU time moved backward or its observation interval was nonpositive.";
        }
        else reason = "One or both process CPU snapshots are unavailable; inspect endpoint unavailable fields.";
        return new JObject {
            { "start", start == null ? new JObject() : start.Json() },
            { "end", end == null ? new JObject() : end.Json() },
            { "captureWallSeconds", wall },
            { "processCpuSeconds", new JValue(cpuSeconds.HasValue ? (object)cpuSeconds.Value : null) },
            { "processCpuObservationWallSeconds", new JValue(cpuWallSeconds.HasValue ? (object)cpuWallSeconds.Value : null) },
            { "processCpuPercentOneCore", new JValue(percent.HasValue ? (object)percent.Value : null) },
            { "cpuUnavailableReason", reason },
            { "semantics", Semantics }
        };
    }

    internal const string Semantics = "Boundary observations, not peaks or allocation counts. CPU time is Process.TotalProcessorTime across all process threads in 100 ns TimeSpan ticks; percent uses its two Stopwatch observation timestamps and can exceed 100%. WorkingSet64 is the runtime's resident/working-set report, not a memory limit. Unity allocated/reserved/unused values are allocator snapshots, not lifetime allocations; they overlap with Mono/managed/process memory and must not be summed. Unity zero values can reflect unsupported release-player instrumentation. Endpoints are sequential, not atomic. No collection is forced. Boundary polling overhead can affect adjacent frame intervals; match instrumentation across comparisons.";
}
