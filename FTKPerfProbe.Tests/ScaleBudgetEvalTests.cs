using System.Collections.Generic;
using Xunit;

namespace FTKPerfProbe.Tests
{
    /// <summary>
    /// Tests the pure budget math. No Unity, no I/O. The calibration (no-baseline) case is the gate's
    /// responsibility, not Compare's; here we model it as "a baseline written from the current metrics
    /// always passes the very same metrics" (the invariant the gate relies on on a calibration run).
    /// </summary>
    public class ScaleBudgetEvalTests
    {
        // Budget with multipliers of 2.0 and small floors, so a non-trivial baseline drives the budget
        // (the floor only wins for near-zero baselines, exercised by its own test below).
        private static ScaleBudget Budget()
        {
            return new ScaleBudget(
                loadMsHeadroomMultiplier: 2.0,
                loadMsAbsoluteFloorMs: 100,
                memoryHeadroomMultiplier: 2.0,
                memoryAbsoluteFloorBytes: 1024,
                saveSizePerEntryBudgetBytes: 64);
        }

        private static BaselineRecord Baseline(long loadMs, long heapBytes)
        {
            BaselineRecord b = new BaselineRecord();
            b.BaselineLoadMs = loadMs;
            b.BaselineHeapBytes = heapBytes;
            b.SchemaVersion = 1;
            b.FrameworkVersion = "0.1.0";
            b.CalibratedAtUtc = "2026-01-01T00:00:00.0000000Z";
            b.CustomRowCountAtCalibration = 10;
            return b;
        }

        private static ScaleMetrics Metrics(long loadMs, long heapBytes, long saveProxyBytes, int registered)
        {
            return new ScaleMetrics(loadMs, heapBytes, saveProxyBytes, registered, 0, 0);
        }

        [Fact]
        public void WithinBudget_Passes_WithNoFailReasons()
        {
            // baseline 500ms / 10MB -> budgets 1000ms / 20MB; save: 10 entries * 64 = 640.
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            ScaleMetrics m = Metrics(900, 18 * 1024 * 1024, 600, 10);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.True(v.Pass);
            Assert.Empty(v.FailReasons);
            Assert.Equal(1000, v.LoadBudgetMs);
            Assert.Equal(20L * 1024 * 1024, v.HeapBudgetBytes);
            Assert.Equal(640, v.SaveProxyBudgetBytes);
            Assert.Equal(10, v.RegisteredEntries);
        }

        [Fact]
        public void LoadBreach_FailsWithExactlyOneReason()
        {
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            // load 1001 > 1000 budget; heap/save well within.
            ScaleMetrics m = Metrics(1001, 1 * 1024 * 1024, 100, 10);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.False(v.Pass);
            Assert.Single(v.FailReasons);
            Assert.Contains("load", v.FailReasons[0]);
        }

        [Fact]
        public void HeapBreach_FailsWithExactlyOneReason()
        {
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            // heap 21MB > 20MB budget; load/save within.
            ScaleMetrics m = Metrics(100, 21L * 1024 * 1024, 100, 10);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.False(v.Pass);
            Assert.Single(v.FailReasons);
            Assert.Contains("heap", v.FailReasons[0]);
        }

        [Fact]
        public void SaveProxyBreach_FailsWithExactlyOneReason()
        {
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            // 10 entries -> save budget 640; proxy 641 breaches it. load/heap within.
            ScaleMetrics m = Metrics(100, 1 * 1024 * 1024, 641, 10);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.False(v.Pass);
            Assert.Single(v.FailReasons);
            Assert.Contains("saveProxy", v.FailReasons[0]);
        }

        [Fact]
        public void AllThreeBreached_ProducesThreeReasons()
        {
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            ScaleMetrics m = Metrics(5000, 100L * 1024 * 1024, 100000, 10);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.False(v.Pass);
            Assert.Equal(3, v.FailReasons.Count);
        }

        [Fact]
        public void Floor_TakesPrecedence_OverNearZeroBaseline()
        {
            // Near-zero baseline: 2.0 * baseline would give a tiny budget; the absolute floor must win.
            BaselineRecord baseline = Baseline(1, 1);
            ScaleBudget budget = Budget(); // floors: 100ms, 1024B.

            // load 1*2 = 2, floor 100 wins -> 50ms passes; heap 1*2 = 2, floor 1024 wins -> 512B passes.
            ScaleMetrics m = Metrics(50, 512, 100, 5);
            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, budget);

            Assert.True(v.Pass);
            Assert.Equal(100, v.LoadBudgetMs);   // floor, not 2.
            Assert.Equal(1024, v.HeapBudgetBytes); // floor, not 2.

            // and a value above the floor (but above the scaled baseline) still fails on the floor basis.
            ScaleMetrics over = Metrics(101, 1025, 100, 5);
            ScaleVerdict ov = ScaleBudgetEval.Compare(over, baseline, budget);
            Assert.False(ov.Pass);
            Assert.Equal(2, ov.FailReasons.Count); // load + heap both just over their floors.
        }

        [Fact]
        public void CalibrationCase_BaselineFromOwnMetrics_AlwaysPasses()
        {
            // Models the gate's calibration run: a baseline derived from the current metrics is, by
            // construction, never a FAIL when re-evaluated against those same metrics (multiplier >= 1).
            ScaleMetrics m = Metrics(750, 33L * 1024 * 1024, 1280, 20);
            BaselineRecord baseline = Baseline(m.LoadMs, m.HeapBytes);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.True(v.Pass);
            Assert.Empty(v.FailReasons);
        }

        [Fact]
        public void ZeroEntries_SaveBudgetIsZero_ZeroProxyPasses()
        {
            // A sample-only / data-disabled load: 0 registered high-band rows -> save budget 0; a 0-byte
            // proxy is within a 0 budget (not greater-than), so it does not breach.
            BaselineRecord baseline = Baseline(500, 10 * 1024 * 1024);
            ScaleMetrics m = Metrics(100, 1 * 1024 * 1024, 0, 0);

            ScaleVerdict v = ScaleBudgetEval.Compare(m, baseline, Budget());

            Assert.True(v.Pass);
            Assert.Equal(0, v.SaveProxyBudgetBytes);
            Assert.Empty(v.FailReasons);
        }
    }
}
