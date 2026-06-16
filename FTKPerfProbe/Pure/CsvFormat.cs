using System.Globalization;
using System.Text;

namespace FTKPerfProbe
{
    /// <summary>One frame's row of capture data.</summary>
    public struct CaptureRow
    {
        public int Frame;
        public double UnscaledDtMs;
        public double Fps;
        public int Gc0Delta;
        public bool GcFired;
        public long HeapBytes;
        public long AllocEstBytes;
        public int IdCalls; public double IdMs;
        public double OwMs;
        public int PmCalls; public double PmMs;
        public int CanvasCalls; public double CanvasMs;
    }

    /// <summary>Invariant-culture CSV header + row formatting (locale-safe across Mac/Windows/Linux).</summary>
    public static class CsvFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string Header =
            "frame,unscaled_dt_ms,fps,gc0_delta,gc_fired,heap_bytes,alloc_est_bytes," +
            "id_calls,id_ms,ow_ms,pm_calls,pm_ms,canvas_calls,canvas_ms";

        public static string Row(CaptureRow r)
        {
            StringBuilder sb = new StringBuilder(160);
            sb.Append(r.Frame.ToString(Inv)).Append(',');
            sb.Append(r.UnscaledDtMs.ToString("F3", Inv)).Append(',');
            sb.Append(r.Fps.ToString("F1", Inv)).Append(',');
            sb.Append(r.Gc0Delta.ToString(Inv)).Append(',');
            sb.Append(r.GcFired ? "1" : "0").Append(',');
            sb.Append(r.HeapBytes.ToString(Inv)).Append(',');
            sb.Append(r.AllocEstBytes.ToString(Inv)).Append(',');
            sb.Append(r.IdCalls.ToString(Inv)).Append(',');
            sb.Append(r.IdMs.ToString("F3", Inv)).Append(',');
            sb.Append(r.OwMs.ToString("F3", Inv)).Append(',');
            sb.Append(r.PmCalls.ToString(Inv)).Append(',');
            sb.Append(r.PmMs.ToString("F3", Inv)).Append(',');
            sb.Append(r.CanvasCalls.ToString(Inv)).Append(',');
            sb.Append(r.CanvasMs.ToString("F3", Inv));
            return sb.ToString();
        }
    }
}
