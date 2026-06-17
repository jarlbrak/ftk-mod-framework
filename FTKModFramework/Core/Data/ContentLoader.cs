using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// Orchestrates the JSON data pipeline: discover manifest-valid mod folders, parse their content
    /// files, then register each entry through the PUBLIC <c>Content.Add*</c> API. It never registers
    /// rows directly and never re-implements <c>ContentRegistry</c> (spec #6): it only DRIVES the
    /// authoring helpers, exactly as a hand-written content class would.
    ///
    /// P1a is single-phase: collect every entry, then register in one pass. The collect/register split
    /// is the two-phase seam — P1b/P1c (#8/#9) insert a "declare ids" pass before a "resolve references"
    /// pass without reshaping this class. P1a registers immediately in <see cref="RegisterEntry"/>.
    ///
    /// Fault isolation is total: one bad manifest, file, entry, template, or field never aborts the
    /// load. Everything tolerated is recorded on the <see cref="ValidationReport"/> and summarized at
    /// the end via <c>Plugin.Log</c>.
    /// </summary>
    internal static class ContentLoader
    {
        /// <summary>Entry point called from the TableManager.Initialize postfix (after sample content).</summary>
        public static void Load(string contentRoot)
        {
            ValidationReport report = new ValidationReport();

            List<DiscoveredMod> mods = ModDiscovery.Discover(contentRoot, report);
            List<PendingEntry> pending = CollectEntries(mods, report);

            int registered = 0;
            int weaponCount = 0;
            string firstWeaponId = null;
            float firstWeaponMaxDmg = 0f;

            foreach (PendingEntry pe in pending)
            {
                RegisterResult r = RegisterEntry(pe, report);
                if (!r.Registered) continue;
                registered++;
                if (r.IsWeapon)
                {
                    weaponCount++;
                    if (firstWeaponId == null)
                    {
                        firstWeaponId = pe.Entry.Id;
                        firstWeaponMaxDmg = r.WeaponMaxDmg;
                    }
                }
            }

            EmitSelfTest(registered, weaponCount, firstWeaponId, firstWeaponMaxDmg);
            LogSummary(report);
        }

        /// <summary>
        /// Phase-1 collect: parse every discovered mod's files into a flat, ordered work list. Parsing
        /// is fault-tolerant (a malformed file is recorded and skipped). The work list preserves the
        /// deterministic (modGuid, folder, filename, in-file) order so id minting is reproducible.
        /// </summary>
        private static List<PendingEntry> CollectEntries(List<DiscoveredMod> mods, ValidationReport report)
        {
            List<PendingEntry> pending = new List<PendingEntry>();

            foreach (DiscoveredMod mod in mods)
            {
                foreach (string path in mod.ContentFilePaths)
                {
                    ContentFile file = JsonContentParser.ParseFile(path, report);
                    if (file == null) continue; // error already recorded
                    if (file.Entries == null) continue;

                    foreach (ContentEntry entry in file.Entries)
                    {
                        if (entry == null) continue;
                        pending.Add(new PendingEntry(mod.Manifest.ModGuid, path, entry));
                    }
                }
            }

            return pending;
        }

        /// <summary>
        /// Register one entry through the matching public helper. Resolves the <c>template</c> string to
        /// the kind's <c>.ID</c> enum via case-insensitive <c>Enum.Parse</c>, then applies scalar field
        /// overrides inside the helper's configure callback via <see cref="OverrideEngine"/>. Unknown
        /// kind / template / missing id are recorded errors and skip the entry.
        /// </summary>
        private static RegisterResult RegisterEntry(PendingEntry pe, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            string ctx = Context(pe);

            if (IsBlank(entry.Kind)) { report.Error(ctx + ": entry missing 'kind'."); return RegisterResult.Skipped; }
            if (IsBlank(entry.Id)) { report.Error(ctx + ": entry missing 'id'."); return RegisterResult.Skipped; }
            if (IsBlank(entry.Template)) { report.Error(ctx + ": entry '" + entry.Id + "' missing 'template'."); return RegisterResult.Skipped; }

            switch (entry.Kind.ToLowerInvariant())
            {
                case "weapon": return RegisterWeapon(pe, ctx, report);
                default:
                    report.Error(ctx + ": entry '" + entry.Id + "' has unknown kind '" + entry.Kind + "' (skipped).");
                    return RegisterResult.Skipped;
            }
        }

        private static RegisterResult RegisterWeapon(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;

            FTK_itembase.ID template;
            if (!TryParseEnum<FTK_itembase.ID>(entry.Template, out template))
            {
                report.Error(ctx + ": entry '" + entry.Id + "' has unknown weapon template '" + entry.Template + "' (skipped).");
                return RegisterResult.Skipped;
            }

            int appliedFields = 0;
            FTK_weaponStats2 row = Content.AddWeapon(
                pe.ModGuid, entry.Id, template, entry.DisplayName,
                w => { appliedFields = OverrideEngine.Apply(w, entry.Fields, ctx + " '" + entry.Id + "'", report); });

            Plugin.Log.LogInfo("Data: registered weapon '" + entry.Id + "' (template " + template +
                ", " + appliedFields + " field override(s)).");

            return RegisterResult.Weapon(row != null ? row._maxdmg : 0f);
        }

        private static void EmitSelfTest(int registered, int weaponCount, string firstWeaponId, float firstWeaponMaxDmg)
        {
            if (weaponCount > 0 && firstWeaponId != null)
            {
                Plugin.Log.LogInfo("SELF-TEST PASS: data-content loaded " + registered + " entries (" +
                    weaponCount + " weapon '" + firstWeaponId + "' maxdmg=" + firstWeaponMaxDmg + ").");
            }
            else if (registered > 0)
            {
                Plugin.Log.LogInfo("SELF-TEST PASS: data-content loaded " + registered + " entries (no weapons).");
            }
            else
            {
                Plugin.Log.LogInfo("Data: no content entries registered (root empty or all skipped).");
            }
        }

        private static void LogSummary(ValidationReport report)
        {
            Plugin.Log.LogInfo("Data content load complete: " + report.Errors.Count + " error(s), " +
                report.Warnings.Count + " warning(s).");
            foreach (string w in report.Warnings) Plugin.Log.LogWarning("Data warning: " + w);
            foreach (string e in report.Errors) Plugin.Log.LogError("Data error: " + e);
        }

        // --- helpers ---

        private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            try
            {
                result = (TEnum)Enum.Parse(typeof(TEnum), value, true); // ignoreCase: true
                return Enum.IsDefined(typeof(TEnum), result);
            }
            catch (ArgumentException) { result = default(TEnum); return false; }
            catch (OverflowException) { result = default(TEnum); return false; }
        }

        private static string Context(PendingEntry pe)
        {
            return "[" + pe.ModGuid + "] " + System.IO.Path.GetFileName(pe.SourcePath);
        }

        private static bool IsBlank(string s)
        {
            return s == null || s.Trim().Length == 0;
        }

        /// <summary>A parsed entry tagged with the mod guid + source it came from. The unit of work.</summary>
        private sealed class PendingEntry
        {
            public readonly string ModGuid;
            public readonly string SourcePath;
            public readonly ContentEntry Entry;

            public PendingEntry(string modGuid, string sourcePath, ContentEntry entry)
            {
                ModGuid = modGuid;
                SourcePath = sourcePath;
                Entry = entry;
            }
        }

        /// <summary>Outcome of registering one entry, with the bits the self-test needs.</summary>
        private struct RegisterResult
        {
            public bool Registered;
            public bool IsWeapon;
            public float WeaponMaxDmg;

            public static readonly RegisterResult Skipped = new RegisterResult();

            public static RegisterResult Weapon(float maxDmg)
            {
                RegisterResult r = new RegisterResult();
                r.Registered = true;
                r.IsWeapon = true;
                r.WeaponMaxDmg = maxDmg;
                return r;
            }
        }
    }
}
