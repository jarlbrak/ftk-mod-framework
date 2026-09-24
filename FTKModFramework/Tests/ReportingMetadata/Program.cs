using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core.Reporting;

internal static class Program
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Main()
    {
        var input = new ReportingMetadataInput { FrameworkVersion = "1.0.2", UnityVersion = "2017.2.2p2",
            GameVersion = "1.1.00", DataContent = true, BehaviorLoading = true, CampaignEngine = true, SelfTests = false, ScaleBudgetGate = false,
            Platform = "Unix", RuntimeVersion = "2.0.50727", ProcessBits = 64, Phase = "session_or_transition", SourcesReady = true };
        var rows = new List<ReportingInventoryRow> {
            new ReportingInventoryRow { Id = "test.mod", Version = "1.2.3", Enabled = true, PendingEnabled = false },
            new ReportingInventoryRow { Id = "/Users/private/name", Version = "https://private/token", Enabled = false },
            new ReportingInventoryRow { Id = "person@example.test", Version = "C:\\private\\secret" } };
        input.Mods = new ReportingInventory { Rows = rows, TotalCount = rows.Count };
        string json = ReportingMetadata.Capture(input, DateTime.UtcNow);
        Require(!json.Contains("private") && !json.Contains("example.test"), "Unsafe machine fields leaked");
        var snapshot = JObject.Parse(json);
        var mods = snapshot["sections"]["mods"]["payload"];
        Require((string)mods[2]["id"] == "test.mod" && (bool)mods[2]["enabled"] && !(bool)mods[2]["pendingEnabled"], "Selection lost");
        Require((string)mods[2]["outcome"] == "unknown", "Invented outcome");
        Require((string)snapshot["sections"]["logs"]["status"] == "omitted", "Logs enabled automatically");
        rows[0].Id = "changed.mod";
        Require(json.Contains("test.mod") && !json.Contains("changed.mod"), "Snapshot retained live references");
        input.Transitioning = true;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "transition_in_progress", "Transition leaked candidate rows");
        input.Transitioning = false; input.RuntimeFaulted = true;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "runtime_faulted", "Fault leaked rows");
        input.RuntimeFaulted = false; input.SourcesReady = false;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "not_initialized", "Early empty inventory claimed success");
        input.SourcesReady = true;
        var huge = new List<ReportingInventoryRow>();
        string maxField = new string('a', 128).Replace("a", "a-");
        for (int i = 0; i < 256; i++) huge.Add(new ReportingInventoryRow { Id = maxField, Version = maxField });
        input.Mods = input.Plugins = input.Active = input.Pending = new ReportingInventory { Rows = huge, TotalCount = 256 };
        json = ReportingMetadata.Capture(input, DateTime.UtcNow);
        Require(Encoding.UTF8.GetByteCount(json) <= ReportingMetadata.MaximumBytes, "Exceeded escaped byte cap");
        Require((string)JObject.Parse(json)["status"] == "partial", "Byte cap claimed complete");
        Require((string)JObject.Parse(json)["reason"] == "limit_reached", "Truncation not disclosed");
        Require(ReportingMetadata.Identifier(new string('a', 257)) == null && ReportingMetadata.Identifier("line\nbreak") == null, "Field cap/control accepted");
        foreach (string sensitive in new[] { "192.168.1.10", "mod.192.168.1.10", "192.168.1.10-mod", "999.192.168.1.10", "ghp_abc", "github_pat_abc", "sk-abc", "my_secret", new string('a', 32) })
            Require(ReportingMetadata.Identifier(sensitive) == null, "Sensitive identifier accepted: " + sensitive);
        var empty = new ReportingInventory { Rows = new List<ReportingInventoryRow>(), TotalCount = 0 };
        input.Mods = input.Plugins = input.Active = input.Pending = empty;
        var healthy = JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow));
        Require((string)healthy["status"] == "complete" && (string)healthy["reason"] == "none", "Invented source error");
        int calls = 0;
        var previous = new ReportingIncident { SessionId = "previous-session", CheckpointId = "previous-checkpoint",
            Metadata = "previous-mods", ObservedAtUtc = DateTime.UtcNow };
        var report = ReportingReport.Create(delegate { calls++; return json; }, previous, DateTime.UtcNow);
        previous.Metadata = "new-session-mods";
        Require(calls == 1 && report.IncludeMetadata && !report.IncludeLogs, "Creation did not collect automatically");
        Require(report.PreviousMetadata == "previous-mods" && report.CurrentMetadata == json, "Restart provenance overwritten");
        report.IncludeMetadata = false;
        Require(!report.IncludeMetadata && report.CurrentMetadata == json, "Exclusion destroyed local recovery evidence");
        var absentReport = ReportingReport.Create(delegate { return null; }, null, DateTime.UtcNow);
        Require((string)JObject.Parse(absentReport.CurrentMetadata)["status"] == "failed", "Absent capture claimed success");
        Require(report.ReportId != report.CaptureId, "Identities conflated");
        var failedReport = ReportingReport.Create(delegate { throw new Exception("private path or secret"); }, null, DateTime.UtcNow);
        Require(!failedReport.CurrentMetadata.Contains("private") && failedReport.CurrentMetadata.Contains("source_error"), "Collector failure leaked text");
        Console.WriteLine("PASS: bounded metadata, redaction, detached selection, missing/transition/fault authority and logs off");
    }
}
