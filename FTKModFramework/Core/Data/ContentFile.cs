using System.Collections.Generic;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// One parsed content <c>*.json</c> file: a flat list of <see cref="ContentEntry"/>. The on-disk
    /// shape is a JSON object with a single <c>"entries"</c> array, so a file can carry many entries.
    /// </summary>
    internal sealed class ContentFile
    {
        // Entries is populated by Newtonsoft via reflection, not by C# code; silence "never assigned".
#pragma warning disable CS0649
        [JsonProperty("entries")] public List<ContentEntry> Entries;
#pragma warning restore CS0649

        /// <summary>Absolute path this file was parsed from (for diagnostics). Filled by the parser.</summary>
        [JsonIgnore] public string SourcePath;
    }
}
