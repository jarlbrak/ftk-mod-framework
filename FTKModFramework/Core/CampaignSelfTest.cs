using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the campaign builder (#38). It authors a 2-stage LINEAR campaign over a clone of
    /// the installed DungeonCrawl adventure, exercising all three reuse verbs (kill → BountyQuestDef, visit →
    /// VisitQuestDef / clear → DungeonQuestDef, encounter → MiniEncounterQuestDef), and registers it via
    /// <see cref="Adventures.AddCampaignFromTemplate"/>.
    ///
    /// It then proves STRUCTURAL validity WITHOUT needing gameplay: it takes the authored full GameDefinition
    /// JSON (the exact bytes the game would load — <c>preview.m_FullFileData</c>) and deserializes it with the
    /// SAME settings the game uses (<c>TypeNameHandling.Auto</c> + <c>StringEnumConverter</c>, per
    /// GameDefJSONMapper.Start) into a real <c>GameDefinition</c>, then asserts the stage/quest counts and that
    /// each quest deserialized to the EXACT expected concrete type. Round-tripping back through the game's own
    /// serializer is what validates the <c>$type</c> discriminators at load time.
    ///
    /// Emits exactly one "SELF-TEST PASS [campaign-builder]" line on success (or a matching FAIL line). Gated
    /// by EnableSampleContent (it registers a real, listable demo adventure), like the rest of AdventureContent.
    /// </summary>
    internal static class CampaignSelfTest
    {
        // A distinct save key so the demo campaign is its own selectable adventure (not colliding with
        // SmugglersRun). It clones DungeonCrawl, so it inherits that template's realm scaffolding + art.
        private const string SaveFileName = "FtkmfCampaignDemo";
        private const string Template = "DungeonCrawl";

        // A realm present in DungeonCrawl's m_RealmStages (verified against the installed .ftk2), so the
        // authored quest destinations resolve. The cloned stage inherits the template stage's m_RealmStages.
        private const string DestRealm = "GuardianForest";

        public static void Run()
        {
            try
            {
                GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                    Plugin.Guid, SaveFileName, Template,
                    "Campaign Demo",
                    "A two-stage linear campaign demonstrating the kill/visit/encounter reuse verbs.",
                    configure: Author);

                if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData))
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [campaign-builder]: AddCampaignFromTemplate returned " +
                        (preview == null ? "null (template '" + Template + "' not installed?)" : "no m_FullFileData") + ".");
                    return;
                }

                // Deserialize the authored bytes with the EXACT game settings (GameDefJSONMapper.Start).
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Auto;
                settings.Converters.Add(new StringEnumConverter());
                GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);

                Validate(gd);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [campaign-builder]: " + e);
            }
        }

        /// <summary>
        /// Author the 2-stage linear campaign. Stage 1: kill then visit. Stage 2: clear-dungeon then encounter.
        /// The very last quest authored (the encounter in stage 2) is what fires VICTORY when completed.
        /// Target ids are vanilla rows verified against the decompile: enemy set "bounty1A" (FTK_enemySet.ID),
        /// dungeon "Cave" (FTK_dungeonEncounter.ID), mini encounter "TreasureChest" (FTK_miniEncounter.ID).
        /// Unknown enum names would make the game's StringEnumConverter throw on the round-trip below, which is
        /// precisely the structural check this self-test enforces.
        /// </summary>
        private static void Author(CampaignBuilder campaign)
        {
            StageBuilder stage1 = campaign.AddStage("FtkmfCampaign_Stage1");
            stage1.AddKillQuest("ftkmf_camp_s1_kill", "bounty1A", DestRealm);
            stage1.AddVisitQuest("ftkmf_camp_s1_visit", DestRealm);

            StageBuilder stage2 = campaign.AddStage("FtkmfCampaign_Stage2");
            stage2.AddClearDungeonQuest("ftkmf_camp_s2_clear", "Cave", DestRealm);
            stage2.AddEncounterQuest("ftkmf_camp_s2_encounter", "TreasureChest", DestRealm);
        }

        /// <summary>Assert structure + concrete types from the round-tripped GameDefinition, emit the single PASS/FAIL line.</summary>
        private static void Validate(GameDefinition gd)
        {
            if (gd == null || gd.m_Stages == null)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [campaign-builder]: round-trip produced a null GameDefinition/m_Stages.");
                return;
            }

            int stageCount = gd.m_Stages.Count;
            bool stagesOk = stageCount == 2;
            bool eachStageHasQuest = stagesOk
                && gd.m_Stages[0].m_Quests != null && gd.m_Stages[0].m_Quests.Count >= 1
                && gd.m_Stages[1].m_Quests != null && gd.m_Stages[1].m_Quests.Count >= 1;

            // The $type discriminators must have round-tripped to the EXACT expected concrete types.
            QuestDefBase s1q0 = eachStageHasQuest ? gd.m_Stages[0].m_Quests[0] : null;
            QuestDefBase s1q1 = eachStageHasQuest ? gd.m_Stages[0].m_Quests[1] : null;
            QuestDefBase s2q0 = eachStageHasQuest ? gd.m_Stages[1].m_Quests[0] : null;
            QuestDefBase s2q1 = eachStageHasQuest ? gd.m_Stages[1].m_Quests[1] : null;

            bool typesOk =
                s1q0 is BountyQuestDef &&
                s1q1 is VisitQuestDef &&
                s2q0 is DungeonQuestDef &&
                s2q1 is MiniEncounterQuestDef;

            int totalQuests = eachStageHasQuest
                ? gd.m_Stages[0].m_Quests.Count + gd.m_Stages[1].m_Quests.Count : 0;

            if (stagesOk && eachStageHasQuest && typesOk)
                Plugin.Log.LogInfo("SELF-TEST PASS [campaign-builder]: 2-stage linear campaign registered (stages=" +
                    stageCount + ", quests=" + totalQuests + "; types: " +
                    s1q0.GetType().Name + ", " + s1q1.GetType().Name + ", " +
                    s2q0.GetType().Name + ", " + s2q1.GetType().Name + ").");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [campaign-builder]: stagesOk=" + stagesOk +
                    " eachStageHasQuest=" + eachStageHasQuest + " typesOk=" + typesOk +
                    " (stages=" + stageCount + ", types=" +
                    TypeName(s1q0) + "/" + TypeName(s1q1) + "/" + TypeName(s2q0) + "/" + TypeName(s2q1) + ").");
        }

        private static string TypeName(QuestDefBase q)
        {
            return q == null ? "null" : q.GetType().Name;
        }
    }
}
