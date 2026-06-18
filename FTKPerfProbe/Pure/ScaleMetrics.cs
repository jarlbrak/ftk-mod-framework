namespace FTKPerfProbe
{
    /// <summary>
    /// The measured numbers from one content load, as plain data. Pure: NO Unity, NO BepInEx, NO I/O,
    /// NO GC.* of its own. The caller measures (Stopwatch, GC.GetTotalMemory, Profiler) and hands the
    /// values in; this type only carries them so the budget math (<see cref="ScaleBudgetEval"/>) and the
    /// tests can operate on a deterministic, side-effect-free record.
    ///
    /// Only LoadMs, HeapBytes and SaveProxyBytes participate in the pass/fail decision. ProfilerUsedBytes
    /// and ProfilerHeapBytes are INFORMATIONAL ONLY (recorded for diagnostics, never budgeted): the gate's
    /// memory verdict is driven by the managed-heap figure (GC.GetTotalMemory(true)) for cross-machine
    /// reproducibility, not by the Mono profiler's allocator-pool numbers.
    /// </summary>
    public sealed class ScaleMetrics
    {
        /// <summary>Wall-clock load duration in milliseconds (the single existing Stopwatch).</summary>
        public readonly long LoadMs;

        /// <summary>Managed heap after load, from GC.GetTotalMemory(true). Budgeted (memory metric).</summary>
        public readonly long HeapBytes;

        /// <summary>
        /// Save-size PROXY in bytes: high-band registered row count * per-id footprint. A registration
        /// footprint estimate, NOT a real save measurement. Budgeted (save-size metric).
        /// </summary>
        public readonly long SaveProxyBytes;

        /// <summary>
        /// High-band synthetic-id row count this proxy is computed from (excludes positional classes). This
        /// is the WHOLE-PROCESS high-band count at gate time (sample + data + synthetic, i.e. every id minted
        /// through IdAllocator), NOT the per-load cached.Count from the "Data content load complete: X/Y"
        /// summary line. It is the value shown as N= in the verdict line; co-op clients that load identical
        /// content register an identical count, but the count varies with which optional content each client
        /// enabled.
        /// </summary>
        public readonly int RegisteredEntries;

        /// <summary>Informational only: Profiler.GetMonoUsedSizeLong(). Never budgeted.</summary>
        public readonly long ProfilerUsedBytes;

        /// <summary>Informational only: Profiler.GetMonoHeapSizeLong(). Never budgeted.</summary>
        public readonly long ProfilerHeapBytes;

        public ScaleMetrics(
            long loadMs,
            long heapBytes,
            long saveProxyBytes,
            int registeredEntries,
            long profilerUsedBytes,
            long profilerHeapBytes)
        {
            LoadMs = loadMs;
            HeapBytes = heapBytes;
            SaveProxyBytes = saveProxyBytes;
            RegisteredEntries = registeredEntries;
            ProfilerUsedBytes = profilerUsedBytes;
            ProfilerHeapBytes = profilerHeapBytes;
        }
    }
}
