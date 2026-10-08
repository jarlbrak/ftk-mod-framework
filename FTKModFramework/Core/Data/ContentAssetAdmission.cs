using System;
using System.Collections.Generic;
using System.IO;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.Data
{
    // The generation lock belongs only to its selected package roots. Manual assets
    // remain independent, but a selected GUID cannot use a different root to bypass it.
    internal sealed class ContentAssetAdmission
    {
        private readonly ManagedSnapshot managed;
        private readonly string managedRoot;
        private readonly Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketplaceGenerationFile> files = new Dictionary<string, MarketplaceGenerationFile>(StringComparer.Ordinal);

        internal ContentAssetAdmission(ManagedSnapshot snapshot)
        {
            managed = snapshot;
            if (snapshot == null) return;
            managedRoot = Canonical(snapshot.ContentRoot);
            foreach (PackageDescriptor package in snapshot.Packages)
            {
                string root = Canonical(Path.Combine(managedRoot, package.PackageId));
                if (!string.Equals(Path.GetDirectoryName(root), managedRoot, StringComparison.Ordinal))
                    throw new ArgumentException("Managed package root escapes its generation.");
                roots.Add(package.ModGuid, root);
                string prefix = package.PackageId + "/";
                foreach (MarketplaceGenerationFile file in snapshot.Files)
                    if (file != null && file.Path != null && file.Path.StartsWith(prefix, StringComparison.Ordinal))
                        files[package.ModGuid + "\n" + file.Path.Substring(prefix.Length)] = file;
            }
        }

        internal string Register(string modGuid, string packageRoot, string relativePath)
        {
            string root = Canonical(packageRoot);
            string expectedRoot;
            if (!roots.TryGetValue(modGuid, out expectedRoot))
            {
                if (managedRoot != null && (root == managedRoot || root.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                    throw new ArgumentException("Asset source is not a selected managed package.");
                return PackageModelPaths.Register(modGuid, root, relativePath);
            }
            if (!string.Equals(root, expectedRoot, StringComparison.Ordinal))
                throw new ArgumentException("Managed package identity does not match its asset source root.");
            if (!managed.FilesVerified)
                throw new ArgumentException("Managed asset generation has not been verified.");
            MarketplaceGenerationFile file;
            if (!files.TryGetValue(modGuid + "\n" + relativePath, out file))
                throw new ArgumentException("Managed asset is absent from the verified generation lock.");
            return PackageModelPaths.RegisterVerified(modGuid, root, relativePath, file.Sha256, file.Size);
        }

        private static string Canonical(string root)
        {
            if (string.IsNullOrEmpty(root)) throw new ArgumentException("Missing package root.");
            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
