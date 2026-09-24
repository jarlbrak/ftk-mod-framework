using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    // Detached metadata must already be allowlisted and sanitized by the collector.
    internal sealed class ReportingIncident
    {
        internal string SessionId, CheckpointId, Metadata, Phase, ReportId;
        internal DateTime StartedAtUtc, ObservedAtUtc;
        internal DateTime? ShutdownObservedAtUtc;
        internal bool ShutdownObserved { get { return ShutdownObservedAtUtc.HasValue; } }
        internal ReportingIncident Copy() { return (ReportingIncident)MemberwiseClone(); }
    }

    internal sealed class ReportingSessionStore : IDisposable
    {
        internal const int RecordLimit = 256 * 1024;
        internal const long StoreLimit = 5 * 1024 * 1024;
        private readonly string root;
        private readonly ReportingSessionLease lease;
        private ReportingIncident current, pending;
        private long generation;
        private bool disposed, failed;
        internal bool OwnsRoot(string candidate)
        { return !disposed && !failed && lease.Acquired && String.Equals(root, Path.GetFullPath(candidate), StringComparison.Ordinal); }
        internal string SessionId { get { return current.SessionId; } }
        internal ReportingIncident Pending { get { return disposed || failed || pending == null ? null : pending.Copy(); } }

        private ReportingSessionStore(string root, ReportingSessionLease lease) { this.root = root; this.lease = lease; }
        internal static bool TryOpen(string root, DateTime utcNow, out ReportingSessionStore store)
        {
            store = null;
            ReportingSessionLease owner = new ReportingSessionLease();
            try
            {
                RequireUtc(utcNow);
                root = Path.GetFullPath(root);
                RejectLinks(root);
                owner.Acquire(root);
                ReportingSessionStore candidate = new ReportingSessionStore(root, owner);
                candidate.Load();
                candidate.Expire(utcNow);
                // A linked draft owns the incident evidence after durable disposition.
                if (candidate.pending != null && candidate.pending.ReportId != null) candidate.pending = null;
                if (candidate.current != null && utcNow - candidate.current.StartedAtUtc >= TimeSpan.FromDays(7)) candidate.current = null;
                if (candidate.pending == null && candidate.current != null && !candidate.current.ShutdownObserved)
                    candidate.pending = candidate.current;
                candidate.current = new ReportingIncident { SessionId = Id(), StartedAtUtc = utcNow,
                    ObservedAtUtc = utcNow, Phase = "startup", Metadata = "", CheckpointId = null };
                candidate.Publish();
                store = candidate;
                return true;
            }
            catch { owner.Dispose(); return false; }
        }
        internal bool TryCheckpoint(string sanitizedMetadata, string phase, DateTime utcNow)
        {
            return Change(delegate {
                RequireUtc(utcNow);
                if (current.ShutdownObserved) throw new InvalidOperationException();
                if (sanitizedMetadata == null || phase == null || !ValidPhase(phase) || Encoding.UTF8.GetByteCount(sanitizedMetadata) > RecordLimit)
                    throw new InvalidDataException();
                current.Metadata = sanitizedMetadata; current.Phase = phase;
                current.CheckpointId = Id(); current.ObservedAtUtc = utcNow;
            }, utcNow);
        }
        internal bool TryRecordShutdown(DateTime utcNow)
        { return Change(delegate { current.ShutdownObservedAtUtc = utcNow; }, utcNow); }
        internal bool TryDismissPending(DateTime utcNow)
        { return Change(delegate { pending = null; }, utcNow); }
        // Caller must persist the local draft before linking; this does not save a draft.
        internal bool TryLinkPendingReport(string reportId, DateTime utcNow)
        {
            return Change(delegate {
                if (pending == null || !ValidId(reportId) || (pending.ReportId != null && pending.ReportId != reportId))
                    throw new InvalidOperationException();
                pending.ReportId = reportId;
            }, utcNow);
        }
        private bool Change(Action action, DateTime utcNow)
        {
            if (disposed || failed) return false;
            ReportingIncident oldCurrent = current.Copy(), oldPending = pending == null ? null : pending.Copy();
            try { RequireUtc(utcNow); Expire(utcNow); action(); Publish(); return true; }
            catch { current = oldCurrent; pending = oldPending; failed = true; return false; }
        }
        private void Expire(DateTime now)
        {
            if (pending != null && now - pending.StartedAtUtc >= TimeSpan.FromDays(7)) pending = null;
        }
        private static bool ValidPhase(string phase) { return phase == "startup" || phase == "title" || phase == "session_or_transition"; }
        private static string Id() { return Guid.NewGuid().ToString("N"); }
        private static bool ValidId(string value)
        {
            if (value == null || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++) if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }
        private static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc || value.Ticks == 0) throw new InvalidDataException(); }
        private static void RejectLinks(string path)
        {
            for (string cursor = path; !String.IsNullOrEmpty(cursor); cursor = Path.GetDirectoryName(cursor))
                try
                {
                    if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reporting storage cannot traverse a link.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
        }
        private static byte[] Encode(ReportingIncident value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(value.SessionId); writer.Write(value.CheckpointId ?? ""); writer.Write(value.StartedAtUtc.Ticks);
                writer.Write(value.ObservedAtUtc.Ticks); writer.Write(value.ShutdownObservedAtUtc.HasValue ? value.ShutdownObservedAtUtc.Value.Ticks : 0L);
                writer.Write(value.Phase); writer.Write(value.Metadata); writer.Write(value.ReportId ?? ""); writer.Flush();
                if (stream.Length > RecordLimit) throw new InvalidDataException();
                return stream.ToArray();
            }
        }
        private static ReportingIncident Decode(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BinaryReader reader = new BinaryReader(stream, new UTF8Encoding(false, true));
                ReportingIncident value = new ReportingIncident();
                value.SessionId = reader.ReadString(); value.CheckpointId = Null(reader.ReadString());
                value.StartedAtUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                value.ObservedAtUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc); long shutdown = reader.ReadInt64(); value.ShutdownObservedAtUtc = shutdown == 0 ? (DateTime?)null : new DateTime(shutdown, DateTimeKind.Utc);
                value.Phase = reader.ReadString(); value.Metadata = reader.ReadString(); value.ReportId = Null(reader.ReadString());
                if (stream.Position != stream.Length || !ValidId(value.SessionId) ||
                    (value.CheckpointId != null && !ValidId(value.CheckpointId)) || (value.ReportId != null && !ValidId(value.ReportId)) ||
                    !ValidPhase(value.Phase) || (value.CheckpointId == null && value.Metadata.Length != 0)) throw new InvalidDataException();
                return value;
            }
        }
        private static string Null(string value) { return value.Length == 0 ? null : value; }
        private string[] Files()
        {
            // net35 has no lazy directory enumeration; bound all subsequent file work.
            string[] paths = Directory.GetFiles(root);
            if (paths.Length > 128 || Directory.GetDirectories(root).Length != 0) throw new IOException("Reporting directory exceeds bounds.");
            return paths;
        }
        private void Load()
        {
            string latest = null;
            foreach (string path in Files())
            {
                RejectLinks(path);
                string name = Path.GetFileName(path);
                long number;
                if (!StateName(name, out number)) continue;
                if (number > generation) { generation = number; latest = path; }
            }
            if (latest == null) return;
            ReadState(latest, generation, out current, out pending);
        }
        private static bool StateName(string name, out long number)
        {
            number = 0;
            if (name.Length != 33 || !name.StartsWith("state-", StringComparison.Ordinal) || !name.EndsWith(".record", StringComparison.Ordinal)) return false;
            for (int i = 6; i < 26; i++) if (name[i] < '0' || name[i] > '9') return false;
            return Int64.TryParse(name.Substring(6, 20), out number) && number > 0;
        }
        private static bool StageName(string name)
        { return name.Length == 42 && name.StartsWith("stage-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) && ValidId(name.Substring(6, 32)); }
        private static void ReadState(string latest, long expectedGeneration, out ReportingIncident current, out ReportingIncident pending)
        {
            FileInfo info = new FileInfo(latest);
            if (info.Length > RecordLimit * 2 + 64 || info.Length < 48) throw new InvalidDataException();
            byte[] bytes = File.ReadAllBytes(latest);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes, 0, bytes.Length - 32);
                for (int i = 0; i < 32; i++) if (bytes[bytes.Length - 32 + i] != digest[i]) throw new InvalidDataException();
            }
            using (MemoryStream stream = new MemoryStream(bytes, 0, bytes.Length - 32))
            {
                BinaryReader reader = new BinaryReader(stream);
                if (reader.ReadInt32() != 1) throw new InvalidDataException();
                long storedGeneration = reader.ReadInt64();
                if (storedGeneration < 1 || (expectedGeneration != 0 && storedGeneration != expectedGeneration)) throw new InvalidDataException();
                current = ReadRecord(reader); pending = ReadRecord(reader);
                if (current == null || stream.Position != stream.Length || (pending != null && pending.SessionId == current.SessionId)) throw new InvalidDataException();
            }
        }
        private static ReportingIncident ReadRecord(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0) return null;
            if (length < 0 || length > RecordLimit) throw new InvalidDataException();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new InvalidDataException();
            return Decode(bytes);
        }
        private void Publish()
        {
            byte[] body;
            long next = checked(generation + 1);
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(1); writer.Write(next);
                foreach (ReportingIncident record in new ReportingIncident[] { current, pending })
                { byte[] bytes = record == null ? new byte[0] : Encode(record); writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Flush(); body = stream.ToArray();
            }
            long used = 0;
            foreach (string path in Files()) { RejectLinks(path); used = checked(used + new FileInfo(path).Length); }
            if (Directory.GetDirectories(root).Length != 0 || used + body.Length + 32 > StoreLimit) throw new IOException("Reporting capacity unavailable.");
            string temp = Path.Combine(root, "stage-" + Id() + ".tmp");
            string published = Path.Combine(root, "state-" + next.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + ".record");
            try
            {
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (SHA256 hash = SHA256.Create())
                { file.Write(body, 0, body.Length); byte[] digest = hash.ComputeHash(body); file.Write(digest, 0, digest.Length); file.Flush(); }
                // Reopen before publication so truncation is never promoted.
                byte[] staged = File.ReadAllBytes(temp);
                if (staged.Length != body.Length + 32) throw new InvalidDataException();
                for (int i = 0; i < body.Length; i++) if (staged[i] != body[i]) throw new InvalidDataException();
                using (SHA256 hash = SHA256.Create())
                { byte[] digest = hash.ComputeHash(body); for (int i = 0; i < 32; i++) if (staged[body.Length + i] != digest[i]) throw new InvalidDataException(); }
                File.Move(temp, published); generation = next;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            foreach (string path in Files())
            {
                string name = Path.GetFileName(path);
                long stateGeneration;
                bool stateName = StateName(name, out stateGeneration);
                if (path == published || (!stateName && !StageName(name))) continue;
                try
                {
                    ReportingIncident ignoredCurrent, ignoredPending;
                    ReadState(path, stateName ? stateGeneration : 0, out ignoredCurrent, out ignoredPending);
                    File.Delete(path);
                }
                catch { /* Unknown or invalid files remain untouched and count against quota. */ }
            }
        }
        public void Dispose() { if (disposed) return; disposed = true; lease.Dispose(); }
    }
}
