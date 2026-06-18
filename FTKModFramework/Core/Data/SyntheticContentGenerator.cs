using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// DEV/STRESS aid (P5b, #23): manufactures N deterministic throwaway content entries and writes them
    /// as a SINGLE synthetic mod (one manifest + one content file, in the exact shape <see cref="ContentLoader"/>
    /// consumes) into a RESERVED subfolder under DataContentRoot. The existing single
    /// <c>ContentLoader.Load(DataContentRoot)</c> pass then discovers and registers the synthetic mod through
    /// the public <c>Content.*</c> API, so the existing determinism self-test and the scale-budget gate cover
    /// it for free. Nothing here re-implements registration: the generator only WRITES JSON.
    ///
    /// Determinism: the generator writes STRING ids (<c>synthetic_000001</c>...); the deterministic INT id is
    /// minted downstream by the existing ContentLoader -> Content.AddWeapon -> ContentRegistry -> IdAllocator
    /// path, identical on every machine for the same count. The generator adds no randomness and no
    /// cross-references, so two machines with the same count produce byte-identical synthetic content.
    ///
    /// Folder safety: the ONLY directory this class ever creates or deletes is the reserved subfolder
    /// (<see cref="ReservedSubfolderName"/>). It never touches sibling mod folders or DataContentRoot itself.
    /// At count 0 it is a true no-op AND it removes a stale reserved subfolder a prior higher-N run may have
    /// left, so a stale higher-N count can never leak into a later lower-N run.
    /// </summary>
    internal static class SyntheticContentGenerator
    {
        /// <summary>
        /// RESERVED mod guid for generated synthetic stress content. A REAL data mod must never declare this
        /// guid; discovery (<see cref="ModDiscovery"/>) rejects it for any folder other than the reserved
        /// subfolder so a real mod cannot perturb the deterministic (modGuid, id) sort.
        /// </summary>
        public const string ReservedModGuid = "com.ftkmf.synthetic";

        /// <summary>
        /// RESERVED subfolder name under DataContentRoot. The generator owns this one folder outright:
        /// it is cleared and rewritten at the start of every gated run. Full path =
        /// <c>Path.Combine(DataContentRoot, ReservedSubfolderName)</c>.
        /// </summary>
        public const string ReservedSubfolderName = "__ftkmf_synthetic__";

        private const string ManifestFileName = "manifest.json";
        private const string ContentFileName = "synthetic.json";
        private const string ManifestName = "FTKMF Synthetic Stress Content";
        private const string ManifestVersion = "1.0.0";

        // Pad ids to 6 digits: enough headroom for the max supported N. Co-op clients with the same count
        // produce identical ids by construction.
        private const string IdFormat = "synthetic_{0:D6}";
        private const string DisplayNameFormat = "Synthetic {0:D6}";

        /// <summary>
        /// Clear the reserved subfolder, then (when <paramref name="count"/> &gt; 0) write a synthetic mod of
        /// exactly <paramref name="count"/> entries into it. ALWAYS runs (even at count 0) so a stale subfolder
        /// from a prior higher-N run is removed. A null/blank <paramref name="dataContentRoot"/> is a no-op.
        /// </summary>
        public static void Generate(string dataContentRoot, int count, string kind, string template)
        {
            if (IsBlank(dataContentRoot))
            {
                Plugin.Log.LogDebug("Synthetic content: DataContentRoot is blank; nothing to generate.");
                return;
            }

            string subfolder = Path.Combine(dataContentRoot, ReservedSubfolderName);

            // Always clear first: this both rewrites on a fresh higher-N run and guarantees a count-0 run
            // leaves NO subfolder behind (so a stale higher-N run cannot leak into a later lower-N run).
            ClearReservedSubfolder(subfolder);

            if (count <= 0)
            {
                Plugin.Log.LogInfo("Synthetic content: count <= 0; reserved subfolder removed (no synthetic content).");
                return;
            }

            // Defaults match the verified-valid sample-data template (a bladeDagger weapon clone) so synthetic
            // weapons land in the high band the save-proxy counts. Only count/kind/template are configurable.
            if (IsBlank(kind)) kind = "weapon";
            if (IsBlank(template)) template = "bladeDagger";

            Directory.CreateDirectory(subfolder);
            WriteManifest(subfolder);
            WriteContentFile(subfolder, count, kind, template);

            Plugin.Log.LogInfo("Synthetic content: wrote " + count + " '" + kind + "' entries (template '" +
                template + "') to '" + subfolder + "' as mod '" + ReservedModGuid + "'.");
        }

        /// <summary>
        /// Delete the reserved subfolder if (and only if) it exists AND its path ends with the reserved name.
        /// The guard makes a wrong-path deletion impossible: this class only ever removes the one folder it owns,
        /// never DataContentRoot itself and never a sibling mod folder.
        /// </summary>
        private static void ClearReservedSubfolder(string subfolder)
        {
            if (!IsReservedPath(subfolder))
            {
                // Defensive: never delete anything that is not the reserved subfolder.
                Plugin.Log.LogWarning("Synthetic content: refusing to clear non-reserved path '" + subfolder + "'.");
                return;
            }

            if (!Directory.Exists(subfolder)) return;

            try
            {
                Directory.Delete(subfolder, true); // recursive: only ever the reserved scratch dir.
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Synthetic content: failed to clear reserved subfolder '" + subfolder + "': " + e.Message);
            }
        }

        /// <summary>True iff the path's last path component is exactly the reserved subfolder name.</summary>
        private static bool IsReservedPath(string path)
        {
            if (IsBlank(path)) return false;
            string leaf = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(leaf, ReservedSubfolderName, StringComparison.Ordinal);
        }

        private static void WriteManifest(string subfolder)
        {
            ManifestDto dto = new ManifestDto();
            dto.ModGuid = ReservedModGuid;
            dto.Name = ManifestName;
            dto.Version = ManifestVersion;

            string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            File.WriteAllText(Path.Combine(subfolder, ManifestFileName), json);
        }

        private static void WriteContentFile(string subfolder, int count, string kind, string template)
        {
            ContentFileDto file = new ContentFileDto();
            file.Entries = new List<EntryDto>(count);

            // Index 1..count: a fixed prefix plus the zero-padded index. No randomized fields, no
            // cross-references, no proficiencies: the minimum valid entry shape ContentLoader accepts.
            for (int i = 1; i <= count; i++)
            {
                EntryDto entry = new EntryDto();
                entry.Kind = kind;
                entry.Id = string.Format(CultureInfo.InvariantCulture, IdFormat, i);
                entry.Template = template;
                entry.DisplayName = string.Format(CultureInfo.InvariantCulture, DisplayNameFormat, i);
                entry.Fields = new Dictionary<string, object>(1);
                entry.Fields["goldValue"] = 1; // single scalar; enough to be a valid minimal override.
                file.Entries.Add(entry);
            }

            string json = JsonConvert.SerializeObject(file, Formatting.Indented);
            File.WriteAllText(Path.Combine(subfolder, ContentFileName), json);
        }

        // net35 has no string.IsNullOrWhiteSpace; do it by hand (mirrors the loader's own helper).
        private static bool IsBlank(string s)
        {
            return s == null || s.Trim().Length == 0;
        }

        // ---- Serialization DTOs: mirror the EXACT property names ContentLoader's parser consumes -----------
        // (ModManifest / ContentFile / ContentEntry use these same [JsonProperty] keys; we write them with
        // matching anonymous-style DTOs so the round-trip through the existing parser is guaranteed.)

        private sealed class ManifestDto
        {
            [JsonProperty("modGuid")] public string ModGuid;
            [JsonProperty("name")] public string Name;
            [JsonProperty("version")] public string Version;
        }

        private sealed class ContentFileDto
        {
            [JsonProperty("entries")] public List<EntryDto> Entries;
        }

        private sealed class EntryDto
        {
            [JsonProperty("kind")] public string Kind;
            [JsonProperty("id")] public string Id;
            [JsonProperty("template")] public string Template;
            [JsonProperty("displayName")] public string DisplayName;
            [JsonProperty("fields")] public Dictionary<string, object> Fields;
        }
    }
}
