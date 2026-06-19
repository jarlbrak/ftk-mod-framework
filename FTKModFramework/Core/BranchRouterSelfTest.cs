using System;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the light-DAG branch router (#42, spec #37 P3b). It drives the REAL
    /// <see cref="QuestRouterPatch.Postfix"/> (via reflection, never a re-implementation) against a real
    /// <see cref="GameDefinition"/> built + <c>Initialize()</c>d from a tiny authored 3-quest campaign, and proves
    /// the three acceptance points, then emits a single <c>SELF-TEST PASS [branch-router]</c> line (or matching FAIL):
    /// <list type="bullet">
    /// <item><b>Case A (match redirects):</b> from completed quest Q1 (which has
    /// <c>OnCompleteSetFlag("took_path","set",1)</c> + <c>BranchTo("Q3", took_path eq 1)</c>), with the vanilla
    /// successor seeded as Q2, assert the Postfix APPLIED the on-complete flag AND redirected <c>__result</c> to the
    /// branch target Q3. This proves on-complete-flags-before-conditions + the first-match redirect + the m_Stages
    /// target walk together.</item>
    /// <item><b>Case B (no match unchanged):</b> from completed quest Q2 (which has a branch guarded by an
    /// unsatisfiable condition <c>took_path eq 2</c>), with the vanilla successor seeded as Q3, assert <c>__result</c>
    /// stays Q3 (the vanilla linear successor).</item>
    /// <item><b>Case C (unknown op rejected at authoring):</b> assert an unknown COMPARISON op (<c>"gt"</c>) and an
    /// unknown MUTATION op are each rejected by the builder with an <see cref="ArgumentException"/> (two disjoint
    /// closed vocabularies, each validated against its own set).</item>
    /// </list>
    ///
    /// The Postfix is host-guarded (<c>PhotonNetwork.isMasterClient</c>) and its flag ops go through
    /// <see cref="Campaign"/> (which needs a live <c>GameLogic</c>). At plugin LOAD neither holds, so the harness
    /// temporarily forces offline-mode (=&gt; host) and installs an uninitialized <c>GameLogic</c> singleton purely
    /// so the store materializes, then RESTORES both in a finally. It does NOT call the real <c>GetNextQuest</c>
    /// (which NREs on the null <c>FTKGameStats.Inst</c> at load); it drives only the Postfix, exactly as the
    /// collect-N self-test drives its patch method directly. Gated the same way as the other campaign self-tests
    /// (from Content/AdventureContent.cs), since it registers a real selectable demo adventure.
    /// </summary>
    internal static class BranchRouterSelfTest
    {
        private const string SaveFileName = "FtkmfBranchDemo";
        private const string Template = "DungeonCrawl";
        private const string DestRealm = "GuardianForest"; // present in DungeonCrawl's m_RealmStages (see CampaignSelfTest)

        private const string Q1 = "ftkmf_branch_q1";
        private const string Q2 = "ftkmf_branch_q2";
        private const string Q3 = "ftkmf_branch_q3";
        private const string Flag = "took_path";

        public static void Run()
        {
            try
            {
                GameDefinition gd = BuildInitializedCampaign();
                if (gd == null) return; // BuildInitializedCampaign logged the FAIL reason

                // Resolve the three quest defs from the initialized def (so each has a non-null m_Stage).
                QuestDefBase q1 = Find(gd, Q1), q2 = Find(gd, Q2), q3 = Find(gd, Q3);
                if (q1 == null || q2 == null || q3 == null)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [branch-router]: a built quest was not resolvable after Initialize (" +
                        "q1=" + (q1 != null) + " q2=" + (q2 != null) + " q3=" + (q3 != null) + ").");
                    return;
                }

                bool caseA, caseB;
                using (FakeHostGame.Enter()) // host + live store for the duration; restored on Dispose
                {
                    caseA = CheckMatchRedirects(gd, q2, q3);
                    caseB = CheckNoMatchUnchanged(gd, q3);
                }
                bool caseC = CheckUnknownOpRejected();

                if (caseA && caseB && caseC)
                    Plugin.Log.LogInfo("SELF-TEST PASS [branch-router]: Postfix applied on-complete flag '" + Flag +
                        "'=1 then redirected Q1's successor " + Q2 + " -> branch target " + Q3 +
                        " (m_Stages walk); unsatisfied condition left Q2's vanilla successor " + Q3 +
                        " unchanged; unknown compare op 'gt' and unknown mutation op both rejected at authoring.");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [branch-router]: caseA(match-redirect)=" + caseA +
                        " caseB(no-match-unchanged)=" + caseB + " caseC(unknown-op-rejected)=" + caseC + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router]: " + e);
            }
        }

        // Author Q1 -> Q2 -> Q3 (linear), recording into the sidecar: Q1 sets took_path=1 on complete and branches
        // to Q3 when took_path eq 1; Q2 branches to Q3 only when took_path eq 2 (unsatisfiable => Case B no-match).
        // Build the full gamedef JSON via the same pipeline the game loads, deserialize with the game's exact
        // settings, and Initialize() so m_Stages / each quest's m_Stage / m_QuestLookup / m_NextStoryQuestID exist.
        private static GameDefinition BuildInitializedCampaign()
        {
            GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                Plugin.Guid, SaveFileName, Template,
                "Branch Router Demo",
                "A 3-quest campaign exercising the flag-conditioned branch router.",
                Author);

            if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData))
            {
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router]: AddCampaignFromTemplate returned " +
                    (preview == null ? "null (template '" + Template + "' not installed?)" : "no m_FullFileData") + ".");
                return null;
            }

            // EXACT game settings (GameDefJSONMapper.Start / GetNewGameDefInstance).
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.Converters.Add(new StringEnumConverter());
            GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);
            if (gd == null || gd.m_Stages == null || gd.m_Stages.Count < 1)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router]: round-trip produced a null GameDefinition/m_Stages.");
                return null;
            }

            gd.Initialize(); // builds m_StageLookup/m_QuestLookup/m_NextStoryQuestID + sets each quest's m_Stage

            // Make StoryQuestID readable without the Data(GameDefinition) ctor (which walks m_ActualRealms, unset
            // before map gen). An uninitialized Data lets us set m_StoryQuestID directly per case.
            gd.m_GameDefData = (GameDefinition.Data)FormatterServices.GetUninitializedObject(typeof(GameDefinition.Data));
            return gd;
        }

        private static void Author(CampaignBuilder campaign)
        {
            StageBuilder stage = campaign.AddStage("FtkmfBranch_Stage1");
            // Three visit quests in play order: the linear successor is positional (Q1->Q2->Q3).
            QuestBuilder q1 = stage.AddVisitQuest(Q1, DestRealm);
            QuestBuilder q2 = stage.AddVisitQuest(Q2, DestRealm);
            stage.AddVisitQuest(Q3, DestRealm);

            // Q1: set the flag on completion, then branch on that same flag. On-complete flags apply BEFORE the
            // conditions, so this self-branch matches in Case A (took_path becomes 1, condition wants 1).
            q1.OnCompleteSetFlag(Flag, "set", 1)
              .BranchTo(Q3, new BranchCondition { Flag = Flag, Op = "eq", Value = 1 });

            // Q2: a branch guarded by an UNSATISFIABLE condition (took_path is 1, never 2). Q2 has no on-complete
            // flag op, so its condition fails -> Case B no-match (vanilla successor Q3 preserved).
            q2.BranchTo(Q3, new BranchCondition { Flag = Flag, Op = "eq", Value = 2 });
        }

        // Case A: from Q1, vanilla successor = Q2. The Postfix must apply took_path=1 then redirect __result to Q3.
        private static bool CheckMatchRedirects(GameDefinition gd, QuestDefBase q2, QuestDefBase q3)
        {
            ClearFlag();
            gd.m_GameDefData.m_StoryQuestID = Q1;

            QuestDefBase result = q2; // the vanilla linear successor the real GetNextQuest would have returned
            InvokePostfix(gd, ref result);

            bool flagApplied = Campaign.GetFlag(Flag) == 1;
            bool redirected = ReferenceEquals(result, q3);
            if (!flagApplied || !redirected)
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router] (A): flagApplied=" + flagApplied +
                    " (" + Flag + "=" + Campaign.GetFlag(Flag) + ") redirectedToQ3=" + redirected +
                    " (result=" + ResultId(result) + ").");
            return flagApplied && redirected;
        }

        // Case B: from Q2 (branch guarded by took_path eq 2, unsatisfiable), vanilla successor = Q3. __result stays Q3.
        private static bool CheckNoMatchUnchanged(GameDefinition gd, QuestDefBase q3)
        {
            // Seed took_path = 1 so Q2's "eq 2" condition genuinely fails (self-contained, not relying on Case A).
            Campaign.SetFlag(Flag, 1);
            gd.m_GameDefData.m_StoryQuestID = Q2;

            QuestDefBase result = q3; // vanilla linear successor
            InvokePostfix(gd, ref result);

            bool unchanged = ReferenceEquals(result, q3);
            if (!unchanged)
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router] (B): __result changed off the vanilla successor " +
                    "(expected " + Q3 + ", got " + ResultId(result) + ").");
            return unchanged;
        }

        // Case C: both closed vocabularies reject an unknown op at AUTHORING time (the builder throws).
        private static bool CheckUnknownOpRejected()
        {
            QuestBuilder qb = new StageBuilder(new Newtonsoft.Json.Linq.JArray(), 0)
                .AddVisitQuest("ftkmf_branch_guard", DestRealm);

            bool compareRejected = false;
            try { qb.BranchTo(Q3, new BranchCondition { Flag = Flag, Op = "gt", Value = 1 }); }
            catch (ArgumentException) { compareRejected = true; }

            bool mutateRejected = false;
            try { qb.OnCompleteSetFlag(Flag, "increment", 1); }
            catch (ArgumentException) { mutateRejected = true; }

            if (!compareRejected || !mutateRejected)
                Plugin.Log.LogError("SELF-TEST FAIL [branch-router] (C): compareRejected=" + compareRejected +
                    " mutateRejected=" + mutateRejected + " (an unknown op was NOT rejected at authoring).");
            return compareRejected && mutateRejected;
        }

        // ---- harness plumbing -----------------------------------------------------------------------------

        // Drive the REAL QuestRouterPatch.Postfix(GameDefinition __instance, ref QuestDefBase __result) by reflection.
        private static void InvokePostfix(GameDefinition gd, ref QuestDefBase result)
        {
            MethodInfo postfix = typeof(QuestRouterPatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static);
            if (postfix == null)
                throw new InvalidOperationException("could not reflect QuestRouterPatch.Postfix.");

            object[] args = { gd, result }; // __result is by-ref => read it back out of args after the call
            postfix.Invoke(null, args);
            result = (QuestDefBase)args[1];
        }

        private static QuestDefBase Find(GameDefinition gd, string id)
        {
            foreach (GameStage stage in gd.m_Stages)
                if (stage != null && stage.m_Quests != null)
                    foreach (QuestDefBase q in stage.m_Quests)
                        if (q != null && q.m_StoryQuestID == id) return q;
            return null;
        }

        private static string ResultId(QuestDefBase q) { return q == null ? "null" : q.m_StoryQuestID; }

        private static void ClearFlag() { Campaign.SetFlag(Flag, 0); }

        /// <summary>
        /// Temporarily make this process the HOST (offline-mode) with a live (uninitialized) <c>GameLogic</c> so the
        /// host-guarded Postfix runs and <see cref="Campaign"/>'s flag store materializes, restoring both statics on
        /// <see cref="Dispose"/>. Purely a LOAD-TIME test fixture; never touched in production.
        /// </summary>
        private sealed class FakeHostGame : IDisposable
        {
            private readonly bool _prevOffline;
            private readonly object _prevGameLogic;

            private FakeHostGame(bool prevOffline, object prevGameLogic)
            {
                _prevOffline = prevOffline;
                _prevGameLogic = prevGameLogic;
            }

            internal static FakeHostGame Enter()
            {
                // isMasterClient => offlineMode short-circuits true. Set the backing field directly (the public
                // setter touches networkingPeer, which is unsafe at load).
                bool prevOffline = (bool)Reflect.Field(typeof(PhotonNetwork), "isOfflineMode").GetValue(null);
                Reflect.Field(typeof(PhotonNetwork), "isOfflineMode").SetValue(null, true);

                // Setup is not atomic: once isOfflineMode is flipped, any throw before the disposable exists would
                // leak offline-mode into the real game. Guard the second mutation so a partial failure restores it.
                try
                {
                    // GameLogic.Instance reads private static gGameLogic. Install an uninitialized instance so
                    // GetQuestTable() (lazy-creates its backing dict) gives Campaign a live store, with no MonoBehaviour.
                    object prevGl = Reflect.Field(typeof(GameLogic), "gGameLogic").GetValue(null);
                    GameLogic fake = (GameLogic)FormatterServices.GetUninitializedObject(typeof(GameLogic));
                    Reflect.Field(typeof(GameLogic), "gGameLogic").SetValue(null, fake);

                    return new FakeHostGame(prevOffline, prevGl);
                }
                catch
                {
                    Reflect.Field(typeof(PhotonNetwork), "isOfflineMode").SetValue(null, prevOffline);
                    throw;
                }
            }

            public void Dispose()
            {
                Reflect.Field(typeof(GameLogic), "gGameLogic").SetValue(null, _prevGameLogic);
                Reflect.Field(typeof(PhotonNetwork), "isOfflineMode").SetValue(null, _prevOffline);
            }
        }
    }
}
