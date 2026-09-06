using System.Collections.Generic;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Marketplace
{
#pragma warning disable CS0649
    internal sealed class FrameworkRelease
    {
        [JsonProperty("releaseId")] public long ReleaseId;
        [JsonProperty("version")] public string Version;
        [JsonProperty("tag")] public string Tag;
        [JsonProperty("prerelease")] public bool Prerelease;
        [JsonProperty("publishedAt")] public string PublishedAt;
        [JsonProperty("url")] public string Url;
        [JsonProperty("notes")] public string Notes;
        [JsonProperty("available")] public bool Available;
        [JsonProperty("reason")] public string Reason;
    }
    internal sealed class FrameworkUpdateResult
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("operationId")] public string OperationId;
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("status")] public string Status;
        [JsonProperty("message")] public string Message;
        [JsonProperty("installedVersion")] public string InstalledVersion;
        [JsonProperty("selectedMode")] public string SelectedMode;
        [JsonProperty("selectedTag")] public string SelectedTag;
        [JsonProperty("stable")] public FrameworkRelease Stable;
        [JsonProperty("preview")] public FrameworkRelease Preview;
        [JsonProperty("releases")] public List<FrameworkRelease> Releases = new List<FrameworkRelease>();
        [JsonProperty("cacheAgeSeconds")] public long CacheAgeSeconds;
        [JsonProperty("moreAvailable")] public bool MoreAvailable;
        [JsonProperty("launcherCompatible")] public bool LauncherCompatible;
        [JsonProperty("launcherMessage")] public string LauncherMessage;
    }
    internal sealed class FrameworkUpdateRequest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion = 1;
        [JsonProperty("operationId")] public string OperationId;
        [JsonProperty("gameDir")] public string GameDir;
        [JsonProperty("mode")] public string Mode;
        [JsonProperty("releaseId")] public long ReleaseId;
        [JsonProperty("tag")] public string Tag;
    }
#pragma warning restore CS0649
}
