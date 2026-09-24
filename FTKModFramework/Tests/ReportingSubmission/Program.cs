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
        ExclusionAndFreeze(); Bounds(); UsefulLogDump(); Responses(); HelperEnvironment();
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
        Check(Encoding.UTF8.GetByteCount(log) <= ReportingSubmissionPayload.MaximumLogBytes, "Log UTF8 byte limit exceeded");
        ReportingReport huge = ReportingReport.Create(delegate { return "{\"blob\":\"" + new string('x', 25000) + "\"}"; }, null, DateTime.UtcNow);
        Check(ReportingSubmissionPayload.Create(huge, "", "error", "", "").Contains("size_limit"), "Oversize metadata retained");
        ReportingReport broken = ReportingReport.Create(delegate { return "invalid json"; }, null, DateTime.UtcNow);
        Check(ReportingSubmissionPayload.Create(broken, "", "error", "", "").Contains("invalid_source"), "Invalid metadata not marked unavailable");
        Throws(delegate { ReportingSubmissionPayload.Create(report, new string('x', 4001), "manual", "", ""); }, "Description length accepted");
        Throws(delegate { ReportingSubmissionPayload.Create(report, "", "unknown", "", ""); }, "Unknown kind accepted");
        Throws(delegate { ReportingSubmissionPayload.Create(null, "", "manual", "", ""); }, "Null report accepted");
    }
    private static void UsefulLogDump()
    {
        string current = "CURRENT BEGIN\n" + new string('i', 120000) + "\nCURRENT WARN END";
        string previous = "PREVIOUS BEGIN\n" + new string('w', 120000) + "\nPREVIOUS ERROR END";
        string json = ReportingSubmissionPayload.Create(Report(true), "Large diagnostic log dump", "unexpected_exit", current, previous);
        JObject payload = JObject.Parse(json);
        Check(Encoding.UTF8.GetByteCount(json) > 128 * 1024, "Useful log dump did not exercise larger envelope");
        Check((string)payload["diagnostics"]["logs"] == current, "Current useful log was trimmed to old 20k limit");
        Check((string)payload["diagnostics"]["previousSession"]["logs"] == previous, "Previous useful log was trimmed to old 20k limit");
        string manyEmoji = string.Concat(System.Linq.Enumerable.Repeat("\U0001f680", 50000)) + "LATEST FAILURE";
        payload = JObject.Parse(ReportingSubmissionPayload.Create(Report(false), "", "error", manyEmoji, null));
        string tail = (string)payload["diagnostics"]["logs"];
        Check(Encoding.UTF8.GetByteCount(tail) <= ReportingSubmissionPayload.MaximumLogBytes && !tail.Contains("\ufffd") && tail.EndsWith("LATEST FAILURE"), "Bounded log tail lost newest text or split UTF8");
        string escaping = new string('\u0001', ReportingSubmissionPayload.MaximumLogBytes);
        Check(Encoding.UTF8.GetByteCount(ReportingSubmissionPayload.Create(Report(true), "", "unexpected_exit", escaping, escaping)) <= ReportingSubmissionPayload.MaximumBytes, "JSON escaping exceeded transport cap");
        Check(ReportingSubmissionPayload.Disclosure.Contains("informational messages, warnings and errors"), "Disclosure still promises only error logs");
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
        JObject environment = new JObject();
        foreach (string name in InjectionVariables) environment[name] = Environment.GetEnvironmentVariable(name);
        environment["FTK_REPORTING_TEST_CONTEXT"] = Environment.GetEnvironmentVariable("FTK_REPORTING_TEST_CONTEXT");
        File.WriteAllText(Path.Combine(root, "observed-environment.json"), environment.ToString());
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
        string[] inherited = new string[InjectionVariables.Length];
        for (int i = 0; i < InjectionVariables.Length; i++)
        {
            inherited[i] = Environment.GetEnvironmentVariable(InjectionVariables[i]);
            Environment.SetEnvironmentVariable(InjectionVariables[i], "reporting-test-injector");
        }
        string context = Environment.GetEnvironmentVariable("FTK_REPORTING_TEST_CONTEXT");
        Environment.SetEnvironmentVariable("FTK_REPORTING_TEST_CONTEXT", "ordinary-helper-context");
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
            JObject observedEnvironment = JObject.Parse(File.ReadAllText(Path.Combine(delivery, "observed-environment.json")));
            foreach (string name in InjectionVariables)
            {
                Check(observedEnvironment[name].Type == JTokenType.Null, "Helper inherited game injector: " + name);
                Check(Environment.GetEnvironmentVariable(name) == "reporting-test-injector", "Parent injector environment changed: " + name);
            }
            Check((string)observedEnvironment["FTK_REPORTING_TEST_CONTEXT"] == "ordinary-helper-context", "Unrelated helper environment removed");
            string other = ReportingSubmissionPayload.Create(Report(false), "different report", "manual", "", "");
            ReportingSubmissionResult conflict = Send(other);
            Check(!conflict.Success && conflict.Error == "pending_report_exists", "Pending report overwritten by new report");
            Check(File.ReadAllText(Path.Combine(delivery, "pending.json")) == frozen, "Conflict changed pending file");
            File.WriteAllText(Path.Combine(delivery, "allow-submit"), "yes");
            ReportingSubmissionResult done = Send(frozen);
            Check(done.Success && done.ReportId == report.ReportId && done.IssueNumber == 187, "Retry failed to submit same report");
            Check(ReportingSubmission.PendingPayload == null && !File.Exists(Path.Combine(delivery, "pending.json")), "Successful send left pending data");
            int calls = File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length;
            FTKModFramework.Core.Marketplace.MarketplaceProtocol.RefuseHelper = true;
            Check(Send(frozen).Success, "Receipt replay failed");
            Check(File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length == calls, "Receipt replay sent duplicate request");
            FTKModFramework.Core.Marketplace.MarketplaceProtocol.RefuseHelper = false;
            string receiptPath = Path.Combine(delivery, "submitted.json");
            string boundReceipt = File.ReadAllText(receiptPath);
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                string expectedHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(frozen))).Replace("-", "").ToLowerInvariant();
                Check((string)JObject.Parse(boundReceipt)["requestSha256"] == expectedHash, "Receipt not bound to exact UTF8 request");
            }
            JObject changed = JObject.Parse(frozen); changed["description"] = "Changed description \u2603";
            string changedSameId = changed.ToString(Formatting.None);
            File.Delete(Path.Combine(delivery, "allow-submit"));
            ReportingSubmissionResult changedResult = Send(changedSameId);
            Check(!changedResult.Success && changedResult.Error == "retry_later", "Changed payload with same ID replayed old success");
            Check(File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length == calls + 1, "Changed payload skipped server idempotency");
            Check(ReportingSubmission.PendingPayload == changedSameId && File.ReadAllText(Path.Combine(delivery, "pending.json")) == changedSameId, "Old receipt discarded changed request");
            Check(File.ReadAllText(receiptPath) == boundReceipt, "Pending changed request replaced successful receipt");
            Discard(changedSameId);
            string whitespaceChanged = frozen + "\n";
            ReportingSubmissionResult whitespaceResult = Send(whitespaceChanged);
            Check(!whitespaceResult.Success && ReportingSubmission.PendingPayload == whitespaceChanged, "Receipt matched reserialized content instead of exact bytes");
            Discard(whitespaceChanged);
            File.WriteAllText(receiptPath, Receipt(report.ReportId).ToString(Formatting.None));
            calls = File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length;
            ReportingSubmissionResult legacy = Send(frozen);
            Check(!legacy.Success && legacy.Error == "retry_later", "Legacy unbound receipt replayed success");
            Check(File.ReadAllLines(Path.Combine(delivery, "helper-calls.txt")).Length == calls + 1 && ReportingSubmission.PendingPayload == frozen, "Legacy receipt bypassed server retry or lost pending request");
            File.WriteAllText(Path.Combine(delivery, "allow-submit"), "yes");
            Check(Send(frozen).Success, "Legacy receipt did not recover through server submission");
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
        finally
        {
            for (int i = 0; i < InjectionVariables.Length; i++) Environment.SetEnvironmentVariable(InjectionVariables[i], inherited[i]);
            Environment.SetEnvironmentVariable("FTK_REPORTING_TEST_CONTEXT", context);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private static readonly string[] InjectionVariables = {
        "DYLD_INSERT_LIBRARIES", "LD_PRELOAD", "DOORSTOP_ENABLED", "DOORSTOP_TARGET_ASSEMBLY",
        "DOORSTOP_BOOT_CONFIG_OVERRIDE", "DOORSTOP_IGNORE_DISABLED_ENV", "DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE",
        "DOORSTOP_MONO_DEBUG_ENABLED", "DOORSTOP_MONO_DEBUG_ADDRESS", "DOORSTOP_MONO_DEBUG_SUSPEND",
        "DOORSTOP_CLR_RUNTIME_CORECLR_PATH", "DOORSTOP_CLR_CORLIB_DIR", "DOORSTOP_DISABLE"
    };
    private static void HelperEnvironment()
    {
        ProcessStartInfo info = new ProcessStartInfo();
        foreach (string name in InjectionVariables) info.EnvironmentVariables[name] = "libdoorstop-test";
        info.EnvironmentVariables["doorstop_future_setting"] = "future-control";
        info.EnvironmentVariables["FTK_REPORTING_TEST_CONTEXT"] = "preserved";
        ReportingSubmission.PrepareHelperEnvironment(info);
        foreach (string name in InjectionVariables) Check(!info.EnvironmentVariables.ContainsKey(name), "Prepared environment kept " + name);
        Check(!info.EnvironmentVariables.ContainsKey("doorstop_future_setting"), "Doorstop prefix cleanup missed another setting");
        Check(info.EnvironmentVariables["FTK_REPORTING_TEST_CONTEXT"] == "preserved", "Prepared environment lost unrelated context");
    }
    private static ReportingSubmissionResult Send(string json)
    {
        ReportingSubmissionResult result = null;
        Check(ReportingSubmission.Start(json, delegate(ReportingSubmissionResult value) { result = value; }), "Send refused");
        Spin(delegate { return result != null; }); return result;
    }
    private static void Discard(string payload)
    {
        bool? done = null;
        ReportingSubmission.DiscardPending(payload, delegate(bool value) { done = value; });
        Spin(delegate { return done.HasValue; });
        Check(done == true && ReportingSubmission.PendingPayload == null, "Matching pending discard failed");
    }
    private static void Spin(Func<bool> done)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!done()) { if (timeout.ElapsedMilliseconds > 10000) throw new Exception("Callback timed out"); ReportingSubmission.Tick(); Thread.Sleep(10); }
    }
    private static string ShellQuote(string value) { return "'" + value.Replace("'", "'\\''") + "'"; }
}
