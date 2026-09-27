using System;
using Xunit;

namespace FTKPerfProbe.Tests
{
    public class CaptureBufferTests
    {
        [Fact]
        public void FullBufferStopsWithoutOverwritingEarlyTailFrames()
        {
            var buffer = new CaptureBuffer(1000);
            for (int i = 0; i < 20; i++) Assert.True(buffer.TryAdd(new CaptureRow { UnscaledDtMs = 100 }));
            for (int i = 20; i < 1000; i++) Assert.True(buffer.TryAdd(new CaptureRow { UnscaledDtMs = 10 }));
            Assert.False(buffer.TryAdd(new CaptureRow { UnscaledDtMs = 999 }));
            var summary = buffer.Summarize();
            Assert.Equal(1000, buffer.Count);
            Assert.Equal(100, buffer[0].UnscaledDtMs);
            Assert.Equal(11.8, summary.MeanMs, 8);
            Assert.Equal(10, summary.P50Ms);
            Assert.Equal(10, summary.P95Ms);
            Assert.Equal(100, summary.P99Ms);
            Assert.Equal(100, summary.MaxMs);
        }

        [Fact]
        public void SummaryUsesOnlyCapturedRowsAndCountsCollections()
        {
            var buffer = new CaptureBuffer(100);
            buffer.TryAdd(new CaptureRow { UnscaledDtMs = 20, GcFired = true, Gc0Delta = 2 });
            var summary = buffer.Summarize();
            Assert.Equal(20, summary.P50Ms);
            Assert.Equal(20, summary.P95Ms);
            Assert.Equal(20, summary.P99Ms);
            Assert.Equal(1, summary.GcFrames);
            Assert.Equal(2, summary.GcCollections);
            Assert.Throws<ArgumentOutOfRangeException>(() => { var ignored = buffer[1]; });
        }

        [Fact]
        public void EmptySummaryIsZeroAndInvalidCapacityRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureBuffer(0));
            var summary = new CaptureBuffer(1).Summarize();
            Assert.Equal(0, summary.MeanMs);
            Assert.Equal(0, summary.P99Ms);
            Assert.Equal(0, summary.MaxMs);
        }

        [Fact]
        public void RecordingAllocatesNoManagedMemoryAfterConstruction()
        {
            var buffer = new CaptureBuffer(1000);
            buffer.TryAdd(new CaptureRow());
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i < 1000; i++) buffer.TryAdd(new CaptureRow { Frame = i });
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        }
    }
}
