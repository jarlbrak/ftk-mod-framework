using System;
using System.Collections.Generic;
using System.IO;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// One discovered, manifest-valid mod folder: its <see cref="ModManifest"/> plus the absolute
    /// paths of its content <c>*.json</c> files (manifest.json itself excluded), filename-sorted.
    /// </summary>
    internal sealed class DiscoveredMod
    {
        public readonly ModManifest Manifest;
        public readonly List<string> ContentFilePaths;

        /// <summary>
        /// Resolved ABSOLUTE path of the mod's behaviour DLL (the canonicalized, traversal-guarded
        /// <c>manifest.behaviorDll</c>), or null when the manifest declares none OR the declared value was
        /// rejected by the guard. This slice (#32) only records the path; loading it is #33's job.
        /// </summary>
        public readonly string BehaviorDllPath;

        public DiscoveredMod(ModManifest manifest, List<string> contentFilePaths, string behaviorDllPath)
        {
            Manifest = manifest;
            ContentFilePaths = contentFilePaths;
            BehaviorDllPath = behaviorDllPath;
        }
    }

    /// <summary>
    /// Walks the configured content root for immediate subfolders containing a <c>manifest.json</c>.
    ///
    /// A folder with no <c>manifest.json</c> is skipped and logged at debug level (it is not an error:
    /// the plugins dir holds unrelated BepInEx plugins too). A folder whose manifest is missing a
    /// required field produces a validation error and is skipped; other mods still load.
    ///
    /// Discovery order is DETERMINISTic, sorted by <c>(modGuid, folderName)</c>, so the load order is
    /// identical on every machine regardless of OS directory-enumeration order. Co-op clients must mint
    /// ids in the same order for the synthetic-id band to line up.
    /// </summary>
    internal static class ModDiscovery
    {
        public static List<DiscoveredMod> Discover(string contentRoot, ValidationReport report)
        {
            List<DiscoveredMod> mods = new List<DiscoveredMod>();

            if (!Directory.Exists(contentRoot))
            {
                Plugin.Log.LogWarning("Data content root does not exist: " + contentRoot);
                return mods;
            }

            string[] folders = Directory.GetDirectories(contentRoot);
            Array.Sort(folders, StringComparer.Ordinal); // stable pre-sort; final sort is by (modGuid, folder)

            foreach (string folder in folders)
            {
                string manifestPath = Path.Combine(folder, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Plugin.Log.LogDebug("Skipping '" + folder + "': no manifest.json.");
                    continue;
                }

                ModManifest manifest = ReadManifest(manifestPath, folder, report);
                if (manifest == null) continue;           // malformed manifest JSON: error already recorded
                if (!manifest.Validate(report)) continue; // missing required field: error already recorded

                // RESERVED guid: com.ftkmf.synthetic belongs ONLY to the generator's own reserved subfolder
                // (SyntheticContentGenerator.ReservedSubfolderName). A real data mod declaring it from any
                // other folder is rejected and skipped, so it cannot perturb the deterministic (modGuid, id)
                // sort the synthetic-id band depends on. The generator's own folder passes this check.
                if (string.Equals(manifest.ModGuid, SyntheticContentGenerator.ReservedModGuid, StringComparison.Ordinal) &&
                    !IsReservedSyntheticFolder(folder))
                {
                    report.Error("manifest.json (" + folder + "): modGuid '" + SyntheticContentGenerator.ReservedModGuid +
                        "' is RESERVED for generated synthetic content and may only be used by the '" +
                        SyntheticContentGenerator.ReservedSubfolderName + "' subfolder (mod skipped).");
                    continue;
                }

                List<string> contentFiles = ContentFilesIn(folder);

                // OPTIONAL behaviorDll (FR-3 manifest side, #32): only parsed + shape-validated here; #33
                // owns the actual Assembly.LoadFrom. An absent value is no error and no log. A present but
                // traversal-unsafe value is a validation ERROR, but the mod's CONTENT still loads (the DLL
                // path is just dropped). Determinism is preserved: this neither aborts discovery nor skips
                // the mod, so the (modGuid, id) load order is identical with or without a valid behaviorDll.
                string behaviorDllPath = ResolveBehaviorDll(manifest, folder, report);

                mods.Add(new DiscoveredMod(manifest, contentFiles, behaviorDllPath));
            }

            // Deterministic across machines: order by (modGuid, folder name).
            mods.Sort(CompareMods);
            return mods;
        }

        private static ModManifest ReadManifest(string path, string folder, ValidationReport report)
        {
            try
            {
                string json = File.ReadAllText(path);
                ModManifest manifest = JsonContentParser.Deserialize<ModManifest>(json);
                if (manifest == null)
                {
                    report.Error("manifest.json (" + folder + "): parsed to null (empty or 'null' content).");
                    return null;
                }
                manifest.FolderPath = folder;
                return manifest;
            }
            catch (Exception e)
            {
                report.Error("manifest.json (" + folder + "): malformed JSON: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Resolve the manifest's optional <c>behaviorDll</c> against the mod folder. Returns the
        /// canonicalized absolute path on success, or null when the value is absent (no error, no log) or
        /// rejected by the traversal guard (a validation error is recorded; the mod's content still loads).
        /// </summary>
        private static string ResolveBehaviorDll(ModManifest manifest, string folder, ValidationReport report)
        {
            string declared = manifest.BehaviorDll;
            // Absent: not an error and not logged. The mod simply has no behaviour DLL.
            if (declared == null || declared.Trim().Length == 0) return null;

            string resolvedPath;
            string reason;
            if (!ModManifest.TryResolveBehaviorDll(folder, declared, out resolvedPath, out reason))
            {
                report.Error("manifest.json (" + folder + "): behaviorDll '" + declared +
                    "' rejected (" + reason + "); not loaded.");
                return null; // content still loads; only the DLL path is dropped.
            }

            Plugin.Log.LogInfo("ModDiscovery: '" + manifest.ModGuid + "' behaviorDll resolved to " + resolvedPath);
            return resolvedPath;
        }

        private static List<string> ContentFilesIn(string folder)
        {
            List<string> files = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.json"))
            {
                if (string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(path);
            }
            files.Sort(StringComparer.Ordinal); // deterministic per-mod file order
            return files;
        }

        /// <summary>True iff this folder's leaf name is the reserved synthetic subfolder (the one folder
        /// allowed to declare the reserved synthetic guid).</summary>
        private static bool IsReservedSyntheticFolder(string folder)
        {
            string leaf = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(leaf, SyntheticContentGenerator.ReservedSubfolderName, StringComparison.Ordinal);
        }

        private static int CompareMods(DiscoveredMod a, DiscoveredMod b)
        {
            int byGuid = string.CompareOrdinal(a.Manifest.ModGuid, b.Manifest.ModGuid);
            if (byGuid != 0) return byGuid;
            return string.CompareOrdinal(a.Manifest.FolderPath, b.Manifest.FolderPath);
        }
    }
}
