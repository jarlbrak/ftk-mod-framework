using System;
using Newtonsoft.Json.Linq;

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
        private readonly JObject _quest; // the quest's live JObject in the stage m_Quests array (for narrative authoring)

        /// <summary>
        /// Internal: built by the <see cref="StageBuilder"/> Add*Quest methods. <paramref name="quest"/> is the
        /// quest's live <c>JObject</c> in the stage's <c>m_Quests</c> array, so the story methods
        /// (<see cref="WithStartStory"/>/<see cref="WithCompleteStory"/>) can append narrative popups to it as DATA;
        /// the branch methods (<see cref="BranchTo"/>/<see cref="OnCompleteSetFlag"/>) never touch it (they record
        /// into the framework <see cref="BranchSidecar"/> only).
        /// </summary>
        internal QuestBuilder(string storyQuestId, JObject quest)
        {
            _storyQuestId = storyQuestId;
            _quest = quest;
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

        /// <summary>
        /// Attach a START-OF-QUEST narrative: a story popup shown the moment this quest becomes the active quest,
        /// authored as DATA on the quest's own <c>m_StartEvents</c> (no Harmony patch, no engine internals).
        ///
        /// Emits ONE <c>StoryEvent.Event</c> spoken by the UserNPC <paramref name="npcKey"/>, with one
        /// <c>StoryEvent.Dialogue</c> page per entry in <paramref name="pages"/> (one page = one popup, advanced
        /// in order). The event sets <c>m_UserNPC = npcKey</c> and <c>m_Talker = None</c>, so the popup uses the
        /// UserNPC's Portrait/Name/Title VERBATIM. <paramref name="npcKey"/> MUST equal an <c>npcs/&lt;key&gt;/</c>
        /// folder shipped with the adventure (the folder name is the key the game scans at gamedef init); see
        /// <see cref="Adventures.RegisterUserNpc"/> for shipping one.
        ///
        /// PAGE TEXT IS RENDERED VERBATIM. The game resolves each page through
        /// <c>Localized&lt;TextStory&gt;</c> then <c>GetUserModText</c>, both of which return the literal string on
        /// a miss, so put the prose itself into each page (no STR_ key, no localization table row needed). Because
        /// the text is run through <c>string.Format</c>, a page MUST NOT contain '{' or '}'.
        /// </summary>
        /// <param name="npcKey">The UserNPC folder key (== <c>npcs/&lt;npcKey&gt;/</c>); non-empty.</param>
        /// <param name="pages">One or more popup pages, each shown verbatim; at least one, none containing '{' or '}'.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">If <paramref name="npcKey"/> is empty, no pages are supplied, or any
        /// page is null/contains '{' or '}'.</exception>
        public QuestBuilder WithStartStory(string npcKey, params string[] pages)
        {
            AppendStory("m_StartEvents", npcKey, pages);
            return this;
        }

        /// <summary>
        /// Attach an ON-COMPLETE narrative: a story popup shown when this quest completes, authored as DATA on the
        /// quest's own <c>m_CompleteEvents</c>. Identical shape and rules to <see cref="WithStartStory"/> (one
        /// <c>StoryEvent.Event</c> spoken by <paramref name="npcKey"/>, one page per popup, text rendered VERBATIM,
        /// no '{' or '}'); only the target event list differs.
        /// </summary>
        /// <param name="npcKey">The UserNPC folder key (== <c>npcs/&lt;npcKey&gt;/</c>); non-empty.</param>
        /// <param name="pages">One or more popup pages, each shown verbatim; at least one, none containing '{' or '}'.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">If <paramref name="npcKey"/> is empty, no pages are supplied, or any
        /// page is null/contains '{' or '}'.</exception>
        public QuestBuilder WithCompleteStory(string npcKey, params string[] pages)
        {
            AppendStory("m_CompleteEvents", npcKey, pages);
            return this;
        }

        /// <summary>
        /// Append one UserNPC-spoken <c>StoryEvent.Event</c> (one <c>StoryEvent.Dialogue</c> page per
        /// <paramref name="pages"/> entry) to the named event list on the quest JObject, creating the list if
        /// absent. Both nested types carry their Newtonsoft <c>$type</c> (the '+' nested-type token) so the gamedef
        /// round-trip under <c>TypeNameHandling.Auto</c> reconstructs them exactly. Validates verbatim-text rules
        /// (mirrors the builder's <see cref="ArgumentException"/> style).
        /// </summary>
        private void AppendStory(string eventListField, string npcKey, string[] pages)
        {
            if (string.IsNullOrEmpty(npcKey))
                throw new ArgumentException(
                    "QuestBuilder." + eventListField + ": npcKey must be non-empty (quest '" + _storyQuestId + "').", "npcKey");
            if (pages == null || pages.Length == 0)
                throw new ArgumentException(
                    "QuestBuilder." + eventListField + ": at least one page is required (quest '" + _storyQuestId + "').", "pages");
            if (_quest == null)
                throw new InvalidOperationException(
                    "QuestBuilder." + eventListField + ": quest JObject is null (quest '" + _storyQuestId + "'); cannot author narrative.");

            JArray dialogues = new JArray();
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                if (page == null)
                    throw new ArgumentException(
                        "QuestBuilder." + eventListField + ": page[" + i + "] is null (quest '" + _storyQuestId + "').", "pages");
                // The text is run through string.Format at render; '{'/'}' would throw a FormatException in-game.
                // Reject at authoring time so the author notices (matching the builder's fail-fast style).
                if (page.IndexOf('{') >= 0 || page.IndexOf('}') >= 0)
                    throw new ArgumentException(
                        "QuestBuilder." + eventListField + ": page[" + i + "] contains '{' or '}', which break the " +
                        "in-game string.Format render (quest '" + _storyQuestId + "').", "pages");

                JObject dialogue = new JObject();
                dialogue["$type"] = "StoryEvent+Dialogue, Assembly-CSharp";
                dialogue["m_Text"] = page; // rendered VERBATIM (Localized<TextStory> -> GetUserModText pass-through)
                dialogues.Add(dialogue);
            }

            JObject ev = new JObject();
            ev["$type"] = "StoryEvent+Event, Assembly-CSharp";
            ev["m_Talker"] = "None";       // UserNPC supplies the portrait/name/title; no talking-head needed
            ev["m_UserNPC"] = npcKey;      // == npcs/<npcKey>/ folder scanned at gamedef init
            ev["m_Dialogues"] = dialogues;

            JArray list = _quest[eventListField] as JArray;
            if (list == null)
            {
                list = new JArray();
                _quest[eventListField] = list;
            }
            list.Add(ev);
        }
    }
}
