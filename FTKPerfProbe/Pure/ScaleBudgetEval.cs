using System;
using System.Collections.Generic;

namespace FTKPerfProbe
{
    /// <summary>
    /// The pure result of comparing measured metrics against computed budgets. DATA ONLY: a pass flag,
    /// the per-metric measured values, the per-metric computed budgets, and one fail reason per breached
    /// metric. It deliberately contains NO "SCALE-BUDGET" marker string and NO composed log line: the
    /// gate (Core/Diagnostics/ScaleBudgetGate) owns the marker and the line shape so the verdict wording
    /// can change without touching the Pure math.
    /// </summary>
    public sealed class ScaleVerdict
    {
        /// <summary>True iff every budgeted metric is within budget.</summary>
        public readonly bool Pass;

        public readonly long MeasuredLoadMs;
        public readonly long LoadBudgetMs;

        public readonly long MeasuredHeapBytes;
        public readonly long HeapBudgetBytes;

        public readonly long MeasuredSaveProxyBytes;
        public readonly long SaveProxyBudgetBytes;

        public readonly int RegisteredEntries;

        /// <summary>One reason per breached metric; empty when <see cref="Pass"/> is true. Never null.</summary>
        public readonly List<string> FailReasons;

        public ScaleVerdict(
            bool pass,
            long measuredLoadMs, long loadBudgetMs,
            long measuredHeapBytes, long heapBudgetBytes,
            long measuredSaveProxyBytes, long saveProxyBudgetBytes,
            int registeredEntries,
            List<string> failReasons)
        {
            Pass = pass;
            MeasuredLoadMs = measuredLoadMs;
            LoadBudgetMs = loadBudgetMs;
            MeasuredHeapBytes = measuredHeapBytes;
            HeapBudgetBytes = heapBudgetBytes;
            MeasuredSaveProxyBytes = measuredSaveProxyBytes;
            SaveProxyBudgetBytes = saveProxyBudgetBytes;
            RegisteredEntries = registeredEntries;
            FailReasons = failReasons ?? new List<string>();
        }
    }

    /// <summary>
    /// Pure budget math. NO Unity, NO BepInEx, NO I/O, NO GC.*. Given the measured metrics, the persisted
    /// baseline, and the tunable budget, computes each metric's budget and whether it is breached. The
    /// verdict is PASS only when every budgeted metric is within budget; otherwise FAIL with one reason
    /// per breached metric. Marker strings and log lines are the gate's job, not this method's.
    /// </summary>
    public static class ScaleBudgetEval
    {
        /// <summary>
        /// Computes each metric's budget from the baseline and tunables, then returns a verdict that is
        /// PASS only when every budgeted metric is within budget (one fail reason per breach otherwise).
        ///
        /// Note on the save axis: until P5d calibrates a MEASURED per-entry save footprint, the save-size
        /// proxy equals its own budget by construction. Both sides are the SAME registered high-band count
        /// (<see cref="ScaleMetrics.RegisteredEntries"/>) times the SAME per-entry constant
        /// (<c>SaveSizePerEntryBudgetBytes</c>), so the save axis reports the registration-footprint
        /// magnitude in the verdict line but can never BREACH on a real run. The load and heap axes are the
        /// enforceable ones today. Do not edit only one of the two constants: keep them paired so the
        /// intended symmetry holds until P5d supplies a real measured per-entry footprint.
        /// </summary>
        public static ScaleVerdict Compare(ScaleMetrics metrics, BaselineRecord baseline, ScaleBudget budget)
        {
            // load budget   = max(baselineLoadMs * headroom, absoluteFloorMs)
            long loadBudget = Max(
                MulRound(baseline.BaselineLoadMs, budget.LoadMsHeadroomMultiplier),
                budget.LoadMsAbsoluteFloorMs);

            // memory budget = max(baselineHeapBytes * headroom, absoluteFloorBytes)
            long heapBudget = Max(
                MulRound(baseline.BaselineHeapBytes, budget.MemoryHeadroomMultiplier),
                budget.MemoryAbsoluteFloorBytes);

            // save budget   = registeredEntries * perEntryFootprint  (the proxy's own budget basis)
            long saveBudget = metrics.RegisteredEntries * budget.SaveSizePerEntryBudgetBytes;

            List<string> fails = new List<string>();
            if (metrics.LoadMs > loadBudget)
                fails.Add("load " + metrics.LoadMs + "ms > budget " + loadBudget + "ms");
            if (metrics.HeapBytes > heapBudget)
                fails.Add("heap " + metrics.HeapBytes + "B > budget " + heapBudget + "B");
            if (metrics.SaveProxyBytes > saveBudget)
                fails.Add("saveProxy " + metrics.SaveProxyBytes + "B > budget " + saveBudget + "B");

            return new ScaleVerdict(
                fails.Count == 0,
                metrics.LoadMs, loadBudget,
                metrics.HeapBytes, heapBudget,
                metrics.SaveProxyBytes, saveBudget,
                metrics.RegisteredEntries,
                fails);
        }

        /// <summary>value * multiplier, rounded to the nearest long (net35-safe; no checked overflow needed at these scales).</summary>
        private static long MulRound(long value, double multiplier)
        {
            return (long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
        }

        private static long Max(long a, long b)
        {
            return a > b ? a : b;
        }
    }
}
