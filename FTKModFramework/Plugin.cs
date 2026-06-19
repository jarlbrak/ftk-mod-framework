using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using FullSerializer;
using GridEditor;
using FTKModFramework.Core;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Diagnostics;

namespace FTKModFramework
{
    [BepInPlugin(Guid, Name, Version)]
    // NOTE: intentionally no [BepInProcess(...)]. The process is "FTK.exe" on Windows but "FTK"
    // on macOS/Linux; gating on one name would stop the plugin loading cross-platform. We only
    // ever install this into FTK's BepInEx/plugins, so the gate adds nothing anyway.
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ftkmf.framework";
        public const string Name = "FTK Mod Framework";
        public const string Version = "0.1.0";

        public static Plugin Instance;
        public static ManualLogSource Log;

        /// <summary>
        /// Whether to register the bundled example content (Emberbrand weapon + Ember Lash ability,
        /// added to the Blacksmith's starting kit). Off = the framework only powers other mods.
        /// </summary>
        public static ConfigEntry<bool> EnableSampleContent;

        /// <summary>
        /// DEBUG verification aid: replace every overworld LAND enemy the game spawns with the custom
        /// "Cutpurse" so enemy injection is immediately visible in combat. Turn off for normal play.
        /// </summary>
        public static ConfigEntry<bool> ForceCustomEnemy;

        /// <summary>
        /// DEBUG verification aid: replace every overworld encounter the game spawns with the custom
        /// "Smuggler's Cache" so injection is immediately visible in a normal run. Turn off for normal play.
        /// </summary>
        public static ConfigEntry<bool> ForceCustomEncounter;

        /// <summary>
        /// Whether to run the JSON data-content loader: discover mod folders under
        /// <see cref="DataContentRoot"/>, parse their content files, and register them through the
        /// public Content.* API. Independent of <see cref="EnableSampleContent"/>: disabling the bundled
        /// demo never disables third-party data mods.
        /// </summary>
        public static ConfigEntry<bool> EnableDataContent;

        /// <summary>
        /// Whether to run the external-DLL behaviour pre-pass (#33): for each discovered mod that declares a
        /// behaviorDll, Assembly.LoadFrom + reflect + register its [ContentBehavior] proficiencies. Gates ONLY
        /// the external-DLL pre-pass; the in-assembly Phase-1 behaviours (FrameworkBehaviors /
        /// com.ftkmf.sampledata:Steal) are registered separately and are NOT affected. Default on; inert when
        /// no mod declares a behaviorDll. Set false to skip all Assembly.LoadFrom work (0 DLL behaviours loaded).
        /// </summary>
        public static ConfigEntry<bool> EnableBehaviorLoading;

        /// <summary>
        /// Master switch for the campaign engine (#43, spec #37 P4): the flag-conditioned branch router
        /// (<see cref="QuestRouterPatch"/>, #42) and the custom-objective-verb resolver
        /// (<see cref="QuestVerbResolverPatch"/>, #40). Both patches are PREPARE-gated on this flag, so when it is
        /// false NEITHER patch is installed and behavior is provably identical to a router-free / resolver-free
        /// build. When true (the default) the patches install but stay inert on any vanilla quest (no sidecar
        /// entry / no ModQuestDef), so ON == today's behavior. Default ON, mirroring EnableBehaviorLoading (an
        /// engine feature that is inert until content uses it); set false to disable the campaign engine entirely.
        /// The flag store accessors (Campaign.SetFlag/GetFlag/EnsureStore) are unaffected: they are already inert
        /// without campaign content. Also gates the load-time QuestValidator pre-pass.
        /// </summary>
        public static ConfigEntry<bool> EnableCampaignEngine;

        /// <summary>
        /// Folder the data loader scans for mod subfolders (each with a manifest.json). Defaults to
        /// BepInEx's plugins dir, so dropping a content-mod folder in alongside plugins just works.
        /// </summary>
        public static ConfigEntry<string> DataContentRoot;

        // ---- Diagnostics: scale-and-performance gate (P5a, #22) ----------------------------------------
        // The gate measures one content load against a persisted calibration baseline + tunable budgets and
        // emits exactly one SCALE-BUDGET line. The five budget fields are calibrated later; the values here
        // are conservative starting points (small load floor, 64 MiB memory floor, 2x headroom).

        /// <summary>Master switch for the scale-budget gate. When false, NO SCALE-BUDGET line is emitted.</summary>
        public static ConfigEntry<bool> DiagnosticsEnableGate;

        /// <summary>Directory the baseline JSON is written to / read from (relative paths root at the game folder).</summary>
        public static ConfigEntry<string> DiagnosticsOutputDirectory;

        /// <summary>Load budget = max(baselineLoadMs * this, LoadMsAbsoluteFloorMs).</summary>
        public static ConfigEntry<double> DiagnosticsLoadMsHeadroomMultiplier;

        /// <summary>Absolute floor for the load budget, so a fast vanilla load never trips the gate.</summary>
        public static ConfigEntry<long> DiagnosticsLoadMsAbsoluteFloorMs;

        /// <summary>Memory budget = max(baselineHeapBytes * this, MemoryAbsoluteFloorBytes).</summary>
        public static ConfigEntry<double> DiagnosticsMemoryHeadroomMultiplier;

        /// <summary>Absolute floor for the memory budget in bytes (64 MiB default).</summary>
        public static ConfigEntry<long> DiagnosticsMemoryAbsoluteFloorBytes;

        /// <summary>Per-high-band-id save-size footprint in bytes; the save-size PROXY's per-entry cost.</summary>
        public static ConfigEntry<long> DiagnosticsSaveSizePerEntryBytes;

        /// <summary>
        /// One-shot recalibration switch (P5c, #24). When true the gate re-measures and OVERWRITES the
        /// scale-budget baseline on that run (emitting CALIBRATED), then the operator sets it back to false
        /// for normal PASS/FAIL gating. The gate never auto-resets this flag in the config file; a normal run
        /// (flag false) never auto-updates the baseline.
        /// </summary>
        public static ConfigEntry<bool> DiagnosticsRecalibrateBaseline;

        // ---- Diagnostics: synthetic content generator (P5b, #23) ---------------------------------------
        // DEV/STRESS only. When count > 0, the generator writes N throwaway synthetic entries under a reserved
        // subfolder of DataContentRoot BEFORE the data load, so the single existing ContentLoader.Load pass
        // registers them through the public Content.* API and the scale-budget gate measures the load at scale.
        // Default 0 = a true no-op (and any stale reserved subfolder is removed). See SyntheticContentGenerator.

        /// <summary>DEBUG/stress: number of throwaway synthetic content entries to generate. 0 = off.</summary>
        public static ConfigEntry<int> SyntheticContentCount;

        /// <summary>Kind for every generated synthetic entry (default "weapon").</summary>
        public static ConfigEntry<string> SyntheticContentKind;

        /// <summary>Template (vanilla row to clone) for every generated synthetic entry (default "bladeDagger").</summary>
        public static ConfigEntry<string> SyntheticContentTemplate;

        // ---- Diagnostics: synthetic CAMPAIGN scale gate (P5, #45) --------------------------------------
        // The campaign analogue of the data-content scale gate. It authors a synthetic multi-stage campaign at
        // a scale target through the PUBLIC campaign builder, then gates load-time, memory, and the REAL
        // serialized save-size (campaigns bypass IdAllocator, so the data gate's IdAllocator proxy captures none
        // of their growth, spec #37 NFR-1). The two stage/quest binds default to 0 = "use the fixed default scale
        // target" so the gate is OBSERVABLE by default (deterministic smoke line) yet inert beyond that fixed
        // target until a tester raises it; the three budget binds are ABSOLUTE (the baseline model does not map to
        // a one-shot campaign authoring or to real-bytes save-size). All five are read by CampaignScaleBudgetGate.

        /// <summary>Stages in the synthetic scale-probe campaign. 0 = use the fixed default target (20).</summary>
        public static ConfigEntry<int> SyntheticCampaignStages;

        /// <summary>Quests per stage in the synthetic scale-probe campaign. 0 = use the fixed default target (25).</summary>
        public static ConfigEntry<int> SyntheticCampaignQuestsPerStage;

        /// <summary>Absolute load-time budget (ms) for authoring the synthetic campaign at scale.</summary>
        public static ConfigEntry<long> CampaignLoadMsBudget;

        /// <summary>Absolute memory budget (bytes) for the heap delta the synthetic campaign adds.</summary>
        public static ConfigEntry<long> CampaignMemoryBytesBudget;

        /// <summary>Absolute save-size budget (bytes) for the synthetic campaign's REAL serialized m_FullFileData.</summary>
        public static ConfigEntry<long> CampaignSaveSizeBytesBudget;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            EnableSampleContent = Config.Bind("Demo", "EnableSampleContent", true,
                "Register the bundled example content (a custom weapon + ability, given to the Blacksmith). " +
                "Set false if you only want the framework as a dependency for other content mods.");

            ForceCustomEnemy = Config.Bind("Enemies", "ForceCustomEnemy", false,
                "DEBUG: replace every overworld LAND enemy that spawns with the custom 'Cutpurse' so enemy " +
                "injection is immediately visible in combat. Set false for normal play.");

            ForceCustomEncounter = Config.Bind("Adventures", "ForceCustomEncounter", false,
                "DEBUG: replace every overworld encounter that spawns with the custom 'Smuggler's Cache' so " +
                "encounter injection is immediately visible in-game. Set false for normal play.");

            EnableDataContent = Config.Bind("Data", "EnableDataContent", true,
                "Run the JSON data-content loader (discovers content-mod folders under DataContentRoot and " +
                "registers their content). Independent of EnableSampleContent.");

            EnableBehaviorLoading = Config.Bind("Data", "EnableBehaviorLoading", true,
                "Run the external-DLL behaviour pre-pass: for each mod that declares a behaviorDll, load + " +
                "reflect + register its [ContentBehavior] proficiencies. Gates ONLY the external-DLL pre-pass; " +
                "the in-assembly behaviours (FrameworkBehaviors / com.ftkmf.sampledata:Steal) are unaffected. " +
                "Default on; inert when no mod declares a behaviorDll. Set false to skip all Assembly.LoadFrom " +
                "work (0 DLL behaviours loaded).");

            EnableCampaignEngine = Config.Bind("Campaign", "EnableCampaignEngine", true,
                "Master switch for the campaign engine: the flag-conditioned branch router (Postfix on " +
                "GameDefinition.GetNextQuest) and the custom-objective-verb resolver (Prefix on " +
                "QuestLogicBase.GetQuestLogicTypeInstanceFromQuestDef). Both patches are Prepare-gated on this " +
                "flag, so false => NEITHER is installed (provably identical to a build without them). Default on, " +
                "mirroring EnableBehaviorLoading; inert on any vanilla quest even when on. Also gates the " +
                "load-time campaign QuestValidator pre-pass.");

            DataContentRoot = Config.Bind("Data", "DataContentRoot", Paths.PluginPath,
                "Folder scanned for content-mod subfolders (each with a manifest.json). Defaults to the " +
                "BepInEx plugins directory.");

            DiagnosticsEnableGate = Config.Bind("Diagnostics", "EnableScaleBudgetGate", true,
                "Measure each content load against a calibration baseline and budgets, emitting one " +
                "SCALE-BUDGET line. First run with no baseline writes one and emits CALIBRATED. " +
                "Set false to emit no SCALE-BUDGET line at all.");

            DiagnosticsOutputDirectory = Config.Bind("Diagnostics", "OutputDirectory", "BepInEx/FTKPerfProbe",
                "Folder for the scale-baseline.json calibration file. Relative paths root at the game " +
                "folder (so the default lands beside the other BepInEx folders).");

            // The five budget fields below are deliberately conservative; they are calibrated against real
            // content later. Multipliers give headroom over the calibrated baseline; the floors keep a small
            // vanilla load (fast, low-memory) from ever tripping the gate.
            DiagnosticsLoadMsHeadroomMultiplier = Config.Bind("Diagnostics", "LoadMsHeadroomMultiplier", 2.0,
                "Load budget = max(baselineLoadMs * this, LoadMsAbsoluteFloorMs).");

            DiagnosticsLoadMsAbsoluteFloorMs = Config.Bind("Diagnostics", "LoadMsAbsoluteFloorMs", 1000L,
                "Absolute floor (ms) for the load budget, so a fast vanilla load never trips the gate.");

            DiagnosticsMemoryHeadroomMultiplier = Config.Bind("Diagnostics", "MemoryHeadroomMultiplier", 2.0,
                "Memory budget = max(baselineHeapBytes * this, MemoryAbsoluteFloorBytes).");

            DiagnosticsMemoryAbsoluteFloorBytes = Config.Bind("Diagnostics", "MemoryAbsoluteFloorBytes", 67108864L,
                "Absolute floor (bytes) for the memory budget. Default 67108864 = 64 MiB.");

            DiagnosticsSaveSizePerEntryBytes = Config.Bind("Diagnostics", "SaveSizePerEntryBudgetBytes", 64L,
                "Per-high-band-id footprint (bytes) for the save-size PROXY (a registration-footprint " +
                "estimate, not a real save measurement).");

            DiagnosticsRecalibrateBaseline = Config.Bind("Diagnostics", "RecalibrateBaseline", false,
                "Set true for ONE run to re-measure and overwrite the scale-budget baseline (emits CALIBRATED), " +
                "then set it back to false for normal PASS/FAIL gating. A normal run never auto-updates the baseline. " +
                "Recalibrate with custom/synthetic content DISABLED so the baseline anchors on vanilla load only.");

            SyntheticContentCount = Config.Bind("Diagnostics", "SyntheticContentCount", 0,
                "DEBUG/stress: generate this many throwaway synthetic content entries under DataContentRoot " +
                "before the data load, to exercise the scale-budget gate. 0 = off (no synthetic content).");

            SyntheticContentKind = Config.Bind("Diagnostics", "SyntheticContentKind", "weapon",
                "Kind for each generated synthetic entry (weapon clones land in the save-proxy's high band).");

            SyntheticContentTemplate = Config.Bind("Diagnostics", "SyntheticContentTemplate", "bladeDagger",
                "Template (vanilla row to clone) for each generated synthetic entry.");

            SyntheticCampaignStages = Config.Bind("Diagnostics", "SyntheticCampaignStages", 0,
                "P5 campaign scale gate: stages in the synthetic scale-probe campaign. 0 = use the fixed default " +
                "target (20 stages). Capped at 200. The gate authors stages x questsPerStage quests through the " +
                "public campaign builder and gates load/memory/real-save-size.");

            SyntheticCampaignQuestsPerStage = Config.Bind("Diagnostics", "SyntheticCampaignQuestsPerStage", 0,
                "P5 campaign scale gate: quests per stage in the synthetic scale-probe campaign. 0 = use the fixed " +
                "default target (25 per stage). Capped at 200. Default 20x25 = 500 quests = 250x vanilla DungeonCrawl (2).");

            CampaignLoadMsBudget = Config.Bind("Diagnostics", "CampaignLoadMsBudget", 3000L,
                "P5 campaign scale gate: ABSOLUTE load-time budget (ms) for authoring+deserializing+validating " +
                "the synthetic campaign at scale (the baseline model does not map to a one-shot campaign authoring).");

            CampaignMemoryBytesBudget = Config.Bind("Diagnostics", "CampaignMemoryBytesBudget", 67108864L,
                "P5 campaign scale gate: ABSOLUTE memory budget (bytes, 64 MiB default) for the heap delta the " +
                "synthetic campaign adds (GC.GetTotalMemory(true) before/after, matching the data gate's figure).");

            CampaignSaveSizeBytesBudget = Config.Bind("Diagnostics", "CampaignSaveSizeBytesBudget", 4194304L,
                "P5 campaign scale gate: ABSOLUTE save-size budget (bytes, 4 MiB default) for the REAL serialized " +
                "campaign save artifact (m_FullFileData byte length), NOT the IdAllocator proxy (campaigns are " +
                "string-keyed and bypass IdAllocator, spec #37 NFR-1).");

            // Save-safety: synthetic enum ids must round-trip through saves as their int value.
            // The save-size proxy depends on this invariant holding (ids persist as ints).
            fsConfig.SerializeEnumsAsInteger = true;

            _harmony = new Harmony(Guid);
            DbLookupPatcher.Init(_harmony);
            _harmony.PatchAll();

            Log.LogInfo(Name + " " + Version + " loaded (For The King / Unity 2017.2.2p2 / Mono).");
        }
    }

    /// <summary>
    /// Single content entry point. By the time TableManager.Initialize() returns, every
    /// FTK_*DB table is populated, so this is the one safe place to inject custom content.
    /// </summary>
    [HarmonyPatch(typeof(TableManager), "Initialize")]
    internal static class TableManager_Initialize_Patch
    {
        private static bool _done;

        private static void Postfix()
        {
            if (_done) return; // Initialize can be reached more than once; only seed content once.
            _done = true;

            // Register the bundled-demo row UNCONDITIONALLY, before its gate is read. EnableSampleContent.Value
            // backs the row's Enabled state (so a disabled demo stays listed and re-enableable); registration
            // itself never depends on that value. Doing this before the gate is what stops the FR-3 fail-open
            // default from silently re-enabling sample content the user turned off.
            ModRegistry.Register(Plugin.Guid, "Bundled Sample Content", true, null, Plugin.EnableSampleContent.Value);

            // Bundled demo content (opt-in via the gate). Disabling it must NOT skip the data loader below.
            if (ModRegistry.IsEnabled(Plugin.Guid))
            {
                Run("sample weapon/ability", SampleContent.Register);
                Run("thief class", ThiefClass.Register);
                Run("cutpurse enemy", CutpurseEnemy.Register);
                Run("sample encounter + adventure", AdventureContent.Register);
            }

            // Behaviour primitives self-test (P3, #29). Runs UNCONDITIONALLY (independent of EnableSampleContent):
            // it only exercises its own throwaway keys/types and proves BehaviorRegistry + BehaviorHost work.
            Run("behavior primitives", BehaviorSelfTest.Run);

            // behaviorDll path-traversal guard self-test (P3, #32). Runs UNCONDITIONALLY: it exercises the
            // manifest guard as a pure function (no filesystem, no game state), proving '..'/separator/
            // absolute values are rejected and a bare filename resolves under the mod root.
            Run("behavior dll guard", BehaviorDllGuardSelfTest.Run);

            // Synthetic stress content (P5b, #23). Runs ALWAYS, BEFORE the data load, so a count-0 run still
            // clears a stale reserved subfolder a prior higher-N run may have left. When count > 0 it writes N
            // synthetic entries into the reserved subfolder under DataContentRoot; the single existing
            // ContentLoader.Load(DataContentRoot) pass below then discovers and registers them. NOTE: if
            // EnableDataContent is false the loader does not run, so the synthetic mod is written/cleared but
            // not registered; the stress workflow assumes EnableDataContent=true (the default).
            Run("synthetic content", () => SyntheticContentGenerator.Generate(
                Plugin.DataContentRoot.Value,
                Plugin.SyntheticContentCount.Value,
                Plugin.SyntheticContentKind.Value,
                Plugin.SyntheticContentTemplate.Value));

            // Framework-shipped behaviours (#31). Runs UNCONDITIONALLY (independent of EnableSampleContent)
            // and BEFORE the data loader, so the bundled-demo behaviour key (com.ftkmf.sampledata:Steal) is
            // present when the loader resolves the demo fixture's behavior:"Steal". This is the in-assembly
            // demo path that lets the shipped sampledata fixture drop the "minus the MonoBehaviour" caveat;
            // real third-party mods supply behaviours via their own DLL under their own guid (#33/#34).
            Run("framework behaviors", FrameworkBehaviors.Register);

            // JSON data-content mods (opt-in, independent of the demo). Runs AFTER sample content so a
            // data mod can reference vanilla rows the same way the demo does. ContentLoader registers each
            // discovered mod into ModRegistry, so the summary below sees data mods too. The LoadResult is
            // captured in a LOCAL (not a static "last load" field on ContentLoader, which the spec forbids)
            // so the scale-budget gate can read the single existing measurement.
            LoadResult loadResult = null;
            if (Plugin.EnableDataContent.Value)
                Run("data content", () => { loadResult = LoadDataContent(); });
            else
                Plugin.Log.LogInfo("ModRegistry: data discovery skipped (EnableDataContent=false).");

            // Scale-and-performance gate (#22). Runs once, after the data load, inside this same _done guard
            // via Run(...) so a throw is caught and the load continues. loadResult is null when data content
            // was disabled; the gate then captures metrics with registered/elapsed = 0 and still emits its
            // one SCALE-BUDGET line (one line per content load). When the gate itself is disabled it emits none.
            Run("scale budget", () => ScaleBudgetGate.Evaluate(loadResult));

            // Campaign scale gate (#45). Sibling of the data-content gate above: it authors a SYNTHETIC multi-stage
            // campaign at a fixed scale target (default 20x25 = 500 quests, 250x vanilla) through the PUBLIC campaign
            // builder, then gates load-time, memory, and the REAL serialized save-size. It runs INDEPENDENTLY of
            // EnableDataContent (campaigns are not part of the data load) and of EnableSampleContent (it registers its
            // OWN reserved scale-probe adventure), gated only by DiagnosticsEnableGate so the smoke log shows a
            // deterministic SCALE-BUDGET [campaign] + SELF-TEST PASS [campaign-scale] line without config edits. The
            // data gate's IdAllocator save-proxy captures ZERO campaign growth (string-keyed quests, NFR-1), so this
            // gate measures the campaign's own m_FullFileData bytes and asserts CustomIdCount is unchanged.
            Run("campaign scale budget", CampaignScaleBudgetGate.Evaluate);

            // Emit AFTER both registration sites so N counts the demo + every discovered data mod. Kept in the
            // postfix (not inside ContentLoader.Load) so it still fires when EnableDataContent is false.
            LogModRegistrySummary();
        }

        /// <summary>
        /// FR-8 observability: a single "ModRegistry: N mods, M enabled" line over the whole registry, plus an
        /// info line per disabled row. Runs once, after both registration sites have populated the registry.
        /// </summary>
        private static void LogModRegistrySummary()
        {
            int total = 0;
            int enabled = 0;
            foreach (ModEntry e in ModRegistry.Entries)
            {
                total++;
                if (e.Enabled) enabled++;
            }

            Plugin.Log.LogInfo("ModRegistry: " + total + " mods, " + enabled + " enabled.");

            foreach (ModEntry e in ModRegistry.Entries)
                if (!e.Enabled)
                    Plugin.Log.LogInfo("ModRegistry: '" + e.Key + "' is disabled (its content was not loaded).");
        }

        private static LoadResult LoadDataContent()
        {
            return ContentLoader.Load(Plugin.DataContentRoot.Value);
        }

        private static void Run(string what, Action register)
        {
            try { register(); }
            catch (Exception e) { Plugin.Log.LogError(what + " registration failed: " + e); }
        }
    }
}
