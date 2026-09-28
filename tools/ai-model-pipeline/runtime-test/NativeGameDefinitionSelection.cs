using System;
using System.Collections.Generic;
using System.Reflection;
using StartGameFE;
using GameCache;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

// This is a scratch-only bridge to the game's existing adventure-row callback.
// It never writes PlayerPrefs directly or constructs native menu controls.
public sealed partial class RuntimeModelTest
{
    sealed class NativeGameDefinitionSelectionContext
    {
        public uiStartGame menu;
        public GameConfig config;
        public GameLogic logic;
        public GameDefButton button;
        public string saveFileName;
        public string token;
    }

    bool nativeGameDefinitionSelectionConsumed;
    NativeGameDefinitionSelectionContext nativeGameDefinitionSelection;

    static string NativeGameDefinitionSaveName(GameDefButton button)
    {
        GameDefinitionPreview preview = button == null ? null : button.GetPreview();
        return preview == null ? null : preview.m_SaveFileName;
    }

    static bool NativeGameDefinitionCandidateReady(GameDefButton candidate)
    {
        if (candidate == null || !SceneOwner(candidate) || !candidate.gameObject.activeInHierarchy
            || !candidate.isActiveAndEnabled || string.IsNullOrEmpty(candidate.m_GameDefName)
            || candidate.m_GameDefNameText == null || string.IsNullOrEmpty(NativeGameDefinitionSaveName(candidate)))
            return false;
        Button button = candidate.GetComponent<Button>();
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()
            || button.onClick == null || button.onClick.GetPersistentEventCount() != 1)
            return false;
        return button.onClick.GetPersistentTarget(0) == candidate
            && button.onClick.GetPersistentMethodName(0) == "OnClick";
    }

    JObject NativeGameDefinitionCandidate(GameDefButton candidate, string currentSaveName)
    {
        string saveName = NativeGameDefinitionSaveName(candidate);
        Button button = candidate == null ? null : candidate.GetComponent<Button>();
        bool ready = NativeGameDefinitionCandidateReady(candidate);
        return new JObject {
            {"buttonInstanceId", candidate == null ? 0 : candidate.GetInstanceID()},
            {"displayName", candidate == null || candidate.m_GameDefNameText == null ? null : candidate.m_GameDefNameText.text},
            {"saveFileName", saveName},
            {"active", candidate != null && SceneOwner(candidate) && candidate.gameObject.activeInHierarchy},
            {"interactable", button != null && button.IsInteractable()},
            {"nativeOnClickCallback", button != null && button.onClick != null && button.onClick.GetPersistentEventCount() == 1
                && button.onClick.GetPersistentTarget(0) == candidate && button.onClick.GetPersistentMethodName(0) == "OnClick"},
            {"selected", saveName != null && saveName == currentSaveName},
            {"eligible", ready},
        };
    }

    void RequireNativeGameDefinitionScreen(out uiStartGame menu, out GameConfig config, out GameLogic logic)
    {
        menu = uiStartGame.Instance;
        config = menu == null ? null : menu.m_GameConfig;
        logic = GameLogic.Instance;
        if (menu == null || config == null || logic == null || uiScreen.gCurrent != config
            || !SceneOwner(config) || !config.gameObject.activeInHierarchy || config.m_IsResume
            || menu.m_GameStarted || !logic.IsSinglePlayer() || FTKInput.Instance == null
            || FTKInput.Instance.m_CurrentInputFocus != config || config.m_GameDefButtons == null)
            throw new InvalidOperationException("Active native offline New Game configuration with its input focus is required.");
        if (config.m_GameDefButtons.Count < 1 || config.m_GameDefButtons.Count > 64)
            throw new InvalidOperationException("Native adventure row list is empty or exceeds 64 entries.");
    }

    static bool NativeGameDefinitionSameRoute(NativeGameDefinitionSelectionContext context)
    {
        return context != null && uiStartGame.Instance == context.menu && context.menu.m_GameConfig == context.config
            && GameLogic.Instance == context.logic && context.logic.IsSinglePlayer()
            && uiScreen.gCurrent == context.config && !context.config.m_IsResume && !context.menu.m_GameStarted
            && FTKInput.Instance != null && FTKInput.Instance.m_CurrentInputFocus == context.config;
    }

    JObject NativeGameDefinitionSelect(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "inspectionToken", "buttonInstanceId");
        CatalogNoLinks(root);
        string action = Str(command, "action");
        if (action != "inspect" && action != "submit")
            throw new ArgumentException("Use inspect or submit.");
        if (nativeGameDefinitionSelectionConsumed)
            throw new InvalidOperationException("Native adventure selection already consumed for this process.");

        uiStartGame menu;
        GameConfig config;
        GameLogic logic;
        RequireNativeGameDefinitionScreen(out menu, out config, out logic);

        GameDefinitionPreview currentPreview = config.GetCurrentGameDefPreview();
        string currentSaveName = currentPreview == null ? null : currentPreview.m_SaveFileName;
        JArray candidates = new JArray();
        Dictionary<int, GameDefButton> byId = new Dictionary<int, GameDefButton>();
        foreach (GameDefButton candidate in config.m_GameDefButtons)
        {
            if (candidate == null) throw new InvalidOperationException("Native adventure row list contains a null entry.");
            int candidateId = candidate.GetInstanceID();
            if (byId.ContainsKey(candidateId)) throw new InvalidOperationException("Native adventure row list contains a duplicate instance.");
            byId.Add(candidateId, candidate);
            candidates.Add(NativeGameDefinitionCandidate(candidate, currentSaveName));
        }

        int requestedId = Int(command, "buttonInstanceId", 0);
        GameDefButton target;
        if (action == "inspect")
        {
            nativeGameDefinitionSelection = null;
            if (requestedId == 0 || !byId.TryGetValue(requestedId, out target)
                || !NativeGameDefinitionCandidateReady(target))
            {
                return new JObject {
                    {"ok", true}, {"readOnly", true},
                    {"status", requestedId == 0 ? "native_game_definition_selection_required" : "native_game_definition_candidate_unavailable"},
                    {"candidates", candidates}, {"currentSaveFileName", currentSaveName},
                    {"configInstanceId", config.GetInstanceID()},
                };
            }

            string token = Guid.NewGuid().ToString("N");
            nativeGameDefinitionSelection = new NativeGameDefinitionSelectionContext {
                menu = menu, config = config, logic = logic, button = target,
                saveFileName = NativeGameDefinitionSaveName(target), token = token,
            };
            return new JObject {
                {"ok", true}, {"readOnly", true}, {"status", "native_game_definition_eligible"},
                {"candidates", candidates}, {"currentSaveFileName", currentSaveName},
                {"configInstanceId", config.GetInstanceID()}, {"buttonInstanceId", target.GetInstanceID()},
                {"inspectionToken", token},
            };
        }

        NativeGameDefinitionSelectionContext context = nativeGameDefinitionSelection;
        if (context == null || !NativeGameDefinitionSameRoute(context) || !byId.TryGetValue(requestedId, out target)
            || context.button != target || context.button.GetInstanceID() != requestedId
            || context.saveFileName != NativeGameDefinitionSaveName(target)
            || !NativeGameDefinitionCandidateReady(target)
            || Str(command, "inspectionToken") != context.token)
            throw new InvalidOperationException("Exact inspected native adventure row changed before submission.");

        string previousSaveName = currentSaveName;
        nativeGameDefinitionSelectionConsumed = true;
        target.OnClick();
        GameDefinitionPreview selectedPreview = config.GetCurrentGameDefPreview();
        string selectedSaveName = selectedPreview == null ? null : selectedPreview.m_SaveFileName;
        string selectedLabel = selectedPreview == null ? null : selectedPreview.GetDisplayName();
        if (selectedSaveName != context.saveFileName || menu.m_GameDefName != context.saveFileName)
            throw new InvalidOperationException("Native adventure-row callback did not select the inspected campaign.");

        return new JObject {
            {"ok", true}, {"status", "native_game_definition_selected"},
            {"callback", "GameDefButton.OnClick -> GameConfig.OnChangeValueGameDef"},
            {"buttonInstanceId", target.GetInstanceID()}, {"previousSaveFileName", previousSaveName},
            {"selectedSaveFileName", selectedSaveName}, {"selectedDisplayName", selectedLabel},
            {"scope", "One inspected, scene-owned native adventure row and its existing callback. No UI was constructed and no preference was written directly."},
        };
    }
}
