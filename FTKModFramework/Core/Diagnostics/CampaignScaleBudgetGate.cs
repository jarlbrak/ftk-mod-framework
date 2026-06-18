using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using FTKPerfProbe;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>
    /// The CAMPAIGN scale-and-performance gate (P5, #45). Sibling of <see cref="ScaleBudgetGate"/> (which gates
    /// the DATA-content load); this one proves the campaign engine supports campaigns MANY TIMES longer than
    /// vanilla within the P5 performance envelope. It runs once, gated by <see cref="Plugin.DiagnosticsEnableGate"/>,
    /// from the same Plugin.cs load path as the data-content gate (inside the _done-guarded postfix via Run(...)).
    ///
    /// WHY A SEPARATE GATE (the KEY INSIGHT): the data gate's save-size figure is the IdAllocator PROXY
    /// (<c>CustomIdCount * perEntryBytes</c>). Campaigns deliberately BYPASS IdAllocator (spec #37 NFR-1:
    /// quest/stage/flag ids are STRING-keyed), so that proxy captures ZERO campaign growth. This gate therefore
    /// measures the REAL campaign save artifact: the byte length of the authored <c>m_FullFileData</c> (the JSON
    /// the game persists and reloads), NOT the IdAllocator proxy.
    ///
    /// SCALE TARGET (the spec's Open Question, confirmed against P5 #21): vanilla DungeonCrawl ships 1 stage / 2
    /// quests (verified in the installed DungeonCrawl.ftk2). The gate authors a synthetic campaign at a fixed
    /// scale target (default 20 stages x 25 quests = 500 quests = 250x the vanilla quest count), capped at a sane
    /// maximum, so the smoke log shows a deterministic line WITHOUT the operator hand-editing config. The public
    /// SyntheticCampaign* config binds can raise/lower the target; 0 means "use the fixed default target" so the
    /// gate is observable by default (mirroring the data gate, which also runs by default when enabled).
    ///
    /// BUDGET MODEL: the calibrated-baseline model (load/heap vs a persisted vanilla-load baseline) does NOT map
    /// to a one-shot campaign-authoring measurement or to a real-bytes save-size, so all three campaign axes use
    /// ABSOLUTE budgets driven by config binds (the spec's "budgets come from config, not literals" convention).
    /// The breach ARITHMETIC is still the Pure layer's (<see cref="ScaleBudgetEval.Compare"/>), reused by feeding
    /// a zero baseline so each axis's <c>max(baseline*headroom, floor)</c> collapses to the absolute floor, and by
    /// carrying the real save bytes on the save axis with a unit count so its budget is the absolute config value.
    /// </summary>
    internal static class CampaignScaleBudgetGate
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The fixed default scale target used when the public synthetic-campaign config binds are left at 0, so
        // the gate produces a deterministic observable line by default. 20 x 25 = 500 quests (250x vanilla's 2).
        private const int DefaultStages = 20;
        private const int DefaultQuestsPerStage = 25;

        // Sane caps: a single synthetic campaign authored at load time. The product is the quest count; cap each
        // dimension so a mis-set config can never author an unbounded campaign (logged when it clamps).
        private const int MaxStages = 200;
        private const int MaxQuestsPerStage = 200;

        // The synthetic campaign is registered under a RESERVED save-file-name so it never collides with the demo
        // campaigns and is obviously a scale-test artifact in the adventure list.
        private const string SaveFileName = "FtkmfScaleProbe";
        private const string Template = "DungeonCrawl";

        /// <summary>
        /// Author + measure + gate the synthetic campaign, emitting exactly one "SCALE-BUDGET ... [campaign]" line
        /// (and a "SELF-TEST PASS [campaign-scale]" line on a clean pass). Never throws (the caller's Run(...)
        /// wrapper also catches, but this is defensive so one bad measurement never aborts the load).
        /// </summary>
        public static void Evaluate()
        {
            if (!Plugin.DiagnosticsEnableGate.Value)
                return; // disabled: emit NO campaign SCALE-BUDGET line (mirrors the data gate).

            int stages, questsPerStage;
            ResolveScaleTarget(out stages, out questsPerStage);
            int targetQuests = stages * questsPerStage;

            // NFR-1 anchor: snapshot the high-band id count BEFORE authoring so we can assert the campaign added
            // ZERO synthetic ids (campaigns are string-keyed). Read once, compared once after registration.
            int idCountBefore = IdAllocator.CustomIdCount;

            // MEMORY: force a collection to a stable floor, then measure the heap delta the authored campaign
            // adds. GC.GetTotalMemory(true) matches the data gate's memory figure for cross-machine reproducibility.
            long heapBefore = GC.GetTotalMemory(true);

            // LOAD: time the full author + deserialize + validate pass (the load-time cost a real campaign incurs
            // at registration). The single Stopwatch mirrors the data loader's single measurement.
            Stopwatch sw = Stopwatch.StartNew();

            GameDefinitionPreview preview;
            int actualQuests, actualStages;
            bool authored = AuthorAndMeasure(stages, questsPerStage,
                out preview, out actualStages, out actualQuests);

            sw.Stop();
            long heapAfter = GC.GetTotalMemory(true);

            int idCountAfter = IdAllocator.CustomIdCount;

            if (!authored || preview == null || string.IsNullOrEmpty(preview.m_FullFileData))
            {
                Plugin.Log.LogError("SCALE-BUDGET FAIL [campaign]: could not author the synthetic campaign at " +
                    stages + "x" + questsPerStage + " (template '" + Template + "' not installed?).");
                return;
            }

            long loadMs = sw.ElapsedMilliseconds;
            long heapDelta = heapAfter - heapBefore;
            if (heapDelta < 0) heapDelta = 0; // a collection can drop the heap below the floor; clamp to 0.
            // SAVE-SIZE: the REAL serialized save artifact, NOT the IdAllocator proxy. m_FullFileData is the exact
            // JSON the game persists/reloads (GetNewGameDefInstance re-parses it), so its UTF-8 byte length is the
            // campaign's on-disk save-size footprint.
            long saveBytes = Encoding.UTF8.GetByteCount(preview.m_FullFileData);

            // NFR-1 assertion: the synthetic campaign must not have minted ANY synthetic id (string-keyed only).
            bool nfr1Ok = idCountAfter == idCountBefore;

            ScaleVerdict verdict = ComputeVerdict(loadMs, heapDelta, saveBytes);
            EmitVerdict(verdict, actualStages, actualQuests, targetQuests, idCountBefore, idCountAfter, nfr1Ok);
        }

        /// <summary>
        /// Resolve the scale target from config, clamping each dimension to its cap (logging when it clamps) and
        /// falling back to the fixed default when a dimension is &lt;= 0 (so the gate is observable by default).
        /// </summary>
        private static void ResolveScaleTarget(out int stages, out int questsPerStage)
        {
            int cfgStages = Plugin.SyntheticCampaignStages.Value;
            int cfgQuests = Plugin.SyntheticCampaignQuestsPerStage.Value;

            stages = cfgStages > 0 ? cfgStages : DefaultStages;
            questsPerStage = cfgQuests > 0 ? cfgQuests : DefaultQuestsPerStage;

            if (stages > MaxStages)
            {
                Plugin.Log.LogWarning("SCALE-BUDGET [campaign]: stages " + stages + " capped to " + MaxStages + ".");
                stages = MaxStages;
            }
            if (questsPerStage > MaxQuestsPerStage)
            {
                Plugin.Log.LogWarning("SCALE-BUDGET [campaign]: questsPerStage " + questsPerStage +
                    " capped to " + MaxQuestsPerStage + ".");
                questsPerStage = MaxQuestsPerStage;
            }
        }

        /// <summary>
        /// Author the synthetic campaign through the public builder (via <see cref="Adventures.AddCampaignFromTemplate"/>),
        /// then deserialize the authored bytes with the EXACT game settings (TypeNameHandling.Auto +
        /// StringEnumConverter, as GameDefJSONMapper does) to confirm the actual stage/quest counts the game would
        /// load. Returns false (and a null preview) when the template is missing. Never throws.
        /// </summary>
        private static bool AuthorAndMeasure(int stages, int questsPerStage,
            out GameDefinitionPreview preview, out int actualStages, out int actualQuests)
        {
            preview = null;
            actualStages = 0;
            actualQuests = 0;

            try
            {
                preview = Adventures.AddCampaignFromTemplate(
                    Plugin.Guid, SaveFileName, Template,
                    "Scale Probe (" + stages + "x" + questsPerStage + ")",
                    "A synthetic multi-stage campaign authored to exercise the P5 scale budget gate.",
                    delegate(CampaignBuilder c) { SyntheticCampaignGenerator.Author(c, stages, questsPerStage); });

                if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData)) return false;

                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Auto;
                settings.Converters.Add(new StringEnumConverter());
                GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);

                if (gd != null && gd.m_Stages != null)
                {
                    actualStages = gd.m_Stages.Count;
                    for (int i = 0; i < gd.m_Stages.Count; i++)
                        if (gd.m_Stages[i] != null && gd.m_Stages[i].m_Quests != null)
                            actualQuests += gd.m_Stages[i].m_Quests.Count;
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SCALE-BUDGET [campaign]: authoring threw: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Reuse the Pure breach arithmetic (<see cref="ScaleBudgetEval.Compare"/>) with ABSOLUTE config budgets:
        /// a ZERO baseline collapses each axis's <c>max(baseline*headroom, floor)</c> to the absolute floor, and a
        /// UNIT save count makes the save budget the absolute config value while the measured save bytes ride the
        /// proxy slot. No budget math is duplicated here.
        /// </summary>
        private static ScaleVerdict ComputeVerdict(long loadMs, long heapDelta, long saveBytes)
        {
            ScaleMetrics metrics = new ScaleMetrics(
                loadMs,
                heapDelta,
                saveBytes,     // SaveProxyBytes slot carries the REAL serialized save bytes for the campaign.
                1,             // RegisteredEntries = 1 so saveBudget = 1 * absoluteSaveBudget (absolute budget).
                0L, 0L);       // profiler fields unused for the campaign scenario.

            BaselineRecord zeroBaseline = new BaselineRecord();
            zeroBaseline.BaselineLoadMs = 0;   // => load budget collapses to the absolute load floor.
            zeroBaseline.BaselineHeapBytes = 0; // => memory budget collapses to the absolute memory floor.

            // Headroom multipliers are irrelevant against a zero baseline; pass the existing data-gate ones so the
            // shape is identical. The FLOORS are the campaign-specific absolute config budgets.
            ScaleBudget budget = new ScaleBudget(
                Plugin.DiagnosticsLoadMsHeadroomMultiplier.Value,
                Plugin.CampaignLoadMsBudget.Value,            // absolute load budget (ms)
                Plugin.DiagnosticsMemoryHeadroomMultiplier.Value,
                Plugin.CampaignMemoryBytesBudget.Value,       // absolute memory budget (bytes)
                Plugin.CampaignSaveSizeBytesBudget.Value);    // absolute save-size budget (bytes), via unit count

            return ScaleBudgetEval.Compare(metrics, zeroBaseline, budget);
        }

        /// <summary>
        /// Emit the single campaign verdict line (tagged [campaign], distinct from the data-content line) plus the
        /// NFR-1 assertion and, on a clean PASS, the deterministic "SELF-TEST PASS [campaign-scale]" smoke line.
        /// The save axis is labelled "save" (real serialized bytes) NOT "saveProxy" (the data gate's estimate).
        /// </summary>
        private static void EmitVerdict(ScaleVerdict v, int stages, int quests, int targetQuests,
            int idBefore, int idAfter, bool nfr1Ok)
        {
            string line = "SCALE-BUDGET " + (v.Pass ? "PASS" : "FAIL") + ": " +
                "load=" + v.MeasuredLoadMs.ToString(Inv) + "/" + v.LoadBudgetMs.ToString(Inv) +
                " heap=" + v.MeasuredHeapBytes.ToString(Inv) + "/" + v.HeapBudgetBytes.ToString(Inv) +
                " save=" + v.MeasuredSaveProxyBytes.ToString(Inv) + "/" + v.SaveProxyBudgetBytes.ToString(Inv) +
                " (N=" + quests.ToString(Inv) + ") [campaign]";

            bool overallPass = v.Pass && nfr1Ok;

            if (v.Pass)
                Plugin.Log.LogInfo(line);
            else
                Plugin.Log.LogError(line + " breached: " + string.Join("; ", v.FailReasons.ToArray()));

            // NFR-1: the synthetic campaign must not have minted ANY synthetic id (string-keyed only). Surface it
            // alongside the budget line as its own assertion so a regression is obvious in the smoke log.
            if (nfr1Ok)
                Plugin.Log.LogInfo("SCALE-BUDGET [campaign] NFR-1 OK: IdAllocator.CustomIdCount unchanged (" +
                    idBefore + " == " + idAfter + "); quest/stage/flag ids are string-keyed, IdAllocator bypassed.");
            else
                Plugin.Log.LogError("SCALE-BUDGET [campaign] NFR-1 BREACH: IdAllocator.CustomIdCount changed " +
                    idBefore + " -> " + idAfter + " (the synthetic campaign minted synthetic ids; campaigns MUST " +
                    "be string-keyed).");

            // Deterministic observable smoke line: emitted ONLY when the campaign at the scale target was
            // generated + measured + within budget AND bypassed IdAllocator (the work-item's required proof).
            if (overallPass)
                Plugin.Log.LogInfo("SELF-TEST PASS [campaign-scale]: synthetic campaign stages=" + stages +
                    " quests=" + quests + " (target=" + targetQuests + ", vanilla DungeonCrawl=2) authored within " +
                    "budget (load=" + v.MeasuredLoadMs.ToString(Inv) + "ms<=" + v.LoadBudgetMs.ToString(Inv) +
                    ", heap=" + v.MeasuredHeapBytes.ToString(Inv) + "B<=" + v.HeapBudgetBytes.ToString(Inv) +
                    ", save=" + v.MeasuredSaveProxyBytes.ToString(Inv) + "B<=" + v.SaveProxyBudgetBytes.ToString(Inv) +
                    "); IdAllocator bypassed.");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [campaign-scale]: budgetPass=" + v.Pass + " nfr1Ok=" + nfr1Ok +
                    " (stages=" + stages + ", quests=" + quests + ", target=" + targetQuests + ").");
        }
    }
}
