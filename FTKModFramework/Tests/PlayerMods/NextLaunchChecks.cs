using System;
using System.Collections.Generic;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;
using FTKModFramework.Core.UI;

internal static class NextLaunchChecks
{
    private static void Assert(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: next launch / " + name);
    }

    internal static void Run()
    {
        ManagedSnapshot active = new ManagedSnapshot();
        active.Packages.Add(new PackageDescriptor { PackageId = "paladin", Name = "Paladin", Version = "1.0.0", Enabled = true });
        List<ModEntry> entries = new List<ModEntry>();
        ModEntry managed = new ModEntry("paladin", "Paladin", "1.0.0", true, null, null);
        managed.MarkManaged("paladin");
        entries.Add(managed);
        List<string> lines = ModsPanelNextLaunch.Lines(active, null, entries);
        Assert(lines.Count == 2 && lines[1] == "Paladin 1.0.0 / ON", "unchanged active selection is populated once");

        ManagedSnapshot pending = new ManagedSnapshot();
        lines = ModsPanelNextLaunch.Lines(active, pending, entries);
        Assert(lines.Count == 2 && lines[1] == "No community mods selected.", "pending uninstall does not resurrect active Paladin");
        pending.Packages.Add(new PackageDescriptor { PackageId = "paladin", Name = "Paladin", Version = "1.0.0", Enabled = false });
        lines = ModsPanelNextLaunch.Lines(active, pending, entries);
        Assert(lines[1] == "Paladin 1.0.0 / OFF" && active.Packages[0].Enabled, "pending turn off is shown without changing the running selection");
        active.Packages[0].Enabled = false;
        pending.Packages[0].Enabled = true;
        lines = ModsPanelNextLaunch.Lines(active, pending, entries);
        Assert(lines[1] == "Paladin 1.0.0 / ON" && !active.Packages[0].Enabled, "pending turn on overrides the disabled running selection");
        lines = ModsPanelNextLaunch.Lines(active, null, entries);
        Assert(lines[1] == "Paladin 1.0.0 / OFF", "unchanged disabled installed mod remains visible");
        lines = ModsPanelNextLaunch.Lines(null, pending, entries);
        Assert(lines[1] == "Paladin 1.0.0 / ON", "first installation appears before an active generation exists");

        entries = new List<ModEntry>();
        lines = ModsPanelNextLaunch.Lines(null, null, entries);
        Assert(lines.Count == 2 && lines[1] == "No community mods selected.", "empty state is explicit");
        ModEntry manual = new ModEntry("manual", "Manual mod", "2.0.0", true, null, null);
        manual.PendingEnabled = false;
        ModEntry blocked = new ModEntry("blocked", "Incompatible mod", "1.0.0", true, null, null, ">=999.0.0", true);
        entries.Add(manual);
        entries.Add(blocked);
        lines = ModsPanelNextLaunch.Lines(null, null, entries);
        Assert(lines.Contains("Manual mod 2.0.0 / OFF"), "manual pending preference is reflected");
        Assert(lines.Contains("Incompatible mod 1.0.0 / BLOCKED / FRAMEWORK REQUIREMENT"), "incompatible enabled preference is not promised to load");
        manual.PendingEnabled = null;
        lines = ModsPanelNextLaunch.Lines(null, null, entries);
        Assert(lines.Contains("Manual mod 2.0.0 / ON"), "unchanged manual selection remains visible");
    }
}
