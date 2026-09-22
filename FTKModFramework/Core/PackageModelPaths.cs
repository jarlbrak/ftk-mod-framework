using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FTKModFramework.Core
{
    // Package assets use opaque, content-addressed names in the existing renderer APIs.
    // Keeping the package root out of that API prevents global model-folder collisions.
    internal static class PackageModelPaths
    {
        private const string Prefix = "package-model:";
        private sealed class Asset
        {
            internal string Root;
            internal string Path;
            internal string Hash;
            internal long Size;
            internal bool VerifiedGeneration;
        }
        private static Dictionary<string, Asset> Paths =
            new Dictionary<string, Asset>(StringComparer.Ordinal);

        internal static int ReloadPathCount { get { return Paths.Count; } }
        internal static Action SuspendForReload()
        {
            Dictionary<string, Asset> old = Paths;
            Paths = new Dictionary<string, Asset>(StringComparer.Ordinal);
            return delegate { Paths = old; };
        }

        internal static string Register(string modGuid, string packageRoot, string relativePath)
        {
            return RegisterInternal(modGuid, packageRoot, relativePath, null, -1);
        }

        // The marketplace helper has just hashed the complete immutable generation under the
        // process lease. Preserve that verified content identity instead of hashing the same bytes
        // again on the shipped Mono runtime during the title-screen transaction.
        internal static string RegisterVerified(string modGuid, string packageRoot, string relativePath, string expectedHash, long expectedSize)
        {
            if (string.IsNullOrEmpty(expectedHash) || expectedHash.Length != 64 || expectedSize < 0)
                throw new ArgumentException("Invalid verified package asset record.");
            foreach (char c in expectedHash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid verified package asset hash.");
            return RegisterInternal(modGuid, packageRoot, relativePath, expectedHash, expectedSize);
        }

        private static string RegisterInternal(string modGuid, string packageRoot, string relativePath, string expectedHash, long expectedSize)
        {
            if (string.IsNullOrEmpty(modGuid)) throw new ArgumentException("Missing package identity.");
            if (string.IsNullOrEmpty(packageRoot)) throw new ArgumentException("Missing package root.");
            ValidateRelativePath(relativePath);
            string root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("Model path escapes its package.");
            if (expectedHash == null) RejectLinks(root, candidate);
            if (!File.Exists(candidate)) throw new FileNotFoundException("Package model asset is missing.", relativePath);

            string identity;
            string hash;
            long size = new FileInfo(candidate).Length;
            if (expectedHash != null)
            {
                if (size != expectedSize) throw new IOException("Verified package asset size changed.");
                hash = expectedHash;
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] key = Encoding.UTF8.GetBytes(modGuid + "\n" + relativePath + "\n" + hash);
                    identity = Prefix + Hex(sha.ComputeHash(key));
                }
            }
            else using (SHA256 sha = SHA256.Create())
            {
                byte[] assetHash;
                using (FileStream stream = File.OpenRead(candidate)) assetHash = sha.ComputeHash(stream);
                hash = Hex(assetHash);
                byte[] key = Encoding.UTF8.GetBytes(modGuid + "\n" + relativePath + "\n" + hash);
                identity = Prefix + Hex(sha.ComputeHash(key));
            }
            Asset existing;
            if (Paths.TryGetValue(identity, out existing) && !string.Equals(existing.Path, candidate, StringComparison.Ordinal))
                throw new InvalidOperationException("Package asset identity already belongs to another root.");
            Paths[identity] = new Asset { Root = root, Path = candidate, Hash = hash, Size = size, VerifiedGeneration = expectedHash != null };
            return identity;
        }

        internal static bool IsPackagePath(string name)
        {
            return name != null && name.StartsWith(Prefix, StringComparison.Ordinal);
        }

        internal static string Resolve(string identity)
        {
            Asset asset;
            if (identity == null || !Paths.TryGetValue(identity, out asset))
                throw new ArgumentException("Unregistered package model identity.");
            if (new FileInfo(asset.Path).Length != asset.Size) throw new IOException("Package model size changed after registration.");
            if (asset.VerifiedGeneration) return asset.Path;
            // Manual package paths do not have a helper-validated immutable generation contract.
            RejectLinks(asset.Root, asset.Path);
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(asset.Path))
                if (Hex(sha.ComputeHash(stream)) != asset.Hash)
                    throw new IOException("Package model changed after registration.");
            return asset.Path;
        }

        private static void ValidateRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("assets/", StringComparison.Ordinal) ||
                path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || Path.IsPathRooted(path))
                throw new ArgumentException("Package models must use a relative assets/ path.");
            string[] parts = path.Split('/');
            foreach (string part in parts)
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("Invalid package model path component.");
            string extension = Path.GetExtension(path);
            if (extension != ".glb" && extension != ".png")
                throw new ArgumentException("Package models support lowercase GLB and PNG assets only.");
        }

        private static void RejectLinks(string root, string candidate)
        {
            string current = candidate;
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Package model paths cannot contain symbolic links.");
                if (string.Equals(current, root, StringComparison.Ordinal)) break;
                current = Path.GetDirectoryName(current);
            }
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
