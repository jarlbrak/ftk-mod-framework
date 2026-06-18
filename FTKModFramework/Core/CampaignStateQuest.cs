using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The Core-owned campaign-flag store (#41, spec #37 P3): an invisible DUMMY quest that carries a single
    /// <see cref="m_Flags"/> dictionary of string-keyed integers. It is injected directly into
    /// <c>GameLogic._fullQuestTable</c> (via the public <c>GetQuestTable()</c> accessor) under a fixed SENTINEL
    /// key (<see cref="Campaign.SentinelKey"/>) and is NEVER advanced into the story chain.
    ///
    /// WHY A DUMMY QUEST (not a bespoke manager):
    /// <c>GameStatesSerialize.Serialize</c> (GameStatesSerialize.cs:28-37) walks the ENTIRE quest table into
    /// <c>m_QuestList</c>/<c>m_QuestIDs</c>, so this dummy (and its flags) is saved to disk for free. On load,
    /// <c>GameStatesSerialize.Deserialize</c> (GameStatesSerialize.cs:44-47) direct-assigns any element whose
    /// runtime type <c>IsSubclassOf(typeof(QuestLogicBase))</c> back into the table under its saved key, so the
    /// dummy is AUTO-REHYDRATED as a <see cref="CampaignStateQuest"/> with its flags intact. The same table is
    /// the co-op RPC sync surface, so flags ride the existing quest-table sync to clients with no extra wiring.
    ///
    /// WHY IT IS NEVER ADVANCED / INVISIBLE:
    /// it is injected ONLY into the quest table, never into a <c>GameStage</c>, so it is absent from
    /// <c>m_NextStoryQuestID</c>/<c>m_QuestLookup</c>. The advancement path (<c>GetNextQuest</c>) and the quest
    /// HUD therefore never touch it. The SENTINEL key (<see cref="Campaign.SentinelKey"/> = <c>int.MinValue</c>)
    /// sits outside both quest-id bands: story quests get NEGATIVE ids and generated quests POSITIVE ids off the
    /// <c>GameLogic.m_QuestID</c> counter (GameLogic.cs:579-582), so the sentinel can never collide with a real id.
    ///
    /// WHY THE CTOR SETS SAFE-DEFAULT STATE (the load-sweep gotcha):
    /// after a load, <c>GameLogic.StateDataDeserializeDone()</c> (GameLogic.cs:2353-2361) iterates EVERY
    /// <c>_fullQuestTable.Values</c> and calls three NON-VIRTUAL base methods we cannot override:
    /// <c>CorrectQuestPropertiesAtLoadGame()</c>, <c>PostDeserialized(true)</c>, <c>AssignDescription()</c>. The
    /// dummy must carry state that makes all three safe no-ops (verified against the decompile):
    /// <list type="bullet">
    /// <item><c>m_IsCompleted = true</c> -&gt; <c>IsRawComplete()</c> true (QuestLogicBase.cs:1166-1169), so
    /// <c>CorrectQuestPropertiesAtLoadGame</c> (QuestLogicBase.cs:432) skips its body (no GetGameDef deref).</item>
    /// <item><c>m_StoryQuestID = 0</c> -&gt; <c>HasQuestDefID()</c> false (QuestLogicBase.cs:557-560), so
    /// <c>PostDeserialized</c> skips all def work and <c>StateDataDeserializeDone</c> skips
    /// <c>QuestDefFillOutInfo</c> (gated on <c>HasQuestDefID()</c>, GameLogic.cs:2356).</item>
    /// <item><c>m_Type = Visit</c> (enum ordinal 0; <c>OpenEnded = -1</c> precedes it, QuestLogicBase.cs:11-30):
    /// NOT Bounty, so <c>PostDeserialized</c>'s Bounty branches are skipped, and <c>Visit</c> is a valid
    /// <c>OneLinerLookup</c> key (QuestLogicBase.cs:98-103) so <c>GetOneLineDesc()</c> does not throw
    /// KeyNotFound inside <c>AssignDescription</c>.</item>
    /// <item><c>m_OnelineID</c> defaults to 0 (<c>oneline0</c>), a real <c>FTK_questOneLine</c> row, so
    /// <c>FTK_questOneLineDB.Get(m_OnelineID)</c> in <c>AssignDescription</c> (QuestLogicBase.cs:787) resolves
    /// instead of returning null. Left at its default; documented here for the record.</item>
    /// <item><c>m_QuestDef = null</c>: never dereferenced on any of these three paths (the def-work blocks are
    /// all gated behind <c>HasQuestDefID()</c>, which is false here).</item>
    /// </list>
    /// The base <c>List&lt;int&gt; m_SubQuestIDs</c> / <c>List&lt;EncounterData&gt; m_Encounters</c> are non-null
    /// by their field initializers (QuestLogicBase.cs:242,246), so we leave them.
    ///
    /// NOT SEALED, and PUBLIC: FullSerializer (disk) emits the <c>$type</c> discriminator only when the runtime
    /// type differs from the storage type AND the type is not sealed (<c>fsBaseConverter.RequestInheritanceSupport</c>
    /// returns <c>!storageType.IsSealed</c>; <c>fsSerializer.InternalSerialize_2_Inheritance</c> writes <c>$type</c>
    /// only on a type mismatch). The save path stores elements as <c>QuestLogicBase</c>, so leaving this class
    /// unsealed is what makes the disk serializer record the concrete subtype and round-trip it back. Public is
    /// required for Activator / Newtonsoft / FullSerializer reflective construction. This type adds NO field
    /// other than <see cref="m_Flags"/> (NFR-1: flags are string-keyed ints, never routed through IdAllocator or
    /// any FTK_*.ID enum band), and no typed schema or namespaced sub-stores.
    /// </summary>
    public class CampaignStateQuest : QuestLogicBase
    {
        /// <summary>
        /// The campaign flag store: arbitrary string keys to int values. Serialized to disk by FullSerializer
        /// (a public field, no attribute needed under <c>fsConfig.DefaultMemberSerialization = Default</c>;
        /// <c>Dictionary&lt;string,int&gt;</c> is handled natively by <c>fsDictionaryConverter</c>) and over the
        /// co-op RPC path by Newtonsoft (<c>TypeNameHandling.Auto</c>), so flags round-trip on both surfaces.
        /// </summary>
        public Dictionary<string, int> m_Flags = new Dictionary<string, int>();

        /// <summary>
        /// Public parameterless ctor (matches the base <c>QuestLogicBase()</c> at QuestLogicBase.cs:274, which
        /// Newtonsoft / FullSerializer / Activator use to construct the instance before populating fields). Sets
        /// the safe-default load-sweep state documented on the class; see that doc for why each value matters.
        /// </summary>
        public CampaignStateQuest()
        {
            m_IsCompleted = true;            // IsRawComplete() true -> CorrectQuestPropertiesAtLoadGame is a no-op
            m_StoryQuestID = 0;              // HasQuestDefID() false -> PostDeserialized skips all def work
            m_Type = Type.Visit;             // not Bounty; a valid OneLinerLookup key (AssignDescription safe)
            m_OnelineID = FTK_questOneLine.ID.oneline0; // a real row -> FTK_questOneLineDB.Get does not NRE
            m_QuestDef = null;               // never dereferenced on the (HasQuestDefID-gated) load-sweep paths
        }
    }
}
