using System;

namespace FTKPerfProbe
{
    /// <summary>
    /// Fixed-capacity ring buffer of frame durations (ms). Computes mean, max, mean-FPS, and the
    /// "1% low" = the 99th-percentile DURATION (worst-1% threshold; higher is worse). Pure, no Unity.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly double[] _buf;
        private int _count;
        private int _next;

        public FrameStats(int capacity)
        {
            if (capacity < 1) capacity = 1;
            _buf = new double[capacity];
        }

        public int Count { get { return _count; } }

        public void Push(double frameMs)
        {
            _buf[_next] = frameMs;
            _next = (_next + 1) % _buf.Length;
            if (_count < _buf.Length) _count++;
        }

        public double MeanMs()
        {
            if (_count == 0) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < _count; i++) sum += _buf[i];
            return sum / _count;
        }

        public double MaxMs()
        {
            double max = 0.0;
            for (int i = 0; i < _count; i++) if (_buf[i] > max) max = _buf[i];
            return max;
        }

        public double OnePercentLowMs()
        {
            if (_count == 0) return 0.0;
            double[] sorted = new double[_count];
            Array.Copy(_buf, sorted, _count);
            Array.Sort(sorted);
            int rank = (int)Math.Ceiling(0.99 * _count) - 1;
            if (rank < 0) rank = 0;
            if (rank >= _count) rank = _count - 1;
            return sorted[rank];
        }

        public double MeanFps()
        {
            double mean = MeanMs();
            return mean > 0.0 ? 1000.0 / mean : 0.0;
        }
    }
}
