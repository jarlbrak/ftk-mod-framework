using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    // Used only by the session lease's serialized worker. One generation publishes the whole list.
    internal sealed class ReportingDraftStore
    {
        internal const int MaximumDrafts = 10;
        internal const int MaximumCollectionBytes = 1536 * 1024;
        private readonly ReportingSessionStore owner;
        private readonly string root;
        private long generation;
        private ReportingDraft[] drafts = new ReportingDraft[0];
        internal ReportingDraft Saved { get { return drafts.Length == 0 ? null : drafts[0].Copy(); } }
        internal ReportingDraft[] Drafts { get { return Copy(drafts); } }
        private ReportingDraftStore(ReportingSessionStore owner, string root) { this.owner = owner; this.root = root; }
        internal static bool TryOpen(ReportingSessionStore owner, string root, DateTime now, out ReportingDraftStore result)
        {
            result = null;
            try
            {
                if (owner == null || !owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc || now.Ticks == 0) return false;
                ReportingDraftStore candidate = new ReportingDraftStore(owner, Path.GetFullPath(root));
                string latest = null;
                foreach (string file in candidate.Files())
                {
                    long number;
                    if (Name(Path.GetFileName(file), out number) && number > candidate.generation) { latest = file; candidate.generation = number; }
                }
                if (latest != null) candidate.drafts = Read(latest, candidate.generation);
                candidate.Cleanup();
                ReportingDraft[] live = Unexpired(candidate.drafts, now);
                if (live.Length != candidate.drafts.Length)
                {
                    candidate.drafts = live;
                    // Even when external files block cleanup, expired entries are never offered.
                    candidate.Publish(live, now, false);
                }
                result = candidate; return true;
            }
            catch { return false; }
        }
        internal bool TrySave(ReportingDraft draft, DateTime now)
        {
            if (!owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc || now.Ticks == 0 || draft == null) return false;
            try { draft = ReportingDraft.Create(draft.Report, draft.Description, draft.SavedAtUtc, draft.Kind, draft.CurrentLogs, draft.PreviousLogs, draft.ErrorId); }
            catch { return false; }
            if (now - draft.SavedAtUtc >= TimeSpan.FromDays(7)) return false;
            List<ReportingDraft> next = new List<ReportingDraft>(Unexpired(drafts, now));
            int index = next.FindIndex(delegate(ReportingDraft item) { return item.Report.ReportId == draft.Report.ReportId; });
            if (index < 0) { if (next.Count >= MaximumDrafts) return false; next.Add(draft); }
            else next[index] = draft;
            return Publish(Sort(next.ToArray()), now, true);
        }
        internal bool TryDelete(string expectedReportId, DateTime now)
        {
            if (!owner.OwnsRoot(root) || now.Kind != DateTimeKind.Utc || now.Ticks == 0 || !ReportingDraft.ValidId(expectedReportId)) return false;
            List<ReportingDraft> next = new List<ReportingDraft>(Unexpired(drafts, now));
            int index = next.FindIndex(delegate(ReportingDraft item) { return item.Report.ReportId == expectedReportId; });
            if (index < 0) return false;
            next.RemoveAt(index);
            return Publish(next.ToArray(), now, false);
        }
        private bool Publish(ReportingDraft[] nextDrafts, DateTime now, bool reserveDeletion)
        {
            string temp = null;
            bool created = false;
            try
            {
                long next = checked(generation + 1);
                byte[] body = Encode(nextDrafts, next, now);
                long used = 0;
                string[] files = Files();
                if (files.Length >= 128) return false;
                foreach (string file in files) used = checked(used + new FileInfo(file).Length);
                // Accepted saves reserve another full collection publication, so filling the
                // draft list cannot consume the staging space needed to delete an entry.
                if (used + (body.Length + 32L) * (reserveDeletion ? 2 : 1) > ReportingSessionStore.StoreLimit) return false;
                temp = Path.Combine(root, "draft-stage-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (SHA256 hash = SHA256.Create())
                { created = true; stream.Write(body, 0, body.Length); byte[] digest = hash.ComputeHash(body); stream.Write(digest, 0, digest.Length); stream.Flush(); }
                ReportingDraft[] verified = Read(temp, next);
                string published = Path.Combine(root, "draft-" + next.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + ".record");
                File.Move(temp, published);
                generation = next; drafts = verified;
                try { Cleanup(); } catch { }
                return true;
            }
            catch { return false; }
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
        private void Cleanup()
        {
            foreach (string file in Files())
            {
                string name = Path.GetFileName(file); long number;
                bool record = Name(name, out number);
                bool stage = name.Length == 48 && name.StartsWith("draft-stage-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) && ReportingDraft.ValidId(name.Substring(12, 32));
                if ((!record && !stage) || (record && number == generation)) continue;
                try { Read(file, record ? number : 0); File.Delete(file); }
                catch { /* Invalid content stays quota-counted. Latest empty generations prevent revival. */ }
            }
        }
        private static bool Name(string name, out long number)
        {
            number = 0;
            if (name.Length != 33 || !name.StartsWith("draft-", StringComparison.Ordinal) || !name.EndsWith(".record", StringComparison.Ordinal)) return false;
            for (int i = 6; i < 26; i++) if (name[i] < '0' || name[i] > '9') return false;
            return Int64.TryParse(name.Substring(6, 20), out number) && number > 0;
        }
        private static byte[] Encode(ReportingDraft[] values, long generation, DateTime now)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(3); writer.Write(generation); writer.Write(now.Ticks); writer.Write(values.Length);
                foreach (ReportingDraft draft in values)
                {
                    ReportingReport report = draft.Report;
                    writer.Write(draft.SavedAtUtc.Ticks);
                    writer.Write(report.ReportId); writer.Write(report.CaptureId); writer.Write(report.CreatedAtUtc.Ticks);
                    writer.Write(report.CurrentMetadata); writer.Write(draft.Description);
                    writer.Write(report.PreviousSessionId ?? ""); writer.Write(report.PreviousCheckpointId ?? ""); writer.Write(report.PreviousMetadata ?? "");
                    writer.Write(report.PreviousObservedAtUtc.HasValue ? report.PreviousObservedAtUtc.Value.Ticks : 0L);
                    writer.Write(report.IncludeMetadata); writer.Write(draft.Kind); writer.Write(draft.CurrentLogs); writer.Write(draft.PreviousLogs); writer.Write(draft.ErrorId ?? "");
                    if (stream.Length > MaximumCollectionBytes) throw new InvalidDataException();
                }
                writer.Flush(); return stream.ToArray();
            }
        }
        private static ReportingDraft[] Read(string path, long expected)
        {
            FileInfo file = new FileInfo(path);
            if (file.Length < 52 || file.Length > MaximumCollectionBytes + 32 || (file.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
            byte[] bytes = File.ReadAllBytes(path);
            using (SHA256 hash = SHA256.Create())
            { byte[] digest = hash.ComputeHash(bytes, 0, bytes.Length - 32); for (int i = 0; i < 32; i++) if (digest[i] != bytes[bytes.Length - 32 + i]) throw new InvalidDataException(); }
            using (MemoryStream stream = new MemoryStream(bytes, 0, bytes.Length - 32))
            {
                BinaryReader reader = new BinaryReader(stream, new UTF8Encoding(false, true));
                int schema = reader.ReadInt32();
                if (schema < 1 || schema > 3) throw new InvalidDataException();
                long generation = reader.ReadInt64(); if (generation < 1 || (expected != 0 && generation != expected)) throw new InvalidDataException();
                DateTime recordedAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                if (recordedAt.Ticks == 0) throw new InvalidDataException();
                int count = schema == 3 ? reader.ReadInt32() : schema == 2 ? reader.ReadByte() : 1;
                if (count < 0 || count > (schema == 3 ? MaximumDrafts : 1)) throw new InvalidDataException();
                List<ReportingDraft> values = new List<ReportingDraft>();
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    DateTime saved = schema == 3 ? new DateTime(reader.ReadInt64(), DateTimeKind.Utc) : recordedAt;
                    string reportId = reader.ReadString(), captureId = reader.ReadString(); DateTime created = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                    string metadata = reader.ReadString(), description = reader.ReadString();
                    string session = reader.ReadString(), checkpoint = reader.ReadString(), previousMetadata = reader.ReadString(); long observed = reader.ReadInt64();
                    if (!ids.Add(reportId) || (session.Length == 0 && (checkpoint.Length != 0 || previousMetadata.Length != 0 || observed != 0))) throw new InvalidDataException();
                    ReportingIncident previous = session.Length == 0 ? null : new ReportingIncident { SessionId = session, CheckpointId = checkpoint.Length == 0 ? null : checkpoint,
                        Metadata = previousMetadata, ObservedAtUtc = new DateTime(observed, DateTimeKind.Utc) };
                    ReportingReport report = ReportingReport.Restore(reportId, captureId, created, metadata, previous);
                    if (schema < 3) values.Add(ReportingDraft.ImportLegacy(report, description, saved));
                    else
                    {
                        byte include = reader.ReadByte(); if (include > 1) throw new InvalidDataException();
                        report.IncludeMetadata = include == 1;
                        string kind = reader.ReadString(), currentLogs = reader.ReadString(), previousLogs = reader.ReadString(), errorId = reader.ReadString();
                        values.Add(ReportingDraft.RestoreSaved(report, description, saved, kind, currentLogs, previousLogs, errorId.Length == 0 ? null : errorId));
                    }
                }
                if (stream.Position != stream.Length) throw new InvalidDataException();
                return Sort(values.ToArray());
            }
        }
        private static ReportingDraft[] Unexpired(ReportingDraft[] values, DateTime now)
        {
            List<ReportingDraft> result = new List<ReportingDraft>();
            foreach (ReportingDraft draft in values) if (now - draft.SavedAtUtc < TimeSpan.FromDays(7)) result.Add(draft);
            return result.ToArray();
        }
        private static ReportingDraft[] Sort(ReportingDraft[] values)
        {
            Array.Sort(values, delegate(ReportingDraft a, ReportingDraft b) {
                int time = b.SavedAtUtc.CompareTo(a.SavedAtUtc);
                return time != 0 ? time : String.CompareOrdinal(a.Report.ReportId, b.Report.ReportId);
            });
            return values;
        }
        private static ReportingDraft[] Copy(ReportingDraft[] values)
        {
            ReportingDraft[] result = new ReportingDraft[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i].Copy();
            return result;
        }
    }
}
