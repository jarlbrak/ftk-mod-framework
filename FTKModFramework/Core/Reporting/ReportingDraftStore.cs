using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    // Used only by the session lease's serialized worker. This never acquires a competing lease.
    internal sealed class ReportingDraftStore
    {
        private const int MaximumRecord = ReportingMetadata.MaximumBytes * 2 + 16448;
        private readonly ReportingSessionStore owner;
        private readonly string root;
        private long generation;
        private bool failed;
        private ReportingDraft saved;
        internal ReportingDraft Saved { get { return failed || saved == null ? null : saved.Copy(); } }
        private ReportingDraftStore(ReportingSessionStore owner, string root) { this.owner = owner; this.root = root; }
        internal static bool TryOpen(ReportingSessionStore owner, string root, DateTime now, out ReportingDraftStore result)
        {
            result = null;
            try
            {
                if (owner == null || !owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc) return false;
                ReportingDraftStore candidate = new ReportingDraftStore(owner, Path.GetFullPath(root));
                string latest = null;
                foreach (string file in candidate.Files())
                {
                    long number;
                    if (Name(Path.GetFileName(file), out number) && number > candidate.generation) { latest = file; candidate.generation = number; }
                }
                if (latest != null)
                {
                    candidate.saved = Read(latest, candidate.generation);
                    if (candidate.saved != null && now - candidate.saved.SavedAtUtc >= TimeSpan.FromDays(7)) candidate.saved = null;
                }
                candidate.Cleanup(now);
                result = candidate; return true;
            }
            catch { return false; }
        }
        internal bool TrySave(ReportingDraft draft, DateTime now)
        {
            if (failed || !owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc || draft == null) return false;
            try { draft = ReportingDraft.Create(draft.Report, draft.Description, draft.SavedAtUtc); }
            catch { return false; }
            if (saved != null && now - saved.SavedAtUtc < TimeSpan.FromDays(7) && saved.Report.ReportId != draft.Report.ReportId) return false;
            return Publish(draft, now);
        }
        internal bool TryDelete(string expectedReportId, DateTime now)
        {
            if (failed || !owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc || now.Ticks == 0 ||
                !ReportingDraft.ValidId(expectedReportId) || saved == null || saved.Report.ReportId != expectedReportId) return false;
            return Publish(null, now);
        }
        private bool Publish(ReportingDraft draft, DateTime now)
        {
            string temp = null;
            bool created = false;
            try
            {
                long next = checked(generation + 1);
                byte[] body = Encode(draft, next, now);
                long used = 0;
                foreach (string file in Files()) used = checked(used + new FileInfo(file).Length);
                if (used + body.Length + 32 > ReportingSessionStore.StoreLimit) return false;
                temp = Path.Combine(root, "draft-stage-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (SHA256 hash = SHA256.Create())
                { created = true; stream.Write(body, 0, body.Length); byte[] digest = hash.ComputeHash(body); stream.Write(digest, 0, digest.Length); stream.Flush(); }
                ReportingDraft verified = Read(temp, next);
                string published = Path.Combine(root, "draft-" + next.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + ".record");
                File.Move(temp, published);
                generation = next; saved = verified;
                // Publication is already verified and committed. Cleanup failure cannot undo it.
                try { Cleanup(now); } catch { }
                return true;
            }
            catch { failed = true; return false; }
            finally { if (created && temp != null) try { File.Delete(temp); } catch { } }
        }
        private string[] Files()
        {
            if (!owner.OwnsRoot(root)) throw new IOException();
            string[] files = Directory.GetFiles(root);
            if (files.Length > 128 || Directory.GetDirectories(root).Length != 0) throw new IOException();
            foreach (string file in files) if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException();
            return files;
        }
        private void Cleanup(DateTime now)
        {
            string expiredLatest = null;
            bool cleanupFailed = false;
            foreach (string file in Files())
            {
                string name = Path.GetFileName(file); long number;
                bool record = Name(name, out number);
                bool stage = name.Length == 48 && name.StartsWith("draft-stage-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) && ReportingDraft.ValidId(name.Substring(12, 32));
                if (!record && !stage) continue;
                try
                {
                    ReportingDraft value = Read(file, record ? number : 0);
                    if (stage || number != generation) File.Delete(file);
                    else if (value != null && now - value.SavedAtUtc >= TimeSpan.FromDays(7)) expiredLatest = file;
                    // Latest tombstones retain publication authority until a newer save supersedes them.
                }
                catch { cleanupFailed = true; /* Unknown or invalid content stays quota-counted, never erased. */ }
            }
            // Keep the latest authority if an older record could not be removed; never revive it.
            if (expiredLatest != null && !cleanupFailed) try { File.Delete(expiredLatest); } catch { }
        }
        private static bool Name(string name, out long number)
        {
            number = 0;
            if (name.Length != 33 || !name.StartsWith("draft-", StringComparison.Ordinal) || !name.EndsWith(".record", StringComparison.Ordinal)) return false;
            for (int i = 6; i < 26; i++) if (name[i] < '0' || name[i] > '9') return false;
            return Int64.TryParse(name.Substring(6, 20), out number) && number > 0;
        }
        private static byte[] Encode(ReportingDraft draft, long generation, DateTime now)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(2); writer.Write(generation); writer.Write(draft == null ? now.Ticks : draft.SavedAtUtc.Ticks);
                writer.Write(draft != null);
                if (draft == null) { writer.Flush(); return stream.ToArray(); }
                ReportingReport report = draft.Report;
                writer.Write(report.ReportId); writer.Write(report.CaptureId); writer.Write(report.CreatedAtUtc.Ticks);
                writer.Write(report.CurrentMetadata); writer.Write(draft.Description);
                writer.Write(report.PreviousSessionId ?? ""); writer.Write(report.PreviousCheckpointId ?? ""); writer.Write(report.PreviousMetadata ?? "");
                writer.Write(report.PreviousObservedAtUtc.HasValue ? report.PreviousObservedAtUtc.Value.Ticks : 0L); writer.Flush();
                if (stream.Length > MaximumRecord) throw new InvalidDataException();
                return stream.ToArray();
            }
        }
        private static ReportingDraft Read(string path, long expected)
        {
            FileInfo file = new FileInfo(path);
            if (file.Length < 52 || file.Length > MaximumRecord + 32 || (file.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
            byte[] bytes = File.ReadAllBytes(path);
            using (SHA256 hash = SHA256.Create())
            { byte[] digest = hash.ComputeHash(bytes, 0, bytes.Length - 32); for (int i = 0; i < 32; i++) if (digest[i] != bytes[bytes.Length - 32 + i]) throw new InvalidDataException(); }
            using (MemoryStream stream = new MemoryStream(bytes, 0, bytes.Length - 32))
            {
                BinaryReader reader = new BinaryReader(stream, new UTF8Encoding(false, true));
                int schema = reader.ReadInt32();
                if (schema != 1 && schema != 2) throw new InvalidDataException();
                long generation = reader.ReadInt64(); if (generation < 1 || (expected != 0 && generation != expected)) throw new InvalidDataException();
                DateTime saved = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                if (saved.Ticks == 0) throw new InvalidDataException();
                if (schema == 2)
                {
                    byte kind = reader.ReadByte();
                    if (kind > 1) throw new InvalidDataException();
                    if (kind == 0)
                    {
                        if (stream.Position != stream.Length) throw new InvalidDataException();
                        return null;
                    }
                }
                string reportId = reader.ReadString(), captureId = reader.ReadString(); DateTime created = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                string metadata = reader.ReadString(), description = reader.ReadString();
                string session = reader.ReadString(), checkpoint = reader.ReadString(), previousMetadata = reader.ReadString(); long observed = reader.ReadInt64();
                if (stream.Position != stream.Length || (session.Length == 0 && (checkpoint.Length != 0 || previousMetadata.Length != 0 || observed != 0))) throw new InvalidDataException();
                ReportingIncident previous = session.Length == 0 ? null : new ReportingIncident { SessionId = session, CheckpointId = checkpoint.Length == 0 ? null : checkpoint,
                    Metadata = previousMetadata, ObservedAtUtc = new DateTime(observed, DateTimeKind.Utc) };
                return ReportingDraft.Create(ReportingReport.Restore(reportId, captureId, created, metadata, previous), description, saved);
            }
        }
    }
}
