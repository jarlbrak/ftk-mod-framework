using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FTKModFramework.Core.Reporting
{
    // Two independently verified slots keep one complete snapshot if an update is interrupted.
    // The session store owns the installation lease for the whole lifetime of this writer.
    internal sealed class ReportingDiagnosticsStore
    {
        private readonly ReportingSessionStore owner;
        private readonly string root, sessionId;
        private long generation;
        internal string PreviousSessionId { get; private set; }
        internal string Previous { get; private set; }
        internal ReportingDiagnosticsStore(ReportingSessionStore owner, string root)
        {
            if (owner == null || !owner.OwnsRoot(root)) throw new IOException("Diagnostics need the reporting session lease.");
            this.owner = owner; this.root = root; sessionId = owner.SessionId;
            ReportingIncident pending = owner.Pending;
            PreviousSessionId = pending == null ? null : pending.SessionId;
            Previous = PreviousSessionId == null ? "" : Read(PreviousSessionId);
            foreach (string path in Directory.GetFiles(root))
            {
                string name = Path.GetFileName(path);
                if (!Regex.IsMatch(name, @"^diagnostics-[a-f0-9]{32}-[01]\.(record|tmp)$")) continue;
                RejectLink(path);
                string id = name.Substring(12, 32);
                if (id != sessionId && id != PreviousSessionId)
                {
                    // Matching names alone do not establish ownership. Invalid or unrelated
                    // files remain untouched and still count against the shared storage quota.
                    try { long ignored; ReadFile(path, id, false, out ignored); File.Delete(path); }
                    catch { }
                }
            }
        }
        internal void Write(string sanitizedText)
        {
            if (!owner.OwnsRoot(root)) throw new IOException("Diagnostics session ended.");
            byte[] text = Encoding.UTF8.GetBytes(sanitizedText);
            if (text.Length > ReportingDiagnosticsBuffer.ByteLimit) throw new InvalidDataException();
            long next = checked(generation + 1);
            string path = Path.Combine(root, "diagnostics-" + sessionId + "-" + (next % 2) + ".record");
            string stage = Path.ChangeExtension(path, ".tmp");
            RejectLink(path); RejectLink(stage);
            if (File.Exists(stage)) File.Delete(stage);
            byte[] body;
            using (MemoryStream memory = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(memory, Encoding.UTF8);
                writer.Write(1); writer.Write(sessionId); writer.Write(next); writer.Write(DateTime.UtcNow.Ticks);
                writer.Write(text.Length); writer.Write(text); writer.Flush(); body = memory.ToArray();
            }
            string[] files = Directory.GetFiles(root);
            long used = 0;
            foreach (string file in files) { RejectLink(file); used = checked(used + new FileInfo(file).Length); }
            if (files.Length > 124 || used + body.Length + 32 > ReportingSessionStore.StoreLimit)
                throw new IOException("Diagnostics capacity unavailable.");
            using (FileStream output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (SHA256 hash = SHA256.Create())
            { output.Write(body, 0, body.Length); byte[] digest = hash.ComputeHash(body); output.Write(digest, 0, digest.Length); output.Flush(); }
            long verified;
            if (ReadFile(stage, sessionId, true, out verified) != sanitizedText || verified != next) throw new InvalidDataException();
            // The other slot remains intact until this new snapshot has been fully published.
            if (File.Exists(path)) File.Delete(path);
            File.Move(stage, path); generation = next;
        }
        private string Read(string id)
        {
            string result = ""; long newest = 0;
            for (int slot = 0; slot < 2; slot++)
            {
                try
                {
                    long number;
                    string text = ReadFile(Path.Combine(root, "diagnostics-" + id + "-" + slot + ".record"), id, true, out number);
                    if (number > newest) { newest = number; result = text; }
                }
                catch { /* A torn or expired snapshot is unavailable, never another session's data. */ }
            }
            return result;
        }
        private static string ReadFile(string path, string expectedId, bool requireRecent, out long number)
        {
            number = 0; RejectLink(path);
            if (new FileInfo(path).Length > ReportingDiagnosticsBuffer.ByteLimit + 128) throw new InvalidDataException();
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 70) throw new InvalidDataException();
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes, 0, bytes.Length - 32);
                for (int i = 0; i < 32; i++) if (bytes[bytes.Length - 32 + i] != digest[i]) throw new InvalidDataException();
            }
            using (MemoryStream input = new MemoryStream(bytes, 0, bytes.Length - 32))
            {
                BinaryReader reader = new BinaryReader(input, new UTF8Encoding(false, true));
                if (reader.ReadInt32() != 1 || reader.ReadString() != expectedId) throw new InvalidDataException();
                number = reader.ReadInt64();
                DateTime at = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                if (number < 1 || at.Ticks == 0 || at > DateTime.UtcNow.AddMinutes(5) ||
                    (requireRecent && DateTime.UtcNow - at >= TimeSpan.FromDays(7))) throw new InvalidDataException();
                int length = reader.ReadInt32();
                if (length < 0 || length > ReportingDiagnosticsBuffer.ByteLimit || input.Length - input.Position != length) throw new InvalidDataException();
                return new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
            }
        }
        private static void RejectLink(string path)
        {
            try { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Diagnostics cannot follow a link."); }
            catch (FileNotFoundException) { }
        }
    }
}
