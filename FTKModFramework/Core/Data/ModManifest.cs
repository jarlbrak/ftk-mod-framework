using System;
using System.IO;
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

        /// <summary>
        /// OPTIONAL bare FILENAME (not a path) of the mod's behaviour DLL inside its own folder, e.g.
        /// <c>"mymod.behaviors.dll"</c>. NOT a required field: a manifest without it validates and loads
        /// exactly as one with it, just with no behaviour assembly. It is NOT loaded by this slice (#33
        /// owns <c>Assembly.LoadFrom</c>); here it is only parsed and run through a path-traversal guard
        /// (see <see cref="TryResolveBehaviorDll"/>): a value containing <c>..</c>, a rooted path, any separator,
        /// or one that canonicalizes outside the mod folder is rejected and the mod's content still loads without it.
        /// </summary>
        [JsonProperty("behaviorDll")] public string BehaviorDll;
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

        /// <summary>
        /// Path-traversal guard for a PRESENT <c>behaviorDll</c> value: the caller only invokes this when
        /// <paramref name="behaviorDll"/> is non-blank, so a reject here is ALWAYS an error (a blank value
        /// means "no behaviour DLL" and is handled by the caller, not here). The DLL must live directly
        /// inside the mod folder under a bare filename: a co-op client loading a mod from a different
        /// install path must resolve the SAME relative file, never something outside the folder.
        ///
        /// Rejects (returns false, sets <paramref name="rejectReason"/>, <paramref name="resolvedPath"/> =
        /// null) for, in MOST-SPECIFIC-FIRST order so every branch is independently reachable: (1) a value
        /// containing <c>..</c> (path traversal), (2) a rooted/absolute value, (3) a value containing any
        /// directory separator, (4) a value that canonicalizes outside the mod-folder root. net35 has no
        /// <c>Path.GetRelativePath</c>, so the escape test is <c>Path.GetFullPath</c> + an ORDINAL
        /// <c>StartsWith(root + separator)</c>.
        /// Accepts (returns true) with <paramref name="resolvedPath"/> = the canonicalized absolute path.
        /// </summary>
        public static bool TryResolveBehaviorDll(string modFolder, string behaviorDll,
            out string resolvedPath, out string rejectReason)
        {
            resolvedPath = null;
            rejectReason = null;

            // Checks are ordered MOST-SPECIFIC-FIRST so each rejection gives the most informative reason and
            // every branch is independently reachable (a generic separator test would otherwise mask the
            // '..' and rooted cases, since "../evil.dll" and "/etc/evil.dll" both also contain a separator).
            // The ACCEPT/REJECT decision is unchanged: the union of reject conditions is identical to before.

            // 1) Path traversal: a literal ".." anywhere is the most dangerous and most specific signal.
            if (behaviorDll.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                rejectReason = "contains '..' (path traversal)";
                return false;
            }

            // 2) Rooted/absolute: a leading separator or drive-qualified path escapes the mod folder outright.
            if (Path.IsPathRooted(behaviorDll))
            {
                rejectReason = "is rooted/absolute (must be a bare filename)";
                return false;
            }

            // 3) Any remaining separator (both platform separators plus the literal forms): a behaviour DLL is
            // a bare filename inside the mod folder, never a sub-path.
            if (behaviorDll.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                behaviorDll.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
                behaviorDll.IndexOf('/') >= 0 ||
                behaviorDll.IndexOf('\\') >= 0)
            {
                rejectReason = "contains a directory separator (must be a bare filename)";
                return false;
            }

            // Defence in depth: even with the checks above, confirm the canonicalized path stays inside the
            // mod folder. Build root with a trailing separator so the prefix test cannot match a sibling
            // folder that merely shares a name prefix (e.g. ".../mod" vs ".../mod-evil").
            string full = Path.GetFullPath(Path.Combine(modFolder, behaviorDll));
            string root = Path.GetFullPath(modFolder);
            string rootWithSep = root;
            if (rootWithSep.Length == 0 ||
                (rootWithSep[rootWithSep.Length - 1] != Path.DirectorySeparatorChar &&
                 rootWithSep[rootWithSep.Length - 1] != Path.AltDirectorySeparatorChar))
            {
                rootWithSep = root + Path.DirectorySeparatorChar;
            }

            if (!string.Equals(full, root, StringComparison.Ordinal) &&
                !full.StartsWith(rootWithSep, StringComparison.Ordinal))
            {
                rejectReason = "canonicalizes outside the mod folder";
                return false;
            }

            resolvedPath = full;
            return true;
        }
    }
}
