using System;
using System.IO;
using Newtonsoft.Json;
using FTKPerfProbe;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>
    /// The ONLY persisted file in the scale-budget gate: the calibration baseline JSON. Reads and writes a
    /// <see cref="BaselineRecord"/> through the game's bundled Newtonsoft (same dependency the data loader
    /// uses, no new runtime dep). There is no run-over-run history, no deltas, no trend lines: just one
    /// baseline file that a calibration run writes and every later run reads.
    ///
    /// P5a definition of "no readable baseline" = the file is MISSING or UNPARSEABLE. Both produce a
    /// calibration run (TryLoad returns false). P5c (#24) adds schema / framework-version STALENESS via
    /// <see cref="IsStale"/>: a baseline whose schema version or framework version does not match this build
    /// is treated as absent (the gate recalibrates, it never FAILs). The schema/version authority lives here
    /// so "current" is defined in one place; the gate calls IsStale rather than re-deriving the rule.
    /// </summary>
    internal static class BaselineStore
    {
        public const int CurrentSchemaVersion = 1;
        private const string FileName = "scale-baseline.json";

        /// <summary>
        /// True when a loaded baseline is STALE: its on-disk schema version differs from
        /// <see cref="CurrentSchemaVersion"/>, or its recorded framework version differs from the running
        /// <see cref="Plugin.Version"/>. A stale baseline is treated as absent by the gate (it recalibrates,
        /// never FAILs), because a baseline taken under a different framework build / schema is not a
        /// trustworthy anchor for this build's load. Centralised here so the schema/version definitions stay
        /// in one place. A null record is reported stale (defensive; the gate only calls this on a loaded one).
        /// </summary>
        public static bool IsStale(BaselineRecord b)
        {
            if (b == null) return true;
            return b.SchemaVersion != CurrentSchemaVersion ||
                   !string.Equals(b.FrameworkVersion, Plugin.Version, StringComparison.Ordinal);
        }

        /// <summary>The baseline file path under the configured output directory.</summary>
        public static string PathFor(string outputDir)
        {
            return Path.Combine(outputDir, FileName);
        }

        /// <summary>
        /// Try to load a readable baseline. Returns false (and a null out) when the file is missing or
        /// cannot be parsed: P5a treats both as "calibrate". Never throws.
        /// </summary>
        public static bool TryLoad(string outputDir, out BaselineRecord record)
        {
            record = null;
            string path = PathFor(outputDir);
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                BaselineRecord parsed = JsonConvert.DeserializeObject<BaselineRecord>(json);
                if (parsed == null) return false; // "null" / empty content: treat as no baseline.
                record = parsed;
                return true;
            }
            catch (Exception e)
            {
                // Unparseable / unreadable: not an error to surface, just calibrate. Note it for diagnostics.
                Plugin.Log.LogWarning("ScaleBudget: baseline at '" + path + "' unreadable (" + e.Message +
                    "); will recalibrate.");
                return false;
            }
        }

        /// <summary>
        /// Write a freshly calibrated baseline, creating the output directory if needed. Returns true on
        /// success. Failures are caught and logged (the gate must never crash the content load).
        /// </summary>
        public static bool TryWrite(string outputDir, BaselineRecord record)
        {
            try
            {
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                string json = JsonConvert.SerializeObject(record, Formatting.Indented);
                File.WriteAllText(PathFor(outputDir), json);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("ScaleBudget: could not write baseline to '" + outputDir + "' (" +
                    e.Message + ").");
                return false;
            }
        }
    }
}
