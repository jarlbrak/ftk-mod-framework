namespace FTKModFramework.Core
{
    /// <summary>The GameFlowMC.EndTurn inputs the dungeon Find Herb decision reads, captured before
    /// vanilla changes any of them. Turn order is passed as TurnIndex values in list order, so the
    /// decision can repeat vanilla's lookups without game types.</summary>
    internal struct DungeonEndTurnState
    {
        /// <summary>FTKHub.AnyPlayersInDungeon(): some character has m_EnteredDungeon set. This is
        /// also the test BeginTurnFinished2 uses to skip GameLogic.UpdateTurn, so a cleared dungeon
        /// with players still inside counts.</summary>
        internal bool PlayersInDungeon { get; set; }
        /// <summary>GameFlowMC.m_JustEnterDungeon, set on the master when the party enters.</summary>
        internal bool JustEnteredDungeon { get; set; }
        /// <summary>GameFlow.m_DungeonEnterCow is not null.</summary>
        internal bool HasDungeonEnterCharacter { get; set; }
        /// <summary>GameFlow.m_DungeonEnterCow.m_FTKPlayerID.TurnIndex.</summary>
        internal int DungeonEnterTurnIndex { get; set; }
        /// <summary>FTKHub.m_CharacterOverworlds, each m_FTKPlayerID.TurnIndex, in list order.</summary>
        internal int[] CharacterTurnIndices { get; set; }
        /// <summary>GameFlowMC.m_PlayerCurrentTurn.TurnIndex before vanilla re-points it.</summary>
        internal int CurrentTurnIndex { get; set; }
        /// <summary>GameFlowMC.m_IngamePlayerIDs, each TurnIndex, in list order.</summary>
        internal int[] IngameTurnIndices { get; set; }
        /// <summary>GameFlow.m_StartTurnIndex, which vanilla compares with a list index.</summary>
        internal int StartTurnIndex { get; set; }
        /// <summary>EndTurn's _advanceRound argument, FTKHub.m_SkipRound as GameFlow.EndTurn sends it.</summary>
        internal bool AdvanceRound { get; set; }
    }

    /// <summary>The dungeon Find Herb fix's view of GameFlowMC.EndTurn. Vanilla computes a new round
    /// there as (next == m_StartTurnIndex || _advanceRound) and then suppresses it while players are
    /// in a dungeon, so GameEventManager.UpdateTurnMC never clears GameFlow.m_FindHerbRoundCoolDown.
    /// This repeats vanilla's index computation without changing anything, so the patch can clear the
    /// cooldown exactly where vanilla suppressed a round.</summary>
    internal static class DungeonHerbCycle
    {
        /// <summary>The m_IngamePlayerIDs index EndTurn hands the next turn to, or -1 where vanilla
        /// would throw instead (an empty list, or no characters to re-point through).</summary>
        internal static int NextListIndex(DungeonEndTurnState state)
        {
            int[] ingame = state.IngameTurnIndices;
            if (ingame == null || ingame.Length == 0) return -1;
            int current = state.CurrentTurnIndex;
            // Vanilla re-points the current turn only in a dungeon, and only on the first end turn after
            // entering: to the character whose TurnIndex is the entrant's minus one, modulo the character
            // count, keeping the current turn when none matches.
            if (state.PlayersInDungeon && state.JustEnteredDungeon)
            {
                int[] characters = state.CharacterTurnIndices;
                if (!state.HasDungeonEnterCharacter || characters == null || characters.Length == 0) return -1;
                int count = characters.Length;
                int previous = (state.DungeonEnterTurnIndex + count - 1) % count;
                for (int i = 0; i < characters.Length; i++)
                {
                    if (characters[i] == previous)
                    {
                        current = previous;
                        break;
                    }
                }
            }
            // GetTurnIndex: the first list entry with that TurnIndex; a missing one restarts from 0.
            int index = 0;
            for (int i = 0; i < ingame.Length; i++)
            {
                if (ingame[i] == current)
                {
                    index = i;
                    break;
                }
            }
            return (index + 1) % ingame.Length;
        }

        /// <summary>True where vanilla would have started a new round but suppressed it because
        /// players are in a dungeon: (next == m_StartTurnIndex || _advanceRound) and AnyPlayersInDungeon().</summary>
        internal static bool CycleEnds(DungeonEndTurnState state)
        {
            if (!state.PlayersInDungeon) return false;
            int next = NextListIndex(state);
            if (next < 0) return false;
            return next == state.StartTurnIndex || state.AdvanceRound;
        }
    }
}
