using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using FTKModFramework.Core.Reporting;

namespace BepInEx { internal static class Paths { internal static string BepInExRootPath; } }
namespace FTKModFramework { internal static class Plugin { internal const string Version = "test"; } }
namespace FTKModFramework.Core.HotReload
{
    internal static class HotReloadCoordinator { internal static bool Busy, Faulted; internal static int Epoch; }
}
namespace FTKModFramework.Core.Reporting
{
    internal static class ReportingDiagnostics
    {
        internal static void Start() { }
        internal static void BindSession(ReportingSessionStore owner, string root) { }
        internal static void Flush() { }
        internal static void Stop() { }
    }
    internal static class ReportingSources
    {
        internal static int MainThread, Captures;
        internal static string Capture(bool ready)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread) throw new Exception("not Unity thread");
            Captures++;
            return ReportingMetadata.Capture(new ReportingMetadataInput { SourcesReady = ready, FrameworkVersion = "test" }, DateTime.UtcNow);
        }
    }
}
internal static class Program
{
    private static void Main(string[] args)
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-report-runtime-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        BepInEx.Paths.BepInExRootPath = root;
        ReportingSources.MainThread = Thread.CurrentThread.ManagedThreadId;
        string storeRoot = Path.Combine(root, "Reporting");
        ReportingSessionStore prior;
        Check(ReportingSessionStore.TryOpen(storeRoot, DateTime.UtcNow, out prior), "prepare store");
        Check(prior.TryCheckpoint("old metadata", "title", DateTime.UtcNow), "prior checkpoint");
        bool busy = args.Length != 0;
        if (!busy) prior.Dispose();
        try
        {
            ReportingRuntime.Start();
            Wait(delegate { return ReportingRuntime.Notice != "Restart tracking is starting."; });
            Check(ReportingRuntime.TrackingAvailable != busy, "lease status");
            Check(ReportingSources.Captures == 0, "startup uses no game sources");
            if (busy)
            {
                Check(ReportingRuntime.Pending == null, "busy owner never read");
                ReportingReport manual = ReportingRuntime.CreateReport(false);
                Check(manual.CurrentMetadata != null, "manual reporting remains available");
                bool rejected = false;
                ReportingRuntime.SaveDraft(manual, "busy", delegate(bool ok) { Check(!ok, "busy lease rejects save"); rejected = true; });
                Wait(delegate { ReportingRuntime.Tick(); return rejected; });
                bool exportRejected = false;
                ReportingRuntime.ExportReviewed(manual.ReportId, 1, null, "reviewed", null, delegate(ReportingExportArtifact artifact) { Check(artifact == null, "busy root rejects export"); exportRejected = true; });
                Wait(delegate { ReportingRuntime.Tick(); return exportRejected; });
            }
            else
            {
                Check(ReportingRuntime.Pending.Metadata == "old metadata", "previous provenance");
                ReportingReport report = ReportingRuntime.CreateReport(true);
                Check(report.PreviousMetadata == "old metadata" && ReportingRuntime.Pending != null, "review does not consume pending");
                ReportingIssueNarrative narrative = new ReportingIssueNarrative("Synthetic summary &labels=evil#fragment", "1. Launch the game.\n2. Open Options.", "Expected: menu opens.\nActual: game closes.", false);
                ReportingIssuePlan issuePlan = ReportingIssueHandoff.Create(report, narrative);
                Check(issuePlan.Prefilled && issuePlan.Frequency == "Not sure" && issuePlan.Affected == "Not sure", "valid optional form choices");
                Check(issuePlan.Reproduction == narrative.Reproduction && issuePlan.ExpectedActual == narrative.ExpectedActual && issuePlan.ReportText.Contains(narrative.ExpectedActual), "reviewed fields preserved in browser and local report");
                Check(issuePlan.Url.Contains("summary=Synthetic%20summary%20%26labels%3Devil%23fragment") && issuePlan.Url.Contains("repro=1.%20Launch") && issuePlan.Url.Contains("frequency=Not%20sure"), "required fields safely prefilled");
                Check(!issuePlan.Url.Contains("old%20metadata") && !issuePlan.Url.Contains("old metadata") && issuePlan.DiagnosticsText.Contains("reportCreationMetadata"), "full metadata stays out of URL and in attachment");
                Check(System.Text.Encoding.ASCII.GetByteCount(issuePlan.Url) <= ReportingIssueHandoff.MaximumUrlBytes, "bounded complete URL");
                string marker = "PRIVATE_METADATA_MARKER_9f4a";
                ReportingReport privacyReport = ReportingReport.Create(delegate { return "{\"schemaVersion\":1,\"marker\":\"" + marker + "\"}"; }, null, DateTime.UtcNow);
                ReportingIssuePlan privacyPlan = ReportingIssueHandoff.Create(privacyReport, narrative);
                Check(!privacyPlan.Url.Contains(marker) && privacyPlan.DiagnosticsText.Contains(marker), "private metadata is attachment-only");
                ReportingIssuePlan multilinePlan = ReportingIssueHandoff.Create(report,
                    new ReportingIssueNarrative("First summary line\nAdditional summary context", "Step", "Expected and actual.", false));
                Check(multilinePlan.Title == "Bug: First summary line" && multilinePlan.Summary.Contains("Additional summary context"), "title is single-line while summary stays complete");
                ReportingIssueNarrative longNarrative = new ReportingIssueNarrative(new string('x', 7600), "No steps", "Expected and actual are not known.", false);
                ReportingIssuePlan boundedPlan = ReportingIssueHandoff.Create(report, longNarrative);
                Check(!boundedPlan.Prefilled && boundedPlan.Url == "https://github.com/jarlbrak/ftk-mod-framework/issues/new?template=bug_report.yml", "oversized prefill uses blank template");
                Check(boundedPlan.TitleShortened && ScalarCount(boundedPlan.Title) == 120, "visible scalar-safe title bound");
                report.IncludeMetadata = false;
                ReportingIssuePlan textOnlyPlan = ReportingIssueHandoff.Create(report, narrative);
                Check(textOnlyPlan.DiagnosticsText == null && textOnlyPlan.Url.Contains("Metadata%20was%20excluded"), "text-only plan has no diagnostics attachment");
                report.IncludeMetadata = true;
                Check(ReportingRuntime.DraftsReady, "draft loading completed");
                bool saved = false;
                ReportingRuntime.SaveDraft(report, "saved description", delegate(bool ok) {
                    Check(Thread.CurrentThread.ManagedThreadId == ReportingSources.MainThread && ok, "main-thread saved callback"); saved = true;
                });
                Wait(delegate { ReportingRuntime.Tick(); return saved; });
                Check(ReportingRuntime.SavedDraft.Description == "saved description" && ReportingRuntime.SavedDraft.Report.IncludeMetadata, "saved diagnostics default");
                ReportingRuntime.SavedDraft.Report.IncludeMetadata = false;
                Check(ReportingRuntime.SavedDraft.Report.IncludeMetadata, "cache detached from UI mutation");
                Check(ReportingRuntime.Pending != null, "save does not acknowledge incident");
                ReportingReport secondDraft = ReportingRuntime.CreateReport(false);
                secondDraft.IncludeMetadata = false;
                ReportingDraft snapshot = ReportingDraft.Create(secondDraft, "second draft", DateTime.UtcNow, "error", "exact captured errors", "", new string('e', 32));
                bool secondSaved = false;
                ReportingRuntime.SaveDraft(snapshot, delegate(bool ok) { Check(ok, "second distinct draft saved"); secondSaved = true; });
                Check(ReportingRuntime.DraftsBusy, "save busy until callback");
                Wait(delegate { ReportingRuntime.Tick(); return secondSaved; });
                Check(!ReportingRuntime.DraftsBusy && ReportingRuntime.SavedDrafts.Length == 2 &&
                    ReportingRuntime.SavedDraft.CurrentLogs == "exact captured errors" && !ReportingRuntime.SavedDraft.Report.IncludeMetadata, "draft collection frozen logs and explicit optout");
                ReportingRuntime.SavedDrafts[0].Report.IncludeMetadata = true;
                Check(!ReportingRuntime.SavedDraft.Report.IncludeMetadata, "collection detached from consumer");
                string pressure = Path.Combine(storeRoot, "user-quota-pressure.bin");
                File.WriteAllBytes(pressure, new byte[ReportingSessionStore.StoreLimit]);
                bool failedDelete = false;
                ReportingRuntime.DeleteDraft(secondDraft.ReportId, delegate(bool ok) { Check(!ok, "quota rejects deletion safely"); failedDelete = true; });
                Check(ReportingRuntime.DraftsBusy && ReportingRuntime.SavedDrafts.Length == 1, "target draft hidden during delete");
                Wait(delegate { ReportingRuntime.Tick(); return failedDelete; });
                Check(ReportingRuntime.SavedDrafts.Length == 2 && ReportingRuntime.SavedDraft.Report.ReportId == secondDraft.ReportId &&
                    ReportingRuntime.SavedDraft.CurrentLogs == "exact captured errors", "failed user deletion restores exact snapshot");
                File.Delete(pressure);
                bool secondDeleted = false;
                ReportingRuntime.DeleteDraft(secondDraft.ReportId, delegate(bool ok) { Check(ok, "delete retry succeeds"); secondDeleted = true; });
                Wait(delegate { ReportingRuntime.Tick(); return secondDeleted; });
                Check(ReportingRuntime.SavedDrafts.Length == 1 && ReportingRuntime.SavedDraft.Report.ReportId == report.ReportId, "single draft delete preserves other identity");
                bool wrongDelete = false;
                ReportingRuntime.DeleteDraft(Guid.NewGuid().ToString("N"), delegate(bool ok) { Check(!ok, "unrelated draft preserved"); wrongDelete = true; });
                Wait(delegate { ReportingRuntime.Tick(); return wrongDelete; });
                Check(ReportingRuntime.SavedDraft.Report.ReportId == report.ReportId, "unrelated submission keeps saved draft");
                bool deleted = false;
                ReportingRuntime.DeleteDraft(report.ReportId, delegate(bool ok) { Check(ok, "submitted draft retired"); deleted = true; });
                Check(ReportingRuntime.SavedDraft == null, "submitted draft hidden before deletion callback");
                Wait(delegate { ReportingRuntime.Tick(); return deleted; });
                Check(ReportingRuntime.SavedDraft == null, "submitted draft remains retired");
                ReportingReport nextReport = ReportingRuntime.CreateReport(false);
                Check(nextReport.ReportId != report.ReportId, "next report has a fresh identity");
                saved = false;
                ReportingRuntime.SaveDraft(nextReport, "saved description", delegate(bool ok) { Check(ok, "new draft saved after retirement"); saved = true; });
                Wait(delegate { ReportingRuntime.Tick(); return saved; });
                bool exported = false;
                ReportingRuntime.ExportReviewed(report.ReportId, 1, report.CaptureId, "exact approved text", report.CurrentMetadata, delegate(ReportingExportArtifact artifact) {
                    Check(Thread.CurrentThread.ManagedThreadId == ReportingSources.MainThread && artifact != null, "export main-thread callback");
                    Check(File.ReadAllText(artifact.ReportPath) == "exact approved text" && File.ReadAllText(artifact.DiagnosticsPath) == report.CurrentMetadata, "exact approved files");
                    exported = true;
                });
                Wait(delegate { ReportingRuntime.Tick(); return exported; });
                ReportingRuntime.SourcesReady(); ReportingRuntime.Tick();
                int baseline = ReportingSources.Captures;
                FTKModFramework.Core.HotReload.HotReloadCoordinator.Busy = true;
                FTKModFramework.Core.HotReload.HotReloadCoordinator.Epoch++;
                ReportingRuntime.Tick(); Check(ReportingSources.Captures == baseline, "busy candidate not captured");
                FTKModFramework.Core.HotReload.HotReloadCoordinator.Busy = false;
                ReportingRuntime.Tick(); Check(ReportingSources.Captures == baseline + 1, "stable epoch captured");
                FTKModFramework.Core.HotReload.HotReloadCoordinator.Faulted = true;
                FTKModFramework.Core.HotReload.HotReloadCoordinator.Epoch++;
                ReportingRuntime.Tick(); Check(ReportingSources.Captures == baseline + 1, "faulted epoch not captured");
                bool completed = false, throwingSaveDelivered = false;
                ReportingRuntime.SaveDraft(ReportingRuntime.SavedDraft.Report, "saved description", delegate(bool ok) {
                    Check(ok, "second save"); throwingSaveDelivered = true; throw new Exception("synthetic UI failure");
                });
                ReportingRuntime.DismissPending(delegate(bool ok) {
                    Check(Thread.CurrentThread.ManagedThreadId == ReportingSources.MainThread, "main-thread completion");
                    Check(ok, "durable dismiss"); completed = true;
                });
                Wait(delegate { ReportingRuntime.Tick(); return completed; });
                Check(ReportingRuntime.Pending == null && throwingSaveDelivered, "dismiss completion survives throwing save callback");
            }
            ReportingRuntime.Quit();
            if (busy) prior.Dispose();
            ReportingSessionStore next;
            Check(ReportingSessionStore.TryOpen(storeRoot, DateTime.UtcNow, out next), "worker released lease");
            using (next)
            {
                Check(busy || next.Pending == null, "normal quit observed after queued work");
                if (!busy)
                {
                    ReportingDraftStore drafts;
                    Check(ReportingDraftStore.TryOpen(next, storeRoot, DateTime.UtcNow, out drafts), "saved draft load after worker shutdown");
                    Check(drafts.Saved.Description == "saved description", "worker save durable across restart");
                }
            }
            Console.WriteLine("PASS runtime " + (busy ? "busy owner and manual recovery" : "ordering, provenance, epoch and main-thread completion"));
        }
        finally { ReportingRuntime.Quit(); prior.Dispose(); Directory.Delete(root, true); }
    }
    private static void Wait(Func<bool> done)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!done()) { if (timeout.ElapsedMilliseconds > 5000) throw new Exception("wait timed out"); Thread.Sleep(10); }
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static int ScalarCount(string value)
    { int count = 0; for (int i = 0; i < value.Length; i++, count++) if (Char.IsHighSurrogate(value[i]) && i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1])) i++; return count; }
}
