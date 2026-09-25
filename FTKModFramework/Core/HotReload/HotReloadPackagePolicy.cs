using System;
using System.Collections.Generic;
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
            return null;
        }

        internal static void Require(ManagedSnapshot selection)
        {
            string reason = InvalidReason(selection);
            if (reason != null) throw new InvalidOperationException(reason);
        }
    }
}
