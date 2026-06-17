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
    /// P1a was single-phase. P1b keeps the collect/register split but SORTS the collected work list by
    /// ordinal <c>(modGuid, id)</c> before registering, so positional content (classes register at
    /// id == array index) lands in IDENTICAL slots on every co-op client regardless of OS enumeration
    /// order (spec FR-1/FR-3/FR-8). The collect/register split is also the two-phase seam — P1c (#9)
    /// inserts a "declare ids" pass before a "resolve references" pass without reshaping this class.
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

            // Deterministic registration order across machines: sort by ordinal (modGuid, id). This is the
            // load-order contract positional content depends on — a class registers at id == array index,
            // so every co-op client MUST mint those indices in the same order (spec FR-1/FR-3/FR-8).
            pending.Sort(CompareEntries);

            int registered = 0;
            int weaponCount = 0;
            string firstWeaponId = null;
            float firstWeaponMaxDmg = 0f;
            ClassResult firstClass = ClassResult.None;

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
                if (r.IsClass && !firstClass.Present) firstClass = r.ClassInfo;
            }

            EmitSelfTest(registered, weaponCount, firstWeaponId, firstWeaponMaxDmg);
            EmitClassSelfTest(firstClass);
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
                case "class": return RegisterClass(pe, ctx, report);
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
                w => { appliedFields = OverrideEngine.Apply(w, "weapon", entry.Fields, ctx + " '" + entry.Id + "'", report); });

            Plugin.Log.LogInfo("Data: registered weapon '" + entry.Id + "' (template " + template +
                ", " + appliedFields + " field override(s)).");

            return RegisterResult.Weapon(row != null ? row._maxdmg : 0f);
        }

        /// <summary>
        /// Register a playable CLASS via <see cref="Content.AddClass"/>. The class id == its array index
        /// (positional, load-order-dependent) — which is exactly why <see cref="Load"/> sorts the pending
        /// list by (modGuid, id) first. Fields (the stat block, m_StartItems, m_CharacterSkills, the
        /// primary stat, ...) are resolved by <see cref="OverrideEngine"/>; aliases map friendly names
        /// (strength -&gt; _toughness, skills -&gt; m_CharacterSkills, ...) to the real fields.
        /// </summary>
        private static RegisterResult RegisterClass(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;

            FTK_playerGameStart.ID template;
            if (!TryParseEnum<FTK_playerGameStart.ID>(entry.Template, out template))
            {
                report.Error(ctx + ": entry '" + entry.Id + "' has unknown class template '" + entry.Template + "' (skipped).");
                return RegisterResult.Skipped;
            }

            int appliedFields = 0;
            FTK_playerGameStart row = Content.AddClass(
                pe.ModGuid, entry.Id, template, entry.DisplayName,
                c => { appliedFields = OverrideEngine.Apply(c, "class", entry.Fields, ctx + " '" + entry.Id + "'", report); });

            if (row == null)
            {
                report.Error(ctx + ": entry '" + entry.Id + "' failed to register as a class (skipped).");
                return RegisterResult.Skipped;
            }

            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();
            int id = db.GetIntFromID(entry.Id);
            int lastIndex = ((Array)Reflect.GetField(db, "m_Array")).Length - 1;

            Plugin.Log.LogInfo("Data: registered class '" + entry.Id + "' (template " + template +
                ", id/index " + id + ", " + appliedFields + " field override(s)).");

            return RegisterResult.Class(new ClassResult(entry.Id, id, lastIndex, row));
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

        /// <summary>
        /// Prove the data-class path end to end: the class registered via Content.AddClass at id == array
        /// index; a stat resolved; a content-id array element (m_StartItems[0]) resolved to a real int id;
        /// a nested skill bool set; and that a value set by ALIAS equals the same value set by the RAW
        /// field name (the test class sets _toughness via the 'strength' alias and _awareness via the raw
        /// field, both to the same number, so equality proves alias and raw share one resolution path).
        /// </summary>
        private static void EmitClassSelfTest(ClassResult c)
        {
            if (!c.Present) return; // no data class in the loaded mods: nothing to assert.

            FTK_playerGameStart row = c.Row;
            bool idIsIndex = c.Id == c.LastIndex;
            float strength = row._toughness;
            bool aliasEqualsRaw = row._toughness == row._awareness; // alias 'strength' vs raw '_awareness'

            int startItemCount = row.m_StartItems != null ? row.m_StartItems.Length : 0;
            bool firstItemResolved = startItemCount > 0 && row.m_StartItems[0] != FTK_itembase.ID.None;
            bool sneak = row.m_CharacterSkills != null && row.m_CharacterSkills.m_Sneak;

            bool ok = idIsIndex && firstItemResolved && sneak && aliasEqualsRaw;
            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS: data-content class '" + c.ContentId + "' at index " + c.Id +
                    " (str=" + strength + ", startItems=" + startItemCount + " resolved, skills.Sneak=true, alias strength==raw).");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [data-class]: id=" + c.Id + " lastIndex=" + c.LastIndex +
                    " idIsIndex=" + idIsIndex + " firstItemResolved=" + firstItemResolved +
                    " sneak=" + sneak + " aliasEqualsRaw=" + aliasEqualsRaw + ".");
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

        /// <summary>
        /// Deterministic ordinal sort key for the work list: (modGuid, id). Identical on every machine,
        /// so positional content (classes register at id == array index) gets the same slot everywhere.
        /// A null id sorts before a non-null one; both null are equal (the register loop skips them).
        /// </summary>
        private static int CompareEntries(PendingEntry a, PendingEntry b)
        {
            int byGuid = string.CompareOrdinal(a.ModGuid, b.ModGuid);
            if (byGuid != 0) return byGuid;
            return string.CompareOrdinal(a.Entry.Id, b.Entry.Id);
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
            public bool IsClass;
            public ClassResult ClassInfo;

            public static readonly RegisterResult Skipped = new RegisterResult();

            public static RegisterResult Weapon(float maxDmg)
            {
                RegisterResult r = new RegisterResult();
                r.Registered = true;
                r.IsWeapon = true;
                r.WeaponMaxDmg = maxDmg;
                return r;
            }

            public static RegisterResult Class(ClassResult c)
            {
                RegisterResult r = new RegisterResult();
                r.Registered = true;
                r.IsClass = true;
                r.ClassInfo = c;
                return r;
            }
        }

        /// <summary>The bits the class self-test reads: id/index match + the live row to probe fields on.</summary>
        private struct ClassResult
        {
            public bool Present;
            public string ContentId;
            public int Id;
            public int LastIndex;
            public FTK_playerGameStart Row;

            public static readonly ClassResult None = new ClassResult();

            public ClassResult(string contentId, int id, int lastIndex, FTK_playerGameStart row)
            {
                Present = true;
                ContentId = contentId;
                Id = id;
                LastIndex = lastIndex;
                Row = row;
            }
        }
    }
}
