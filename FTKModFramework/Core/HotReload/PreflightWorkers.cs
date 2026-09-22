using System;
using System.Threading;

namespace FTKModFramework.Core.HotReload
{
    // One candidate owns its queue and errors. Completion fences all workers, so a caller
    // observes deterministic job-order failures without racing rollback or another candidate.
    internal sealed class PreflightWorkers
    {
        private readonly Action[] jobs;
        private readonly Exception[] errors;
        private int next = -1;
        private int remaining;
        internal PreflightWorkers(Action[] jobs)
        {
            this.jobs = (Action[])jobs.Clone();
            errors = new Exception[jobs.Length];
            remaining = Math.Min(jobs.Length, Math.Min(4, Math.Max(1, Environment.ProcessorCount)));
            int count = remaining;
            for (int i = 0; i < count; i++)
            {
                bool queued = false;
                try { queued = ThreadPool.QueueUserWorkItem(Work); }
                catch (Exception) { /* Run inline if the runtime cannot schedule a worker. */ }
                if (!queued) Work(null);
            }
        }
        private void Work(object ignored)
        {
            try
            {
                int index;
                while ((index = Interlocked.Increment(ref next)) < jobs.Length)
                    try { jobs[index](); }
                    catch (Exception error) { errors[index] = error; }
            }
            finally { Interlocked.Decrement(ref remaining); }
        }
        internal bool Complete { get { return Interlocked.CompareExchange(ref remaining, 0, 0) == 0; } }
        internal void ThrowIfFailed()
        {
            if (!Complete) throw new InvalidOperationException("Asset workers have not completed.");
            for (int i = 0; i < errors.Length; i++)
                if (errors[i] != null) throw new InvalidOperationException("GLB preflight job " + i + " failed: " + errors[i].Message, errors[i]);
        }
    }
}
