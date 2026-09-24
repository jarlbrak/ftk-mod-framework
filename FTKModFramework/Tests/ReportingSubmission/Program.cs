using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core.Reporting;

namespace BepInEx { internal static class Paths { internal static string BepInExRootPath; } }
namespace FTKModFramework.Core.Marketplace
{
    internal static class MarketplaceProtocol
    {
        internal static bool RefuseHelper;
        internal static void VerifyHelper(string path)
        { if (RefuseHelper || !File.Exists(path)) throw new IOException("test helper unavailable"); }
    }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--helper") return FakeHelper(args);
        ExclusionAndFreeze(); Bounds(); Responses();
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) Bridge();
        Console.WriteLine("Reporting submission: " + checks + " checks passed.");
        return 0;
    }
    private static ReportingReport Report(bool previous)
    {
        ReportingIncident prior = previous ? new ReportingIncident { SessionId = new string('a', 32),
            CheckpointId = new string('b', 32), ObservedAtUtc = DateTime.UtcNow,
            Metadata = "{\"version\":\"previous-private-marker\"}" } : null;
        return ReportingReport.Create(delegate { return "{\"version\":\"current-private-marker\"}"; }, prior, DateTime.UtcNow);
    }
    private static void ExclusionAndFreeze()
    {
        ReportingReport report = Report(true);
        report.IncludeMetadata = false;
        string excluded = ReportingSubmissionPayload.Create(report, "description", "manual", "CURRENT-LOG-MARKER", "PREVIOUS-LOG-MARKER");
        JObject parsed = JObject.Parse(excluded);
        Check(parsed["diagnostics"] == null && (bool)parsed["includeDiagnostics"] == false, "Diagnostics exclusion did not remove diagnostics");
        Check(!excluded.Contains("private-marker") && !excluded.Contains("LOG-MARKER") && !excluded.Contains(new string('a', 32)), "Excluded diagnostics leaked into payload");
        report.IncludeMetadata = true;
        string frozen = ReportingSubmissionPayload.Create(report, "before edit", "unexpected_exit", "CURRENT-LOG-MARKER", "PREVIOUS-LOG-MARKER");
        JObject included = JObject.Parse(frozen);
        Check((string)included["diagnostics"]["logs"] == "CURRENT-LOG-MARKER", "Current log missing");
        Check((string)included["diagnostics"]["previousSession"]["logs"] == "PREVIOUS-LOG-MARKER", "Prior log missing");
        Check((string)included["diagnostics"]["previousSession"]["sessionId"] == new string('a', 32), "Prior session identity missing");
        report.IncludeMetadata = false;
        string edited = ReportingSubmissionPayload.Create(report, "after edit", "manual", "CHANGED", "CHANGED");
        Check((bool)JObject.Parse(frozen)["includeDiagnostics"] && frozen.Contains("before edit") && !frozen.Contains("CHANGED"), "Frozen payload changed after edit");
        Check(!edited.Contains("CURRENT-LOG-MARKER") && !edited.Contains("PREVIOUS-LOG-MARKER"), "Editing produced stale diagnostics inclusion");
        string currentOnly = ReportingSubmissionPayload.Create(Report(false), "", "error", "current", "MUST-NOT-ATTACH");
        Check(!currentOnly.Contains("MUST-NOT-ATTACH") && JObject.Parse(currentOnly)["diagnostics"]["previousSession"] == null, "Uncorrelated previous logs attached");
    }
    private static void Bounds()
    {
        ReportingReport report = Report(true);
        string result = ReportingSubmissionPayload.Create(report, new string('\u2603', 4000), "manual", new string('\u2603', 50000), new string('\u2603', 50000));
        Check(Encoding.UTF8.GetByteCount(result) <= ReportingSubmissionPayload.MaximumBytes, "Multibyte report exceeded payload limit");
        Check(result.Contains("Earlier entries omitted"), "Truncation not disclosed");
        string emoji = string.Concat(System.Linq.Enumerable.Repeat("\U0001f680", 25000));
        string emojiResult = ReportingSubmissionPayload.Create(report, "emoji", "error", "x" + emoji, "x" + emoji);
        string log = (string)JObject.Parse(emojiResult)["diagnostics"]["logs"];
        Check(!log.Contains("\ufffd") && Encoding.UTF8.GetByteCount(emojiResult) <= ReportingSubmissionPayload.MaximumBytes, "Surrogate tail broken");
        ReportingReport huge = ReportingReport.Create(delegate { return "{\"blob\":\"" + new string('x', 25000) + "\"}"; }, null, DateTime.UtcNow);
        Check(ReportingSubmissionPayload.Create(huge, "", "error", "", "").Contains("size_limit"), "Oversize metadata retained");
        ReportingReport broken = ReportingReport.Create(delegate { return "invalid json"; }, null, DateTime.UtcNow);
        Check(ReportingSubmissionPayload.Create(broken, "", "error", "", "").Contains("invalid_source"), "Invalid metadata not marked unavailable");
        Throws(delegate { ReportingSubmissionPayload.Create(report, new string('x', 4001), "manual", "", ""); }, "Description length accepted");
        Throws(delegate { ReportingSubmissionPayload.Create(report, "", "unknown", "", ""); }, "Unknown kind accepted");
        Throws(delegate { ReportingSubmissionPayload.Create(null, "", "manual", "", ""); }, "Null report accepted");
    }
    private static void Throws(Action action, string message)
    { bool thrown = false; try { action(); } catch (ArgumentException) { thrown = true; } Check(thrown, message); }
    private static JObject Receipt(string id)
    { return new JObject { ["schemaVersion"] = 1, ["status"] = "submitted", ["reportId"] = id, ["issueNumber"] = 187, ["issueUrl"] = "https://github.com/jarlbrak/ftk-mod-framework/issues/187" }; }
    private static void Responses()
    {
        string id = new string('a', 32);
        JObject good = Receipt(id);
        Check(ReportingSubmission.ParseResult(good.ToString(), id).Success, "Valid success rejected");
        string[] malformed = { "", "null", "[]", "{", "{\"schemaVersion\":2,\"status\":\"submitted\"}" };
        foreach (string json in malformed) Check(!ReportingSubmission.ParseResult(json, id).Success, "Malformed response accepted");
        string[] urls = { "http://github.com/jarlbrak/ftk-mod-framework/issues/187", "https://github.com/attacker/repo/issues/187", "https://github.com/jarlbrak/ftk-mod-framework/issues/188", "https://github.com/jarlbrak/ftk-mod-framework/issues/187?token=secret", "https://github.com.evil.example/jarlbrak/ftk-mod-framework/issues/187", "https://github.com/jarlbrak/ftk-mod-framework/issues/187/" };
        foreach (string url in urls) { JObject response = Receipt(id); response["issueUrl"] = url; Check(!ReportingSubmission.ParseResult(response.ToString(), id).Success, "Foreign/mismatched URL accepted"); }
        JObject wrongId = Receipt(new string('b', 32));
        Check(!ReportingSubmission.ParseResult(wrongId.ToString(), id).Success, "Foreign report receipt accepted");
        foreach (JToken number in new JToken[] { 0, -1, "187", 187.1, true, JValue.CreateNull() })
        { JObject response = Receipt(id); response["issueNumber"] = number; Check(!ReportingSubmission.ParseResult(response.ToString(), id).Success, "Malformed issue number accepted: " + number); }
        foreach (JToken version in new JToken[] { "1", 1.1, true, JValue.CreateNull() })
        { JObject response = Receipt(id); response["schemaVersion"] = version; Check(!ReportingSubmission.ParseResult(response.ToString(), id).Success, "Malformed schema accepted: " + version); }
        Check(!ReportingSubmission.ParseResult(Receipt("bad").ToString(), "bad").Success, "Invalid report identity accepted");
        Check(!ReportingSubmission.ParseResult("{\"schemaVersion\":1,\"status\":\"pending\",\"error\":\"retry_later\"}", id).Success, "Pending status treated as success");
    }
    private static int FakeHelper(string[] args)
    {
        string request = null, result = null;
        for (int i = 1; i < args.Length - 1; i++)
        { if (args[i] == "--request") request = args[++i]; else if (args[i] == "--result") result = args[++i]; }
        if (request == null || result == null) return 99;
        string root = Path.GetDirectoryName(request);
        File.AppendAllText(Path.Combine(root, "helper-calls.txt"), "call\n");
        string json = File.ReadAllText(request); File.WriteAllText(Path.Combine(root, "observed.json"), json);
        string id = (string)JObject.Parse(json)["reportId"];
        JObject response = File.Exists(Path.Combine(root, "allow-submit")) ? Receipt(id) :
            new JObject { ["schemaVersion"] = 1, ["status"] = "pending", ["error"] = "retry_later", ["reportId"] = id };
        File.WriteAllText(result, response.ToString(Formatting.None)); return 0;
    }
    private static void Bridge()
    {
        if (OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "ftk-report-submit-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        BepInEx.Paths.BepInExRootPath = root;
        Directory.CreateDirectory(Path.Combine(root, "ftkmf"));
        string helper = Path.Combine(Path.Combine(root, "ftkmf"), "ftkmf-launcher-helper");
        try
        {
            File.WriteAllText(helper, "#!/bin/sh\nexec " + ShellQuote(Environment.ProcessPath) + " --helper \"$@\"\n");
            File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            ReportingSubmission.Initialize(); Spin(delegate { return ReportingSubmission.Ready; });
            string delivery = Path.Combine(root, "ReportingDelivery");
            ReportingReport report = Report(true);
            string frozen = ReportingSubmissionPayload.Create(report, "frozen description", "manual", "frozen-current", "frozen-previous");
            ReportingSubmissionResult first = null;
            Check(ReportingSubmission.Start(frozen, delegate(ReportingSubmissionResult value) { first = value; }), "Submission did not start");
            Check(!ReportingSubmission.Start(frozen, null), "Concurrent send allowed");
            report.IncludeMetadata = false;
            Spin(delegate { return first != null; });
            Check(!first.Success && first.Error == "retry_later", "Offline/pending helper result not propagated");
            Check(File.ReadAllText(Path.Combine(delivery, "pending.json")) == frozen && ReportingSubmission.PendingPayload == frozen, "Frozen request was not durable");
            Check(File.ReadAllText(Path.Combine(delivery, "observed.json")) == frozen, "Helper did not receive frozen request");
            string other = ReportingSubmissionPayload.Create(Report(false), "different report", "manual", "", "");
            ReportingSubmissionResult conflict = Send(other);
            Check(!conflict.Success && conflict.Error == "pending_report_exists", "Pending report overwritten by new report");
            Check(File.ReadAllText(Path.Combine(delivery, "pending.json")) == frozen, "Conflict changed pending file");
            File.WriteAllText(Path.Combine(delivery, "allow-submit"), "yes");
            ReportingSubmissionResult done = Send(frozen);
            Check(done.Success && done.ReportId == report.ReportId && done.IssueNumber == 187, "Retry failed to submit same report");
            Check(ReportingSubmission.PendingPayload == null && !File.Exists(Path.Combine(delivery, "pending.json")), "Successful send left pending data");
            int calls = File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length;
            Check(Send(frozen).Success, "Receipt replay failed");
            Check(File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length == calls, "Receipt replay sent duplicate request");
            FTKModFramework.Core.Marketplace.MarketplaceProtocol.RefuseHelper = true;
            ReportingSubmissionResult unavailable = Send(other);
            Check(!unavailable.Success, "Missing helper accepted");
            Check(ReportingSubmission.PendingPayload == other && File.ReadAllText(Path.Combine(delivery, "pending.json")) == other, "Missing helper lost consented report");
            bool? staleDiscard = null;
            ReportingSubmission.DiscardPending(frozen, delegate(bool success) { staleDiscard = success; });
            Spin(delegate { return staleDiscard.HasValue; });
            Check(staleDiscard == false && ReportingSubmission.PendingPayload == other, "Stale editor discarded another pending report");
            bool? discarded = null;
            ReportingSubmission.DiscardPending(other, delegate(bool success) { discarded = success; });
            Spin(delegate { return discarded.HasValue; });
            Check(discarded == true && ReportingSubmission.PendingPayload == null && !File.Exists(Path.Combine(delivery, "pending.json")), "Discard failed");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static ReportingSubmissionResult Send(string json)
    {
        ReportingSubmissionResult result = null;
        Check(ReportingSubmission.Start(json, delegate(ReportingSubmissionResult value) { result = value; }), "Send refused");
        Spin(delegate { return result != null; }); return result;
    }
    private static void Spin(Func<bool> done)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!done()) { if (timeout.ElapsedMilliseconds > 10000) throw new Exception("Callback timed out"); ReportingSubmission.Tick(); Thread.Sleep(10); }
    }
    private static string ShellQuote(string value) { return "'" + value.Replace("'", "'\\''") + "'"; }
}
