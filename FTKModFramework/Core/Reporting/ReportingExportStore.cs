using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingExportRequest
    {
        internal readonly string ReportId, CaptureId, ReportText, DiagnosticsText;
        internal readonly long Revision;
        private ReportingExportRequest(string reportId, long revision, string captureId, string reportText, string diagnosticsText)
        { ReportId = reportId; Revision = revision; CaptureId = captureId; ReportText = reportText; DiagnosticsText = diagnosticsText; }
        internal static ReportingExportRequest Create(string reportId, long revision, string captureId, string reportText, string diagnosticsText)
        {
            if (!ReportingDraft.ValidId(reportId) || revision < 1 || String.IsNullOrEmpty(reportText) || reportText.Length > 65536 ||
                Encoding.UTF8.GetByteCount(reportText) > 65536 ||
                (diagnosticsText == null ? captureId != null : !ReportingDraft.ValidId(captureId) || diagnosticsText.Length > 262144 || Encoding.UTF8.GetByteCount(diagnosticsText) > 262144))
                throw new ArgumentException("Invalid reviewed export.");
            return new ReportingExportRequest(reportId, revision, captureId, reportText, diagnosticsText);
        }
    }
    internal sealed class ReportingExportArtifact
    {
        internal readonly string ReportId, CaptureId, ReportPath, DiagnosticsPath, ManifestPath;
        internal readonly long Revision;
        internal readonly DateTime CreatedAtUtc;
        internal ReportingExportArtifact(string root, string stem, string reportId, long revision, string captureId, DateTime created)
        {
            ReportId = reportId; Revision = revision; CaptureId = captureId; CreatedAtUtc = created;
            ReportPath = Path.Combine(root, stem + "-report.txt"); DiagnosticsPath = captureId == null ? null : Path.Combine(root, stem + "-diagnostics.txt");
            ManifestPath = Path.Combine(root, stem + ".manifest");
        }
    }
    // The manifest is the publication marker. Unpublished/unknown files stay quota-counted.
    internal sealed class ReportingExportStore
    {
        private readonly ReportingSessionStore owner;
        private readonly string root;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal ReportingExportStore(ReportingSessionStore owner, string root) { this.owner = owner; this.root = Path.GetFullPath(root); }
        internal void PruneExpired(DateTime now)
        { if (now.Kind == DateTimeKind.Utc) try { Cleanup(now); } catch { } }
        internal ReportingExportArtifact TryPublish(ReportingExportRequest request, DateTime now)
        {
            string stageReport = null, stageDiagnostics = null, stageManifest = null;
            try
            {
                if (request == null || now.Kind != DateTimeKind.Utc || now.Ticks == 0 || !owner.OwnsRoot(root)) return null;
                request = ReportingExportRequest.Create(request.ReportId, request.Revision, request.CaptureId, request.ReportText, request.DiagnosticsText);
                Cleanup(now);
                string stem = Stem(request.ReportId, request.Revision);
                ReportingExportArtifact artifact = new ReportingExportArtifact(root, stem, request.ReportId, request.Revision, request.CaptureId, now);
                byte[] report = Utf8.GetBytes(request.ReportText), diagnostics = request.DiagnosticsText == null ? null : Utf8.GetBytes(request.DiagnosticsText);
                if (File.Exists(artifact.ManifestPath))
                {
                    ReportingExportArtifact prior = Read(artifact.ManifestPath);
                    if (prior.CaptureId != request.CaptureId || !Same(File.ReadAllBytes(prior.ReportPath), report) ||
                        (diagnostics != null && !Same(File.ReadAllBytes(prior.DiagnosticsPath), diagnostics))) return null;
                    return prior;
                }
                byte[] manifest = Manifest(request, now, report, diagnostics);
                long used = 0; int published = 0;
                foreach (string file in Files())
                {
                    used = checked(used + new FileInfo(file).Length);
                    string id; long revision;
                    if (ParseName(Path.GetFileName(file), out id, out revision)) published++;
                }
                if (published >= 16 || used + report.Length + (diagnostics == null ? 0 : diagnostics.Length) + manifest.Length > ReportingSessionStore.StoreLimit) return null;
                string staging = Path.Combine(root, "export-stage-" + Guid.NewGuid().ToString("N"));
                string candidate = staging + "-report.tmp"; Write(candidate, report); stageReport = candidate;
                if (diagnostics != null) { candidate = staging + "-diagnostics.tmp"; Write(candidate, diagnostics); stageDiagnostics = candidate; }
                candidate = staging + "-manifest.tmp"; Write(candidate, manifest); stageManifest = candidate;
                File.Move(stageReport, artifact.ReportPath); stageReport = null;
                if (diagnostics != null) { File.Move(stageDiagnostics, artifact.DiagnosticsPath); stageDiagnostics = null; }
                // Validate all final payloads against the staged publication marker before commit.
                Read(stageManifest, stem);
                File.Move(stageManifest, artifact.ManifestPath); stageManifest = null;
                return artifact;
            }
            catch { return null; }
            finally
            {
                RemoveStage(stageReport); RemoveStage(stageDiagnostics); RemoveStage(stageManifest);
            }
        }
        private static void RemoveStage(string path) { if (path != null) try { File.Delete(path); } catch { } }
        private string[] Files()
        {
            if (owner == null || !owner.OwnsRoot(root)) throw new IOException();
            string[] files = Directory.GetFiles(root);
            if (files.Length > 128 || Directory.GetDirectories(root).Length != 0) throw new IOException();
            foreach (string file in files) if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException();
            return files;
        }
        private void Cleanup(DateTime now)
        {
            foreach (string path in Files())
            {
                string id; long revision;
                if (!ParseName(Path.GetFileName(path), out id, out revision)) continue;
                try
                {
                    ReportingExportArtifact artifact = Read(path);
                    if (now - artifact.CreatedAtUtc < TimeSpan.FromDays(7)) continue;
                    File.Delete(artifact.ReportPath);
                    if (artifact.DiagnosticsPath != null) File.Delete(artifact.DiagnosticsPath);
                    File.Delete(artifact.ManifestPath);
                }
                catch { /* Invalid/unknown files are never cleanup authority. */ }
            }
        }
        private static string Stem(string reportId, long revision) { return "export-" + reportId + "-" + revision.ToString("D20", CultureInfo.InvariantCulture); }
        private static bool ParseName(string name, out string id, out long revision)
        {
            id = null; revision = 0;
            if (name.Length != 69 || !name.StartsWith("export-", StringComparison.Ordinal) || !name.EndsWith(".manifest", StringComparison.Ordinal) || name[39] != '-') return false;
            id = name.Substring(7, 32);
            for (int i = 40; i < 60; i++) if (name[i] < '0' || name[i] > '9') return false;
            return ReportingDraft.ValidId(id) && Int64.TryParse(name.Substring(40, 20), out revision) && revision > 0;
        }
        private static byte[] Manifest(ReportingExportRequest request, DateTime now, byte[] report, byte[] diagnostics)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Utf8);
                writer.Write(1); writer.Write(request.ReportId); writer.Write(request.Revision); writer.Write(request.CaptureId ?? ""); writer.Write(now.Ticks);
                writer.Write(report.Length); writer.Write(Hash(report)); writer.Write(diagnostics == null ? -1 : diagnostics.Length);
                if (diagnostics != null) writer.Write(Hash(diagnostics)); writer.Flush();
                byte[] body = stream.ToArray(); writer.Write(Hash(body)); writer.Flush(); return stream.ToArray();
            }
        }
        private ReportingExportArtifact Read(string manifest, string expectedStem = null)
        {
            CheckFile(manifest, 1024);
            byte[] bytes = File.ReadAllBytes(manifest);
            if (bytes.Length < 32) throw new InvalidDataException();
            byte[] body = new byte[bytes.Length - 32]; Array.Copy(bytes, body, body.Length);
            byte[] digest = Hash(body); for (int i = 0; i < 32; i++) if (bytes[body.Length + i] != digest[i]) throw new InvalidDataException();
            using (MemoryStream stream = new MemoryStream(body))
            {
                BinaryReader reader = new BinaryReader(stream, Utf8);
                if (reader.ReadInt32() != 1) throw new InvalidDataException();
                string id = reader.ReadString(); long revision = reader.ReadInt64(); string capture = reader.ReadString();
                DateTime created = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                if (!ReportingDraft.ValidId(id) || revision < 1 || (capture.Length != 0 && !ReportingDraft.ValidId(capture)) || created.Ticks == 0) throw new InvalidDataException();
                string stem = Stem(id, revision);
                if (expectedStem != null ? expectedStem != stem : Path.GetFileName(manifest) != stem + ".manifest") throw new InvalidDataException();
                int reportLength = reader.ReadInt32(); byte[] reportHash = reader.ReadBytes(32);
                int diagnosticsLength = reader.ReadInt32(); byte[] diagnosticsHash = diagnosticsLength < 0 ? null : reader.ReadBytes(32);
                if (reportLength < 1 || reportLength > 65536 || diagnosticsLength < -1 || diagnosticsLength > 262144 ||
                    (diagnosticsLength == -1) != (capture.Length == 0) || stream.Position != stream.Length) throw new InvalidDataException();
                ReportingExportArtifact result = new ReportingExportArtifact(root, stem, id, revision, capture.Length == 0 ? null : capture, created);
                VerifyPayload(result.ReportPath, reportLength, reportHash);
                if (result.DiagnosticsPath != null) VerifyPayload(result.DiagnosticsPath, diagnosticsLength, diagnosticsHash);
                return result;
            }
        }
        private static void CheckFile(string path, int maximum)
        {
            FileInfo info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length > maximum) throw new InvalidDataException();
        }
        private static void VerifyPayload(string path, int length, byte[] hash)
        {
            CheckFile(path, length); byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != length || !Same(Hash(bytes), hash)) throw new InvalidDataException();
        }
        private static bool Same(byte[] a, byte[] b)
        { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static byte[] Hash(byte[] value) { using (SHA256 hash = SHA256.Create()) return hash.ComputeHash(value); }
        private static void Write(string path, byte[] bytes)
        {
            bool created = false;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { created = true; stream.Write(bytes, 0, bytes.Length); stream.Flush(); }
                if (!Same(File.ReadAllBytes(path), bytes)) throw new InvalidDataException();
            }
            catch { if (created) RemoveStage(path); throw; }
        }
    }
}
