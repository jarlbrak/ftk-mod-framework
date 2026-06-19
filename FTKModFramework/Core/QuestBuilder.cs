using System;

namespace FTKModFramework.Core
{
    /// <summary>
    /// A handle to a quest just appended to a campaign stage, returned for chaining. It carries the quest's
    /// <c>m_StoryQuestID</c> (the globally-unique key the game uses to build its runtime quest chain) and is the
    /// authoring seam for the branch router (#42): <see cref="BranchTo"/> records a flag-conditioned branch edge
    /// and <see cref="OnCompleteSetFlag"/> records an on-complete flag mutation. Both record into the framework
    /// <see cref="BranchSidecar"/> keyed by this quest's string id; NEITHER touches the quest JSON, so quests
    /// stay vanilla data and the branch graph stays a pure framework concern.
    ///
    /// Returned by the <see cref="StageBuilder"/> Add*Quest methods and never constructed directly. It exposes
    /// no engine internals (the builder methods take/return only public pure-data DTOs +
    /// <see cref="QuestBuilder"/> itself).
    /// </summary>
    public sealed class QuestBuilder
    {
        private readonly string _storyQuestId;

        /// <summary>Internal: built by the <see cref="StageBuilder"/> Add*Quest methods.</summary>
        internal QuestBuilder(string storyQuestId)
        {
            _storyQuestId = storyQuestId;
        }

        /// <summary>The quest's globally-unique <c>m_StoryQuestID</c> (the runtime m_QuestLookup key).</summary>
        public string StoryQuestId { get { return _storyQuestId; } }

        /// <summary>
        /// Record a flag-conditioned branch EDGE out of this quest: when ALL <paramref name="conditions"/> pass
        /// (pass NONE for the unconditional DEFAULT edge), the router redirects the story chain from this quest
        /// to <paramref name="nextQuestId"/> instead of the vanilla linear successor. Multiple
        /// <see cref="BranchTo"/> calls on the same quest are evaluated in CALL ORDER, FIRST MATCH wins; put the
        /// unconditional default edge LAST.
        ///
        /// The edge is recorded into the <see cref="BranchSidecar"/> keyed by this quest's
        /// <c>m_StoryQuestID</c>, never into the quest JSON. Each condition's op is validated against the closed
        /// comparison vocabulary (<c>eq</c>/<c>ne</c>/<c>ge</c>/<c>le</c>) HERE, at authoring time.
        /// </summary>
        /// <param name="nextQuestId">The branch target's <c>m_StoryQuestID</c>. The router resolves it within the
        /// SAME initialized GameDefinition (a walk of its <c>m_Stages</c>); a dangling target is a WARN at runtime
        /// and falls through to the vanilla successor (the router never throws). Must be non-empty.</param>
        /// <param name="conditions">The guard: ALL must pass for this edge to fire (none = always fires).</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">If <paramref name="nextQuestId"/> is empty, or any condition has a
        /// null/empty flag or an op outside the closed comparison set.</exception>
        public QuestBuilder BranchTo(string nextQuestId, params BranchCondition[] conditions)
        {
            if (string.IsNullOrEmpty(nextQuestId))
                throw new ArgumentException("QuestBuilder.BranchTo: nextQuestId must be non-empty.", "nextQuestId");

            // params => never null in practice, but treat null as the empty (unconditional) edge.
            BranchCondition[] conds = conditions ?? new BranchCondition[0];
            for (int i = 0; i < conds.Length; i++)
            {
                BranchCondition c = conds[i];
                if (c == null)
                    throw new ArgumentException(
                        "QuestBuilder.BranchTo: condition[" + i + "] is null (quest '" + _storyQuestId + "').", "conditions");
                if (string.IsNullOrEmpty(c.Flag))
                    throw new ArgumentException(
                        "QuestBuilder.BranchTo: condition[" + i + "].Flag must be non-empty (quest '" + _storyQuestId + "').", "conditions");
                BranchEvaluator.ValidateCompareOp(c.Op); // closed comparison vocabulary, validated at authoring time
            }

            BranchSidecar.Instance.AddRule(new BranchRule
            {
                QuestId = _storyQuestId,
                Conditions = conds,
                NextQuestId = nextQuestId
            });
            return this;
        }

        /// <summary>
        /// Record an on-complete flag MUTATION for this quest: when this quest finishes, the router applies it to
        /// the campaign flag store BEFORE evaluating this quest's branch conditions (so a quest can set a flag on
        /// completion and branch on that same flag). The mutation is recorded into the <see cref="BranchSidecar"/>
        /// keyed by this quest's <c>m_StoryQuestID</c>, never into the quest JSON.
        /// </summary>
        /// <param name="flag">The campaign flag key to mutate. Must be non-empty.</param>
        /// <param name="op">The mutation op: <c>set</c> (assign <paramref name="value"/>) or <c>add</c> (add
        /// <paramref name="value"/> to the current value).</param>
        /// <param name="value">The assigned value (<c>set</c>) or the delta (<c>add</c>).</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">If <paramref name="flag"/> is empty or <paramref name="op"/> is
        /// outside the closed mutation set (<c>set</c>/<c>add</c>).</exception>
        public QuestBuilder OnCompleteSetFlag(string flag, string op, int value)
        {
            if (string.IsNullOrEmpty(flag))
                throw new ArgumentException("QuestBuilder.OnCompleteSetFlag: flag must be non-empty.", "flag");
            BranchEvaluator.ValidateMutateOp(op); // closed mutation vocabulary, validated at authoring time

            BranchSidecar.Instance.AddFlagOp(_storyQuestId, new FlagOp { Flag = flag, Op = op, Value = value });
            return this;
        }
    }
}
