namespace FTKModFramework.Core
{
    /// <summary>
    /// A handle to a quest just appended to a campaign stage, returned for chaining. In this slice it only
    /// carries the quest's <c>m_StoryQuestID</c> (the globally-unique key the game uses to build its runtime
    /// quest chain). Later phases of the Quest &amp; Campaign Engine add chaining verbs here (e.g. BranchTo,
    /// OnCompleteSetFlag); the type exists now so those can be added without changing the Add*Quest signatures.
    ///
    /// Returned by the <see cref="StageBuilder"/> Add*Quest methods and never constructed directly. It exposes
    /// no engine internals.
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
    }
}
