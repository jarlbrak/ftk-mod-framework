using System;
using System.Collections.Generic;
using System.IO;

namespace FTKModFramework.Core.Marketplace
{
    internal sealed class PackageDescriptor { internal string ModGuid; internal bool Enabled; }
    internal sealed class MarketplaceGenerationFile { internal string Path; }
    internal sealed class ManagedSnapshot {
        internal List<PackageDescriptor> Packages = new List<PackageDescriptor>();
        internal string ContentRoot = Path.GetTempPath();
        internal bool FilesVerified = true;
        internal List<MarketplaceGenerationFile> Files = new List<MarketplaceGenerationFile>();
    }
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
            string root = Path.Combine(Path.GetTempPath(), "ftk-race-policy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                selection.ContentRoot = root;
                selection.Files.Add(new Marketplace.MarketplaceGenerationFile { Path = "content.json" });
                string content = Path.Combine(root, "content.json");
                File.WriteAllText(content, "{\"entries\":[{\"kind\":\"race\",\"id\":\"possum\"}]}");
                Check(HotReloadPackagePolicy.InvalidReason(selection) != null,
                    "Active race capability disables title activation so startup uses the ordinary loader.");
                Reject(selection, "Pending race capability rejects activation before coordinator mutation.");
                selection.Packages[0].Enabled = false;
                Reject(selection, "Disabled race declarations cannot hide unsnapshotted capability.");
                File.WriteAllText(content, "{\"entries\":[{\"kind\":\"class\",\"raceBindings\":[]}]}");
                Reject(selection, "Race bindings reject activation regardless of kind or package identity.");
                File.WriteAllText(content, "{\"entries\":[{\"kind\":\"item\",\"id\":\"tools\"}]}");
                Check(HotReloadPackagePolicy.InvalidReason(selection) == null,
                    "Ordinary capabilities remain eligible under the same arbitrary package identities.");
                File.Delete(content);
                Reject(selection, "Unreadable content fails closed before mutation.");
                selection.FilesVerified = false;
                Reject(selection, "Unverified selection cannot enter capability admission.");
            }
            finally { Directory.Delete(root, true); }
            Console.WriteLine("PASS: " + checks + " hot-reload package policy checks.");
        }
    }
}

namespace FTKModFramework.Core.Data { internal sealed class ItemModifierEntry { } }
