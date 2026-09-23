using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using FTKModFramework;
using FTKModFramework.Core.Data;

internal static class CompatibilityChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    internal static void Run(string root)
    {
        Version parsed;
        foreach (string invalid in new[] { "", "1", "1.2", "01.2.3", "1.02.3", "1.2.03", "1.2.3.4", "v1.2.3", "1.2.3-beta", " 1.2.3", "1.2.3 ", "-1.2.3", "2147483648.0.0" })
            Check(!ModFrameworkCompatibility.TryParse(invalid, out parsed), "reject noncanonical version " + invalid);
        Check(ModFrameworkCompatibility.TryParse("2147483647.0.0", out parsed), "Int32 maximum component accepted");
        foreach (string running in new[] { "0.1.2", "0.1.3", "0.2.0", "0.999.999" })
            Check(ModFrameworkCompatibility.Reason("0.1.2", running) == null, "same-major compatible " + running);
        foreach (string running in new[] { "0.1.1", "0.0.9", "1.0.0" })
            Check(ModFrameworkCompatibility.Reason("0.1.2", running) != null, "incompatible framework " + running);
        Check(ModFrameworkCompatibility.Reason(null, "0.1.3") != null, "missing declaration is unverified");

        string content = Path.Combine(root, "compatibility");
        Directory.CreateDirectory(content);
        foreach (string declaration in new string[] { null, "bad", "1.0.0", "0.999.0" })
        {
            string key = "blocked." + Guid.NewGuid().ToString("N");
            string folder = Path.Combine(content, key);
            Directory.CreateDirectory(folder);
            ModManifest manifest = new ModManifest { ModGuid = key, Name = key, Version = "1.0.0", FrameworkVersion = declaration, BehaviorDll = "../must-not-resolve.dll" };
            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(Path.Combine(folder, "content.json"), "not json");
            UnityEngine.PlayerPrefs.SetInt(ModRegistry.PrefKeyPrefix + key, 1);
            ModEntry entry = ModRegistry.Register(key, key, "1.0.0", true, null, null, declaration, true);
            Check(entry.Enabled && !entry.FrameworkCompatible && !ModRegistry.IsEnabled(key), "blocked preference retained but loading disabled");
            ModRegistry.SetEnabled(key, true);
            Check(entry.PendingEnabled == null && UnityEngine.PlayerPrefs.GetInt(ModRegistry.PrefKeyPrefix + key, 0) == 1, "blocked enable cannot rewrite preference");
            ValidationReport behavior = new ValidationReport();
            BehaviorLoader.LoadAll(new List<DiscoveredMod> { new DiscoveredMod(manifest, new List<string>(), Path.Combine(folder, "missing.dll")) }, behavior);
            Check(behavior.Errors.Count == 0, "compatibility gate prevents behavior file access");
        }
        ValidationReport report = new ValidationReport();
        List<DiscoveredMod> mods = ModDiscovery.Discover(content, report);
        Check(mods.Count == 4 && report.Errors.Count == 0, "incompatible identities remain visible without resolving invalid DLL paths");
        foreach (DiscoveredMod mod in mods)
            Check(mod.ContentFilePaths.Count == 0 && mod.BehaviorDllPath == null, "blocked discovery queues no content or behavior");
        ValidationReport unknownBehavior = new ValidationReport();
        BehaviorLoader.LoadAll(new List<DiscoveredMod> {
            new DiscoveredMod(new ModManifest { ModGuid = "unregistered.blocked", FrameworkVersion = "1.0.0" },
                new List<string>(), Path.Combine(root, "missing-unregistered.dll"))
        }, unknownBehavior);
        Check(unknownBehavior.Errors.Count == 0, "manifest gate blocks behavior even without registry entry");
        ModRegistry.Register("author.updated", "Author update", "1.0.0", true, null, null, null, true);
        Check(ModRegistry.CompatibilityReasonFor("author.updated", "1.0.0") != null, "installed unverified version retains its blocker");
        Check(ModRegistry.CompatibilityReasonFor("author.updated", "1.0.1") == null &&
            ModFrameworkCompatibility.Reason(Plugin.Version, Plugin.Version) == null,
            "compatible author replacement is not blocked by old installed manifest");
        ModManifest invalidVersion = new ModManifest { ModGuid = "invalid.version", Name = "Invalid", Version = "1.0", FrameworkVersion = Plugin.Version };
        Check(!invalidVersion.Validate(new ValidationReport()), "mod version itself requires strict X.Y.Z");
    }
}
