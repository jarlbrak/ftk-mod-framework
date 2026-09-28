using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Dungeon Find Herb fix (fix.dungeon-find-herb). GameFlowMC.EndTurn is a PunRPC sent to
    /// the master client. It rotates m_PlayerCurrentTurn and would start a new round at the wrap, but
    /// not while players are in a dungeon, so GameEventManager.UpdateTurnMC never clears
    /// GameFlow.m_FindHerbRoundCoolDown there. The prefix computes that suppressed wrap from vanilla's
    /// own inputs before any of them change, and the postfix clears the cooldown through vanilla's
    /// UpdateFindHerbRoundCoolDown, whose SyncMember reaches the other machines. The rotation covers
    /// every in-game player, so a clear also frees herbalists still in the overworld, at the rate of one
    /// herb per party per cycle that an overworld round allows.
    ///
    /// The postfix never reads m_PlayerCurrentTurn: EndTurn ends in BeginTurn, and a turn can end again
    /// inside it, so the state after the call need not be this call's. The prefix returns void and
    /// catches everything, so vanilla always runs.</summary>
    [HarmonyPatch(typeof(GameFlowMC), "EndTurn")]
    internal static class DungeonFindHerbPatch
    {
        private const string Via = "GameFlowMC.EndTurn";

        private static void Prefix(GameFlowMC __instance, bool _advanceRound, out bool __state)
        {
            __state = false;
            int handle = FrameworkTweaks.DungeonFindHerb;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                FTKHub hub = FTKHub.Instance;
                GameFlow flow = GameFlow.Instance;
                if (__instance == null || hub == null || flow == null) return;
                // Cheap exit first: outside a dungeon vanilla's own round runs and nothing is suppressed.
                if (!hub.AnyPlayersInDungeon()) return;
                // m_DungeonEnterCow is dereferenced without a Unity null check, as vanilla does.
                // The re-point inputs are read only when vanilla will re-point.
                bool justEntered = __instance.m_JustEnterDungeon;
                CharacterOverworld enterCow = justEntered ? flow.m_DungeonEnterCow : null;
                bool hasEnterCow = !ReferenceEquals(enterCow, null);
                var state = new DungeonEndTurnState
                {
                    PlayersInDungeon = true,
                    JustEnteredDungeon = justEntered,
                    HasDungeonEnterCharacter = hasEnterCow,
                    DungeonEnterTurnIndex = hasEnterCow ? enterCow.m_FTKPlayerID.TurnIndex : 0,
                    CharacterTurnIndices = justEntered ? CharacterTurnIndices(hub.m_CharacterOverworlds) : null,
                    CurrentTurnIndex = __instance.m_PlayerCurrentTurn.TurnIndex,
                    IngameTurnIndices = IngameTurnIndices(__instance.m_IngamePlayerIDs),
                    StartTurnIndex = flow.m_StartTurnIndex,
                    AdvanceRound = _advanceRound,
                };
                __state = FrameworkTweaks.DungeonHerbCycleEnds(Tweaks.Registry, handle, state);
            }
            catch (Exception e)
            {
                __state = false;
                Tweaks.Fault(handle, e);
            }
        }

        private static void Postfix(bool __runOriginal, bool __state)
        {
            if (!__state) return;
            int handle = FrameworkTweaks.DungeonFindHerb;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                GameFlow flow = GameFlow.Instance;
                if (flow == null) return;
                if (!FrameworkTweaks.DungeonHerbClear(Tweaks.Registry, handle, __runOriginal, __state,
                        PhotonNetwork.isMasterClient, flow.m_FindHerbRoundCoolDown))
                    return;
                flow.UpdateFindHerbRoundCoolDown(false);
                Plugin.Log.LogInfo("Tweaks: fix.dungeon-find-herb cleared the Find Herb cooldown at the end of a dungeon turn cycle.");
                Tweaks.Session.Trace("herb-clear", Via);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        private static int[] CharacterTurnIndices(List<CharacterOverworld> characters)
        {
            if (characters == null) return null;
            var indices = new int[characters.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = characters[i].m_FTKPlayerID.TurnIndex;
            return indices;
        }

        private static int[] IngameTurnIndices(List<FTKPlayerID> players)
        {
            if (players == null) return null;
            var indices = new int[players.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = players[i].TurnIndex;
            return indices;
        }
    }
}
