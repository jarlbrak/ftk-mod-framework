using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Builds the GET /state snapshot entirely through defensive reflection (Core.Reflect +
    /// AccessTools.TypeByName), so a missing/renamed game member degrades to a warnings[] entry and a null
    /// field rather than a 500. Every section is its own try/catch. Returns a nested
    /// Dictionary&lt;string,object&gt; / List&lt;object&gt; tree that <see cref="Json"/> serializes.
    ///
    /// MUST run on the Unity main thread (singletons resolve only inside a live session). The bridge
    /// marshals this via AgentBridge.RunOnMainThread.
    /// </summary>
    internal static class StateReader
    {
        public static object ReadState()
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            List<object> warnings = new List<object>();

            // --- session-level scalars ---------------------------------------------------------------
            bool inSession = false;
            bool? singlePlayer = null;
            object glInstance = TryGet(() => StaticInstance("GameLogic"), warnings, "GameLogic.Instance");

            try
            {
                object usgInstance = StaticInstance("uiStartGame");
                if (usgInstance != null)
                    inSession = ToBool(Reflect.GetField(usgInstance, "m_GameStarted"));
            }
            catch (Exception e) { warnings.Add("inSession: " + e.Message); }

            try
            {
                if (glInstance != null)
                {
                    object sp = Reflect.Invoke(glInstance, "IsSinglePlayer");
                    if (sp is bool) singlePlayer = (bool)sp;
                }
            }
            catch (Exception e) { warnings.Add("singlePlayer: " + e.Message); }

            root["inSession"] = inSession;
            root["singlePlayer"] = singlePlayer;

            // --- GameFlow time scalars ---------------------------------------------------------------
            try
            {
                object gf = StaticInstance("GameFlow");
                root["day"] = gf != null ? ToNullableInt(Reflect.Invoke(gf, "get_CurrentGameDay")) : null;
                root["timeIndex"] = gf != null ? ToNullableInt(Reflect.Invoke(gf, "get_TimeIndex")) : null;
                root["round"] = gf != null ? ToNullableInt(Reflect.GetField(gf, "m_RoundCount")) : null;
            }
            catch (Exception e)
            {
                warnings.Add("gameflow: " + e.Message);
                root["day"] = null; root["timeIndex"] = null; root["round"] = null;
            }

            // --- quest scalars -----------------------------------------------------------------------
            // The AUTHORITATIVE active quest is GameEventManager.Instance.GetCurrentQuest(), NOT
            // GameLogic.GetQuestByID(m_QuestID) (m_QuestID is just an allocation counter). questId is the
            // GameEventManager.m_CurrentQuestID field (0 when the questline is exhausted).
            object quest = null;
            int? questId = null;
            try
            {
                object gem = StaticInstance("GameEventManager");
                if (gem != null)
                {
                    quest = SafeInvoke(gem, "GetCurrentQuest");
                    questId = ToNullableInt(SafeField(gem, "m_CurrentQuestID"));
                }
            }
            catch (Exception e) { warnings.Add("questId: " + e.Message); }
            root["questId"] = questId;

            try
            {
                if (quest != null)
                {
                    object oneline = Reflect.GetField(quest, "m_OnelineID");
                    root["questObjectiveKey"] = oneline != null ? oneline.ToString() : null;
                    root["questDestRealm"] = ToNullableInt(Reflect.GetField(quest, "m_DestRealm"));
                    object complete = Reflect.Invoke(quest, "IsRawComplete");
                    root["questComplete"] = complete is bool ? (object)(bool)complete : null;
                    object last = Reflect.GetField(quest, "m_IsLastQuest");
                    root["questIsLast"] = last is bool ? (object)(bool)last : null;
                }
                else
                {
                    root["questObjectiveKey"] = null; root["questDestRealm"] = null;
                    root["questComplete"] = null; root["questIsLast"] = null;
                }
            }
            catch (Exception e)
            {
                warnings.Add("quest: " + e.Message);
                root["questObjectiveKey"] = null; root["questDestRealm"] = null;
                root["questComplete"] = null; root["questIsLast"] = null;
            }

            // --- current turn FID --------------------------------------------------------------------
            try
            {
                if (glInstance != null)
                {
                    object cur = Reflect.GetField(glInstance, "m_CurrentPlayer");
                    root["currentTurnFid"] = FidDict(cur);
                }
                else root["currentTurnFid"] = null;
            }
            catch (Exception e) { warnings.Add("currentTurnFid: " + e.Message); root["currentTurnFid"] = null; }

            // --- party -------------------------------------------------------------------------------
            object hub = TryGet(() => StaticInstance("FTKHub"), warnings, "FTKHub.Instance");
            object partyList = ReadParty(hub, warnings);
            root["party"] = partyList;
            int partyCount = (partyList is IList) ? ((IList)partyList).Count : 0;

            // --- combat ------------------------------------------------------------------------------
            object combat = null;
            bool inCombat = false;
            try
            {
                object es = StaticInstance("EncounterSession");
                if (es != null)
                {
                    object ic = Reflect.GetField(es, "m_IsInCombat");
                    inCombat = ToBool(ic);
                    combat = ReadCombat(es, inCombat, warnings);
                }
            }
            catch (Exception e) { warnings.Add("combat: " + e.Message); }
            root["combat"] = combat;

            // --- dungeon -----------------------------------------------------------------------------
            // The in-dungeon traversal state the agent loop branches on. All reads defensive; null on any miss.
            root["dungeon"] = ReadDungeon(inCombat, warnings);

            // --- map ---------------------------------------------------------------------------------
            root["map"] = ReadMap(hub, glInstance, warnings);

            // --- choices / modal ---------------------------------------------------------------------
            string modalType = null;
            object choices = ReadChoices(inSession, warnings, out modalType);
            root["choices"] = choices;

            // --- selectable adventures (menu only) ---------------------------------------------------
            // Surface the injected adventures so the agent loop can see the framework's D1 ("HollowMire")
            // as a pickable start_run option and confirm the current selection. STRICTLY READ-ONLY: this is
            // an observation, so it must never force the cache to build (that is the list_adventures action's
            // job). GetPreviewNamesIfBuilt returns null until something else builds the cache.
            if (!inSession)
            {
                try
                {
                    List<string> advNames = AdventureCache.GetPreviewNamesIfBuilt();
                    if (advNames == null) root["adventures"] = null;
                    else
                    {
                        List<object> boxed = new List<object>();
                        foreach (string n in advNames) boxed.Add(n);
                        root["adventures"] = boxed;
                    }
                }
                catch (Exception e) { warnings.Add("adventures: " + e.Message); root["adventures"] = null; }

                try
                {
                    object usg = StaticInstance("uiStartGame");
                    root["selectedAdventure"] = usg != null
                        ? Reflect.GetField(usg, "m_GameDefName") as string : null;
                }
                catch (Exception e) { warnings.Add("selectedAdventure: " + e.Message); root["selectedAdventure"] = null; }
            }

            // --- signals -----------------------------------------------------------------------------
            Dictionary<string, object> signals = new Dictionary<string, object>();
            bool choiceOpen = (choices is IList && ((IList)choices).Count > 0);
            signals["inCombat"] = inCombat;
            signals["modalOpen"] = choiceOpen || modalType != null;
            signals["modalType"] = modalType;
            signals["choiceOpen"] = choiceOpen;
            signals["allDead"] = ReadAllDead(hub, warnings);
            // victoryShowing: FTKHelp.CreditScreen.Instance.m_ShowingInEndGame (true on the end-game/credit
            // screen). The Instance getter derefs FTKUI.Instance and can throw, so it is fully wrapped; null/false
            // when unavailable. m_ShowingInEndGame is a private bool, read via Reflect (non-public flags).
            signals["victoryShowing"] = ReadVictoryShowing(warnings);
            signals["warnings"] = warnings;
            // victoryArmed: heuristic from quest scalars (no traced terminal FSM state, see followups).
            bool victoryArmed = (root["questComplete"] as bool?) == true && (root["questIsLast"] as bool?) == true;
            signals["victoryArmed"] = victoryArmed;
            root["signals"] = signals;

            // --- phase -------------------------------------------------------------------------------
            root["phase"] = DecidePhase(inSession, inCombat, choiceOpen || modalType != null, victoryArmed,
                                        root["questComplete"] as bool?, signals["allDead"] as bool?, partyCount);

            return root;
        }

        // ============================================================ party ============================

        private static object ReadParty(object hub, List<object> warnings)
        {
            List<object> party = new List<object>();
            if (hub == null) return party;
            try
            {
                object cowsObj = Reflect.GetField(hub, "m_CharacterOverworlds");
                IEnumerable cows = cowsObj as IEnumerable;
                if (cows == null) return party;
                foreach (object cow in cows)
                {
                    if (cow == null) continue;
                    party.Add(ReadCharacter(cow, warnings));
                }
            }
            catch (Exception e) { warnings.Add("party: " + e.Message); }
            return party;
        }

        private static object ReadCharacter(object cow, List<object> warnings)
        {
            Dictionary<string, object> p = new Dictionary<string, object>();
            try { p["fid"] = FidDict(Reflect.GetField(cow, "m_FTKPlayerID")); }
            catch { p["fid"] = null; }

            object stats = SafeField(cow, "m_CharacterStats");
            if (stats != null)
            {
                p["name"] = SafeString(stats, "m_CharacterName");
                p["classId"] = ToNullableInt(SafeField(stats, "m_CharacterClass"));
                p["hp"] = ToNullableInt(SafeField(stats, "m_HealthCurrent"));
                p["maxHp"] = ToNullableInt(SafeProp(stats, "MaxHealth"));
                p["focus"] = ToNullableInt(SafeField(stats, "m_FocusPoints"));
                p["maxFocus"] = ToNullableInt(SafeProp(stats, "MaxFocus"));
                p["gold"] = ToNullableInt(SafeField(stats, "m_Gold"));
                p["level"] = ToNullableInt(SafeField(stats, "m_PlayerLevel"));
                p["xp"] = ToNullableInt(SafeField(stats, "m_PlayerXP"));
                p["actionPoints"] = ToNullableInt(SafeField(stats, "m_ActionPoints"));
                p["spentFocus"] = ToNullableInt(SafeProp(stats, "SpentFocus"));
            }
            else
            {
                warnings.Add("character: m_CharacterStats null");
            }

            // hex/realm/position from m_HexLand
            try
            {
                object hex = SafeProp(cow, "m_HexLand");
                if (hex == null) hex = SafeField(cow, "m_HexLand");
                if (hex != null)
                {
                    object realm = Reflect.Invoke(hex, "GetRealm");
                    p["realmId"] = ToNullableInt(realm);
                    object hexInfo = Reflect.GetField(hex, "m_HexInfo");
                    if (hexInfo != null)
                    {
                        p["hexBig"] = ToNullableInt(Reflect.GetField(hexInfo, "m_HexBig"));
                        p["hexSmall"] = ToNullableInt(Reflect.GetField(hexInfo, "m_HexSmall"));
                    }
                }
            }
            catch (Exception e) { warnings.Add("character.hex: " + e.Message); }

            try
            {
                object tr = SafeProp(cow, "transform");
                if (tr != null)
                {
                    object pos = Reflect.Invoke(tr, "get_position");
                    if (pos != null) p["pos"] = Vec3Dict(pos);
                }
            }
            catch { /* position is best-effort */ }

            return p;
        }

        // ============================================================ combat ===========================

        private static object ReadCombat(object es, bool inCombat, List<object> warnings)
        {
            Dictionary<string, object> c = new Dictionary<string, object>();
            c["active"] = inCombat;
            try
            {
                object liveC = Reflect.Invoke(es, "GetLiveCombatantCount");
                c["liveCombatants"] = ToNullableInt(liveC);
            }
            catch (Exception e) { warnings.Add("combat.liveCombatants: " + e.Message); c["liveCombatants"] = null; }

            try { c["currentEnemyFid"] = FidDict(Reflect.GetField(es, "m_CurrentEnemy")); }
            catch { c["currentEnemyFid"] = null; }

            // whoseTurn: EncounterSessionMC.m_FightOrder[0].m_Pid + IsPlayer(). Lets the agent know when to call
            // auto_combat_turn (isPlayer==true) vs wait for the enemy's turn.
            c["whoseTurn"] = ReadWhoseTurn(warnings);

            // heroTurnReady: FTKUI.m_BattleStanceButtons.m_Initialized && the current combat COW's dummy FSM is
            // parked in "Wait For Stance". This is the exact readiness gate the combat commit checks.
            c["heroTurnReady"] = ReadHeroTurnReady(warnings);
            // readyParts:{initialized, fsmState} so the harness can SEE which half of heroTurnReady is false
            // (banner intro vs enemy-turn vs genuinely-ready). The empty abilities[] below is NORMAL whenever
            // initialized==false (UI not built yet), not an error.
            c["readyParts"] = ReadReadyParts(warnings);

            // enemies list from m_EnemyDummies (Dictionary<FTKPlayerID, EnemyDummy>)
            int liveEnemyCount = 0;
            bool stuck = false; // inCombat && any enemy hp<=0 with alive==true: the death RPC never fired.
            List<object> enemies = new List<object>();
            try
            {
                object dummiesObj = Reflect.GetField(es, "m_EnemyDummies");
                IDictionary dummies = dummiesObj as IDictionary;
                if (dummies != null)
                {
                    foreach (DictionaryEntry de in dummies)
                    {
                        Dictionary<string, object> en = new Dictionary<string, object>();
                        en["fid"] = FidDict(de.Key);
                        object dummy = de.Value;
                        // EnemyDummy : EnemyInfo : CharacterDummy. An EnemyDummy has NO m_CharacterStats;
                        // its health lives on EnemyInfo and its enemy-row data on m_EnemyCombat.
                        object ec = SafeField(dummy, "m_EnemyCombat");
                        en["name"] = ReadEnemyName(ec, dummy);
                        // type: FTK_enemyCombat.m_ID (string) preferred, else EnemyDummy.m_EnemyType (string).
                        object typeStr = ec != null ? SafeField(ec, "m_ID") : null;
                        if (typeStr == null) typeStr = SafeField(dummy, "m_EnemyType");
                        en["type"] = typeStr as string;
                        // hp: EnemyInfo.m_CurrentHealth (int field).
                        int? hp = ToNullableInt(SafeField(dummy, "m_CurrentHealth"));
                        en["hp"] = hp;
                        // hpMax: FTK_enemyCombat.GetHealthTotal() (int).
                        en["hpMax"] = ec != null ? ToNullableInt(SafeInvoke(ec, "GetHealthTotal")) : null;
                        // alive: CharacterDummy.m_IsAlive is a bool FIELD (there is no IsAlive() method). It is
                        // cleared ONLY by RemoveCombatant via the CombatEnemyDie RPC, so hp/alive disagreement
                        // (hp<=0 && alive==true) IS the stuck signal.
                        object aliveObj = SafeField(dummy, "m_IsAlive");
                        bool isAlive = aliveObj is bool && (bool)aliveObj;
                        en["alive"] = aliveObj is bool ? (object)isAlive : null;
                        if (isAlive) liveEnemyCount++;
                        if (inCombat && isAlive && hp.HasValue && hp.Value <= 0) stuck = true;
                        enemies.Add(en);
                    }
                }
            }
            catch (Exception e) { warnings.Add("combat.enemies: " + e.Message); }
            c["enemies"] = enemies;
            // liveEnemies recomputed from enemies[].alive so it always agrees with the per-enemy data the agent
            // commits against (kept consistent with the old GetLiveEnemyCount semantics).
            c["liveEnemies"] = liveEnemyCount;
            // stuck: the HUD-shows-0-but-m_IsAlive-still-true corruption fingerprint. When true the harness must
            // NOT re-fire a commit; it should report and abort the run rather than spin.
            c["stuck"] = stuck;

            // winningPlayerFid: EncounterSessionMC.m_WinningPlayerID, populated post-resolve so the harness can
            // distinguish a true VICTORY from a flee/defeat exit when active flips false. Null while unresolved.
            c["winningPlayerFid"] = ReadWinningPlayerFid(warnings);

            // fightOrder from EncounterSessionMC.m_FightOrder (best-effort; structure unverified)
            c["fightOrder"] = ReadFightOrder(warnings);
            // abilities for the acting hero (best-effort via FTKUI battle stance buttons). EMPTY whenever
            // heroTurnReady==false (UI not initialized) -- empty abilities + ready:false is normal pre-stance.
            c["abilities"] = ReadAbilities(warnings);

            // driver: mirrors dungeon.driver so win_combat/force_win/auto_combat progress is pollable on
            // /state.combat (the harness polls combat.active==false but can also watch driver.lastError).
            c["driver"] = ReadCombatDriver();

            return c;
        }

        // readyParts:{initialized:bool, fsmState:string}. fsmState surfaces the banner-vs-enemy-turn distinction
        // when heroTurnReady is false (e.g. "Wait For Stance" means ready; a banner/anim state means not yet).
        private static object ReadReadyParts(List<object> warnings)
        {
            Dictionary<string, object> rp = new Dictionary<string, object>();
            try
            {
                object ui = StaticInstance("FTKUI");
                object bsb = ui != null ? Reflect.GetField(ui, "m_BattleStanceButtons") : null;
                rp["initialized"] = bsb != null && ToBool(SafeField(bsb, "m_Initialized"));
                object gl = StaticInstance("GameLogic");
                object cow = gl != null ? SafeInvoke(gl, "GetCurrentCombatCOW") : null;
                object dummy = cow != null ? SafeField(cow, "m_CurrentDummy") : null;
                object fsm = dummy != null ? SafeField(dummy, "m_CharacterDummyFSM") : null;
                object sn = fsm != null ? SafeProp(fsm, "ActiveStateName") : null;
                rp["fsmState"] = sn as string;
            }
            catch (Exception e)
            {
                warnings.Add("combat.readyParts: " + e.Message);
                rp["initialized"] = false; rp["fsmState"] = null;
            }
            return rp;
        }

        // EncounterSessionMC.m_WinningPlayerID (an FTKPlayerID), or null while combat is unresolved.
        private static object ReadWinningPlayerFid(List<object> warnings)
        {
            try
            {
                object mc = StaticInstance("EncounterSessionMC");
                if (mc == null) return null;
                object fid = SafeField(mc, "m_WinningPlayerID");
                if (fid == null) return null;
                int? ti = ToNullableInt(SafeField(fid, "m_TurnIndex"));
                // NullPlayerID is (-1,*); treat a negative turn index as "no winner yet".
                if (ti.HasValue && ti.Value < 0) return null;
                return FidDict(fid);
            }
            catch (Exception e) { warnings.Add("combat.winningPlayerFid: " + e.Message); return null; }
        }

        // {running, lastError} from CombatDriver when the resolve coroutine is active or left an error.
        private static object ReadCombatDriver()
        {
            if (CombatDriver.IsRunning)
            {
                Dictionary<string, object> drv = new Dictionary<string, object>();
                drv["running"] = true;
                drv["lastError"] = CombatDriver.LastError;
                return drv;
            }
            if (CombatDriver.LastError != null)
            {
                Dictionary<string, object> drv = new Dictionary<string, object>();
                drv["running"] = false;
                drv["lastError"] = CombatDriver.LastError;
                return drv;
            }
            return null;
        }

        // whoseTurn: { fid, isPlayer } from EncounterSessionMC.m_FightOrder[0].m_Pid. Null when no fight order.
        private static object ReadWhoseTurn(List<object> warnings)
        {
            try
            {
                object mc = StaticInstance("EncounterSessionMC");
                if (mc == null) return null;
                object foObj = Reflect.GetField(mc, "m_FightOrder");
                IList fo = foObj as IList;
                if (fo == null || fo.Count == 0) return null;
                object first = fo[0];
                if (first == null) return null;
                object fid = SafeField(first, "m_Pid");
                if (fid == null) return null;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["fid"] = FidDict(fid);
                object isPlayer = SafeInvoke(fid, "IsPlayer");
                d["isPlayer"] = isPlayer is bool ? (object)(bool)isPlayer : null;
                d["source"] = "m_FightOrder[0]";
                return d;
            }
            catch (Exception e) { warnings.Add("combat.whoseTurn: " + e.Message); return null; }
        }

        // heroTurnReady: m_BattleStanceButtons.m_Initialized && GetCurrentCombatCOW().m_CurrentDummy
        // .m_CharacterDummyFSM.ActiveStateName == "Wait For Stance".
        private static object ReadHeroTurnReady(List<object> warnings)
        {
            try
            {
                object ui = StaticInstance("FTKUI");
                if (ui == null) return false;
                object bsb = Reflect.GetField(ui, "m_BattleStanceButtons");
                if (bsb == null) return false;
                // Inside a dungeon the forced-scroll-ack commit path leaves m_Initialized false while the dummy is
                // genuinely in "Wait For Stance" (the authoritative gate); require m_Initialized only on the
                // overworld. Mirrors CombatDriver.HeroTurnReady so /state and the driver agree.
                if (!DungeonOps.InDungeon() && !ToBool(SafeField(bsb, "m_Initialized"))) return false;
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
            catch (Exception e) { warnings.Add("combat.heroTurnReady: " + e.Message); return false; }
        }

        private static object ReadEnemyName(object enemyCombat, object dummy)
        {
            try
            {
                if (enemyCombat != null)
                {
                    string id = Reflect.GetField(enemyCombat, "m_ID") as string;
                    string name;
                    if (id != null && Localization.TryGetName(id, out name)) return name;
                    object disp = Reflect.Invoke(enemyCombat, "GetEnemyDisplay");
                    if (disp is string) return (string)disp;
                    return id;
                }
            }
            catch { }
            return null;
        }

        private static object ReadFightOrder(List<object> warnings)
        {
            List<object> order = new List<object>();
            try
            {
                Type mcType = AccessTools.TypeByName("EncounterSessionMC");
                if (mcType == null) return order;
                object mc = StaticInstance("EncounterSessionMC");
                if (mc == null) return order;
                object fo = Reflect.GetField(mc, "m_FightOrder");
                IEnumerable seq = fo as IEnumerable;
                if (seq == null) return order;
                foreach (object item in seq)
                {
                    Dictionary<string, object> e = new Dictionary<string, object>();
                    // EncounterSessionMC.FightOrderEntry stores the player id in m_Pid (FTKPlayerID).
                    object fid = SafeField(item, "m_Pid");
                    if (fid == null) fid = SafeField(item, "m_FID");
                    if (fid == null) fid = SafeField(item, "fid");
                    e["fid"] = FidDict(fid);
                    e["tta"] = ToNullableDouble(SafeField(item, "m_TTA"));
                    order.Add(e);
                }
            }
            catch (Exception e) { warnings.Add("combat.fightOrder: " + e.Message); }
            return order;
        }

        private static object ReadAbilities(List<object> warnings)
        {
            List<object> abilities = new List<object>();
            try
            {
                object ui = StaticInstance("FTKUI");
                if (ui == null) return abilities;
                object bsb = Reflect.GetField(ui, "m_BattleStanceButtons");
                if (bsb == null) return abilities;
                object profs = Reflect.GetField(bsb, "m_Proficiencies");
                IEnumerable seq = profs as IEnumerable;
                if (seq == null) return abilities;
                int index = 0;
                foreach (object pv in seq)
                {
                    if (pv == null) { index++; continue; }
                    Dictionary<string, object> a = new Dictionary<string, object>();
                    object prof = SafeField(pv, "m_Prof");
                    a["profId"] = ToNullableInt(prof);
                    a["index"] = index;
                    string label = null;
                    string idStr = prof != null ? prof.ToString() : null;
                    if (idStr != null) Localization.TryGetName(idStr, out label);
                    string name = label != null ? label : idStr;
                    a["label"] = name;
                    a["name"] = name;
                    // buttonEnabled: the ProfValues.m_Button (a uiBattleButton). Treat it as enabled when its
                    // GameObject is active in the hierarchy (best-effort; null button => false).
                    object button = SafeField(pv, "m_Button");
                    a["buttonEnabled"] = button != null && GameObjectActiveInHierarchy(button);
                    abilities.Add(a);
                    index++;
                }
            }
            catch (Exception e) { warnings.Add("combat.abilities: " + e.Message); }
            return abilities;
        }

        // ============================================================ dungeon =========================

        // Read the in-dungeon traversal block. The agent branches on this: when inDungeon && inCombat it runs
        // resolve_turn{KillAll}; otherwise it advances rooms. cleared/deactivated is the victory-armed gate.
        // Game members verified via ilspycmd (Jun 2026): FTKHub.AnyPlayersInDungeon(), GameFlow.m_DungeonEntered
        // (MiniHexDungeon), MiniHexDungeon.m_ID/m_Level/m_RoomIndex/GetLevelCount()/GetRoomCount(int)/
        // GetCurrentRoom()->RoomInfo.m_Type/IsInLastRoom()/IsAtLastLevel()/IsDungeonCleared()/m_Deactivated.
        private static object ReadDungeon(bool inCombat, List<object> warnings)
        {
            Dictionary<string, object> dg = new Dictionary<string, object>();
            try
            {
                bool inDungeon = false;
                object hub = StaticInstance("FTKHub");
                if (hub != null)
                {
                    object r = SafeInvoke(hub, "AnyPlayersInDungeon");
                    if (r is bool) inDungeon = (bool)r;
                }

                object gf = StaticInstance("GameFlow");
                object d = gf != null ? SafeField(gf, "m_DungeonEntered") : null;
                if (!inDungeon && d != null) inDungeon = true;

                dg["inDungeon"] = inDungeon;
                dg["inCombat"] = inCombat;

                if (d == null)
                {
                    dg["dungeonId"] = null;
                    dg["level"] = null; dg["room"] = null;
                    dg["levelCount"] = null; dg["roomCount"] = null;
                    dg["currentRoomType"] = null;
                    dg["encounterObjects"] = null;
                    dg["inLastRoom"] = null; dg["atLastLevel"] = null;
                    dg["cleared"] = null; dg["deactivated"] = null;
                }
                else
                {
                    object id = SafeField(d, "m_ID");
                    dg["dungeonId"] = id != null ? id.ToString() : null;
                    dg["level"] = ToNullableInt(SafeField(d, "m_Level"));
                    dg["room"] = ToNullableInt(SafeField(d, "m_RoomIndex"));
                    dg["levelCount"] = ToNullableInt(SafeInvoke(d, "GetLevelCount"));
                    int? lvl = ToNullableInt(SafeField(d, "m_Level"));
                    dg["roomCount"] = lvl.HasValue ? ToNullableInt(SafeInvoke2(d, "GetRoomCount", lvl.Value)) : null;

                    // GetCurrentRoom() -> RoomInfo (or null if that level/dict is not populated). currentRoomType is
                    // RoomInfo.m_Type (an EncounterType, e.g. "Enemy"/"Stair"/"Cleared"); encounterObjects is
                    // RoomInfo.m_EncounterObjects (List<string>) emitted as a JSON array so the harness can see a
                    // room's enemy/boss set. Both null when GetCurrentRoom is null; each read is swallowed on throw.
                    object room = SafeInvoke(d, "GetCurrentRoom");
                    object roomType = room != null ? SafeField(room, "m_Type") : null;
                    dg["currentRoomType"] = roomType != null ? roomType.ToString() : null;
                    dg["encounterObjects"] = ReadEncounterObjects(room);

                    object inLast = SafeInvoke(d, "IsInLastRoom");
                    dg["inLastRoom"] = inLast is bool ? (object)(bool)inLast : null;
                    object atLast = SafeInvoke(d, "IsAtLastLevel");
                    dg["atLastLevel"] = atLast is bool ? (object)(bool)atLast : null;
                    object cleared = SafeInvoke(d, "IsDungeonCleared");
                    dg["cleared"] = cleared is bool ? (object)(bool)cleared : null;
                    object deact = SafeField(d, "m_Deactivated");
                    dg["deactivated"] = deact is bool ? (object)(bool)deact : null;
                }

                // driver status (only when the clear orchestrator is running).
                if (DungeonDriver.IsRunning)
                {
                    Dictionary<string, object> drv = new Dictionary<string, object>();
                    drv["running"] = true;
                    drv["lastError"] = DungeonDriver.LastError;
                    dg["driver"] = drv;
                }
                else if (DungeonDriver.LastError != null)
                {
                    Dictionary<string, object> drv = new Dictionary<string, object>();
                    drv["running"] = false;
                    drv["lastError"] = DungeonDriver.LastError;
                    dg["driver"] = drv;
                }
                else dg["driver"] = null;
            }
            catch (Exception e) { warnings.Add("dungeon: " + e.Message); }
            return dg;
        }

        // RoomInfo.m_EncounterObjects (List<string>) -> a JSON array of strings (the room's enemy/boss/chest set),
        // or null when the room is null, the field is missing, or the list is empty. Fully defensive.
        private static object ReadEncounterObjects(object room)
        {
            try
            {
                if (room == null) return null;
                IEnumerable seq = SafeField(room, "m_EncounterObjects") as IEnumerable;
                if (seq == null) return null;
                List<object> objs = new List<object>();
                foreach (object o in seq)
                    if (o != null) objs.Add(o.ToString());
                return objs.Count > 0 ? (object)objs : null;
            }
            catch { return null; }
        }

        // ============================================================ map ==============================

        private static object ReadMap(object hub, object glInstance, List<object> warnings)
        {
            try
            {
                // Prefer the current-turn cow (its hex defines the actionable tile + reachable set); fall back to
                // the first cow if the current player is not resolvable (e.g. mid start-up).
                object cow = CurrentTurnCow(hub, glInstance);
                if (cow == null) cow = FirstCow(hub);
                if (cow == null) return null;
                Dictionary<string, object> map = new Dictionary<string, object>();
                object hex = SafeProp(cow, "m_HexLand");
                if (hex == null) hex = SafeField(cow, "m_HexLand");
                if (hex != null)
                {
                    map["realmId"] = ToNullableInt(SafeInvoke(hex, "GetRealm"));
                    // tile big/small come from the HexLand itself (m_ParentIndex/m_Index); HexInfo has no
                    // m_HexBig/m_HexSmall. m_HexInfo.m_StageIndex gives the realm stage.
                    Dictionary<string, object> tile = new Dictionary<string, object>();
                    tile["big"] = ToNullableInt(SafeField(hex, "m_ParentIndex"));
                    tile["small"] = ToNullableInt(SafeField(hex, "m_Index"));
                    map["tile"] = tile;
                    object hexInfo = SafeField(hex, "m_HexInfo");
                    map["stageIndex"] = hexInfo != null ? ToNullableInt(SafeField(hexInfo, "m_StageIndex")) : null;
                    object poi = SafeField(hex, "m_POI");
                    map["hasPOI"] = poi != null;
                    if (poi != null)
                    {
                        object miniType = SafeField(poi, "m_MiniHexType");
                        map["poiType"] = miniType != null ? miniType.ToString() : null;
                    }
                    else map["poiType"] = null;

                    // reachableTiles: direct neighbors the party can travel to. Bounded by m_NeighborCount and
                    // gated on CanTravel(); cheap (<= 6 hexes), read-only.
                    map["reachableTiles"] = ReadReachableTiles(hex, warnings);
                }
                // objective: the active quest's destination hex (QuestLogicBase.m_Destination, a HexLandID).
                map["objective"] = ReadObjective(glInstance, warnings);
                return map;
            }
            catch (Exception e) { warnings.Add("map: " + e.Message); return null; }
        }

        private static object ReadReachableTiles(object hex, List<object> warnings)
        {
            List<object> tiles = new List<object>();
            try
            {
                object neighborsObj = SafeField(hex, "m_Neighbors");
                IList neighbors = neighborsObj as IList;
                if (neighbors == null) return tiles;
                int count = ToNullableInt(SafeField(hex, "m_NeighborCount")) ?? neighbors.Count;
                for (int i = 0; i < count && i < neighbors.Count; i++)
                {
                    object n = neighbors[i];
                    if (n == null) continue;
                    object canTravel = SafeInvoke(n, "CanTravel");
                    if (!(canTravel is bool) || !(bool)canTravel) continue;
                    Dictionary<string, object> t = new Dictionary<string, object>();
                    t["big"] = ToNullableInt(SafeField(n, "m_ParentIndex"));
                    t["small"] = ToNullableInt(SafeField(n, "m_Index"));
                    object poi = SafeField(n, "m_POI");
                    if (poi != null)
                    {
                        object miniType = SafeField(poi, "m_MiniHexType");
                        if (miniType != null) t["poiType"] = miniType.ToString();
                    }
                    tiles.Add(t);
                }
            }
            catch (Exception e) { warnings.Add("map.reachableTiles: " + e.Message); }
            return tiles;
        }

        private static object ReadObjective(object glInstance, List<object> warnings)
        {
            try
            {
                // Use the AUTHORITATIVE active quest (GameEventManager.Instance.GetCurrentQuest()), not
                // GameLogic.GetQuestByID(m_QuestID). glInstance is unused now but kept in the signature so the
                // caller wiring is undisturbed.
                object gem = StaticInstance("GameEventManager");
                if (gem == null) return null;
                object quest = SafeInvoke(gem, "GetCurrentQuest");
                if (quest == null) return null;
                object dest = SafeField(quest, "m_Destination");
                if (dest == null) return null;
                int? big = ToNullableInt(SafeField(dest, "m_BigIndex"));
                int? small = ToNullableInt(SafeField(dest, "m_SmallIndex"));
                if (!big.HasValue || !small.HasValue) return null;
                // HexLandID.NullHexLandID is (-1,-1); treat that as "no objective".
                if (big.Value < 0 && small.Value < 0) return null;
                Dictionary<string, object> obj = new Dictionary<string, object>();
                obj["big"] = big.Value;
                obj["small"] = small.Value;
                return obj;
            }
            catch (Exception e) { warnings.Add("map.objective: " + e.Message); return null; }
        }

        // Current-turn cow via FTKHub.GetCharacterOverworldByFID(GameLogic.m_CurrentPlayer). Null on any miss.
        private static object CurrentTurnCow(object hub, object glInstance)
        {
            try
            {
                if (hub == null || glInstance == null) return null;
                object fid = SafeField(glInstance, "m_CurrentPlayer");
                if (fid == null) return null;
                MethodInfo m = null;
                for (Type cur = hub.GetType(); cur != null && m == null; cur = cur.BaseType)
                    m = cur.GetMethod("GetCharacterOverworldByFID", Reflect.All, null,
                        new[] { fid.GetType() }, null);
                return m != null ? m.Invoke(hub, new[] { fid }) : null;
            }
            catch { return null; }
        }

        // One-arg invoke (the file's SafeInvoke is zero-arg only).
        private static object SafeInvoke2(object obj, string name, object arg)
        {
            if (obj == null) return null;
            try { return Reflect.Invoke(obj, name, arg); } catch { return null; }
        }

        // ============================================================ choices ==========================

        private static object ReadChoices(bool inSession, List<object> warnings, out string modalType)
        {
            modalType = null;
            List<object> choices = new List<object>();
            try
            {
                object ui = StaticInstance("FTKUI");
                // (1) global message: yes/no or OK
                if (ui != null)
                {
                    object gm = Reflect.GetField(ui, "m_GlobalMessage");
                    if (gm != null)
                    {
                        object choicePanel = Reflect.GetField(gm, "m_ChoiceButtonPanel");
                        bool choiceActive = GameObjectActive(choicePanel);
                        bool fullyOpen = ToBool(Reflect.GetField(gm, "m_IsFullyOpened"));
                        if (choiceActive && fullyOpen)
                        {
                            choices.Add(Choice(0, "Yes", "yesno", "yes"));
                            choices.Add(Choice(1, "No", "yesno", "no"));
                            return choices;
                        }
                        object msgPanel = Reflect.GetField(gm, "m_MessagePanel");
                        if (GameObjectActive(msgPanel))
                        {
                            choices.Add(Choice(0, "OK", "ok", "ok"));
                            return choices;
                        }
                    }

                    // (2) reward menu
                    object rewardMenu = Reflect.GetField(ui, "m_ChooseRewardMenu");
                    if (rewardMenu != null)
                    {
                        object allButtons = Reflect.GetField(rewardMenu, "m_AllButtons");
                        IEnumerable seq = allButtons as IEnumerable;
                        if (seq != null)
                        {
                            foreach (object btn in seq)
                            {
                                if (btn == null) continue;
                                object info = SafeField(btn, "m_Info");
                                int idx = ToNullableInt(SafeField(info, "m_Index")) ?? choices.Count;
                                object textObj = SafeField(btn, "m_Text");
                                string label = SafeString(textObj, "text") ?? null;
                                object func = SafeField(info, "m_Func");
                                choices.Add(Choice(idx, label, "reward", func != null ? func.ToString() : null));
                            }
                            if (choices.Count > 0) return choices;
                        }
                    }
                }

                // (3) system dialog. Checked INDEPENDENTLY of FTKUI (which is typically null at the title
                // screen), so the startup "For the King / Understood" panel is surfaced at the menu too.
                object sys = StaticInstance("uiSystemDialog");
                if (sys != null)
                {
                    object dialogRoot = SafeField(sys, "m_DialogRoot");
                    if (GameObjectActiveInHierarchy(sys) || GameObjectActiveInHierarchy(dialogRoot))
                    {
                        modalType = "system";
                        choices.Add(Choice(0, "Yes", "system", "yes"));
                        choices.Add(Choice(1, "No", "system", "no"));
                        return choices;
                    }
                }

                // (4) menu-phase startup modals on uiStartGame. Only meaningful at the title screen; both are
                // dismissed via the dismiss_dialog action.
                if (!inSession)
                {
                    object usg = StaticInstance("uiStartGame");
                    if (usg != null)
                    {
                        // (4a) "prepare to die" startup modal (m_PrepareToDie, a uiScreen). This is the real
                        // fresh-launch confirm panel; dismissed via uiStartGame.OnPrepareToDie().
                        object prepare = SafeField(usg, "m_PrepareToDie");
                        if (prepare != null && GameObjectActiveInHierarchy(prepare))
                        {
                            modalType = "prepareToDie";
                            choices.Add(Choice(0, "Understood", "prepareToDie", "understood"));
                            return choices;
                        }

                        // (4b) beta disclaimer ("Understood") via uiStartGame.OnBetaDisclaimer().
                        object beta = SafeField(usg, "m_BetaDisclamer"); // game's misspelling
                        if (beta != null && GameObjectActiveInHierarchy(beta))
                        {
                            modalType = "betaDisclaimer";
                            choices.Add(Choice(0, "Understood", "betaDisclaimer", "understood"));
                            return choices;
                        }
                    }
                }
            }
            catch (Exception e) { warnings.Add("choices: " + e.Message); }

            // modalType from MessageCoordinator.CurrentMessageType()
            try
            {
                object mc = StaticInstance("MessageCoordinator");
                if (mc != null)
                {
                    object mt = Reflect.Invoke(mc, "CurrentMessageType");
                    if (mt != null && mt.ToString() != "None")
                        modalType = mt.ToString();
                }
            }
            catch (Exception e) { warnings.Add("modalType: " + e.Message); }

            return choices;
        }

        private static Dictionary<string, object> Choice(int index, string label, string kind, string func)
        {
            Dictionary<string, object> c = new Dictionary<string, object>();
            c["index"] = index;
            c["label"] = label;
            c["kind"] = kind;
            c["func"] = func;
            return c;
        }

        // ============================================================ helpers ==========================

        // victoryShowing: FTKHelp.CreditScreen.Instance.m_ShowingInEndGame. Resolved by full type name (the type
        // is in the FTKHelp namespace, so a bare "CreditScreen" lookup misses). The Instance getter lazily walks
        // FTKUI.Instance.m_MainCanvas and can throw before the UI exists, so the whole probe is swallowed to false.
        private static object ReadVictoryShowing(List<object> warnings)
        {
            try
            {
                Type t = AccessTools.TypeByName("FTKHelp.CreditScreen");
                if (t == null) t = AccessTools.TypeByName("CreditScreen");
                if (t == null) return false;
                PropertyInfo pi = t.GetProperty("Instance", Reflect.All);
                object inst = pi != null ? pi.GetValue(null, null) : null;
                if (inst == null) return false;
                object showing = Reflect.GetField(inst, "m_ShowingInEndGame");
                return showing is bool ? (object)(bool)showing : false;
            }
            catch (Exception e) { warnings.Add("victoryShowing: " + e.Message); return false; }
        }

        private static object ReadAllDead(object hub, List<object> warnings)
        {
            try
            {
                if (hub == null) return null;
                // FTKHub has no AreAllCharacterDead. The verified all-party-dead check is IsPartyDead()
                // (true only when every member is dead with no sanctum). AnyDeadPlayers() also exists but
                // means "at least one down", which is not the defeat condition.
                object res = Reflect.Invoke(hub, "IsPartyDead");
                if (res is bool) return (bool)res;
            }
            catch (Exception e) { warnings.Add("allDead: " + e.Message); }
            return null;
        }

        private static string DecidePhase(bool inSession, bool inCombat, bool modalOpen, bool victoryArmed,
                                          bool? questComplete, bool? allDead, int partyCount)
        {
            if (!inSession) return "menu";
            if (victoryArmed) return "victory";
            // Defeat ONLY when there is a real party and every member is dead. During session startup the session
            // is open (m_GameStarted) but FTKHub.m_CharacterOverworlds is still empty, and IsPartyDead() can read
            // true over an empty party; that is start-up, not a wipe, so it must not be reported as "defeat".
            if (partyCount > 0 && allDead == true) return "defeat";
            if (inCombat) return "combat";
            if (modalOpen) return "modal";
            return "overworld";
        }

        private static object FirstCow(object hub)
        {
            if (hub == null) return null;
            IEnumerable cows = Reflect.GetField(hub, "m_CharacterOverworlds") as IEnumerable;
            if (cows == null) return null;
            foreach (object cow in cows)
                if (cow != null) return cow;
            return null;
        }

        private static Dictionary<string, object> FidDict(object fid)
        {
            if (fid == null) return null;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["turnIndex"] = ToNullableInt(Reflect.GetField(fid, "m_TurnIndex"));
            d["photonId"] = ToNullableInt(Reflect.GetField(fid, "m_PhotonID"));
            return d;
        }

        private static Dictionary<string, object> Vec3Dict(object v)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["x"] = ToNullableDouble(Reflect.GetField(v, "x"));
            d["y"] = ToNullableDouble(Reflect.GetField(v, "y"));
            d["z"] = ToNullableDouble(Reflect.GetField(v, "z"));
            return d;
        }

        /// <summary>Resolve a game type by simple name and read its static Instance property/field.</summary>
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

        private static string SafeString(object obj, string name)
        {
            object v = SafeField(obj, name);
            if (v == null) v = SafeProp(obj, name);
            return v as string;
        }

        private static bool ToBool(object o)
        {
            return o is bool && (bool)o;
        }

        private static int? ToNullableInt(object o)
        {
            if (o == null) return null;
            try
            {
                if (o is int) return (int)o;
                if (o is Enum) return Convert.ToInt32(o);
                if (o is long || o is short || o is byte || o is uint || o is ushort || o is sbyte)
                    return Convert.ToInt32(o);
                if (o is bool) return ((bool)o) ? 1 : 0;
            }
            catch { }
            return null;
        }

        private static double? ToNullableDouble(object o)
        {
            if (o == null) return null;
            try
            {
                if (o is float) return (double)(float)o;
                if (o is double) return (double)o;
                if (o is int || o is long || o is short) return Convert.ToDouble(o);
            }
            catch { }
            return null;
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

        private static object TryGet(Func<object> f, List<object> warnings, string label)
        {
            try { return f(); }
            catch (Exception e) { warnings.Add(label + ": " + e.Message); return null; }
        }
    }
}
