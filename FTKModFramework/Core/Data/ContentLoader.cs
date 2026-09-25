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
            return LoadInternal(contentRoot, Marketplace.MarketplaceRuntime.Active, false);
        }

        // Publication is intentionally side-effecting. The hot-reload coordinator must hold its
        // quiescent lock and restore the complete definition/resource snapshot on any exception.
        internal static LoadResult LoadCandidate(string contentRoot, Marketplace.ManagedSnapshot target)
        {
            return LoadInternal(contentRoot, target, true);
        }

        private static LoadResult LoadInternal(string contentRoot, Marketplace.ManagedSnapshot managed, bool strict)
        {
            if (!strict && !Marketplace.MarketplaceRuntime.CanDiscover)
            {
                Plugin.Log.LogError("Data discovery skipped: activation helper is still running. Quit and repair the framework.");
                return new LoadResult(0, 0, 0);
            }
            Stopwatch sw = Stopwatch.StartNew();
            ValidationReport report = new ValidationReport();

            List<DiscoveredMod> mods = ModDiscovery.DiscoverAll(contentRoot, managed == null ? null : managed.ContentRoot, report);

            if (strict) ValidateCandidateDiscovery(managed, mods, report);

            // Read persisted enabled states before any external code can execute.
            foreach (DiscoveredMod mod in mods)
                ModRegistry.RegisterDiscovered(mod.Manifest, managed, FindManaged(managed, mod.Manifest.ModGuid));

            // SINGLE behaviour-DLL pre-pass (FR-7): load + reflect + register every mod's behaviorDll behaviours
            // BEFORE any content-registration phase. This is the sequencing invariant the Phase-2 WireBehavior
            // step (#31) depends on: a content entry's behavior:"name" can only resolve modGuid:name once the
            // pre-pass has registered it, so the resolution can never run ahead of registration.
            if (strict)
            {
                foreach (DiscoveredMod mod in mods)
                    if (mod.BehaviorDllPath != null || !string.IsNullOrEmpty(mod.Manifest.BehaviorDll))
                        report.Error("Behavior DLLs cannot participate in hot activation: " + mod.Manifest.ModGuid);
                RequireComplete(report);
            }
            else BehaviorLoader.LoadAll(mods, report);

            List<PendingEntry> pending = CollectEntries(mods, report);

            // Deterministic registration order across machines: sort by ordinal (modGuid, id). This is the
            // load-order contract positional content depends on (a class registers at id == array index),
            // and it is also the determinism contract for the synthetic-id band (FR-1/FR-3/FR-8).
            pending.Sort(CompareEntries);
            if (strict)
            {
                foreach (PendingEntry entry in pending)
                {
                    string kind = (entry.Entry.Kind ?? "").ToLowerInvariant();
                    if (!CandidateKindSupported(kind) ||
                        !string.IsNullOrEmpty(entry.Entry.Behavior) || entry.Entry.PlayerModels != null)
                        report.Error("Unsupported hot activation entry: " + entry.ModGuid + "/" + entry.Entry.Id);
                }
                RequireComplete(report);
            }

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

            // Capability registration validates exact live rows through indexed DB lookups.
            // EndBatch must publish those indexes before this phase. Guardian/modifier capabilities
            // may register their own rows, which are indexed immediately outside the base-row batch.
            Dictionary<string, Marketplace.MarketplaceGenerationFile> verifiedFiles = VerifiedFiles(managed);
            foreach (Cached c in cached) ApplyCapabilities(c, report, verifiedFiles);
            sw.Stop();

            if (strict)
            {
                RequireComplete(report);
                if (cached.Count != pending.Count) throw new InvalidOperationException("Incomplete hot activation registration.");
                return new LoadResult(cached.Count, pending.Count, sw.ElapsedMilliseconds);
            }
            EmitDeterminismSelfTest(cached);
            LogSummary(report, cached.Count, pending.Count, sw.ElapsedMilliseconds);
            Marketplace.MarketplaceRuntime.RecordRegistrationErrors(report);

            // Return the SAME measured values the summary just logged: no second Stopwatch, no re-count.
            return new LoadResult(cached.Count, pending.Count, sw.ElapsedMilliseconds);
        }

        private static void ValidateCandidateDiscovery(Marketplace.ManagedSnapshot managed,
            List<DiscoveredMod> mods, ValidationReport report)
        {
            if (managed != null && (string.IsNullOrEmpty(managed.ContentRoot) ||
                !System.IO.Directory.Exists(managed.ContentRoot))) report.Error("Candidate content directory is missing.");
            int expected = managed == null || managed.Packages == null ? 0 : managed.Packages.Count;
            if (mods.Count != expected) report.Error("Candidate discovery does not match its package lock.");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> packageGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DiscoveredMod mod in mods)
            {
                Marketplace.PackageDescriptor package = FindManaged(managed, mod.Manifest.ModGuid);
                if (!packageGuids.Add(mod.Manifest.ModGuid) || package == null ||
                    managed == null || !mod.Manifest.FolderPath.StartsWith(managed.ContentRoot +
                        System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    mod.Manifest.Version != package.Version || mod.Manifest.CompatibilityReason != null ||
                    !string.IsNullOrEmpty(mod.Manifest.BehaviorDll))
                    report.Error("Candidate contains an unsupported or mismatched package: " + mod.Manifest.ModGuid);
                // Inspect disabled packages too. Enablement must not hide unsupported capabilities.
                foreach (string path in mod.ContentFilePaths)
                {
                    ContentFile file = ParseCandidateFile(path, report);
                    if (file == null || file.Entries == null) { report.Error("Candidate content file has no entries."); continue; }
                    foreach (ContentEntry entry in file.Entries)
                    {
                        if (entry == null) { report.Error("Null candidate entry."); continue; }
                        string kind = (entry.Kind ?? "").ToLowerInvariant();
                        Type tableType = kind == "class" ? typeof(FTK_playerGameStartDB) :
                            kind == "item" ? typeof(FTK_itemsDB) : kind == "weapon" ? typeof(FTK_weaponStats2DB) :
                            kind == "proficiency" ? typeof(FTK_proficiencyTableDB) : null;
                        if (tableType != null && !string.IsNullOrEmpty(entry.Id))
                            foreach (object native in (Array)Reflect.GetField(TableManager.Instance.Get(tableType), "m_Array"))
                                if (string.Equals((string)Reflect.GetField(native, "m_ID"), entry.Id, StringComparison.OrdinalIgnoreCase))
                                    report.Error("Candidate shadows a baseline row: " + entry.Id);
                        if (!CandidateTemplateSupported(kind, entry.Template))
                            report.Error("Unsupported hot activation template: " + entry.Template);
                        if (string.IsNullOrEmpty(entry.Id) || !ids.Add(mod.Manifest.ModGuid + "/" + entry.Id) ||
                            !CandidateKindSupported(kind) ||
                            !string.IsNullOrEmpty(entry.Behavior) || entry.PlayerModels != null)
                            report.Error("Unsupported or duplicate candidate entry: " + entry.Id);
                    }
                }
            }
            RequireComplete(report);
        }

        private static bool CandidateKindSupported(string kind)
        {
            return kind == "class" || kind == "item" || kind == "weapon" || kind == "proficiency";
        }

        private static bool CandidateTemplateSupported(string kind, string template)
        {
            if (string.IsNullOrEmpty(template)) return false;
            FTK_playerGameStart.ID classId;
            FTK_itembase.ID itemId;
            FTK_proficiencyTable.ID proficiencyId;
            return kind == "class" ? TryParseEnum(template, out classId) :
                kind == "item" || kind == "weapon" ? TryParseEnum(template, out itemId) :
                kind == "proficiency" && TryParseEnum(template, out proficiencyId);
        }

        private static ContentFile ParseCandidateFile(string path, ValidationReport report)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<ContentFile>(System.IO.File.ReadAllText(path),
                    new Newtonsoft.Json.JsonSerializerSettings { MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Error });
            }
            catch (Exception error)
            {
                report.Error("Candidate content rejected: " + path + ": " + error.Message);
                return null;
            }
        }

        private static Marketplace.PackageDescriptor FindManaged(Marketplace.ManagedSnapshot managed, string guid)
        {
            if (managed != null && managed.Packages != null)
                foreach (Marketplace.PackageDescriptor package in managed.Packages)
                    if (package.ModGuid == guid) return package;
            return null;
        }

        private static void RequireComplete(ValidationReport report)
        {
            // Existing startup tolerates partial rows and dropped fields. Hot activation cannot.
            if (report.Errors.Count != 0 || report.Warnings.Count != 0)
                throw new InvalidOperationException("Hot activation validation failed: " +
                    string.Join("; ", report.Errors.ToArray()) + "; " + string.Join("; ", report.Warnings.ToArray()));
        }

        /// <summary>
        /// Parse every discovered mod's files into a flat, ordered work list. Parsing is fault-tolerant
        /// (a malformed file is recorded and skipped). The work list preserves the deterministic
        /// (modGuid, folder, filename, in-file) order so id minting is reproducible before the final sort.
        ///
        /// Discovery already registered each mod before the DLL pre-pass (so a disabled mod still
        /// appears in <c>ModRegistry.Entries</c> and the UI can re-enable it). Its files are skipped
        /// when <c>ModRegistry.IsEnabled</c> is false. A disabled mod contributes NO PendingEntry, so the
        /// global (modGuid, id) sort and the id minting that follows see only the surviving set (FR-3/NFR-3).
        /// </summary>
        private static List<PendingEntry> CollectEntries(List<DiscoveredMod> mods, ValidationReport report)
        {
            List<PendingEntry> pending = new List<PendingEntry>();

            foreach (DiscoveredMod mod in mods)
            {
                string modGuid = mod.Manifest.ModGuid;

                if (mod.Manifest.CompatibilityReason != null || !ModRegistry.IsEnabled(modGuid))
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
            if (!string.Equals(entry.Kind, "race", StringComparison.OrdinalIgnoreCase) && IsBlank(entry.Template))
            { report.Error(ctx + ": entry '" + entry.Id + "' missing 'template'."); return null; }

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
                case "race":
                    if (IsBlank(entry.DisplayName) || entry.RaceBindings == null || entry.RaceBindings.Length == 0 ||
                        (entry.Fields != null && entry.Fields.Count != 0))
                    {
                        report.Error(entryCtx + ": a race requires displayName and raceBindings, with no row field overrides.");
                        return null;
                    }
                    try { return Cached.Make(pe, "race", Content.AddRace(pe.ModGuid, entry.Id, entry.DisplayName), null); }
                    catch (Exception e) { report.Error(entryCtx + ": race registration failed: " + e.Message); return null; }
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
            if (c.Kind == "race") return;
            string ctx = c.Context;

            int refs = OverrideEngine.ApplyResolved(c.Row, c.ReferenceFields, ctx, report);
            if (refs > 0) Plugin.Log.LogInfo("Data: resolved " + refs + " reference field(s) on '" + c.Id + "'.");

            WireBehavior(c, report);
            AttachProficiencies(c, report);
            ApplyLocalization(c);
        }

        private static void ApplyCapabilities(Cached c, ValidationReport report,
            Dictionary<string, Marketplace.MarketplaceGenerationFile> verifiedFiles)
        {
            try
            {
                if (c.Entry.RaceBindings != null)
                {
                    if (c.Kind != "race") throw new ArgumentException("raceBindings requires a race");
                    foreach (RaceBindingEntry binding in c.Entry.RaceBindings)
                    {
                        FTK_skinset.ID skinset;
                        if (binding == null || string.IsNullOrEmpty(binding.Class) || binding.Body == null ||
                            !TryParseEnum(binding.Skinset, out skinset)) throw new ArgumentException("invalid race class, skinset or body");
                        FTK_playerGameStart row = Content.Db<FTK_playerGameStartDB>().GetEntryByStringID(binding.Class);
                        if (row == null) throw new ArgumentException("unknown race class '" + binding.Class + "'");
                        PlayerRendererMesh[] body = new PlayerRendererMesh[binding.Body.Length];
                        for (int i = 0; i < body.Length; i++)
                        {
                            ModelRendererEntry entry = binding.Body[i];
                            if (entry == null) throw new ArgumentException("null race body assignment");
                            body[i] = new PlayerRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                        }
                        PlayerApparelMesh[] apparel = new PlayerApparelMesh[binding.Apparel == null ? 0 : binding.Apparel.Length];
                        for (int i = 0; i < apparel.Length; i++)
                        {
                            ModelRendererEntry entry = binding.Apparel[i];
                            if (entry == null) throw new ArgumentException("null race apparel assignment");
                            apparel[i] = new PlayerApparelMesh(entry.Path, entry.NativeMesh, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                        }
                        if (!Content.SetRaceClassBodyMeshesFromGlb((int)c.Row, row, skinset, body, apparel))
                            throw new ArgumentException("race binding registration rejected for '" + binding.Class + "'");
                    }
                }
                if (c.Entry.Guardian && (c.Kind != "class" || !Content.AddGuardian((FTK_playerGameStart)c.Row)))
                    throw new ArgumentException("guardian requires a registered custom class");
                if (c.Entry.Opportunist && (c.Kind != "class" || !Content.AddOpportunist((FTK_playerGameStart)c.Row)))
                    throw new ArgumentException("opportunist requires a registered custom class");
                if (!string.IsNullOrEmpty(c.Entry.PrecisionWeapon) &&
                    (c.Kind != "weapon" || !Content.SetPrecisionWeapon((FTK_weaponStats2)c.Row, c.Entry.PrecisionWeapon)))
                    throw new ArgumentException("precisionWeapon requires a registered physical paired weapon or bow");
                if (!string.IsNullOrEmpty(c.Entry.PrecisionAction) &&
                    (c.Kind != "proficiency" || !Content.SetPrecisionAction((FTK_proficiencyTable)c.Row, c.Entry.PrecisionAction)))
                    throw new ArgumentException("precisionAction requires a registered direct damage proficiency");
                if (!string.IsNullOrEmpty(c.Entry.ThiefArtifact) &&
                    (c.Kind != "weapon" || !Content.SetThiefArtifact((FTK_weaponStats2)c.Row, c.Entry.ThiefArtifact)))
                    throw new ArgumentException("thiefArtifact requires a registered physical precision weapon");
                if (c.Kind == "class" && c.Entry.Proficiencies != null && c.Entry.Proficiencies.Length > 0 &&
                    !Content.AttachClassProficiencies((FTK_playerGameStart)c.Row, c.Entry.Proficiencies))
                    throw new ArgumentException("class proficiency grant rejected; every action must resolve");
                if (c.Kind == "item" && c.Entry.Proficiencies != null && c.Entry.Proficiencies.Length > 0 &&
                    !Content.AttachItemProficiencies((FTK_items)c.Row, c.Entry.Proficiencies))
                    throw new ArgumentException("item proficiency grant requires registered equipment and resolved actions");
                if (c.Entry.RandomDebuffOutcomes != null && c.Entry.ResistanceDamageBonus != null)
                    throw new ArgumentException("randomDebuffOutcomes and resistanceDamageBonus cannot coexist");
                if (c.Entry.RandomDebuffOutcomes != null)
                {
                    if (c.Kind != "proficiency" || !Content.SetRandomDebuffOutcomes((FTK_proficiencyTable)c.Row,
                        ResolveCapabilityProficiencies(c.Entry.RandomDebuffOutcomes)))
                        throw new ArgumentException("randomDebuffOutcomes requires two compatible registered armor/resistance debuffs including itself");
                }
                if (c.Entry.ResistanceDamageBonus != null)
                {
                    ResistanceDamageBonusEntry bonus = c.Entry.ResistanceDamageBonus;
                    if (c.Kind != "proficiency" || !Content.SetResistanceDebuffDamageBonus((FTK_proficiencyTable)c.Row,
                        ResolveCapabilityProficiencies(bonus.Sources), bonus.Multiplier))
                        throw new ArgumentException("resistanceDamageBonus requires a magic action, registered resistance debuffs and a finite multiplier greater than one and at most sixteen");
                }
                if (c.Entry.OverworldAilmentImmunity != null &&
                    (c.Kind != "class" || !Content.AddOverworldAilmentImmunity((FTK_playerGameStart)c.Row,
                        c.Entry.OverworldAilmentImmunity.DisplayName)))
                    throw new ArgumentException("overworldAilmentImmunity requires a registered custom class");
                if (c.Entry.GuardianBonuses != null)
                {
                    if (c.Kind != "item" && c.Kind != "weapon") throw new ArgumentException("guardianBonuses requires equipment");
                    GuardianBonusEntry b = c.Entry.GuardianBonuses;
                    if ((b.GuardFocusRestore > 0 || b.GuardReckoning) && c.Kind != "weapon")
                        throw new ArgumentException("guardFocusRestore and guardReckoning require a weapon");
                    if (!Content.SetGuardianEquipment((FTK_itembase)c.Row, new GuardianEquipmentBonuses(b.GuardHealPercent,
                        b.FocusHealBonusPercent, b.RetaliationDamage, b.WardDebuffs, b.GuardFocusRestore,
                        b.GuardReckoning, b.GuardCleanse))) throw new ArgumentException("guardian bonus registration rejected");
                }
                if (!string.IsNullOrEmpty(c.Entry.Icon))
                {
                    UnityEngine.Sprite icon = PackageIcons.Load(Asset(c, c.Entry.Icon, verifiedFiles));
                    if (c.Kind == "item" || c.Kind == "weapon")
                    {
                        FTK_itembase item = (FTK_itembase)c.Row;
                        item.m_Icon = icon; item.m_IconNonClickable = icon;
                    }
                    else if (c.Kind == "proficiency") ((FTK_proficiencyTable)c.Row).m_BattleButton = icon;
                    else if (c.Kind == "class" && c.Entry.Guardian)
                        Content.Db<FTK_proficiencyTableDB>().GetEntry(GuardianRuntime.ActionId).m_BattleButton = icon;
                    else if (c.Kind == "class" && c.Entry.Opportunist)
                        Content.Db<FTK_proficiencyTableDB>().GetEntry(ThiefRuntime.SlipAwayId).m_BattleButton = icon;
                    else throw new ArgumentException("icon requires equipment, a proficiency or supported class capability");
                }
                if (c.Entry.ApparelModels != null)
                {
                    if (c.Kind != "item") throw new ArgumentException("apparelModels requires an item");
                    ApparelModelEntry a = c.Entry.ApparelModels;
                    FTK_skinset.ID female, male;
                    if (!TryParseEnum(a.FemaleBinding, out female) || !TryParseEnum(a.MaleBinding, out male) || a.Renderers == null)
                        throw new ArgumentException("invalid apparel binding");
                    PlayerApparelMesh[] meshes = new PlayerApparelMesh[a.Renderers.Length];
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        ModelRendererEntry r = a.Renderers[i];
                        meshes[i] = new PlayerApparelMesh(r.Path, r.NativeMesh, Asset(c, r.Model, verifiedFiles), Asset(c, r.Texture, verifiedFiles));
                    }
                    if (!Content.SetItemApparelMeshesFromGlb((FTK_items)c.Row, female, male, meshes)) throw new ArgumentException("item apparel registration rejected");
                }
                if (c.Entry.Modifiers != null)
                {
                    if (c.Kind != "item" && c.Kind != "weapon") throw new ArgumentException("modifiers requires equipment");
                    ItemModifierEntry m = c.Entry.Modifiers;
                    m.Validate();
                    if (Content.SetItemModifiers(c.ModGuid, (FTK_itembase)c.Row, delegate(FTK_characterModifier modifier)
                    {
                        m.Apply(modifier);
                    }) == null) throw new ArgumentException("item modifier registration rejected");
                }
                if (c.Entry.ItemModels != null)
                {
                    if (c.Kind != "item" && c.Kind != "weapon") throw new ArgumentException("itemModels requires equipment");
                    ItemRendererMesh[] meshes = new ItemRendererMesh[c.Entry.ItemModels.Length];
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        ModelRendererEntry entry = c.Entry.ItemModels[i];
                        meshes[i] = new ItemRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                    }
                    if (!Content.SetItemMeshesFromGlb((FTK_itembase)c.Row, meshes)) throw new ArgumentException("item model registration rejected");
                }
                if (c.Entry.OffHandModels != null)
                {
                    if (c.Kind != "weapon") throw new ArgumentException("offHandModels requires a weapon");
                    ItemRendererMesh[] meshes = new ItemRendererMesh[c.Entry.OffHandModels.Length];
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        ModelRendererEntry entry = c.Entry.OffHandModels[i];
                        meshes[i] = new ItemRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                    }
                    if (!Content.SetItemOffHandMeshesFromGlb((FTK_itembase)c.Row, meshes)) throw new ArgumentException("off-hand item model registration rejected");
                }
                if (c.Entry.DisplayModels != null)
                {
                    if (c.Kind != "item" && c.Kind != "weapon") throw new ArgumentException("displayModels requires equipment");
                    ItemRendererMesh[] meshes = new ItemRendererMesh[c.Entry.DisplayModels.Length];
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        ModelRendererEntry entry = c.Entry.DisplayModels[i];
                        meshes[i] = new ItemRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                    }
                    if (!Content.SetItemDisplayMeshesFromGlb((FTK_itembase)c.Row, meshes)) throw new ArgumentException("display model registration rejected");
                }
                if (c.Entry.PlayerModels != null)
                {
                    if (c.Kind != "class") throw new ArgumentException("playerModels requires a class");
                    foreach (PlayerModelEntry model in c.Entry.PlayerModels)
                    {
                        FTK_skinset.ID skinset;
                        if (model == null || !TryParseEnum(model.Skinset, out skinset) || model.Body == null)
                            throw new ArgumentException("invalid player model skinset or body");
                        PlayerRendererMesh[] body = new PlayerRendererMesh[model.Body.Length];
                        for (int i = 0; i < body.Length; i++)
                        {
                            ModelRendererEntry entry = model.Body[i];
                            body[i] = new PlayerRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                        }
                        PlayerApparelMesh[] apparel = new PlayerApparelMesh[model.Apparel == null ? 0 : model.Apparel.Length];
                        for (int i = 0; i < apparel.Length; i++)
                        {
                            ModelRendererEntry entry = model.Apparel[i];
                            apparel[i] = new PlayerApparelMesh(entry.Path, entry.NativeMesh, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                        }
                        if (!Content.SetClassBodyMeshesFromGlb((FTK_playerGameStart)c.Row, skinset, body, apparel))
                            throw new ArgumentException("player model registration rejected");
                        if (model.Backpack != null)
                        {
                            PlayerRendererMesh[] backpack = new PlayerRendererMesh[model.Backpack.Length];
                            for (int i = 0; i < backpack.Length; i++)
                            {
                                ModelRendererEntry entry = model.Backpack[i];
                                backpack[i] = new PlayerRendererMesh(entry.Path, Asset(c, entry.Model, verifiedFiles), Asset(c, entry.Texture, verifiedFiles));
                            }
                            if (!Content.SetClassBackpackMeshesFromGlb((FTK_playerGameStart)c.Row, skinset, backpack))
                                throw new ArgumentException("player backpack registration rejected");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (c.Kind == "race") PlayerRaceRegistry.Disable((int)c.Row);
                report.Error(c.Context + ": capability registration failed: " + e.Message);
            }
        }

        private static string Asset(Cached c, string relativePath,
            Dictionary<string, Marketplace.MarketplaceGenerationFile> verifiedFiles)
        {
            if (string.IsNullOrEmpty(relativePath)) throw new ArgumentException("model and original texture paths are required");
            Marketplace.MarketplaceGenerationFile file;
            if (verifiedFiles != null)
            {
                if (!verifiedFiles.TryGetValue(c.ModGuid + "\n" + relativePath, out file))
                    throw new ArgumentException("Managed asset is absent from the verified generation lock.");
                return PackageModelPaths.RegisterVerified(c.ModGuid, c.PackageRoot, relativePath, file.Sha256, file.Size);
            }
            return PackageModelPaths.Register(c.ModGuid, c.PackageRoot, relativePath);
        }

        private static Dictionary<string, Marketplace.MarketplaceGenerationFile> VerifiedFiles(Marketplace.ManagedSnapshot managed)
        {
            if (managed == null || !managed.FilesVerified) return null;
            Dictionary<string, Marketplace.MarketplaceGenerationFile> result =
                new Dictionary<string, Marketplace.MarketplaceGenerationFile>(StringComparer.Ordinal);
            foreach (Marketplace.PackageDescriptor package in managed.Packages)
            {
                string prefix = package.PackageId + "/";
                foreach (Marketplace.MarketplaceGenerationFile file in managed.Files)
                    if (file != null && file.Path != null && file.Path.StartsWith(prefix, StringComparison.Ordinal))
                        result[package.ModGuid + "\n" + file.Path.Substring(prefix.Length)] = file;
            }
            return result;
        }

        /// <summary>
        /// Wire a custom <c>behavior</c> into a proficiency row's <c>m_ProficiencyPrefab</c> (#31). This is a
        /// DEDICATED step, NOT routed through the field-override engine: <c>behavior</c>/<c>behaviorCategory</c>
        /// are not real FTK_proficiencyTable members (<c>m_ProficiencyPrefab</c> is a ProficiencyBase
        /// reference, not one of the five content-id enum fields), so they must never go through Split's
        /// base/reference partition.
        ///
        /// Timing (decompile-grounded): ProficiencyManager.Start does
        /// <c>Instantiate(row.m_ProficiencyPrefab); .Init(id); cache[id]=clone</c> ONCE, in a combat scene
        /// AFTER TableManager.Initialize (where this loader runs). So setting the prefab here is correctly
        /// BEFORE Start, the same timing the compiled Thief already relies on. There is NO rebuild API, so a
        /// prefab set later would be ignored; setting it now is the only correct moment.
        ///
        /// Fault isolation: a behaviour on a non-proficiency kind, an unresolved (dangling) key, and an
        /// unknown category all WARN and continue. The rest of the entry already loaded in Phase 1.
        /// </summary>
        private static void WireBehavior(Cached c, ValidationReport report)
        {
            if (IsBlank(c.Entry.Behavior)) return; // no behaviour authored: nothing to wire.

            if (c.Kind != "proficiency")
            {
                report.Warning(c.Context + ": 'behavior' is only supported on kind 'proficiency', not '" +
                    c.Kind + "' (ignored).");
                return;
            }

            string key = c.ModGuid + ":" + c.Entry.Behavior;
            Type type;
            BehaviorKind kind;
            if (!BehaviorRegistry.TryResolve(key, out type, out kind))
            {
                // Dangling behaviour reference: the row already loaded its data fields in Phase 1; only the
                // prefab wiring is skipped. Mirrors the dangling-content-id reference policy (warn + continue).
                report.Warning(c.Context + ": behavior '" + c.Entry.Behavior +
                    "' is not registered (dangling; wiring skipped).");
                return;
            }

            // Branch on the resolved kind (closed: proficiency or questlogic). Only the proficiency kind is
            // wired into a proficiency row's m_ProficiencyPrefab; the questlogic kind is a plain
            // (non-MonoBehaviour) object and must NOT be routed through BehaviorHost.Create / AddComponent.
            if (kind != BehaviorKind.Proficiency)
            {
                // No questlogic content is wired into a proficiency row in this slice: the runtime questlogic
                // resolution path (Activator.CreateInstance) lands in #40. Skip cleanly so a future questlogic
                // registration is never mis-hosted as a proficiency prefab.
                report.Warning(c.Context + ": behavior '" + c.Entry.Behavior + "' resolved as kind '" + kind +
                    "', which is not wired into a proficiency row here (questlogic runtime resolution lands in " +
                    "#40; wiring skipped).");
                return;
            }

            ProficiencyBase inst = BehaviorHost.Create(type, "ftkmf_databehavior_" + c.Id);
            if (inst == null)
            {
                report.Warning(c.Context + ": behavior '" + c.Entry.Behavior +
                    "' (" + type.Name + ") could not be hosted (wiring skipped).");
                return;
            }

            // Seed the hosted instance's resting category from behaviorCategory, if authored. A runtime
            // AddComponent instance has empty serialized state (decompile-verified), so m_Category MUST be set
            // imperatively here; otherwise it stays the default. An unknown name warns and leaves the default.
            if (!IsBlank(c.Entry.BehaviorCategory))
            {
                ProficiencyBase.Category category;
                if (TryParseEnum(c.Entry.BehaviorCategory, out category))
                    inst.m_Category = category;
                else
                    report.Warning(c.Context + ": behaviorCategory '" + c.Entry.BehaviorCategory +
                        "' is not a valid ProficiencyBase.Category (left at default).");
            }

            ((FTK_proficiencyTable)c.Row).m_ProficiencyPrefab = inst;
            Plugin.Log.LogInfo("Data: wired behavior '" + c.Entry.Behavior + "' (" + type.Name +
                ") to proficiency '" + c.Id + "'.");
        }

        /// <summary>Attach inline <c>proficiencies</c>: weapon -&gt; AttachProficiencies, enemy -&gt; AttachEnemyProficiencies.</summary>
        private static FTK_proficiencyTable[] ResolveCapabilityProficiencies(string[] ids)
        {
            if (ids == null || ids.Length == 0 || ids.Length > 16) return null;
            FTK_proficiencyTableDB db = Content.Db<FTK_proficiencyTableDB>();
            FTK_proficiencyTable[] rows = new FTK_proficiencyTable[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) return null;
                int id = db.GetIntFromID(ids[i]);
                if (id < 0) return null;
                rows[i] = db.GetEntryByInt(id);
            }
            return rows;
        }

        private static void AttachProficiencies(Cached c, ValidationReport report)
        {
            string[] profs = c.Entry.Proficiencies;
            if (profs == null || profs.Length == 0) return;

            if (c.Kind == "weapon")
            {
                if (c.Entry.ReplaceProficiencies)
                    Content.ReplaceProficiencies((FTK_weaponStats2)c.Row, profs);
                else
                    Content.AttachProficiencies((FTK_weaponStats2)c.Row, profs);
            }
            else if (c.Kind == "enemy")
            {
                Content.AttachEnemyProficiencies((FTK_enemyCombat)c.Row, profs);
            }
            else if (c.Kind == "class" || c.Kind == "item")
            {
                // Exact row validation needs published indexes; ApplyCapabilities runs after EndBatch.
                return;
            }
            else
            {
                report.Warning(c.Context + ": 'proficiencies' is only supported on weapon/enemy/class/item, not '" + c.Kind + "' (ignored).");
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
            public readonly string PackageRoot;

            private Cached(PendingEntry pe, string kind, object row, Dictionary<string, object> referenceFields)
            {
                ModGuid = pe.ModGuid;
                PackageRoot = System.IO.Path.GetDirectoryName(pe.SourcePath);
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
