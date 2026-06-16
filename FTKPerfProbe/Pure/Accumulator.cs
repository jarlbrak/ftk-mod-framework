namespace FTKPerfProbe
{
    /// <summary>
    /// Per-bucket inclusive wall-time accumulator with a re-entrancy depth guard.
    /// Counts every Enter; accumulates elapsed ticks only for the OUTERMOST span,
    /// so nested self-calls are not double-counted. Pure: the clock is passed in.
    /// </summary>
    public sealed class Accumulator
    {
        private int _depth;
        private long _start;

        public int Calls { get; private set; }
        public long Ticks { get; private set; }

        public void Enter(long now)
        {
            Calls++;
            if (_depth++ == 0) _start = now;
        }

        public void Exit(long now)
        {
            if (_depth <= 0) return;
            if (--_depth == 0) Ticks += now - _start;
        }

        public void SnapshotAndReset(out int calls, out long ticks)
        {
            calls = Calls;
            ticks = Ticks;
            Calls = 0;
            Ticks = 0;
        }
    }
}
