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
        private readonly JArray _quests;   // the stage's m_Quests (the live array)
        private readonly int _stageIndex;  // this stage's positional index in GameDefinition.m_Stages

        /// <summary>
        /// Internal: built by <see cref="CampaignBuilder.AddStage"/> over the stage's m_Quests array.
        /// <paramref name="stageIndex"/> is the stage's position in <c>GameDefinition.m_Stages</c>; the game
        /// keys its per-stage realm/hex allocation buckets by that positional index
        /// (<c>FTKHex.m_HexLandInRealmStage[stageIndex][realm]</c>, <c>m_StoryQuestRealmStageQueue[stageIndex]</c>,
        /// see <c>GameDefinition.cs</c> stage loops), so every quest this builder emits must carry
        /// <c>m_SpecifiedRealmStageIndex = stageIndex</c> for its Specified-realm destination to resolve against
        /// the correct stage bucket (verified: <c>QuestLogicBase.DetermineDestinations</c> copies
        /// <c>m_SpecifiedRealmStageIndex</c> straight into <c>m_DestStageIndex</c>).
        /// </summary>
        internal StageBuilder(JArray quests, int stageIndex)
        {
            _quests = quests;
            _stageIndex = stageIndex;
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
            // None => a plain Bounty. BountyQuestDef.GetQuestType() returns BountySiege for ANY non-None
            // m_DestinationType, and a BountySiege needs siege scaffolding the framework does not author, so it
            // never commences (the in-game soft-lock); additionally BountyQuestLogic's RandomRealmPoi branch
            // does an unguarded list[Random.Range(0,0)] on an empty static-POI list. None keeps it a Bounty and
            // uses BountyQuestLogic's robust spawn-queue/clear-hex destination fallback. (Vanilla bounties: None.)
            q["m_DestinationType"] = "None";
            q["m_EnemySet"] = RequireValue(enemySet, "enemySet"); // priority target field (over m_Enemies)
            _quests.Add(q);
            return new QuestBuilder(storyQuestId, q); // q is the live quest JObject, for narrative authoring
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
            // RealmCapital => VisitQuestLogic resolves the destination to the realm's capital town (always present
            // in the cloned realm scaffolding). RandomRealmPoi instead does an unguarded list[Random.Range(0,0)]
            // on an empty static-POI list and never places the destination (the soft-lock); no vanilla Visit uses
            // it. The quest TYPE stays Visit because VisitQuestDef.GetQuestType() keys off m_DeliveryInstructions
            // (None => Visit), not m_DestinationType.
            q["m_DestinationType"] = "RealmCapital";
            q["m_DeliveryInstructions"] = "None";      // None => plain Visit (no deliver/fetch)
            _quests.Add(q);
            return new QuestBuilder(storyQuestId, q); // q is the live quest JObject, for narrative authoring
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
            return new QuestBuilder(storyQuestId, q); // q is the live quest JObject, for narrative authoring
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
            // No m_DestinationType: MiniEncounterQuestDef does not declare that field (only BountyQuestDef and
            // VisitQuestDef do), and MiniEncounterQuestLogic ignores destination type entirely, resolving the
            // destination via the spawn-queue/clear-hex fallback and spawning the encounter hex at m_MiniEncounterID.
            // The previously-authored "RealmMiniEncounter" was a phantom property (silently dropped by the gamedef
            // deserializer's default MissingMemberHandling) that masked the fact that the verb needs only the id.
            q["m_MiniEncounterID"] = RequireValue(miniEncounterId, "miniEncounterId");
            _quests.Add(q);
            return new QuestBuilder(storyQuestId, q); // q is the live quest JObject, for narrative authoring
        }

        /// <summary>
        /// Append a COLLECT-N objective: a <see cref="ModQuestDef"/> carrying the framework collect-N verb key,
        /// the target item, and the required party-wide count. Unlike the reuse verbs above (which emit native
        /// <c>Assembly-CSharp</c> quest defs), this emits a FRAMEWORK quest def: its <c>$type</c> names THIS
        /// assembly (<c>FTKModFramework</c>), and at runtime <see cref="QuestVerbResolverPatch"/> resolves
        /// <c>m_BehaviorKey</c> to <see cref="CollectNQuestLogic"/>, which completes the quest when the party's
        /// combined Backpack holds at least <paramref name="count"/> of <paramref name="item"/>.
        ///
        /// The destination scaffolding (<c>m_DestinationRealmType = Specified</c> + <c>m_SpecifiedRealm</c>) is
        /// authored just like the reuse verbs so the quest is map-gen valid; collect-N is satisfied by inventory
        /// regardless of where the destination lands.
        /// </summary>
        /// <param name="storyQuestId">Globally-unique quest id (must be non-empty and unique across the whole campaign).</param>
        /// <param name="item">The <c>FTK_itembase.ID</c> the verb counts across the party Backpack.</param>
        /// <param name="count">Required party-wide count; MUST be >= 1.</param>
        /// <param name="specifiedRealm">Destination realm (an <c>FTK_realm.ID</c> name present in the stage's m_RealmStages).</param>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="count"/> is less than 1.</exception>
        public QuestBuilder AddCollectQuest(string storyQuestId, GridEditor.FTK_itembase.ID item, int count, string specifiedRealm)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException("count",
                    "StageBuilder.AddCollectQuest: count must be >= 1 (got " + count + ").");

            // Framework $type: names FTKModFramework (resolved by Newtonsoft's default binder via
            // LoadWithPartialName, which finds the already-loaded BepInEx plugin), not Assembly-CSharp.
            JObject q = NewQuestWithType("FTKModFramework.Core.ModQuestDef, FTKModFramework", storyQuestId, specifiedRealm);
            q["m_BehaviorKey"] = FrameworkBehaviors.CollectNVerbKey; // resolver -> CollectNQuestLogic
            q["m_ItemId"] = item.ToString();                         // StringEnumConverter on the round-trip
            q["m_Count"] = count;
            _quests.Add(q);
            return new QuestBuilder(storyQuestId, q); // q is the live quest JObject, for narrative authoring
        }

        /// <summary>
        /// Build the shared <c>SingleQuestDefBase</c> skeleton common to every reuse verb, with the <c>$type</c>
        /// formed from a short native type NAME (<c>"&lt;typeName&gt;, Assembly-CSharp"</c>). Thin wrapper over
        /// <see cref="NewQuestWithType"/> for the vanilla verbs; see it for the field rationale.
        /// </summary>
        private JObject NewSingleQuest(string typeName, string storyQuestId, string specifiedRealm)
        {
            return NewQuestWithType(typeName + ", Assembly-CSharp", storyQuestId, specifiedRealm);
        }

        /// <summary>
        /// Build the shared quest skeleton from a FULL Newtonsoft <c>$type</c> token (so a framework-assembly
        /// def like <see cref="ModQuestDef"/> can name its own assembly): the required <c>$type</c> discriminator,
        /// the unique <c>m_StoryQuestID</c>, and a resolvable destination (<c>m_DestinationRealmType = Specified</c>
        /// + <c>m_SpecifiedRealm</c>). <c>m_OnelinerOverride</c> is intentionally left empty so the built-in
        /// vanilla one-liner (STR_*_OneLine) renders the objective text; setting a custom override with no
        /// TextQuest row would render the literal "TextQuest".
        /// </summary>
        private JObject NewQuestWithType(string fullType, string storyQuestId, string specifiedRealm)
        {
            if (string.IsNullOrEmpty(storyQuestId))
                throw new ArgumentException("StageBuilder: storyQuestId must be non-empty.", "storyQuestId");
            if (string.IsNullOrEmpty(specifiedRealm))
                throw new ArgumentException("StageBuilder: specifiedRealm must be non-empty.", "specifiedRealm");

            JObject q = new JObject();
            q["$type"] = fullType;                          // full token; matches GameDefJSONMapper's TypeNameHandling.Auto
            q["m_StoryQuestID"] = storyQuestId;             // unique m_QuestLookup key; empty would break HashID
            q["m_DestinationRealmType"] = "Specified";      // resolve the destination in m_SpecifiedRealm
            q["m_SpecifiedRealm"] = specifiedRealm;         // must be an FTK_realm.ID present in the stage's m_RealmStages
            q["m_SpecifiedRealmStageIndex"] = _stageIndex;  // MUST equal this quest's stage index: DetermineDestinations
                                                            // copies it into m_DestStageIndex, the key into the game's
                                                            // per-stage hex/realm allocation (FTKHex.m_HexLandInRealmStage
                                                            // [stageIndex][realm]); leaving it at the default 0 resolved
                                                            // every stage's Specified realm against stage 0's bucket.
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
