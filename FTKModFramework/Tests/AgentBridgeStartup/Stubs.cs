using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public class GameObject
    {
        public static int MainThread;
        public static int Created;
        public HideFlags hideFlags;
        public GameObject(string name)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread) throw new Exception("Unity called off main thread");
            Interlocked.Increment(ref Created);
        }
        public T AddComponent<T>() where T : new() { return new T(); }
    }
}
namespace FTKModFramework
{
    internal static class Plugin { internal static readonly TestLog Log = new TestLog(); }
    internal class TestLog
    {
        internal readonly ManualResetEvent Ready = new ManualResetEvent(false);
        internal readonly ManualResetEvent Failed = new ManualResetEvent(false);
        internal void LogInfo(object message) { Ready.Set(); }
        internal void LogWarning(object message) { }
        internal void LogError(object message) { Failed.Set(); }
    }
}
namespace FTKModFramework.Agent
{
    internal class BridgeHost { }
    internal static class HttpRouter
    {
        internal static readonly ManualResetEvent Dispatched = new ManualResetEvent(false);
        internal static int Count;
        internal static void Dispatch(System.Net.HttpListenerContext context)
        {
            Interlocked.Increment(ref Count);
            Dispatched.Set();
        }
    }
}
namespace System.Net
{
    // Test-only type binding compiles the actual bridge against controllable listener behavior.
    public class HttpListener
    {
        public static readonly ManualResetEvent ConstructEntered = new ManualResetEvent(false);
        public static readonly ManualResetEvent ConstructRelease = new ManualResetEvent(true);
        public static readonly ManualResetEvent StartEntered = new ManualResetEvent(false);
        public static readonly ManualResetEvent StartRelease = new ManualResetEvent(true);
        public static readonly ManualResetEvent ContextEntered = new ManualResetEvent(false);
        public static readonly ManualResetEvent Closed = new ManualResetEvent(false);
        public static bool FailConstruct;
        public static bool FailStart;
        public static int Created;
        public static int ClosedCount;
        public static int WorkerThread;
        public static HttpListener Instance;
        public readonly List<string> Prefixes = new List<string>();
        private readonly Queue<HttpListenerContext> _requests = new Queue<HttpListenerContext>();
        private bool _stopped;
        public HttpListener()
        {
            WorkerThread = Thread.CurrentThread.ManagedThreadId;
            if (WorkerThread == UnityEngine.GameObject.MainThread) throw new Exception("Listener constructed on Unity main thread");
            Interlocked.Increment(ref Created);
            ConstructEntered.Set();
            if (!ConstructRelease.WaitOne(10000)) throw new Exception("Constructor test gate expired");
            if (FailConstruct) throw new Exception("Controlled constructor failure");
            Instance = this;
        }
        public void Start()
        {
            StartEntered.Set();
            if (!StartRelease.WaitOne(10000)) throw new Exception("Startup test gate expired");
            if (FailStart) throw new Exception("Controlled startup failure");
        }
        public HttpListenerContext GetContext()
        {
            lock (_requests)
            {
                ContextEntered.Set();
                while (!_stopped && _requests.Count == 0) Monitor.Wait(_requests);
                if (_stopped) throw new Exception("Stopped");
                return _requests.Dequeue();
            }
        }
        public void Enqueue(HttpListenerContext context)
        {
            lock (_requests) { _requests.Enqueue(context); Monitor.PulseAll(_requests); }
        }
        public void Stop()
        {
            lock (_requests) { _stopped = true; Monitor.PulseAll(_requests); }
        }
        public void Close()
        {
            Interlocked.Increment(ref ClosedCount);
            Stop();
            Closed.Set();
        }
    }
    public class HttpListenerContext { public readonly TestResponse Response = new TestResponse(); }
    public class TestResponse { public int StatusCode; public void Close() { } }
}
