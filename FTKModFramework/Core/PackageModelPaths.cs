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
        }
        private static readonly Dictionary<string, Asset> Paths =
            new Dictionary<string, Asset>(StringComparer.Ordinal);

        internal static string Register(string modGuid, string packageRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(modGuid)) throw new ArgumentException("Missing package identity.");
            if (string.IsNullOrEmpty(packageRoot)) throw new ArgumentException("Missing package root.");
            ValidateRelativePath(relativePath);
            string root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("Model path escapes its package.");
            RejectLinks(root, candidate);
            if (!File.Exists(candidate)) throw new FileNotFoundException("Package model asset is missing.", relativePath);

            string identity;
            string hash;
            using (SHA256 sha = SHA256.Create())
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
            Paths[identity] = new Asset { Root = root, Path = candidate, Hash = hash };
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
            // Managed package generations are immutable, but also reject a replaced symlink
            // when the same API is used by a manually installed data mod.
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
