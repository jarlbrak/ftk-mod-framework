using System;
using System.Collections;

namespace FTKModFramework.Core
{
    // Own exactly one target-selection iterator. Cancellation never advances its commit tail.
    internal sealed class BlacksmithTargetWait : IEnumerator, IDisposable
    {
        private readonly IEnumerator inner;
        private readonly Action waiting;
        private readonly Action finished;
        private bool disposed;

        internal BlacksmithTargetWait(IEnumerator inner, Action waiting, Action finished)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            this.inner = inner; this.waiting = waiting; this.finished = finished;
        }

        public object Current { get { return inner.Current; } }
        public bool MoveNext()
        {
            if (disposed) return false;
            try
            {
                if (!inner.MoveNext()) { Dispose(); return false; }
                if (waiting != null) waiting();
                return !disposed;
            }
            catch { Dispose(); throw; }
        }
        internal bool Cancel()
        {
            if (disposed) return false;
            Dispose(); return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                IDisposable disposable = inner as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
            finally { if (finished != null) finished(); }
        }
        public void Reset() { throw new NotSupportedException(); }
    }
}
