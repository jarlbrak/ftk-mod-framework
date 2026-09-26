using System;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core.Reporting;

namespace FTKModFramework
{
    internal sealed class TestEntry<T> { internal T Value; internal TestEntry(T value) { Value = value; } }
    internal sealed class TestConfig { internal int Saves; internal void Save() { Saves++; } }
    internal sealed class Plugin
    {
        internal static readonly Plugin Instance = new Plugin();
        internal readonly TestConfig Config = new TestConfig();
        internal static readonly TestEntry<string> AutomaticBugReportHistory = new TestEntry<string>("");
    }
}
namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingIncident { internal string SessionId; }
    internal sealed class ReportingDiagnosticsError
    {
        internal string Id, Summary, Signature;
        internal ReportingDiagnosticsError(string id, string summary, string signature)
        { Id = id; Summary = summary; Signature = signature; }
    }
    internal static class ReportingDiagnostics
    {
        internal static ReportingDiagnosticsError PendingError;
        internal static string CaptureCurrent() { return "logged error"; }
        internal static string CapturePrevious(string id) { return "previous logs"; }
        internal static void Acknowledge(string id)
        { if (PendingError != null && PendingError.Id == id) PendingError = null; }
    }
    internal sealed class ReportingReport
    {
        internal string ReportId = Guid.NewGuid().ToString("N"), CaptureId = Guid.NewGuid().ToString("N");
        internal string CurrentMetadata = "{\"status\":\"complete\"}", PreviousMetadata = "{\"status\":\"complete\"}";
        internal string PreviousSessionId;
        internal bool IncludeMetadata = true;
    }
    internal static class ReportingDraft
    { internal static bool ValidId(string id) { return id != null && id.Length == 32; } }
    internal static class ReportingRuntime
    {
        internal static ReportingIncident Pending;
        internal static ReportingReport CreateReport(bool previous) { return new ReportingReport(); }
        internal static ReportingReport CreateReport(ReportingIncident previous)
        { return new ReportingReport { PreviousSessionId = previous.SessionId }; }
        internal static void DismissPending(Action<bool> completed) { Pending = null; }
    }
    internal sealed class ReportingSubmissionResult { internal bool Success; }
    internal static class ReportingSubmission
    {
        internal static bool Ready = true, Busy;
        internal static string PendingPayload, PendingAutomaticPayload, LastPayload;
        private static Action<ReportingSubmissionResult> completion;
        internal static bool Start(string json, Action<ReportingSubmissionResult> done)
        {
            if (Busy || !Ready) return false;
            Busy = true; LastPayload = json;
            if ((string)JObject.Parse(json)["submissionMode"] == "automatic") PendingAutomaticPayload = json;
            else PendingPayload = json;
            completion = done; return true;
        }
        internal static void Complete(bool success)
        {
            Busy = false; if (success) {
                if (PendingAutomaticPayload == LastPayload) PendingAutomaticPayload = null;
                else PendingPayload = null;
            }
            Action<ReportingSubmissionResult> done = completion; completion = null;
            done(new ReportingSubmissionResult { Success = success });
        }
    }
}
internal static class Program
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Main()
    {
        ReportingDiagnostics.PendingError = new ReportingDiagnosticsError("first", "Detected error", "Error at Mod.DoThing");
        ReportingAutomatic.Tick(false);
        Check(ReportingSubmission.LastPayload == null && ReportingDiagnostics.PendingError != null, "disabled reporting sent or lost an error");
        ReportingAutomatic.Tick(true);
        JObject first = JObject.Parse(ReportingSubmission.LastPayload);
        Check((string)first["submissionMode"] == "automatic" && (string)first["kind"] == "error" &&
            (string)first["fingerprint"] == ReportingAutomatic.Digest("Error at Mod.DoThing") &&
            (bool)first["includeDiagnostics"] && ReportingDiagnostics.PendingError == null, "automatic error payload or acknowledgement");
        ReportingSubmission.Complete(true);
        Check(ReportingAutomatic.RecentlyReported((string)first["fingerprint"], DateTime.UtcNow) &&
            FTKModFramework.Plugin.Instance.Config.Saves == 1, "successful signature not persisted");
        Check(!ReportingAutomatic.RecentlyReported((string)first["fingerprint"], DateTime.UtcNow.AddDays(31)),
            "expired signature blocked a new report");
        string delivered = ReportingSubmission.LastPayload;
        ReportingDiagnostics.PendingError = new ReportingDiagnosticsError("second", "Detected error", "Error at Mod.DoThing");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.LastPayload == delivered && ReportingDiagnostics.PendingError == null, "repeat error sent again");

        ReportingRuntime.Pending = new ReportingIncident { SessionId = new string('a', 32) };
        ReportingAutomatic.Tick(true);
        JObject restart = JObject.Parse(ReportingSubmission.LastPayload);
        Check((string)restart["kind"] == "unexpected_exit" &&
            (string)restart["diagnostics"]["previousSession"]["sessionId"] == new string('a', 32), "previous session provenance missing");
        ReportingSubmission.Complete(true);
        Check(ReportingRuntime.Pending == null, "confirmed previous session not acknowledged");

        ReportingSubmission.PendingPayload = "{\"submissionMode\":\"manual\"}";
        delivered = ReportingSubmission.LastPayload;
        ReportingDiagnostics.PendingError = new ReportingDiagnosticsError("third", "Another error", "Error at AnotherMod.Tick");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.LastPayload == delivered && ReportingDiagnostics.PendingError != null, "manual pending report displaced");
        Console.WriteLine("PASS automatic setting, payload, deduplication, restart disposition and manual priority");
    }
}
