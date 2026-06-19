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
    }
}
