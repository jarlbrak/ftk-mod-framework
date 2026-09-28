using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FTKModFramework.Core.Diagnostics
{
    /// <summary>
    /// Softlock log signatures (spec #265 FR-2). Diagnostics only: each hook reads game state and may
    /// write one <c>SOFTLOCK-SIGNATURE kind=...</c> Warning line. None changes a value, sends an RPC,
    /// or alters control flow; the load finalizer returns the exception it was given. A Warning is not
    /// an error in the reporting buffer, so a line reaches bug-report logs without an error prompt.
    ///
    /// Gated by <c>[Diagnostics] SoftlockSignatures</c>, read at startup. Every patch class has a
    /// Prepare that returns the setting, so with it off none of these patches is installed. It is a
    /// sibling of <c>StuckTurnWatchdog</c> rather than the same key because the watchdog patches
    /// nothing, while these hooks sit on load, loot, discovery and turn-start methods; each can be
    /// removed without losing the other.
    ///
    /// Each kind writes at most <see cref="SoftlockBudget.MaxPerKind"/> lines per launch. A hook that
    /// fails unexpectedly stops every signature for the rest of the process with one warning.
    /// </summary>
    internal static class SoftlockSignatures
    {
        internal static readonly SoftlockBudget Budget = new SoftlockBudget();
        private static readonly SoftlockLatch DungeonLatch = new SoftlockLatch();
        private static readonly SoftlockLatch CombatLatch = new SoftlockLatch();
        private static readonly PoiDiscoveryWatch Discovery = new PoiDiscoveryWatch();

        private static bool _stopped;
        /// <summary>Which CheckFindPOI is on the stack: "movement", "walk" or null. Unity starts a
        /// coroutine synchronously up to its first yield, and both CheckFindPOI_CR bodies call
        /// GetHiddenPoiInRange before yielding, so the search runs inside CheckFindPOI.</summary>
        private static string _discoverySource;

        internal static bool Enabled
        {
            get { return Plugin.SoftlockSignatures == null || Plugin.SoftlockSignatures.Value; }
        }

        private static bool Active { get { return !_stopped; } }

        private static void Write(SoftlockKind kind, string line)
        {
            if (line == null || !Budget.TryTake(kind)) return;
            Plugin.Log.LogWarning(line);
        }

        private static void Stop(string where, Exception e)
        {
            if (_stopped) return;
            _stopped = true;
            Plugin.Log.LogWarning("Softlock signatures stopped after an unexpected error in " + where + ": " + e.Message);
        }

        private static long Key(FTKPlayerID id)
        {
            return ((long)id.m_PhotonID << 32) | (uint)id.m_TurnIndex;
        }

        private static string Player(FTKPlayerID id)
        {
            return id.m_PhotonID.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + id.m_TurnIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string HexOf(HexLand hex)
        {
            return hex != null ? SoftlockSignature.Hex(hex.m_ParentIndex, hex.m_Index) : "none";
        }

        private static bool SameHex(HexLand a, HexLand b)
        {
            return a != null && b != null && a.m_ParentIndex == b.m_ParentIndex && a.m_Index == b.m_Index;
        }

        // ---- 1. Load exception --------------------------------------------------------------------

        internal static void LoadException(MiniHexInfo poi, Exception exception)
        {
            if (!Active || exception == null) return;
            try
            {
                string type = "unavailable";
                bool hasHex = false;
                int big = -1, small = -1, carrying = -1, markers = -1;
                if (poi != null)
                {
                    type = poi.m_MiniHexType.ToString();
                    HexLand hex = poi.m_HexLand;
                    if (hex != null)
                    {
                        hasHex = true;
                        big = hex.m_ParentIndex;
                        small = hex.m_Index;
                    }
                    carrying = poi.m_CarryingPlayersSerialized != null ? poi.m_CarryingPlayersSerialized.Count : -1;
                    markers = poi.m_DestForQuests != null ? poi.m_DestForQuests.Count : -1;
                }
                Write(SoftlockKind.LoadException,
                    SoftlockSignature.LoadException(exception, type, big, small, hasHex, carrying, markers));
            }
            catch (Exception e)
            {
                Stop("MiniHexInfo.DeserializeFinalRPC", e);
            }
        }

        // ---- 2. Empty loot vote queue ---------------------------------------------------------------

        internal static void LootVote(EncounterSessionMC session)
        {
            if (!Active || session == null || session.m_VoteType != EncounterSessionMC.VoteType.Loot) return;
            try
            {
                List<FTKPlayerID> queue = session.m_VoteQueue;
                int count = queue != null ? queue.Count : 0;
                bool firstNull = count > 0 && FTKPlayerID.IsNull(queue[0]);
                FTKHub hub = FTKHub.Instance;
                bool found = count > 0 && hub != null && hub.GetCharacterOverworldByFID(queue[0]) != null;
                string reason = SoftlockSignature.LootVoteFault(true, count, firstNull, found);
                if (reason == null) return;
                List<FTKPlayerID> alive = session.m_AllCombtatantsAlive;
                Write(SoftlockKind.LootVote, SoftlockSignature.Line(SoftlockKind.LootVote)
                    .Add("reason", reason)
                    .Add("queue", count)
                    .Add("first", count > 0 ? Player(queue[0]) : null)
                    .Add("winner", Player(session.m_WinningPlayerID))
                    .Add("alive", alive != null ? alive.Count : -1)
                    .Add("location", session.m_EncounterLocation.ToString())
                    .Add("master", PhotonNetwork.isMasterClient)
                    .ToString());
            }
            catch (Exception e)
            {
                Stop("EncounterSessionMC.UpdateVoteQueue", e);
            }
        }

        // ---- 3. Repeated POI discovery ------------------------------------------------------------

        internal static void EnterDiscovery(string source) { _discoverySource = source; }

        internal static void ExitDiscovery() { _discoverySource = null; }

        internal static void Discovered(MiniHexInfo poi)
        {
            string source = _discoverySource;
            if (!Active || source == null) return;
            try
            {
                HexLand hex = poi != null ? poi.m_HexLand : null;
                int count = Discovery.Observe(poi != null, hex != null ? hex.m_ParentIndex : -1, hex != null ? hex.m_Index : -1);
                if (count == 0) return;
                Write(SoftlockKind.PoiRediscovery, SoftlockSignature.Line(SoftlockKind.PoiRediscovery)
                    .Add("source", source)
                    .Add("hex", HexOf(hex))
                    .Add("poi", poi.m_MiniHexType.ToString())
                    .Add("count", count)
                    .Add("hidden", poi.m_Hidden)
                    .Add("findLock", poi.m_FindLock)
                    .Add("airPoi", hex != null && hex.m_AirPOI != null)
                    .ToString());
            }
            catch (Exception e)
            {
                Stop("FTKHex.GetHiddenPoiInRange", e);
            }
        }

        // ---- 4 and 5. Turn-start character checks ---------------------------------------------------

        internal static void TurnStart()
        {
            if (!Active) return;
            try
            {
                FTKHub hub = FTKHub.Instance;
                if (hub == null || hub.m_CharacterOverworlds == null) return;
                GameFlow flow = GameFlow.Instance;
                MiniHexDungeon dungeon = flow != null ? flow.m_DungeonEntered : null;
                HexLand dungeonHex = dungeon != null ? dungeon.m_HexLand : null;
                EncounterSession session = EncounterSession.Instance;
                EncounterSessionMC master = EncounterSessionMC.Instance;
                bool sessionCombat = session != null && session.m_IsInCombat;
                bool masterCombat = master != null && master.m_IsInCombat;
                bool started = master != null && master.m_EncounterStarted;

                foreach (CharacterOverworld cow in hub.m_CharacterOverworlds)
                {
                    if (cow == null) continue;
                    long key = Key(cow.m_FTKPlayerID);
                    HexLand hex = cow.m_HexLand;

                    string stale = SoftlockSignature.StaleDungeonFault(cow.m_EnteredDungeon, dungeonHex != null,
                        SameHex(hex, dungeonHex));
                    if (DungeonLatch.Observe(key, stale != null))
                        Write(SoftlockKind.StaleDungeon, SoftlockSignature.Line(SoftlockKind.StaleDungeon)
                            .Add("reason", stale)
                            .Add("character", Player(cow.m_FTKPlayerID))
                            .Add("hex", HexOf(hex))
                            .Add("dungeon", dungeon != null ? dungeon.m_MiniHexType.ToString() + "@" + HexOf(dungeonHex) : null)
                            .Add("respawning", cow.m_WaitForRespawn)
                            .Add("current", Player(GameLogic.Instance != null ? GameLogic.Instance.m_CurrentPlayer : FTKPlayerID.Null))
                            .ToString());

                    bool inCombat = cow.m_CharacterStats != null && cow.m_CharacterStats.m_IsInCombat;
                    bool orphan = SoftlockSignature.CombatWithoutEncounter(inCombat, sessionCombat, masterCombat, started);
                    if (CombatLatch.Observe(key, orphan))
                        Write(SoftlockKind.CombatWithoutEncounter, SoftlockSignature.Line(SoftlockKind.CombatWithoutEncounter)
                            .Add("character", Player(cow.m_FTKPlayerID))
                            .Add("hex", HexOf(hex))
                            .Add("dungeon", cow.m_EnteredDungeon)
                            .Add("respawning", cow.m_WaitForRespawn)
                            .Add("dummy", cow.m_CurrentDummy != null)
                            .Add("current", Player(GameLogic.Instance != null ? GameLogic.Instance.m_CurrentPlayer : FTKPlayerID.Null))
                            .ToString());
                }
            }
            catch (Exception e)
            {
                Stop("GameFlow.BeginTurn", e);
            }
        }
    }

    /// <summary>MiniHexInfo.DeserializeFinalRPC is not virtual and no subclass declares its own, so
    /// this one finalizer covers every POI type. GameLogic.DeserializeFinalCR calls it for each entry of
    /// FTKHex.m_MiniHexList with no try/catch; an exception ends that coroutine before it sends
    /// RPC("DeserializeFinalRPC"), so the ClientReady barrier never completes. The finalizer only logs
    /// and returns the same exception, so vanilla's failure is unchanged.</summary>
    [HarmonyPatch(typeof(MiniHexInfo), "DeserializeFinalRPC")]
    internal static class LoadExceptionSignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static Exception Finalizer(MiniHexInfo __instance, Exception __exception)
        {
            if (__exception != null) SoftlockSignatures.LoadException(__instance, __exception);
            return __exception;
        }
    }

    /// <summary>VotePrepare calls UpdateVoteQueue before the loot collection FSM asks for the voter.</summary>
    [HarmonyPatch(typeof(EncounterSessionMC), "UpdateVoteQueue")]
    internal static class LootVoteSignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static void Postfix(EncounterSessionMC __instance) { SoftlockSignatures.LootVote(__instance); }
    }

    [HarmonyPatch(typeof(Movement), "CheckFindPOI")]
    internal static class MovementDiscoverySignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static void Prefix() { SoftlockSignatures.EnterDiscovery("movement"); }

        private static Exception Finalizer(Exception __exception)
        {
            SoftlockSignatures.ExitDiscovery();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "CheckFindPOI")]
    internal static class WalkDiscoverySignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static void Prefix() { SoftlockSignatures.EnterDiscovery("walk"); }

        private static Exception Finalizer(Exception __exception)
        {
            SoftlockSignatures.ExitDiscovery();
            return __exception;
        }
    }

    /// <summary>GameLogic's encounter roll also calls GetHiddenPoiInRange; outside CheckFindPOI the
    /// discovery source is null and the postfix returns at once.</summary>
    [HarmonyPatch(typeof(FTKHex), "GetHiddenPoiInRange")]
    internal static class HiddenPoiSignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static void Postfix(MiniHexInfo __result) { SoftlockSignatures.Discovered(__result); }
    }

    /// <summary>GameFlowMC.BeginTurn sends GameFlow.BeginTurn to every joined client at the start of
    /// each turn, in the overworld and in dungeons, so each machine checks its own copy of every
    /// character once per turn.</summary>
    [HarmonyPatch(typeof(GameFlow), "BeginTurn")]
    internal static class TurnStartSignaturePatch
    {
        private static bool Prepare() { return SoftlockSignatures.Enabled; }

        private static void Postfix() { SoftlockSignatures.TurnStart(); }
    }
}
