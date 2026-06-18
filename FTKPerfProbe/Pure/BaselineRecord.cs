namespace FTKPerfProbe
{
    /// <summary>
    /// The persisted calibration baseline: the reference load-ms and heap-bytes a later run is budgeted
    /// against. Pure data: NO Unity, NO BepInEx, NO I/O. The store (Core/Diagnostics/BaselineStore) is the
    /// only thing that reads/writes the JSON; this type just defines the SHAPE so that shape is stable.
    ///
    /// The four metadata fields (SchemaVersion, FrameworkVersion, CalibratedAtUtc,
    /// CustomRowCountAtCalibration) were written and read in P5a and are now ACTED UPON by P5c (#24):
    /// SchemaVersion + FrameworkVersion drive staleness (a baseline from a different build/schema is treated
    /// as absent, see BaselineStore.IsStale), and a nonzero CustomRowCountAtCalibration triggers the
    /// poisoned-anchor warning on a normal gating run. CalibratedAtUtc remains informational.
    ///
    /// Public settable fields (not readonly): the JSON round-trips through Newtonsoft, which needs a
    /// parameterless ctor and writable members. Mutability is confined to (de)serialization; the gate
    /// treats a loaded baseline as read-only.
    /// </summary>
    public sealed class BaselineRecord
    {
        /// <summary>Baseline load duration in ms; the load-budget basis.</summary>
        public long BaselineLoadMs;

        /// <summary>Baseline managed-heap bytes; the memory-budget basis.</summary>
        public long BaselineHeapBytes;

        /// <summary>Baseline-file schema version. P5c uses this for staleness; P5a only stores it.</summary>
        public int SchemaVersion;

        /// <summary>Framework version (Plugin.Version) at calibration. P5c uses this; P5a only stores it.</summary>
        public string FrameworkVersion;

        /// <summary>ISO-8601 UTC timestamp of calibration (DateTime.UtcNow.ToString("o")).</summary>
        public string CalibratedAtUtc;

        /// <summary>High-band registered row count when this baseline was calibrated.</summary>
        public int CustomRowCountAtCalibration;
    }
}
