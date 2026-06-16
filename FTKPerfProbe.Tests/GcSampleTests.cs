using Xunit;

namespace FTKPerfProbe.Tests
{
    public class GcSampleTests
    {
        [Fact]
        public void FirstSample_IsPrimingOnly()
        {
            var g = new GcSample();
            var f = g.Update(0, 1000);
            Assert.False(f.GcFired);
            Assert.Equal(0, f.Gc0Delta);
            Assert.Equal(-1L, f.AllocEstBytes);
            Assert.Equal(1000L, f.HeapBytes);
        }

        [Fact]
        public void HeapGrowthWithoutCollection_IsAllocEstimate()
        {
            var g = new GcSample();
            g.Update(0, 1000);
            var f = g.Update(0, 1500);
            Assert.False(f.GcFired);
            Assert.Equal(500L, f.AllocEstBytes);
        }

        [Fact]
        public void CollectionDelta_FlagsGcFired_AndAllocUnknown()
        {
            var g = new GcSample();
            g.Update(0, 1000);
            var f = g.Update(1, 600);
            Assert.True(f.GcFired);
            Assert.Equal(1, f.Gc0Delta);
            Assert.Equal(-1L, f.AllocEstBytes);
        }

        [Fact]
        public void HeapShrinkWithoutCollection_ClampsAllocToZero()
        {
            var g = new GcSample();
            g.Update(0, 1000);
            var f = g.Update(0, 900);
            Assert.False(f.GcFired);
            Assert.Equal(0L, f.AllocEstBytes);
        }
    }
}
