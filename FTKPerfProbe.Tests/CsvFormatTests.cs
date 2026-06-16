using Xunit;

namespace FTKPerfProbe.Tests
{
    public class CsvFormatTests
    {
        [Fact]
        public void Header_HasExpectedColumns()
        {
            Assert.Equal(
                "frame,unscaled_dt_ms,fps,gc0_delta,gc_fired,heap_bytes,alloc_est_bytes," +
                "id_calls,id_ms,ow_ms,pm_calls,pm_ms,canvas_calls,canvas_ms,photon_calls,photon_ms",
                CsvFormat.Header);
        }

        [Fact]
        public void Row_UsesInvariantDecimals_AndBoolAsOneZero()
        {
            var r = new CaptureRow
            {
                Frame = 7, UnscaledDtMs = 16.6667, Fps = 60.0, Gc0Delta = 1, GcFired = true,
                HeapBytes = 12345678, AllocEstBytes = 2048,
                IdCalls = 12, IdMs = 0.5, OwMs = 1.25, PmCalls = 30, PmMs = 2.5,
                CanvasCalls = 3, CanvasMs = 0.75,
                PhotonCalls = 8, PhotonMs = 1.5
            };
            Assert.Equal(
                "7,16.667,60.0,1,1,12345678,2048,12,0.500,1.250,30,2.500,3,0.750,8,1.500",
                CsvFormat.Row(r));
        }
    }
}
