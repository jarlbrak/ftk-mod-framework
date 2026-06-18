namespace FTKPerfProbe
{
    /// <summary>
    /// The persisted calibration baseline: the reference load-ms and heap-bytes a later run is budgeted
    /// against. Pure data: NO Unity, NO BepInEx, NO I/O. The store (Core/Diagnostics/BaselineStore) is the
    /// only thing that reads/writes the JSON; this type just defines the SHAPE so that shape is stable.
    ///
    /// The four metadata fields (SchemaVersion, FrameworkVersion, CalibratedAtUtc,
    /// CustomRowCountAtCalibration) are written and read in P5a but NOT yet acted upon: P5c (#24) adds
    /// schema / framework-version staleness handling and the recalibrate flag. They live here NOW so the
    /// on-disk JSON shape never changes when P5c lands.
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
