using System;
using System.Collections.Generic;
using System.IO;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.HotReload
{
    // Managed data packages share the same database and resource transaction. Package identity
    // and dependency closure are validated by the marketplace before this policy runs.
    internal static class HotReloadPackagePolicy
    {
        internal static string InvalidReason(ManagedSnapshot selection)
        {
            if (selection == null) return null;
            if (selection.Packages == null) return "Managed package selection is malformed.";
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PackageDescriptor package in selection.Packages)
            {
                if (package == null || string.IsNullOrEmpty(package.ModGuid))
                    return "Managed package selection contains an invalid package.";
                if (!guids.Add(package.ModGuid)) return "Managed package selection repeats a package identity.";
            }
            if (selection.Packages.Count == 0) return null;
            if (!selection.FilesVerified || selection.Files == null || string.IsNullOrEmpty(selection.ContentRoot))
                return "Managed content capabilities have not been verified; restart to activate.";
            try
            {
                string root = Path.GetFullPath(selection.ContentRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (MarketplaceGenerationFile record in selection.Files)
                {
                    if (record == null || string.IsNullOrEmpty(record.Path)) return "Invalid managed content file.";
                    if (Path.GetExtension(record.Path) != ".json" || Path.GetFileName(record.Path) == "manifest.json") continue;
                    string path = Path.GetFullPath(Path.Combine(root, record.Path));
                    if (!path.StartsWith(root, StringComparison.Ordinal)) return "Managed content file escapes its root.";
                    ContentFile file = JsonContentParser.Deserialize<ContentFile>(File.ReadAllText(path));
                    if (file == null || file.Entries == null) return "Cannot inspect managed content capabilities.";
                    foreach (ContentEntry entry in file.Entries)
                        if (entry == null || string.Equals(entry.Kind, "race", StringComparison.OrdinalIgnoreCase) || entry.RaceBindings != null)
                            return "Custom races require next-launch activation.";
                }
            }
            catch (Exception) { return "Cannot inspect managed content capabilities; restart to activate."; }
            return null;
        }

        internal static void Require(ManagedSnapshot selection)
        {
            string reason = InvalidReason(selection);
            if (reason != null) throw new InvalidOperationException(reason);
        }
    }
}
