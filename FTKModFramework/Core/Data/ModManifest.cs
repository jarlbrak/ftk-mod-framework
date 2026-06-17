using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// A mod's <c>manifest.json</c>: the per-folder identity card the loader needs before it will
    /// queue any of that folder's content files. All three fields are required; a manifest missing
    /// one is a validation error and the whole mod folder is skipped (other mods still load).
    ///
    /// <see cref="ModGuid"/> is passed straight through to the public <c>Content.Add*</c> API as the
    /// namespacing argument (R4): it is NOT pre-concatenated into a content id. <c>ContentRegistry</c>
    /// already namespaces ids as <c>IdAllocator.Allocate(modGuid, dbType.Name + "/" + id)</c>.
    /// </summary>
    internal sealed class ModManifest
    {
        // Fields are populated by Newtonsoft via reflection, not by C# code; silence "never assigned".
#pragma warning disable CS0649
        [JsonProperty("modGuid")] public string ModGuid;
        [JsonProperty("name")] public string Name;
        [JsonProperty("version")] public string Version;
#pragma warning restore CS0649

        /// <summary>Absolute path of the folder this manifest was loaded from (filled by discovery).</summary>
        [JsonIgnore] public string FolderPath;

        /// <summary>
        /// Append any missing-required-field problems to <paramref name="report"/>. Returns true when
        /// the manifest is usable. A blank/whitespace value counts as missing.
        /// </summary>
        public bool Validate(ValidationReport report)
        {
            bool ok = true;
            if (IsBlank(ModGuid)) { report.Error("manifest.json (" + FolderPath + "): missing required field 'modGuid'."); ok = false; }
            if (IsBlank(Name)) { report.Error("manifest.json (" + FolderPath + "): missing required field 'name'."); ok = false; }
            if (IsBlank(Version)) { report.Error("manifest.json (" + FolderPath + "): missing required field 'version'."); ok = false; }
            return ok;
        }

        // net35 has no string.IsNullOrWhiteSpace; do it by hand.
        private static bool IsBlank(string s)
        {
            return s == null || s.Trim().Length == 0;
        }
    }
}
