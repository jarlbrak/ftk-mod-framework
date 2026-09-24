using System;
using System.IO;
using FTKModFramework.Core.Reporting;
internal static class Program
{
    private static readonly DateTime Now = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
    private static int checks;
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-draft-test-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        Directory.CreateDirectory(root);
        try
        {
            Collection(Path.Combine(root, "collection"));
            Bounds(Path.Combine(root, "bounds"));
            CorruptCollection(Path.Combine(root, "corrupt"));
            Legacy(Path.Combine(root, "legacy1"), 1);
            Legacy(Path.Combine(root, "legacy2"), 2);
            ExportChecks(Path.Combine(root, "exports"));
            Console.WriteLine("PASS draft collection: " + checks + " checks");
        }
        finally { Directory.Delete(root, true); }
    }
    private static ReportingReport Report(bool previous)
    {
        return ReportingReport.Create(delegate { return "current"; }, previous ? new ReportingIncident {
            SessionId = new string('a', 32), CheckpointId = new string('b', 32), Metadata = "prior", ObservedAtUtc = Now } : null, Now);
    }
    private static void Collection(string root)
    {
        ReportingSessionStore session = Session(root); ReportingDraftStore store = Drafts(session, root, Now);
        ReportingReport report = Report(true);
        Check(report.IncludeMetadata, "new reports default diagnostics on");
        string errorId = new string('e', 32);
        ReportingDraft draft = ReportingDraft.Create(report, "description 🎲", Now, "error", "captured current errors", "captured prior errors", errorId);
        Check(store.TrySave(draft, Now), "first save");
        Check(store.Saved.Report.IncludeMetadata && store.Saved.Report.ReportId == report.ReportId, "recovery identity and diagnostics default");
        store.Saved.Report.IncludeMetadata = false;
        ReportingDraft[] detached = store.Drafts; detached[0] = null;
        Check(store.Saved.Report.IncludeMetadata && store.Drafts[0] != null, "detached copy");
        report.IncludeMetadata = false;
        Check(store.TrySave(ReportingDraft.Create(report, "updated", Now.AddSeconds(1), "error", "frozen current", "frozen previous", errorId), Now.AddSeconds(1)), "same identity update");
        Check(!store.Saved.Report.IncludeMetadata && store.Saved.CurrentLogs == "frozen current", "explicit exclusion and logs retained");
        ReportingReport other = Report(false);
        Check(store.TrySave(ReportingDraft.Create(other, "second", Now.AddSeconds(2)), Now.AddSeconds(2)), "distinct identity allowed");
        Check(store.Drafts.Length == 2 && store.Saved.Report.ReportId == other.ReportId, "newest first");
        Check(!store.TryDelete(new string('f', 32), Now), "unknown delete rejected");
        Check(store.TryDelete(other.ReportId, Now.AddSeconds(3)) && store.Drafts.Length == 1 && store.Saved.Report.ReportId == report.ReportId, "single identity deletion");
        for (int i = 0; i < 9; i++) Check(store.TrySave(ReportingDraft.Create(Report(false), "item " + i, Now.AddMinutes(i + 1)), Now.AddMinutes(i + 1)), "fill slot " + i);
        Check(store.Drafts.Length == 10, "ten drafts retained");
        Check(!store.TrySave(ReportingDraft.Create(Report(false), "eleventh", Now.AddHours(1)), Now.AddHours(1)) && store.Drafts.Length == 10, "eleventh rejected without eviction");
        Check(store.TrySave(ReportingDraft.Create(report, "still editable", Now.AddHours(2), "error", "frozen current", "frozen previous", errorId), Now.AddHours(2)), "update allowed at ten slots");
        string authority = Latest(root);
        long used = 0; foreach (string file in Directory.GetFiles(root)) used += new FileInfo(file).Length;
        string pressure = Path.Combine(root, "foreign-pressure.bin");
        int leave = (int)new FileInfo(authority).Length + 64;
        File.WriteAllBytes(pressure, new byte[ReportingSessionStore.StoreLimit - used - leave]);
        Check(!store.TrySave(ReportingDraft.Create(report, "quota update", Now.AddHours(3), "error", "frozen current", "frozen previous", errorId), Now.AddHours(3)), "save reserves delete staging capacity");
        Check(store.Saved.Description == "still editable", "failed save preserves exact cache");
        string removeId = store.Drafts[9].Report.ReportId;
        Check(store.TryDelete(removeId, Now.AddHours(3)) && store.Drafts.Length == 9, "deletion works after save hits shared quota");
        Check(File.Exists(pressure), "quota relief did not delete foreign data"); File.Delete(pressure);
        Check(session.TryRecordShutdown(Now), "normal quit"); session.Dispose();
        session = Session(root); store = Drafts(session, root, Now.AddHours(3));
        Check(store.Drafts.Length == 9 && store.Saved.Report.ReportId == report.ReportId, "collection restart identity");
        Check(!store.Saved.Report.IncludeMetadata && store.Saved.Kind == "error" && store.Saved.ErrorId == errorId &&
            store.Saved.CurrentLogs == "frozen current" && store.Saved.PreviousLogs == "frozen previous", "choice and captured diagnostic provenance roundtrip");
        Check(store.Saved.Report.CaptureId == report.CaptureId && store.Saved.Report.PreviousCheckpointId == report.PreviousCheckpointId, "report provenance preserved");
        ReportingReport fresh = Report(false);
        Check(store.TrySave(ReportingDraft.Create(fresh, "fresh", Now.AddDays(6)), Now.AddDays(6)), "save fresh before old expiry");
        File.WriteAllText(Path.Combine(root, "draft-stage-not-ours.tmp"), "untouched");
        session.Dispose(); session = Session(root); store = Drafts(session, root, Now.AddDays(8));
        Check(store.Drafts.Length == 1 && store.Saved.Report.ReportId == fresh.ReportId, "seven day expiry per draft");
        Check(File.ReadAllText(Path.Combine(root, "draft-stage-not-ours.tmp")) == "untouched", "foreign stage preserved");
        Check(store.TryDelete(fresh.ReportId, Now.AddDays(8)) && store.Saved == null, "delete final draft");
        session.Dispose(); session = Session(root); store = Drafts(session, root, Now.AddDays(8));
        Check(store.Drafts.Length == 0, "empty authority survives restart");
        Check(store.TrySave(ReportingDraft.Create(report, "post expiry", Now.AddDays(8)), Now.AddDays(8)), "new publication after expiry");
        File.WriteAllBytes(Latest(root), new byte[] { 1, 2, 3 });
        ReportingDraftStore corrupt;
        Check(!ReportingDraftStore.TryOpen(session, root, Now.AddDays(8), out corrupt), "corrupt latest fails closed");
        session.Dispose(); Check(!store.TrySave(draft, Now), "released lease cannot write");
    }
    private static void Bounds(string root)
    {
        ReportingReport report = Report(false);
        Check(ReportingDraft.Create(report, new string('界', 4000), Now).Description.Length == 4000, "UTF8 description supported up to 4000 chars");
        Invalid(delegate { ReportingDraft.Create(report, new string('x', 4001), Now); }, "4001 chars rejected");
        Invalid(delegate { ReportingDraft.Create(report, "", Now, "error", new string('界', 50000), "", null); }, "log UTF8 bound");
        Invalid(delegate { ReportingDraft.Create(report, "", Now, "unknown", "", "", null); }, "kind validation");
        Invalid(delegate { ReportingDraft.Create(report, "", Now, "error", "", "", "bad"); }, "error id validation");
        Invalid(delegate { ReportingDraft.Create(report, "", Now, "manual", "", "unrelated", null); }, "uncorrelated prior logs rejected");
        Invalid(delegate { ReportingDraft.Create(report, "", Now, "manual", "", "", new string('a', 32)); }, "manual error identity rejected");
        using (ReportingSessionStore session = Session(root))
        {
            ReportingDraftStore store = Drafts(session, root, Now);
            ReportingIncident prior = new ReportingIncident { SessionId = new string('a', 32), CheckpointId = new string('b', 32), Metadata = new string('p', ReportingMetadata.MaximumBytes), ObservedAtUtc = Now };
            for (int i = 0; i < 2; i++)
            {
                ReportingReport large = ReportingReport.Create(delegate { return new string('m', ReportingMetadata.MaximumBytes); }, prior, Now);
                Check(store.TrySave(ReportingDraft.Create(large, "large", Now, "unexpected_exit", new string('l', ReportingDraft.MaximumLogBytes), new string('q', ReportingDraft.MaximumLogBytes), null), Now), "large bounded draft " + i);
            }
            ReportingReport excess = ReportingReport.Create(delegate { return new string('m', ReportingMetadata.MaximumBytes); }, prior, Now);
            Check(!store.TrySave(ReportingDraft.Create(excess, "too large total", Now, "unexpected_exit", new string('l', ReportingDraft.MaximumLogBytes), new string('q', ReportingDraft.MaximumLogBytes), null), Now) && store.Drafts.Length == 2, "collection byte bound preserves existing entries");
            Check(store.TryDelete(store.Saved.Report.ReportId, Now), "delete at collection capacity");
        }
    }
    private static void Legacy(string root, int schema)
    {
        using (ReportingSessionStore session = Session(root))
        {
            ReportingReport report = Report(false);
            string path = Path.Combine(root, "draft-00000000000000000001.record");
            byte[] body;
            using (MemoryStream bytes = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8);
                writer.Write(schema); writer.Write(1L); writer.Write(Now.Ticks); if (schema == 2) writer.Write(true);
                writer.Write(report.ReportId); writer.Write(report.CaptureId); writer.Write(report.CreatedAtUtc.Ticks);
                writer.Write(report.CurrentMetadata); writer.Write(new string('d', 5000)); writer.Write(""); writer.Write(""); writer.Write(""); writer.Write(0L);
                writer.Flush(); body = bytes.ToArray();
            }
            using (FileStream file = File.Create(path))
            using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
            { file.Write(body, 0, body.Length); byte[] checksum = hash.ComputeHash(body); file.Write(checksum, 0, checksum.Length); }
            ReportingDraftStore store = Drafts(session, root, Now);
            Check(store.Saved.Report.IncludeMetadata && store.Saved.Description.Length == 5000 && store.Saved.Kind == "manual" && store.Saved.CurrentLogs == "", "legacy schema " + schema + " imported with diagnostics on and text intact");
            Check(store.TrySave(ReportingDraft.Create(Report(false), "new entry", Now.AddSeconds(1)), Now), "schema3 publication preserves legacy draft");
            store = Drafts(session, root, Now);
            Check(store.Drafts.Length == 2 && store.Drafts[1].Description.Length == 5000, "legacy roundtrip through collection");
        }
    }
    private static void CorruptCollection(string root)
    {
        using (ReportingSessionStore session = Session(root))
        {
            ReportingDraftStore store = Drafts(session, root, Now);
            Check(store.TrySave(ReportingDraft.Create(Report(false), "single entry", Now), Now), "corruption fixture saved");
            string file = Latest(root); byte[] original = File.ReadAllBytes(file);
            byte[] body = new byte[original.Length - 32]; Array.Copy(original, body, body.Length);
            byte[] invalidCount = (byte[])body.Clone(); Array.Copy(BitConverter.GetBytes(11), 0, invalidCount, 20, 4);
            WriteHashed(file, invalidCount);
            ReportingDraftStore rejected;
            Check(!ReportingDraftStore.TryOpen(session, root, Now, out rejected), "over-limit collection count rejected with valid checksum");
            int entryLength = body.Length - 24;
            byte[] duplicate = new byte[24 + entryLength * 2];
            Array.Copy(body, duplicate, body.Length); Array.Copy(BitConverter.GetBytes(2), 0, duplicate, 20, 4);
            Array.Copy(body, 24, duplicate, body.Length, entryLength); WriteHashed(file, duplicate);
            Check(!ReportingDraftStore.TryOpen(session, root, Now, out rejected), "duplicate identities rejected with valid checksum");
            File.WriteAllBytes(file, original);
            Check(ReportingDraftStore.TryOpen(session, root, Now, out rejected), "verified original remains recoverable");
            string foreign = Path.Combine(root, "draft-stage-" + new string('c', 32) + ".tmp");
            File.WriteAllText(foreign, "not a verified framework draft");
            Check(store.TrySave(ReportingDraft.Create(Report(false), "second", Now), Now), "save beside invalid matching stage filename");
            Check(File.ReadAllText(foreign) == "not a verified framework draft", "matching filename does not authorize deleting foreign data");
            Check(String.CompareOrdinal(store.Drafts[0].Report.ReportId, store.Drafts[1].Report.ReportId) < 0, "equal timestamps sorted deterministically by identity");
        }
    }
    private static void WriteHashed(string path, byte[] body)
    {
        using (FileStream file = File.Create(path))
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
        { file.Write(body, 0, body.Length); byte[] checksum = hash.ComputeHash(body); file.Write(checksum, 0, checksum.Length); }
    }
    private static string Latest(string root)
    { string[] files = Directory.GetFiles(root, "draft-*.record"); Array.Sort(files, StringComparer.Ordinal); return files[files.Length - 1]; }
    private static void Invalid(Action action, string why)
    { bool invalid = false; try { action(); } catch (ArgumentException) { invalid = true; } Check(invalid, why); }
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
    private static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
}
