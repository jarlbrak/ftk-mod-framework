using System;
using System.Threading;
using FTKModFramework.Agent;

internal static class Program
{
    private static int _checks;

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        _checks++;
    }

    private static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T e) { _checks++; return e; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Main()
    {
        int calls = 0;
        var pending = new MainThreadWork(delegate { calls++; return "unexpected"; });
        var timeout = Throws<TimeoutException>(delegate { pending.Wait(0); });
        pending.Execute();
        Check(calls == 0 && timeout.Message.Contains("before execution"), "Pending timeout must prevent later execution");

        var stopped = new MainThreadWork(delegate { calls++; return null; });
        stopped.CancelPending();
        stopped.Execute();
        Throws<InvalidOperationException>(delegate { stopped.Wait(100); });
        Check(calls == 0, "Stopping must prevent pending work");

        var success = new MainThreadWork(delegate { calls++; return "ok"; });
        success.Execute();
        success.Execute();
        success.CancelPending();
        Check((string)success.Wait(0) == "ok" && calls == 1, "Completion and once-only execution");

        var failure = new ApplicationException("work failure");
        var failed = new MainThreadWork(delegate { throw failure; });
        failed.Execute();
        Check(ReferenceEquals(failure, Throws<ApplicationException>(delegate { failed.Wait(100); })), "Preserve captured exception");

        using (var entered = new ManualResetEvent(false))
        using (var release = new ManualResetEvent(false))
        {
            var running = new MainThreadWork(delegate { entered.Set(); release.WaitOne(); return 42; });
            var thread = new Thread(running.Execute);
            thread.Start();
            try
            {
                Check(entered.WaitOne(5000), "Worker starts");
                running.CancelPending();
                timeout = Throws<TimeoutException>(delegate { running.Wait(1); });
                Check(timeout.Message.Contains("outcome unknown"), "Running timeout must distinguish unknown outcome");
            }
            finally { release.Set(); Check(thread.Join(5000), "Late completion remains safe"); }
            Check((int)running.Wait(0) == 42, "Running cancellation does not abort or lose completion");
        }

        // Race pending timeout against execution; either cancellation or one completion is valid.
        for (int i = 0; i < 200; i++)
        {
            int runs = 0;
            var raced = new MainThreadWork(delegate { Interlocked.Increment(ref runs); return 7; });
            var thread = new Thread(raced.Execute);
            thread.Start();
            bool cancelled = false;
            try { Check((int)raced.Wait(0) == 7, "Raced result"); }
            catch (TimeoutException e) { cancelled = e.Message.Contains("before execution"); }
            Check(thread.Join(5000), "Race terminates");
            raced.Execute();
            Check(runs <= 1 && (!cancelled || runs == 0), "Race preserves cancellation and once-only execution");
        }

        using (var waiting = new ManualResetEvent(false))
        {
            var cancelWait = new MainThreadWork(delegate { throw new Exception("must not run"); });
            Exception observed = null;
            var thread = new Thread(delegate()
            {
                waiting.Set();
                try { cancelWait.Wait(5000); }
                catch (Exception e) { observed = e; }
            });
            thread.Start();
            Check(waiting.WaitOne(5000), "Waiter starts");
            cancelWait.CancelPending();
            Check(thread.Join(1000), "Stop promptly wakes pending waiter");
            Check(observed is InvalidOperationException, "Stop returns explicit cancellation");
        }
        Throws<ArgumentOutOfRangeException>(delegate { new MainThreadWork(delegate { return null; }).Wait(-1); });
        Throws<ArgumentNullException>(delegate { new MainThreadWork(null); });
        Console.WriteLine("AgentQueue: " + _checks + " checks passed");
    }
}
