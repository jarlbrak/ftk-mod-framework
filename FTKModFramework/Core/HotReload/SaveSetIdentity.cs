using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FTKModFramework.Core.HotReload
{
    // Generation IDs identify preparation operations, not save compatibility. Encode each
    // value with its length so delimiters in settings cannot create ambiguous identities.
    internal static class SaveSetIdentity
    {
        internal static string Compute(string gameHash, string frameworkVersion, IDictionary<string, string> enabledPackages, IDictionary<string, string> settings)
        {
            RequireHash(gameHash);
            if (string.IsNullOrEmpty(frameworkVersion)) throw new ArgumentException("Missing framework version.");
            using (MemoryStream buffer = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(buffer, Encoding.UTF8))
            {
                writer.Write("ftkmf-save-set-v1");
                writer.Write(gameHash.ToLowerInvariant()); writer.Write(frameworkVersion);
                WriteMap(writer, enabledPackages, true); WriteMap(writer, settings, false);
                writer.Flush();
                using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        private static void WriteMap(BinaryWriter writer, IDictionary<string, string> values, bool hashes)
        {
            List<string> keys = new List<string>(values.Keys); keys.Sort(StringComparer.Ordinal);
            writer.Write(keys.Count);
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key) || values[key] == null) throw new ArgumentException("Missing save identity input.");
                if (hashes) RequireHash(values[key]);
                writer.Write(key); writer.Write(hashes ? values[key].ToLowerInvariant() : values[key]);
            }
        }
        internal static void RequireHash(string value)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("Expected SHA-256 identity.");
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) throw new ArgumentException("Invalid SHA-256 identity.");
        }
        internal static string DirectoryFor(string persistentRoot, string fingerprint)
        {
            RequireHash(fingerprint);
            return Path.Combine(Path.Combine(persistentRoot, "ftkmf-saves"), fingerprint.ToLowerInvariant());
        }
        internal static void ValidateOwnedDirectory(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(path));
            RejectReparsePoint(directory.FullName);
            if (directory.Parent == null) throw new IOException("Missing owned directory parent.");
            RejectReparsePoint(directory.Parent.FullName);
        }
        internal static void CreateOwnedDirectory(string path)
        {
            ValidateOwnedDirectory(path);
            Directory.CreateDirectory(path);
            ValidateOwnedDirectory(path);
        }
        private static void RejectReparsePoint(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Framework-owned path is a symbolic link or reparse point: " + path);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        internal static bool IsSavePath(string directory, string path, bool mustExist)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path) ||
                    !string.Equals(Path.GetExtension(path), ".run", StringComparison.OrdinalIgnoreCase)) return false;
                string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(path);
                StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!string.Equals(Path.GetDirectoryName(full), root, comparison)) return false;
                // OS-managed aliases above the owned parent remain outside this check.
                ValidateOwnedDirectory(root);
                RejectReparsePoint(full);
                return !mustExist || File.Exists(full);
            }
            catch { return false; }
        }
        internal static void WritePin(string stateRoot, string fingerprint, string generation)
        {
            RequireHash(fingerprint);
            if (generation == null || generation.Length != 32) throw new ArgumentException("Expected generation ID.");
            foreach (char c in generation) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) throw new ArgumentException("Invalid generation ID.");
            string directory = Path.Combine(stateRoot, "save-pins"); CreateOwnedDirectory(directory);
            string path = Path.Combine(directory, fingerprint.ToLowerInvariant() + ".json");
            RejectReparsePoint(path);
            // A namespace may return through another generation containing identical bytes.
            // Preserve the original immutable generation pin rather than racing GC to replace it.
            string body = "{\"schemaVersion\":1,\"generation\":\"" + generation + "\"}";
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 256) throw new IOException("Invalid existing save pin.");
                string existing = File.ReadAllText(path);
                if (!System.Text.RegularExpressions.Regex.IsMatch(existing, "^\\s*\\{\\s*\"schemaVersion\"\\s*:\\s*1\\s*,\\s*\"generation\"\\s*:\\s*\"[a-f0-9]{32}\"\\s*\\}\\s*$"))
                    throw new IOException("Invalid existing save pin.");
                return;
            }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(body); stream.Write(bytes, 0, bytes.Length); stream.Flush();
                }
                ValidateOwnedDirectory(directory);
                RejectReparsePoint(path);
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
