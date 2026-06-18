using System;
using System.Collections.Generic;
using System.Diagnostics;
using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// Immutable result of one content load: the registered/total entry counts and the elapsed load time,
    /// all taken from the SINGLE existing Stopwatch/count measurement inside <see cref="ContentLoader.Load"/>.
    /// The scale-budget gate reads this directly; there is deliberately no static "last load" field on
    /// ContentLoader (a mutable global would be a co-op/determinism hazard and is forbidden by the spec).
    /// </summary>
    public sealed class LoadResult
    {
        public readonly int RegisteredCount;
        public readonly int TotalCount;
        public readonly long ElapsedMs;

        public LoadResult(int registeredCount, int totalCount, long elapsedMs)
        {
            RegisteredCount = registeredCount;
            TotalCount = totalCount;
            ElapsedMs = elapsedMs;
        }
    }

    /// <summary>
    /// Orchestrates the JSON data pipeline: discover manifest-valid mod folders, parse their content
    /// files, then register each entry through the PUBLIC <c>Content.Add*</c> API. It never registers
    /// rows directly and never re-implements <c>ContentRegistry</c> (spec #6): it only DRIVES the
    /// authoring helpers, exactly as a hand-written content class would.
    ///
    /// P1c is TWO-PHASE (FR-6). A field is a Phase-2 REFERENCE field iff its type is one of the five
    /// content-id enums (or an array of one), see <see cref="OverrideEngine.IsContentIdField"/>; every
    /// other field is a Phase-1 BASE field.
    ///   Phase 1: for each entry (sorted by ordinal (modGuid, id)) call the matching <c>Content.Add*</c>
    ///            applying ONLY base fields, then CACHE the returned live row tagged with its kind.
    ///   Phase 2: for each cached row apply its REFERENCE fields (a custom weapon a class points at may
    ///            be registered by a later file, so references resolve only after every base row exists),
    ///            then attach inline <c>proficiencies</c>, then map flavor/description to Localization.
    /// This makes cross-file references ORDER-INDEPENDENT: the class file may sort before the weapon file
    /// it references and still resolve, because Phase 2 runs after every Phase-1 row is registered.
    ///
    /// The (modGuid, id) pre-sort is the load-order contract positional content depends on: a class
    /// registers at id == array index, so every co-op client MUST mint those indices in the same order
    /// (FR-1/FR-3/FR-8). Fault isolation is total: one bad manifest, file, entry, template, field, or
    /// reference never aborts the load. Everything tolerated is recorded on the <see cref="ValidationReport"/>
    /// and summarized at the end via <c>Plugin.Log</c>.
    /// </summary>
    internal static class ContentLoader
    {
        /// <summary>Entry point called from the TableManager.Initialize postfix (after sample content).</summary>
        public static LoadResult Load(string contentRoot)
        {
            Stopwatch sw = Stopwatch.StartNew();
            ValidationReport report = new ValidationReport();

            List<DiscoveredMod> mods = ModDiscovery.Discover(contentRoot, report);
            List<PendingEntry> pending = CollectEntries(mods, report);

            // Deterministic registration order across machines: sort by ordinal (modGuid, id). This is the
            // load-order contract positional content depends on (a class registers at id == array index),
            // and it is also the determinism contract for the synthetic-id band (FR-1/FR-3/FR-8).
            pending.Sort(CompareEntries);

            // Batch index rebuilds across BOTH phases: ContentRegistry.Register defers each DB's
            // MakeIndex while batching, so registering N rows into one DB costs ONE reindex at
            // EndBatch instead of N (the O(N^2) MakeIndex blow-up at scale). The try/finally is a load
            // invariant: EndBatch MUST run even if a phase throws, so every touched DB is left correctly
            // indexed (fault isolation), and it MUST run before the self-tests read rows by int.
            List<Cached> cached = new List<Cached>();
            ContentRegistry.BeginBatch();
            try
            {
                // --- Phase 1: register base rows, cache them for phase 2 ---
                HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal); // modGuid + "/" + id
                foreach (PendingEntry pe in pending)
                {
                    Cached c = RegisterPhase1(pe, seenIds, report);
                    if (c != null) cached.Add(c);
                }

                // --- Phase 2: resolve cross-file references, attach proficiencies, set localization ---
                foreach (Cached c in cached) ResolvePhase2(c, report);
            }
            finally
            {
                ContentRegistry.EndBatch(); // one MakeIndex per touched DB, before any int-keyed read.
            }

            // Stop AFTER EndBatch so the gated load time INCLUDES the (now single) index build: the
            // scale-budget gate must measure the real end-to-end cost, not a load minus its reindex.
            sw.Stop();

            EmitParitySelfTest(cached);
            EmitDeterminismSelfTest(cached);
            LogSummary(report, cached.Count, pending.Count, sw.ElapsedMilliseconds);

            // Return the SAME measured values the summary just logged: no second Stopwatch, no re-count.
            return new LoadResult(cached.Count, pending.Count, sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// Parse every discovered mod's files into a flat, ordered work list. Parsing is fault-tolerant
        /// (a malformed file is recorded and skipped). The work list preserves the deterministic
        /// (modGuid, folder, filename, in-file) order so id minting is reproducible before the final sort.
        ///
        /// Each discovered mod is REGISTERED into <see cref="ModRegistry"/> first (so a disabled mod still
        /// appears in <c>ModRegistry.Entries</c> and the UI can re-enable it), THEN its files are skipped
        /// when <c>ModRegistry.IsEnabled</c> is false. A disabled mod contributes NO PendingEntry, so the
        /// global (modGuid, id) sort and the id minting that follows see only the surviving set (FR-3/NFR-3).
        /// </summary>
        private static List<PendingEntry> CollectEntries(List<DiscoveredMod> mods, ValidationReport report)
        {
            List<PendingEntry> pending = new List<PendingEntry>();

            foreach (DiscoveredMod mod in mods)
            {
                string modGuid = mod.Manifest.ModGuid;

                // Register BEFORE gating: a disabled mod must still be listed in ModRegistry.Entries.
                ModRegistry.Register(modGuid, mod.Manifest.Name, false, mod.Manifest.Version, true);
                if (!ModRegistry.IsEnabled(modGuid))
                {
                    Plugin.Log.LogInfo("ModRegistry: skipping disabled mod '" + modGuid + "' (no entries loaded).");
                    continue; // disabled: queue none of its files, so nothing reaches the pending work list.
                }

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

        // ===================== PHASE 1 =====================

        /// <summary>
        /// Phase 1: validate the entry, split its fields into base/reference, register the base row via the
        /// kind's public helper (applying ONLY base fields), and cache the live row for Phase 2. A duplicate
        /// id within the same mod is an error and the SECOND entry is skipped (FR-7). Returns null when the
        /// entry was skipped for any reason.
        /// </summary>
        private static Cached RegisterPhase1(PendingEntry pe, HashSet<string> seenIds, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            string ctx = Context(pe);

            if (IsBlank(entry.Kind)) { report.Error(ctx + ": entry missing 'kind'."); return null; }
            if (IsBlank(entry.Id)) { report.Error(ctx + ": entry missing 'id'."); return null; }
            if (IsBlank(entry.Template)) { report.Error(ctx + ": entry '" + entry.Id + "' missing 'template'."); return null; }

            string idKey = pe.ModGuid + "/" + entry.Id;
            if (!seenIds.Add(idKey))
            {
                report.Error(ctx + ": duplicate id '" + entry.Id + "' within mod '" + pe.ModGuid + "' (skipped).");
                return null;
            }

            string entryCtx = ctx + " '" + entry.Id + "'";
            switch (entry.Kind.ToLowerInvariant())
            {
                case "weapon": return RegisterWeapon(pe, entryCtx, report);
                case "item": return RegisterItem(pe, entryCtx, report);
                case "proficiency": return RegisterProficiency(pe, entryCtx, report);
                case "class": return RegisterClass(pe, entryCtx, report);
                case "enemy": return RegisterEnemy(pe, entryCtx, report);
                case "encounter": return RegisterEncounter(pe, entryCtx, report);
                default:
                    report.Error(ctx + ": entry '" + entry.Id + "' has unknown kind '" + entry.Kind + "' (skipped).");
                    return null;
            }
        }

        private static Cached RegisterWeapon(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_itembase.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown weapon template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_weaponStats2), "weapon", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_weaponStats2 row = Content.AddWeapon(pe.ModGuid, entry.Id, template, entry.DisplayName,
                w => { applied = OverrideEngine.ApplyResolved(w, baseFields, ctx, report); });

            Plugin.Log.LogInfo("Data: registered weapon '" + entry.Id + "' (template " + template + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "weapon", row, refFields);
        }

        private static Cached RegisterItem(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_itembase.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown item template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_items), "item", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_items row = Content.AddItem(pe.ModGuid, entry.Id, template, entry.DisplayName,
                it => { applied = OverrideEngine.ApplyResolved(it, baseFields, ctx, report); });

            Plugin.Log.LogInfo("Data: registered item '" + entry.Id + "' (template " + template + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "item", row, refFields);
        }

        private static Cached RegisterProficiency(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_proficiencyTable.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown proficiency template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_proficiencyTable), "proficiency", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_proficiencyTable row = Content.AddProficiency(pe.ModGuid, entry.Id, template, entry.DisplayName,
                p => { applied = OverrideEngine.ApplyResolved(p, baseFields, ctx, report); });

            Plugin.Log.LogInfo("Data: registered proficiency '" + entry.Id + "' (template " + template + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "proficiency", row, refFields);
        }

        /// <summary>
        /// Register a playable CLASS via <see cref="Content.AddClass"/>. The class id == its array index
        /// (positional, load-order-dependent), which is exactly why <see cref="Load"/> sorts the pending
        /// list by (modGuid, id) first. The stat block and other base fields apply now; m_StartWeapon /
        /// m_StartItems are content-id REFERENCES and resolve in Phase 2 (FR-6).
        /// </summary>
        private static Cached RegisterClass(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_playerGameStart.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown class template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_playerGameStart), "class", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_playerGameStart row = Content.AddClass(pe.ModGuid, entry.Id, template, entry.DisplayName,
                c => { applied = OverrideEngine.ApplyResolved(c, baseFields, ctx, report); });

            if (row == null)
            {
                report.Error(ctx + ": failed to register as a class (skipped).");
                return null;
            }

            int id = Content.Db<FTK_playerGameStartDB>().GetIntFromID(entry.Id);
            Plugin.Log.LogInfo("Data: registered class '" + entry.Id + "' (template " + template + ", id/index " + id + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "class", row, refFields);
        }

        private static Cached RegisterEnemy(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_enemyCombat.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown enemy template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_enemyCombat), "enemy", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_enemyCombat row = Content.AddEnemy(pe.ModGuid, entry.Id, template, entry.DisplayName,
                e => { applied = OverrideEngine.ApplyResolved(e, baseFields, ctx, report); });

            Plugin.Log.LogInfo("Data: registered enemy '" + entry.Id + "' (template " + template + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "enemy", row, refFields);
        }

        private static Cached RegisterEncounter(PendingEntry pe, string ctx, ValidationReport report)
        {
            ContentEntry entry = pe.Entry;
            FTK_miniEncounter.ID template;
            if (!TryParseEnum(entry.Template, out template))
            {
                report.Error(ctx + ": unknown encounter template '" + entry.Template + "' (skipped).");
                return null;
            }

            Dictionary<string, object> baseFields, refFields;
            OverrideEngine.Split(typeof(FTK_miniEncounter), "encounter", entry.Fields, ctx, report, out baseFields, out refFields);

            int applied = 0;
            FTK_miniEncounter row = Content.AddEncounter(pe.ModGuid, entry.Id, template, entry.DisplayName,
                en => { applied = OverrideEngine.ApplyResolved(en, baseFields, ctx, report); });

            Plugin.Log.LogInfo("Data: registered encounter '" + entry.Id + "' (template " + template + ", " + applied + " base field(s)).");
            return Cached.Make(pe, "encounter", row, refFields);
        }

        // ===================== PHASE 2 =====================

        /// <summary>
        /// Phase 2 for one cached row: apply its content-id REFERENCE fields (now that every base row
        /// exists, a cross-file reference like a class' m_StartWeapon resolves regardless of file order),
        /// attach any inline <c>proficiencies</c> by kind, then map flavor/description to Localization.
        /// </summary>
        private static void ResolvePhase2(Cached c, ValidationReport report)
        {
            string ctx = c.Context;

            int refs = OverrideEngine.ApplyResolved(c.Row, c.ReferenceFields, ctx, report);
            if (refs > 0) Plugin.Log.LogInfo("Data: resolved " + refs + " reference field(s) on '" + c.Id + "'.");

            AttachProficiencies(c, report);
            ApplyLocalization(c);
        }

        /// <summary>Attach inline <c>proficiencies</c>: weapon -&gt; AttachProficiencies, enemy -&gt; AttachEnemyProficiencies.</summary>
        private static void AttachProficiencies(Cached c, ValidationReport report)
        {
            string[] profs = c.Entry.Proficiencies;
            if (profs == null || profs.Length == 0) return;

            if (c.Kind == "weapon")
            {
                Content.AttachProficiencies((FTK_weaponStats2)c.Row, profs);
            }
            else if (c.Kind == "enemy")
            {
                Content.AttachEnemyProficiencies((FTK_enemyCombat)c.Row, profs);
            }
            else
            {
                report.Warning(c.Context + ": 'proficiencies' is only supported on weapon/enemy, not '" + c.Kind + "' (ignored).");
            }
        }

        /// <summary>Map the entry's flavor/description text to the Localization helper for its kind.</summary>
        private static void ApplyLocalization(Cached c)
        {
            if (c.Kind == "class" && !IsBlank(c.Entry.Flavor))
                Localization.SetClassFlavor(c.Id, c.Entry.Flavor);
            else if (c.Kind == "proficiency" && !IsBlank(c.Entry.Description))
                Localization.SetProficiencyDescription(c.Id, c.Entry.Description);
            else if (c.Kind == "enemy" && !IsBlank(c.Entry.Description))
                Localization.SetEnemyDescription(c.Id, c.Entry.Description);
        }

        // ===================== SELF-TESTS =====================

        /// <summary>
        /// Parity self-test (the #9 grep target): reproduce <c>Content/ThiefClass.cs</c> in JSON minus the
        /// custom Steal MonoBehaviour. Asserts the class' m_StartWeapon resolved CROSS-FILE to a custom
        /// synthetic id, three proficiencies are attached to that weapon, the seeded dangling start-item was
        /// skipped (the rest of m_StartItems still loaded), and the flavor text was registered.
        /// </summary>
        private static void EmitParitySelfTest(List<Cached> cached)
        {
            Cached thief = Find(cached, "class", "sampledata_thief");
            Cached weapon = Find(cached, "weapon", "sampledata_shadowfang");
            if (thief == null || weapon == null) return; // sample data not present: nothing to assert.

            FTK_playerGameStart row = (FTK_playerGameStart)thief.Row;
            int weaponSynthetic = Content.Db<FTK_weaponStats2DB>().GetIntFromID("sampledata_shadowfang");

            bool startWeaponResolved = (int)row.m_StartWeapon == weaponSynthetic && IdAllocator.IsCustom((int)row.m_StartWeapon);
            int startItems = row.m_StartItems != null ? row.m_StartItems.Length : 0;
            // Derive the expectation from the AUTHORED fixture instead of hardcoding a count: kept items ==
            // authored - dangling, where a dangling element resolves to NEITHER a vanilla FTK_itembase.ID
            // name NOR a registered custom id. So the assertion follows the fixture if it ever changes.
            int authoredCount, danglingCount;
            CountAuthoredStartItems(thief.Entry, out authoredCount, out danglingCount);
            bool danglingSkipped = authoredCount > 0 && startItems == authoredCount - danglingCount;

            int profCount = ProficiencyCount((FTK_weaponStats2)weapon.Row);
            bool profsAttached = profCount >= 3;

            string flavor;
            bool flavorSet = Localization.TryGetClassFlavor("sampledata_thief", out flavor) && !IsBlank(flavor);

            bool ok = startWeaponResolved && profsAttached && danglingSkipped && flavorSet;
            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS: data-content ThiefClass parity (startWeapon=" + weaponSynthetic +
                    " resolved cross-file, 3 profs attached, dangling skipped, flavor set)");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [data-parity]: startWeaponResolved=" + startWeaponResolved +
                    " profsAttached=" + profsAttached + " (" + profCount + ") danglingSkipped=" + danglingSkipped +
                    " (startItems=" + startItems + ") flavorSet=" + flavorSet + ".");
        }

        /// <summary>
        /// Determinism self-test (the #9 grep target): for the HASHED kinds (weapon + proficiency, those
        /// minted from IdAllocator's high band, NOT classes which use id == array index), assert the
        /// registered synthetic id equals <c>IdAllocator.Allocate(modGuid, dbType.Name + "/" + id)</c>
        /// computed directly. The expected int is DERIVED from the allocator, never a literal (FR-8).
        /// </summary>
        private static void EmitDeterminismSelfTest(List<Cached> cached)
        {
            int hashedEntries = 0;
            bool allMatch = true;

            foreach (Cached c in cached)
            {
                Type dbType;
                int registered;
                if (!TryHashedRegisteredId(c, out dbType, out registered)) continue; // class/encounter/etc: skip.
                hashedEntries++;

                int expected = IdAllocator.Allocate(c.ModGuid, dbType.Name + "/" + c.Id);
                if (registered != expected)
                {
                    allMatch = false;
                    Plugin.Log.LogError("SELF-TEST FAIL [data-determinism]: '" + c.Id + "' registered=" +
                        registered + " expected=" + expected + " (db " + dbType.Name + ").");
                }
            }

            if (hashedEntries == 0) return; // no hashed content in the loaded mods: nothing to assert.
            if (allMatch)
                Plugin.Log.LogInfo("SELF-TEST PASS: data-content determinism (" + hashedEntries +
                    " hashed entries across weapon+proficiency, synthetic id == IdAllocator.Allocate)");
        }

        /// <summary>
        /// The registered synthetic id for a HASHED-band kind (weapon -&gt; FTK_weaponStats2DB,
        /// proficiency -&gt; FTK_proficiencyTableDB). Returns false for kinds that are not hash-allocated
        /// (class uses id == array index; encounters/enemies/items are out of scope for this test set).
        /// </summary>
        private static bool TryHashedRegisteredId(Cached c, out Type dbType, out int registered)
        {
            dbType = null;
            registered = 0;
            if (c.Kind == "weapon")
            {
                dbType = typeof(FTK_weaponStats2DB);
                registered = Content.Db<FTK_weaponStats2DB>().GetIntFromID(c.Id);
                return true;
            }
            if (c.Kind == "proficiency")
            {
                dbType = typeof(FTK_proficiencyTableDB);
                registered = Content.Db<FTK_proficiencyTableDB>().GetIntFromID(c.Id);
                return true;
            }
            return false;
        }

        // ===================== SUMMARY =====================

        private static void LogSummary(ValidationReport report, int registered, int total, long elapsedMs)
        {
            Plugin.Log.LogInfo("Data content load complete: " + registered + "/" + total + " entries registered, " +
                report.Errors.Count + " error(s), " + report.Warnings.Count + " warning(s), " + elapsedMs + " ms.");
            foreach (string w in report.Warnings) Plugin.Log.LogWarning("Data warning: " + w);
            foreach (string e in report.Errors) Plugin.Log.LogError("Data error: " + e);
        }

        // ===================== HELPERS =====================

        /// <summary>Count distinct proficiencies on a weapon's prefab (the game's own read path).</summary>
        private static int ProficiencyCount(FTK_weaponStats2 weapon)
        {
            if (weapon == null || weapon.m_Prefab == null) return 0;
            // Intentional: instantiate the prefab and read m_ProficiencyEffects off the live Weapon
            // component, i.e. the game's REAL read path, then Destroy the throwaway clone. This is the
            // highest-fidelity count and only runs in the sample-data parity self-test (guarded by the
            // null check above and by the caller only invoking it when sample data is present).
            UnityEngine.GameObject inst = UnityEngine.Object.Instantiate(weapon.m_Prefab);
            int count = 0;
            Weapon w = inst.GetComponentInChildren<Weapon>(true);
            if (w != null && w.m_ProficiencyEffects != null) count = w.m_ProficiencyEffects.Count;
            UnityEngine.Object.Destroy(inst);
            return count;
        }

        private static Cached Find(List<Cached> cached, string kind, string id)
        {
            foreach (Cached c in cached)
                if (c.Kind == kind && c.Id == id) return c;
            return null;
        }

        /// <summary>
        /// Read the AUTHORED start-items array off a class entry and count its elements plus how many are
        /// dangling. The fixture writes the array under the alias "startItems" (raw "m_StartItems"); take
        /// whichever is present. An element is dangling iff it resolves to NEITHER a vanilla
        /// FTK_itembase.ID name (case-insensitive) NOR a registered custom id in the weapon+item DBs. The
        /// elements arrive as a Newtonsoft JArray (or list) of boxed values, so coerce each to a string.
        /// </summary>
        private static void CountAuthoredStartItems(ContentEntry entry, out int authored, out int dangling)
        {
            authored = 0;
            dangling = 0;
            if (entry == null || entry.Fields == null) return;

            object raw;
            if (!entry.Fields.TryGetValue("startItems", out raw) &&
                !entry.Fields.TryGetValue("m_StartItems", out raw))
                return;

            System.Collections.IEnumerable elements = raw as System.Collections.IEnumerable;
            if (elements == null || raw is string) return; // a string is enumerable too; not an array.

            foreach (object element in elements)
            {
                authored++;
                string token = StartItemToken(element);
                if (token == null) { dangling++; continue; }
                int synthetic;
                bool isCustom = ContentRegistry.TryGetSyntheticId(token, out synthetic,
                    typeof(FTK_weaponStats2DB), typeof(FTK_itemsDB));
                if (!IsVanillaItemName(token) && !isCustom) dangling++;
            }
        }

        /// <summary>Unwrap a start-items element (a JValue or scalar box) to its string token.</summary>
        private static string StartItemToken(object element)
        {
            Newtonsoft.Json.Linq.JValue jv = element as Newtonsoft.Json.Linq.JValue;
            object box = jv != null ? jv.Value : element;
            return box == null ? null : box.ToString();
        }

        /// <summary>Case-insensitive: is <paramref name="token"/> a vanilla FTK_itembase.ID name?</summary>
        private static bool IsVanillaItemName(string token)
        {
            FTK_itembase.ID parsed;
            return TryParseEnum(token, out parsed);
        }

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

        /// <summary>
        /// A Phase-1 registered row carried into Phase 2: the live game row, its kind, the entry it came
        /// from, and the alias-resolved REFERENCE (content-id) fields still to apply once every base row
        /// exists.
        /// </summary>
        private sealed class Cached
        {
            public readonly string ModGuid;
            public readonly string Id;
            public readonly string Kind;
            public readonly object Row;
            public readonly ContentEntry Entry;
            public readonly Dictionary<string, object> ReferenceFields;
            public readonly string Context;

            private Cached(PendingEntry pe, string kind, object row, Dictionary<string, object> referenceFields)
            {
                ModGuid = pe.ModGuid;
                Id = pe.Entry.Id;
                Kind = kind;
                Row = row;
                Entry = pe.Entry;
                ReferenceFields = referenceFields;
                Context = "[" + pe.ModGuid + "] " + System.IO.Path.GetFileName(pe.SourcePath) + " '" + pe.Entry.Id + "'";
            }

            public static Cached Make(PendingEntry pe, string kind, object row, Dictionary<string, object> referenceFields)
            {
                if (row == null) return null;
                return new Cached(pe, kind, row, referenceFields);
            }
        }
    }
}
