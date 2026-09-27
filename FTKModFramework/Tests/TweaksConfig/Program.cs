using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FTKModFramework.Core;

// FR-2 through the real BepInEx ConfigFile: the [Tweaks] section round-trips across restarts,
// writes save at once, unparsed values resolve to Default, and retired keys survive.
internal static class Program
{
    private static int _checks;
    private static readonly List<string> Logs = new List<string>();
    private const string SkipIntro = "convenience.skip-intro";
    private const string FixId = "fix.config-probe";

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    /// <summary>A process restart: a fresh ConfigFile over the same path and a fresh registry.</summary>
    private static TweakRegistry Boot(string path, out ConfigFile config)
    {
        Logs.Clear();
        config = new ConfigFile(path, true);
        var registry = new TweakRegistry(Logs.Add);
        FrameworkTweaks.RegisterAll(registry);
        registry.Register(new TweakDescriptor(FixId, TweakCategory.Fix, TweakScope.Local, "Probe fix", "Probe.", "Test."));
        Check(registry.Initialize(new TweakConfigStore(config, registry)), "initialization from the config file succeeds");
        return registry;
    }

    private static string Line(string path, string key)
    {
        foreach (string line in File.ReadAllLines(path))
            if (line.StartsWith(key + " = ", StringComparison.Ordinal)) return line.Substring(key.Length + 3);
        return null;
    }

    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftkmf-tweaks-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            RoundTrip(Path.Combine(root, "fresh.cfg"));
            UnparsedAndRetired(Path.Combine(root, "edited.cfg"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
        Console.WriteLine("TweaksConfig: " + _checks + " checks passed ([Tweaks] config round trip, unparsed values, retired keys).");
    }

    private static void RoundTrip(string path)
    {
        ConfigFile config;
        TweakRegistry r = Boot(path, out config);
        int skip, fix;
        bool registered = r.TryGetHandle(SkipIntro, out skip);
        registered &= r.TryGetHandle(FixId, out fix);
        Check(registered, "both tweaks are registered");
        Check(!r.IsOn(skip) && r.IsOn(fix) && Logs.Count == 0, "a fresh install follows every default");
        Check(File.ReadAllText(path).Contains("[" + TweakConfigStore.Section + "]"), "the store writes a [Tweaks] section");
        Check(Line(path, SkipIntro) == "Default" && Line(path, FixId) == "Default", "each registered ID gets one entry holding Default");
        Check(config.ContainsKey(new ConfigDefinition(TweakConfigStore.Section, SkipIntro)), "the entry is keyed by the tweak ID");

        Check(r.Toggle(skip) && r.IsOn(skip), "toggling Skip intro turns it on");
        Check(Line(path, SkipIntro) == "On", "the write is saved to disk immediately");
        Check(r.Toggle(fix) && !r.IsOn(fix) && Line(path, FixId) == "Off", "a default-on tweak toggles to Off on disk");

        TweakRegistry restarted = Boot(path, out config);
        Check(restarted.IsOn(skip) && restarted.Preference(skip) == TweakPreference.On, "On survives a restart");
        Check(!restarted.IsOn(fix) && restarted.Preference(fix) == TweakPreference.Off, "Off survives a restart");

        Check(restarted.Toggle(skip) && Line(path, SkipIntro) == "Default" && !restarted.IsOn(skip), "toggling back stores Default");
        Check(restarted.ResetAll() && Line(path, FixId) == "Default" && restarted.IsOn(fix), "reset stores Default for every row");

        TweakRegistry again = Boot(path, out config);
        Check(!again.IsOn(skip) && again.IsOn(fix) && again.Preference(fix) == TweakPreference.Default, "Default survives a restart and follows the default");
    }

    private static void UnparsedAndRetired(string path)
    {
        File.WriteAllLines(path, new[]
        {
            "[Tweaks]",
            SkipIntro + " = 7",
            FixId + " = On, Off",
            "retired.tweak = On",
        });
        ConfigFile config;
        TweakRegistry r = Boot(path, out config);
        int skip, fix;
        r.TryGetHandle(SkipIntro, out skip);
        r.TryGetHandle(FixId, out fix);
        Check(r.Preference(skip) == TweakPreference.Default && !r.IsOn(skip), "a stored 7 resolves to Default");
        Check(r.Preference(fix) == TweakPreference.Default && r.IsOn(fix), "a stored 'On, Off' resolves to Default");
        Check(Logs.Count == 0, "the framework adds no log line for values BepInEx accepted");
        Check(!r.IsOn("retired.tweak"), "a retired key is ignored");

        File.WriteAllLines(path, new[]
        {
            "[Tweaks]",
            SkipIntro + " = not-a-value",
            FixId + " = off",
            "retired.tweak = On",
        });
        r = Boot(path, out config);
        Check(r.Preference(skip) == TweakPreference.Default && !r.IsOn(skip), "an unparseable value falls back to Default");
        Check(Logs.Count == 0, "the framework adds no second log line for an unparseable value");
        Check(r.Preference(fix) == TweakPreference.Off && !r.IsOn(fix), "a lower-case off is an explicit choice");

        Check(r.Toggle(skip) && Line(path, SkipIntro) == "On", "a later write saves normally");
        Check(Line(path, "retired.tweak") == "On", "a retired key is left in the file untouched after a save");
    }
}
