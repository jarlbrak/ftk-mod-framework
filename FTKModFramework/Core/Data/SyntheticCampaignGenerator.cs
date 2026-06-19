using System.Globalization;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// DEV/STRESS aid (P5, #45): authors a SYNTHETIC multi-stage campaign at a scale target through the
    /// PUBLIC campaign builder (<see cref="CampaignBuilder"/> / <see cref="StageBuilder"/> / <see cref="QuestBuilder"/>),
    /// purely as DATA. It is the campaign analogue of <see cref="SyntheticContentGenerator"/>: that one writes
    /// synthetic GridEditor rows (which mint synthetic IdAllocator ids), THIS one authors synthetic quests
    /// (which are STRING-keyed and deliberately BYPASS IdAllocator, per spec #37 NFR-1). Nothing here registers
    /// or measures: it only drives the builder so <see cref="Adventures.AddCampaignFromTemplate"/> produces the
    /// authored GameDefinition the campaign scale gate then measures.
    ///
    /// The authored campaign is VALIDATOR-CLEAN (see <see cref="QuestValidator"/>): every quest carries a unique
    /// <c>m_StoryQuestID</c>, every objective targets a vanilla row verified against the decompile (enemy set
    /// "bounty1A", dungeon "Cave", mini-encounter "TreasureChest", collect item townTeleport, realm
    /// "GuardianForest"), it includes at least one CONVERGENT branch (two quests both branch to a common later
    /// quest, leaving victory reachable), and the LAST quest of the LAST stage is the victory quest. The objective
    /// type cycles kill -> visit -> clear -> encounter -> collect-N so every reuse verb is exercised at scale.
    ///
    /// Determinism: all ids are a fixed prefix plus zero-padded indices; there is no randomness and no
    /// IdAllocator/FTK_*.ID-band usage, so two machines authoring the same (stages, questsPerStage) produce a
    /// byte-identical campaign. Internal to Core; never part of the public API surface.
    /// </summary>
    internal static class SyntheticCampaignGenerator
    {
        // Vanilla rows verified against the decompile (mirrors CampaignSelfTest's constants exactly).
        private const string EnemySet = "bounty1A";        // FTK_enemySet.ID
        private const string Dungeon = "Cave";             // FTK_dungeonEncounter.ID
        private const string MiniEncounter = "TreasureChest"; // FTK_miniEncounter.ID
        private const string DestRealm = "GuardianForest"; // present in DungeonCrawl's m_RealmStages

        // The collect-N target: a real item id present in FTK_itemsDB (verified, used by the work-item brief).
        private const GridEditor.FTK_itembase.ID CollectItem = GridEditor.FTK_itembase.ID.townTeleport;

        // Id prefixes. All quest/stage/flag keys are STRINGS; none touch IdAllocator (NFR-1).
        private const string StageIdFormat = "FtkmfScale_Stage{0:D4}";
        private const string QuestIdFormat = "ftkmf_scale_s{0:D4}_q{1:D4}";
        private const string ConvergeFlag = "ftkmf_scale_converge";

        /// <summary>
        /// Author <paramref name="stages"/> stages of <paramref name="questsPerStage"/> quests each (total =
        /// stages * questsPerStage) into <paramref name="campaign"/>, cycling the objective verbs and recording
        /// one convergent branch. Caller guarantees both counts are >= 1 (the gate clamps + caps before calling).
        /// </summary>
        public static void Author(CampaignBuilder campaign, int stages, int questsPerStage)
        {
            // The convergent-branch source quests (chosen up front so we can record edges as we author). Two
            // distinct early quests both branch UNCONDITIONALLY to a common later quest, then fall back to the
            // linear successor; this keeps victory reachable (every reachable quest still flows to the last).
            // Convergence target = the FIRST quest of the SECOND stage (exists whenever stages >= 2).
            string convergeTarget = stages >= 2 ? QuestId(2, 1) : null;
            string branchSourceA = QuestId(1, 1);
            string branchSourceB = stages >= 2 ? QuestId(1, System.Math.Min(2, questsPerStage)) : null;

            for (int s = 1; s <= stages; s++)
            {
                StageBuilder stage = campaign.AddStage(StageId(s));
                for (int q = 1; q <= questsPerStage; q++)
                {
                    string id = QuestId(s, q);
                    QuestBuilder qb = AddCyclicQuest(stage, id, GlobalIndex(s, q, questsPerStage));

                    // Convergent branch: two early sources both route to the same later quest. The first source
                    // also sets the convergence flag on completion (so the condition is satisfiable on a real
                    // play-through); a conditional BranchTo adds the target edge AND keeps the linear fall-through,
                    // so victory stays reachable and the router/validator are exercised at scale.
                    if (convergeTarget != null && id == branchSourceA)
                        qb.OnCompleteSetFlag(ConvergeFlag, "set", 1)
                          .BranchTo(convergeTarget, new BranchCondition { Flag = ConvergeFlag, Op = "ge", Value = 0 });
                    else if (convergeTarget != null && id == branchSourceB)
                        qb.BranchTo(convergeTarget, new BranchCondition { Flag = ConvergeFlag, Op = "ge", Value = 0 });
                }
            }
        }

        /// <summary>
        /// Add one quest whose objective TYPE is selected by the global play-order index modulo 5, so the five
        /// verbs (kill / visit / clear / encounter / collect-N) cycle uniformly across the whole campaign.
        /// </summary>
        private static QuestBuilder AddCyclicQuest(StageBuilder stage, string id, int globalIndex)
        {
            switch (globalIndex % 5)
            {
                case 0: return stage.AddKillQuest(id, EnemySet, DestRealm);
                case 1: return stage.AddVisitQuest(id, DestRealm);
                case 2: return stage.AddClearDungeonQuest(id, Dungeon, DestRealm);
                case 3: return stage.AddEncounterQuest(id, MiniEncounter, DestRealm);
                default: return stage.AddCollectQuest(id, CollectItem, 1, DestRealm); // count >= 1 (validator-clean)
            }
        }

        /// <summary>Flattened play-order index of stage s, quest q (1-based stage/quest) for the verb cycle.</summary>
        private static int GlobalIndex(int stage, int quest, int questsPerStage)
        {
            return (stage - 1) * questsPerStage + (quest - 1);
        }

        private static string StageId(int s)
        {
            return string.Format(CultureInfo.InvariantCulture, StageIdFormat, s);
        }

        private static string QuestId(int s, int q)
        {
            return string.Format(CultureInfo.InvariantCulture, QuestIdFormat, s, q);
        }
    }
}
