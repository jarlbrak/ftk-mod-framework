using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

// This belongs only to the isolated runtime-test plugin.  It deliberately
// drives the one real Create Game callback and then observes its native
// continuation; it never constructs preview UI or chooses a player class.
public sealed partial class RuntimeModelTest
{
    sealed class NativeCreateCharacterScreenContext
    {
        public uiStartGame menu;
        public StartGameFE.GameConfig config;
        public GameLogic logic;
        public JObject preflight;
    }

    // This carries the same live references that the mutating route pins after
    // eligibility has been established.  The inspection itself only reads the
    // native menu graph, so it is also safe to expose as a diagnostic command.
    sealed class NativeCreateCharacterScreenInspection
    {
        public uiStartGame menu;
        public StartGameFE.GameConfig config;
        public GameLogic logic;
        public bool eligible;
        public JObject report;
    }

    static object NativeCreateField(object owner, string name)
    {
        if (owner == null) throw new InvalidOperationException("Native owner is unavailable: " + name);
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Members | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(owner);
        }
        throw new MissingFieldException(owner.GetType().FullName, name);
    }

    static string NativeCreateStringField(object owner, string name)
    {
        object value = NativeCreateField(owner, name);
        return value == null ? null : Convert.ToString(value);
    }

    static bool NativeCreatePhotonOfflineMode()
    {
        Type photon = typeof(GameLogic).Assembly.GetType("PhotonNetwork", true);
        PropertyInfo property = photon.GetProperty("offlineMode", Statics);
        if (property == null || property.PropertyType != typeof(bool))
            throw new MissingMemberException(photon.FullName, "offlineMode");
        return (bool)property.GetValue(null, null);
    }

    static bool NativeCreateMapReady()
    {
        Type flowType = typeof(GameLogic).Assembly.GetType("GameFlowMC", true);
        object flow = Instance(flowType);
        if (flow == null) return false;
        object value = NativeCreateField(flow, "m_IsMapReady");
        if (!(value is bool)) throw new InvalidOperationException("Native map-ready flag is not Boolean.");
        return (bool)value;
    }

    static bool NativeCreateRootActive(uiStartGame menu)
    {
        object characterRoot = NativeCreateField(menu, "m_CreateCharacterRoot");
        if (characterRoot == null) return false;
        object uiRoot = NativeCreateField(characterRoot, "m_UIRoot");
        GameObject gameObject = uiRoot as GameObject;
        if (gameObject == null)
        {
            Component component = uiRoot as Component;
            if (component == null) throw new InvalidOperationException("Native character-create root is not a scene object.");
            gameObject = component.gameObject;
        }
        return gameObject.activeInHierarchy;
    }

    static JObject NativeCreateObjectState(UnityEngine.Object value)
    {
        if (value == null)
            return new JObject {
                {"present", false},
                {"instanceId", 0},
                {"hasGameObject", false},
                {"activeInHierarchy", false},
            };

        GameObject gameObject = value as GameObject;
        if (gameObject == null)
        {
            Component component = value as Component;
            gameObject = component == null ? null : component.gameObject;
        }
        return new JObject {
            {"present", true},
            {"instanceId", value.GetInstanceID()},
            {"hasGameObject", gameObject != null},
            {"activeInHierarchy", gameObject != null && gameObject.activeInHierarchy},
        };
    }

    NativeCreateCharacterScreenInspection InspectNativeCreateCharacterScreen()
    {
        uiStartGame menu = uiStartGame.Instance;
        StartGameFE.GameConfig config = menu == null ? null : menu.m_GameConfig;
        GameLogic logic = GameLogic.Instance;
        uiScreen currentScreen = uiScreen.gCurrent;
        object definitionPreview = config == null ? null : config.GetCurrentGameDefPreview();

        bool menuPresent = menu != null;
        bool configPresent = config != null;
        bool logicPresent = logic != null;
        bool currentScreenMatchesConfig = configPresent && currentScreen == config;
        bool configActive = configPresent && config.gameObject != null && config.gameObject.activeInHierarchy;
        bool createGameButtonPresent = configPresent && config.m_CreateGame != null;
        bool createGameButtonActive = createGameButtonPresent && config.m_CreateGame.gameObject != null
            && config.m_CreateGame.gameObject.activeInHierarchy;
        bool createGameButtonInteractable = createGameButtonPresent && config.m_CreateGame.interactable;
        bool isResume = configPresent && config.m_IsResume;
        bool useOnlineSinglePlayer = menuPresent && menu.m_UseOnlineSinglePlayer;
        bool isSinglePlayer = logicPresent && logic.IsSinglePlayer();
        bool definitionPresent = definitionPreview != null;
        bool gameStarted = menuPresent && menu.m_GameStarted;
        bool createUiListPresent = menuPresent && menu.m_CreateUIs != null;
        int createUiCount = createUiListPresent ? menu.m_CreateUIs.Count : 0;
        bool noCreateUis = createUiListPresent && createUiCount == 0;
        bool eligible = menuPresent && configPresent && logicPresent && currentScreenMatchesConfig
            && configActive && createGameButtonPresent && createGameButtonActive && createGameButtonInteractable
            && !isResume && !useOnlineSinglePlayer && isSinglePlayer && definitionPresent && !gameStarted
            && noCreateUis;

        JObject report = new JObject {
            {"readOnly", true},
            {"eligible", eligible},
            {"menu", NativeCreateObjectState(menu)},
            {"config", NativeCreateObjectState(config)},
            {"logic", NativeCreateObjectState(logic)},
            {"currentScreen", NativeCreateObjectState(currentScreen)},
            {"createGameButton", NativeCreateObjectState(createGameButtonPresent ? config.m_CreateGame : null)},
            {"values", new JObject {
                {"isResume", configPresent ? new JValue(isResume) : new JValue((object)null)},
                {"useOnlineSinglePlayer", menuPresent ? new JValue(useOnlineSinglePlayer) : new JValue((object)null)},
                {"isSinglePlayer", logicPresent ? new JValue(isSinglePlayer) : new JValue((object)null)},
                {"gameDefinitionPresent", definitionPresent},
                {"m_GameStarted", menuPresent ? new JValue(gameStarted) : new JValue((object)null)},
                {"createUiCount", createUiListPresent ? new JValue(createUiCount) : new JValue((object)null)},
            }},
            {"checks", new JObject {
                {"menuPresent", menuPresent},
                {"configPresent", configPresent},
                {"logicPresent", logicPresent},
                {"currentScreenMatchesConfig", currentScreenMatchesConfig},
                {"configActive", configActive},
                {"createGameButtonPresent", createGameButtonPresent},
                {"createGameButtonActive", createGameButtonActive},
                {"createGameButtonInteractable", createGameButtonInteractable},
                {"notResume", configPresent && !isResume},
                {"notOnlineSinglePlayer", menuPresent && !useOnlineSinglePlayer},
                {"isSinglePlayer", isSinglePlayer},
                {"gameDefinitionPresent", definitionPresent},
                {"notGameStarted", menuPresent && !gameStarted},
                {"createUiListPresent", createUiListPresent},
                {"noCreateUis", noCreateUis},
            }},
        };
        return new NativeCreateCharacterScreenInspection {
            menu = menu,
            config = config,
            logic = logic,
            eligible = eligible,
            report = report,
        };
    }

    JObject NativeCreateCharacterPreflight(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        NativeCreateCharacterScreenInspection inspection = InspectNativeCreateCharacterScreen();
        inspection.report["ok"] = true;
        inspection.report["status"] = "native_create_character_preflight";
        return inspection.report;
    }

    static JObject NativeCreateCandidate(uiQuickPlayerCreate candidate)
    {
        CharacterEventListener avatar = candidate == null ? null : candidate.m_Avatar;
        bool sceneOwner = candidate != null && SceneOwner(candidate);
        bool active = sceneOwner && candidate.gameObject.activeInHierarchy;
        bool avatarActive = avatar != null && SceneOwner(avatar) && avatar.gameObject.activeInHierarchy;
        bool reciprocal = avatar != null && avatar.m_uiQuickPlayerCreate == candidate;
        bool nativePedestal = candidate != null && candidate.m_CharacterPos != null && avatar != null
            && avatar.transform.parent == candidate.m_CharacterPos;
        return new JObject {
            {"ownerInstanceId", candidate == null ? 0 : candidate.GetInstanceID()},
            {"classId", candidate == null ? -1 : candidate.m_ClassID},
            {"skinType", candidate == null ? null : new JValue((int)candidate.m_SkinType)},
            {"turnIndex", candidate == null ? -1 : candidate.m_TurnIndex},
            {"active", active},
            {"avatarInstanceId", avatar == null ? 0 : avatar.GetInstanceID()},
            {"avatarActive", avatarActive},
            {"reciprocalPreviewReference", reciprocal},
            {"parentIsNativePedestal", nativePedestal},
            {"classLabel", candidate == null || candidate.m_PlayerClass == null ? null : candidate.m_PlayerClass.text},
        };
    }

    static bool NativeCreateCandidateReady(JObject candidate)
    {
        return (bool)candidate["active"] && (bool)candidate["avatarActive"]
            && (bool)candidate["reciprocalPreviewReference"] && (bool)candidate["parentIsNativePedestal"];
    }

    static JArray NativeCreateCandidates(uiStartGame menu, out bool hasReadyCandidate)
    {
        if (menu.m_CreateUIs == null) throw new InvalidOperationException("Native preview list is unavailable.");
        if (menu.m_CreateUIs.Count > 8) throw new InvalidOperationException("Native preview list exceeds 8.");
        JArray result = new JArray();
        hasReadyCandidate = false;
        foreach (uiQuickPlayerCreate candidate in menu.m_CreateUIs)
        {
            JObject row = NativeCreateCandidate(candidate);
            result.Add(row);
            hasReadyCandidate |= NativeCreateCandidateReady(row);
        }
        return result;
    }

    NativeCreateCharacterScreenContext ResolveNativeCreateCharacterScreen(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        RequireSinglePlayer();

        NativeCreateCharacterScreenInspection inspection = InspectNativeCreateCharacterScreen();
        uiStartGame menu = inspection.menu;
        StartGameFE.GameConfig config = inspection.config;
        GameLogic logic = inspection.logic;
        if (!inspection.eligible)
            throw new InvalidOperationException("Exact active native offline Create Game screen is required.");

        JObject core = PreviewCoreIdentity();
        JObject preflight = inspection.report;
        preflight["root"] = root;
        preflight["session"] = sessionId;
        preflight["menuInstanceId"] = menu.GetInstanceID();
        preflight["configInstanceId"] = config.GetInstanceID();
        preflight["currentScreenInstanceId"] = uiScreen.gCurrent.GetInstanceID();
        preflight["createGameButtonInstanceId"] = config.m_CreateGame.GetInstanceID();
        preflight["gameDefinitionName"] = menu.m_GameDefName;
        preflight["previewSaveFileName"] = NativeCreateStringField(config.GetCurrentGameDefPreview(), "m_SaveFileName");
        preflight["gameMode"] = logic.m_GameMode.ToString();
        preflight["isResume"] = config.m_IsResume;
        preflight["useOnlineSinglePlayer"] = menu.m_UseOnlineSinglePlayer;
        preflight["actualMaxCharCount"] = menu.m_ActualMaxCharCount;
        preflight["createUiCount"] = menu.m_CreateUIs.Count;
        preflight["core"] = core;
        preflight["helper"] = ScaleIdentity(typeof(RuntimeModelTest).Assembly);
        preflight["gameAssembly"] = ScaleIdentity(typeof(uiStartGame).Assembly);
        return new NativeCreateCharacterScreenContext { menu = menu, config = config, logic = logic, preflight = preflight };
    }

    bool NativeCreateSameRoute(NativeCreateCharacterScreenContext context)
    {
        return uiStartGame.Instance == context.menu && context.menu.m_GameConfig == context.config
            && GameLogic.Instance == context.logic && context.logic.IsSinglePlayer();
    }

    IEnumerator NativeCreateCharacterScreen(string id, NativeCreateCharacterScreenContext context)
    {
        Exception failure = null;
        JObject success = null;
        int waitedFrames = 0;
        try
        {
            // This is the exact public callback wired to the live Create Game button.
            context.config.OnStartGame();
            if (!NativeCreateSameRoute(context) || context.logic.GetGameDef() == null || !NativeCreatePhotonOfflineMode())
                throw new InvalidOperationException("Native Create Game callback did not enter the expected offline route.");
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        while (failure == null && success == null && waitedFrames < 3600)
        {
            try
            {
                if (!NativeCreateSameRoute(context))
                    throw new InvalidOperationException("Native menu identities changed during Create Game continuation.");
                bool mapReady = NativeCreateMapReady();
                bool rootActive = NativeCreateRootActive(context.menu);
                bool candidateReady;
                JArray candidates = NativeCreateCandidates(context.menu, out candidateReady);
                if (mapReady && rootActive && candidateReady)
                {
                    success = new JObject {
                        {"ok", true},
                        {"status", "native_create_game_reached_actual_character_create"},
                        {"method", "StartGameFE.GameConfig.OnStartGame"},
                        {"preflight", context.preflight},
                        {"waitedFrames", waitedFrames},
                        {"mapReady", mapReady},
                        {"characterCreateRootActive", rootActive},
                        {"candidates", candidates},
                        {"scope", "Invoked only the active native Create Game callback. The game created its own room, map, character-create UI, preview owners, and initial class choices; this helper did not construct or select any avatar."},
                    };
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            if (failure == null && success == null)
            {
                waitedFrames++;
                yield return null;
            }
        }

        if (success != null)
            Finish(id, success);
        else if (failure != null)
        {
            Logger.LogError("MODEL TEST NATIVE CREATE CHARACTER SCREEN FAILED: " + failure);
            Finish(id, new JObject {
                {"ok", false},
                {"status", "native_create_game_character_create_failed"},
                {"preflight", context.preflight},
                {"error", failure.ToString()},
            });
        }
        else
            Finish(id, new JObject {
                {"ok", false},
                {"status", "native_create_game_character_create_timeout"},
                {"preflight", context.preflight},
                {"error", "Native room, map, and character-create route did not yield an actual preview owner within 3600 frames."},
            });
        busy = false;
    }

    void StartNativeCreateCharacterScreen(string id, JObject command)
    {
        NativeCreateCharacterScreenContext context = ResolveNativeCreateCharacterScreen(command);
        busy = true;
        StartCoroutine(NativeCreateCharacterScreen(id, context));
    }
}
