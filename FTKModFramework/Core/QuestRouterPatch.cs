using System.Collections.Generic;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The light-DAG BRANCH ROUTER (#42, spec #37 P3b). A host-only Harmony POSTFIX on
    /// <c>GameDefinition.GetNextQuest()</c> that redirects the linear story chain at flag-conditioned branch
    /// points. Branch rules + on-complete flag ops live in the framework <see cref="BranchSidecar"/> keyed by the
    /// just-completed quest's STRING <c>QuestDefBase.m_StoryQuestID</c> (NEVER inside <c>QuestDefBase</c>).
    ///
    /// POSTFIX, NOT PREFIX (decompile-grounded, kb_50d90b0e): <c>GetNextQuest</c>'s only in-method side effect is
    /// <c>FTKGameStats.Inst.m_QuestTaken++</c>, a write-only analytics counter read nowhere in gameplay. A Postfix
    /// runs AFTER the single original call and only reads <c>__instance.StoryQuestID</c> + overwrites
    /// <c>ref __result</c>; it never re-enters the method, so the counter increments EXACTLY ONCE. A Prefix
    /// returning false that substituted the result would either skip or (via a re-invoke) double-count it, and the
    /// method has no try/catch.
    ///
    /// EXACTLY-ONCE CADENCE (kb_50d90b0e #5): <c>GetNextQuest</c> has a single call site
    /// (<c>GameEventManager._advanceStoryQuest</c>), itself called once per quest completion by the FSM-driven
    /// <c>GetNextStoryQuest</c>. There are no speculative/loop calls, so this Postfix fires exactly once per real
    /// advance. THAT is what makes applying the just-completed quest's on-complete <see cref="FlagOp"/>s INCLUDING
    /// the non-idempotent <c>add</c> SAFE here without any per-quest one-shot guard.
    ///
    /// IDEMPOTENCY (NFR-4): this is NOT a one-shot registration patch and needs NO <c>_done</c> flag. It is a
    /// per-call transform whose only state writes are host-authoritative flag-store writes (themselves once per
    /// advance) and a <c>ref __result</c> swap; re-running it on the same advance is structurally impossible given
    /// the single call site, and it has no shared static mutable state of its own. The patch CLASS is installed
    /// exactly once via the plugin's single <c>Harmony.PatchAll()</c>.
    ///
    /// HOST-ONLY + FREE CLIENT SYNC (kb_50d90b0e #3): the advance chain is host-gated; after advancing,
    /// <c>SyncProgress</c> -&gt; <c>SyncProgressRPC</c> pushes the resulting <c>m_StoryQuestID</c> string to
    /// clients (clients do NOT recompute <c>GetNextQuest</c>). So the body is guarded on
    /// <c>PhotonNetwork.isMasterClient</c> and the redirect syncs for free.
    ///
    /// For this slice the patch installs UNCONDITIONALLY: it is a no-op when the sidecar has no entry for the
    /// current key, so a vanilla game (or any quest with no branch rules) is untouched. #43 wraps the campaign
    /// engine behind the <c>EnableCampaignEngine</c> config gate.
    /// </summary>
    [HarmonyPatch(typeof(GameDefinition), "GetNextQuest")]
    internal static class QuestRouterPatch
    {
        // The method is an INSTANCE method, so __instance is the GameDefinition; the just-completed quest's STRING
        // key is read off it via the public getter (__instance.StoryQuestID => m_GameDefData.m_StoryQuestID), NOT
        // a parameter. __result is the vanilla linear successor (or null at chain end) that we may redirect.
        private static void Postfix(GameDefinition __instance, ref QuestDefBase __result)
        {
            // Host authority: only the master client advances + computes the next quest; clients receive the
            // resulting string key via the host's quest-table sync. Same guard the rest of the engine uses.
            if (!PhotonNetwork.isMasterClient) return;
            if (__instance == null) return;

            string key = __instance.StoryQuestID; // the just-completed quest's string m_StoryQuestID
            if (!BranchSidecar.Instance.Has(key)) return; // no branch concern for this quest -> leave __result vanilla

            // 1) Apply this quest's on-complete flag mutations FIRST, so its own branch conditions can observe
            //    them ("set a flag on completion and branch on that same flag"). Once per advance (cadence above)
            //    => non-idempotent `add` is safe. SetFlag is itself host-guarded (we are host here).
            List<FlagOp> ops = BranchSidecar.Instance.GetFlagOps(key);
            if (ops != null)
            {
                for (int i = 0; i < ops.Count; i++)
                {
                    FlagOp op = ops[i];
                    int newValue = BranchEvaluator.Apply(Campaign.GetFlag(op.Flag), op.Op, op.Value);
                    Campaign.SetFlag(op.Flag, newValue);
                }
            }

            // 2) Evaluate the branch edges in authored order, FIRST MATCH wins.
            List<BranchRule> rules = BranchSidecar.Instance.GetRules(key);
            if (rules == null) return; // on-complete flags applied, but no edges -> vanilla linear successor

            for (int i = 0; i < rules.Count; i++)
            {
                BranchRule rule = rules[i];
                if (!Matches(rule.Conditions)) continue;

                // 3) First matching rule: resolve the target by walking the SAME initialized def's m_Stages
                //    (m_QuestLookup is private). A non-null result has a non-null m_Stage + a resolvable key, so
                //    the caller (_advanceStoryQuest) accepts the redirect.
                QuestDefBase target = ResolveTarget(__instance, rule.NextQuestId);
                if (target != null)
                {
                    Plugin.Log.LogInfo("QuestRouter: '" + key + "' redirected to branch target '" +
                        rule.NextQuestId + "' (rule " + i + ").");
                    __result = target;
                }
                else
                {
                    // 4) Dangling target: WARN + leave __result UNCHANGED (fall through to the vanilla successor).
                    //    The router must NEVER throw; #43's validator enforces dangling=WARN at load time.
                    Plugin.Log.LogWarning("QuestRouter: '" + key + "' branch target '" +
                        (rule.NextQuestId ?? "<null>") + "' not found in m_Stages; leaving the vanilla successor.");
                }
                return; // first match wins (whether resolved or dangling): stop scanning edges
            }
            // 5) No matching rule -> leave __result unchanged (vanilla linear successor).
        }

        /// <summary>True when ALL conditions pass (empty/null = unconditional default edge = always matches).</summary>
        private static bool Matches(BranchCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return true; // unconditional default edge
            for (int i = 0; i < conditions.Length; i++)
            {
                BranchCondition c = conditions[i];
                if (c == null) return false; // defensive; authoring rejects null conditions
                if (!BranchEvaluator.Compare(Campaign.GetFlag(c.Flag), c.Op, c.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Resolve a branch target by its string <c>m_StoryQuestID</c> via the PUBLIC <c>m_Stages</c> walk
        /// (<c>m_QuestLookup</c> is private; this needs no reflection). Returns the matching <see cref="QuestDefBase"/>
        /// from the SAME initialized def (so it has a non-null <c>m_Stage</c>), or null if not found (dangling).
        /// </summary>
        private static QuestDefBase ResolveTarget(GameDefinition gd, string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || gd.m_Stages == null) return null;
            for (int s = 0; s < gd.m_Stages.Count; s++)
            {
                GameStage stage = gd.m_Stages[s];
                if (stage == null || stage.m_Quests == null) continue;
                for (int q = 0; q < stage.m_Quests.Count; q++)
                {
                    QuestDefBase quest = stage.m_Quests[q];
                    if (quest != null && quest.m_StoryQuestID == targetId) return quest;
                }
            }
            return null;
        }
    }
}
