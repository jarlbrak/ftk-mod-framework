using Xunit;

namespace FTKPerfProbe.Tests
{
    public class FrameStatsTests
    {
        [Fact]
        public void MeanMaxCount_OverSmallSet()
        {
            var s = new FrameStats(10);
            s.Push(10); s.Push(20); s.Push(30);
            Assert.Equal(3, s.Count);
            Assert.Equal(20.0, s.MeanMs(), 6);
            Assert.Equal(30.0, s.MaxMs(), 6);
        }

        [Fact]
        public void MeanFps_IsInverseOfMeanMs()
        {
            var s = new FrameStats(10);
            s.Push(20); s.Push(20);
            Assert.Equal(50.0, s.MeanFps(), 6);
        }

        [Fact]
        public void RingBuffer_DropsOldest_WhenOverCapacity()
        {
            var s = new FrameStats(2);
            s.Push(10); s.Push(20); s.Push(30);
            Assert.Equal(2, s.Count);
            Assert.Equal(25.0, s.MeanMs(), 6);
        }

        [Fact]
        public void OnePercentLow_IsThe99thPercentileDuration()
        {
            var s = new FrameStats(200);
            for (int i = 1; i <= 100; i++) s.Push(i);
            Assert.Equal(99.0, s.OnePercentLowMs(), 6);
        }

        [Fact]
        public void Empty_ReturnsZeros()
        {
            var s = new FrameStats(10);
            Assert.Equal(0.0, s.MeanMs(), 6);
            Assert.Equal(0.0, s.OnePercentLowMs(), 6);
            Assert.Equal(0.0, s.MeanFps(), 6);
        }
    }
}
