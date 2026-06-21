using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Shared, fully defensive game-access primitives for dungeon find / enter / traverse / clear, plus the
    /// in-dungeon combat-win reuse. Both <see cref="ActionExecutor"/> (the single-step actions) and
    /// <see cref="DungeonDriver"/> (the clear coroutine) call these so the reflection lives in ONE place.
    ///
    /// Everything runs on the Unity main thread (the bridge marshals it) and NEVER throws: a missing/renamed
    /// member degrades to null/false. Public entry points return the same {ok,error,result} dictionary shape as
    /// ActionExecutor and tag failures "at enter_dungeon.&lt;step&gt;" / "at advance_room.&lt;step&gt;".
    ///
    /// Verified game members (ilspycmd, Assembly-CSharp, Jun 2026):
    ///   FTKHex.GetSpecificDungeon(FTK_dungeonEncounter.ID, FTK_realm.ID=None, int=-1) -> MiniHexDungeon.
    ///   FTKHex.GetPOIList(MiniHexInfo.MiniHexType) -> List&lt;MiniHexInfo&gt;. MiniHexInfo.MiniHexType.Dungeon.
    ///   MiniHexDungeon : MiniHexInfo. m_ID (FTK_dungeonEncounter.ID), m_HexLand, m_Level, m_RoomIndex.
    ///   MiniHexDungeon.OnLoadParty(CharacterOverworld) (void). Encounter(FTKPlayerID, ContinueFSM=null) (void).
    ///   MiniHexDungeon.SetClear() (void; DeactivateHex -> MiniHexInfo.m_Deactivated). RPCAllButSelf(string,object[]).
    ///   MiniHexDungeon.IsDungeonCleared()/GetCurrentRoom()/GetLevelCount()/GetRoomCount(int)/IsInLastRoom()/IsAtLastLevel().
    ///   MiniHexInfo.m_Deactivated/m_Locked/m_Hidden (public bool).
    ///   CharacterOverworld.DungeonEncounter() (void), DungeonStairDecision(bool) [PunRPC], IsInDungeon()->bool, SnapTo(HexLand,bool,bool).
    ///   FTKHub.AnyPlayersInDungeon()->bool. GameFlow.m_DungeonEntered (MiniHexDungeon), m_DungeonEnterCow (CharacterOverworld).
    /// </summary>
    internal static class DungeonOps
    {
        // ============================================================ enter ============================

        /// <summary>
        /// ENTER (overworld -> inside the dungeon). Locates the dungeon POI, gates cleared/locked/turn/in-world,
        /// SnapTo's the party onto its hex, then reflect-invokes OnLoadParty(cow) (the engage-equivalent enter).
        /// Returns Ok{entered,dungeonId,where} or Fail("at enter_dungeon.&lt;step&gt;"). Single main-thread call.
        /// </summary>
        public static object EnterDungeon(string dungeonKey)
        {
            string key = string.IsNullOrEmpty(dungeonKey) ? "FloodedCrypt" : dungeonKey;

            // STEP 2 already-in gate (idempotency).
            if (InDungeon() || EnteredDungeon() != null)
                return Ok(EnteredResult(true, "already-in-dungeon", null));

            // STEP 1 locate the dungeon POI.
            object d = LocateDungeon(key);
            if (d == null)
                return Fail("at enter_dungeon.locate: " + key + " POI not on map");

            // STEP 3 clear / lock gate.
            if (ToBool(SafeField(d, "m_Deactivated")))
                return Fail("at enter_dungeon.cleared: dungeon already deactivated");
            if (ToBool(SafeField(d, "m_Locked")))
                return Fail("at enter_dungeon.locked: dungeon locked");

            // STEP 4 in-world + cow gate.
            object usg = StaticInstance("uiStartGame");
            if (usg == null || !ToBool(SafeField(usg, "m_GameStarted")))
                return Fail("at enter_dungeon.inWorld: not in-world");
            object cow = CurrentCow();
            if (cow == null)
                return Fail("at enter_dungeon.cow: no current character");

            // STEP 5 snap onto the dungeon hex (deterministic placement, reused from engage). m_HexLand is a
            // public field on MiniHexInfo; SnapTo(HexLand, _justVisual:false, _isOnGround:true).
            object hex = SafeField(d, "m_HexLand");
            if (hex == null)
                return Fail("at enter_dungeon.hex: dungeon has no m_HexLand");
            SafeInvokeArgs(cow, "SnapTo",
                new[] { hex.GetType(), typeof(bool), typeof(bool) }, new object[] { hex, false, true });

            // STEP 6 engage-equivalent enter: the single-overload OnLoadParty(CharacterOverworld). This is what
            // uiLocationMenuEntry invokes (m_Function="OnLoadParty"): PartyEnterHex, EndTurn, movementFinish,
            // and generates level0/room0. Returns void; null is expected, not a failure.
            SafeInvokeArgs(d, "OnLoadParty", new[] { cow.GetType() }, new object[] { cow });
            Plugin.Log.LogInfo("[agent] dungeon: OnLoadParty(" + key + ") kicked.");

            // STEP 7 confirm gate. inDungeon may flip on a later FSM pump (same async shape as engage's combat
            // start), so a not-yet-true read is Ok{entered:false}, NOT a failure.
            bool entered = InDungeon() || EnteredDungeon() != null;
            if (entered)
                return Ok(EnteredResult(true, "entered", key));
            return Ok(EnteredResult(false, "kicked; poll /state.dungeon.inDungeon", key));
        }

        // Locate the dungeon MiniHexDungeon by its FTK_dungeonEncounter.ID name. Primary: GetSpecificDungeon
        // (resolve the enum member via Enum.Parse so a rename surfaces cleanly, never a literal). Fallback:
        // scan GetPOIList(Dungeon) for a MiniHexDungeon whose m_ID name matches.
        public static object LocateDungeon(string key)
        {
            object hexInst = StaticInstance("FTKHex");
            if (hexInst == null) return null;

            Type idType = AccessTools.TypeByName("FTK_dungeonEncounter+ID");
            if (idType == null) idType = AccessTools.TypeByName("FTK_dungeonEncounter/ID");
            object idVal = null;
            if (idType != null && idType.IsEnum)
            {
                try { if (Enum.IsDefined(idType, key)) idVal = Enum.Parse(idType, key); }
                catch { idVal = null; }
            }

            // Primary: GetSpecificDungeon(ID, FTK_realm.ID=None, int=-1). Resolve via the 1-arg-effective overload
            // by matching the full (ID, realm, int) signature with default-ish args.
            if (idVal != null)
            {
                Type realmIdType = AccessTools.TypeByName("FTK_realm+ID");
                if (realmIdType == null) realmIdType = AccessTools.TypeByName("FTK_realm/ID");
                object realmNone = null;
                if (realmIdType != null && realmIdType.IsEnum)
                {
                    try { realmNone = Enum.IsDefined(realmIdType, "None") ? Enum.Parse(realmIdType, "None") : Enum.ToObject(realmIdType, 0); }
                    catch { realmNone = null; }
                }
                if (realmIdType != null && realmNone != null)
                {
                    object found = SafeInvokeArgs(hexInst, "GetSpecificDungeon",
                        new[] { idType, realmIdType, typeof(int) }, new object[] { idVal, realmNone, -1 });
                    if (found != null) return found;
                }
            }

            // Fallback: scan GetPOIList(MiniHexInfo.MiniHexType.Dungeon) for the matching m_ID name.
            Type mtType = AccessTools.TypeByName("MiniHexInfo+MiniHexType");
            if (mtType == null) mtType = AccessTools.TypeByName("MiniHexInfo/MiniHexType");
            if (mtType == null || !mtType.IsEnum) return null;
            object dungeonType;
            try { dungeonType = Enum.Parse(mtType, "Dungeon"); }
            catch { return null; }
            object list = SafeInvokeArgs(hexInst, "GetPOIList", new Type[] { mtType }, new object[] { dungeonType });
            IEnumerable en = list as IEnumerable;
            if (en == null) return null;
            foreach (object poi in en)
            {
                if (poi == null) continue;
                object idObj = SafeField(poi, "m_ID");
                if (idObj != null && string.Equals(idObj.ToString(), key, StringComparison.Ordinal))
                    return poi;
            }
            return null;
        }

        // ============================================================ advance ==========================

        /// <summary>
        /// ADVANCE one room (single-step primitive). Reads the entered dungeon's current room type; Stair ->
        /// cow.DungeonStairDecision(true) (descend); else <see cref="AdvanceRoom"/>, which now calls the entered
        /// dungeon's Encounter(FTKPlayerID, ContinueFSM=null) DIRECTLY (cow.DungeonEncounter casts m_HexLand.m_POI,
        /// which is not the dungeon after a bridge enter, so it threw). Returns Ok{roomType,advanced,startedCombat}
        /// or Fail("at advance_room.&lt;step&gt;").
        /// </summary>
        public static object AdvanceRoomAction()
        {
            object d = EnteredDungeon();
            if (d == null) return Fail("at advance_room.inDungeon: not in a dungeon");

            object room = SafeInvoke(d, "GetCurrentRoom");
            string roomType = room != null ? (SafeField(room, "m_Type") != null ? SafeField(room, "m_Type").ToString() : null) : null;

            bool ok;
            if (roomType == "Stair")
            {
                ok = StairDescend();
                if (!ok) return Fail("at advance_room.stair: DungeonStairDecision unavailable");
            }
            else
            {
                ok = AdvanceRoom();
                if (!ok) return Fail("at advance_room.encounter: DungeonEncounter unavailable");
            }

            Plugin.Log.LogInfo("[agent] dungeon: advance_room type=" + (roomType ?? "?"));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["roomType"] = roomType;
            r["advanced"] = true;
            r["startedCombat"] = CombatActive();
            return Ok(r);
        }

        // Fire the current room's encounter by calling the ENTERED dungeon's Encounter(FTKPlayerID, ContinueFSM)
        // DIRECTLY. We do NOT route through cow.DungeonEncounter(): that decompiles to a cast of m_HexLand.m_POI to
        // MiniHexDungeon, which is NOT the dungeon after a bridge enter (OnLoadParty + SnapTo), so the cast throws.
        // Calling Encounter(fid, null) on the entered MiniHexDungeon is the real per-room start: it copies the
        // current room's RoomInfo into m_EncounterType/m_EncounterObjects, then (with _cfsm == null) initiates an
        // EncounterLocation.Dungeon session and wires the "encounterFinish" continuation onto the cow's TurnEngage
        // FSM, so the round loop starts and post-combat room flow happens normally. Returns false on any miss.
        public static bool AdvanceRoom()
        {
            try
            {
                object d = EnteredDungeon();
                if (d == null) { Plugin.Log.LogWarning("[agent] dungeon advance: no entered dungeon"); return false; }
                object cow = DungeonEnterCowOrCurrent();
                if (cow == null) { Plugin.Log.LogWarning("[agent] dungeon advance: no cow"); return false; }
                object fid = SafeField(cow, "m_FTKPlayerID");
                if (fid == null) { Plugin.Log.LogWarning("[agent] dungeon advance: no m_FTKPlayerID"); return false; }
                // ContinueFSM is required to bind the (FTKPlayerID, ContinueFSM) overload; passing null for the arg
                // is identical to the single-arg Encounter(fid). Without the type we cannot select the overload.
                Type cfsmType = AccessTools.TypeByName("ContinueFSM");
                if (cfsmType == null) { Plugin.Log.LogWarning("[agent] dungeon advance: ContinueFSM type not found"); return false; }
                SafeInvokeArgs(d, "Encounter",
                    new Type[] { fid.GetType(), cfsmType }, new object[] { fid, null });
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] dungeon advance: " + e.Message); return false; }
        }

        // Descend a stair: cow.DungeonStairDecision(true) -> m_DungeonFlow "fadeTransition" -> ClearedRoom.
        public static bool StairDescend()
        {
            object cow = DungeonEnterCowOrCurrent();
            if (cow == null) return false;
            MethodInfo m = cow.GetType().GetMethod("DungeonStairDecision", Reflect.All, null, new[] { typeof(bool) }, null);
            if (m == null) m = FindMethod(cow.GetType(), "DungeonStairDecision");
            if (m == null) return false;
            try { m.Invoke(cow, new object[] { true }); return true; }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] dungeon stair: " + e.Message); return false; }
        }

        // ============================================================ encounter ========================

        /// <summary>
        /// START the CURRENT room's encounter DIRECTLY (no Stair branching), so the harness can trigger a fight
        /// without relying on AdvanceRoomAction's room-type dispatch. Reads the current room type for the result,
        /// then fires <see cref="AdvanceRoom"/> (the entered dungeon's Encounter(FTKPlayerID, ContinueFSM=null)).
        /// Combat starts after a short camera transition; the harness polls /state.combat until heroTurnReady.
        /// Returns Ok{roomType,level,roomIndex,startedCombat,note} or Fail("at dungeon_encounter.&lt;step&gt;").
        /// </summary>
        public static object DungeonEncounterAction()
        {
            object d = EnteredDungeon();
            if (d == null) return Fail("at dungeon_encounter.inDungeon: not in a dungeon");

            object room = SafeInvoke(d, "GetCurrentRoom");
            string roomType = room != null
                ? (SafeField(room, "m_Type") != null ? SafeField(room, "m_Type").ToString() : null)
                : null;

            bool ok = AdvanceRoom();
            if (!ok) return Fail("at dungeon_encounter.trigger: Encounter unavailable");

            Plugin.Log.LogInfo("[agent] dungeon: dungeon_encounter type=" + (roomType ?? "?"));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["roomType"] = roomType;
            r["level"] = SafeField(d, "m_Level");
            r["roomIndex"] = SafeField(d, "m_RoomIndex");
            r["startedCombat"] = CombatActive();
            r["note"] = "combat starts after a short camera transition; poll /state.combat until heroTurnReady";
            return Ok(r);
        }

        /// <summary>
        /// Manually advance the room pointer via MiniHexDungeon.ClearedRoom() (a [PunRPC]; in solo the local client
        /// is master, so a direct local invoke advances it). For use when the post-combat FSM does not auto-advance
        /// under bridge driving. ClearedRoom increments m_RoomIndex and at the room-count boundary rolls to the next
        /// level (m_RoomIndex=0, m_Level++). Returns Ok{levelBefore,roomBefore,level,roomIndex,inLastRoom,cleared}
        /// or Fail("at cleared_room.&lt;step&gt;").
        /// </summary>
        public static object ClearedRoomAction()
        {
            object d = EnteredDungeon();
            if (d == null) return Fail("at cleared_room.inDungeon: not in a dungeon");

            object levelBefore = SafeField(d, "m_Level");
            object roomBefore = SafeField(d, "m_RoomIndex");

            SafeInvoke(d, "ClearedRoom");
            Plugin.Log.LogInfo("[agent] dungeon: cleared_room advanced pointer.");

            object inLast = SafeInvoke(d, "IsInLastRoom");
            object cleared = SafeInvoke(d, "IsDungeonCleared");

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["levelBefore"] = levelBefore;
            r["roomBefore"] = roomBefore;
            r["level"] = SafeField(d, "m_Level");
            r["roomIndex"] = SafeField(d, "m_RoomIndex");
            r["inLastRoom"] = inLast is bool ? (object)(bool)inLast : null;
            r["cleared"] = cleared is bool ? (object)(bool)cleared : null;
            return Ok(r);
        }

        // ============================================================ clear ============================

        /// <summary>
        /// FORCE-CLEAR (optional primitive). If the entered dungeon IsDungeonCleared(), call SetClear() locally
        /// and RPCAllButSelf("SetClear") (solo: local==master). Returns Ok{cleared,deactivated} or, when not yet
        /// cleared, Ok{cleared:false,note}. Removes the dependency on SetClear auto-firing.
        /// </summary>
        public static object ForceClearAction()
        {
            object d = EnteredDungeon();
            if (d == null) return Fail("at force_clear.inDungeon: not in a dungeon");

            object clearedObj = SafeInvoke(d, "IsDungeonCleared");
            bool cleared = clearedObj is bool && (bool)clearedObj;
            if (!cleared)
            {
                Dictionary<string, object> nr = new Dictionary<string, object>();
                nr["cleared"] = false;
                nr["note"] = "not yet cleared";
                return Ok(nr);
            }

            ForceClear(d);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["cleared"] = true;
            r["deactivated"] = ToBool(SafeField(d, "m_Deactivated"));
            return Ok(r);
        }

        // Local SetClear + RPCAllButSelf("SetClear") (solo master). Best-effort; never throws.
        public static void ForceClear(object d)
        {
            if (d == null) return;
            SafeInvoke(d, "SetClear");
            // RPCAllButSelf(string, object[]) -- mirror CheatKillAll's solo master-auth pattern.
            SafeInvokeArgs(d, "RPCAllButSelf",
                new[] { typeof(string), typeof(object[]) }, new object[] { "SetClear", null });
            Plugin.Log.LogInfo("[agent] dungeon: force SetClear() committed.");
        }

        // ============================================================ combat reuse =====================

        // One in-dungeon hero combat turn via the proven overworld win path: select the first live enemy on the
        // battle-stance UI, then CheatKillAll. A clean no-op when it is the enemy's turn or the UI is not ready
        // (we just keep calling each frame). Identical to ActionExecutor.AutoCombatTurn's commit.
        public static void TryWinCombatTurn()
        {
            try
            {
                object mc = StaticInstance("EncounterSessionMC");
                object es = StaticInstance("EncounterSession");
                if (!CombatActiveOn(mc, es)) return;

                // Only act on the hero's turn with the UI ready.
                object active = ActiveTurnFid(mc);
                if (active == null) return;
                object isPlayer = SafeInvoke(active, "IsPlayer");
                if (!(isPlayer is bool) || !(bool)isPlayer) return;
                if (!HeroTurnReady()) return;

                object enemyFid = FirstLiveEnemyFid(es);
                if (enemyFid == null) return; // victory likely; combat will flip off.

                object bsb = BattleStanceButtons();
                if (bsb == null) return;
                SafeInvokeArgs(bsb, "SelectEnemyDummy", new[] { enemyFid.GetType() }, new object[] { enemyFid });
                Reflect.Invoke(bsb, "CheatKillAll");
                Plugin.Log.LogInfo("[agent] dungeon: in-room CheatKillAll committed.");
            }
            catch { /* combat win is best-effort each frame */ }
        }

        // Dismiss the front-most popup (post-fight / reward / non-combat room) by walking the three message
        // surfaces, exactly like ActionExecutor.DismissMessage. Best-effort.
        public static void DismissPopups()
        {
            try
            {
                object ui = StaticInstance("FTKUI");
                if (ui == null) return;
                if (OkayMessageSurface(ui, "m_PortraitMessage")) return;
                if (OkayMessageSurface(ui, "m_QuestConfirm")) return;
                if (OkayMessageSurface(ui, "m_GlobalMessage")) return;
            }
            catch { }
        }

        private static bool OkayMessageSurface(object ui, string fieldName)
        {
            object hud = SafeField(ui, fieldName);
            if (hud == null) return false;
            object open = SafeInvoke(hud, "IsMessagePanelOpen");
            if (!(open is bool) || !(bool)open) return false;
            SafeInvoke(hud, "UseOkayButton");
            return true;
        }

        // ============================================================ questline / victory =============
        // Drive the FTK questline to TRUE-VICTORY. The AUTHORITATIVE active quest is
        // GameEventManager.Instance.GetCurrentQuest() -> QuestLogicBase (it internally resolves
        // GameLogic.Instance.GetQuestByID(GameEventManager.m_CurrentQuestID)); m_CurrentQuestID is 0 when the
        // questline is exhausted. FORCE-COMPLETE one quest = set quest.m_IsForceComplete=true (safe regardless of
        // turn or whether the destination is resolved; do NOT call OnCompleteQuest()/IsCompleteState, which deref
        // GetHexLandDestination().GetPOI() and can NPE). ADVANCE = GameEventManager.GetNextStoryQuest() (no args;
        // advances m_CurrentQuestID to the next quest or 0 at the end). On the last quest the engine fires
        // m_CheckQuestStatusFSM.SendEvent("complete") to run the credit/end-game chain. All reads defensive.

        // quest_info: read the active quest's identity + completion flags. Never throws.
        public static object QuestInfoAction()
        {
            object gem = StaticInstance("GameEventManager");
            if (gem == null) return Fail("at quest_info.gem: GameEventManager.Instance null");
            int? cqid = ToNullableInt(SafeField(gem, "m_CurrentQuestID"));
            object quest = SafeInvoke(gem, "GetCurrentQuest");
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["currentQuestId"] = cqid;
            if (quest == null) { r["quest"] = null; return Ok(r); }
            r["type"] = quest.GetType().Name;
            r["isLast"] = ToNullBool(SafeField(quest, "m_IsLastQuest"));
            r["isCompleted"] = ToNullBool(SafeField(quest, "m_IsCompleted"));
            r["isForceComplete"] = ToNullBool(SafeField(quest, "m_IsForceComplete"));
            object raw = SafeInvoke(quest, "IsRawComplete");
            r["rawComplete"] = raw is bool ? (object)(bool)raw : null;
            return Ok(r);
        }

        // quest_advance: force-complete the current quest, then GetNextStoryQuest(). Returns the new current quest
        // so the caller can loop until isLast. Fail only when there is no current quest.
        public static object QuestAdvanceAction()
        {
            object gem = StaticInstance("GameEventManager");
            if (gem == null) return Fail("at quest_advance.gem: GameEventManager.Instance null");
            object quest = SafeInvoke(gem, "GetCurrentQuest");
            if (quest == null) return Fail("at quest_advance.noQuest: questline exhausted or not started");

            string advancedFrom = quest.GetType().Name;
            SafeSetField(quest, "m_IsForceComplete", true);
            SafeInvoke(gem, "GetNextStoryQuest");
            Plugin.Log.LogInfo("[agent] quest: advanced from " + advancedFrom + " (force-completed + GetNextStoryQuest)");

            object next = SafeInvoke(gem, "GetCurrentQuest");
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["advancedFrom"] = advancedFrom;
            r["currentQuestId"] = ToNullableInt(SafeField(gem, "m_CurrentQuestID"));
            r["type"] = next != null ? next.GetType().Name : null;
            r["isLast"] = next != null ? ToNullBool(SafeField(next, "m_IsLastQuest")) : null;
            return Ok(r);
        }

        // force_victory: force-complete each quest and advance until the last quest (left force-completed), capped
        // at 12 iterations to avoid any infinite loop. Then fire m_CheckQuestStatusFSM.SendEvent("complete") to run
        // the credit/end-game chain. Does NOT call ShowStoneHero (that is the show_endgame fallback).
        public static object ForceVictoryAction()
        {
            object gem = StaticInstance("GameEventManager");
            if (gem == null) return Fail("at force_victory.gem: GameEventManager.Instance null");

            bool lastQuestForced = false;
            for (int i = 0; i < 12; i++)
            {
                object q = SafeInvoke(gem, "GetCurrentQuest");
                if (q == null) break;
                SafeSetField(q, "m_IsForceComplete", true);
                if (ToBool(SafeField(q, "m_IsLastQuest"))) { lastQuestForced = true; break; }
                SafeInvoke(gem, "GetNextStoryQuest");
            }
            Plugin.Log.LogInfo("[agent] quest: force_victory looped (lastQuestForced=" + lastQuestForced + ")");

            bool sentComplete = false;
            object fsm = SafeField(gem, "m_CheckQuestStatusFSM");
            if (fsm != null)
            {
                SafeInvokeArgs(fsm, "SendEvent", new[] { typeof(string) }, new object[] { "complete" });
                sentComplete = true;
                Plugin.Log.LogInfo("[agent] quest: m_CheckQuestStatusFSM.SendEvent(\"complete\") fired.");
            }

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["lastQuestForced"] = lastQuestForced;
            r["sentComplete"] = sentComplete;
            r["currentQuestId"] = ToNullableInt(SafeField(gem, "m_CurrentQuestID"));
            r["note"] = "poll /state.signals.victoryShowing";
            return Ok(r);
        }

        // show_endgame: direct fallback. Build a no-op ContinueFSM and call
        // GameEventManager.Instance.ShowStoneHero(ContinueFSM), which starts the end-game stone-hero/credit
        // coroutine directly. Fail if the ContinueFSM cannot be built.
        public static object ShowEndgameAction()
        {
            object gem = StaticInstance("GameEventManager");
            if (gem == null) return Fail("at show_endgame.gem: GameEventManager.Instance null");
            object cfsm = MakeNoopContinueFSM();
            if (cfsm == null) return Fail("at show_endgame.cfsm: could not build ContinueFSM");
            SafeInvokeArgs(gem, "ShowStoneHero", new[] { cfsm.GetType() }, new object[] { cfsm });
            Plugin.Log.LogInfo("[agent] quest: ShowStoneHero forced.");
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["shown"] = true;
            r["note"] = "forced ShowStoneHero; poll /state.signals.victoryShowing";
            return Ok(r);
        }

        // Build a ContinueFSM with no finish callback (Continue() treats null callbacks as a no-op; the game's own
        // FSM drives teardown). Mirrors ActionExecutor.MakeNoopContinueFSM: prefer ContinueFSM(WaitClients=Self),
        // then a single-delegate ctor with a no-op delegate, then a parameterless ctor. Returns null on no usable
        // ctor.
        private static object MakeNoopContinueFSM()
        {
            try
            {
                Type cfsmType = AccessTools.TypeByName("ContinueFSM");
                if (cfsmType == null) return null;

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
                foreach (ConstructorInfo c in ctors)
                {
                    ParameterInfo[] ps = c.GetParameters();
                    if (ps.Length == 1 && typeof(Delegate).IsAssignableFrom(ps[0].ParameterType))
                    {
                        Delegate noop = MakeNoopDelegate(ps[0].ParameterType);
                        if (noop != null) return c.Invoke(new object[] { noop });
                    }
                }
                foreach (ConstructorInfo c in ctors)
                    if (c.GetParameters().Length == 0) return c.Invoke(null);
                return null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[agent] quest: ContinueFSM build failed: " + e.Message);
                return null;
            }
        }

        // No-op void zero-arg delegate matching the given delegate type (else null). Same shape as
        // ActionExecutor's MakeNoopDelegate/CombatFinishNoop.
        private static Delegate MakeNoopDelegate(Type delegateType)
        {
            try
            {
                MethodInfo invoke = delegateType.GetMethod("Invoke");
                if (invoke == null) return null;
                int argc = invoke.GetParameters().Length;
                bool returnsVoid = invoke.ReturnType == typeof(void);
                if (argc == 0 && returnsVoid)
                    return Delegate.CreateDelegate(delegateType,
                        typeof(DungeonOps).GetMethod("ContinueNoop", Reflect.All));
                return null;
            }
            catch { return null; }
        }

        // Trivial main-thread continue callback handed to ContinueFSM. The end-game chain is driven by the game's
        // own FSM/coroutine; we only need a valid delegate target.
        private static void ContinueNoop()
        {
            try { Plugin.Log.LogInfo("[agent] quest: end-game ContinueFSM finished."); } catch { }
        }

        private static bool? ToNullBool(object o) { return o is bool ? (bool?)(bool)o : null; }

        // ============================================================ state probes =====================

        // inDungeon: FTKHub.AnyPlayersInDungeon() (fallback GameFlow.m_DungeonEntered != null).
        public static bool InDungeon()
        {
            object hub = StaticInstance("FTKHub");
            if (hub != null)
            {
                object r = SafeInvoke(hub, "AnyPlayersInDungeon");
                if (r is bool && (bool)r) return true;
            }
            return EnteredDungeon() != null;
        }

        // GameFlow.Instance.m_DungeonEntered (the entered MiniHexDungeon), or null.
        public static object EnteredDungeon()
        {
            object gf = StaticInstance("GameFlow");
            if (gf == null) return null;
            return SafeField(gf, "m_DungeonEntered");
        }

        // The COW that entered the dungeon (GameFlow.m_DungeonEnterCow), falling back to the current-turn COW.
        public static object DungeonEnterCowOrCurrent()
        {
            object gf = StaticInstance("GameFlow");
            object cow = gf != null ? SafeField(gf, "m_DungeonEnterCow") : null;
            if (cow != null) return cow;
            return CurrentCow();
        }

        public static bool CombatActive()
        {
            return CombatActiveOn(StaticInstance("EncounterSessionMC"), StaticInstance("EncounterSession"));
        }

        private static bool CombatActiveOn(object mc, object es)
        {
            if (mc != null && ToBool(SafeField(mc, "m_IsInCombat"))) return true;
            if (es != null && ToBool(SafeField(es, "m_IsInCombat"))) return true;
            return false;
        }

        private static object ActiveTurnFid(object mc)
        {
            if (mc == null) return null;
            IList fo = SafeField(mc, "m_FightOrder") as IList;
            if (fo == null || fo.Count == 0) return null;
            object first = fo[0];
            if (first == null) return null;
            return SafeField(first, "m_Pid");
        }

        private static bool HeroTurnReady()
        {
            try
            {
                object bsb = BattleStanceButtons();
                if (bsb == null) return false;
                if (!ToBool(SafeField(bsb, "m_Initialized"))) return false;
                object gl = StaticInstance("GameLogic");
                if (gl == null) return false;
                object cow = SafeInvoke(gl, "GetCurrentCombatCOW");
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

        // ============================================================ debug ============================
        // Throwaway diagnostic: dump identity + encounter-dict state for the entered AND located dungeon, plus
        // any enemy POIs on the current hex map. Used to settle why GetCurrentRoom() is null after enter.
        public static object DungeonDebugAction()
        {
            object entered = EnteredDungeon();
            object located = LocateDungeon("FloodedCrypt");
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["entered"] = DumpDungeon(entered);
            r["located"] = DumpDungeon(located);
            r["sameObject"] = entered != null && located != null && ReferenceEquals(entered, located);
            r["dungeonEnemies"] = EnemyPoiNames();
            return Ok(r);
        }

        // Force the dungeon-scroll acknowledge directly. In a dungeon the round loop (CommenceBattle) is gated
        // behind GameLogic.m_DungeonFlow (a PlayMaker camera-scroll FSM) calling EncounterSession.DungeonScrollComplete()
        // when the scroll finishes. Under the bridge's synthetic entry the diorama stays black and that FSM never
        // completes, so the ack never fires. DungeonScrollComplete() is public and merely sends the "Acknowledge"
        // RPC (identical to the private _acknowledgeEncounter), so calling it directly fires CommenceBattle ->
        // the round loop -> "Wait For Stance". (Skips the FSM's diorama/camera finishing work, which is cosmetic
        // for an automated test: force_win routes through the real damage RPC on the logical combatants.)
        public static object DungeonScrollCompleteAction()
        {
            object es = StaticInstance("EncounterSession");
            if (es == null) return Fail("at scroll_complete.session: EncounterSession.Instance null");
            // Replicate what the DungeonScroller FSM does at the end of the scroll: FinishScrolling() calls
            // InitPlayerDummiesForCombat(false) (creates the player combat dummies) and TransitionComplete() calls
            // DungeonScrollComplete() (the ack). Forcing only the ack skips dummy init, so the player dummy never
            // reaches "Wait For Stance" and turns stall. So init the player dummies FIRST, then ack.
            bool initiated = ToBool(SafeField(es, "m_PlayerDummiesInitiated"));
            if (!initiated)
                SafeInvokeArgs(es, "InitPlayerDummiesForCombat", new[] { typeof(bool) }, new object[] { false });
            SafeInvoke(es, "DungeonScrollComplete");
            Plugin.Log.LogInfo("[agent] dungeon: InitPlayerDummiesForCombat(false) + DungeonScrollComplete() forced.");
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["acked"] = true;
            r["dummiesWereInitiated"] = initiated;
            r["note"] = "inited player dummies + forced dungeon-scroll ack; poll /state.combat until heroTurnReady";
            return Ok(r);
        }

        // Throwaway diagnostic: EncounterSession state relevant to the scroll/ack stall.
        public static object SessionDebugAction()
        {
            object es = StaticInstance("EncounterSession");
            Dictionary<string, object> r = new Dictionary<string, object>();
            if (es == null) { r["sessionNull"] = true; return Ok(r); }
            r["isInCombat"] = SafeField(es, "m_IsInCombat");
            r["alreadyInDiorama"] = SafeField(es, "m_AlreadyInDiorama");
            r["firstAfterResume"] = SafeField(es, "m_IsFirstEncounterAfterResume");
            object dio = SafeProp(es, "m_ActiveDiorama");
            if (dio == null) dio = SafeField(es, "m_ActiveDiorama");
            r["activeDioramaNull"] = dio == null;
            r["activeDioramaType"] = dio != null ? dio.GetType().Name : null;
            object ack = SafeField(es, "m_AckID");
            r["ackId"] = ack != null ? ack.ToString() : null;
            return Ok(r);
        }

        // Throwaway: explicitly (re)run the game's static GenerateDungeonEncounters on the entered dungeon,
        // capturing the REAL exception (the enter path swallows it). On success, assign the populated dict.
        public static object DungeonRegenAction()
        {
            object d = EnteredDungeon();
            if (d == null) return Fail("at dungeon_regen.inDungeon: not in a dungeon");
            Dictionary<string, object> r = new Dictionary<string, object>();
            IDictionary before = SafeField(d, "m_DungeonEncounters") as IDictionary;
            r["countBefore"] = before != null ? before.Count : -1;

            object rand = SafeField(d, "m_DungeonRandom");
            if (rand == null)
            {
                Type rt = AccessTools.TypeByName("FTKRandom");
                try { if (rt != null) rand = Activator.CreateInstance(rt); } catch { }
            }
            if (rand == null) return Fail("at dungeon_regen.rand: no FTKRandom available");

            try
            {
                MethodInfo gen = null;
                foreach (MethodInfo mi in d.GetType().GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (mi.Name == "GenerateDungeonEncounters") { gen = mi; break; }
                if (gen == null) return Fail("at dungeon_regen.method: GenerateDungeonEncounters not found");

                object result = gen.Invoke(null, new object[] { d, rand });

                FieldInfo fi = d.GetType().GetField("m_DungeonEncounters", Reflect.All);
                if (fi != null && result != null) fi.SetValue(d, result);

                IDictionary after = result as IDictionary;
                r["countAfter"] = after != null ? after.Count : -1;
                Dictionary<string, object> per = new Dictionary<string, object>();
                Dictionary<string, object> layout = new Dictionary<string, object>();
                if (after != null)
                    foreach (object k in after.Keys)
                    {
                        IList l = after[k] as IList;
                        per[k.ToString()] = l != null ? l.Count : -1;
                        List<object> roomList = new List<object>();
                        if (l != null)
                            foreach (object room2 in l)
                            {
                                Dictionary<string, object> ri = new Dictionary<string, object>();
                                object t2 = SafeField(room2, "m_Type");
                                ri["type"] = t2 != null ? t2.ToString() : null;
                                IList objs = SafeField(room2, "m_EncounterObjects") as IList;
                                List<string> os = new List<string>();
                                if (objs != null) foreach (object ob in objs) os.Add(ob != null ? ob.ToString() : "null");
                                ri["objects"] = os;
                                roomList.Add(ri);
                            }
                        layout[k.ToString()] = roomList;
                    }
                r["perLevel"] = per;
                r["layout"] = layout;
                object room = SafeInvoke(d, "GetCurrentRoom");
                object rtype = room != null ? SafeField(room, "m_Type") : null;
                r["currentRoomType"] = rtype != null ? rtype.ToString() : null;
                r["regenOk"] = true;
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException ?? e;
                r["regenOk"] = false;
                r["exception"] = inner.GetType().Name + ": " + (inner.Message ?? "");
                string st = inner.StackTrace ?? "";
                if (st.Length > 600) st = st.Substring(0, 600);
                r["stack"] = st.Replace("\r", "").Replace("\n", " | ");
            }
            return Ok(r);
        }

        private static Dictionary<string, object> DumpDungeon(object d)
        {
            Dictionary<string, object> o = new Dictionary<string, object>();
            if (d == null) { o["null"] = true; return o; }
            o["idHash"] = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);
            o["clrType"] = d.GetType().Name;
            object dt = SafeField(d, "m_Type"); o["dungeonType"] = dt != null ? dt.ToString() : null;
            object id = SafeField(d, "m_ID"); o["dungeonId"] = id != null ? id.ToString() : null;
            o["level"] = SafeField(d, "m_Level");
            o["roomIndex"] = SafeField(d, "m_RoomIndex");
            o["deactivated"] = ToBool(SafeField(d, "m_Deactivated"));
            IDictionary enc = SafeField(d, "m_DungeonEncounters") as IDictionary;
            if (enc == null) { o["encDictNull"] = true; }
            else
            {
                o["encDictCount"] = enc.Count;
                Dictionary<string, object> per = new Dictionary<string, object>();
                foreach (object k in enc.Keys)
                {
                    IList list = enc[k] as IList;
                    per[k.ToString()] = list != null ? list.Count : -1;
                }
                o["encPerLevel"] = per;
            }
            object room = SafeInvoke(d, "GetCurrentRoom");
            o["currentRoomNull"] = room == null;
            if (room != null)
            {
                object rt = SafeField(room, "m_Type"); o["currentRoomType"] = rt != null ? rt.ToString() : null;
            }
            o["defLevelCount"] = SafeInvoke(d, "GetLevelCount");
            return o;
        }

        private static List<string> EnemyPoiNames()
        {
            List<string> names = new List<string>();
            object hexInst = StaticInstance("FTKHex");
            if (hexInst == null) return names;
            Type mtType = AccessTools.TypeByName("MiniHexInfo+MiniHexType");
            if (mtType == null || !mtType.IsEnum) return names;
            foreach (string nm in new[] { "Enemy", "EnemyCamp" })
            {
                object miniType;
                try { miniType = Enum.Parse(mtType, nm); } catch { continue; }
                object list = SafeInvokeArgs(hexInst, "GetPOIList", new Type[] { mtType }, new object[] { miniType });
                IEnumerable en = list as IEnumerable;
                if (en == null) continue;
                foreach (object mhi in en)
                {
                    if (mhi == null) continue;
                    object h = SafeField(mhi, "m_HexLand");
                    object b = h != null ? SafeField(h, "m_ParentIndex") : null;
                    object s = h != null ? SafeField(h, "m_Index") : null;
                    names.Add(mhi.GetType().Name + "@" + b + "," + s);
                }
            }
            return names;
        }

        private static object FirstLiveEnemyFid(object es)
        {
            if (es == null) return null;
            IDictionary dummies = SafeField(es, "m_EnemyDummies") as IDictionary;
            if (dummies == null) return null;
            foreach (DictionaryEntry de in dummies)
            {
                object dummy = de.Value;
                if (dummy == null) continue;
                if (ToBool(SafeField(dummy, "m_IsAlive"))) return de.Key;
            }
            return null;
        }

        private static object BattleStanceButtons()
        {
            object ui = StaticInstance("FTKUI");
            if (ui == null) return null;
            return SafeField(ui, "m_BattleStanceButtons");
        }

        // GameLogic.GetCurrentCOW path: FTKHub.GetCharacterOverworldByFID(GameLogic.m_CurrentPlayer).
        public static object CurrentCow()
        {
            object gl = StaticInstance("GameLogic");
            if (gl == null) return null;
            object hub = StaticInstance("FTKHub");
            if (hub == null) return null;
            object fid = SafeField(gl, "m_CurrentPlayer");
            if (fid == null) return null;
            return SafeInvokeArgs(hub, "GetCharacterOverworldByFID", new[] { fid.GetType() }, new[] { fid });
        }

        // ============================================================ result shape =====================

        public static bool ResultOk(object result)
        {
            IDictionary<string, object> m = result as IDictionary<string, object>;
            if (m == null) return false;
            object ok;
            return m.TryGetValue("ok", out ok) && ok is bool && (bool)ok;
        }

        public static string ResultError(object result)
        {
            IDictionary<string, object> m = result as IDictionary<string, object>;
            if (m == null) return "no result";
            object err;
            return m.TryGetValue("error", out err) ? err as string : null;
        }

        private static Dictionary<string, object> EnteredResult(bool entered, string where, string dungeonId)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["entered"] = entered;
            d["where"] = where;
            if (dungeonId != null) d["dungeonId"] = dungeonId;
            return d;
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

        // ============================================================ reflection utils =================

        public static object StaticInstance(string typeName)
        {
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return null;
            PropertyInfo pi = t.GetProperty("Instance", Reflect.All);
            if (pi != null) return pi.GetValue(null, null);
            FieldInfo fi = t.GetField("Instance", Reflect.All);
            if (fi != null) return fi.GetValue(null);
            return null;
        }

        public static object SafeField(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.GetField(obj, name); } catch { return null; }
        }

        public static object SafeProp(object obj, string name)
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

        public static object SafeInvoke(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.Invoke(obj, name); } catch { return null; }
        }

        public static object SafeInvokeArgs(object obj, string name, Type[] sig, object[] args)
        {
            if (obj == null) return null;
            try { return Reflect.InvokeArgs(obj, name, sig, args); } catch { return null; }
        }

        // Set a (possibly non-public) instance field defensively, walking the type hierarchy. Never throws.
        private static void SafeSetField(object obj, string name, object val)
        {
            if (obj == null) return;
            try
            {
                for (Type cur = obj.GetType(); cur != null; cur = cur.BaseType)
                {
                    FieldInfo f = cur.GetField(name, Reflect.All | BindingFlags.DeclaredOnly);
                    if (f != null) { f.SetValue(obj, val); return; }
                }
            }
            catch { }
        }

        private static MethodInfo FindMethod(Type t, string name)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                MethodInfo m = cur.GetMethod(name, Reflect.All | BindingFlags.DeclaredOnly);
                if (m != null) return m;
            }
            return null;
        }

        private static bool ToBool(object o) { return o is bool && (bool)o; }

        private static int? ToNullableInt(object o)
        {
            if (o == null) return null;
            try
            {
                if (o is int) return (int)o;
                if (o is Enum) return Convert.ToInt32(o);
                if (o is long || o is short || o is byte || o is uint || o is ushort || o is sbyte)
                    return Convert.ToInt32(o);
            }
            catch { }
            return null;
        }
    }
}
