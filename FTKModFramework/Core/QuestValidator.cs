using System.Collections.Generic;
using GridEditor;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core
{
    /// <summary>
    /// LOAD-TIME campaign validation pre-pass (#43, spec #37 P4). Walks an authored campaign's quests in
    /// DECLARATION order (stage order x within-stage order, flattened) plus the framework
    /// <see cref="BranchSidecar"/>, and emits precise diagnostics through the SAME shared
    /// <see cref="ValidationReport"/> channel the data-content loader uses (errors fail the campaign, warnings
    /// flag hazards). It is a pure structural pass: it needs NO live game and does NOT call
    /// <c>GameDefinition.Initialize</c> (so it never depends on Initialize's <c>[JsonIgnore]</c> side effects
    /// such as <c>IsLastQuestOfStage</c>/<c>m_NextStoryQuestID</c>); it computes the chain itself from
    /// declaration order, exactly the way the game would build it.
    ///
    /// CHECKS (decompile-grounded against kb_50d90b0e / kb_dfc6bd89):
    /// <list type="number">
    /// <item>Duplicate <c>m_StoryQuestID</c> across all stages -> ERROR. (The game keys m_QuestLookup by this
    /// string; a duplicate silently collapses two quests onto one chain node.)</item>
    /// <item>ID resolution per quest type's target field(s) against its <c>FTK_*DB</c> -> ERROR on an
    /// unresolved id (or the <c>None</c> sentinel where a target is REQUIRED).</item>
    /// <item>Unregistered/wrong-kind custom-verb key on a <see cref="ModQuestDef"/> -> WARN (the runtime
    /// resolver falls through to a vanilla Visit, so the quest is harmlessly skipped as a custom objective).</item>
    /// <item><c>m_Count &lt; 1</c> on a collect-N quest -> ERROR (the builder already rejects it; this is the
    /// formal load-time gate for any campaign that reached the validator another way).</item>
    /// <item>Closed-operator validation on the sidecar: every <see cref="BranchCondition.Op"/> in
    /// <c>eq/ne/ge/le</c> and every <see cref="FlagOp.Op"/> in <c>set/add</c> (two DISJOINT vocabularies,
    /// reusing <see cref="BranchEvaluator.IsCompareOp"/>/<see cref="BranchEvaluator.IsMutateOp"/>) -> ERROR on
    /// an unknown op.</item>
    /// <item>Dangling edge (a <see cref="BranchRule.QuestId"/> source or <see cref="BranchRule.NextQuestId"/>
    /// target not among the campaign's quest keys) -> WARN (the router falls through harmlessly).</item>
    /// <item>Cross-stage skip (a branch target whose stage is neither the source quest's stage nor the
    /// immediately-next stage in declaration order) -> WARN (the skipped stage's m_StageCompleted stays false;
    /// cosmetic).</item>
    /// <item>VICTORY-REACHABILITY (the headline ERROR): over a directed graph of POSSIBLE successors, every
    /// quest reachable from the start quest must be able to reach the victory quest (R subset of V). This single
    /// check catches BOTH dead-ends and branch cycles that trap without an exit toward victory.</item>
    /// </list>
    ///
    /// SUCCESSOR MODEL (matches <see cref="QuestRouterPatch"/>'s first-match semantics exactly): the router
    /// evaluates branch rules in authored order, FIRST MATCH wins, and an empty-conditions rule is the
    /// UNCONDITIONAL DEFAULT edge that ALWAYS fires. So a quest's possible successors are: every branch-rule
    /// target UP TO AND INCLUDING the first unconditional rule; PLUS, only if NO unconditional rule exists, the
    /// LINEAR (declaration-order) successor (taken when no conditional rule matches). Rules listed after the
    /// first unconditional rule are unreachable and contribute no edge. A dangling target is NOT a graph edge
    /// (the router falls through to the vanilla successor at runtime), so it is modeled AS the linear successor.
    ///
    /// Internal to Core; no public API. Reused by <see cref="QuestValidatorSelfTest"/> and wired into
    /// <see cref="Adventures.AddCampaignFromTemplate"/> as the load pre-pass.
    /// </summary>
    internal static class QuestValidator
    {
        /// <summary>
        /// A quest in declaration order, with its string key, owning stage index, and the typed def (for id
        /// resolution). Built once per validate so the graph/checks share one flattened view.
        /// </summary>
        private sealed class QuestNode
        {
            public string Key;          // QuestDefBase.m_StoryQuestID (the sidecar / chain key)
            public int StageIndex;      // declaration-order stage index (for the cross-stage-skip check)
            public QuestDefBase Def;     // the typed concrete def (BountyQuestDef / ModQuestDef / ...)
            public string LinearNext;    // the next quest's Key in flattened declaration order (null on victory)
        }

        /// <summary>
        /// Validate <paramref name="gd"/> against <paramref name="report"/>. Returns true when the campaign has
        /// NO errors (warnings are allowed). Never throws on a structurally odd campaign: a null/empty stage
        /// list records an error and returns false. The report is the SAME shared channel the data loader logs.
        /// </summary>
        internal static bool Validate(GameDefinition gd, string campaignName, ValidationReport report)
        {
            string who = "[campaign '" + (campaignName ?? "<unnamed>") + "']";

            if (gd == null || gd.m_Stages == null || gd.m_Stages.Count == 0)
            {
                report.Error(who + " has no stages to validate.");
                return false;
            }

            int errorsBefore = report.Errors.Count;

            // Flatten the quests in declaration order, building the linear-successor chain ourselves (no
            // Initialize side effects). Records the duplicate-key ERROR (check 1) as it indexes by key.
            List<QuestNode> nodes;
            Dictionary<string, QuestNode> byKey;
            Flatten(gd, who, report, out nodes, out byKey);

            if (nodes.Count == 0)
            {
                report.Error(who + " has stages but no quests.");
                return false;
            }

            // 2 + 4: per-quest id resolution and collect-N count guard.
            for (int i = 0; i < nodes.Count; i++)
                ValidateQuestTargets(nodes[i], who, report);

            // 3: unregistered/wrong-kind custom-verb keys (WARN, quest skipped).
            for (int i = 0; i < nodes.Count; i++)
                ValidateCustomVerb(nodes[i], who, report);

            // 5: closed-operator validation on the sidecar (two disjoint vocabularies).
            ValidateOperators(nodes, who, report);

            // 6 + 7: dangling-edge and cross-stage-skip WARNs.
            ValidateEdges(nodes, byKey, who, report);

            // 8: victory-reachability (the headline ERROR). Start = stage[0].quest[0]; victory = last quest of
            // the last stage, by declaration order (NOT IsLastQuestOfStage, which is an Initialize side effect).
            ValidateVictoryReachability(nodes, byKey, who, report);

            return report.Errors.Count == errorsBefore;
        }

        // ---- flatten + duplicate-key check (1) -------------------------------------------------------------

        private static void Flatten(
            GameDefinition gd, string who, ValidationReport report,
            out List<QuestNode> nodes, out Dictionary<string, QuestNode> byKey)
        {
            nodes = new List<QuestNode>();
            byKey = new Dictionary<string, QuestNode>();

            for (int s = 0; s < gd.m_Stages.Count; s++)
            {
                GameStage stage = gd.m_Stages[s];
                if (stage == null || stage.m_Quests == null) continue;
                for (int q = 0; q < stage.m_Quests.Count; q++)
                {
                    QuestDefBase def = stage.m_Quests[q];
                    if (def == null) continue;

                    string key = def.m_StoryQuestID;
                    if (string.IsNullOrEmpty(key))
                    {
                        report.Error(who + " stage[" + s + "].quest[" + q + "] has an empty m_StoryQuestID.");
                        continue; // cannot index/chain a keyless quest
                    }

                    if (byKey.ContainsKey(key))
                    {
                        // Check 1: a duplicate string key collapses two chain nodes in m_QuestLookup.
                        report.Error(who + " duplicate m_StoryQuestID '" + key +
                            "' (stage[" + s + "].quest[" + q + "] repeats an earlier quest's key).");
                        continue; // keep the FIRST occurrence as the canonical node
                    }

                    QuestNode node = new QuestNode();
                    node.Key = key;
                    node.StageIndex = s;
                    node.Def = def;
                    nodes.Add(node);
                    byKey[key] = node;
                }
            }

            // The linear successor is the NEXT node in flattened declaration order (null for the last = victory).
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].LinearNext = (i + 1 < nodes.Count) ? nodes[i + 1].Key : null;
        }

        // ---- id resolution (2) + collect-N count guard (4) ------------------------------------------------

        /// <summary>
        /// Resolve each non-<c>None</c> target id on a quest against its <c>FTK_*DB</c> (the established
        /// <c>Content.Db&lt;T&gt;().GetEntryByInt((int)id)</c> path); a null result is an ERROR naming quest +
        /// id. Where a target is REQUIRED for the quest to be playable (a bounty's enemy set/list, a dungeon's
        /// id, a mini-encounter's id, a collect-N's item), the <c>None</c> sentinel (-1) is itself an ERROR.
        /// Realm (<c>m_SpecifiedRealm</c>) is checked when Specified; collect-N <c>m_Count</c> is gated here too.
        /// </summary>
        private static void ValidateQuestTargets(QuestNode node, string who, ValidationReport report)
        {
            QuestDefBase def = node.Def;

            // ModQuestDef is the framework collect-N def: check FIRST (it subclasses SingleQuestDefBase, so the
            // `is SingleQuestDefBase` realm check below still applies and runs after this).
            ModQuestDef mod = def as ModQuestDef;
            if (mod != null)
            {
                // Check 4: collect-N count must be >= 1 (the builder rejects it; this is the formal load gate).
                if (mod.m_Count < 1)
                    report.Error(who + " quest '" + node.Key + "' (collect-N) has m_Count=" + mod.m_Count +
                        " (must be >= 1).");
                // Required target: the counted item.
                RequireItem(mod.m_ItemId, node.Key, "m_ItemId", who, report);
                // fall through to the realm check below (ModQuestDef : SingleQuestDefBase).
            }
            else
            {
                BountyQuestDef bounty = def as BountyQuestDef;
                if (bounty != null)
                {
                    // A bounty needs SOME target: the priority m_EnemySet OR a non-empty m_Enemies fallback.
                    bool hasEnemySet = bounty.m_EnemySet != FTK_enemySet.ID.None;
                    bool hasEnemies = bounty.m_Enemies != null && bounty.m_Enemies.Count > 0;
                    if (!hasEnemySet && !hasEnemies)
                        report.Error(who + " quest '" + node.Key + "' (kill) has neither m_EnemySet nor " +
                            "m_Enemies set (a bounty needs a target).");
                    if (hasEnemySet && Content.Db<FTK_enemySetDB>().GetEntry(bounty.m_EnemySet) == null)
                        Unresolved(node.Key, "m_EnemySet", bounty.m_EnemySet.ToString(),
                            (int)bounty.m_EnemySet, "FTK_enemySetDB", who, report);
                    if (hasEnemies)
                        for (int e = 0; e < bounty.m_Enemies.Count; e++)
                            if (Content.Db<FTK_enemyCombatDB>().GetEntry(bounty.m_Enemies[e]) == null)
                                Unresolved(node.Key, "m_Enemies[" + e + "]", bounty.m_Enemies[e].ToString(),
                                    (int)bounty.m_Enemies[e], "FTK_enemyCombatDB", who, report);
                }

                DungeonQuestDef dungeon = def as DungeonQuestDef;
                if (dungeon != null)
                {
                    if (dungeon.m_DungeonID == FTK_dungeonEncounter.ID.None)
                        report.Error(who + " quest '" + node.Key + "' (clear) has m_DungeonID=None (required).");
                    else if (Content.Db<FTK_dungeonEncounterDB>().GetEntry(dungeon.m_DungeonID) == null)
                        Unresolved(node.Key, "m_DungeonID", dungeon.m_DungeonID.ToString(),
                            (int)dungeon.m_DungeonID, "FTK_dungeonEncounterDB", who, report);
                }

                MiniEncounterQuestDef mini = def as MiniEncounterQuestDef;
                if (mini != null)
                {
                    if (mini.m_MiniEncounterID == FTK_miniEncounter.ID.None)
                        report.Error(who + " quest '" + node.Key +
                            "' (encounter) has m_MiniEncounterID=None (required).");
                    else if (Content.Db<FTK_miniEncounterDB>().GetEntry(mini.m_MiniEncounterID) == null)
                        Unresolved(node.Key, "m_MiniEncounterID", mini.m_MiniEncounterID.ToString(),
                            (int)mini.m_MiniEncounterID, "FTK_miniEncounterDB", who, report);
                    // Optional enemy target on a mini-encounter quest: only resolved when set (non-None).
                    if (mini.m_EnemySet != FTK_enemySet.ID.None
                        && Content.Db<FTK_enemySetDB>().GetEntry(mini.m_EnemySet) == null)
                        Unresolved(node.Key, "m_EnemySet", mini.m_EnemySet.ToString(),
                            (int)mini.m_EnemySet, "FTK_enemySetDB", who, report);
                }

                VisitQuestDef visit = def as VisitQuestDef;
                if (visit != null)
                {
                    // A plain Visit needs no target id; its optional ids (mini-encounter / sanctum / delivery
                    // item) are resolved only when set (non-None), since None means "no such facet".
                    if (visit.m_MiniEncounterID != FTK_miniEncounter.ID.None
                        && Content.Db<FTK_miniEncounterDB>().GetEntry(visit.m_MiniEncounterID) == null)
                        Unresolved(node.Key, "m_MiniEncounterID", visit.m_MiniEncounterID.ToString(),
                            (int)visit.m_MiniEncounterID, "FTK_miniEncounterDB", who, report);
                    if (visit.m_SanctumID != FTK_sanctumStats.ID.None
                        && Content.Db<FTK_sanctumStatsDB>().GetEntry(visit.m_SanctumID) == null)
                        Unresolved(node.Key, "m_SanctumID", visit.m_SanctumID.ToString(),
                            (int)visit.m_SanctumID, "FTK_sanctumStatsDB", who, report);
                    if (visit.m_QuestItem != FTK_itembase.ID.None)
                        RequireItem(visit.m_QuestItem, node.Key, "m_QuestItem", who, report);
                }
            }

            // Realm destination (shared by every SingleQuestDefBase, incl. ModQuestDef): when the quest resolves
            // its destination in a Specified realm, that realm must be a real FTK_realm.ID row.
            SingleQuestDefBase single = def as SingleQuestDefBase;
            if (single != null
                && single.m_DestinationRealmType == QuestLogicBase.DestinationRealmType.Specified
                && single.m_SpecifiedRealm != FTK_realm.ID.None
                && Content.Db<FTK_realmDB>().GetEntry(single.m_SpecifiedRealm) == null)
            {
                Unresolved(node.Key, "m_SpecifiedRealm", single.m_SpecifiedRealm.ToString(),
                    (int)single.m_SpecifiedRealm, "FTK_realmDB", who, report);
            }
        }

        /// <summary>Record the "id does not resolve" ERROR, naming quest + field + id (string) + int + DB.</summary>
        private static void Unresolved(
            string questKey, string field, string idName, int intId, string dbName, string who, ValidationReport report)
        {
            report.Error(who + " quest '" + questKey + "' " + field + " '" + idName +
                "' (" + intId + ") does not resolve in " + dbName + ".");
        }

        /// <summary>An item target is REQUIRED: None is an ERROR; otherwise resolve it across the weapon+item DBs.</summary>
        private static void RequireItem(
            FTK_itembase.ID id, string questKey, string field, string who, ValidationReport report)
        {
            if (id == FTK_itembase.ID.None)
            {
                report.Error(who + " quest '" + questKey + "' " + field + " is None (an item target is required).");
                return;
            }
            // FTK_itembase.ID spans BOTH the items DB and the weapons DB (see Content.cs GetEntryByInt fallback);
            // resolve in either, matching the game's own item-resolution path.
            object row = Content.Db<FTK_itemsDB>().GetEntry(id);
            if (row == null) row = Content.Db<FTK_weaponStats2DB>().GetEntry(id);
            if (row == null)
                report.Error(who + " quest '" + questKey + "' " + field + " '" + id +
                    "' (" + (int)id + ") does not resolve in FTK_itemsDB or FTK_weaponStats2DB.");
        }

        // ---- unregistered custom-verb key (3) -------------------------------------------------------------

        private static void ValidateCustomVerb(QuestNode node, string who, ValidationReport report)
        {
            ModQuestDef mod = node.Def as ModQuestDef;
            if (mod == null) return; // only framework custom-verb defs carry a behaviour key

            System.Type t;
            BehaviorKind kind;
            if (!BehaviorRegistry.TryResolve(mod.m_BehaviorKey, out t, out kind) || kind != BehaviorKind.QuestLogic)
            {
                // Check 3: WARN, not ERROR. The runtime resolver falls through to a vanilla Visit QuestLogic
                // (ModQuestDef.GetQuestType() == Visit), so the quest is harmlessly skipped as a custom objective.
                report.Warning(who + " quest '" + node.Key + "' has unregistered/wrong-kind verb key '" +
                    (mod.m_BehaviorKey ?? "<null>") + "' (not a registered QuestLogic verb); the custom " +
                    "objective is skipped (router falls through to a vanilla Visit).");
            }
        }

        // ---- closed-operator validation on the sidecar (5) ------------------------------------------------

        /// <summary>
        /// Every branch condition op must be in the closed COMPARISON set and every on-complete flag op in the
        /// closed MUTATION set (two disjoint vocabularies; reuse <see cref="BranchEvaluator"/>). An unknown op
        /// is an ERROR. Only the campaign's OWN quest keys are inspected (the sidecar is process-wide).
        /// </summary>
        private static void ValidateOperators(List<QuestNode> nodes, string who, ValidationReport report)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                string key = nodes[i].Key;

                List<BranchRule> rules = BranchSidecar.Instance.GetRules(key);
                if (rules != null)
                    for (int r = 0; r < rules.Count; r++)
                    {
                        BranchCondition[] conds = rules[r].Conditions;
                        if (conds == null) continue; // null/empty == unconditional default edge (no op to check)
                        for (int c = 0; c < conds.Length; c++)
                        {
                            BranchCondition cond = conds[c];
                            if (cond != null && !BranchEvaluator.IsCompareOp(cond.Op))
                                report.Error(who + " quest '" + key + "' branch rule[" + r + "] condition[" + c +
                                    "] has unknown comparison op '" + (cond.Op ?? "<null>") +
                                    "' (expected one of: " + BranchEvaluator.CompareOps + ").");
                        }
                    }

                List<FlagOp> ops = BranchSidecar.Instance.GetFlagOps(key);
                if (ops != null)
                    for (int o = 0; o < ops.Count; o++)
                    {
                        FlagOp op = ops[o];
                        if (op != null && !BranchEvaluator.IsMutateOp(op.Op))
                            report.Error(who + " quest '" + key + "' on-complete flag op[" + o +
                                "] has unknown mutation op '" + (op.Op ?? "<null>") +
                                "' (expected one of: " + BranchEvaluator.MutateOps + ").");
                    }
            }
        }

        // ---- dangling-edge (6) + cross-stage-skip (7) WARNs -----------------------------------------------

        private static void ValidateEdges(
            List<QuestNode> nodes, Dictionary<string, QuestNode> byKey, string who, ValidationReport report)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                QuestNode src = nodes[i];
                List<BranchRule> rules = BranchSidecar.Instance.GetRules(src.Key);
                if (rules == null) continue;

                for (int r = 0; r < rules.Count; r++)
                {
                    string target = rules[r].NextQuestId;

                    QuestNode dst;
                    if (target == null || !byKey.TryGetValue(target, out dst))
                    {
                        // Check 6: a target not among the campaign's quest keys is dangling -> WARN. (The router
                        // falls through to the vanilla successor, so this is harmless, never an error.)
                        report.Warning(who + " quest '" + src.Key + "' branch rule[" + r + "] target '" +
                            (target ?? "<null>") + "' is not a campaign quest (dangling edge; router falls " +
                            "through to the linear successor).");
                        continue;
                    }

                    // Check 7: a resolvable target whose stage is neither the source's stage nor the
                    // immediately-next stage in declaration order skips a stage -> WARN (cosmetic: the skipped
                    // stage's m_StageCompleted stays false).
                    if (dst.StageIndex != src.StageIndex && dst.StageIndex != src.StageIndex + 1)
                        report.Warning(who + " quest '" + src.Key + "' (stage " + src.StageIndex +
                            ") branches to '" + target + "' (stage " + dst.StageIndex +
                            "), skipping stage(s); the skipped stage's completion flag stays false (cosmetic).");
                }
            }
        }

        // ---- victory-reachability (8, headline) -----------------------------------------------------------

        /// <summary>
        /// Build the possible-successor graph (see the class remarks for the first-match / unconditional-default
        /// edge model), then ERROR if any quest reachable from the start (R) cannot itself reach the victory
        /// quest (V): R must be a subset of V. Catches dead-ends AND branch cycles that trap without an exit.
        /// </summary>
        private static void ValidateVictoryReachability(
            List<QuestNode> nodes, Dictionary<string, QuestNode> byKey, string who, ValidationReport report)
        {
            string start = nodes[0].Key;
            string victory = nodes[nodes.Count - 1].Key; // last quest of the last stage, by declaration order

            // Forward adjacency (possible successors) and its reverse, over the same edge set.
            Dictionary<string, List<string>> forward = BuildSuccessorGraph(nodes, byKey);
            Dictionary<string, List<string>> reverse = Reverse(forward);

            HashSet<string> reachable = Reach(forward, start);          // R
            HashSet<string> canReachVictory = Reach(reverse, victory);   // V (backward from victory)

            // R subset of V: find a representative quest reachable from start that cannot reach victory.
            foreach (QuestNode node in nodes)
            {
                if (reachable.Contains(node.Key) && !canReachVictory.Contains(node.Key))
                {
                    report.Error(who + " quest '" + node.Key + "' is reachable from the start ('" + start +
                        "') but cannot reach the victory quest ('" + victory + "') -> the campaign can become " +
                        "unwinnable (dead-end or branch cycle with no exit toward victory).");
                    return; // one precise representative is enough to fail + name the campaign
                }
            }
        }

        /// <summary>
        /// POSSIBLE successors per quest. For each quest: scan its branch rules in authored order; add each
        /// target (or, when dangling, the linear successor) UP TO AND INCLUDING the first UNCONDITIONAL rule.
        /// If no unconditional rule exists, ALSO add the linear successor (taken when no condition matches).
        /// </summary>
        private static Dictionary<string, List<string>> BuildSuccessorGraph(
            List<QuestNode> nodes, Dictionary<string, QuestNode> byKey)
        {
            Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();

            for (int i = 0; i < nodes.Count; i++)
            {
                QuestNode node = nodes[i];
                List<string> succ = new List<string>();
                bool sawUnconditional = false;

                List<BranchRule> rules = BranchSidecar.Instance.GetRules(node.Key);
                if (rules != null)
                {
                    for (int r = 0; r < rules.Count; r++)
                    {
                        BranchRule rule = rules[r];
                        // A resolvable target is the edge; a dangling target falls through to the linear
                        // successor at runtime, so model it AS the linear successor here.
                        string edge = (rule.NextQuestId != null && byKey.ContainsKey(rule.NextQuestId))
                            ? rule.NextQuestId
                            : node.LinearNext;
                        AddEdge(succ, edge);

                        if (IsUnconditional(rule))
                        {
                            // First unconditional rule ALWAYS fires => later rules + the linear successor are
                            // unreachable. Stop adding edges.
                            sawUnconditional = true;
                            break;
                        }
                    }
                }

                // No unconditional rule guarantees a redirect => the linear successor is still a possible
                // outcome (no conditional rule matched). Add it (null = victory has no successor).
                if (!sawUnconditional)
                    AddEdge(succ, node.LinearNext);

                graph[node.Key] = succ;
            }
            return graph;
        }

        /// <summary>An unconditional rule has no conditions (null/empty) => its Matches() is always true.</summary>
        private static bool IsUnconditional(BranchRule rule)
        {
            return rule.Conditions == null || rule.Conditions.Length == 0;
        }

        private static void AddEdge(List<string> succ, string target)
        {
            if (target != null && !succ.Contains(target)) succ.Add(target);
        }

        private static Dictionary<string, List<string>> Reverse(Dictionary<string, List<string>> forward)
        {
            Dictionary<string, List<string>> rev = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in forward)
            {
                if (!rev.ContainsKey(kv.Key)) rev[kv.Key] = new List<string>();
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    string to = kv.Value[i];
                    List<string> list;
                    if (!rev.TryGetValue(to, out list)) { list = new List<string>(); rev[to] = list; }
                    list.Add(kv.Key);
                }
            }
            return rev;
        }

        /// <summary>BFS over an adjacency map from a single source, returning every reachable key (incl. source).</summary>
        private static HashSet<string> Reach(Dictionary<string, List<string>> graph, string source)
        {
            HashSet<string> seen = new HashSet<string>();
            Queue<string> queue = new Queue<string>();
            seen.Add(source);
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                List<string> next;
                if (!graph.TryGetValue(cur, out next)) continue;
                for (int i = 0; i < next.Count; i++)
                    if (next[i] != null && seen.Add(next[i]))
                        queue.Enqueue(next[i]);
            }
            return seen;
        }
    }
}
