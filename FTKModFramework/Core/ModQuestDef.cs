using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// A framework-authored story-quest DEFINITION that carries a CUSTOM objective verb. It is the data half of
    /// the custom-verb seam (#40): the campaign builder emits one of these as a JSON element of a stage's
    /// <c>m_Quests</c> (carrying a Newtonsoft <c>$type</c> that names THIS assembly), and the runtime resolver
    /// (<see cref="QuestVerbResolverPatch"/>) reads <see cref="m_BehaviorKey"/> off it to substitute the matching
    /// custom <c>QuestLogicBase</c> when the story FSM starts the quest.
    ///
    /// It subclasses the vanilla <c>SingleQuestDefBase</c> (which is a <c>QuestDefBase</c>) so it inherits the
    /// destination scaffolding (<c>m_DestinationRealmType</c> + <c>m_SpecifiedRealm</c>) every reuse verb in
    /// <see cref="StageBuilder"/> already authors, plus the two abstract overrides
    /// <c>InitializeAsQuestRunTime</c>/<c>AddToMultiQuestRunTime</c>. Only the six remaining
    /// <c>QuestDefBase</c> abstract members are implemented here (verified against the decompile:
    /// <c>QuestDefBase</c> declares exactly eight abstract members; <c>SingleQuestDefBase</c> supplies two).
    ///
    /// PUBLIC (not internal) because Newtonsoft instantiates it from the <c>$type</c> discriminator via the
    /// default binder, which requires a public, public-default-ctor type. Its authored fields
    /// (<see cref="m_BehaviorKey"/>/<see cref="m_ItemId"/>/<see cref="m_Count"/>) carry NO <c>[JsonIgnore]</c> so
    /// they serialize into the <c>.ftk2</c> and round-trip back. No <c>internal</c> Core type is exposed in any
    /// of its public members.
    ///
    /// <see cref="GetQuestType"/> returns the benign vanilla <c>Visit</c> quest-type: it is the ONLY enum value
    /// the resolver's switch consults for a ModQuestDef, and it maps cleanly to <c>VisitQuestLogic</c> in the
    /// game's <c>_getQuestLogicSystemTypeFromQuestType</c>. So if a verb key is dangling (unregistered), the
    /// resolver Prefix falls through to the unpatched game path and gets a SANE vanilla QuestLogic instead of a
    /// null Type that would throw in the original's (try/catch-less) Activator call.
    /// </summary>
    public class ModQuestDef : SingleQuestDefBase
    {
        /// <summary>
        /// The custom-verb behaviour key (<c>modGuid + ":" + verbName</c>, matching
        /// <c>BehaviorRegistry.MakeKey</c>). The resolver looks this up in <see cref="BehaviorRegistry"/>; a hit
        /// of kind <c>QuestLogic</c> selects the custom <c>QuestLogicBase</c> to instantiate. A plain serialized
        /// string so it round-trips through the gamedef JSON.
        /// </summary>
        public string m_BehaviorKey = "";

        /// <summary>The item the collect-N verb counts across the party Backpack. Authored; round-trips as an enum.</summary>
        public FTK_itembase.ID m_ItemId = FTK_itembase.ID.None;

        /// <summary>The required party-wide count for the collect-N verb. Authored; round-trips as an int.</summary>
        public int m_Count;

        // ---- QuestDefBase abstract members not already supplied by SingleQuestDefBase --------------------
        // Mirrors the no-op / "no enemies, no encounters" shape of vanilla VisitQuestDef (decompile-confirmed):
        // a collect-N quest has no map-info to compute, no enemy set, and no encounter rooms. The destination
        // is resolved by the inherited SingleQuestDefBase scaffolding (m_DestinationRealmType/m_SpecifiedRealm).

        public override void ComputeMapInfo()
        {
        }

        /// <summary>
        /// The vanilla quest-type this def reports. See the class remarks: <c>Visit</c> is deliberate so a
        /// dangling verb key falls through to a real vanilla QuestLogic rather than a null-Type throw.
        /// </summary>
        public override QuestLogicBase.Type GetQuestType()
        {
            return QuestLogicBase.Type.Visit;
        }

        public override MiniHexDungeon.RoomInfo GetEncounter(int pIndex, int pDifficulty = 0)
        {
            return null;
        }

        public override int GetEnemyEncounterCount()
        {
            return 0;
        }

        public override FTK_enemySet.ID GetEnemySetID()
        {
            return FTK_enemySet.ID.None;
        }

        public override void OnCompleteQuestDefCustom(QuestLogicBase pLogic, HexLand pDestination)
        {
        }
    }
}
