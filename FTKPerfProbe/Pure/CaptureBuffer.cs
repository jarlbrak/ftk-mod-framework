using System;

namespace FTKPerfProbe
{
    public struct CaptureSummary
    {
        public double MeanMs, P50Ms, P95Ms, P99Ms, MaxMs;
        public int GcFrames, GcCollections;
    }

    /// <summary>Bounded append-only storage. Recording never allocates or discards older frames.</summary>
    public sealed class CaptureBuffer
    {
        private readonly CaptureRow[] _rows;
        public int Count { get; private set; }
        public int Capacity { get { return _rows.Length; } }

        public CaptureBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _rows = new CaptureRow[capacity];
        }

        public CaptureRow this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index");
                return _rows[index];
            }
        }

        public bool TryAdd(CaptureRow row)
        {
            if (Count == _rows.Length) return false;
            _rows[Count++] = row;
            return true;
        }

        // Sorting and allocation belong after sampling has ended.
        public CaptureSummary Summarize()
        {
            CaptureSummary summary = new CaptureSummary();
            if (Count == 0) return summary;
            double[] sorted = new double[Count];
            double sum = 0;
            for (int i = 0; i < Count; i++)
            {
                CaptureRow row = _rows[i];
                sorted[i] = row.UnscaledDtMs;
                sum += row.UnscaledDtMs;
                if (row.GcFired) summary.GcFrames++;
                summary.GcCollections += row.Gc0Delta;
            }
            Array.Sort(sorted);
            summary.MeanMs = sum / Count;
            summary.P50Ms = sorted[(int)Math.Ceiling(0.50 * Count) - 1];
            summary.P95Ms = sorted[(int)Math.Ceiling(0.95 * Count) - 1];
            summary.P99Ms = sorted[(int)Math.Ceiling(0.99 * Count) - 1];
            summary.MaxMs = sorted[Count - 1];
            return summary;
        }
    }
}
