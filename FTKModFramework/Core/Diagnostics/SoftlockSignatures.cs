using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>The known softlock paths that spec #265 FR-2 records as log signatures.</summary>
    internal enum SoftlockKind
    {
        /// <summary>MiniHexInfo.DeserializeFinalRPC threw during a load.</summary>
        LoadException,
        /// <summary>A loot vote started with no valid voter.</summary>
        LootVote,
        /// <summary>One discovery loop found the same hidden POI again and again.</summary>
        PoiRediscovery,
        /// <summary>A character's m_EnteredDungeon is set with no matching dungeon.</summary>
        StaleDungeon,
        /// <summary>A character's m_IsInCombat is set while no encounter is running.</summary>
        CombatWithoutEncounter,
    }

    /// <summary>Unity-free detection predicates and line shape for the softlock signatures. The
    /// patches in <c>Core/Diagnostics/SoftlockSignaturePatches.cs</c> read the game and call these;
    /// Tests/StuckTurn drives them without the game. Nothing here changes game state.</summary>
    internal static class SoftlockSignature
    {
        internal const string Prefix = "SOFTLOCK-SIGNATURE";

        /// <summary>The stack field's own cap. The whole line stays under StuckTurnLine.MaxLength.</summary>
        internal const int MaxStackLength = 1200;

        internal static string KindName(SoftlockKind kind)
        {
            switch (kind)
            {
                case SoftlockKind.LoadException: return "load-exception";
                case SoftlockKind.LootVote: return "loot-vote";
                case SoftlockKind.PoiRediscovery: return "poi-rediscovery";
                case SoftlockKind.StaleDungeon: return "stale-dungeon";
                case SoftlockKind.CombatWithoutEncounter: return "combat-without-encounter";
                default: return "unknown";
            }
        }

        /// <summary>A new line that starts <c>SOFTLOCK-SIGNATURE kind=...</c>.</summary>
        internal static StuckTurnLine Line(SoftlockKind kind)
        {
            return new StuckTurnLine(Prefix).Add("kind", KindName(kind));
        }

        /// <summary>The load exception line. The finalizer runs before the exception leaves the
        /// method, so its StackTrace still holds the frames inside DeserializeFinalRPC; they are kept
        /// here with line breaks turned into <c>|</c>. Null when there is no exception.</summary>
        internal static string LoadException(Exception exception, string poiType, int bigIndex, int smallIndex,
            bool hasHex, int carryingPlayers, int questMarkers)
        {
            if (exception == null) return null;
            StuckTurnLine line = Line(SoftlockKind.LoadException)
                .Add("poi", poiType)
                .Add("hex", hasHex ? Hex(bigIndex, smallIndex) : null)
                .Add("carrying", carryingPlayers)
                .Add("questMarkers", questMarkers)
                .Add("exception", exception.GetType().FullName)
                .Add("message", exception.Message);
            line.Add("stack", Flatten(exception.StackTrace), MaxStackLength);
            return line.ToString();
        }

        internal static string Hex(int bigIndex, int smallIndex)
        {
            return bigIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                + smallIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\r\n", "|").Replace('\n', '|').Replace('\r', '|');
        }

        /// <summary>Why a loot vote has no one to vote, or null when it is fine. The loot vote is
        /// EncounterSessionMC.m_VoteType == Loot after UpdateVoteQueue, which rebuilds m_VoteQueue from
        /// m_WinningPlayerID over m_AllCombtatantsAlive and sets the private m_VotingPlayerID to
        /// m_VoteQueue[0], or to FTKPlayerID.Null when the queue is empty.</summary>
        /// <param name="lootVote">m_VoteType is Loot.</param>
        /// <param name="queueCount">m_VoteQueue.Count.</param>
        /// <param name="firstVoterNull">m_VoteQueue[0] is FTKPlayerID.Null.</param>
        /// <param name="firstVoterFound">FTKHub.GetCharacterOverworldByFID(m_VoteQueue[0]) found a character.</param>
        internal static string LootVoteFault(bool lootVote, int queueCount, bool firstVoterNull, bool firstVoterFound)
        {
            if (!lootVote) return null;
            if (queueCount <= 0) return "empty-queue";
            if (firstVoterNull) return "null-voter";
            if (!firstVoterFound) return "unknown-voter";
            return null;
        }

        /// <summary>Why a character's dungeon flag is stale, or null when it is consistent.
        /// MiniHexInfo.EnterPOI sets GameFlow.m_DungeonEntered and each entrant's m_EnteredDungeon on
        /// the dungeon hex; MiniHexDungeon.SetAllEnteredDungeon(false) clears only the characters in
        /// that hex's m_PlayersInHex, and GameFlow.m_DungeonEntered is never set back to null.</summary>
        /// <param name="enteredDungeon">CharacterOverworld.m_EnteredDungeon (IsInDungeon()).</param>
        /// <param name="dungeonAlive">GameFlow.m_DungeonEntered is a live object.</param>
        /// <param name="onDungeonHex">The character's m_HexLand is that dungeon's hex.</param>
        internal static string StaleDungeonFault(bool enteredDungeon, bool dungeonAlive, bool onDungeonHex)
        {
            if (!enteredDungeon) return null;
            if (!dungeonAlive) return "no-dungeon";
            if (!onDungeonHex) return "off-hex";
            return null;
        }

        /// <summary>True when a character is flagged in combat while no encounter is running.
        /// CharacterOverworld.SetInCombat is sent true at the start of every non-overworld encounter
        /// and false by EncounterSessionMC.ReturnToOverworld for m_AllCombtatants and by
        /// CombatPlayerFlee for a fleeing character.</summary>
        /// <param name="characterInCombat">CharacterStats.m_IsInCombat.</param>
        /// <param name="sessionInCombat">EncounterSession.m_IsInCombat.</param>
        /// <param name="masterInCombat">EncounterSessionMC.m_IsInCombat.</param>
        /// <param name="encounterStarted">EncounterSessionMC.m_EncounterStarted.</param>
        internal static bool CombatWithoutEncounter(bool characterInCombat, bool sessionInCombat, bool masterInCombat,
            bool encounterStarted)
        {
            return characterInCombat && !sessionInCombat && !masterInCombat && !encounterStarted;
        }
    }

    /// <summary>At most <see cref="MaxPerKind"/> lines of each kind per launch.</summary>
    internal sealed class SoftlockBudget
    {
        internal const int MaxPerKind = 10;

        private readonly int[] _written = new int[Enum.GetValues(typeof(SoftlockKind)).Length];

        internal int Written(SoftlockKind kind) { return _written[(int)kind]; }

        internal bool TryTake(SoftlockKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _written.Length || _written[index] >= MaxPerKind) return false;
            _written[index]++;
            return true;
        }
    }

    /// <summary>One line per occurrence of a condition that persists across checks: it reports when
    /// the condition becomes true for a key and re-arms only after the key is seen false again.</summary>
    internal sealed class SoftlockLatch
    {
        private readonly List<long> _raised = new List<long>();

        internal bool Raised(long key) { return _raised.Contains(key); }

        internal bool Observe(long key, bool condition)
        {
            int index = _raised.IndexOf(key);
            if (!condition)
            {
                if (index >= 0) _raised.RemoveAt(index);
                return false;
            }
            if (index >= 0) return false;
            _raised.Add(key);
            return true;
        }
    }

    /// <summary>Counts hidden POI discoveries by hex within one discovery loop. Movement.CheckFindPOI_CR
    /// (and CharacterOverworld.CheckFindPOI_CR after a walk) asks FTKHex.GetHiddenPoiInRange for the
    /// first hidden POI in range, shows it through a POIDiscover coordinated message whose continuation
    /// runs CheckFindPOI again, and stops when nothing hidden is left. A loop therefore ends at the
    /// first empty search. The same hex found <see cref="RepeatThreshold"/> times in one loop reports
    /// once; each discovery also grants XP, so the loop is left alone.</summary>
    internal sealed class PoiDiscoveryWatch
    {
        internal const int RepeatThreshold = 3;

        private readonly Dictionary<long, int> _counts = new Dictionary<long, int>();

        internal int Tracked { get { return _counts.Count; } }

        /// <summary>Returns the count when this discovery reaches the threshold, otherwise 0.</summary>
        internal int Observe(bool found, int bigIndex, int smallIndex)
        {
            if (!found)
            {
                _counts.Clear();
                return 0;
            }
            long key = ((long)bigIndex << 32) | (uint)smallIndex;
            int count;
            _counts.TryGetValue(key, out count);
            count++;
            _counts[key] = count;
            return count == RepeatThreshold ? count : 0;
        }
    }
}
