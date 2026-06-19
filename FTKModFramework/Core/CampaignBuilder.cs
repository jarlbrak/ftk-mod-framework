using System;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Authors a multi-stage linear CAMPAIGN over a cloned <c>GameDefinition</c>'s <c>m_Stages</c>, as DATA.
    ///
    /// FTK resolves the global story order positionally at <c>GameDefinition.Initialize</c>: stage order ×
    /// within-stage quest order, flattened, with the first quest played being <c>m_Stages[0].m_Quests[0]</c>
    /// and VICTORY firing on completing the last quest of the last stage. The runtime quest/stage chain
    /// (<c>m_NextStageID</c>, <c>m_QuestLookup</c>, <c>m_NextStoryQuestID</c>) is <c>[JsonIgnore]</c> and
    /// built positionally, so authoring it means simply appending valid stages and quests in play order.
    ///
    /// A <see cref="CampaignBuilder"/> is created over the live JObject by
    /// <see cref="Adventures.AddCampaignFromTemplate"/> and is never constructed by modders directly. It
    /// operates purely on Newtonsoft <c>JObject</c>/<c>JArray</c> (no engine internals leak through its
    /// surface). Stages are cloned from the template's first stage so the realm scaffolding that the game
    /// needs for map generation (<c>m_RealmStages</c>, <c>m_RealmStartFilter</c>, progression fields) is
    /// inherited verbatim; only the stage id and quest list are replaced.
    /// </summary>
    public sealed class CampaignBuilder
    {
        private readonly JObject _gameDef;
        private readonly JArray _stages;          // GameDefinition.m_Stages (the live array on _gameDef)
        private readonly JObject _stageTemplate;  // a deep-clone source for new stages (template m_Stages[0])
        private bool _finalized;                  // FinalizeCampaign() runs the map-layout patch exactly once

        /// <summary>Internal: built by <see cref="Adventures.AddCampaignFromTemplate"/> over the cloned gamedef JObject.</summary>
        internal CampaignBuilder(JObject gameDef)
        {
            _gameDef = gameDef;

            JToken stagesTok = gameDef["m_Stages"];
            _stages = stagesTok as JArray;
            if (_stages == null || _stages.Count == 0)
                throw new InvalidOperationException(
                    "CampaignBuilder: template GameDefinition has no m_Stages to clone a stage scaffold from.");

            // The first template stage is the reuse source for every authored stage: it carries a valid,
            // non-empty m_RealmStages (required for map gen / stage validity) plus the realm/progression
            // scaffolding. We snapshot it ONCE, then clear the live array so the campaign is exactly the
            // stages the caller authors (a linear campaign, not template-stage + caller-stages).
            _stageTemplate = (JObject)_stages[0].DeepClone();
            _stages.Clear();
        }

        /// <summary>
        /// Append a new <c>GameStage</c> to the campaign and return a <see cref="StageBuilder"/> for adding
        /// its quests. The stage is a deep clone of the template's realm scaffolding (so it is map-gen valid)
        /// with <c>m_ThisStageID</c> set to <paramref name="stageId"/> and an empty quest list. Stages play
        /// in the order they are added; the first quest of the first added stage is the campaign's entry quest.
        /// </summary>
        /// <param name="stageId">Unique, non-empty stage id (the m_StageLookup key; must differ per stage).</param>
        public StageBuilder AddStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                throw new ArgumentException("CampaignBuilder.AddStage: stageId must be non-empty.", "stageId");

            JObject stage = (JObject)_stageTemplate.DeepClone();
            stage["m_ThisStageID"] = stageId;
            stage["m_Quests"] = new JArray(); // a fresh, empty quest list; StageBuilder fills it in play order

            // The new stage's positional index == its slot in m_Stages == the current Count BEFORE the Add. The
            // game keys its per-stage hex/realm allocation by this index, so the StageBuilder stamps it onto every
            // quest's m_SpecifiedRealmStageIndex (see StageBuilder ctor) so Specified-realm destinations resolve
            // against THIS stage's bucket rather than always stage 0's.
            int stageIndex = _stages.Count;
            _stages.Add(stage);
            return new StageBuilder((JArray)stage["m_Quests"], stageIndex);
        }

        /// <summary>
        /// Internal: run AFTER the caller has appended every stage, BEFORE the preview/round-trip is built.
        /// Authors per-stage map-layout caster coverage so EVERY authored stage index generates hexes.
        ///
        /// WHY THIS IS REQUIRED (the multi-stage soft-lock, #37): map generation only produces hexes for a
        /// stage index <c>i</c> when the chosen <c>MapLayoutData.m_RealmCasterData</c> contains a
        /// <c>RealmCasterData</c> with <c>m_StageIndex == i</c> (verified: <c>GameDefinition._createRealmCasterTable</c>
        /// loops every stage index and, for each stage's realm, requires a caster whose <c>m_StageIndex == i</c>
        /// (preferred-realm match) or a generic <c>m_PreferredRealm == None</c> caster with <c>m_StageIndex == i</c>;
        /// GameDefinition.cs:1090,1108). The cloned single-stage DungeonCrawl layout only defines casters for
        /// <c>m_StageIndex == 0</c>, so a 2nd authored stage (index 1) gets ZERO casters → no hex carries
        /// <c>m_StageIndex == 1</c> → <c>FTKHex.m_HexInfoInRealmStage</c> never gets a <c>[1]</c> key →
        /// <c>GameStage.ComputeMapInfo()</c> throws <c>KeyNotFoundException</c> on
        /// <c>m_HexInfoInRealmStage[1].Keys</c> (GameStage.cs:132) → <c>GameDefinition.ComputeMapInfo()</c> aborts
        /// BEFORE building <c>_questHashLookup</c> (GameDefinition.cs:613-617) → it stays empty →
        /// <c>GetQuestByHashID</c> returns null → <c>CheckStoryQuestStartMessage</c> NREs every frame (the freeze).
        ///
        /// THE FIX: for each <c>MapLayoutData</c>, for each authored stage index <c>i</c> from 1..(stageCount-1),
        /// append deep-clones of that layout's <c>m_StageIndex == 0</c> generic (<c>m_PreferredRealm == None</c>)
        /// casters with <c>m_StageIndex = i</c>. Each authored stage's <c>m_RealmStages</c> is cloned from stage 0's
        /// (AddStage), so its realms are exactly stage 0's realms, which these generic casters cover (the second
        /// pass at GameDefinition.cs:1108 assigns a generic None caster per realm). Idempotent (guarded by
        /// <see cref="_finalized"/>) and a no-op for single-stage campaigns (no index >= 1 to cover).
        /// </summary>
        internal void FinalizeCampaign()
        {
            if (_finalized) return;
            _finalized = true;

            int stageCount = _stages.Count;
            if (stageCount <= 1) return; // single-stage: stage 0's existing casters already cover it.

            JArray layouts = _gameDef["m_MapLayoutOptions"] as JArray;
            if (layouts == null || layouts.Count == 0)
            {
                // No map layout to extend. A campaign with >1 stage CANNOT generate hexes for stage >= 1 without
                // caster coverage, so this would soft-lock in-game; surface it loudly rather than fail silently.
                Plugin.Log.LogError("CampaignBuilder.FinalizeCampaign: GameDefinition has no m_MapLayoutOptions to author " +
                    "per-stage caster coverage on; multi-stage campaign would not generate hexes for stages >= 1.");
                return;
            }

            foreach (JToken layoutTok in layouts)
            {
                JObject layout = layoutTok as JObject;
                if (layout == null) continue;

                JArray casters = layout["m_RealmCasterData"] as JArray;
                if (casters == null) continue;

                // Snapshot the stage-0 GENERIC casters (m_PreferredRealm == None) ONCE, before appending to the
                // live list (so we clone only the originals, never our own appended copies). Generic casters are
                // the realm-agnostic ones the second-pass assigner (GameDefinition.cs:1108) hands to a stage's
                // realms; preferred-realm casters are left to their authored realm and not duplicated here.
                System.Collections.Generic.List<JObject> stage0Generic =
                    new System.Collections.Generic.List<JObject>();
                foreach (JToken cTok in casters)
                {
                    JObject c = cTok as JObject;
                    if (c == null) continue;
                    if (StageIndexOf(c) == 0 && IsNoneRealm(c))
                        stage0Generic.Add(c);
                }

                if (stage0Generic.Count == 0)
                {
                    // No generic stage-0 caster to clone (template uses only preferred-realm casters). We cannot
                    // synthesize generic coverage safely; report so the regression is visible at load.
                    Plugin.Log.LogError("CampaignBuilder.FinalizeCampaign: map layout '" +
                        (layout["m_MapLayoutID"] != null ? layout["m_MapLayoutID"].ToString() : "?") +
                        "' has no stage-0 generic (None) RealmCasterData to clone; stages >= 1 would not generate hexes.");
                    continue;
                }

                for (int i = 1; i < stageCount; i++)
                {
                    foreach (JObject src in stage0Generic)
                    {
                        JObject clone = (JObject)src.DeepClone();
                        clone["m_StageIndex"] = i;
                        casters.Add(clone);
                    }
                }
            }
        }

        private static int StageIndexOf(JObject caster)
        {
            JToken t = caster["m_StageIndex"];
            return t != null ? (int)t : 0;
        }

        private static bool IsNoneRealm(JObject caster)
        {
            JToken t = caster["m_PreferredRealm"];
            // m_PreferredRealm is an FTK_realm.ID serialized by StringEnumConverter; default/None is the realm-
            // agnostic generic caster. Treat a missing field as None (the enum default).
            return t == null || string.Equals((string)t, "None", StringComparison.Ordinal);
        }
    }
}
