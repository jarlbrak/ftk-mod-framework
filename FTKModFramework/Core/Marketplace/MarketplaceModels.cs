using System.Collections.Generic;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Marketplace
{
#pragma warning disable CS0649
    internal sealed class PackageSelection
    {
        [JsonProperty("packageId")] public string PackageId;
        [JsonProperty("version")] public string Version;
        [JsonProperty("enabled")] public bool Enabled;
    }
    internal sealed class PackageDescriptor
    {
        [JsonProperty("packageId")] public string PackageId;
        [JsonProperty("modGuid")] public string ModGuid;
        [JsonProperty("name")] public string Name;
        [JsonProperty("author")] public string Author;
        [JsonProperty("description")] public string Description;
        [JsonProperty("category")] public string Category;
        [JsonProperty("version")] public string Version;
        [JsonProperty("license")] public string License;
        [JsonProperty("frameworkRange")] public string FrameworkRange;
        [JsonProperty("gameFingerprints")] public string[] GameFingerprints;
        [JsonProperty("platforms")] public string[] Platforms;
        [JsonProperty("dependencies")] public PackageSelection[] Dependencies;
        [JsonProperty("sha256")] public string Sha256;
        [JsonProperty("classification")] public string Classification;
        [JsonProperty("requirements")] public string[] Requirements;
        [JsonProperty("contentChanges")] public string[] ContentChanges;
        [JsonProperty("changelog")] public string Changelog;
        [JsonProperty("sourceUrl")] public string SourceUrl;
        [JsonProperty("supportUrl")] public string SupportUrl;
        [JsonProperty("screenshots")] public string[] Screenshots;
        [JsonProperty("screenshotPaths")] public string[] ScreenshotPaths;
        [JsonProperty("compatible")] public bool Compatible;
        [JsonProperty("compatibilityReason")] public string CompatibilityReason;
        [JsonProperty("revoked")] public bool Revoked;
        [JsonProperty("enabled")] public bool Enabled;
    }
    internal sealed class ManagedSnapshot
    {
        [JsonProperty("generationId")] public string GenerationId;
        [JsonProperty("contentRoot")] public string ContentRoot;
        [JsonProperty("packages")] public List<PackageDescriptor> Packages = new List<PackageDescriptor>();
    }
    internal sealed class MarketplacePlanEntry
    {
        [JsonProperty("action")] public string Action;
        [JsonProperty("packageId")] public string PackageId;
        [JsonProperty("name")] public string Name;
        [JsonProperty("fromVersion")] public string FromVersion;
        [JsonProperty("toVersion")] public string ToVersion;
        [JsonProperty("dependency")] public bool Dependency;
    }
    internal sealed class MarketplaceResult
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("operationId")] public string OperationId;
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("status")] public string Status;
        [JsonProperty("message")] public string Message;
        [JsonProperty("catalogAgeSeconds")] public long CatalogAgeSeconds;
        [JsonProperty("packages")] public List<PackageDescriptor> Packages = new List<PackageDescriptor>();
        [JsonProperty("active")] public ManagedSnapshot Active;
        [JsonProperty("pending")] public ManagedSnapshot Pending;
        [JsonProperty("previousAvailable")] public bool PreviousAvailable;
        [JsonProperty("planRevision")] public string PlanRevision;
        [JsonProperty("plan")] public List<MarketplacePlanEntry> Plan = new List<MarketplacePlanEntry>();
        [JsonProperty("exportPath")] public string ExportPath;
    }
    internal sealed class MarketplaceRequest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion = 1;
        [JsonProperty("operationId")] public string OperationId;
        [JsonProperty("stateRoot")] public string StateRoot;
        [JsonProperty("frameworkVersion")] public string FrameworkVersion;
        [JsonProperty("gameAssemblyPath")] public string GameAssemblyPath;
        [JsonProperty("platform")] public string Platform;
        [JsonProperty("manualRoots")] public string[] ManualRoots;
        [JsonProperty("manualGuids")] public string[] ManualGuids;
        [JsonProperty("bundledGuids")] public string[] BundledGuids;
        [JsonProperty("selection")] public List<PackageSelection> Selection;
        [JsonProperty("dryRun")] public bool DryRun;
        [JsonProperty("expectedRevision")] public string ExpectedRevision;
        [JsonProperty("settings")] public Dictionary<string, object> Settings;
    }
    internal sealed class MarketplaceStateRecord
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("current")] public string Current;
        [JsonProperty("previous")] public string Previous;
        [JsonProperty("pending")] public string Pending;
    }
    internal sealed class MarketplaceGenerationLock
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("packages")] public List<PackageDescriptor> Packages;
    }
    internal sealed class HelperManifest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("protocolVersion")] public int ProtocolVersion;
        [JsonProperty("sha256")] public string Sha256;
    }
#pragma warning restore CS0649
}
