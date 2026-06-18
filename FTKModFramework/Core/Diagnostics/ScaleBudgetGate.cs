using System.Globalization;
using System.IO;
using UnityEngine.Profiling;
using FTKPerfProbe;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>
    /// The scale-and-performance gate (P5a). Runs once per content load, AFTER LoadDataContent, inside the
    /// existing _done-guarded TableManager.Initialize postfix (via the Run(...) wrapper, so a throw is
    /// caught and the load continues). It:
    ///
    ///   1. captures metrics from the <see cref="LoadResult"/> + a one-shot heap/profiler read,
    ///   2. computes the save-size PROXY (high-band registered rows * a per-id footprint),
    ///   3. on a calibration run (no readable baseline) writes the baseline and emits CALIBRATED,
    ///   4. otherwise compares against the baseline+budget and emits PASS or FAIL,
    ///
    /// emitting EXACTLY ONE "SCALE-BUDGET ..." line via <see cref="Plugin.Log"/>. The pass/fail decision and
    /// budget arithmetic live in the Unity-free Pure layer (<see cref="ScaleBudgetEval"/>); this class only
    /// measures, persists, and formats.
    /// </summary>
    internal static class ScaleBudgetGate
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Evaluate the gate against a completed load. <paramref name="load"/> may be null when data
        /// content was disabled; in that case metrics are captured with registered/total/elapsed = 0 so
        /// the gate still emits exactly one line (the spec wants one line per load).
        /// </summary>
        public static void Evaluate(LoadResult load)
        {
            if (!Plugin.DiagnosticsEnableGate.Value)
                return; // disabled: emit NO SCALE-BUDGET line.

            ScaleMetrics metrics = CaptureMetrics(load);
            string outputDir = ResolveOutputDir(Plugin.DiagnosticsOutputDirectory.Value);
            ScaleBudget budget = BuildBudget();

            BaselineRecord baseline;
            if (!BaselineStore.TryLoad(outputDir, out baseline))
            {
                Calibrate(metrics, outputDir);
                return;
            }

            ScaleVerdict verdict = ScaleBudgetEval.Compare(metrics, baseline, budget);
            EmitVerdict(verdict);
        }

        /// <summary>
        /// Build the measured record. The managed heap is read ONCE via GC.GetTotalMemory(true) (a forced
        /// collection so the figure is reproducible across machines); the Mono profiler sizes are recorded
        /// informational-only and are NOT used in the pass/fail decision. The save proxy is COMPUTED from
        /// the high-band registered row count, never read from a file.
        /// </summary>
        private static ScaleMetrics CaptureMetrics(LoadResult load)
        {
            long loadMs = load != null ? load.ElapsedMs : 0L;

            // High-band registered rows only (IdAllocator.CustomIdCount): positional class ids are excluded
            // because classes register with an explicit id and never enter the allocator's map.
            int registered = IdAllocator.CustomIdCount;
            long saveProxyBytes = (long)registered * Plugin.DiagnosticsSaveSizePerEntryBytes.Value;

            // GC.GetTotalMemory(true): force a collection so the budgeted heap figure is stable run to run.
            long heapBytes = System.GC.GetTotalMemory(true);

            // Informational only (NOT budgeted). Present in Unity 2017.2 (UnityEngine.Profiling.Profiler).
            long profilerUsed = Profiler.GetMonoUsedSizeLong();
            long profilerHeap = Profiler.GetMonoHeapSizeLong();

            return new ScaleMetrics(loadMs, heapBytes, saveProxyBytes, registered, profilerUsed, profilerHeap);
        }

        /// <summary>Assemble the tunable budget from the Diagnostics config binds (never literals in Pure/).</summary>
        private static ScaleBudget BuildBudget()
        {
            return new ScaleBudget(
                Plugin.DiagnosticsLoadMsHeadroomMultiplier.Value,
                Plugin.DiagnosticsLoadMsAbsoluteFloorMs.Value,
                Plugin.DiagnosticsMemoryHeadroomMultiplier.Value,
                Plugin.DiagnosticsMemoryAbsoluteFloorBytes.Value,
                Plugin.DiagnosticsSaveSizePerEntryBytes.Value);
        }

        /// <summary>
        /// First run with no readable baseline: write the baseline JSON from the current metrics and emit
        /// CALIBRATED. Never emits FAIL. The four metadata fields are written now (for a stable JSON shape)
        /// even though P5a does not yet act on them.
        /// </summary>
        private static void Calibrate(ScaleMetrics m, string outputDir)
        {
            BaselineRecord record = new BaselineRecord();
            record.BaselineLoadMs = m.LoadMs;
            record.BaselineHeapBytes = m.HeapBytes;
            record.SchemaVersion = BaselineStore.CurrentSchemaVersion;
            record.FrameworkVersion = Plugin.Version;
            record.CalibratedAtUtc = System.DateTime.UtcNow.ToString("o");
            record.CustomRowCountAtCalibration = m.RegisteredEntries;

            BaselineStore.TryWrite(outputDir, record);

            // CALIBRATED uses the same line shape; the "budget" slots show the metrics' own values, since a
            // calibration run has nothing to compare against (the baseline IS these numbers).
            Plugin.Log.LogInfo(Line("CALIBRATED",
                m.LoadMs, m.LoadMs,
                m.HeapBytes, m.HeapBytes,
                m.SaveProxyBytes, m.SaveProxyBytes,
                m.RegisteredEntries));
        }

        /// <summary>Emit PASS (info) or FAIL (error, naming each breached metric) as exactly one line.</summary>
        private static void EmitVerdict(ScaleVerdict v)
        {
            string line = Line(v.Pass ? "PASS" : "FAIL",
                v.MeasuredLoadMs, v.LoadBudgetMs,
                v.MeasuredHeapBytes, v.HeapBudgetBytes,
                v.MeasuredSaveProxyBytes, v.SaveProxyBudgetBytes,
                v.RegisteredEntries);

            if (v.Pass)
            {
                Plugin.Log.LogInfo(line);
            }
            else
            {
                // Append the per-metric breach reasons so a FAIL names each metric it tripped.
                string reasons = string.Join("; ", v.FailReasons.ToArray());
                Plugin.Log.LogError(line + " breached: " + reasons);
            }
        }

        /// <summary>
        /// The exact verdict-line shape from the spec. saveProxy is explicitly labelled a registration
        /// footprint PROXY. Numbers are invariant-culture so the line is identical across locales/machines.
        ///   SCALE-BUDGET PASS|FAIL|CALIBRATED: load=&lt;ms&gt;/&lt;budget&gt; heap=&lt;bytes&gt;/&lt;budget&gt; saveProxy=&lt;bytes&gt;/&lt;budget&gt; (N=&lt;count&gt;)
        /// </summary>
        private static string Line(string verdict,
            long loadMs, long loadBudget,
            long heapBytes, long heapBudget,
            long saveProxy, long saveBudget,
            int n)
        {
            return "SCALE-BUDGET " + verdict + ": " +
                "load=" + loadMs.ToString(Inv) + "/" + loadBudget.ToString(Inv) +
                " heap=" + heapBytes.ToString(Inv) + "/" + heapBudget.ToString(Inv) +
                " saveProxy=" + saveProxy.ToString(Inv) + "/" + saveBudget.ToString(Inv) +
                " (N=" + n.ToString(Inv) + ")";
        }

        /// <summary>
        /// Resolve the configured output directory. A relative default like "BepInEx/FTKPerfProbe" is
        /// rooted at the BepInEx parent of the plugins dir so it lands beside the other BepInEx folders;
        /// an absolute path is used as-is.
        /// </summary>
        private static string ResolveOutputDir(string configured)
        {
            if (string.IsNullOrEmpty(configured)) configured = "BepInEx/FTKPerfProbe";
            if (Path.IsPathRooted(configured)) return configured;
            // BepInEx.Paths.BepInExRootPath is the "BepInEx" folder; the default already starts with
            // "BepInEx/", so root the remainder at BepInEx's PARENT to avoid "BepInEx/BepInEx/...".
            string gameRoot = Directory.GetParent(BepInEx.Paths.BepInExRootPath).FullName;
            return Path.Combine(gameRoot, configured);
        }
    }
}
