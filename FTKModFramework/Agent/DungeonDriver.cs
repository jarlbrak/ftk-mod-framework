using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// The WHOLE in-dungeon traversal -> boss -> victory flow for a SOLO clear, run as a Unity coroutine on
    /// <see cref="BridgeHost"/> (net35: no Task/async, so we poll FSM/EncounterSession state across frames).
    /// The <c>clear_dungeon</c> action only ARMS this driver (and optionally runs <c>enter_dungeon</c> first),
    /// then returns immediately; the agent polls <c>/state.dungeon</c> until <c>cleared</c>/<c>deactivated</c>.
    ///
    /// COUNT-AGNOSTIC: the loop drives by <c>MiniHexDungeon.IsDungeonCleared()</c>, never a hard-coded room or
    /// level count. Each iteration:
    ///   G1 cleared check  -> if <c>IsDungeonCleared()</c>, break to CLEAR-FINISH.
    ///   G2 in combat?     -> reuse the proven overworld win path (uiBattleStanceButtons.CheatKillAll via the
    ///                        existing combat helpers) until the fight ends, then dismiss any post-fight popup.
    ///   G3 else advance   -> Stair room: <c>cow.DungeonStairDecision(true)</c> (descend). Any other room:
    ///                        <c>cow.DungeonEncounter()</c> (-> <c>MiniHexDungeon.Encounter(fid)</c>), which spins
    ///                        up the next EncounterSession (combat or a self-resolving non-combat room); G2 catches
    ///                        combat next iteration, otherwise we dismiss/auto-resolve the popup.
    /// CLEAR-FINISH: force <c>MiniHexDungeon.SetClear()</c> (+ RPCAllButSelf("SetClear") for the solo master) if
    /// the dungeon has not auto-deactivated, so we do not wait a turn cycle. The bound DungeonQuestDef then
    /// completes; <c>m_EndGameAfterLastQuest</c> raises VICTORY, surfaced by the existing /state victory scalars.
    ///
    /// Verified game members (ilspycmd, Assembly-CSharp, Jun 2026):
    ///   FTKHex.GetSpecificDungeon(FTK_dungeonEncounter.ID, FTK_realm.ID=None, int=-1) -> MiniHexDungeon.
    ///   FTKHex.GetPOIList(MiniHexInfo.MiniHexType) -> List&lt;MiniHexInfo&gt;.
    ///   MiniHexDungeon : MiniHexInfo. m_ID (FTK_dungeonEncounter.ID), m_HexLand, m_Level, m_RoomIndex.
    ///   MiniHexDungeon.IsDungeonCleared()/IsInLastRoom()/IsAtLastLevel()/GetLevelCount()/GetRoomCount(int).
    ///   MiniHexDungeon.GetCurrentRoom() -> RoomInfo { EncounterType m_Type }.
    ///   MiniHexDungeon.EncounterType { Next, Enemy, ..., Stair, ..., Cleared, ... }.
    ///   MiniHexDungeon.OnLoadParty(CharacterOverworld) (void, the engage-equivalent enter).
    ///   MiniHexDungeon.Encounter(FTKPlayerID, ContinueFSM=null) (void).
    ///   MiniHexDungeon.SetClear() (void; calls DeactivateHex() -> MiniHexInfo.m_Deactivated=true).
    ///   MiniHexInfo.m_Deactivated/m_Locked/m_Hidden (public bool). RPCAllButSelf(string, object[]).
    ///   CharacterOverworld.DungeonEncounter() (void), DungeonStairDecision(bool) [PunRPC], IsInDungeon()->bool.
    ///   FTKHub.AnyPlayersInDungeon() -> bool. GameFlow.m_DungeonEntered (MiniHexDungeon), m_DungeonEnterCow.
    /// </summary>
    internal static class DungeonDriver
    {
        // Frame budgets (~60fps). A single room (incl. a boss fight + post-fight popups) gets a generous stall
        // guard; the whole-dungeon budget bounds a stuck driver so it always terminates with a named error.
        private const int MaxRoomFrames = 3600;     // ~60s per room (stall guard)
        private const int MaxTotalFrames = 36000;   // ~10min absolute ceiling
        private const int EnterWaitFrames = 600;    // ~10s for OnLoadParty -> inDungeon to flip
        private const int AdvanceSettleFrames = 90;  // ~1.5s for an Encounter to spin up a session

        private static bool _running;
        private static string _lastError;       // null on success; the "at clear_dungeon.<step>" terminus otherwise
        private static string _dungeonKey;      // adventure-relative dungeon id name (e.g. "FloodedCrypt")
        private static bool _enterFirst;

        public static bool IsRunning { get { return _running; } }
        public static string LastError { get { return _lastError; } }

        /// <summary>
        /// Arm the clear coroutine on the BridgeHost (must be called on the main thread). Idempotent: returns
        /// false if already running. <paramref name="dungeonKey"/> defaults to "FloodedCrypt".
        /// <paramref name="enterFirst"/> runs the enter sequence before traversal when not already in a dungeon.
        /// </summary>
        public static bool Arm(string dungeonKey, bool enterFirst)
        {
            if (_running) return false;
            BridgeHost host = BridgeHost.Instance;
            if (host == null)
            {
                Plugin.Log.LogError("[agent] dungeon clear failed at arm: no BridgeHost (no session host)");
                return false;
            }
            _dungeonKey = string.IsNullOrEmpty(dungeonKey) ? "FloodedCrypt" : dungeonKey;
            _enterFirst = enterFirst;
            _lastError = null;
            _running = true;
            try { host.StartCoroutine(Drive()); }
            catch (Exception e)
            {
                _running = false;
                _lastError = "at clear_dungeon.arm: " + e.Message;
                Plugin.Log.LogError("[agent] dungeon " + _lastError);
                return false;
            }
            return true;
        }

        private static IEnumerator Drive()
        {
            // PRECONDITION: be inside the dungeon. If asked to enter, run the same OnLoadParty path enter_dungeon
            // uses, then wait for inDungeon to flip (it lands on a later FSM pump, same async shape as engage).
            if (!InDungeon())
            {
                if (!_enterFirst)
                {
                    Done("at clear_dungeon.enter: not in dungeon (pass {enter:true} or call enter_dungeon first)");
                    yield break;
                }

                object enterResult = DungeonOps.EnterDungeon(_dungeonKey);
                bool enterOk = DungeonOps.ResultOk(enterResult);
                if (!enterOk)
                {
                    Done("at clear_dungeon.enter: " + DungeonOps.ResultError(enterResult));
                    yield break;
                }

                int ef = 0;
                while (ef < EnterWaitFrames && !InDungeon()) { ef++; yield return null; }
                if (!InDungeon())
                {
                    Done("at clear_dungeon.enterWait: OnLoadParty kicked but inDungeon never flipped in budget");
                    yield break;
                }
                Plugin.Log.LogInfo("[agent] dungeon: entered, beginning traversal.");
            }

            int total = 0;

            // MAIN LOOP -- count-agnostic, driven by IsDungeonCleared().
            while (true)
            {
                // G0: the entered dungeon.
                object d = EnteredDungeon();
                if (d == null) { Done("at clear_dungeon.dungeon: GameFlow.m_DungeonEntered null mid-clear"); yield break; }

                // G1: cleared? -> CLEAR-FINISH.
                if (IsDungeonCleared(d)) break;

                int roomFrames = 0;

                // G2: in combat? -> win it with the proven KillAll path, then clear the post-fight popup.
                if (CombatActive())
                {
                    while (CombatActive() && roomFrames < MaxRoomFrames && total < MaxTotalFrames)
                    {
                        // Reuse the exact overworld win primitive: select first live enemy + CheatKillAll. The
                        // helper is a clean no-op when it is the enemy's turn / the UI is not ready, so we just
                        // keep calling it each frame until combat ends.
                        DungeonOps.TryWinCombatTurn();
                        roomFrames++; total++;
                        yield return null;
                    }
                    // Clear any post-fight / reward popup so the next room can advance.
                    DungeonOps.DismissPopups();
                    yield return null; yield return null;
                    if (roomFrames >= MaxRoomFrames) { Done("at clear_dungeon.combat: fight did not end in budget"); yield break; }
                    if (total >= MaxTotalFrames) { Done("at clear_dungeon.budget: total frame ceiling hit in combat"); yield break; }
                    continue;
                }

                // G3: advance the current room.
                string roomType = CurrentRoomType(d);
                if (roomType == "Stair")
                {
                    // Descend to the next level. DungeonStairDecision(true) -> ResetEncounterType + m_DungeonFlow
                    // "fadeTransition". Single-level overrides never hit this branch (loop is count-agnostic).
                    if (!DungeonOps.StairDescend())
                    {
                        Done("at clear_dungeon.stair: DungeonStairDecision unavailable");
                        yield break;
                    }
                    Plugin.Log.LogInfo("[agent] dungeon: descending stair (level " + Dlevel(d) + ").");
                }
                else
                {
                    // Any other room (Enemy/boss OR non-combat): fire the room encounter, then let the session
                    // spin up. G2 catches a combat room next iteration; a non-combat room is auto-resolved by
                    // dismissing its popups.
                    if (!DungeonOps.AdvanceRoom())
                    {
                        Done("at clear_dungeon.advance: could not advance room (DungeonEncounter unavailable)");
                        yield break;
                    }
                    Plugin.Log.LogInfo("[agent] dungeon: advancing room type=" + (roomType ?? "?")
                                       + " (L" + Dlevel(d) + " R" + Droom(d) + ").");

                    // Give the EncounterSession a few frames to come up; if it did not become combat, treat it as
                    // a self-resolving non-combat room and dismiss/auto-resolve its popups.
                    int settle = 0;
                    while (settle < AdvanceSettleFrames && !CombatActive()) { settle++; total++; yield return null; }
                    if (!CombatActive())
                    {
                        DungeonOps.DismissPopups();
                        yield return null; yield return null;
                    }
                }

                total++;
                yield return null;
                if (total >= MaxTotalFrames) { Done("at clear_dungeon.budget: total frame ceiling hit during traversal"); yield break; }
            }

            // CLEAR-FINISH: ensure the dungeon is deactivated (the VICTORY-armed gate). SetClear normally fires
            // on its own at the next turn start, but the driver forces it so victory lands immediately.
            object dd = EnteredDungeon();
            if (dd == null) { Done("at clear_dungeon.finish: GameFlow.m_DungeonEntered null at finish"); yield break; }
            if (!IsDeactivated(dd))
            {
                DungeonOps.ForceClear(dd);
                yield return null; yield return null;
            }

            int fin = 0;
            while (fin < EnterWaitFrames && !IsDeactivated(dd)) { fin++; yield return null; }
            if (!IsDeactivated(dd))
            {
                Done("at clear_dungeon.deactivate: SetClear did not deactivate the dungeon in budget");
                yield break;
            }

            Plugin.Log.LogInfo("[agent] dungeon: CLEARED (deactivated). Victory should arm via the bound quest.");
            Done(null);
        }

        // ------------------------------------------------------------- completion ------------------------

        private static void Done(string error)
        {
            _running = false;
            _lastError = error;
            if (error == null)
                Plugin.Log.LogInfo("[agent] dungeon clear: done. crypt cleared, victory armed.");
            else
                Plugin.Log.LogError("[agent] dungeon clear: " + error);
        }

        // ------------------------------------------------------------- state probes ----------------------

        private static bool InDungeon() { return DungeonOps.InDungeon(); }
        private static object EnteredDungeon() { return DungeonOps.EnteredDungeon(); }

        private static bool IsDungeonCleared(object d)
        {
            object r = DungeonOps.SafeInvoke(d, "IsDungeonCleared");
            return r is bool && (bool)r;
        }

        private static bool IsDeactivated(object d)
        {
            object r = DungeonOps.SafeField(d, "m_Deactivated");
            return r is bool && (bool)r;
        }

        private static bool CombatActive() { return DungeonOps.CombatActive(); }

        private static string CurrentRoomType(object d)
        {
            object room = DungeonOps.SafeInvoke(d, "GetCurrentRoom");
            if (room == null) return null;
            object t = DungeonOps.SafeField(room, "m_Type");
            return t != null ? t.ToString() : null;
        }

        private static string Dlevel(object d)
        {
            object v = DungeonOps.SafeField(d, "m_Level");
            return (v is int ? (int)v : -1).ToString();
        }

        private static string Droom(object d)
        {
            object v = DungeonOps.SafeField(d, "m_RoomIndex");
            return (v is int ? (int)v : -1).ToString();
        }
    }
}
