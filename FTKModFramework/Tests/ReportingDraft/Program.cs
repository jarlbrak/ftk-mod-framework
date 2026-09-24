using System;
using System.IO;
using FTKModFramework.Core.Reporting;
internal static class Program
{
    private static readonly DateTime Now = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-draft-test-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        ReportingReport report = ReportingReport.Create(delegate { return "current"; }, new ReportingIncident {
            SessionId = new string('a', 32), CheckpointId = new string('b', 32), Metadata = "prior", ObservedAtUtc = Now }, Now);
        try
        {
            ReportingSessionStore session = Session(root); ReportingDraftStore store = Drafts(session, root, Now);
            ReportingDraft draft = ReportingDraft.Create(report, "description 🎲", Now);
            Check(store.TrySave(draft, Now), "save");
            Check(!store.Saved.Report.IncludeMetadata && store.Saved.Report.ReportId == report.ReportId, "recovery identity and consent");
            store.Saved.Report.IncludeMetadata = true;
            Check(!store.Saved.Report.IncludeMetadata, "detached cache");
            Check(store.Saved.Report.PreviousMetadata == "prior", "prior preserved");
            ReportingReport other = ReportingReport.Create(delegate { return "other"; }, null, Now);
            Check(!store.TrySave(ReportingDraft.Create(other, "other", Now), Now), "different identity protected");
            Check(store.TrySave(ReportingDraft.Create(report, "updated", Now), Now), "same identity update");
            Check(store.Saved.Description == "updated", "updated text");
            bool invalid = false;
            try { ReportingDraft.Create(report, new string('界', 3000), Now); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "UTF8 narrative rejected");
            File.WriteAllBytes(Path.Combine(root, "foreign.bin"), new byte[ReportingSessionStore.StoreLimit]);
            Check(!store.TrySave(ReportingDraft.Create(report, "failed update", Now), Now), "shared quota blocks staging");
            Check(store.Saved.Description == "updated", "failed save retains cache"); File.Delete(Path.Combine(root, "foreign.bin"));
            Check(session.TryRecordShutdown(Now), "normal quit"); session.Dispose();
            session = Session(root); store = Drafts(session, root, Now);
            Check(session.Pending == null && store.Saved.Description == "updated", "normal restart draft roundtrip");
            Check(store.Saved.Report.CaptureId == report.CaptureId && store.Saved.Report.PreviousCheckpointId == report.PreviousCheckpointId, "capture provenance");
            File.WriteAllText(Path.Combine(root, "draft-not-ours.record"), "untouched");
            File.WriteAllText(Path.Combine(root, "draft-stage-not-ours.tmp"), "untouched");
            Check(store.TrySave(ReportingDraft.Create(report, "final", Now), Now), "save beside foreign names");
            Check(File.ReadAllText(Path.Combine(root, "draft-stage-not-ours.tmp")) == "untouched", "foreign stage preserved");
            session.Dispose(); session = Session(root); store = Drafts(session, root, Now.AddDays(8));
            Check(store.Saved == null, "expired draft not offered"); session.Dispose();
            session = Session(root); store = Drafts(session, root, Now);
            Check(store.TrySave(draft, Now), "save after expiry");
            string file = null; foreach (string candidate in Directory.GetFiles(root, "draft-*.record")) if (Path.GetFileName(candidate) != "draft-not-ours.record") file = candidate;
            File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
            ReportingDraftStore corrupt;
            Check(!ReportingDraftStore.TryOpen(session, root, Now, out corrupt), "corruption fails closed");
            session.Dispose(); Check(!store.TrySave(draft, Now), "released lease cannot write");
            ExportChecks(Path.Combine(root, "export-root"));
            Console.WriteLine("PASS draft identity/provenance, reset consent, updates, slot conflict, quota failure, normal restart, expiry, corruption, owned lease");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void ExportChecks(string root)
    {
        using (ReportingSessionStore owner = Session(root))
        {
            ReportingExportStore exports = new ReportingExportStore(owner, root);
            string id = new string('d', 32), capture = new string('e', 32);
            ReportingExportRequest textOnly = ReportingExportRequest.Create(id, 1, null, "exact reviewed 🎲\ntext", null);
            ReportingExportArtifact first = exports.TryPublish(textOnly, Now);
            Check(first != null && first.DiagnosticsPath == null && File.ReadAllText(first.ReportPath) == textOnly.ReportText, "exact text-only artifact");
            Check(File.ReadAllBytes(first.ReportPath)[0] == (byte)'e', "UTF8 without BOM");
            Check(exports.TryPublish(textOnly, Now).ManifestPath == first.ManifestPath, "idempotent revision retry");
            Check(exports.TryPublish(ReportingExportRequest.Create(id, 1, null, "different", null), Now) == null, "immutable revision conflict");
            ReportingExportArtifact included = exports.TryPublish(ReportingExportRequest.Create(id, 2, capture, "approved report", "approved diagnostics"), Now);
            Check(included != null && File.ReadAllText(included.DiagnosticsPath) == "approved diagnostics", "included artifact");
            File.WriteAllBytes(Path.Combine(root, "quota.bin"), new byte[ReportingSessionStore.StoreLimit]);
            Check(exports.TryPublish(ReportingExportRequest.Create(id, 3, null, "blocked", null), Now) == null, "export shared quota");
            Check(File.ReadAllText(included.DiagnosticsPath) == "approved diagnostics", "failed export preserves prior");
            File.Delete(Path.Combine(root, "quota.bin"));
            File.WriteAllText(included.ManifestPath, "corrupt");
            Check(exports.TryPublish(ReportingExportRequest.Create(id, 2, capture, "approved report", "approved diagnostics"), Now) == null, "corrupt manifest never reused");
            File.WriteAllText(Path.Combine(root, "export-not-ours.manifest"), "foreign");
            exports.PruneExpired(Now.AddDays(8));
            Check(!File.Exists(first.ReportPath) && !File.Exists(first.ManifestPath), "validated expired artifact removed");
            Check(File.Exists(included.DiagnosticsPath) && File.ReadAllText(Path.Combine(root, "export-not-ours.manifest")) == "foreign", "unknown and corrupt artifact preserved");
        }
    }
    private static ReportingSessionStore Session(string root)
    { ReportingSessionStore store; Check(ReportingSessionStore.TryOpen(root, Now, out store), "session lease"); return store; }
    private static ReportingDraftStore Drafts(ReportingSessionStore owner, string root, DateTime now)
    { ReportingDraftStore store; Check(ReportingDraftStore.TryOpen(owner, root, now, out store), "draft load"); return store; }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
}
