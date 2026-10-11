using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using FTKModFramework.Agent;
using FTKModFramework;
using Listener = System.Net.HttpListener;

internal static class Program
{
    private static readonly string[] Cases = { "disabled", "blocked-constructor", "blocked-start", "construct-failure", "start-failure", "ready", "cancel-pending", "cancel-after-release" };
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Wait(WaitHandle gate, string message) { Require(gate.WaitOne(5000), message); }
    private static void NoWork()
    {
        try { AgentBridge.RunOnMainThread(delegate { return null; }, 100); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Bridge incorrectly accepted work");
    }
    private static void JoinWorker()
    {
        Thread worker = (Thread)typeof(AgentBridge).GetField("_thread", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Require(worker == null || worker.Join(5000), "Listener worker remained live");
    }
    private static void WaitQueued()
    {
        Require(SpinWait.SpinUntil(delegate { lock (AgentBridge.Gate) { return AgentBridge.Queue.Count == 1; } }, 5000), "Work was not queued");
    }
    private static void Ready()
    {
        AgentBridge.Start();
        Wait(Plugin.Log.Ready, "Listener not ready");
        Wait(Listener.ContextEntered, "Listener not serving");
        Require(Listener.WorkerThread != UnityEngine.GameObject.MainThread, "Listener used main thread");
        Require(Listener.Instance.Prefixes.Count == 1 && Listener.Instance.Prefixes[0] == "http://127.0.0.1:17329/", "Loopback/port changed");
    }
    private static void RunCase(string name)
    {
        UnityEngine.GameObject.MainThread = Thread.CurrentThread.ManagedThreadId;
        Environment.SetEnvironmentVariable(AgentBridge.EnvFlag, name == "disabled" ? null : "1");
        Environment.SetEnvironmentVariable(AgentBridge.EnvPort, "17329");
        if (name == "disabled")
        {
            AgentBridge.Start();
            Environment.SetEnvironmentVariable(AgentBridge.EnvFlag, "1");
            AgentBridge.Start();
            Require(UnityEngine.GameObject.Created == 0 && Listener.Created == 0, "Disabled bridge created resources or restarted");
            NoWork(); AgentBridge.Stop(); return;
        }
        if (name == "blocked-constructor" || name == "blocked-start")
        {
            bool constructor = name == "blocked-constructor";
            if (constructor) Listener.ConstructRelease.Reset(); else Listener.StartRelease.Reset();
            AgentBridge.Start(); // Must return while the worker is held behind its test gate.
            Wait(constructor ? Listener.ConstructEntered : Listener.StartEntered, "Startup worker did not reach blocked gate");
            Require(UnityEngine.GameObject.Created == 1, "Main-thread host missing");
            AgentBridge.Start();
            Require(UnityEngine.GameObject.Created == 1 && Listener.Created == 1, "Duplicate startup resources");
            NoWork();
            ManualResetEvent stopped = new ManualResetEvent(false);
            Thread stop = new Thread(delegate() { AgentBridge.Stop(); stopped.Set(); }); stop.Start();
            Wait(stopped, "Stop blocked behind initialization");
            Require(Listener.ClosedCount == 0 && !Plugin.Log.Ready.WaitOne(0), "Starting listener leaked or was closed by Stop");
            if (constructor) Listener.ConstructRelease.Set(); else Listener.StartRelease.Set();
            Wait(Listener.Closed, "Cancelled worker did not close its own listener"); JoinWorker(); stop.Join();
            Require(Listener.ClosedCount == 1 && !Plugin.Log.Ready.WaitOne(0), "Cancelled startup published late");
            NoWork(); return;
        }
        if (name == "construct-failure" || name == "start-failure")
        {
            Listener.FailConstruct = name == "construct-failure";
            Listener.FailStart = name == "start-failure";
            AgentBridge.Start(); Wait(Plugin.Log.Failed, "Startup failure not reported"); JoinWorker();
            Require(!Plugin.Log.Ready.WaitOne(0), "Failed startup became ready");
            Require(Listener.ClosedCount == (Listener.FailConstruct ? 0 : 1), "Owned failed listener cleanup changed");
            NoWork(); AgentBridge.Start(); Require(Listener.Created == 1, "Failed startup retried"); return;
        }
        if (name == "cancel-after-release")
        {
            Listener.StartRelease.Reset(); AgentBridge.Start(); Wait(Listener.StartEntered, "No blocked startup");
            // Both possible orderings must finish stopped; the cancellation flag never resets.
            Listener.StartRelease.Set(); AgentBridge.Stop(); JoinWorker();
            NoWork(); Require(Listener.ClosedCount >= 1, "Released listener not cleaned up"); return;
        }
        Ready();
        if (name == "ready")
        {
            AgentBridge.Start(); Require(UnityEngine.GameObject.Created == 1 && Listener.Created == 1, "Ready startup duplicated");
            Listener.Instance.Enqueue(new System.Net.HttpListenerContext()); Wait(HttpRouter.Dispatched, "Request was not dispatched");
            object value = null; Exception error = null; int executions = 0;
            Thread caller = new Thread(delegate() { try { value = AgentBridge.RunOnMainThread(delegate { Interlocked.Increment(ref executions); return 42; }, 5000); } catch (Exception e) { error = e; } });
            caller.Start(); WaitQueued(); Action work; lock (AgentBridge.Gate) { work = AgentBridge.Queue.Dequeue(); }
            work(); work(); Require(caller.Join(5000), "Completed work did not return");
            Require(error == null && (int)value == 42 && executions == 1, "Main-thread work did not execute exactly once");
            AgentBridge.Stop(); AgentBridge.Stop(); JoinWorker(); NoWork(); return;
        }
        Require(name == "cancel-pending", "Unknown scenario");
        Exception cancelled = null; int effects = 0;
        Thread pending = new Thread(delegate() { try { AgentBridge.RunOnMainThread(delegate { effects++; return null; }, 5000); } catch (Exception e) { cancelled = e; } });
        pending.Start(); WaitQueued(); Action stale; lock (AgentBridge.Gate) { stale = AgentBridge.Queue.Peek(); }
        AgentBridge.Stop(); Require(pending.Join(5000), "Pending work did not cancel"); stale(); JoinWorker();
        Require(cancelled is InvalidOperationException && effects == 0 && AgentBridge.Queue.Count == 0, "Stop permitted pending mutation"); NoWork();
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1) { RunCase(args[0]); Console.WriteLine("PASS " + args[0]); return 0; }
            foreach (string name in Cases)
            {
                ProcessStartInfo start = new ProcessStartInfo("dotnet");
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); start.ArgumentList.Add(name);
                start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
                using (Process process = Process.Start(start))
                {
                    if (!process.WaitForExit(15000)) { process.Kill(true); throw new Exception("Scenario deadline exceeded: " + name); }
                    string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
                    Require(process.ExitCode == 0, name + ": " + error + output); Console.Write(output);
                }
            }
            Console.WriteLine("PASS 8 actual-source bridge startup scenarios"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
