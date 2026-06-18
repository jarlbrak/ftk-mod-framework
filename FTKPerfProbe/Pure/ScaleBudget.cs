namespace FTKPerfProbe
{
    /// <summary>
    /// The tunable budget knobs, as plain data. Pure: NO Unity, NO BepInEx, NO I/O. These come from the
    /// Diagnostics config section (never hard-coded literals inside the Pure math) and are passed into
    /// <see cref="ScaleBudgetEval.Compare"/>. The defaults documented here are the config-bind defaults
    /// for reference only; the authoritative defaults live on the BepInEx Config.Bind calls.
    ///
    ///   load budget   = max(BaselineLoadMs * LoadMsHeadroomMultiplier, LoadMsAbsoluteFloorMs)
    ///   memory budget = max(BaselineHeapBytes * MemoryHeadroomMultiplier, MemoryAbsoluteFloorBytes)
    ///   save budget   = RegisteredEntries * SaveSizePerEntryBudgetBytes
    /// </summary>
    public sealed class ScaleBudget
    {
        /// <summary>Multiplier on the baseline load-ms (default 2.0).</summary>
        public readonly double LoadMsHeadroomMultiplier;

        /// <summary>Absolute floor for the load budget in ms (default 1000): keeps a fast vanilla load from tripping.</summary>
        public readonly long LoadMsAbsoluteFloorMs;

        /// <summary>Multiplier on the baseline heap-bytes (default 2.0).</summary>
        public readonly double MemoryHeadroomMultiplier;

        /// <summary>Absolute floor for the memory budget in bytes (default 67108864 = 64 MiB).</summary>
        public readonly long MemoryAbsoluteFloorBytes;

        /// <summary>Per-high-band-id save-size footprint in bytes (default 64). The proxy's per-entry cost.</summary>
        public readonly long SaveSizePerEntryBudgetBytes;

        public ScaleBudget(
            double loadMsHeadroomMultiplier,
            long loadMsAbsoluteFloorMs,
            double memoryHeadroomMultiplier,
            long memoryAbsoluteFloorBytes,
            long saveSizePerEntryBudgetBytes)
        {
            LoadMsHeadroomMultiplier = loadMsHeadroomMultiplier;
            LoadMsAbsoluteFloorMs = loadMsAbsoluteFloorMs;
            MemoryHeadroomMultiplier = memoryHeadroomMultiplier;
            MemoryAbsoluteFloorBytes = memoryAbsoluteFloorBytes;
            SaveSizePerEntryBudgetBytes = saveSizePerEntryBudgetBytes;
        }
    }
}
