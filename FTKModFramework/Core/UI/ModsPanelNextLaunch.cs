using System.Collections.Generic;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.UI
{
    internal static class ModsPanelNextLaunch
    {
        internal static List<string> Lines(ManagedSnapshot active, ManagedSnapshot pending, IEnumerable<ModEntry> entries)
        {
            List<string> lines = new List<string>();
            lines.Add("Community mods after restart:");
            // An empty pending snapshot means removal, not a fallback to the active mods.
            ManagedSnapshot selected = pending ?? active;
            if (selected == null || selected.Packages.Count == 0)
                lines.Add("No community mods selected.");
            else foreach (PackageDescriptor package in selected.Packages)
                lines.Add(Label(package.Name, package.Version, package.Enabled ? "ON" : "OFF"));

            bool addedLocalHeading = false;
            foreach (ModEntry entry in entries)
            {
                if (entry.IsManaged) continue;
                if (!addedLocalHeading)
                {
                    lines.Add("Manually installed mods after restart:");
                    addedLocalHeading = true;
                }
                bool enabled = entry.PendingEnabled ?? entry.Enabled;
                string state = !enabled ? "OFF" : entry.FrameworkCompatible ? "ON" : "BLOCKED / FRAMEWORK REQUIREMENT";
                lines.Add(Label(entry.DisplayName, entry.Version, state));
            }
            return lines;
        }

        private static string Label(string name, string version, string state)
        {
            return name + (string.IsNullOrEmpty(version) ? "" : " " + version) + " / " + state;
        }
    }
}
