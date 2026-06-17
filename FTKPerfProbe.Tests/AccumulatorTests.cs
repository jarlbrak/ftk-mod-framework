using Xunit;

namespace FTKPerfProbe.Tests
{
    public class AccumulatorTests
    {
        [Fact]
        public void SimpleSpan_CountsOneCall_AndElapsedTicks()
        {
            var a = new Accumulator();
            a.Enter(100); a.Exit(150);
            int calls; long ticks; a.SnapshotAndReset(out calls, out ticks);
            Assert.Equal(1, calls);
            Assert.Equal(50L, ticks);
        }

        [Fact]
        public void NestedSpans_CountAllCalls_ButOnlyOutermostWallTime()
        {
            var a = new Accumulator();
            a.Enter(100); a.Enter(120); a.Exit(140); a.Exit(160);
            int calls; long ticks; a.SnapshotAndReset(out calls, out ticks);
            Assert.Equal(2, calls);
            Assert.Equal(60L, ticks);
        }

        [Fact]
        public void SnapshotAndReset_ClearsCountsForNextFrame()
        {
            var a = new Accumulator();
            a.Enter(0); a.Exit(10);
            int c; long t; a.SnapshotAndReset(out c, out t);
            a.SnapshotAndReset(out c, out t);
            Assert.Equal(0, c);
            Assert.Equal(0L, t);
        }

        [Fact]
        public void UnmatchedExit_DoesNotThrow_AndAddsNoTime()
        {
            var a = new Accumulator();
            a.Exit(100);
            int c; long t; a.SnapshotAndReset(out c, out t);
            Assert.Equal(0, c);
            Assert.Equal(0L, t);
        }
    }
}
