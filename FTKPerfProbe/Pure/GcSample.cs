namespace FTKPerfProbe
{
    /// <summary>One frame's GC-derived numbers.</summary>
    public struct GcFrame
    {
        public bool GcFired;
        public int Gc0Delta;
        public long HeapBytes;
        public long AllocEstBytes;
    }

    /// <summary>
    /// Pure per-frame GC accounting. Feed it raw GC.CollectionCount(0) and GC.GetTotalMemory(false);
    /// it derives the per-frame deltas. No Unity, no GC calls of its own (so it is deterministic in tests).
    /// </summary>
    public sealed class GcSample
    {
        private bool _primed;
        private int _lastCount;
        private long _lastHeap;

        public GcFrame Update(int collectionCount0, long totalMemory)
        {
            GcFrame f = new GcFrame();
            f.Gc0Delta = _primed ? collectionCount0 - _lastCount : 0;
            f.GcFired = f.Gc0Delta > 0;
            f.HeapBytes = totalMemory;
            if (!_primed || f.GcFired)
                f.AllocEstBytes = -1;
            else
                f.AllocEstBytes = totalMemory > _lastHeap ? totalMemory - _lastHeap : 0;
            _primed = true;
            _lastCount = collectionCount0;
            _lastHeap = totalMemory;
            return f;
        }
    }
}
