using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// One branch EDGE out of a quest: when ALL <see cref="Conditions"/> pass (empty/null = always passes = the
    /// unconditional default edge), the story chain is redirected to <see cref="NextQuestId"/>. INTERNAL pure
    /// data; the public surface only exposes <see cref="BranchCondition"/>/<see cref="FlagOp"/>.
    ///
    /// <see cref="QuestId"/> is the SOURCE quest's STRING <c>QuestDefBase.m_StoryQuestID</c> (NOT the int
    /// <c>QuestLogicBase.m_StoryQuestID</c> HashID); it is the sidecar key under which this rule is filed.
    /// </summary>
    internal sealed class BranchRule
    {
        public string QuestId;
        public BranchCondition[] Conditions;
        public string NextQuestId;
    }

    /// <summary>
    /// The framework-owned branch table, populated by <see cref="QuestBuilder"/> at authoring time and read by
    /// <see cref="QuestRouterPatch"/> at runtime. It maps a completed quest's STRING
    /// <c>QuestDefBase.m_StoryQuestID</c> to that quest's on-complete <see cref="FlagOp"/>s AND its ordered
    /// <see cref="BranchRule"/>s. The branch graph deliberately lives HERE, NEVER inside <c>QuestDefBase</c> /
    /// the quest JSON: the quests stay vanilla data, and the routing is a pure framework concern keyed by the
    /// same stable string the game uses for its own chain.
    ///
    /// A SINGLE static sidecar is shared across the process (one campaign engine per game). The builders record
    /// into <see cref="Instance"/>; the router reads it. Authoring-time only mutation (no co-op/runtime writes),
    /// so no host guard is needed here.
    /// </summary>
    internal sealed class BranchSidecar
    {
        /// <summary>The process-wide sidecar the builders write and the router reads.</summary>
        internal static readonly BranchSidecar Instance = new BranchSidecar();

        // questId (string m_StoryQuestID) -> on-complete flag mutations, in authored order.
        private readonly Dictionary<string, List<FlagOp>> _onComplete = new Dictionary<string, List<FlagOp>>();

        // questId (string m_StoryQuestID) -> branch edges, in authored (first-match) order.
        private readonly Dictionary<string, List<BranchRule>> _rules = new Dictionary<string, List<BranchRule>>();

        /// <summary>Record a branch edge out of <c>rule.QuestId</c>, appended after any earlier edges (first-match order preserved).</summary>
        internal void AddRule(BranchRule rule)
        {
            List<BranchRule> list;
            if (!_rules.TryGetValue(rule.QuestId, out list))
            {
                list = new List<BranchRule>();
                _rules[rule.QuestId] = list;
            }
            list.Add(rule);
        }

        /// <summary>Record an on-complete flag mutation for <paramref name="questId"/>, appended in authored order.</summary>
        internal void AddFlagOp(string questId, FlagOp op)
        {
            List<FlagOp> list;
            if (!_onComplete.TryGetValue(questId, out list))
            {
                list = new List<FlagOp>();
                _onComplete[questId] = list;
            }
            list.Add(op);
        }

        /// <summary>True when this quest has either an on-complete flag op or a branch rule (i.e. the router cares about it).</summary>
        internal bool Has(string questId)
        {
            return questId != null && (_rules.ContainsKey(questId) || _onComplete.ContainsKey(questId));
        }

        /// <summary>The on-complete flag mutations for <paramref name="questId"/>, or null if none recorded.</summary>
        internal List<FlagOp> GetFlagOps(string questId)
        {
            List<FlagOp> list;
            return (questId != null && _onComplete.TryGetValue(questId, out list)) ? list : null;
        }

        /// <summary>The ordered branch edges out of <paramref name="questId"/>, or null if none recorded.</summary>
        internal List<BranchRule> GetRules(string questId)
        {
            List<BranchRule> list;
            return (questId != null && _rules.TryGetValue(questId, out list)) ? list : null;
        }
    }
}
