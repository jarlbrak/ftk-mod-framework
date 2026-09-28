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
    internal sealed class ReportingSubmissionResult { internal bool Success; internal string Error; }
    // Mirrors ReportingSubmission's queue rules: one retained automatic payload that a different
    // payload cannot overwrite, and deletion of an automatic payload on a permanent failure.
    internal static class ReportingSubmission
    {
        internal static bool Ready = true, Busy;
        internal static string PendingPayload, PendingAutomaticPayload, LastPayload;
        internal static int Discards;
        private static Action<ReportingSubmissionResult> completion;
        internal static bool Start(string json, Action<ReportingSubmissionResult> done)
        {
            if (Busy || !Ready) return false;
            bool automatic = (string)JObject.Parse(json)["submissionMode"] == "automatic";
            if (automatic && PendingAutomaticPayload != null && PendingAutomaticPayload != json)
                throw new Exception("automatic payload overwritten without a discard");
            Busy = true; LastPayload = json;
            if (automatic) PendingAutomaticPayload = json;
            else PendingPayload = json;
            completion = done; return true;
        }
        internal static void Complete(bool success) { Complete(success ? null : "service_unavailable"); }
        internal static void Complete(string error)
        {
            Busy = false;
            if (error == null || (PendingAutomaticPayload == LastPayload && ReportingSubmissionPayload.PermanentFailure(error)))
            {
                if (PendingAutomaticPayload == LastPayload) PendingAutomaticPayload = null;
                else PendingPayload = null;
            }
            Action<ReportingSubmissionResult> done = completion; completion = null;
            done(new ReportingSubmissionResult { Success = error == null, Error = error });
        }
        internal static void DiscardAutomatic(string expected, Action<bool> done)
        {
            Discards++;
            bool match = PendingAutomaticPayload == expected;
            if (match) PendingAutomaticPayload = null;
            done(match);
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
        ReportingSubmission.PendingPayload = null;

        // A payload that failed this launch never blocks a crash report or a newer error.
        NewLaunch();
        JObject staleRequest = JObject.Parse(ReportingSubmissionPayload.Create(new ReportingReport(), "Old error", "error", "old logs", "", true));
        string oldFingerprint = ReportingAutomatic.Digest("Error at Old.Thing");
        staleRequest["fingerprint"] = oldFingerprint;
        string stale = staleRequest.ToString(Newtonsoft.Json.Formatting.None);
        ReportingSubmission.PendingAutomaticPayload = stale;
        ReportingRuntime.Pending = new ReportingIncident { SessionId = new string('b', 32) };
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.LastPayload == stale, "retained payload not retried once with its original bytes");
        ReportingSubmission.Complete("service_unavailable");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.Discards == 1 && ReportingSubmission.PendingAutomaticPayload == null &&
            !ReportingAutomatic.RecentlyReported(oldFingerprint, DateTime.UtcNow), "failed payload blocked newer reports or settled an undelivered error");
        ReportingAutomatic.Tick(true);
        JObject crash = JObject.Parse(ReportingSubmission.LastPayload);
        Check((string)crash["kind"] == "unexpected_exit" && ReportingDiagnostics.PendingError != null, "crash report did not take priority over the error");
        ReportingSubmission.Complete("transport_error");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.Discards == 2 && ReportingRuntime.Pending == null, "uncertain crash report left its incident to be resent under a new ID");
        ReportingAutomatic.Tick(true);
        JObject third = JObject.Parse(ReportingSubmission.LastPayload);
        string thirdFingerprint = ReportingAutomatic.Digest("Error at AnotherMod.Tick");
        Check((string)third["kind"] == "error" && (string)third["fingerprint"] == thirdFingerprint &&
            ReportingDiagnostics.PendingError == null, "newer error blocked by a failed crash report");

        // A permanent refusal drops the payload and settles its subject.
        ReportingSubmission.Complete("invalid_report");
        Check(ReportingSubmission.PendingAutomaticPayload == null && ReportingAutomatic.RecentlyReported(thirdFingerprint, DateTime.UtcNow),
            "permanently refused error kept or will be resent every launch");
        ReportingRuntime.Pending = new ReportingIncident { SessionId = new string('e', 32) };
        ReportingAutomatic.Tick(true);
        ReportingSubmission.Complete("payload_too_large");
        Check(ReportingSubmission.PendingAutomaticPayload == null && ReportingRuntime.Pending == null, "permanently refused crash report kept its incident");

        // A crash report that certainly was not delivered is dropped for a newer error, but its
        // incident stays pending and is reported again next launch.
        NewLaunch();
        string sessionC = new string('c', 32);
        ReportingRuntime.Pending = new ReportingIncident { SessionId = sessionC };
        ReportingAutomatic.Tick(true);
        string firstCrash = ReportingSubmission.LastPayload;
        ReportingSubmission.Complete("rate_limited");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.LastPayload == firstCrash && ReportingSubmission.Discards == 2 &&
            ReportingSubmission.PendingAutomaticPayload == firstCrash, "failed payload retried or dropped without a newer report");
        ReportingDiagnostics.PendingError = new ReportingDiagnosticsError("fourth", "Fourth error", "Error at Fourth.Mod");
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.Discards == 3 && ReportingRuntime.Pending != null && ReportingRuntime.Pending.SessionId == sessionC,
            "undelivered crash report was not replaced or lost its incident");
        ReportingAutomatic.Tick(true);
        Check((string)JObject.Parse(ReportingSubmission.LastPayload)["kind"] == "error", "newer error not sent");
        ReportingSubmission.Complete(true);
        NewLaunch();
        ReportingAutomatic.Tick(true);
        JObject retried = JObject.Parse(ReportingSubmission.LastPayload);
        Check((string)retried["kind"] == "unexpected_exit" && (string)retried["diagnostics"]["previousSession"]["sessionId"] == sessionC,
            "pending incident not reported on the next launch");

        // A retained crash report is retried with its own bytes, never rebuilt under a new ID.
        string retainedCrash = ReportingSubmission.LastPayload;
        ReportingSubmission.Complete("service_unavailable");
        NewLaunch();
        ReportingAutomatic.Tick(true);
        Check(ReportingSubmission.LastPayload == retainedCrash, "retained crash report rebuilt instead of retried");
        ReportingSubmission.Complete(true);
        Check(ReportingRuntime.Pending == null && ReportingSubmission.PendingAutomaticPayload == null, "delivered retry did not settle its incident");
        Console.WriteLine("PASS automatic setting, payload, deduplication, restart disposition, manual priority, " +
            "best-effort replacement, crash priority and permanent failures");
    }
    // Automatic send state is per launch. Reset it as a new process would.
    private static void NewLaunch()
    {
        foreach (string name in new[] { "attemptedPayload", "attemptedErrorId", "attemptedSessionId", "failure" })
            typeof(ReportingAutomatic).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, null);
        typeof(ReportingAutomatic).GetField("nextAttemptUtc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, DateTime.MinValue);
    }
}
