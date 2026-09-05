using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using FTKModFramework;
using FTKModFramework.Core.Data;

internal static class Program
{
    private static void Assert(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }

    private static void WriteMod(string root, string folder, ModManifest manifest)
    {
        string path = Path.Combine(root, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "manifest.json"), JsonConvert.SerializeObject(manifest));
        // Intentionally invalid content proves discovery filters before the content loader could see it.
        File.WriteAllText(Path.Combine(path, "content.json"), "invalid fixture content");
    }

    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--startup-recovery")
        {
            MarketplaceChecks.RecoveryProcess(args[1], args[2] == "invalid");
            return;
        }
        ModManifest legacy = JsonContentParser.Deserialize<ModManifest>("{\"modGuid\":\"thirdparty.legacy\",\"name\":\"Legacy mod\",\"version\":\"1\"}");
        Assert(legacy.Validate(new ValidationReport()) && !legacy.IsDevelopmentOnly && legacy.Description == null,
            "existing player manifests need no new metadata fields");
        string fixtures = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("FTKModFramework/SampleData");
        string root = Path.Combine(Path.GetTempPath(), "ftkmf-player-mods-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int fixturesCopied = 0;
        foreach (string path in Directory.GetDirectories(fixtures))
        {
            ModManifest manifest = JsonContentParser.Deserialize<ModManifest>(File.ReadAllText(Path.Combine(path, "manifest.json")));
            Assert(manifest.DevelopmentOnly, manifest.ModGuid + " explicitly marked developmentOnly");
            // Simulate already-installed, pre-metadata copies of the known fixtures.
            manifest.DevelopmentOnly = false;
            WriteMod(root, Path.GetFileName(path), manifest);
            fixturesCopied++;
        }
        WriteMod(root, "dev", new ModManifest { ModGuid = "thirdparty.dev", Name = "Developer fixture", Version = "1", DevelopmentOnly = true, BehaviorDll = "../unsafe.dll" });
        WriteMod(root, "player", new ModManifest { ModGuid = "thirdparty.player", Name = "Playable mod", Version = "2", Description = "An actual adventure.", Author = "A modder" });
        ValidationReport report = new ValidationReport();
        List<DiscoveredMod> mods = ModDiscovery.Discover(root, report);
        Assert(mods.Count == 1 && mods[0].Manifest.ModGuid == "thirdparty.player", "player mode excludes flagged and legacy fixtures");
        Assert(report.Errors.Count == 0, "player mode does not validate fixture DLL paths");
        Assert(mods[0].Manifest.Description == "An actual adventure." && mods[0].Manifest.Author == "A modder", "player metadata survives discovery");
        Plugin.SelfTestsEnabled = true;
        report = new ValidationReport();
        mods = ModDiscovery.Discover(root, report);
        Assert(mods.Count == fixturesCopied + 2, "development mode discovers fixtures alongside player mods");
        Assert(report.Errors.Count == 2, "development mode validates both intentional unsafe DLL paths");
        Plugin.SelfTestsEnabled = false;

        string key = "thirdparty.disabled";
        UnityEngine.PlayerPrefs.SetInt(ModRegistry.PrefKeyPrefix + key, 0);
        ModEntry entry = ModRegistry.Register(key, "Disabled mod", false, "3", true, "Description", "Author");
        Assert(!entry.Enabled && entry.Description == "Description" && entry.Author == "Author", "metadata preserves existing disabled preference");
        List<DiscoveredMod> disabled = new List<DiscoveredMod>();
        disabled.Add(new DiscoveredMod(new ModManifest { ModGuid = key }, new List<string>(), Path.Combine(root, "missing.dll")));
        report = new ValidationReport();
        BehaviorLoader.LoadAll(disabled, report);
        Assert(report.Errors.Count == 0, "disabled mod skips behavior DLL file access and loading");
        ModRegistry.SetEnabled(key, true);
        Assert(!entry.Enabled && entry.PendingEnabled == true && !ModRegistry.IsEnabled(key), "toggle writes pending state without changing this session's enabled snapshot");
        string enabledKey = "thirdparty.enabled";
        ModRegistry.Register(enabledKey, "Enabled mod", false, "1", true, null, null);
        disabled[0] = new DiscoveredMod(new ModManifest { ModGuid = enabledKey }, new List<string>(), Path.Combine(root, "missing.dll"));
        BehaviorLoader.LoadAll(disabled, report);
        Assert(report.Errors.Count == 1 && report.Errors[0].Contains("behaviorDll not found"), "enabled mod reaches behavior DLL validation");
        Plugin.EnableSampleContent.Value = false;
        ModEntry bundled = ModRegistry.Register("com.ftkmf.framework", "FTK Adventure Pack", true, null, true, "Classes and adventures", "FTK Mod Framework team");
        Assert(!bundled.Enabled, "renamed bundled pack preserves config disabled state");
        Assert(object.ReferenceEquals(bundled, ModRegistry.Register(bundled.Key, "Other name", true, null, true, null, null)), "registration remains idempotent by stable key");
        MarketplaceChecks.Run(root);
        Console.WriteLine("Fixtures retained at " + root);
    }
}
