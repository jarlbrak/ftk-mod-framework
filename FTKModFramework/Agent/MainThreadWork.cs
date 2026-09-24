using System;
using System.Diagnostics;
using System.Threading;

namespace FTKModFramework.Agent
{
    // Cancellation owns only pending work. Once execution starts, a timeout cannot undo its effects.
    // Monitor keeps notification lifetime tied to this object, including completion after a timeout.
    internal sealed class MainThreadWork
    {
        private enum State { Pending, Running, Complete, Cancelled }
        private readonly object _gate = new object();
        private readonly Func<object> _work;
        private State _state;
        private object _result;
        private Exception _error;

        internal MainThreadWork(Func<object> work)
        {
            if (work == null) throw new ArgumentNullException("work");
            _work = work;
        }

        internal void Execute()
        {
            lock (_gate)
            {
                if (_state != State.Pending) return;
                _state = State.Running;
            }

            object result = null;
            Exception error = null;
            try { result = _work(); }
            catch (Exception e) { error = e; }
            lock (_gate)
            {
                _result = result;
                _error = error;
                _state = State.Complete;
                Monitor.PulseAll(_gate);
            }
        }

        internal void CancelPending()
        {
            lock (_gate)
            {
                if (_state != State.Pending) return;
                _state = State.Cancelled;
                Monitor.PulseAll(_gate);
            }
        }

        internal object Wait(int timeoutMs)
        {
            if (timeoutMs < 0) throw new ArgumentOutOfRangeException("timeoutMs");
            Stopwatch elapsed = Stopwatch.StartNew();
            lock (_gate)
            {
                while (_state == State.Pending || _state == State.Running)
                {
                    long remaining = timeoutMs - elapsed.ElapsedMilliseconds;
                    if (remaining <= 0)
                    {
                        if (_state == State.Pending)
                        {
                            _state = State.Cancelled;
                            throw new TimeoutException("main-thread work timed out before execution; cancelled without executing");
                        }
                        throw new TimeoutException("main-thread work timed out after execution started; outcome unknown, do not retry automatically");
                    }
                    Monitor.Wait(_gate, (int)remaining);
                }

                if (_state == State.Cancelled)
                    throw new InvalidOperationException("main-thread work cancelled before execution because the bridge stopped");
                if (_error != null) throw _error;
                return _result;
            }
        }
    }
}
