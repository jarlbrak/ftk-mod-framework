using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.Marketplace
{
    internal sealed class PackageDescriptor { internal string ModGuid; internal bool Enabled; }
    internal sealed class ManagedSnapshot { internal List<PackageDescriptor> Packages = new List<PackageDescriptor>(); }
}

namespace FTKModFramework.Core.HotReload
{
    internal static class Program
    {
        private static int checks;
        private static void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception(message); }
        private static void Reject(FTKModFramework.Core.Marketplace.ManagedSnapshot selection, string message)
        {
            bool rejected = false;
            try { HotReloadPackagePolicy.Require(selection); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        private static FTKModFramework.Core.Marketplace.PackageDescriptor Package(string guid)
        { return new FTKModFramework.Core.Marketplace.PackageDescriptor { ModGuid = guid, Enabled = true }; }
        private static void Main()
        {
            var selection = new FTKModFramework.Core.Marketplace.ManagedSnapshot();
            selection.Packages.Add(Package("com.ftkmf.thief"));
            selection.Packages.Add(Package("com.example.shared-tools"));
            HotReloadPackagePolicy.Require(selection);
            Check(HotReloadPackagePolicy.InvalidReason(selection) == null,
                "Multiple managed packages and their dependency closure remain eligible.");
            Reject(new FTKModFramework.Core.Marketplace.ManagedSnapshot { Packages = null },
                "A missing package list is rejected.");
            var duplicate = new FTKModFramework.Core.Marketplace.ManagedSnapshot();
            duplicate.Packages.Add(Package("com.ftkmf.thief")); duplicate.Packages.Add(Package("com.ftkmf.thief"));
            Reject(duplicate, "Duplicate managed identities are rejected.");
            var disabled = new FTKModFramework.Core.Marketplace.ManagedSnapshot();
            disabled.Packages.Add(new FTKModFramework.Core.Marketplace.PackageDescriptor { ModGuid = "com.ftkmf.thief", Enabled = false });
            HotReloadPackagePolicy.Require(disabled);
            Check(HotReloadPackagePolicy.InvalidReason(disabled) == null,
                "A managed generation can retain an installed but disabled package.");
            Console.WriteLine("PASS: " + checks + " hot-reload package policy checks.");
        }
    }
}
