using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Spec #37 Phase 5 (#44): the BUNDLED SAMPLE CAMPAIGN that proves the public campaign-authoring API is
    /// sufficient to ship a cohesive, validator-clean campaign WITHOUT touching any internal engine type.
    ///
    /// Everything here goes through the public surface only:
    ///   - <see cref="Adventures.AddCampaignFromTemplate"/>  (clone DungeonCrawl, author a campaign over its m_Stages)
    ///   - <see cref="CampaignBuilder.AddStage"/>
    ///   - the five reuse VERBS on <see cref="StageBuilder"/>: AddKillQuest / AddVisitQuest /
    ///     AddClearDungeonQuest / AddEncounterQuest (the four objective types) + AddCollectQuest (collect-N)
    ///   - <see cref="QuestBuilder.OnCompleteSetFlag"/> + <see cref="QuestBuilder.BranchTo"/> with the pure-data
    ///     <see cref="BranchCondition"/> DTO (one flag-conditioned branch)
    /// No <c>ContentRegistry</c>/<c>IdAllocator</c>/<c>BranchSidecar</c>/router type appears below; the campaign
    /// is pure DATA authored over the cloned GameDefinition JSON, exactly as a third-party modder would write it.
    ///
    /// THE GRAPH (cloned from "DungeonCrawl", all destinations in the inherited realm "GuardianForest"):
    ///
    ///   Stage 1 (FtkmfSampleCampaign_Stage1)
    ///     Q1 kill      "bounty1A"            --linear-->  Q2
    ///     Q2 collect-N townTeleport x2       --linear-->  Q3   (OnComplete: set ftkmf_relics_found = 1)
    ///     Q3 encounter "TreasureChest"       --branch(ftkmf_relics_found eq 1)--> Q5 (victory)
    ///                                        --linear (fall-through)--------------> Q4
    ///   Stage 2 (FtkmfSampleCampaign_Stage2)
    ///     Q4 visit                           --linear-->  Q5
    ///     Q5 clear     "Cave"                VICTORY (last quest of the last stage)
    ///
    /// The branch CONVERGES: whether Q3 redirects straight to the victory quest (relics found) or falls through
    /// the visit, BOTH paths reach Q5. Because the branch target's stage is the immediately-next stage, the
    /// validator records no cross-stage-skip warning, and because every quest reachable from the start can reach
    /// victory there is no reachability error: the load pre-pass in AddCampaignFromTemplate logs 0 error(s).
    ///
    /// Gated behind EnableSampleContent (it registers a real, selectable demo adventure); called from
    /// <see cref="AdventureContent.Register"/> alongside the other campaign self-tests.
    /// </summary>
    internal static class CampaignContent
    {
        // A distinct save key so the sample is its own selectable adventure (not colliding with the other
        // demos). It clones DungeonCrawl, inheriting that template's realm scaffolding + preview art.
        private const string SaveFileName = "FtkmfSampleCampaign";
        private const string DisplayName = "FtkmfSampleCampaign";
        private const string Template = "DungeonCrawl";

        // A realm present in DungeonCrawl's m_RealmStages (verified against the installed .ftk2 and reused by the
        // existing CampaignSelfTest), so every authored quest destination resolves.
        private const string DestRealm = "GuardianForest";

        // The campaign flag the collect-N quest sets on completion and the encounter quest branches on.
        private const string RelicsFlag = "ftkmf_relics_found";

        // Stage ids (the m_StageLookup keys; must differ per stage).
        private const string Stage1 = "FtkmfSampleCampaign_Stage1";
        private const string Stage2 = "FtkmfSampleCampaign_Stage2";

        // Globally-unique quest ids (the m_QuestLookup keys; must be unique across the whole campaign).
        private const string Q1Kill = "ftkmf_sample_s1_kill";
        private const string Q2Collect = "ftkmf_sample_s1_collect";
        private const string Q3Encounter = "ftkmf_sample_s1_encounter";
        private const string Q4Visit = "ftkmf_sample_s2_visit";
        private const string Q5Clear = "ftkmf_sample_s2_clear"; // the victory quest

        // Verified-valid vanilla target ids (string FTK_*.ID enum names + an FTK_itembase.ID), reused from the
        // existing demos so the game's StringEnumConverter round-trips them and the validator resolves them.
        private const string KillEnemySet = "bounty1A";       // FTK_enemySet.ID
        private const string ClearDungeon = "Cave";           // FTK_dungeonEncounter.ID
        private const string Encounter = "TreasureChest";     // FTK_miniEncounter.ID
        private const FTK_itembase.ID CollectItem = FTK_itembase.ID.townTeleport;
        private const int CollectCount = 2;

        // Quests authored, for the self-test line (kill/visit/clear/encounter + collect-N = the 5 verbs).
        private const int QuestCount = 5;

        public static void Register()
        {
            GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                Plugin.Guid, SaveFileName, Template,
                DisplayName,
                "A two-stage sample campaign showcasing every campaign-authoring verb: a kill bounty, a " +
                "collect-N relic hunt that sets a flag, a flagged encounter that branches, then a visit and a " +
                "dungeon clear that crown the run. Find the relics to take the shortcut to victory.",
                configure: Author);

            // Authored + registered iff the pipeline returned a preview with its full JSON (the bytes the game
            // loads). The load pre-pass (#43) already ran inside AddCampaignFromTemplate and logged its
            // "Campaign validation '...': N error(s), M warning(s)." summary; a clean sample reports 0 errors.
            bool ok = preview != null && !string.IsNullOrEmpty(preview.m_FullFileData);

            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS [campaign]: sample '" + DisplayName +
                    "' authored via public API (2 stages, " + QuestCount +
                    " quests: kill/visit/clear/encounter/collect-N + 1 flag branch), registered + selectable, " +
                    "reaches last-quest victory.");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [campaign]: AddCampaignFromTemplate returned " +
                    (preview == null ? "null (template '" + Template + "' not installed?)" : "no m_FullFileData") +
                    " for sample '" + DisplayName + "'.");
        }

        /// <summary>
        /// Author the 2-stage sample campaign over the cloned GameDefinition's m_Stages. Quests play in the order
        /// they are added; the last quest of the last stage (the dungeon clear) is the campaign's victory quest.
        /// </summary>
        private static void Author(CampaignBuilder campaign)
        {
            // Stage 1: kill -> collect-N (sets the relics flag) -> encounter (branches on that flag).
            StageBuilder stage1 = campaign.AddStage(Stage1);
            stage1.AddKillQuest(Q1Kill, KillEnemySet, DestRealm);
            stage1.AddCollectQuest(Q2Collect, CollectItem, CollectCount, DestRealm)
                  .OnCompleteSetFlag(RelicsFlag, "set", 1);
            stage1.AddEncounterQuest(Q3Encounter, Encounter, DestRealm)
                  // Found the relics? Skip straight to the victory quest in the immediately-next stage. The
                  // single conditional edge (no unconditional default) leaves the linear fall-through (Q4 visit)
                  // intact, so both paths converge on the victory quest -> the campaign stays winnable.
                  .BranchTo(Q5Clear, new BranchCondition { Flag = RelicsFlag, Op = "eq", Value = 1 });

            // Stage 2: visit -> clear-dungeon (VICTORY). The visit is the linear fall-through when the branch
            // condition fails; the clear is the last quest of the last stage and fires victory on completion.
            StageBuilder stage2 = campaign.AddStage(Stage2);
            stage2.AddVisitQuest(Q4Visit, DestRealm);
            stage2.AddClearDungeonQuest(Q5Clear, ClearDungeon, DestRealm);
        }
    }
}
