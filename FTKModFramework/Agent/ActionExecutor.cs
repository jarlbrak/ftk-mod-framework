using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// POST /action dispatch. Every action: (1) is gated on GameLogic.IsSinglePlayer() so co-op is never
    /// perturbed; (2) validates its preconditions BEFORE any game call and returns {ok:false,error} on a
    /// miss rather than throwing to HTTP; (3) resolves all game singletons/members by name via
    /// AccessTools + Reflect, so a missing member degrades to {ok:false,error} instead of a compile error
    /// or crash.
    ///
    /// MUST run on the Unity main thread (marshalled by the bridge). The returned dictionary becomes the
    /// HTTP body {ok, error, result}.
    /// </summary>
    internal static class ActionExecutor
    {
        // Menu-phase allowlist. These three actions run at the TITLE SCREEN where there is no live session
        // yet, so they MUST dispatch BEFORE the in-session single-player gate below. They configure offline
        // single-player themselves (start_run sets m_GameMode=SinglePlayer + CreateOfflineRoom), so requiring
        // IsSinglePlayer()==true up front would deadlock the very flow that establishes it. Each still
        // confirms GameLogic.Instance where it actually needs it.
        private static readonly HashSet<string> MenuActions =
            new HashSet<string> { "start_run", "dismiss_dialog", "list_adventures" };

        public static object Execute(string action, IDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(action))
                return Fail("missing 'action'");

            // Menu-phase actions bypass the in-session SP gate (they run at the title screen). They are
            // still wrapped in the outer try/catch so a thrown member degrades to {ok:false,error}.
            if (MenuActions.Contains(action))
            {
                try
                {
                    switch (action)
                    {
                        case "dismiss_dialog": return DismissDialog(args);
                        case "list_adventures": return ListAdventures(args);
                        case "start_run": return StartRun(args);
                    }
                }
                catch (Exception e)
                {
                    return Fail(action + " threw: " + e.Message);
                }
            }

            // Single-player gate (three independent guards: env, loopback, this check).
            object gl = StaticInstance("GameLogic");
            if (gl == null)
                return Fail("no live GameLogic (not in a session)");
            object spObj = SafeInvoke(gl, "IsSinglePlayer");
            if (!(spObj is bool) || !(bool)spObj)
                return Fail("not-single-player");

            try
            {
                switch (action)
                {
                    case "snap_to": return SnapTo(args);
                    case "move_to": return MoveTo(args);
                    case "engage": return Engage(args);
                    case "combat_turn": return CombatTurn(args);
                    case "win_combat": return WinCombat(args);
                    case "force_win": return ForceWin(args);
                    case "auto_combat": return AutoCombat(args);
                    case "auto_combat_turn": return AutoCombatTurn(args);
                    case "combat_status": return CombatStatus(args);
                    case "set_target": return SetTarget(args);
                    case "choose_ability": return ChooseAbility(args);
                    case "set_focus": return SetFocus(args);
                    case "attack": return Attack(args);
                    case "resolve_turn": return ResolveTurn(args);
                    case "end_turn": return EndTurn(args);
                    case "select_choice": return SelectChoice(args);
                    case "advance": return Advance(args);
                    case "dismiss_message": return DismissMessage(args);
                    case "enter_dungeon": return EnterDungeon(args);
                    case "advance_room": return AdvanceRoom(args);
                    case "dungeon_encounter": return DungeonOps.DungeonEncounterAction();
                    case "cleared_room": return DungeonOps.ClearedRoomAction();
                    case "dungeon_debug": return DungeonOps.DungeonDebugAction();
                    case "dungeon_regen": return DungeonOps.DungeonRegenAction();
                    case "dungeon_scroll_complete": return DungeonOps.DungeonScrollCompleteAction();
                    case "session_debug": return DungeonOps.SessionDebugAction();
                    case "force_clear": return ForceClear(args);
                    case "clear_dungeon": return ClearDungeon(args);
                    case "quest_info": return DungeonOps.QuestInfoAction();
                    case "quest_advance": return DungeonOps.QuestAdvanceAction();
                    case "force_victory": return DungeonOps.ForceVictoryAction();
                    case "show_endgame": return DungeonOps.ShowEndgameAction();
                    case "enter_tile": return Ok(null); // no-op: the Arrive_* FSM auto-fires on arrival.
                    default: return Fail("unknown action '" + action + "'");
                }
            }
            catch (Exception e)
            {
                return Fail(action + " threw: " + e.Message);
            }
        }

        // ----------------------------------------------------------------- actions ----------------------

        private static object SnapTo(IDictionary<string, object> args)
        {
            int big, small;
            if (!GetTile(args, out big, out small)) return Fail("snap_to needs int args {big,small}");

            object cow = CurrentCow();
            if (cow == null) return Fail("no current character");
            object hexInstance = StaticInstance("FTKHex");
            if (hexInstance == null) return Fail("FTKHex.Instance unavailable");
            // FTKHex.GetHexLand has two overloads, (int,int) and (HexLandID); resolve the (int,int) one
            // explicitly so the lookup does not throw AmbiguousMatchException.
            object dest = GetHexLandByIndex(hexInstance, big, small);
            if (dest == null) return Fail("no hex at (" + big + "," + small + ")");

            // cow.SnapTo(dest, false, true) -- deterministic placement without an encounter roll.
            object snap = Reflect.Invoke(cow, "SnapTo", dest, false, true);
            return Ok(Tile("snappedTo", big, small));
        }

        private static object MoveTo(IDictionary<string, object> args)
        {
            int big, small;
            if (!GetTile(args, out big, out small)) return Fail("move_to needs int args {big,small}");

            object cow = CurrentCow();
            if (cow == null) return Fail("no current character");
            // m_HexLand is a property (backing _HexLand); read it as a property first, like StateReader does,
            // then fall back to a field of the same name. SafeField alone misses the property and returns null.
            object start = SafeProp(cow, "m_HexLand");
            if (start == null) start = SafeField(cow, "m_HexLand");
            if (start == null) return Fail("no current hex");

            object hexInstance = StaticInstance("FTKHex");
            if (hexInstance == null) return Fail("FTKHex.Instance unavailable");
            // FTKHex.GetHexLand has two overloads, (int,int) and (HexLandID); resolve the (int,int) one
            // explicitly so the lookup does not throw AmbiguousMatchException.
            object goal = GetHexLandByIndex(hexInstance, big, small);
            if (goal == null) return Fail("no hex at (" + big + "," + small + ")");

            // Fast path: goal is a direct neighbor. Walk one adjacent step with MoveTo(goal, 0, 1, true).
            if (IsDirectNeighbor(start, big, small))
            {
                Reflect.Invoke(cow, "MoveTo", goal, 0, 1, true);
                Plugin.Log.LogInfo("[agent] move_to " + big + "," + small + " -> step (neighbor)");
                return Ok(MoveResult(big, small, cow));
            }

            // Multi-step: pathfind on land, stage the full path via Movement.SetFSMWalkPath, then MoveTo the
            // goal with the path length so the movement FSM follows the staged route.
            List<object> path;
            if (!FindLandPath(start, goal, out path) || path == null || path.Count <= 1)
                return Fail("no path to (" + big + "," + small + ")");

            if (!StageWalkPath(path))
                return Fail("could not stage walk path");

            int steps = path.Count - 1;
            Reflect.Invoke(cow, "MoveTo", goal, 0, steps, true);
            Plugin.Log.LogInfo("[agent] move_to " + big + "," + small + " -> path (" + steps + " steps)");
            return Ok(MoveResult(big, small, cow));
        }

        // Scan start.m_Neighbors[0 .. m_NeighborCount) for a hex whose (m_ParentIndex,m_Index) matches the goal.
        private static bool IsDirectNeighbor(object start, int big, int small)
        {
            object neighborsObj = SafeField(start, "m_Neighbors");
            object[] neighbors = neighborsObj as object[];
            if (neighbors == null)
            {
                // m_Neighbors is HexLand[], not object[]; fall back through IList.
                IList list = neighborsObj as IList;
                if (list == null) return false;
                int n = ToInt(SafeField(start, "m_NeighborCount")) ?? list.Count;
                for (int i = 0; i < n && i < list.Count; i++)
                    if (HexMatches(list[i], big, small)) return true;
                return false;
            }
            int count = ToInt(SafeField(start, "m_NeighborCount")) ?? neighbors.Length;
            for (int i = 0; i < count && i < neighbors.Length; i++)
                if (HexMatches(neighbors[i], big, small)) return true;
            return false;
        }

        private static bool HexMatches(object hex, int big, int small)
        {
            if (hex == null) return false;
            int? hb = ToInt(SafeField(hex, "m_ParentIndex"));
            int? hs = ToInt(SafeField(hex, "m_Index"));
            return hb.HasValue && hs.HasValue && hb.Value == big && hs.Value == small;
        }

        // HexLand.FindPath(start, goal, PathFindingStartState.OnLand, ref List<HexLand> path) -- static. The ref
        // List arg is passed via an object[] slot that Invoke writes back after the call.
        private static bool FindLandPath(object start, object goal, out List<object> outPath)
        {
            outPath = null;
            try
            {
                Type hexLandType = AccessTools.TypeByName("HexLand");
                if (hexLandType == null) return false;
                Type startStateType = AccessTools.TypeByName("HexLand+PathFindingStartState");
                if (startStateType == null) return false;

                Type listType = typeof(List<>).MakeGenericType(hexLandType);
                Type refListType = listType.MakeByRefType();
                MethodInfo m = hexLandType.GetMethod("FindPath", Reflect.All, null,
                    new[] { hexLandType, hexLandType, startStateType, refListType }, null);
                if (m == null) return false;

                object onLand = Enum.ToObject(startStateType, 0); // PathFindingStartState.OnLand (ordinal 0)
                object pathList = Activator.CreateInstance(listType);
                object[] callArgs = new object[] { start, goal, onLand, pathList };
                object res = m.Invoke(null, callArgs);
                if (!(res is bool) || !(bool)res) return false;

                // Read the (possibly reassigned) path back out of the ref slot.
                IEnumerable seq = callArgs[3] as IEnumerable;
                if (seq == null) return false;
                List<object> path = new List<object>();
                foreach (object h in seq) path.Add(h);
                outPath = path;
                return true;
            }
            catch { return false; }
        }

        // Movement.SetFSMWalkPath(List<HexLand>) is static; it copies the route into the movement FSM's
        // "Move Path" playmaker list (skipping path[0], the current hex). Build a typed List<HexLand> from the
        // path and invoke it reflectively.
        private static bool StageWalkPath(List<object> path)
        {
            try
            {
                Type hexLandType = AccessTools.TypeByName("HexLand");
                Type movementType = AccessTools.TypeByName("Movement");
                if (hexLandType == null || movementType == null) return false;

                Type listType = typeof(List<>).MakeGenericType(hexLandType);
                MethodInfo add = listType.GetMethod("Add");
                object typed = Activator.CreateInstance(listType);
                foreach (object h in path) add.Invoke(typed, new[] { h });

                MethodInfo m = movementType.GetMethod("SetFSMWalkPath", Reflect.All, null,
                    new[] { listType }, null);
                if (m == null) return false;
                m.Invoke(null, new[] { typed });
                return true;
            }
            catch { return false; }
        }

        private static Dictionary<string, object> MoveResult(int big, int small, object cow)
        {
            Dictionary<string, object> tile = new Dictionary<string, object>();
            tile["big"] = big;
            tile["small"] = small;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["tile"] = tile;
            object stats = SafeField(cow, "m_CharacterStats");
            d["actionPointsLeft"] = stats != null ? (object)ToInt(SafeField(stats, "m_ActionPoints")) : null;
            return d;
        }

        // ============================================================ engage ===========================

        /// <summary>
        /// Deterministically enter an overworld combat (Option A from kb_b834abf4). The move_to path bypasses
        /// the encounter trigger because a single direct MoveTo(goal,0,1,true) hop skips the per-hex FSM rolls.
        /// engage instead walks the party ONTO an ADJACENT enemy hex in Attack mode, then calls the verified
        /// managed terminus GameFlow.LocalInitCombatSession("fight", new ContinueFSM(noop)). Combat does not go
        /// live synchronously; the agent polls /state.combat (combat.active) until it flips true.
        ///
        /// Optional args {hexBig,hexSmall} pick a specific adjacent enemy hex; otherwise the first adjacent
        /// MiniHexEnemy/Camp is used. Fully gated and defensive: any precondition miss returns ok:false with a
        /// precise "at engage.&lt;step&gt;" error and never throws.
        /// </summary>
        private static object Engage(IDictionary<string, object> args)
        {
            // GATE 1: in-world.
            object usg = StaticInstance("uiStartGame");
            if (usg == null || !ToBool(SafeField(usg, "m_GameStarted")))
                return Fail("at engage.inWorld: not in-world");

            // GATE 2: this COW's overworld turn, with a hex.
            object cow = CurrentCow();
            if (cow == null) return Fail("at engage.cow: no current character");
            object hex = SafeProp(cow, "m_HexLand");
            if (hex == null) hex = SafeField(cow, "m_HexLand");
            if (hex == null) return Fail("at engage.cow: no current hex");

            // Already standing on an enemy hex (e.g. a prior engage hopped on but combat did not start)?
            // Skip straight to session init.
            if (IsEnemyMiniHex(SafeInvoke(cow, "GetMiniHexInfo")))
                return StartCombatSession(cow, "already-on-enemy-hex");

            // STEP 3: find an adjacent enemy hex (MiniHexEnemy; camps subclass it). Respect an explicit
            // {hexBig,hexSmall} if supplied.
            int? wantBig = GetInt(args, "hexBig");
            int? wantSmall = GetInt(args, "hexSmall");
            object targetHex = FindAdjacentEnemyHex(hex, wantBig, wantSmall);
            if (targetHex == null)
            {
                // No adjacent enemy: locate any roaming enemy POI on the map and SNAP the party onto it.
                // SnapTo is deterministic placement (no encounter roll); the explicit LocalInitCombatSession
                // below starts the fight regardless, so this reliably enters combat for the test harness.
                object enemyHex = FindAnyEnemyPoiHex(wantBig, wantSmall);
                if (enemyHex == null)
                    return Fail("at engage.findEnemy: no enemy POI on the map");
                int sb = ToInt(SafeField(enemyHex, "m_ParentIndex")) ?? -1;
                int ss = ToInt(SafeField(enemyHex, "m_Index")) ?? -1;
                Reflect.Invoke(cow, "SnapTo", enemyHex, false, true);
                Plugin.Log.LogInfo("[agent] engage: snapped onto enemy POI (" + sb + "," + ss + ")");
                if (!IsEnemyMiniHex(SafeInvoke(cow, "GetMiniHexInfo")))
                    return Fail("at engage.snapEnemy: not on an enemy POI after snap (" + sb + "," + ss + ")");
                return StartCombatSession(cow, "snapped " + sb + "," + ss);
            }

            int tb = ToInt(SafeField(targetHex, "m_ParentIndex")) ?? -1;
            int ts = ToInt(SafeField(targetHex, "m_Index")) ?? -1;

            // STEP 4: hop ONTO the enemy hex. MoveTo(target, _moveCount:0, _pathLength:1, _decActionPoint:true):
            // flag=(0>=1-1)=true => MovementMode.Attack, party walks onto the enemy hex.
            Reflect.Invoke(cow, "MoveTo", targetHex, 0, 1, true);
            Plugin.Log.LogInfo("[agent] engage: Attack-mode hop onto enemy hex (" + tb + "," + ts + ")");

            // STEP 5: VERIFY the current COW now stands on the enemy POI (unverified-live #4: the AttackMove
            // coroutine may consume the POI). If not, report a precise failure so the agent can fall back.
            object mhi = SafeInvoke(cow, "GetMiniHexInfo");
            if (!IsEnemyMiniHex(mhi))
                return Fail("at engage.onEnemyHex: GetMiniHexInfo() not a MiniHexEnemy after hop "
                            + "(POI may have been consumed; try again or move adjacent + retry)");

            return StartCombatSession(cow, tb + "," + ts);
        }

        // STEP 6: GameFlow.LocalInitCombatSession("fight", new ContinueFSM(noop)) -- the verified deterministic
        // managed combat-start terminus. ContinueFSM ctor takes a finish callback; mirror MiniHexInfo.OnFight
        // (new ContinueFSM(FightFinished)) with a trivial main-thread no-op. Fire-and-forget: combat goes live
        // on a later pump, observed via /state.combat.active.
        private static object StartCombatSession(object cow, string where)
        {
            object gf = StaticInstance("GameFlow");
            if (gf == null) return Fail("at engage.combatStart: GameFlow.Instance unavailable");

            object cfsm = MakeNoopContinueFSM();
            if (cfsm == null) return Fail("at engage.combatStart: could not build ContinueFSM");

            object res = SafeInvokeArgs(gf, "LocalInitCombatSession",
                new[] { typeof(string), cfsm.GetType() }, new object[] { "fight", cfsm });
            // LocalInitCombatSession returns void; a null res is expected. We cannot confirm combat live here
            // (it starts async), so report that the session was kicked off and let the agent poll.
            Plugin.Log.LogInfo("[agent] engage: LocalInitCombatSession(\"fight\") kicked (" + where + ")");
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["engaged"] = true;
            d["where"] = where;
            d["note"] = "poll /state.combat.active until true";
            return Ok(d);
        }

        // Find any roaming enemy POI hex (MiniHexEnemy / EnemyCamp) on the map via FTKHex.GetPOIList. If
        // wantBig/wantSmall are supplied, return that specific enemy hex; otherwise the first one found.
        // Returns the HexLand (or null). Used by engage when no enemy is directly adjacent so the harness can
        // reach a fight without blind wandering. MiniHexType is nested in MiniHexInfo (resolved by name).
        private static object FindAnyEnemyPoiHex(int? wantBig, int? wantSmall)
        {
            object hexInst = StaticInstance("FTKHex");
            if (hexInst == null) return null;
            Type mtType = AccessTools.TypeByName("MiniHexInfo+MiniHexType");
            if (mtType == null || !mtType.IsEnum) return null;
            foreach (string name in new[] { "Enemy", "EnemyCamp" })
            {
                object miniType;
                try { miniType = Enum.Parse(mtType, name); }
                catch { continue; }
                object list = SafeInvokeArgs(hexInst, "GetPOIList", new Type[] { mtType }, new object[] { miniType });
                System.Collections.IEnumerable en = list as System.Collections.IEnumerable;
                if (en == null) continue;
                foreach (object mhi in en)
                {
                    if (mhi == null) continue;
                    object h = SafeField(mhi, "m_HexLand");
                    if (h == null) continue;
                    if (wantBig.HasValue && wantSmall.HasValue)
                    {
                        int b = ToInt(SafeField(h, "m_ParentIndex")) ?? -1;
                        int s = ToInt(SafeField(h, "m_Index")) ?? -1;
                        if (b == wantBig.Value && s == wantSmall.Value) return h;
                    }
                    else return h;
                }
            }
            return null;
        }

        // Build a ContinueFSM with a trivial finish callback. ContinueFSM's ctor is (Action) / (ContinueDelegate)
        // in this build; try the available constructors and pass a no-op delegate of the expected type. Returns
        // null only if no usable ctor is found.
        private static object MakeNoopContinueFSM()
        {
            try
            {
                Type cfsmType = AccessTools.TypeByName("ContinueFSM");
                if (cfsmType == null) return null;

                // Preferred (verified): the simple ctor ContinueFSM(WaitClients _waitClients = Self). It needs no
                // delegate construction and leaves the finish callbacks null, which Continue() treats as a no-op
                // (the game's own FSM drives session teardown). WaitClients is nested in ContinueFSM.
                Type wcType = AccessTools.TypeByName("ContinueFSM+WaitClients");
                if (wcType == null) wcType = AccessTools.TypeByName("WaitClients");
                if (wcType != null && wcType.IsEnum)
                {
                    ConstructorInfo wcCtor = cfsmType.GetConstructor(new Type[] { wcType });
                    if (wcCtor != null)
                    {
                        object self;
                        try { self = Enum.Parse(wcType, "Self"); }
                        catch { self = Enum.ToObject(wcType, 1); }
                        return wcCtor.Invoke(new object[] { self });
                    }
                }

                ConstructorInfo[] ctors = cfsmType.GetConstructors(Reflect.All);
                // Prefer a single-delegate ctor (mirrors new ContinueFSM(FightFinished)).
                foreach (ConstructorInfo c in ctors)
                {
                    ParameterInfo[] ps = c.GetParameters();
                    if (ps.Length == 1 && typeof(Delegate).IsAssignableFrom(ps[0].ParameterType))
                    {
                        Delegate noop = MakeNoopDelegate(ps[0].ParameterType);
                        if (noop != null) return c.Invoke(new object[] { noop });
                    }
                }
                // Fall back to a parameterless ctor if one exists.
                foreach (ConstructorInfo c in ctors)
                    if (c.GetParameters().Length == 0) return c.Invoke(null);
                return null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[agent] engage: ContinueFSM build failed: " + e.Message);
                return null;
            }
        }

        // Create a no-op delegate matching the given delegate type (any signature; returns default).
        private static Delegate MakeNoopDelegate(Type delegateType)
        {
            try
            {
                MethodInfo invoke = delegateType.GetMethod("Invoke");
                if (invoke == null) return null;
                int argc = invoke.GetParameters().Length;
                bool returnsVoid = invoke.ReturnType == typeof(void);
                // The combat-finish callback the game passes is parameterless void (FightFinished()). Only a
                // void, zero-arg delegate maps cleanly to a shared no-op method; anything else we cannot safely
                // satisfy, so return null and let the caller try a parameterless ctor.
                if (argc == 0 && returnsVoid)
                    return Delegate.CreateDelegate(delegateType,
                        typeof(ActionExecutor).GetMethod("CombatFinishNoop", Reflect.All));
                return null;
            }
            catch { return null; }
        }

        // Trivial main-thread combat-finish callback handed to ContinueFSM. The session teardown
        // (FightFinished -> ReturnToOverworld) is driven by the game's own FSM; we only need a valid target.
        private static void CombatFinishNoop()
        {
            try { Plugin.Log.LogInfo("[agent] engage: combat ContinueFSM finished."); } catch { }
        }

        // True iff the mini-hex info is a roaming enemy (MiniHexEnemy) or enemy camp (MiniHexEnemyCamp, which
        // subclasses MiniHexEnemy, so the base check covers it).
        private static bool IsEnemyMiniHex(object mhi)
        {
            if (mhi == null) return false;
            Type enemyType = AccessTools.TypeByName("MiniHexEnemy");
            if (enemyType == null) return false;
            return enemyType.IsInstanceOfType(mhi);
        }

        // Scan a hex's m_Neighbors (HexLand[]) for one with a MiniHexEnemy POI. If wantBig/wantSmall are given,
        // require that exact tile; otherwise return the first match. Mirrors StateReader's neighbor iteration.
        private static object FindAdjacentEnemyHex(object hex, int? wantBig, int? wantSmall)
        {
            object neighborsObj = SafeField(hex, "m_Neighbors");
            IList neighbors = neighborsObj as IList;
            if (neighbors == null) return null;
            int count = ToInt(SafeField(hex, "m_NeighborCount")) ?? neighbors.Count;
            for (int i = 0; i < count && i < neighbors.Count; i++)
            {
                object n = neighbors[i];
                if (n == null) continue;
                object hasPoi = SafeInvoke(n, "HasPOI");
                if (!(hasPoi is bool) || !(bool)hasPoi) continue;
                object poi = SafeField(n, "m_POI");
                if (!IsEnemyMiniHex(poi)) continue;
                if (wantBig.HasValue && wantSmall.HasValue)
                {
                    int nb = ToInt(SafeField(n, "m_ParentIndex")) ?? -1;
                    int ns = ToInt(SafeField(n, "m_Index")) ?? -1;
                    if (nb != wantBig.Value || ns != wantSmall.Value) continue;
                }
                return n;
            }
            return null;
        }

        // ============================================================ combat turn ======================

        /// <summary>
        /// ONE real, turn-ENDING hero attack. The verified-safe single-turn primitive that fixes the in-dungeon
        /// stuck combat: it commits ONLY when all three gates hold (in combat, it is the player's turn, and the
        /// battle-stance UI is parked in "Wait For Stance"); on any gate miss it returns ok:true {acted:false,
        /// waiting:&lt;reason&gt;} so the harness re-polls, NEVER forcing a commit in a not-ready state (firing
        /// before "Wait For Stance" is what corrupted the turn and hung the crypt). The commit routes through the
        /// game's own uiBattleStanceButtons.CheatKillSingle() (sets m_PlayerSlots.m_CheatAttack=KillSingle then
        /// ComputeAttackSlotResults(CombatCow,true) -> StartEngageAttack -> damage -> CombatEnemyDie -> dummy FSM
        /// advances and the turn ENDS), i.e. a clean per-enemy lethal through the REAL damage path, not the
        /// firestorm AoE KillAll. ok:false "at combat_turn.&lt;step&gt;" only on a genuine internal failure.
        ///
        /// args: {targetFid?:{turnIndex,photonId}, cheat?:"KillSingle"|"None", focus?:bool}.
        ///   cheat default KillSingle (guaranteed turn-ending kill); "None" commits a basic Attack() (honest roll).
        ///   focus:true spends max focus before an honest Attack() for the strongest legitimate hit.
        /// </summary>
        private static object CombatTurn(IDictionary<string, object> args)
        {
            // STEP 1 inCombat.
            object mc = StaticInstance("EncounterSessionMC");
            object es = StaticInstance("EncounterSession");
            if (!CombatActive(mc, es))
                return Ok(WaitingResult("not_in_combat"));

            // STEP 2 whoseTurn -- m_FightOrder[0].m_Pid.IsPlayer(). Enemy/banner turn (or empty order) => wait,
            // NEVER force on an enemy turn (that is the hang).
            object active = ActiveTurnFid(mc);
            if (active == null) return Ok(WaitingResult("turn_unresolved"));
            object isPlayer = SafeInvoke(active, "IsPlayer");
            if (!(isPlayer is bool) || !(bool)isPlayer)
                return Ok(WaitingResult("enemy_turn"));

            // STEP 3 ready (THE FIX): m_Initialized && dummy FSM == "Wait For Stance". If not, do NOT commit.
            if (!HeroTurnReady())
                return Ok(WaitingResult("stance_not_ready"));

            // STEP 4 cow.
            object cow = CurrentCombatCow();
            if (cow == null) return Fail("at combat_turn.cow: GetCurrentCombatCOW() null");

            // STEP 5 target: explicit targetFid else first live enemy. None => victory likely; poll combat.active.
            object enemyFid = ResolveFid(args, "targetFid");
            string whoseTargeted = enemyFid != null ? "explicit" : "first-live";
            if (enemyFid == null) enemyFid = FirstLiveEnemyFid(es);
            if (enemyFid == null) return Ok(WaitingResult("no_live_enemy"));

            object bsb = BattleStanceButtons();
            if (bsb == null) return Fail("at combat_turn.ui: battle stance buttons unavailable");

            int? hpBefore = EnemyHpByFid(es, enemyFid);

            // STEP 6 select.
            SafeInvokeArgs(bsb, "SelectEnemyDummy", new[] { enemyFid.GetType() }, new object[] { enemyFid });

            // STEP 7/8/9 commit exactly ONCE through the real turn-ending path. (We are already in "Wait For
            // Stance", so SelectEnemyDummy + the commit settle synchronously within this main-thread call; the
            // CombatDriver coroutine is the place that needs the inter-frame settle yield.)
            string cheat = GetString(args, "cheat") ?? "KillSingle";
            bool focus = GetBool(args, "focus") ?? false;
            string committed;
            if (focus)
            {
                // Honest max-focus hit: spend all focus slots, then a basic Attack(). Falls back to KillSingle if
                // the focus cap is unreadable so the turn still ends cleanly.
                if (TrySpendMaxFocus(cow, bsb)) { Reflect.Invoke(bsb, "Attack"); committed = "Attack(focus)"; }
                else { Reflect.Invoke(bsb, "CheatKillSingle"); committed = "KillSingle"; }
            }
            else if (cheat == "None")
            {
                Reflect.Invoke(bsb, "Attack"); committed = "Attack";
            }
            else
            {
                // Default: clean per-enemy lethal via the game's own CheatKillSingle (real damage + CombatEnemyDie
                // path; sets m_CheatAttack=KillSingle + ComputeAttackSlotResults). Not the firestorm KillAll.
                Reflect.Invoke(bsb, "CheatKillSingle"); committed = "KillSingle";
            }

            Plugin.Log.LogInfo("[agent] combat: combat_turn committed " + committed + " on enemy "
                               + FidLabel(enemyFid) + " (" + whoseTargeted + ", hpBefore="
                               + (hpBefore.HasValue ? hpBefore.Value.ToString() : "?") + ")");

            Dictionary<string, object> d = new Dictionary<string, object>();
            d["committed"] = committed;
            d["target"] = FidDict(enemyFid);
            d["whoseTurnWas"] = "player";
            d["enemyHpBefore"] = hpBefore;
            d["note"] = "turn committed; poll /state.combat (active flips false on resolve, or it becomes the "
                        + "enemy turn next)";
            return Ok(d);
        }

        // Spend max focus on the COW so a following basic Attack() is the strongest legitimate hit. Reads the
        // cap from the active CombatActionProfile.m_Slots (the proficiency's slot count) and writes
        // CharacterStats.m_SpentFocus, clamped to MaxFocus. Returns false (fail-closed) if neither the slot count
        // nor MaxFocus is readable, so the caller falls back to the guaranteed KillSingle.
        private static bool TrySpendMaxFocus(object cow, object bsb)
        {
            try
            {
                object stats = SafeField(cow, "m_CharacterStats");
                if (stats == null) return false;
                int? maxFocus = ToInt(SafeProp(stats, "MaxFocus"));
                if (!maxFocus.HasValue) return false;
                int target = maxFocus.Value;
                // Prefer the active proficiency's slot count as the upper bound on usable focus.
                object profile = SafeField(bsb, "m_CombatActionProfile");
                int? slots = profile != null ? ToInt(SafeField(profile, "m_Slots")) : null;
                if (slots.HasValue && slots.Value >= 0) target = Math.Min(target, slots.Value);
                int clamped = Math.Max(0, Math.Min(target, maxFocus.Value));
                Reflect.SetField(stats, "m_SpentFocus", clamped);
                return true;
            }
            catch { return false; }
        }

        // EnemyInfo.m_CurrentHealth for the dummy keyed by fid in EncounterSession.m_EnemyDummies. Null on miss.
        private static int? EnemyHpByFid(object es, object fid)
        {
            if (es == null || fid == null) return null;
            IDictionary dummies = SafeField(es, "m_EnemyDummies") as IDictionary;
            if (dummies == null) return null;
            int? wantTi = ToInt(SafeField(fid, "m_TurnIndex"));
            int? wantPid = ToInt(SafeField(fid, "m_PhotonID"));
            foreach (DictionaryEntry de in dummies)
            {
                int? ti = ToInt(SafeField(de.Key, "m_TurnIndex"));
                int? pid = ToInt(SafeField(de.Key, "m_PhotonID"));
                if (ti == wantTi && (pid ?? 0) == (wantPid ?? 0))
                    return ToInt(SafeField(de.Value, "m_CurrentHealth"));
            }
            return null;
        }

        /// <summary>
        /// ARM the CombatDriver to loop gated KillSingle per living enemy (waiting out enemy turns and each
        /// commit) until combat resolves clean (win or lose). Returns immediately {status:"resolving"}; the
        /// harness polls /state.combat.active==false. Idempotent: a running driver is not re-armed. Replaces the
        /// old resolve_turn{cheat:KillAll}.
        /// </summary>
        private static object WinCombat(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("at win_combat.inCombat: not in combat");
            if (CombatDriver.IsRunning) return Ok(CombatDriverStatus("resolving"));
            if (!CombatDriver.Arm(false))
                return Fail("at win_combat.arm: could not arm combat driver (already running or no host)");
            return Ok(CombatDriverStatus("resolving"));
        }

        /// <summary>
        /// Same coroutine as win_combat but flagged force_win (per-enemy clean lethal through the real
        /// turn-commit path), for a one-hero-vs-party-balanced boss. Returns {status:"resolving"}.
        /// </summary>
        private static object ForceWin(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("at force_win.inCombat: not in combat");
            if (CombatDriver.IsRunning) return Ok(CombatDriverStatus("resolving"));
            if (!CombatDriver.Arm(true))
                return Fail("at force_win.arm: could not arm combat driver (already running or no host)");
            return Ok(CombatDriverStatus("resolving"));
        }

        /// <summary>
        /// High-level harness loop entry: if not in combat -> ok:false "at auto_combat.notInCombat"; else ARM the
        /// CombatDriver (same as win_combat) and return {status:"resolving"}. Supersedes the old per-frame
        /// auto_combat_turn.
        /// </summary>
        private static object AutoCombat(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("at auto_combat.notInCombat: not in combat");
            if (CombatDriver.IsRunning) return Ok(CombatDriverStatus("resolving"));
            if (!CombatDriver.Arm(false))
                return Fail("at auto_combat.arm: could not arm combat driver (already running or no host)");
            return Ok(CombatDriverStatus("resolving"));
        }

        private static Dictionary<string, object> CombatDriverStatus(string status)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["status"] = status;
            return d;
        }

        /// <summary>
        /// LEGACY per-frame single-turn primitive. Retained for back-compat but now routes the SAME gated, real
        /// turn-ending path as combat_turn (CheatKillSingle behind the three commit gates), so it no longer fires
        /// the corrupting CheatKillAll in a not-ready state. Prefer combat_turn (richer result) or the
        /// auto_combat/win_combat/force_win drivers.
        /// </summary>
        private static object AutoCombatTurn(IDictionary<string, object> args)
        {
            return CombatTurn(args);
        }

        /// <summary>
        /// Read-only combat snapshot convenience (same shape as /state.combat). Defensive; never throws.
        /// </summary>
        private static object CombatStatus(IDictionary<string, object> args)
        {
            object mc = StaticInstance("EncounterSessionMC");
            object es = StaticInstance("EncounterSession");
            Dictionary<string, object> d = new Dictionary<string, object>();
            bool activeCombat = CombatActive(mc, es);
            d["active"] = activeCombat;
            d["heroTurnReady"] = HeroTurnReady();
            d["readyParts"] = ReadyParts();
            object active = ActiveTurnFid(mc);
            if (active != null)
            {
                object isPlayer = SafeInvoke(active, "IsPlayer");
                Dictionary<string, object> wt = new Dictionary<string, object>();
                wt["fid"] = FidDict(active);
                wt["isPlayer"] = isPlayer is bool ? (object)(bool)isPlayer : null;
                wt["source"] = "m_FightOrder[0]";
                d["whoseTurn"] = wt;
            }
            else d["whoseTurn"] = null;

            // perEnemy[].hp/alive plus the stuck fingerprint (in combat && any enemy hp<=0 but alive==true: the
            // death RPC never fired). The harness must NOT re-fire when stuck; it reports and aborts.
            bool stuck = false;
            List<object> perEnemy = new List<object>();
            int live = 0;
            if (es != null)
            {
                IDictionary dummies = SafeField(es, "m_EnemyDummies") as IDictionary;
                if (dummies != null)
                {
                    foreach (DictionaryEntry de in dummies)
                    {
                        object dummy = de.Value;
                        Dictionary<string, object> en = new Dictionary<string, object>();
                        en["fid"] = FidDict(de.Key);
                        int? hp = ToInt(SafeField(dummy, "m_CurrentHealth"));
                        bool alive = ToBool(SafeField(dummy, "m_IsAlive"));
                        en["hp"] = hp;
                        en["alive"] = alive;
                        perEnemy.Add(en);
                        if (alive) live++;
                        if (activeCombat && alive && hp.HasValue && hp.Value <= 0) stuck = true;
                    }
                }
            }
            d["perEnemy"] = perEnemy;
            d["liveEnemies"] = live;
            d["stuck"] = stuck;

            // driver: mirror /state.combat.driver so win_combat/force_win/auto_combat progress is pollable here.
            if (CombatDriver.IsRunning)
            {
                Dictionary<string, object> drv = new Dictionary<string, object>();
                drv["running"] = true;
                drv["lastError"] = CombatDriver.LastError;
                d["driver"] = drv;
            }
            else if (CombatDriver.LastError != null)
            {
                Dictionary<string, object> drv = new Dictionary<string, object>();
                drv["running"] = false;
                drv["lastError"] = CombatDriver.LastError;
                d["driver"] = drv;
            }
            else d["driver"] = null;

            return Ok(d);
        }

        // readyParts:{initialized, fsmState} so the harness can SEE which half of heroTurnReady is false (banner
        // vs enemy-turn vs genuinely-ready) instead of guessing.
        private static Dictionary<string, object> ReadyParts()
        {
            Dictionary<string, object> rp = new Dictionary<string, object>();
            try
            {
                object bsb = BattleStanceButtons();
                rp["initialized"] = bsb != null && ToBool(SafeField(bsb, "m_Initialized"));
                string fsmState = null;
                object cow = CurrentCombatCow();
                object dummy = cow != null ? SafeField(cow, "m_CurrentDummy") : null;
                object fsm = dummy != null ? SafeField(dummy, "m_CharacterDummyFSM") : null;
                object sn = fsm != null ? SafeProp(fsm, "ActiveStateName") : null;
                fsmState = sn as string;
                rp["fsmState"] = fsmState;
            }
            catch { rp["initialized"] = false; rp["fsmState"] = null; }
            return rp;
        }

        // ---- combat-turn helpers ----

        // In combat iff either session manager reports m_IsInCombat. EncounterSessionMC is the master turn
        // manager; EncounterSession holds the dummies. Either being true is treated as live.
        private static bool CombatActive(object mc, object es)
        {
            if (mc != null && ToBool(SafeField(mc, "m_IsInCombat"))) return true;
            if (es != null && ToBool(SafeField(es, "m_IsInCombat"))) return true;
            return false;
        }

        // The active combatant FID: EncounterSessionMC.m_FightOrder[0].m_Pid (an FTKPlayerID). Null on any miss.
        private static object ActiveTurnFid(object mc)
        {
            if (mc == null) return null;
            object foObj = SafeField(mc, "m_FightOrder");
            IList fo = foObj as IList;
            if (fo == null || fo.Count == 0) return null;
            object first = fo[0];
            if (first == null) return null;
            return SafeField(first, "m_Pid");
        }

        // Readiness gate: FTKUI.m_BattleStanceButtons.m_Initialized && current combat COW's dummy FSM
        // ActiveStateName == "Wait For Stance".
        private static bool HeroTurnReady()
        {
            try
            {
                object bsb = BattleStanceButtons();
                if (bsb == null) return false;
                if (!ToBool(SafeField(bsb, "m_Initialized"))) return false;
                object cow = CurrentCombatCow();
                if (cow == null) return false;
                object dummy = SafeField(cow, "m_CurrentDummy");
                if (dummy == null) return false;
                object fsm = SafeField(dummy, "m_CharacterDummyFSM");
                if (fsm == null) return false;
                object stateName = SafeProp(fsm, "ActiveStateName");
                return stateName is string && (string)stateName == "Wait For Stance";
            }
            catch { return false; }
        }

        // GameLogic.GetCurrentCombatCOW() -- the COW whose hero turn it is (FSM global compCombatOverworld).
        private static object CurrentCombatCow()
        {
            object gl = StaticInstance("GameLogic");
            if (gl == null) return null;
            return SafeInvoke(gl, "GetCurrentCombatCOW");
        }

        // First live enemy FID from EncounterSession.m_EnemyDummies (Dictionary<FTKPlayerID,EnemyDummy>), keyed
        // by the dummy whose m_IsAlive is true.
        private static object FirstLiveEnemyFid(object es)
        {
            if (es == null) return null;
            object dummiesObj = SafeField(es, "m_EnemyDummies");
            IDictionary dummies = dummiesObj as IDictionary;
            if (dummies == null) return null;
            foreach (DictionaryEntry de in dummies)
            {
                object dummy = de.Value;
                if (dummy == null) continue;
                if (ToBool(SafeField(dummy, "m_IsAlive"))) return de.Key;
            }
            return null;
        }

        private static int? CountLiveEnemies(object es)
        {
            if (es == null) return null;
            object dummiesObj = SafeField(es, "m_EnemyDummies");
            IDictionary dummies = dummiesObj as IDictionary;
            if (dummies == null) return null;
            int n = 0;
            foreach (DictionaryEntry de in dummies)
                if (de.Value != null && ToBool(SafeField(de.Value, "m_IsAlive"))) n++;
            return n;
        }

        private static Dictionary<string, object> WaitingResult(string waiting)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["acted"] = false;
            d["waiting"] = waiting;
            return d;
        }

        private static Dictionary<string, object> FidDict(object fid)
        {
            if (fid == null) return null;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["turnIndex"] = ToInt(SafeField(fid, "m_TurnIndex"));
            d["photonId"] = ToInt(SafeField(fid, "m_PhotonID"));
            return d;
        }

        private static string FidLabel(object fid)
        {
            if (fid == null) return "?";
            return (ToInt(SafeField(fid, "m_TurnIndex")) ?? -1) + ":" + (ToInt(SafeField(fid, "m_PhotonID")) ?? 0);
        }

        private static object SetTarget(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("not in combat");
            object fid = ResolveFid(args, "enemyFid");
            if (fid == null) return Fail("set_target needs {enemyFid:{turnIndex,photonId}}");

            object bsb = BattleStanceButtons();
            if (bsb == null) return Fail("battle stance buttons unavailable");
            Reflect.Invoke(bsb, "SelectEnemyDummy", fid);
            return Ok(null);
        }

        private static object ChooseAbility(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("not in combat");
            int? profId = GetInt(args, "profId");
            if (!profId.HasValue) return Fail("choose_ability needs {profId:int}");

            object bsb = BattleStanceButtons();
            if (bsb == null) return Fail("battle stance buttons unavailable");

            object profs = Reflect.GetField(bsb, "m_Proficiencies");
            IEnumerable seq = profs as IEnumerable;
            if (seq == null) return Fail("m_Proficiencies unavailable");

            foreach (object pv in seq)
            {
                if (pv == null) continue;
                object prof = SafeField(pv, "m_Prof");
                int? id = ToInt(prof);
                if (id.HasValue && id.Value == profId.Value)
                {
                    object button = SafeField(pv, "m_Button");
                    Reflect.Invoke(bsb, "AttackProficiency", button);
                    return Ok(Result("profId", profId.Value));
                }
            }
            return Fail("no proficiency button with profId " + profId.Value);
        }

        private static object SetFocus(IDictionary<string, object> args)
        {
            int? n = GetInt(args, "n");
            if (!n.HasValue) return Fail("set_focus needs {n:int}");
            object cow = CurrentCow();
            object stats = SafeField(cow, "m_CharacterStats");
            if (stats == null) return Fail("no character stats");
            // Fail closed: if MaxFocus (CharacterStats.MaxFocus, a clamped int property) cannot be read,
            // do not guess a bound -- a wrong cap could overspend focus, so report failure instead.
            int? max = ToInt(SafeProp(stats, "MaxFocus"));
            if (!max.HasValue) return Fail("MaxFocus unreadable");
            int clamped = Math.Max(0, Math.Min(n.Value, max.Value));
            Reflect.SetField(stats, "m_SpentFocus", clamped);
            return Ok(Result("spentFocus", clamped));
        }

        private static object Attack(IDictionary<string, object> args)
        {
            if (!RequireCombat()) return Fail("not in combat");
            object bsb = BattleStanceButtons();
            if (bsb == null) return Fail("battle stance buttons unavailable");
            Reflect.Invoke(bsb, "Attack");
            return Ok(null);
        }

        /// <summary>
        /// Low-level manual fallback for taking a hero turn. RETIRED as the win path in favour of
        /// auto_combat_turn (kb_62f31d88): calling DamageCalculator.StartEngageAttack DIRECTLY only plays the
        /// attack sequence and does NOT advance the dummy turn-commit FSM, so the turn never ends. The real
        /// commit must route through SlotControl.ComputeAttackSlotResults(cow,true), which drives the FSM and
        /// ends the turn. This method therefore (1) optionally selects a target, then (2) commits via the
        /// player-slots path with the supplied cheat level (default KillSingle for a guaranteed turn-ending
        /// kill), mirroring uiBattleStanceButtons.CheatKillSingle/CheatKillAll. The old 3-arg StartEngageAttack
        /// call (which would not even compile against the verified 7-arg signature) is gone.
        ///
        /// args: {targetFid?, cheat? ("KillSingle"|"KillAll"|"None"; default KillSingle)}.
        /// </summary>
        private static object ResolveTurn(IDictionary<string, object> args)
        {
            // DEPRECATED single-shot: superseded by force_win / win_combat (the looped CombatDriver) and the
            // gated single-turn combat_turn. Kept working, but now STRICTLY GATED so it can no longer commit in a
            // not-ready state (the old in-dungeon stuck bug: firing CheatKillAll before "Wait For Stance"). On any
            // gate miss it returns ok:true {acted:false,waiting} for the harness to re-poll, exactly like
            // combat_turn, and its default is the clean per-enemy KillSingle (not the firestorm KillAll).
            object mc = StaticInstance("EncounterSessionMC");
            object es = StaticInstance("EncounterSession");
            if (!CombatActive(mc, es)) return Ok(WaitingResult("not_in_combat"));

            // GATE: player's turn.
            object active = ActiveTurnFid(mc);
            if (active == null) return Ok(WaitingResult("turn_unresolved"));
            object isPlayer = SafeInvoke(active, "IsPlayer");
            if (!(isPlayer is bool) || !(bool)isPlayer) return Ok(WaitingResult("enemy_turn"));

            // GATE: stance UI ready (THE FIX -- never commit before "Wait For Stance").
            if (!HeroTurnReady()) return Ok(WaitingResult("stance_not_ready"));

            object bsb = BattleStanceButtons();
            if (bsb == null) return Fail("at resolve_turn.ui: battle stance buttons unavailable");

            // (1) Optional explicit target; else pick the first live enemy so the commit has a victim.
            object targetFid = ResolveFid(args, "targetFid");
            if (targetFid == null) targetFid = FirstLiveEnemyFid(es);
            if (targetFid == null) return Ok(WaitingResult("no_live_enemy"));
            SafeInvokeArgs(bsb, "SelectEnemyDummy", new[] { targetFid.GetType() }, new object[] { targetFid });

            // (2) Commit through the verified turn-ending paths on uiBattleStanceButtons. These set
            // m_PlayerSlots.m_CheatAttack then call ComputeAttackSlotResults(CombatCow,true), advancing the
            // dummy FSM so the turn ENDS. KillAll is still accepted for back-compat but the default is the
            // clean per-enemy KillSingle (KillAll's firestorm AoE per-enemy death RPCs were the in-dungeon
            // stuck risk; prefer force_win which loops KillSingle).
            string cheat = GetString(args, "cheat") ?? "KillSingle";
            string committed;
            if (cheat == "KillAll") { Reflect.Invoke(bsb, "CheatKillAll"); committed = "KillAll"; }
            else if (cheat == "None") { Reflect.Invoke(bsb, "Attack"); committed = "Attack"; }
            else { Reflect.Invoke(bsb, "CheatKillSingle"); committed = "KillSingle"; }

            Plugin.Log.LogInfo("[agent] combat: resolve_turn committed " + committed + " on enemy "
                               + FidLabel(targetFid) + " (gated)");
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["committed"] = committed;
            d["target"] = FidDict(targetFid);
            return Ok(d);
        }

        private static object EndTurn(IDictionary<string, object> args)
        {
            if (RequireCombat())
            {
                object bsb = BattleStanceButtons();
                if (bsb != null)
                {
                    object r = SafeInvoke(bsb, "DoSkipCombatTurn");
                    return Ok(Result("ended", "combat"));
                }
                return Fail("battle stance buttons unavailable");
            }

            object menu = StaticInstance("uiEncounterMenu");
            if (menu != null)
            {
                Reflect.Invoke(menu, "LeaveOrEndTurn");
                return Ok(Result("ended", "overworld"));
            }
            return Fail("no end-turn surface available");
        }

        private static object SelectChoice(IDictionary<string, object> args)
        {
            int? index = GetInt(args, "index");
            if (!index.HasValue) return Fail("select_choice needs {index:int}");

            object ui = StaticInstance("FTKUI");
            if (ui != null)
            {
                object gm = Reflect.GetField(ui, "m_GlobalMessage");
                if (gm != null)
                {
                    object choicePanel = Reflect.GetField(gm, "m_ChoiceButtonPanel");
                    if (GameObjectActive(choicePanel))
                    {
                        if (index.Value == 0) Reflect.Invoke(gm, "UseYesButton");
                        else Reflect.Invoke(gm, "UseNoButton");
                        return Ok(Result("choice", index.Value));
                    }
                }

                object rewardMenu = Reflect.GetField(ui, "m_ChooseRewardMenu");
                if (rewardMenu != null)
                {
                    object allButtons = Reflect.GetField(rewardMenu, "m_AllButtons");
                    IList list = allButtons as IList;
                    if (list != null && index.Value >= 0 && index.Value < list.Count)
                    {
                        object btn = list[index.Value];
                        if (btn != null) { Reflect.Invoke(btn, "UseButton"); return Ok(Result("reward", index.Value)); }
                    }
                }
            }

            object sys = StaticInstance("uiSystemDialog");
            if (sys != null && GameObjectActiveInHierarchy(sys))
            {
                if (index.Value == 0) Reflect.Invoke(sys, "OnYes");
                else Reflect.Invoke(sys, "OnNo");
                return Ok(Result("system", index.Value));
            }

            return Fail("no open choice surface for index " + index.Value);
        }

        private static object Advance(IDictionary<string, object> args)
        {
            object ui = StaticInstance("FTKUI");
            if (ui != null)
            {
                // In-world story popups (m_PortraitMessage, a StoryQuestMessage) and quest-confirm prompts
                // (m_QuestConfirm) advance via UseOkayButton() just like the global message; probe them first
                // so advance walks an in-world page when no global message is up. Each surface only fires when
                // IsMessagePanelOpen()==true, so this is a clean no-op when the surface is closed.
                if (OkayMessageSurface(ui, "m_PortraitMessage")) return Ok(Result("advanced", "portrait"));
                if (OkayMessageSurface(ui, "m_QuestConfirm")) return Ok(Result("advanced", "questConfirm"));

                object gm = Reflect.GetField(ui, "m_GlobalMessage");
                if (gm != null)
                {
                    object msgPanel = Reflect.GetField(gm, "m_MessagePanel");
                    if (GameObjectActive(msgPanel))
                    {
                        Reflect.Invoke(gm, "UseOkayButton");
                        return Ok(Result("advanced", "okay"));
                    }
                    // Even if the panel-active probe missed, try the okay button defensively.
                    object res = SafeInvoke(gm, "UseOkayButton");
                    return Ok(Result("advanced", "okay-best-effort"));
                }
            }
            return Fail("no continue/okay surface available");
        }

        /// <summary>
        /// Dismiss the front-most game message popup by walking the three message surfaces in priority order:
        ///   (1) m_PortraitMessage  -- the in-world story/NPC popup ("... to Continue").
        ///   (2) m_QuestConfirm     -- quest accept/turn-in confirmation.
        ///   (3) m_GlobalMessage    -- the generic global message HUD.
        /// For each: if the FTKUI field is non-null and IsMessagePanelOpen()==true, call UseOkayButton() and
        /// return which surface was dismissed. UseOkayButton() advances one page; for a multi-page story popup
        /// the agent re-calls until MessageCoordinator.CurrentMessageType() reads None. Fully defensive: a null
        /// field or unreadable member is skipped, never thrown.
        /// </summary>
        private static object DismissMessage(IDictionary<string, object> args)
        {
            object ui = StaticInstance("FTKUI");
            if (ui == null) return Fail("FTKUI.Instance unavailable");

            if (OkayMessageSurface(ui, "m_PortraitMessage")) return Ok(Result("dismissed", "portrait"));
            if (OkayMessageSurface(ui, "m_QuestConfirm")) return Ok(Result("dismissed", "questConfirm"));
            if (OkayMessageSurface(ui, "m_GlobalMessage")) return Ok(Result("dismissed", "global"));

            return Fail("no message open");
        }

        // Probe one FTKUI message surface (a uiPortraitMessageHud / uiQuestConfirmHud / uiGlobalMessageHUD,
        // each exposing bool IsMessagePanelOpen() and void UseOkayButton()). If the field is non-null and the
        // panel reads open, advance it once and return true; otherwise return false. Never throws.
        private static bool OkayMessageSurface(object ui, string fieldName)
        {
            object hud = SafeField(ui, fieldName);
            if (hud == null) return false;
            object open = SafeInvoke(hud, "IsMessagePanelOpen");
            if (!(open is bool) || !(bool)open) return false;
            SafeInvoke(hud, "UseOkayButton");
            return true;
        }

        // ============================================================ dungeon =========================

        /// <summary>
        /// ENTER the realm's main dungeon (overworld -> inside). Locates the dungeon POI (GetSpecificDungeon,
        /// fallback GetPOIList(Dungeon)), gates cleared/locked/in-world/cow, SnapTo's the party onto its hex,
        /// then reflect-invokes OnLoadParty(cow) (the engage-equivalent enter). All plumbing lives in
        /// <see cref="DungeonOps.EnterDungeon"/>. args: optional {dungeonId:"FloodedCrypt"}.
        /// </summary>
        private static object EnterDungeon(IDictionary<string, object> args)
        {
            string key = GetString(args, "dungeonId");
            return DungeonOps.EnterDungeon(key);
        }

        /// <summary>
        /// ADVANCE one dungeon room (single-step primitive). Stair -> descend; else fire the room encounter.
        /// In-dungeon combat rooms are then won with the existing resolve_turn{cheat:"KillAll"} / auto_combat_turn.
        /// </summary>
        private static object AdvanceRoom(IDictionary<string, object> args)
        {
            return DungeonOps.AdvanceRoomAction();
        }

        /// <summary>
        /// FORCE-CLEAR the entered dungeon if it is already IsDungeonCleared() (SetClear + RPCAllButSelf). Optional;
        /// removes the dependency on SetClear auto-firing. No-op {cleared:false} when not yet cleared.
        /// </summary>
        private static object ForceClear(IDictionary<string, object> args)
        {
            return DungeonOps.ForceClearAction();
        }

        /// <summary>
        /// HIGH-LEVEL ORCHESTRATOR. Arms <see cref="DungeonDriver"/> to traverse the dungeon, win every room with
        /// the proven combat path, and force-clear at the end (= victory via the bound DungeonQuestDef). Returns
        /// immediately; the agent polls /state.dungeon until cleared. args: optional {dungeonId, enter:true}.
        /// Idempotent: a running driver is not re-armed; an already-cleared dungeon returns {status:"cleared"}.
        /// </summary>
        private static object ClearDungeon(IDictionary<string, object> args)
        {
            string key = GetString(args, "dungeonId");
            bool enter = GetBool(args, "enter") ?? false;

            // Already running -> report status, do not re-arm.
            if (DungeonDriver.IsRunning)
                return Ok(DungeonStatus("clearing"));

            // Already cleared (entered dungeon is deactivated / IsDungeonCleared) -> nothing to do.
            object d = DungeonOps.EnteredDungeon();
            if (d != null)
            {
                object cleared = DungeonOps.SafeInvoke(d, "IsDungeonCleared");
                object deact = DungeonOps.SafeField(d, "m_Deactivated");
                if ((cleared is bool && (bool)cleared) || (deact is bool && (bool)deact))
                    return Ok(DungeonStatus("cleared"));
            }

            // Not in a dungeon and not asked to enter: explicit, actionable error.
            if (d == null && !DungeonOps.InDungeon() && !enter)
                return Fail("at clear_dungeon.enter: not in dungeon (pass {enter:true} or call enter_dungeon first)");

            if (!DungeonDriver.Arm(key, enter))
                return Fail("at clear_dungeon.arm: could not arm driver (already running or no host)");
            return Ok(DungeonStatus("clearing"));
        }

        private static Dictionary<string, object> DungeonStatus(string status)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["status"] = status;
            return d;
        }

        private static bool? GetBool(IDictionary<string, object> args, string key)
        {
            if (args == null) return null;
            object raw;
            if (!args.TryGetValue(key, out raw)) return null;
            if (raw is bool) return (bool)raw;
            return null;
        }

        // ------------------------------------------------------------- menu actions ---------------------

        /// <summary>
        /// Dismiss the launch startup modal. The live "For the King / Understood" panel can be backed by any of
        /// three surfaces, so we cover all three, system dialog first:
        ///   (1) uiSystemDialog.Instance, if its m_DialogRoot (or its own GameObject) is active -> OnYes().
        ///   (2) uiStartGame.Instance.m_PrepareToDie (a uiScreen), if active in hierarchy ->
        ///       uiStartGame.OnPrepareToDie(). This is the actual startup "are you ready" modal on a fresh
        ///       launch; the older dismiss missed it (it is neither uiSystemDialog nor m_BetaDisclamer).
        ///   (3) uiStartGame.Instance.m_BetaDisclamer (a uiScreen; note the game's "Disclamer" misspelling),
        ///       if active in hierarchy -> uiStartGame.OnBetaDisclaimer() (this also fires the FSM
        ///       "NewGameLoadScene" event, so it doubles as a menu-advance).
        /// Re-calling with nothing open is a clean {ok:false}, never a throw. Verified members:
        /// uiSystemDialog.OnYes()/m_DialogRoot, uiStartGame.m_PrepareToDie/OnPrepareToDie(),
        /// uiStartGame.m_BetaDisclamer/OnBetaDisclaimer().
        /// </summary>
        private static object DismissDialog(IDictionary<string, object> args)
        {
            // (1) system dialog
            object sys = StaticInstance("uiSystemDialog");
            if (sys != null)
            {
                object dialogRoot = SafeField(sys, "m_DialogRoot");
                bool open = GameObjectActiveInHierarchy(sys) || GameObjectActiveInHierarchy(dialogRoot);
                if (open)
                {
                    SafeInvoke(sys, "OnYes");
                    return Ok(Result("dismissed", "system"));
                }
            }

            // (2) "prepare to die" startup modal on uiStartGame (the real fresh-launch confirm panel).
            object usg = StaticInstance("uiStartGame");
            if (usg != null)
            {
                object prepare = SafeField(usg, "m_PrepareToDie");
                if (prepare != null && GameObjectActiveInHierarchy(prepare))
                {
                    SafeInvoke(usg, "OnPrepareToDie");
                    return Ok(Result("dismissed", "prepareToDie"));
                }
            }

            // (3) beta disclaimer ("Understood") on uiStartGame
            if (usg != null)
            {
                object beta = SafeField(usg, "m_BetaDisclamer");
                if (beta != null && GameObjectActiveInHierarchy(beta))
                {
                    SafeInvoke(usg, "OnBetaDisclaimer");
                    return Ok(Result("dismissed", "beta"));
                }
            }

            return Fail("no menu dialog open");
        }

        /// <summary>
        /// Confirm the framework's adventures (e.g. "HollowMire") are injected and selectable. As a deliberate
        /// ACTION it may force the adventure cache to build: GetPreviewNamesForced() runs Initialize() (idempotent
        /// + synchronous) then re-injects our previews, so the returned list always reflects the live cache.
        /// </summary>
        private static object ListAdventures(IDictionary<string, object> args)
        {
            List<string> names = AdventureCache.GetPreviewNamesForced();
            List<object> boxed = new List<object>();
            foreach (string n in names) boxed.Add(n);
            return Ok(Result("adventures", boxed));
        }

        /// <summary>
        /// One-shot title-screen -> in-world orchestrator for SOLO play in a custom adventure. The WHOLE start
        /// sequence is FSM/Photon/coroutine driven (see <see cref="StartRunDriver"/>), so this synchronous entry
        /// only (1) short-circuits if already in-world, (2) does not re-arm a running driver, (3) validates the
        /// one precondition we can check up front (the adventure preview is injected), then (4) ARMS the driver
        /// and returns {phase:'starting'}; the agent polls /state until phase=='overworld'.
        ///
        /// The driver, not this method, dismisses the startup modal, WAITS for the FSM to settle on the start
        /// page, drives MainScreen.OnNewGame() to reach the GameConfig FSM state, configures SOLO, and only THEN
        /// calls CreateOfflineRoom. Doing the configure+room-create here (before the FSM had settled) is exactly
        /// what dropped the room-join callback before, so it is intentionally moved into the waited coroutine.
        /// </summary>
        private static object StartRun(IDictionary<string, object> args)
        {
            string adventureKey = GetString(args, "adventure") ?? "HollowMire";

            // Idempotency: already in-world.
            object usg = StaticInstance("uiStartGame");
            if (usg != null && ToBool(SafeField(usg, "m_GameStarted")))
                return Ok(StartResult(true, "overworld"));

            // If a continuation is already running, do not re-arm; report current status.
            if (StartRunDriver.IsRunning)
                return Ok(StartResult(false, "starting"));

            // Precondition: force-build the cache and resolve the preview. GetPreviewSafe runs Initialize()
            // (which injects our previews via the framework's Postfix), so a null here means genuinely
            // not-injected and the run cannot proceed.
            object preview = AdventureCache.GetPreviewSafe(adventureKey);
            if (preview == null)
                return Fail("start_run failed at get-preview: adventure '" + adventureKey + "' not injected");

            // Arm the full waited coroutine (dismiss -> settle -> NewGame -> GameConfig -> configure -> room ->
            // map-wait -> ready -> EnterFahrul -> intro). It owns all FSM/Photon sequencing.
            StartRunDriver.Arm(adventureKey);
            return Ok(StartResult(false, "starting"));
        }

        // ----------------------------------------------------------- start-run helpers ------------------

        private static Dictionary<string, object> StartResult(bool inSession, string phase)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["inSession"] = inSession;
            d["phase"] = phase;
            return d;
        }

        private static bool ToBool(object o) { return o is bool && (bool)o; }

        // ----------------------------------------------------------------- helpers ----------------------

        private static bool RequireCombat()
        {
            object es = StaticInstance("EncounterSession");
            if (es == null) return false;
            object ic = SafeField(es, "m_IsInCombat");
            return ic is bool && (bool)ic;
        }

        // Resolve FTKHex.GetHexLand(int,int) explicitly. Only this method is ambiguous (two overloads:
        // (int,int) and (HexLandID)); SnapTo/MoveTo are single-overload and use the plain Reflect.Invoke.
        private static object GetHexLandByIndex(object hexInstance, int big, int small)
        {
            return SafeInvokeArgs(hexInstance, "GetHexLand",
                new[] { typeof(int), typeof(int) }, new object[] { big, small });
        }

        private static object BattleStanceButtons()
        {
            object ui = StaticInstance("FTKUI");
            if (ui == null) return null;
            return Reflect.GetField(ui, "m_BattleStanceButtons");
        }

        private static object CurrentCow()
        {
            // GameLogic has NO GetCurrentCharacterOverworld; the verified path (GameLogic.GetCurrentCOW,
            // GameLogic.cs:390-392) resolves the acting character via the hub:
            //   FTKHub.Instance.GetCharacterOverworldByFID(m_CurrentPlayer)  // m_CurrentPlayer is an FTKPlayerID
            object gl = StaticInstance("GameLogic");
            if (gl == null) return null;
            object hub = StaticInstance("FTKHub");
            if (hub == null) return null;
            object fid = SafeField(gl, "m_CurrentPlayer");
            if (fid == null) return null;
            return SafeInvokeArgs(hub, "GetCharacterOverworldByFID", new[] { fid.GetType() }, new[] { fid });
        }

        private static object ResolveFid(IDictionary<string, object> args, string key)
        {
            if (args == null) return null;
            object raw;
            if (!args.TryGetValue(key, out raw)) return null;
            IDictionary<string, object> fidMap = raw as IDictionary<string, object>;
            if (fidMap == null) return null;
            int? ti = GetInt(fidMap, "turnIndex");
            int? pid = GetInt(fidMap, "photonId");
            if (!ti.HasValue) return null;

            Type fidType = AccessTools.TypeByName("FTKPlayerID");
            if (fidType == null) return null;
            try
            {
                object fid = Activator.CreateInstance(fidType);
                Reflect.SetField(fid, "m_TurnIndex", ti.Value);
                Reflect.SetField(fid, "m_PhotonID", pid ?? 0);
                return fid;
            }
            catch { return null; }
        }

        private static bool GetTile(IDictionary<string, object> args, out int big, out int small)
        {
            big = 0; small = 0;
            int? b = GetInt(args, "big");
            int? s = GetInt(args, "small");
            if (!b.HasValue || !s.HasValue) return false;
            big = b.Value; small = s.Value;
            return true;
        }

        private static int? GetInt(IDictionary<string, object> args, string key)
        {
            if (args == null) return null;
            object raw;
            if (!args.TryGetValue(key, out raw)) return null;
            return ToInt(raw);
        }

        private static string GetString(IDictionary<string, object> args, string key)
        {
            if (args == null) return null;
            object raw;
            if (!args.TryGetValue(key, out raw)) return null;
            return raw as string;
        }

        private static int? ToInt(object o)
        {
            if (o == null) return null;
            try
            {
                if (o is int) return (int)o;
                if (o is long) return (int)(long)o;
                if (o is short || o is byte) return Convert.ToInt32(o);
                if (o is Enum) return Convert.ToInt32(o);
                if (o is double) return (int)(double)o;
            }
            catch { }
            return null;
        }

        private static object StaticInstance(string typeName)
        {
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return null;
            PropertyInfo pi = t.GetProperty("Instance", Reflect.All);
            if (pi != null) return pi.GetValue(null, null);
            FieldInfo fi = t.GetField("Instance", Reflect.All);
            if (fi != null) return fi.GetValue(null);
            return null;
        }

        private static object SafeField(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.GetField(obj, name); } catch { return null; }
        }

        private static object SafeProp(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                for (Type cur = obj.GetType(); cur != null; cur = cur.BaseType)
                {
                    PropertyInfo pi = cur.GetProperty(name, Reflect.All | BindingFlags.DeclaredOnly);
                    if (pi != null && pi.CanRead) return pi.GetValue(obj, null);
                }
            }
            catch { }
            return null;
        }

        private static object SafeInvoke(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.Invoke(obj, name); } catch { return null; }
        }

        // Overload-aware, null-on-miss invoke. Use for methods with multiple overloads (e.g.
        // FTKHex.GetHexLand(int,int) vs (HexLandID)) where the plain name lookup throws AmbiguousMatchException.
        private static object SafeInvokeArgs(object obj, string name, Type[] sig, object[] args)
        {
            if (obj == null) return null;
            try { return Reflect.InvokeArgs(obj, name, sig, args); } catch { return null; }
        }

        private static bool GameObjectActive(object component)
        {
            try
            {
                object go = SafeProp(component, "gameObject");
                if (go == null) go = component;
                object active = SafeInvoke(go, "get_activeSelf");
                return active is bool && (bool)active;
            }
            catch { return false; }
        }

        private static bool GameObjectActiveInHierarchy(object component)
        {
            try
            {
                object go = SafeProp(component, "gameObject");
                if (go == null) go = component;
                object active = SafeInvoke(go, "get_activeInHierarchy");
                return active is bool && (bool)active;
            }
            catch { return false; }
        }

        private static Dictionary<string, object> Ok(object result)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = true;
            d["error"] = null;
            d["result"] = result;
            return d;
        }

        private static Dictionary<string, object> Fail(string error)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["error"] = error;
            d["result"] = null;
            return d;
        }

        private static Dictionary<string, object> Result(string key, object value)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d[key] = value;
            return d;
        }

        private static Dictionary<string, object> Tile(string label, int big, int small)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["action"] = label;
            d["big"] = big;
            d["small"] = small;
            return d;
        }
    }
}
