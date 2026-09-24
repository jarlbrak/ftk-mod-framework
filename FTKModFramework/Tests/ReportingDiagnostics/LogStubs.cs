using System;
using System.Collections.Generic;

namespace BepInEx.Logging
{
    [Flags] public enum LogLevel { Fatal = 1, Error = 2, Warning = 4, Message = 8, Info = 16, Debug = 32 }
    public interface ILogSource { string SourceName { get; } }
    public interface ILogListener : IDisposable { void LogEvent(object sender, LogEventArgs args); }
    public sealed class LogEventArgs : EventArgs
    {
        public object Data; public LogLevel Level; public ILogSource Source;
        public LogEventArgs(object data, LogLevel level, ILogSource source) { Data = data; Level = level; Source = source; }
    }
    public static class Logger
    {
        public static readonly ICollection<ILogListener> Listeners = new List<ILogListener>();
        public static void Emit(object data, LogLevel level, ILogSource source)
        { foreach (ILogListener listener in new List<ILogListener>(Listeners)) listener.LogEvent(null, new LogEventArgs(data, level, source)); }
    }
}
namespace UnityEngine
{
    public enum LogType { Error, Assert, Warning, Log, Exception }
    public static class Application
    {
        public delegate void LogCallback(string message, string stack, LogType type);
        public static event LogCallback logMessageReceivedThreaded;
        public static void Emit(string message, string stack, LogType type)
        { if (logMessageReceivedThreaded != null) logMessageReceivedThreaded(message, stack, type); }
    }
}
