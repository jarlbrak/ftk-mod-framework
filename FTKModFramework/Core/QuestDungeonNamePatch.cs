using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Quest dungeon name tweak (fix.quest-dungeon-name, Spec #242 FR-1).
    ///
    /// QuestLogicBase.SetMessageParams writes param 8 twice. First, for a story quest with a
    /// destination, it uses the destination realm's m_MainDungeon; for a realm without one that is
    /// the missing key STR_DungeonNoneDisplay. Then it overwrites param 8 with the start hex realm's
    /// main dungeon when that realm has one and FTKHex.GetSpecificDungeon finds it. A dungeon quest
    /// whose Allocated realm has no dungeon of its own is sent to the dungeon in another realm, so its
    /// raw key survives when the quest started outside a main-dungeon realm.
    ///
    /// The postfix re-reads that game state and, only when the raw key survived and the destination is
    /// a dungeon, sets param 8 to that dungeon's own display value through the vanilla SetMessageParam,
    /// which applies the same rich-text wrapping as every other param. No string comparison is used.
    ///
    /// Ordering: the five overrides (Dungeon, Visit, Bounty, Arena, MiniEncounter quest logic) call
    /// base.SetMessageParams first, so this postfix runs inside them and none of them write param 8
    /// afterwards. GetMessageParams(bool) then runs SetMessageDynamicParams (params 12 to 14 only), and
    /// the realm-name postfix in Localization.cs runs last on the returned array. It replaces only
    /// registered "STR_&lt;int&gt;Display" realm keys, which a dungeon display value does not contain, so the
    /// two patches do not interact. GameLogic.SyncQuestDestinationRPC also calls SetMessageParams
    /// directly; this postfix runs there too.</summary>
    [HarmonyPatch(typeof(QuestLogicBase), "SetMessageParams")]
    internal static class QuestDungeonNamePatch
    {
        private const int DungeonParam = 8;

        private static void Postfix(QuestLogicBase __instance)
        {
            int handle = FrameworkTweaks.QuestDungeonName;
            if (!Tweaks.IsOn(handle)) return;
            string replacement;
            try
            {
                replacement = FrameworkTweaks.QuestDungeonNameReplacement(Tweaks.Registry, handle, Read(__instance));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            if (replacement == null) return;
            try
            {
                // Vanilla's own setter: wraps with the story-quest key-info colour, bold, and swallows its
                // own failures, leaving vanilla's value in place.
                __instance.SetMessageParam(DungeonParam, replacement, true);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        /// <summary>Reads the conditions in the order vanilla evaluates them and stops at the first one
        /// that fails; the flags left false keep vanilla.</summary>
        private static FrameworkTweaks.QuestDungeonNameState Read(QuestLogicBase quest)
        {
            var state = new FrameworkTweaks.QuestDungeonNameState();
            state.HasQuestDefId = quest.HasQuestDefID();
            state.DestinationSet = !HexLandID.IsNull(quest.m_Destination);
            if (!state.HasQuestDefId || !state.DestinationSet) return state;

            RealmProperties destination = GameLogic.Instance.GetGameDef().GetRealmProperties(quest.m_DestRealm, quest.m_DestStageIndex);
            state.DestinationRealmHasNoMainDungeon = destination != null && destination.m_MainDungeon == FTK_dungeonEncounter.ID.None;
            if (!state.DestinationRealmHasNoMainDungeon) return state;

            state.StartRealmDidNotOverwrite = !StartRealmOverwrote(quest);
            if (!state.StartRealmDidNotOverwrite) return state;

            HexLand hex = quest.m_Destination.GetHexLand();
            MiniHexDungeon dungeon = hex != null ? hex.GetPOI() as MiniHexDungeon : null;
            state.DestinationIsDungeon = dungeon != null;
            if (state.DestinationIsDungeon) state.DungeonDisplayValue = dungeon.GetPOIDisplayValue();
            return state;
        }

        /// <summary>Vanilla's second write to param 8: the start hex realm's main dungeon, when it is on
        /// the map. An unreadable start realm counts as an overwrite, so vanilla stands.</summary>
        private static bool StartRealmOverwrote(QuestLogicBase quest)
        {
            HexLand start = FTKHex.Instance.GetHexLand(quest.m_StartHex);
            RealmProperties properties = start != null ? start.GetRealmProperties() : null;
            if (properties == null) return true;
            if (properties.m_MainDungeon == FTK_dungeonEncounter.ID.None) return false;
            return FTKHex.Instance.GetSpecificDungeon(properties.m_MainDungeon, start.m_HexInfo.m_Realm, start.m_HexInfo.m_StageIndex) != null;
        }
    }
}
