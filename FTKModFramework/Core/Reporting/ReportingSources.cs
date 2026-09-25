using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.HotReload;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.Reporting
{
    // Called only by the main-thread report/lifecycle owner, after its readiness observation.
    internal static class ReportingSources
    {
        private static readonly FieldInfo VersionField = typeof(FTKVersion).GetField("gFTKVersion", BindingFlags.Static | BindingFlags.NonPublic);

        internal static string Capture(bool sourcesReady)
        {
            ReportingMetadataInput input = new ReportingMetadataInput();
            input.FrameworkVersion = Plugin.Version;
            input.UnityVersion = UnityEngine.Application.unityVersion;
            input.Platform = Environment.OSVersion.Platform.ToString();
            input.RuntimeVersion = Environment.Version.ToString();
            input.ProcessBits = IntPtr.Size * 8;
            // These are already-existing references; never invoke FTKVersion.Instance discovery.
            FTKVersion version = VersionField == null ? null : VersionField.GetValue(null) as FTKVersion;
            input.GameVersion = version ? version.m_VersionNum : null;
            uiStartGame start = uiStartGame.Instance;
            input.Phase = start && start.m_GameStarted ? "session_or_transition" : "startup_or_unknown";
            input.DataContent = Plugin.EnableDataContent == null ? (bool?)null : Plugin.EnableDataContent.Value;
            input.BehaviorLoading = Plugin.EnableBehaviorLoading == null ? (bool?)null : Plugin.EnableBehaviorLoading.Value;
            input.CampaignEngine = Plugin.EnableCampaignEngine == null ? (bool?)null : Plugin.EnableCampaignEngine.Value;
            input.SelfTests = Plugin.RunSelfTests == null ? (bool?)null : Plugin.RunSelfTests.Value;
            input.ScaleBudgetGate = Plugin.DiagnosticsEnableGate == null ? (bool?)null : Plugin.DiagnosticsEnableGate.Value;
            input.SourcesReady = sourcesReady;
            input.Transitioning = HotReloadCoordinator.Busy;
            input.RuntimeFaulted = HotReloadCoordinator.Faulted;
            if (sourcesReady && !input.Transitioning && !input.RuntimeFaulted)
            {
                var entries = ModRegistry.Entries;
                List<ReportingInventoryRow> mods = new List<ReportingInventoryRow>();
                for (int i = 0; i < Math.Min(entries.Count, ReportingMetadata.MaximumRows); i++)
                {
                    ModEntry entry = entries[i];
                    mods.Add(new ReportingInventoryRow { Id = entry.Key, Version = entry.Version,
                        Enabled = entry.Enabled, PendingEnabled = entry.PendingEnabled });
                }
                input.Mods = new ReportingInventory { Rows = mods, TotalCount = entries.Count };
                input.Active = Managed(MarketplaceRuntime.Active);
                input.Pending = Managed(MarketplaceRuntime.Pending);
                // Only the audited bootstrap dependency is an authority for this dictionary.
                if (typeof(Chainloader).Assembly.GetName().Version.ToString() == "5.4.20.0")
                {
                    List<ReportingInventoryRow> plugins = new List<ReportingInventoryRow>();
                    var infos = Chainloader.PluginInfos;
                    foreach (var pair in infos)
                    {
                        if (plugins.Count == ReportingMetadata.MaximumRows) break;
                        plugins.Add(new ReportingInventoryRow { Id = pair.Value.Metadata.GUID, Version = pair.Value.Metadata.Version.ToString() });
                    }
                    input.Plugins = new ReportingInventory { Rows = plugins, TotalCount = infos.Count };
                }
            }
            return ReportingMetadata.Capture(input, DateTime.UtcNow);
        }

        private static ReportingInventory Managed(ManagedSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Packages == null) return null;
            List<ReportingInventoryRow> rows = new List<ReportingInventoryRow>();
            for (int i = 0; i < Math.Min(snapshot.Packages.Count, ReportingMetadata.MaximumRows); i++)
            {
                PackageDescriptor package = snapshot.Packages[i];
                rows.Add(new ReportingInventoryRow { Id = package.PackageId, Version = package.Version, Enabled = package.Enabled });
            }
            return new ReportingInventory { Rows = rows, TotalCount = snapshot.Packages.Count };
        }
    }
}
