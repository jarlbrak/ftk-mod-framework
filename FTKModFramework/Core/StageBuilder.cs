using System;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Authors the quests of a single campaign stage (the stage's <c>m_Quests</c> list), as DATA. Exposes one
    /// builder per reuse VERB: kill (<see cref="AddKillQuest"/> → <c>BountyQuestDef</c>), reach/clear
    /// (<see cref="AddVisitQuest"/> → <c>VisitQuestDef</c>, <see cref="AddClearDungeonQuest"/> →
    /// <c>DungeonQuestDef</c>), and encounter (<see cref="AddEncounterQuest"/> → <c>MiniEncounterQuestDef</c>).
    ///
    /// Each added quest is a native <c>QuestDefBase</c> JSON object carrying the correct Newtonsoft
    /// <c>$type</c> discriminator (short "&lt;TypeName&gt;, Assembly-CSharp" form — the game (de)serializes the
    /// gamedef with <c>TypeNameHandling.Auto</c>, and <c>m_Quests</c> is the abstract <c>List&lt;QuestDefBase&gt;</c>
    /// so each element REQUIRES a <c>$type</c>). No <c>[JsonIgnore]</c> runtime field is ever authored. Quests
    /// play in the order they are added; the destination is made resolvable via
    /// <c>m_DestinationRealmType</c> + <c>m_SpecifiedRealm</c>.
    ///
    /// A <see cref="StageBuilder"/> is returned by <see cref="CampaignBuilder.AddStage"/> and never constructed
    /// directly. It operates purely on a Newtonsoft <c>JArray</c> (no engine internals leak through its surface).
    /// </summary>
    public sealed class StageBuilder
    {
        private readonly JArray _quests; // the stage's m_Quests (the live array)

        /// <summary>Internal: built by <see cref="CampaignBuilder.AddStage"/> over the stage's m_Quests array.</summary>
        internal StageBuilder(JArray quests)
        {
            _quests = quests;
        }

        /// <summary>
        /// Append a KILL objective: a <c>BountyQuestDef</c> that completes when the named enemy set is
        /// defeated at a destination in <paramref name="specifiedRealm"/>.
        /// </summary>
        /// <param name="storyQuestId">Globally-unique quest id (the m_QuestLookup key; must be non-empty and unique across the whole campaign).</param>
        /// <param name="enemySet">Target <c>FTK_enemySet.ID</c> name (string enum value, e.g. "GoblinPack"); the bounty's target group.</param>
        /// <param name="specifiedRealm">Destination realm (an <c>FTK_realm.ID</c> name present in the stage's m_RealmStages, so the POI resolves).</param>
        public QuestBuilder AddKillQuest(string storyQuestId, string enemySet, string specifiedRealm)
        {
            JObject q = NewSingleQuest("BountyQuestDef", storyQuestId, specifiedRealm);
            q["m_DestinationType"] = "RandomRealmPoi";      // place the bounty at a random POI in the realm
            q["m_EnemySet"] = RequireValue(enemySet, "enemySet"); // priority target field (over m_Enemies)
            _quests.Add(q);
            return new QuestBuilder(storyQuestId);
        }

        /// <summary>
        /// Append a REACH objective: a <c>VisitQuestDef</c> that completes when the party visits a destination
        /// in <paramref name="specifiedRealm"/>. <c>m_DeliveryInstructions</c> is left at <c>None</c>, which
        /// the game treats as a plain Visit (no item delivery/fetch).
        /// </summary>
        /// <param name="storyQuestId">Globally-unique quest id (must be non-empty and unique across the whole campaign).</param>
        /// <param name="specifiedRealm">Destination realm (an <c>FTK_realm.ID</c> name present in the stage's m_RealmStages).</param>
        public QuestBuilder AddVisitQuest(string storyQuestId, string specifiedRealm)
        {
            JObject q = NewSingleQuest("VisitQuestDef", storyQuestId, specifiedRealm);
            q["m_DestinationType"] = "RandomRealmPoi"; // visit a random POI in the realm
            q["m_DeliveryInstructions"] = "None";      // None => plain Visit (no deliver/fetch)
            _quests.Add(q);
            return new QuestBuilder(storyQuestId);
        }

        /// <summary>
        /// Append a CLEAR objective: a <c>DungeonQuestDef</c> that completes when the named dungeon is cleared.
        /// This is the dungeon-clear sibling of <see cref="AddVisitQuest"/> (both are reach/clear verbs).
        /// </summary>
        /// <param name="storyQuestId">Globally-unique quest id (must be non-empty and unique across the whole campaign).</param>
        /// <param name="dungeonId">Target <c>FTK_dungeonEncounter.ID</c> name (string enum value) to clear.</param>
        /// <param name="specifiedRealm">Realm the dungeon destination resolves in (an <c>FTK_realm.ID</c> name present in the stage's m_RealmStages).</param>
        public QuestBuilder AddClearDungeonQuest(string storyQuestId, string dungeonId, string specifiedRealm)
        {
            JObject q = NewSingleQuest("DungeonQuestDef", storyQuestId, specifiedRealm);
            q["m_DungeonID"] = RequireValue(dungeonId, "dungeonId"); // the only target field DungeonQuestDef adds
            _quests.Add(q);
            return new QuestBuilder(storyQuestId);
        }

        /// <summary>
        /// Append an ENCOUNTER objective: a <c>MiniEncounterQuestDef</c> that completes when the named mini
        /// encounter is resolved at a destination in <paramref name="specifiedRealm"/>.
        /// </summary>
        /// <param name="storyQuestId">Globally-unique quest id (must be non-empty and unique across the whole campaign).</param>
        /// <param name="miniEncounterId">Target <c>FTK_miniEncounter.ID</c> name (string enum value) to resolve.</param>
        /// <param name="specifiedRealm">Destination realm (an <c>FTK_realm.ID</c> name present in the stage's m_RealmStages).</param>
        public QuestBuilder AddEncounterQuest(string storyQuestId, string miniEncounterId, string specifiedRealm)
        {
            JObject q = NewSingleQuest("MiniEncounterQuestDef", storyQuestId, specifiedRealm);
            q["m_DestinationType"] = "RealmMiniEncounter";                    // destination is the mini encounter POI
            q["m_MiniEncounterID"] = RequireValue(miniEncounterId, "miniEncounterId");
            _quests.Add(q);
            return new QuestBuilder(storyQuestId);
        }

        /// <summary>
        /// Build the shared <c>SingleQuestDefBase</c> skeleton common to every reuse verb: the required
        /// <c>$type</c> discriminator, the unique <c>m_StoryQuestID</c>, and a resolvable destination
        /// (<c>m_DestinationRealmType = Specified</c> + <c>m_SpecifiedRealm</c>). <c>m_OnelinerOverride</c> is
        /// intentionally left empty so the built-in vanilla one-liner (STR_*_OneLine) renders the objective
        /// text; setting a custom override with no TextQuest row would render the literal "TextQuest".
        /// </summary>
        private static JObject NewSingleQuest(string typeName, string storyQuestId, string specifiedRealm)
        {
            if (string.IsNullOrEmpty(storyQuestId))
                throw new ArgumentException("StageBuilder: storyQuestId must be non-empty.", "storyQuestId");
            if (string.IsNullOrEmpty(specifiedRealm))
                throw new ArgumentException("StageBuilder: specifiedRealm must be non-empty.", "specifiedRealm");

            JObject q = new JObject();
            q["$type"] = typeName + ", Assembly-CSharp"; // short form, matches GameDefJSONMapper's TypeNameHandling.Auto
            q["m_StoryQuestID"] = storyQuestId;           // unique m_QuestLookup key; empty would break HashID
            q["m_DestinationRealmType"] = "Specified";    // resolve the destination in m_SpecifiedRealm
            q["m_SpecifiedRealm"] = specifiedRealm;       // must be an FTK_realm.ID present in the stage's m_RealmStages
            return q;
        }

        private static string RequireValue(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("StageBuilder: " + paramName + " must be non-empty.", paramName);
            return value;
        }
    }
}
