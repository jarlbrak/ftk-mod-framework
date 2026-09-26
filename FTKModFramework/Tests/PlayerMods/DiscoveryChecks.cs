// Game-free checks for ModDiscovery.DiscoverAll merge order and duplicate-GUID handling, and for the
// managed-versus-manual registration path selection ContentLoader delegates to ModRegistry.RegisterDiscovered.
// Registration into game tables is not covered here.
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using FTKModFramework;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

internal static class DiscoveryChecks
{
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: discovery / " + name);
    }

    private static string WriteMod(string root, string folder, string guid)
    {
        string path = Path.Combine(root, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "manifest.json"), JsonConvert.SerializeObject(new ModManifest { ModGuid = guid, Name = guid, Version = "1.0.0", FrameworkVersion = Plugin.Version }));
        return path;
    }

    private static string Guids(List<DiscoveredMod> mods)
    {
        List<string> guids = new List<string>();
        foreach (DiscoveredMod mod in mods) guids.Add(mod.Manifest.ModGuid);
        return string.Join(",", guids.ToArray());
    }

    private static int CountContaining(ValidationReport report, string text)
    {
        int count = 0;
        foreach (string error in report.Errors) if (error.Contains(text)) count++;
        return count;
    }

    private static void ThunderstorePackageDiscovery(string fixture)
    {
        string root = Path.Combine(fixture, "discovery-thunderstore");
        string contentPackage = Path.Combine(root, "JarlBrak-Paladin");
        Directory.CreateDirectory(contentPackage);
        File.WriteAllText(Path.Combine(contentPackage, "manifest.json"),
            "{\"name\":\"Paladin\",\"version_number\":\"1.4.0\",\"website_url\":\"https://github.com/jarlbrak/ftk-mod-framework\",\"description\":\"FTK content\",\"dependencies\":[]}");
        string content = Path.Combine(contentPackage, "FTKMFContent");
        WriteMod(contentPackage, "FTKMFContent", "thirdparty.thunderstore");

        string frameworkPackage = Path.Combine(root, "JarlBrak-FTKModFramework");
        Directory.CreateDirectory(frameworkPackage);
        File.WriteAllText(Path.Combine(frameworkPackage, "manifest.json"),
            "{\"name\":\"FTKModFramework\",\"version_number\":\"1.4.0\",\"website_url\":\"https://github.com/jarlbrak/ftk-mod-framework\",\"description\":\"Framework\",\"dependencies\":[]}");
        File.WriteAllText(Path.Combine(frameworkPackage, "FTKModFramework.dll"), "fixture");

        ValidationReport report = new ValidationReport();
        List<DiscoveredMod> mods = ModDiscovery.Discover(root, report);
        Check(mods.Count == 1 && mods[0].Manifest.ModGuid == "thirdparty.thunderstore" &&
            mods[0].Manifest.FolderPath == content, "Thunderstore metadata delegates to the reserved FTK content folder");
        Check(report.Errors.Count == 0, "Thunderstore metadata and framework-only packages are ignored as content");
    }

    internal static void Run(string fixture)
    {
        ThunderstorePackageDiscovery(fixture);

        string manual = Path.Combine(fixture, "discovery-manual");
        string managed = Path.Combine(fixture, "discovery-managed");
        // Folder names deliberately sort opposite to their GUIDs, and the roots are interleaved, so the
        // resulting order can only come from the (modGuid, folder) sort and not from enumeration order.
        WriteMod(manual, "zz-first", "a.first");
        WriteMod(manual, "mm-third", "c.third");
        WriteMod(managed, "aa-second", "b.second");
        WriteMod(managed, "nn-fourth", "d.fourth");

        ValidationReport report = new ValidationReport();
        List<DiscoveredMod> mods = ModDiscovery.DiscoverAll(manual, managed, report);
        Check(Guids(mods) == "a.first,b.second,c.third,d.fourth" && report.Errors.Count == 0, "manual and managed roots merge into one ordinal GUID order");

        report = new ValidationReport();
        mods = ModDiscovery.DiscoverAll(manual, null, report);
        Check(Guids(mods) == "a.first,c.third", "no active generation discovers the manual root alone");

        report = new ValidationReport();
        mods = ModDiscovery.DiscoverAll(manual, manual, report);
        Check(Guids(mods) == "a.first,c.third" && report.Errors.Count == 0, "a managed root equal to the manual root is not walked twice");

        string duplicate = WriteMod(managed, "zz-first-copy", "a.first");
        report = new ValidationReport();
        mods = ModDiscovery.DiscoverAll(manual, managed, report);
        Check(Guids(mods) == "a.first,b.second,c.third,d.fourth" && CountContaining(report, "Duplicate mod GUID 'a.first'") == 1, "duplicate GUID across roots keeps one copy and reports the conflict");
        string original = Path.Combine(manual, "zz-first");
        string expectedKept = string.CompareOrdinal(original, duplicate) < 0 ? original : duplicate;
        Check(mods[0].Manifest.FolderPath == expectedKept, "duplicate GUID resolution keeps the ordinal-first folder path");
        Directory.Delete(duplicate, true);

        WriteMod(managed, "bundled-clash", Plugin.Guid);
        report = new ValidationReport();
        mods = ModDiscovery.DiscoverAll(manual, managed, report);
        Check(Guids(mods) == "a.first,b.second,c.third,d.fourth" && CountContaining(report, "conflicts with FTK Mod Framework") == 1, "a mod claiming the framework GUID is skipped with an error");

        RegistrationSelection(fixture);
    }

    private static void RegistrationSelection(string fixture)
    {
        string contentRoot = Path.Combine(fixture, "generation-content");
        ManagedSnapshot snapshot = new ManagedSnapshot { GenerationId = "55555555555555555555555555555555", ContentRoot = contentRoot };
        PackageDescriptor package = new PackageDescriptor { PackageId = "pkg", Enabled = false, Description = "Lock description", Author = "Lock author" };
        string insideManaged = Path.Combine(contentRoot, "pkg");

        // The lock says disabled while a stale PlayerPrefs entry says enabled: the managed path must win.
        UnityEngine.PlayerPrefs.SetInt(ModRegistry.PrefKeyPrefix + "select.managed", 1);
        ModEntry entry = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.managed", Name = "Managed", Version = "1.0.0", FolderPath = insideManaged, Description = "Manifest description" }, snapshot, package);
        Check(entry.IsManaged && entry.PackageId == "pkg" && !entry.Enabled && entry.Description == "Lock description", "folder inside the active generation listed in the lock registers as managed");

        UnityEngine.PlayerPrefs.SetInt(ModRegistry.PrefKeyPrefix + "select.manual", 1);
        entry = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.manual", Name = "Manual", Version = "1.0.0", FolderPath = Path.Combine(fixture, "plugins", "pkg"), Description = "Manifest description" }, snapshot, package);
        Check(!entry.IsManaged && entry.PackageId == null && entry.Enabled && entry.Description == "Manifest description", "manual copy outside the generation registers through the manual path even when the lock lists its GUID");

        entry = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.unlisted", Name = "Unlisted", Version = "1.0.0", FolderPath = insideManaged }, snapshot, null);
        Check(!entry.IsManaged && entry.Enabled, "generation folder whose GUID the lock does not list registers as manual");

        entry = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.nogeneration", Name = "No generation", Version = "1.0.0", FolderPath = insideManaged }, null, package);
        Check(!entry.IsManaged && entry.Enabled, "no active generation registers as manual");

        entry = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.sibling", Name = "Sibling", Version = "1.0.0", FolderPath = contentRoot + "-other" + Path.DirectorySeparatorChar + "pkg" }, snapshot, package);
        Check(!entry.IsManaged, "sibling directory sharing the generation root prefix is not managed");

        ModEntry again = ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.managed", Name = "Renamed", Version = "2.0.0", FolderPath = Path.Combine(fixture, "plugins", "pkg") }, snapshot, package);
        Check(object.ReferenceEquals(again, ModRegistry.RegisterDiscovered(new ModManifest { ModGuid = "select.managed", Name = "Renamed", Version = "2.0.0", FolderPath = insideManaged }, snapshot, package)) && again.IsManaged, "selection is idempotent by GUID and never re-seeds an existing row");
    }
}
