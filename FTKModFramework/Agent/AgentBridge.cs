using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityEngine;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Env-gated, single-player-only test harness bridge.
    ///
    /// When (and ONLY when) the environment variable FTK_AGENT_BRIDGE == "1", this starts a loopback
    /// HttpListener on a background thread and creates a hidden <see cref="BridgeHost"/> MonoBehaviour to
    /// pump game calls onto Unity's main thread. With the env var unset, Start() is a no-op: no thread, no
    /// listener, no GameObject, byte-identical shipped behavior. The bridge is for SINGLE-PLAYER content
    /// testing only; every /action additionally checks GameLogic.IsSinglePlayer() so co-op Photon state is
    /// never perturbed even if the env var is set during a co-op session.
    ///
    /// Threading: one background thread blocks in HttpListener.GetContext(); per request the handler builds
    /// a Func&lt;object&gt;, enqueues it under <see cref="Gate"/> into <see cref="Queue"/>, and blocks on a
    /// per-request ManualResetEvent. BridgeHost.Update() drains the queue on the main thread. net35: no
    /// Task/async/ConcurrentQueue, only lock + Queue + ManualResetEvent.
    /// </summary>
    internal static class AgentBridge
    {
        internal const string EnvFlag = "FTK_AGENT_BRIDGE";
        internal const string EnvPort = "FTK_AGENT_BRIDGE_PORT";
        internal const int DefaultPort = 8777;

        // Shared with BridgeHost: the main-thread work queue and its lock.
        internal static readonly object Gate = new object();
        internal static readonly Queue<Action> Queue = new Queue<Action>();

        private static bool _started;
        private static volatile bool _running;
        private static HttpListener _listener;
        private static Thread _thread;
        private static int _port;

        /// <summary>Idempotent. No-ops unless FTK_AGENT_BRIDGE==1. Safe to call once from Plugin.Awake().</summary>
        public static void Start()
        {
            if (_started) return; // _done guard: never start twice.
            _started = true;

            string flag = Environment.GetEnvironmentVariable(EnvFlag);
            if (flag != "1")
                return; // Off: nothing created, shipped behavior unchanged.

            _port = ResolvePort();

            try
            {
                // Hidden main-thread pump. Must exist before requests arrive so RunOnMainThread can drain.
                GameObject go = new GameObject("FTKAgentBridgeHost");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<BridgeHost>();

                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
                _listener.Start();
                _running = true;

                _thread = new Thread(ListenLoop);
                _thread.IsBackground = true;
                _thread.Name = "FTKAgentBridge";
                _thread.Start();

                Plugin.Log.LogInfo("FTKAgentBridge listening on http://127.0.0.1:" + _port + "/");
                Plugin.Log.LogWarning("FTKAgentBridge is SINGLE-PLAYER TEST USE ONLY. Do not enable in co-op.");
            }
            catch (Exception e)
            {
                _running = false;
                Plugin.Log.LogError("FTKAgentBridge failed to start: " + e);
            }
        }

        /// <summary>Stop the listener and background thread. Idempotent.</summary>
        public static void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            try { if (_listener != null) _listener.Close(); } catch { }
            _listener = null;
        }

        private static int ResolvePort()
        {
            string raw = Environment.GetEnvironmentVariable(EnvPort);
            int p;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out p) && p > 0 && p < 65536)
                return p;
            return DefaultPort;
        }

        internal static int Port { get { return _port; } }

        private static void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext(); // blocks until a request or the listener is stopped
                }
                catch (Exception)
                {
                    // Listener stopped/disposed: exit the loop cleanly.
                    break;
                }

                try
                {
                    HttpRouter.Dispatch(ctx);
                }
                catch (Exception e)
                {
                    try
                    {
                        Plugin.Log.LogError("[agent] request dispatch threw: " + e);
                        ctx.Response.StatusCode = 500;
                        ctx.Response.Close();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Marshal <paramref name="work"/> onto the Unity main thread and block (up to <paramref name="timeoutMs"/>)
        /// for its result. Returns the work's return value, or rethrows the captured exception, or throws
        /// TimeoutException if the main thread did not drain the queue in time. Called from the HTTP thread.
        /// </summary>
        public static object RunOnMainThread(Func<object> work, int timeoutMs)
        {
            ManualResetEvent done = new ManualResetEvent(false);
            object[] resultSlot = new object[1];
            Exception[] errorSlot = new Exception[1];

            Action wrapped = delegate
            {
                try { resultSlot[0] = work(); }
                catch (Exception e) { errorSlot[0] = e; }
                finally { done.Set(); }
            };

            lock (Gate)
            {
                Queue.Enqueue(wrapped);
            }

            if (!done.WaitOne(timeoutMs))
                throw new TimeoutException("main-thread work did not complete within " + timeoutMs + "ms");

            if (errorSlot[0] != null)
                throw errorSlot[0];

            return resultSlot[0];
        }
    }
}
