using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// The WHOLE async title-screen -> in-world flow for a SOLO <c>start_run</c>, run as a Unity coroutine on
    /// <see cref="BridgeHost"/> so it can poll FSM/Photon/coroutine-driven game state across frames without
    /// blocking the HTTP thread (net35: no Task/async). <c>start_run</c> only validates preconditions (preview
    /// injected, not already in-world) and then calls <see cref="Arm"/>; the agent polls <c>/state</c> until
    /// <c>phase=='overworld'</c>.
    ///
    /// WHY THE WHOLE FLOW LIVES HERE (the fix): the new-game path is PlayMaker-FSM + Photon + coroutine driven.
    /// <c>uiStartGame.OnPrepareToDie()</c> sends the FSM event "NewGameLoadScene" which is an ASYNC scene/FSM
    /// transition. The earlier code configured the game and called <c>CreateOfflineRoom</c> in the SAME
    /// synchronous block, while the FSM was still mid-transition, so the offline room-join callback
    /// (<c>uiStartGame.OnJoinedRoom</c> -> FSM "PHOTON / JOINED ROOM" -> <c>_OnJoinedRoom</c> -> FSM "Continue")
    /// landed in a state with no transition for it and was dropped: <c>ShowCreateCharacter</c> never ran, so
    /// <c>GameFlowMC.m_IsMapReady</c> stayed false. The FSM ONLY routes the room-join correctly from the
    /// new-game configuration state. We therefore drive the game's REAL supported flow and WAIT between each
    /// async step:
    ///   1. dismiss the startup modals (PrepareToDie / beta / system).
    ///   2. wait until the FSM settles on the START PAGE (uiScreen.gCurrent == uiStartGame.m_MainScreen).
    ///   3. m_MainScreen.OnNewGame() -> FSM "NewGame" -> ShowGameConfig() -> m_GameConfig.Show(); wait until the
    ///      GameConfig screen is current (gCurrent == m_GameConfig). NOW the FSM is in the state whose
    ///      room-join transition reaches ShowCreateCharacter.
    ///   4. select our adventure (GameConfig.OnChangeValueGameDef(key)) then replicate GameConfig.OnStartGame's
    ///      SP+offline branch by direct calls: SetActiveGameDef(preview.GetNewGameDefInstance()),
    ///      m_GameDefName=key, difficulty, m_GameMode=SinglePlayer, m_UseOnlineSinglePlayer=false,
    ///      m_ActualMaxCharCount=1 (set AFTER Show, which resets it to gMaxPlayers), CreateOfflineRoom(...).
    ///   5. wait for create-UIs + generated map (GameFlowMC.m_IsMapReady && m_CreateUIs.Count>=1).
    ///   5b. ASSIGN PHOTON IDS (the COW fix): call AssignPhotonID(PhotonNetwork.player.ID, None, 0, false) on
    ///      each create UI so m_PhotonID stops being -1 BEFORE EnterFahrul. Without this StartGame's CreatePlayer
    ///      no-ops (see below).
    ///   6. ready the hero (RandomClass + SetPlayerReady), wait GetAllPlayersReady().
    ///   7. EnterFahrul(); wait m_GameStarted.
    ///   8. advance the IntroStory popup (m_StoryIntroCycle.Next()) until in-world.
    ///
    /// All game access is via Core.Reflect / AccessTools and fully guarded: a missing/renamed member ends the
    /// run with a named-step failure logged to <see cref="Plugin.Log"/>, never a throw into the game. C# forbids
    /// <c>yield</c> inside try/catch, so each frame's risky work lives in a helper that returns a status and the
    /// coroutine only yields between those calls.
    /// </summary>
    // RE NOTE (re-verified via ilspycmd, Assembly-CSharp, Jun 2026):
    // - uiScreen.gCurrent (public static field) = the screen most recently Show()n; the FSM-settle gate.
    // - MainScreen.OnNewGame(): sets m_GameConfig.m_IsResume=false; m_FSM.SendEvent("NewGame").
    // - GameConfig.Show(): resets uiStartGame.m_ActualMaxCharCount = GameFlowMC.gMaxPlayers (=3), populates
    //   _selectedGameDefPreview, sets m_IsResume default. So set m_ActualMaxCharCount=1 AFTER Show.
    // - GameConfig.OnChangeValueGameDef(string): selects an adventure (sets uiStartGame.m_GameDefName).
    // - GameConfig.OnStartGame() SP+offline branch (m_UseOnlineSinglePlayer==false, !m_IsResume):
    //     SetActiveGameDef(GetCurrentGameDefPreview().GetNewGameDefInstance());
    //     GameFlow.SetGameModeDifficulty(diff); GameLogic.m_GameMode=SinglePlayer;
    //     GameLogic.CreateOfflineRoom("OfflineSinglePlayer").
    // - CreateOfflineRoom -> PhotonNetwork.offlineMode=true; CreateRoom -> uiStartGame.OnJoinedRoom() ->
    //   FSM "PHOTON / JOINED ROOM" -> _OnJoinedRoom() -> (SP branch) FSM "Continue" -> SyncStats ->
    //   ShowCreateCharacterMC -> ShowCreateCharacter(m_GameDefName): SP+master auto CreateAllCreatePlayerUIs()
    //   + StartCoroutine(CreateMap()), which sets GameFlowMC.Instance.m_IsMapReady when generation completes.
    // - EnterFahrulRPC sets m_GameStarted=true; if GetGameDef().m_HasTextIntro the IntroStory popup is raised:
    //   MessageCoordinator.CoordinateMsg(MessageType.IntroStory) -> uiStartGame.m_StoryIntroCycle. Advancing it
    //   is uiStoryIntroCycle.Next() (public, no args) -> close callback -> FSM "Continue" -> StartGame.
    // - In-world: uiStartGame.m_GameStarted && FTKHub.Instance.m_CharacterOverworlds populated (COW.m_HexLand).
    //
    // RE NOTE (COW-creation fix, re-verified via ilspycmd Jun 2026; see ninum kb_7a612756):
    // - uiQuickPlayerCreate.m_PhotonID DEFAULTS TO -1 (the create-UI ctor sets m_PhotonID = -1). It is only set
    //   to a real id by AssignPhotonID(int,AssignDevice.Type,int,bool) -> AssignPhotonIDRPC, which the game runs
    //   exactly once, inside uiStartGame.WaitUntilPanningFinished (SP branch:
    //   createUI.AssignPhotonID(PhotonNetwork.player.ID, AssignDevice.Type.None, 0, _showUI:true)). That coroutine
    //   only runs AFTER FlashGeneratingMapCoroutine sees the SelectScreenCamera Animator reach state "complete"
    //   (the camera PAN). When driven programmatically the pan never settled, so m_PhotonID stayed -1.
    // - WHY THAT BREAKS COWS: StartGame() (master, after the fade) calls
    //   m_CreateUIs[0].CreatePlayer(0, gameDef.GetStartGamePOI_MCOnly().m_HexLand.GetHexLandID()). CreatePlayer's
    //   FIRST line is `bool flag = PhotonNetwork.player.ID == m_PhotonID;`. With m_PhotonID == -1 the local-player
    //   branch (which does PhotonNetwork.Instantiate of m_PlayerPrefab and registers the COW) is SKIPPED, and it
    //   instead RPC_Client("CreatePlayerRPC", new FTKPlayerID(0, -1), ...) -> an RPC to photon player -1, which
    //   never executes. No COW is instantiated, FTKHub.m_CharacterOverworlds stays empty, and EnterGameClientCR
    //   loops forever waiting for a COW's m_HexLand -> permanent black screen.
    // - THE FIX (this driver, step 5b): explicitly call AssignPhotonID(PhotonNetwork.player.ID,
    //   AssignDevice.Type.None, 0, _showUI:false) on each create UI before EnterFahrul. AssignPhotonID does
    //   RPCAllSelf -> PhotonTargets.All, which in offlineMode (CreateOfflineRoom) executes AssignPhotonIDRPC
    //   LOCALLY and synchronously, setting m_PhotonID. flag then becomes true and CreatePlayer instantiates the
    //   COW. This makes the driver independent of the camera-pan animation entirely.
    // - PhotonNetwork.player.ID: PhotonNetwork.player is a public static property (PhotonPlayer) and
    //   PhotonPlayer.ID is a public int property; both resolve in offlineMode (master = the only player).
    // - AssignDevice.Type enum order: None=0, Mouse=1, Controller=2, Online=3 (we pass None).
    internal static class StartRunDriver
    {
        // Frame budgets (~60fps). Map generation is the long one (tuned up: it timed out before).
        private const int SettleWaitFrames = 900;   // ~15s  (start page / GameConfig screen to appear)
        private const int MapWaitFrames = 1800;      // ~30s  (room-join routing + map generation)
        private const int ReadyWaitFrames = 600;     // ~10s
        private const int IntroWaitFrames = 1800;    // ~30s  (fades + StartGame chain + EnterGame COW wait)

        private static bool _running;
        private static string _adventureKey;

        public static bool IsRunning { get { return _running; } }

        /// <summary>
        /// Start the full continuation coroutine on the BridgeHost (must be called on the main thread). Returns
        /// false if already running (idempotent re-arm) or if no host is available. <paramref name="adventureKey"/>
        /// is the adventure save-file key (e.g. "HollowMire").
        /// </summary>
        public static bool Arm(string adventureKey)
        {
            if (_running) return false;
            BridgeHost host = BridgeHost.Instance;
            if (host == null)
            {
                Plugin.Log.LogError("[agent] start_run failed at arm: no BridgeHost (no session host)");
                return false;
            }
            _adventureKey = string.IsNullOrEmpty(adventureKey) ? "HollowMire" : adventureKey;
            _running = true;
            try { host.StartCoroutine(Drive()); }
            catch (Exception e)
            {
                _running = false;
                Plugin.Log.LogError("[agent] start_run failed at arm: " + e.Message);
                return false;
            }
            return true;
        }

        private static IEnumerator Drive()
        {
            // Idempotency inside the driver too: if we are already in-world, just finish.
            if (InWorld()) { Done(null); yield break; }

            // STEP 1: dismiss any startup modal (PrepareToDie / beta / system). OnPrepareToDie also kicks the FSM
            // "NewGameLoadScene" (async scene/FSM transition); we WAIT for it to settle in step 2.
            DismissStartupModals();

            // STEP 2: wait for the FSM to settle on the START PAGE (gCurrent == m_MainScreen). Do NOT proceed
            // while the FSM is mid-transition: that is exactly what dropped the room-join before.
            int frame = 0;
            while (frame < SettleWaitFrames && !ScreenIsCurrent("m_MainScreen"))
            {
                // Re-issue the dismiss every ~45 frames in case the modal re-armed or auth was still pending.
                if ((frame % 45) == 0) DismissStartupModals();
                frame++;
                yield return null;
            }
            if (!ScreenIsCurrent("m_MainScreen"))
            {
                Done("start_run failed at start-page: m_MainScreen never became current in budget");
                yield break;
            }

            // STEP 3: enter the new-game configuration state via the supported MainScreen button handler, which
            // sends the FSM "NewGame" event. Then wait for the GameConfig screen to become current: that is the
            // FSM state whose room-join transition reaches ShowCreateCharacter.
            if (!EnterNewGameConfig())
            {
                Done("start_run failed at new-game: MainScreen.OnNewGame() unavailable");
                yield break;
            }
            frame = 0;
            while (frame < SettleWaitFrames && !ScreenIsCurrent("m_GameConfig"))
            {
                frame++;
                yield return null;
            }
            if (!ScreenIsCurrent("m_GameConfig"))
            {
                Done("start_run failed at game-config: m_GameConfig never became current in budget");
                yield break;
            }

            // STEP 4: configure SOLO + create the offline room (mirror GameConfig.OnStartGame's SP branch) FROM
            // the GameConfig FSM state, so the offline room-join routes _OnJoinedRoom -> "Continue" ->
            // ShowCreateCharacter correctly. CreateOfflineRoom is async (fires the join callback on a later pump);
            // we wait in step 5.
            if (!ConfigureAndCreateRoom(out string configError))
            {
                Done("start_run failed at " + configError);
                yield break;
            }

            // STEP 5: wait for the create UIs + generated map. Generous budget: map-gen can take many seconds and
            // the room-join routing also happens in this window.
            frame = 0;
            while (frame < MapWaitFrames && !MapAndUisReady())
            {
                frame++;
                yield return null;
            }
            if (!MapAndUisReady())
            {
                Done("start_run failed at map-wait: m_IsMapReady/m_CreateUIs not ready in budget");
                yield break;
            }

            // STEP 5b: ASSIGN PHOTON IDS (the COW-creation fix). Each uiQuickPlayerCreate.m_PhotonID defaults to
            // -1; the game only sets it inside WaitUntilPanningFinished (after the camera pan completes), which we
            // do not depend on. If m_PhotonID is left at -1, StartGame's CreatePlayer(0,...) takes the
            // RPC-to-photon-player--1 branch and NEVER instantiates the COW -> empty party -> black screen forever.
            // AssignPhotonID does RPCAllSelf -> PhotonTargets.All, executed locally in offlineMode, so this is
            // synchronous and self-contained. Best-effort and idempotent (skips UIs already assigned).
            AssignPhotonIdsToHeroes();

            // STEP 6: ready the hero(es). RandomClass() FIRST guarantees a usable/unlocked class id (so
            // SetPlayerReady()'s IsUnlock check passes and m_IsReady actually sets), then SetPlayerReady().
            ReadyAllHeroes();
            frame = 0;
            while (frame < ReadyWaitFrames && !AllPlayersReady())
            {
                frame++;
                // Re-issue assignment+ready each ~30 frames in case a slot appeared late (a late UI would still
                // have m_PhotonID=-1, which would break its COW even though it can be readied).
                if ((frame % 30) == 0) { AssignPhotonIdsToHeroes(); ReadyAllHeroes(); }
                yield return null;
            }
            if (!AllPlayersReady())
            {
                Done("start_run failed at ready-wait: GetAllPlayersReady() never true");
                yield break;
            }

            // STEP 7: enter the world. Sets m_GameStarted via EnterFahrulRPC; because the D1 gamedef sets
            // m_HasTextIntro=true, an IntroStory popup is raised by MessageCoordinator before StartGame runs.
            if (!EnterFahrul())
            {
                Done("start_run failed at enter-world: EnterFahrul() unavailable");
                yield break;
            }

            // STEP 8: advance the intro story (uiStoryIntroCycle.Next() per page) until in-world. The intro is
            // NOT the FTKUI global message; it is uiStartGame.m_StoryIntroCycle (ShowStory adds one body page;
            // Next() -> close callback -> FSM "Continue" -> StartGame). Keep advancing while the story GameObject
            // is active; success when m_GameStarted && party populated. Granular per-second logging so a stall
            // here pinpoints the exact sub-step (intro still open? StartGame run yet? COWs created yet?).
            frame = 0;
            while (frame < IntroWaitFrames && !InWorld())
            {
                AdvanceIntroOnce();
                if ((frame % 60) == 0) LogEnterWorldProgress(frame);
                frame++;
                yield return null;
            }

            if (InWorld())
            {
                LogEnterWorldProgress(frame);
                Done(null);
            }
            else
            {
                LogEnterWorldProgress(frame);
                Done("start_run failed at enter-world: not in-world within budget");
            }
        }

        // ------------------------------------------------------------- step actions ---------------------

        // STEP 1: dismiss the startup modals exactly like ActionExecutor.DismissDialog, but inlined here so the
        // driver owns its own flow. System dialog first, then PrepareToDie (the fresh-launch confirm, whose
        // OnPrepareToDie kicks FSM "NewGameLoadScene"), then the beta disclaimer.
        private static void DismissStartupModals()
        {
            try
            {
                object sys = StaticInstance("uiSystemDialog");
                if (sys != null)
                {
                    object dialogRoot = SafeField(sys, "m_DialogRoot");
                    if (GameObjectActiveInHierarchy(sys) || GameObjectActiveInHierarchy(dialogRoot))
                    {
                        SafeInvoke(sys, "OnYes");
                        return;
                    }
                }

                object usg = StaticInstance("uiStartGame");
                if (usg == null) return;

                object prepare = SafeField(usg, "m_PrepareToDie");
                if (prepare != null && GameObjectActiveInHierarchy(prepare))
                {
                    SafeInvoke(usg, "OnPrepareToDie");
                    return;
                }

                object beta = SafeField(usg, "m_BetaDisclamer");
                if (beta != null && GameObjectActiveInHierarchy(beta))
                {
                    SafeInvoke(usg, "OnBetaDisclaimer");
                }
            }
            catch { /* dismiss is best-effort */ }
        }

        // STEP 3: drive the supported new-game button on the start screen. MainScreen.OnNewGame() sets
        // m_GameConfig.m_IsResume=false and sends FSM "NewGame". We resolve m_MainScreen off uiStartGame and
        // invoke OnNewGame() on it. Returns false only if the member chain is missing/renamed.
        private static bool EnterNewGameConfig()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return false;
                object mainScreen = Reflect.GetField(usg, "m_MainScreen");
                if (mainScreen == null) return false;
                MethodInfo m = FindMethod(mainScreen.GetType(), "OnNewGame");
                if (m == null) return false;
                m.Invoke(mainScreen, null);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[agent] start_run new-game: " + e.Message);
                return false;
            }
        }

        // STEP 4: from the GameConfig FSM state, select our adventure then replicate GameConfig.OnStartGame's
        // SP+offline branch by direct calls. Writes a precise step name into <paramref name="error"/> on failure.
        private static bool ConfigureAndCreateRoom(out string error)
        {
            error = null;
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) { error = "uistartgame: uiStartGame.Instance null"; return false; }
                object gl = StaticInstance("GameLogic");
                if (gl == null) { error = "gamelogic: GameLogic.Instance null"; return false; }

                object preview = AdventureCache.GetPreviewSafe(_adventureKey);
                if (preview == null)
                {
                    error = "get-preview: adventure '" + _adventureKey + "' not injected";
                    return false;
                }

                // Select our adventure in the GameConfig screen (best-effort: keeps the screen's selected preview
                // and m_GameDefName consistent). Harmless if GameConfig is unavailable; the direct config below
                // is what actually drives the start.
                object gameConfig = SafeField(usg, "m_GameConfig");
                if (gameConfig != null)
                    SafeInvokeArgs(gameConfig, "OnChangeValueGameDef",
                        new[] { typeof(string) }, new object[] { _adventureKey });

                // SetActiveGameDef(preview.GetNewGameDefInstance())
                object def = SafeInvoke(preview, "GetNewGameDefInstance");
                if (def == null) { error = "gamedef-instance: GetNewGameDefInstance returned null"; return false; }
                SafeInvokeArgs(gl, "SetActiveGameDef", new[] { def.GetType() }, new[] { def });

                Reflect.SetField(usg, "m_GameDefName", _adventureKey);
                Reflect.SetField(usg, "m_UseOnlineSinglePlayer", false);

                // m_GameMode = SinglePlayer (enum order has SinglePlayer=0, so even a miss is safe).
                object spMode = ResolveEnumValue("GameLogic+GameMode", "SinglePlayer");
                if (spMode != null) Reflect.SetField(gl, "m_GameMode", spMode);

                // Difficulty: best-effort. preview.GetDiffTypeByIndex(0) -> GameFlow.SetGameModeDifficulty(diff).
                try
                {
                    object diff = SafeInvokeArgs(preview, "GetDiffTypeByIndex",
                        new[] { typeof(int) }, new object[] { 0 });
                    object gf = StaticInstance("GameFlow");
                    if (diff != null && gf != null)
                        SafeInvokeArgs(gf, "SetGameModeDifficulty", new[] { diff.GetType() }, new[] { diff });
                    else
                        Plugin.Log.LogWarning("[agent] start_run: difficulty unresolved; using default.");
                }
                catch (Exception e) { Plugin.Log.LogWarning("[agent] start_run difficulty: " + e.Message); }

                // Solo: exactly one create slot. Set AFTER GameConfig.Show (it reset this to gMaxPlayers=3).
                Reflect.SetField(usg, "m_ActualMaxCharCount", 1);

                // Create the offline room. Async: fires OnJoinedRoom on a later pump -> (now in the GameConfig
                // FSM state) _OnJoinedRoom -> "Continue" -> ShowCreateCharacter -> CreateAllCreatePlayerUIs +
                // CreateMap. We poll the result in step 5.
                SafeInvokeArgs(gl, "CreateOfflineRoom",
                    new[] { typeof(string) }, new object[] { "OfflineSinglePlayer" });
                return true;
            }
            catch (Exception e)
            {
                error = "configure: " + e.Message;
                return false;
            }
        }

        // ------------------------------------------------------------- step probes ----------------------

        // True iff uiScreen.gCurrent is the named screen field on uiStartGame (e.g. "m_MainScreen",
        // "m_GameConfig"). gCurrent is a public static field on uiScreen, set in Show(); this is our FSM-settle
        // gate (the FSM only routes the room-join from the GameConfig state).
        private static bool ScreenIsCurrent(string usgScreenField)
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return false;
                object screen = Reflect.GetField(usg, usgScreenField);
                if (screen == null) return false;
                Type uiScreenType = AccessTools.TypeByName("uiScreen");
                if (uiScreenType == null) return false;
                FieldInfo gCurrent = uiScreenType.GetField("gCurrent", Reflect.All);
                if (gCurrent == null) return false;
                object cur = gCurrent.GetValue(null);
                return ReferenceEquals(cur, screen);
            }
            catch { return false; }
        }

        private static bool MapAndUisReady()
        {
            try
            {
                object mc = StaticInstance("GameFlowMC");
                if (mc == null) return false;
                if (!ToBool(Reflect.GetField(mc, "m_IsMapReady"))) return false;
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return false;
                object createUis = Reflect.GetField(usg, "m_CreateUIs");
                int count = ListCount(createUis);
                return count >= 1;
            }
            catch { return false; }
        }

        // STEP 5b: assign the local photon id to every create UI so m_PhotonID stops being -1 before EnterFahrul.
        // This is the COW-creation fix: StartGame's CreatePlayer only instantiates+registers the COW when
        // PhotonNetwork.player.ID == createUI.m_PhotonID. We resolve PhotonNetwork.player.ID once and call
        // AssignPhotonID(id, AssignDevice.Type.None=0, 0, _showUI:false) per UI (RPCAllSelf runs it locally in
        // offlineMode). Idempotent: skips any UI whose m_PhotonID already equals the local id.
        private static void AssignPhotonIdsToHeroes()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return;
                IEnumerable seq = Reflect.GetField(usg, "m_CreateUIs") as IEnumerable;
                if (seq == null) return;

                int? localId = PhotonPlayerId();
                if (!localId.HasValue)
                {
                    Plugin.Log.LogWarning("[agent] start_run assign-photon: PhotonNetwork.player.ID unresolved; "
                                          + "create UIs may keep m_PhotonID=-1 and no COW will be created.");
                    return;
                }

                // AssignPhotonID(int, AssignDevice.Type, int, bool). Resolve AssignDevice.Type.None (ordinal 0).
                object devNone = ResolveAssignDeviceNone();
                if (devNone == null)
                {
                    Plugin.Log.LogWarning("[agent] start_run assign-photon: AssignDevice.Type.None unresolved.");
                    return;
                }
                Type devType = devNone.GetType();

                int assigned = 0;
                foreach (object qc in seq)
                {
                    if (qc == null) continue;
                    int? already = ToNullableInt(Reflect.GetField(qc, "m_PhotonID"));
                    if (already.HasValue && already.Value == localId.Value) continue;
                    SafeInvokeArgs(qc, "AssignPhotonID",
                        new[] { typeof(int), devType, typeof(int), typeof(bool) },
                        new object[] { localId.Value, devNone, 0, false });
                    assigned++;
                }
                Plugin.Log.LogInfo("[agent] start_run: assigned photon id " + localId.Value + " to "
                                   + assigned + " create UI(s).");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] start_run assign-photon: " + e.Message); }
        }

        private static void ReadyAllHeroes()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return;
                IEnumerable seq = Reflect.GetField(usg, "m_CreateUIs") as IEnumerable;
                if (seq == null) return;
                foreach (object qc in seq)
                {
                    if (qc == null) continue;
                    if (ToBool(Reflect.GetField(qc, "m_IsReady"))) continue;
                    SafeInvoke(qc, "RandomClass");   // pick a guaranteed-usable class first
                    SafeInvoke(qc, "SetPlayerReady"); // then ready (IsUnlock now passes)
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] start_run ready: " + e.Message); }
        }

        // Resolve PhotonNetwork.player.ID (offlineMode: master = the only player). PhotonNetwork.player is a
        // public static property returning PhotonPlayer; PhotonPlayer.ID is a public int property.
        private static int? PhotonPlayerId()
        {
            try
            {
                Type pn = AccessTools.TypeByName("PhotonNetwork");
                if (pn == null) return null;
                object player = null;
                PropertyInfo pp = pn.GetProperty("player", Reflect.All);
                if (pp != null) player = pp.GetValue(null, null);
                if (player == null)
                {
                    FieldInfo pf = pn.GetField("player", Reflect.All);
                    if (pf != null) player = pf.GetValue(null);
                }
                if (player == null) return null;
                object id = SafeProp(player, "ID");
                if (id == null) id = SafeField(player, "ID");
                if (id == null) id = SafeField(player, "actorID");
                return ToNullableInt(id);
            }
            catch { return null; }
        }

        private static object ResolveAssignDeviceNone()
        {
            // AssignDevice.Type is a nested enum; None has ordinal 0.
            Type t = AccessTools.TypeByName("AssignDevice+Type");
            if (t == null) t = AccessTools.TypeByName("AssignDevice/Type");
            if (t == null || !t.IsEnum) return null;
            try { return Enum.IsDefined(t, "None") ? Enum.Parse(t, "None") : Enum.ToObject(t, 0); }
            catch { return null; }
        }

        private static bool AllPlayersReady()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return false;
                object r = SafeInvoke(usg, "GetAllPlayersReady");
                return r is bool && (bool)r;
            }
            catch { return false; }
        }

        private static bool EnterFahrul()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return false;
                MethodInfo m = FindMethod(usg.GetType(), "EnterFahrul");
                if (m == null) return false;
                m.Invoke(usg, null);
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] start_run EnterFahrul: " + e.Message); return false; }
        }

        // Advance the IntroStory page if its cycle is open. uiStartGame.m_StoryIntroCycle.Next() drives the
        // close callback -> FSM "Continue". Best-effort each frame; harmless if the cycle is not open.
        private static void AdvanceIntroOnce()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return;
                object cycle = Reflect.GetField(usg, "m_StoryIntroCycle");
                if (cycle == null) return;
                if (!GameObjectActiveInHierarchy(cycle)) return;
                SafeInvoke(cycle, "Next");
            }
            catch { /* intro advance is best-effort */ }
        }

        private static bool InWorld()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null || !ToBool(Reflect.GetField(usg, "m_GameStarted"))) return false;
                object hub = StaticInstance("FTKHub");
                if (hub == null) return false;
                object cowsObj = Reflect.GetField(hub, "m_CharacterOverworlds");
                return ListCount(cowsObj) >= 1;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------- completion ------------------------

        private static void Done(string error)
        {
            _running = false;
            if (error == null)
                Plugin.Log.LogInfo("[agent] start_run: in-world. phase=overworld.");
            else
                Plugin.Log.LogError("[agent] " + error);
        }

        // ------------------------------------------------------------- reflection utils -----------------

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

        private static MethodInfo FindMethod(Type t, string name)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                MethodInfo m = cur.GetMethod(name, Reflect.All | BindingFlags.DeclaredOnly);
                if (m != null) return m;
            }
            return null;
        }

        private static object SafeField(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.GetField(obj, name); } catch { return null; }
        }

        private static object SafeInvoke(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.Invoke(obj, name); } catch { return null; }
        }

        // Overload-aware, null-on-miss invoke (e.g. SetActiveGameDef(GameDefinition), CreateOfflineRoom(string)).
        private static object SafeInvokeArgs(object obj, string name, Type[] sig, object[] args)
        {
            if (obj == null) return null;
            try { return Reflect.InvokeArgs(obj, name, sig, args); } catch { return null; }
        }

        private static object ResolveEnumValue(string typeName, string member)
        {
            Type t = AccessTools.TypeByName(typeName);
            if (t == null || !t.IsEnum) return null;
            try { return Enum.IsDefined(t, member) ? Enum.Parse(t, member) : Activator.CreateInstance(t); }
            catch { return null; }
        }

        private static int ListCount(object listLike)
        {
            if (listLike == null) return 0;
            ICollection coll = listLike as ICollection;
            if (coll != null) return coll.Count;
            int n = 0;
            IEnumerable seq = listLike as IEnumerable;
            if (seq != null) foreach (object o in seq) n++;
            return n;
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

        // Granular enter-world telemetry: one line with m_GameStarted, the FTKHub COW count, the create-UI photon
        // ids (so a -1 is obvious), whether the intro story cycle is open, and the current FSM gate (gCurrent
        // screen name). A stall after EnterFahrul then tells us the exact sub-step that is stuck.
        private static void LogEnterWorldProgress(int frame)
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                bool gameStarted = usg != null && ToBool(Reflect.GetField(usg, "m_GameStarted"));

                object hub = StaticInstance("FTKHub");
                int cows = hub != null ? ListCount(Reflect.GetField(hub, "m_CharacterOverworlds")) : -1;

                string photonIds = "?";
                if (usg != null)
                {
                    IEnumerable seq = Reflect.GetField(usg, "m_CreateUIs") as IEnumerable;
                    if (seq != null)
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        foreach (object qc in seq)
                        {
                            if (qc == null) continue;
                            int? pid = ToNullableInt(Reflect.GetField(qc, "m_PhotonID"));
                            bool ready = ToBool(Reflect.GetField(qc, "m_IsReady"));
                            if (sb.Length > 0) sb.Append(",");
                            sb.Append(pid.HasValue ? pid.Value.ToString() : "null");
                            sb.Append(ready ? "(r)" : "");
                        }
                        photonIds = sb.Length > 0 ? sb.ToString() : "none";
                    }
                }

                bool introOpen = false;
                if (usg != null)
                {
                    object cycle = Reflect.GetField(usg, "m_StoryIntroCycle");
                    introOpen = cycle != null && GameObjectActiveInHierarchy(cycle);
                }

                Plugin.Log.LogInfo("[agent] enter-world: frame=" + frame
                                   + " m_GameStarted=" + gameStarted
                                   + " cowCount=" + cows
                                   + " photonIds=[" + photonIds + "]"
                                   + " introOpen=" + introOpen
                                   + " gate=" + CurrentScreenName());
            }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] enter-world: log failed: " + e.Message); }
        }

        // Name of the uiStartGame screen field that uiScreen.gCurrent currently points at (the FSM gate), or a
        // descriptive token if none/unknown. Pure observation; never throws.
        private static string CurrentScreenName()
        {
            try
            {
                object usg = StaticInstance("uiStartGame");
                if (usg == null) return "no-usg";
                Type uiScreenType = AccessTools.TypeByName("uiScreen");
                if (uiScreenType == null) return "no-uiScreen";
                FieldInfo gCurrent = uiScreenType.GetField("gCurrent", Reflect.All);
                if (gCurrent == null) return "no-gCurrent";
                object cur = gCurrent.GetValue(null);
                if (cur == null) return "null";
                string[] names = { "m_MainScreen", "m_GameConfig", "m_PrepareToDie", "m_CreateCharacterRoot" };
                foreach (string n in names)
                {
                    object screen = SafeField(usg, n);
                    if (screen != null && ReferenceEquals(cur, screen)) return n;
                }
                return cur.GetType().Name;
            }
            catch { return "err"; }
        }

        private static bool GameObjectActiveInHierarchy(object component)
        {
            try
            {
                if (component == null) return false;
                object go = null;
                for (Type cur = component.GetType(); cur != null && go == null; cur = cur.BaseType)
                {
                    PropertyInfo pi = cur.GetProperty("gameObject", Reflect.All | BindingFlags.DeclaredOnly);
                    if (pi != null && pi.CanRead) go = pi.GetValue(component, null);
                }
                if (go == null) go = component;
                object active = SafeInvoke(go, "get_activeInHierarchy");
                return active is bool && (bool)active;
            }
            catch { return false; }
        }
    }
}
