using System;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the custom-objective-verb seam + the first custom verb, collect-N (#40). It proves,
    /// WITHOUT gameplay, exactly the three acceptance points, then emits a single
    /// <c>SELF-TEST PASS [collect-n]</c> line (or a matching FAIL):
    /// <list type="bullet">
    /// <item>(a) <b>framework-$type round-trip (verifies OQ2 in-engine):</b> author a collect-N quest via
    /// <see cref="StageBuilder.AddCollectQuest"/>, deserialize the full authored gamedef JSON through the game's
    /// EXACT settings (<c>TypeNameHandling.Auto</c> + <c>StringEnumConverter</c>), and assert the quest element
    /// deserialized to a <see cref="ModQuestDef"/> carrying the expected key/item/count. If the framework
    /// <c>$type</c> ("...ModQuestDef, FTKModFramework") fails to resolve in-engine, the element comes back as a
    /// raw base type (or null) and THIS sub-check fails, surfacing the OQ2 fallback need.</item>
    /// <item>(b) <b>resolver substitution:</b> drive the real <see cref="QuestVerbResolverPatch"/> Prefix (via
    /// reflection) with a constructed <see cref="ModQuestDef"/> whose verb key is the registered collect-N key,
    /// and assert it sets <c>__result</c> to a <see cref="CollectNQuestLogic"/> and returns false (original
    /// skipped). This exercises the actual patch method + the Activator path, not a re-implementation.</item>
    /// <item>(c) <b>count guard:</b> assert <c>AddCollectQuest(..., 0, ...)</c> throws
    /// <see cref="ArgumentOutOfRangeException"/>.</item>
    /// <item>(d, sanity) <see cref="CollectNQuestLogic.IsCompleteState"/> returns false when the party holds
    /// fewer than N (here: no live party, so the sum is 0 &lt; N). The "holds N =&gt; completes" direction is
    /// gameplay, covered by #44's sample campaign.</item>
    /// </list>
    ///
    /// Part (a) needs the installed template, so the whole test runs alongside <see cref="CampaignSelfTest"/>
    /// under the EnableSampleContent path. It registers the collect-N verb itself first (idempotent first-wins),
    /// so parts (b)/(d) do not depend on FrameworkBehaviors having run earlier.
    /// </summary>
    internal static class CollectNSelfTest
    {
        private const string SaveFileName = "FtkmfCollectNDemo";
        private const string Template = "DungeonCrawl";
        private const string DestRealm = "GuardianForest"; // present in DungeonCrawl's m_RealmStages (see CampaignSelfTest)

        private const string QuestId = "ftkmf_collectn_s1_collect";
        private const FTK_itembase.ID Item = FTK_itembase.ID.townTeleport; // a real vanilla item so StringEnumConverter round-trips
        private const int Count = 3;

        public static void Run()
        {
            try
            {
                // The verb must be registered for parts (b)/(d). Idempotent (first-wins), so this is safe even if
                // FrameworkBehaviors.Register already ran in the plugin postfix.
                FrameworkBehaviors.Register();

                bool roundTripOk = CheckTypeRoundTrip();
                bool resolverOk = CheckResolverSubstitution();
                bool guardOk = CheckCountGuard();
                bool incompleteOk = CheckIncompleteWhenBelowN();

                if (roundTripOk && resolverOk && guardOk && incompleteOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS [collect-n]: framework-$type round-trip -> ModQuestDef (" +
                        "key=" + FrameworkBehaviors.CollectNVerbKey + ", item=" + Item + ", count=" + Count +
                        "); resolver Prefix substitutes CollectNQuestLogic via Activator; count<1 rejected; " +
                        "IsCompleteState false with party<N.");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [collect-n]: roundTripOk=" + roundTripOk +
                        " resolverOk=" + resolverOk + " guardOk=" + guardOk + " incompleteOk=" + incompleteOk + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n]: " + e);
            }
        }

        // (a) Author a collect-N quest, round-trip the FULL gamedef through the game's exact settings, and assert
        // the element came back as a ModQuestDef carrying the authored key/item/count. This is the OQ2 check: if
        // the framework $type does not resolve, the element is NOT a ModQuestDef and this returns false.
        private static bool CheckTypeRoundTrip()
        {
            GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                Plugin.Guid, SaveFileName, Template,
                "Collect-N Demo",
                "A one-stage campaign exercising the collect-N custom objective verb.",
                Author);

            if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData))
            {
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (a): AddCampaignFromTemplate returned " +
                    (preview == null ? "null (template '" + Template + "' not installed?)" : "no m_FullFileData") + ".");
                return false;
            }

            // EXACT game settings (GameDefJSONMapper.Start / GameDefinitionPreview.GetNewGameDefInstance).
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.Converters.Add(new StringEnumConverter());
            GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);

            if (gd == null || gd.m_Stages == null || gd.m_Stages.Count < 1
                || gd.m_Stages[0].m_Quests == null || gd.m_Stages[0].m_Quests.Count < 1)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (a): round-trip produced no stage/quest.");
                return false;
            }

            QuestDefBase q = gd.m_Stages[0].m_Quests[0];
            ModQuestDef mod = q as ModQuestDef;
            if (mod == null)
            {
                // OQ2 MISS: the framework $type did not resolve to our type. Surface it loudly so the fallback
                // (carry the verb key on a vanilla def's m_AchievementID) is the next step.
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (a): quest element did NOT deserialize to ModQuestDef " +
                    "(got " + (q == null ? "null" : q.GetType().FullName) + "). The framework $type " +
                    "'FTKModFramework.Core.ModQuestDef, FTKModFramework' did not resolve in-engine -> switch to the " +
                    "m_AchievementID fallback (OQ2).");
                return false;
            }

            bool ok = mod.m_BehaviorKey == FrameworkBehaviors.CollectNVerbKey
                && mod.m_ItemId == Item
                && mod.m_Count == Count;
            if (!ok)
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (a): ModQuestDef fields after round-trip: key='" +
                    mod.m_BehaviorKey + "' item=" + mod.m_ItemId + " count=" + mod.m_Count + ".");
            return ok;
        }

        private static void Author(CampaignBuilder campaign)
        {
            StageBuilder stage1 = campaign.AddStage("FtkmfCollectN_Stage1");
            stage1.AddCollectQuest(QuestId, Item, Count, DestRealm);
        }

        // (b) Drive the REAL resolver Prefix (reflection) with a constructed ModQuestDef on the registered key,
        // and assert it substitutes a CollectNQuestLogic and returns false. Using the actual patch method (not a
        // re-implementation) proves the Activator path + the registry lookup together.
        private static bool CheckResolverSubstitution()
        {
            ModQuestDef def = MakeCtorSafeDef(FrameworkBehaviors.CollectNVerbKey);

            MethodInfo prefix = typeof(QuestVerbResolverPatch).GetMethod(
                "Prefix", BindingFlags.NonPublic | BindingFlags.Static);
            if (prefix == null)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (b): could not reflect QuestVerbResolverPatch.Prefix.");
                return false;
            }

            // Args mirror the patched signature: (_questDef, _start, _isCurrent, _masterQuestID, _destHexes, ref __result).
            // _isCurrent=false skips DetermineDestinations in the 5-arg ctor (no GameLogic dependency at load time).
            object[] args = new object[]
            {
                def, HexLandID.NullHexLandID, false, 0, new List<HexLand>(), null
            };
            object ret = prefix.Invoke(null, args);

            bool returnedFalse = ret is bool && !((bool)ret); // false => original skipped (substituted)
            QuestLogicBase result = args[5] as QuestLogicBase;
            bool isCollectN = result is CollectNQuestLogic;

            if (!returnedFalse || !isCollectN)
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (b): returnedFalse=" + returnedFalse +
                    " result=" + (result == null ? "null" : result.GetType().FullName) + ".");
            return returnedFalse && isCollectN;
        }

        // (c) count < 1 must be rejected at authoring time. A StageBuilder over a throwaway empty quest array
        // (its internal ctor is reachable from this same-assembly Core type) is enough to exercise the guard.
        private static bool CheckCountGuard()
        {
            StageBuilder stage = new StageBuilder(new Newtonsoft.Json.Linq.JArray());
            try
            {
                stage.AddCollectQuest("ftkmf_collectn_guard", Item, 0, DestRealm);
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (c): AddCollectQuest(count=0) did NOT throw.");
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return true; // expected
            }
        }

        // (d, sanity) With no live party (FTKHub null at load time), the party sum is 0 < N, so the quest is
        // NOT complete. Proves IsCompleteState reads the def's count and is false below N (the "holds N" direction
        // is gameplay, #44). Uses the same ctor-safe def.
        private static bool CheckIncompleteWhenBelowN()
        {
            ModQuestDef def = MakeCtorSafeDef(FrameworkBehaviors.CollectNVerbKey);
            CollectNQuestLogic logic = new CollectNQuestLogic(
                def, HexLandID.NullHexLandID, false, 0, new List<HexLand>());
            bool complete = logic.IsCompleteState(null);
            if (complete)
                Plugin.Log.LogError("SELF-TEST FAIL [collect-n] (d): IsCompleteState true with party holding < N.");
            return !complete;
        }

        /// <summary>
        /// Build a ModQuestDef that the vanilla 5-arg QuestLogicBase ctor can run at LOAD time without a live
        /// game graph. The ctor reads <c>m_StoryQuestID.GetHashCode()</c> (so it must be non-null) and, when
        /// <c>m_GoToPersonOverride == None</c>, dereferences <c>m_Stage</c> (null on a bare def). Setting an
        /// override to a non-None talker (queen) makes the ctor take the override branch and never touch m_Stage;
        /// <c>IsLastQuestOfStage</c> defaults false so the m_IsLastQuest line short-circuits before m_Stage too.
        /// </summary>
        private static ModQuestDef MakeCtorSafeDef(string behaviorKey)
        {
            ModQuestDef def = new ModQuestDef();
            def.m_StoryQuestID = "ftkmf_collectn_selftest";
            def.m_GoToPersonOverride = FTK_talkingHead.ID.queen; // non-None => ctor avoids the null m_Stage deref
            def.m_BehaviorKey = behaviorKey;
            def.m_ItemId = Item;
            def.m_Count = Count;
            return def;
        }
    }
}
