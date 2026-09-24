using System;
using BepInEx.Logging;
using UnityEngine;

namespace FTKModFramework.Core.Reporting
{
    internal static class ReportingDiagnostics
    {
        private static readonly object Gate = new object();
        private static readonly ReportingDiagnosticsBuffer Buffer = new ReportingDiagnosticsBuffer();
        private static Listener listener;
        private static ReportingDiagnosticsStore store;
        private static long written = -1;
        private static DateTime nextFlush;
        private static bool stopping;
        private static string previous = "", previousId;
        internal static ReportingDiagnosticsError PendingError { get { return Buffer.Pending; } }
        internal static string CaptureCurrent() { return Buffer.Capture(); }
        internal static string CapturePrevious(string expectedSessionId)
        { lock (Gate) return expectedSessionId != null && expectedSessionId == previousId ? previous : ""; }
        internal static void Acknowledge(string id) { Buffer.Acknowledge(id); }

        // Subscribe in Awake, before content registration. Startup errors stay in memory while
        // the existing session worker obtains its lease and binds the exact session identities.
        internal static void Start()
        {
            lock (Gate)
            {
                if (listener != null || stopping) return;
                listener = new Listener();
                try
                {
                    BepInEx.Logging.Logger.Listeners.Add(listener);
                    Application.logMessageReceivedThreaded += UnityLog;
                }
                catch { try { BepInEx.Logging.Logger.Listeners.Remove(listener); } catch { } listener = null; }
            }
        }
        internal static void BindSession(ReportingSessionStore owner, string root)
        {
            if (store != null || owner == null) return;
            try
            {
                store = new ReportingDiagnosticsStore(owner, root);
                lock (Gate) { previous = store.Previous; previousId = store.PreviousSessionId; }
            }
            catch { store = null; }
        }
        internal static void Stop()
        {
            lock (Gate)
            {
                stopping = true;
                try { if (listener != null) BepInEx.Logging.Logger.Listeners.Remove(listener); } catch { }
                listener = null;
                try { Application.logMessageReceivedThreaded -= UnityLog; } catch { }
            }
        }
        // Called only by ReportingRuntime's storage worker, alongside its checkpoints. Sharing
        // that worker prevents file enumeration from racing another writer in the leased root.
        internal static void Flush()
        {
            try
            {
                if (store == null) return;
                bool final; lock (Gate) final = stopping;
                if (!final && DateTime.UtcNow < nextFlush) return;
                long version = Buffer.Version;
                if (version != written) { store.Write(Buffer.Capture()); written = version; }
                nextFlush = DateTime.UtcNow.AddSeconds(2);
            }
            catch { store = null; /* Never log into ourselves or stop the game. */ }
        }
        private static void UnityLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try { Buffer.Add("Unity " + type, message, stack, DateTime.UtcNow); } catch { }
        }
        private sealed class Listener : ILogListener
        {
            public void LogEvent(object sender, LogEventArgs args)
            {
                if (args == null || (args.Level & (LogLevel.Error | LogLevel.Fatal)) == 0) return;
                try
                {
                    // Unity has a direct callback with stack traces. Avoid its forwarded duplicate.
                    string source = args.Source == null ? "BepInEx" : args.Source.SourceName;
                    if (source == "Unity Log") return;
                    string message = args.Data as string;
                    Exception error = args.Data as Exception;
                    if (message == null) message = error != null ? error.ToString() : "[non-text log data omitted]";
                    Buffer.Add(source, message, "", DateTime.UtcNow);
                }
                catch { }
            }
            public void Dispose() { }
        }
    }
}
