using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.Marketplace
{
    // A helper result the framework cannot read. Distinct from other IO failures so the panel can
    // tell the player to update or repair the framework and helper together instead of retrying.
    internal sealed class MarketplaceProtocolException : IOException
    {
        internal MarketplaceProtocolException(string message) : base(message) { }
    }

    // Pure protocol/path checks shared with the Unity-free tests.
    internal static class MarketplaceProtocol
    {
        internal const int MaxResultBytes = 2 * 1024 * 1024;

        internal static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '-' && c != '_' && c != '.') return false;
            return value != "." && value != "..";
        }

        internal static MarketplaceResult ReadResult(string path, string operationId)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists) throw new IOException("The marketplace helper returned no result. Use Install / Repair in the launcher.");
            if (file.Length > MaxResultBytes) throw new IOException("Marketplace result exceeds the 2 MiB limit.");
            MarketplaceResult result = JsonContentParser.Deserialize<MarketplaceResult>(File.ReadAllText(path));
            if (result == null || result.SchemaVersion != 1) throw new MarketplaceProtocolException("Unsupported marketplace protocol. Update or repair the framework and helper together.");
            if (operationId != null && result.OperationId != operationId) throw new IOException("Marketplace result belongs to a different operation.");
            return result;
        }

        internal static void ValidateSnapshot(string stateRoot, ManagedSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!IsIdentifier(snapshot.GenerationId)) throw new IOException("Invalid managed generation identifier.");
            string expected = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(stateRoot, "generations"), snapshot.GenerationId), "content"));
            if (snapshot.ContentRoot == null || !string.Equals(expected, Path.GetFullPath(snapshot.ContentRoot), StringComparison.Ordinal))
                throw new IOException("Managed content root is outside its generation.");
            if (snapshot.Packages == null) throw new IOException("Managed generation has no package selection.");
            foreach (PackageDescriptor package in snapshot.Packages)
                if (package == null || !IsIdentifier(package.PackageId) || string.IsNullOrEmpty(package.ModGuid))
                    throw new IOException("Invalid managed package identity.");
        }

        internal static MarketplaceResult ReadState(string stateRoot)
        {
            string path = Path.Combine(stateRoot, "state.json");
            if (!File.Exists(path)) return new MarketplaceResult { SchemaVersion = 1, Ok = true };
            MarketplaceStateRecord state = ReadBounded<MarketplaceStateRecord>(path);
            if (state == null || state.SchemaVersion != 1) throw new IOException("Unsupported managed state schema.");
            MarketplaceResult result = new MarketplaceResult { SchemaVersion = 1, Ok = true,
                Active = ReadGeneration(stateRoot, state.Current), PreviousAvailable = !string.IsNullOrEmpty(state.Previous) };
            try { result.Pending = ReadGeneration(stateRoot, state.Pending); }
            catch (Exception e) { result.Message = "Pending generation is unreadable; active generation retained. " + e.Message; }
            return result;
        }

        internal static ManagedSnapshot ReadGeneration(string root, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!IsIdentifier(id)) throw new IOException("Invalid managed generation pointer.");
            string directory = Path.Combine(Path.Combine(root, "generations"), id);
            MarketplaceGenerationLock record = ReadBounded<MarketplaceGenerationLock>(Path.Combine(directory, "lock.json"));
            if (record == null || record.SchemaVersion != 1) throw new IOException("Unsupported generation lock schema.");
            ManagedSnapshot snapshot = new ManagedSnapshot { GenerationId = id, ContentRoot = Path.GetFullPath(Path.Combine(directory, "content")),
                Packages = record.Packages, Files = record.Files ?? new System.Collections.Generic.List<MarketplaceGenerationFile>() };
            ValidateSnapshot(root, snapshot);
            ValidateGenerationFiles(snapshot);
            return snapshot;
        }

        internal static ManagedSnapshot ReadVerifiedGeneration(string root, ManagedSnapshot returned)
        {
            if (returned == null) return null;
            ManagedSnapshot snapshot = ReadGeneration(root, returned.GenerationId);
            snapshot.FilesVerified = true;
            return snapshot;
        }

        private static void ValidateGenerationFiles(ManagedSnapshot snapshot)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string root = snapshot.ContentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (MarketplaceGenerationFile file in snapshot.Files)
            {
                if (file == null || string.IsNullOrEmpty(file.Path) || Path.IsPathRooted(file.Path) ||
                    file.Path.IndexOf('\\') >= 0 || file.Path.IndexOf(':') >= 0 || file.Size < 0 ||
                    string.IsNullOrEmpty(file.Sha256) || file.Sha256.Length != 64 || !seen.Add(file.Path))
                    throw new IOException("Invalid managed generation file record.");
                foreach (string part in file.Path.Split('/'))
                    if (part.Length == 0 || part == "." || part == "..")
                        throw new IOException("Invalid managed generation file path.");
                foreach (char c in file.Sha256)
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                        throw new IOException("Invalid managed generation file hash.");
                string full = Path.GetFullPath(Path.Combine(snapshot.ContentRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(root, StringComparison.Ordinal))
                    throw new IOException("Managed generation file escapes its content root.");
            }
        }

        private static T ReadBounded<T>(string path)
        {
            if (new FileInfo(path).Length > MaxResultBytes) throw new IOException("Managed record exceeds the 2 MiB limit.");
            return JsonContentParser.Deserialize<T>(File.ReadAllText(path));
        }

        internal static bool IsScreenshotPath(string stateRoot, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string cache = Path.GetFullPath(Path.Combine(stateRoot, "cache/screenshots")) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            string extension = Path.GetExtension(full).ToLowerInvariant();
            return full.StartsWith(cache, StringComparison.Ordinal) && (extension == ".png" || extension == ".jpg" || extension == ".jpeg") &&
                File.Exists(full) && new FileInfo(full).Length <= 2 * 1024 * 1024;
        }

        internal static void VerifyHelper(string helperPath)
        {
            if (!File.Exists(helperPath)) throw new IOException("Marketplace helper is missing. Use Install / Repair in the launcher; installed mods remain available.");
            string manifestPath = Path.Combine(Path.GetDirectoryName(helperPath), "helper.json");
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 8192)
                throw new IOException("Marketplace helper verification record is missing or invalid. Use Install / Repair.");
            HelperManifest manifest = JsonContentParser.Deserialize<HelperManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.SchemaVersion != 1 || manifest.ProtocolVersion != 1 || string.IsNullOrEmpty(manifest.Sha256))
                throw new IOException("Marketplace helper protocol is unsupported. Update or repair the framework and helper together.");
            string hash;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(helperPath))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Marketplace helper checksum failed. Use Install / Repair before marketplace actions.");
        }
    }
}
