using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The runtime objective logic for the collect-N custom verb (#40, FR-4): a quest that completes when the
    /// PARTY holds at least N of a given item across their Backpacks. It is the QuestLogic half of the seam;
    /// the data half is <see cref="ModQuestDef"/>, which carries the authored item/count.
    ///
    /// <see cref="QuestVerbResolverPatch"/> instantiates this via <c>Activator.CreateInstance</c> through the
    /// 5-arg ctor below (the exact game <c>QuestLogicBase</c> ctor at QuestLogicBase.cs:283), which the resolver
    /// calls in place of the game's own <c>Activator.CreateInstance(type, ...)</c>. The base ctor stores the def
    /// in the public <c>m_QuestDef</c> field, so <see cref="IsCompleteState"/> reads the authored item/count back
    /// off it.
    ///
    /// PUBLIC because the resolver builds it reflectively (Activator) and because it rides the save/RPC path:
    /// <c>GameStatesSerialize</c> persists the live <c>QuestLogicBase[]</c> and (Newtonsoft, RPC side) emits a
    /// <c>$type</c> for the concrete subclass. This type adds NO new fields of its own (item/count live on the
    /// def, which is itself serialized), so it round-trips cleanly under BOTH FullSerializer (disk) and
    /// Newtonsoft <c>TypeNameHandling.Auto</c> (RPC). The full dual-serializer round-trip gate is #41's scope;
    /// keeping this type field-free is what keeps that future gate green.
    /// </summary>
    public class CollectNQuestLogic : QuestLogicBase
    {
        /// <summary>
        /// The 5-arg ctor the resolver (and the game's own resolver path) uses. It forwards verbatim to the
        /// vanilla <c>QuestLogicBase</c> 5-arg ctor, which records <c>m_QuestDef</c>, the start hex, the master
        /// quest id, and resolves the destination from the def's <c>SingleQuestDefBase</c> scaffolding.
        /// </summary>
        public CollectNQuestLogic(
            QuestDefBase _questDef, HexLandID _start, bool pIsCurrent, int _masterQuestID, List<HexLand> pDestHexes)
            : base(_questDef, _start, pIsCurrent, _masterQuestID, pDestHexes)
        {
        }

        /// <summary>
        /// Complete when the party's combined Backpack holding of the authored item is at least the authored
        /// count. Evaluated HOST-ONLY on movement-stop / encounter-end (the same cadence as a vanilla Fetch),
        /// driven by the game's <c>CheckIsComplete</c> -> <c>IsCompleteState(GetCurrentCOW())</c> chain; this
        /// override is sufficient (no patch on the trigger is needed).
        ///
        /// Sums every party member's Backpack via <c>FTKHub.Instance.m_CharacterOverworlds</c> (the vanilla
        /// Fetch logic only inspects the single current COW; collect-N is party-wide by design). The
        /// <paramref name="pCurrentCOW"/> argument is intentionally unused: the verb is a party total, not a
        /// per-character check.
        /// </summary>
        public override bool IsCompleteState(CharacterOverworld pCurrentCOW)
        {
            ModQuestDef def = m_QuestDef as ModQuestDef;
            if (def == null) return false; // not a collect-N quest's def; never complete (defensive, should not happen)

            FTK_itembase.ID itemId = def.m_ItemId;
            int needed = def.m_Count;

            int total = 0;
            FTKHub hub = FTKHub.Instance;
            if (hub != null && hub.m_CharacterOverworlds != null)
            {
                foreach (CharacterOverworld cow in hub.m_CharacterOverworlds)
                {
                    if (cow == null || cow.m_PlayerInventory == null) continue;
                    total += cow.m_PlayerInventory.GetItemCount(PlayerInventory.ContainerID.Backpack, itemId);
                }
            }

            return total >= needed;
        }
    }
}
