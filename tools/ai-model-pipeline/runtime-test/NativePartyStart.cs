using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    static readonly NativePartyStartPolicy partyStartClaim = new NativePartyStartPolicy();
    string partyStartToken;
    JObject partyStartPins;
    uiStartGame partyStartMenu;
    object partyStartDefinition;

    JObject InspectNativePartyStart(out uiStartGame menu, bool forClassSelection = false, bool requireResume = false)
    {
        RequireSinglePlayer();
        menu = uiStartGame.Instance;
        GameLogic logic = GameLogic.Instance;
        if (menu == null || !SceneOwner(menu) || !menu.gameObject.activeInHierarchy
            || logic == null || !logic.IsSinglePlayer() || !NativeCreatePhotonOfflineMode()
            || !menu.IsMasterClient || menu.m_UseOnlineSinglePlayer || menu.m_IsResuming != requireResume
            || menu.m_GameStarted || menu.m_FahrulEntered
            || !object.Equals(NativeCreateField(menu, "m_MapReady"), true)
            || !NativeCreateMapReady() || logic.GetGameDef() == null)
            throw new InvalidOperationException("Exact offline native Party Select route is required.");
        uiCharacterCreateRoot create = menu.m_CreateCharacterRoot;
        if (create == null || !SceneOwner(create) || !create.enabled || !NativeCreateRootActive(menu)
            || !create.gameObject.activeInHierarchy || menu.m_FSM == null || !menu.m_FSM.enabled
            || uiSystemDialog.Instance == null || uiSystemDialog.Instance.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Active native character-create root without a system dialog is required.");
        Transform start = create.m_StartButton;
        Transform enter = menu.m_EnterFahrulButton;
        // These two serialized references may differ. Only the active root Start
        // with the exact native callback is eligible, never a guessed UI button.
        Button button = start == null ? null : start.GetComponent<Button>();
        if (!forClassSelection && (button == null || !start.IsChildOf(create.transform) || !button.isActiveAndEnabled
            || !button.gameObject.activeInHierarchy || !button.IsInteractable()
            || button.onClick.GetPersistentEventCount() != 1
            || button.onClick.GetPersistentTarget(0) != menu
            || button.onClick.GetPersistentMethodName(0) != "EnterFahrul"))
            throw new InvalidOperationException("Exact active native Start button/callback is unavailable.");
        // Unity 2017 does not expose persistent listener state publicly.
        object calls = forClassSelection ? null : NativeCreateField(button.onClick, "m_PersistentCalls");
        System.Collections.IList entries = calls == null ? null : NativeCreateField(calls, "m_Calls") as System.Collections.IList;
        int callState = entries == null || entries.Count != 1 ? -1
            : Convert.ToInt32(NativeCreateField(entries[0], "m_CallState"));
        if (!forClassSelection && callState != 1 && callState != 2)
            throw new InvalidOperationException("Native Start callback is disabled or ambiguous.");
        if (menu.m_CreateUIs == null || create.m_Players == null || menu.m_CreateUIs.Count < 1
            || menu.m_CreateUIs.Count > 3 || menu.m_CreateUIs.Count != menu.m_ActualMaxCharCount
            || create.m_Players.Count != menu.m_CreateUIs.Count || (!forClassSelection && !menu.GetAllPlayersReady()))
            throw new InvalidOperationException("Complete nonempty native ready preview set is required.");
        FTKInputFocus focus = FTKInput.Instance == null ? null : FTKInput.Instance.m_CurrentInputFocus;
        bool ownedFocus = focus == create;
        HashSet<int> owners = new HashSet<int>();
        HashSet<int> avatars = new HashSet<int>();
        HashSet<int> turns = new HashSet<int>();
        Type photon = typeof(GameLogic).Assembly.GetType("PhotonNetwork", true);
        object localPlayer = photon.GetProperty("player", Statics).GetValue(null, null);
        object localIdValue;
        if (localPlayer == null || !NativeCreateTryReadMember(localPlayer, "ID", out localIdValue)
            || !(localIdValue is int) || (int)localIdValue <= 0)
            throw new InvalidOperationException("Exact local Photon player is unavailable.");
        int localPhotonId = (int)localIdValue;
        JArray previews = new JArray();
        foreach (uiQuickPlayerCreate candidate in menu.m_CreateUIs)
        {
            JObject row = NativeCreateCandidate(candidate);
            if (!NativeCreateCandidateReady(row) || (!forClassSelection && !candidate.m_IsReady)
                || !create.m_Players.Contains(candidate) || !owners.Add(candidate.GetInstanceID())
                || !avatars.Add(candidate.m_Avatar.GetInstanceID()) || !turns.Add(candidate.m_TurnIndex))
                throw new InvalidOperationException("Native preview owners are incomplete, ambiguous, or nonreciprocal.");
            if (forClassSelection && candidate.m_IsReady != FTK_playerGameStartDB.GetDB().IsUnlock(
                (FTK_playerGameStart.ID)candidate.m_ClassID, true))
                throw new InvalidOperationException("Preview readiness does not match the native class unlock state.");
            NativePartyStartPolicy.RequireNativePreview(localPhotonId, candidate.m_PhotonID,
                candidate.m_TurnIndex, menu.m_ActualMaxCharCount);
            ownedFocus |= focus != null && focus == candidate.m_InputFocus;
            row["photonId"] = candidate.m_PhotonID;
            row["ready"] = candidate.m_IsReady;
            row["mode"] = candidate.m_Mode.ToString();
            row["playerName"] = candidate.m_PlayerNameStr;
            row["mainColorIndex"] = candidate.m_MainColorIndex;
            row["skinColorIndex"] = candidate.m_SkinColorIndex;
            row["hairColorIndex"] = candidate.m_HairColorIndex;
            row["armorId"] = (int)candidate.m_CustomOutfit.m_ArmorID;
            row["helmetId"] = (int)candidate.m_CustomOutfit.m_HelmetID;
            row["backpackId"] = (int)candidate.m_CustomOutfit.m_BackpackID;
            previews.Add(row);
        }
        if (!ownedFocus) throw new InvalidOperationException("Focus belongs to another native screen or modal.");
        return new JObject {
            {"session", sessionId}, {"root", root},
            {"menuInstanceId", menu.GetInstanceID()}, {"createRootInstanceId", create.GetInstanceID()},
            {"startButtonInstanceId", button == null ? 0 : button.GetInstanceID()}, {"enterButtonInstanceId", enter == null ? 0 : enter.GetInstanceID()},
            {"persistentCallState", callState},
            {"localPhotonId", localPhotonId},
            {"fsmState", menu.m_FSM.ActiveStateName}, {"gameDefinitionName", menu.m_GameDefName},
            {"gameDefinitionIdentityHash", System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(logic.GetGameDef())},
            {"currentScreenInstanceId", uiScreen.gCurrent == null ? 0 : uiScreen.gCurrent.GetInstanceID()},
            {"previews", previews},
        };
    }

    JObject NativePartyStart(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "inspectionToken");
        CatalogNoLinks(root);
        string action = Str(command, "action");
        if (action != "inspect" && action != "submit") throw new ArgumentException("Use inspect or submit.");
        if (partyStartClaim.Consumed) throw new InvalidOperationException("Party Start already consumed; observe native state separately.");
        uiStartGame menu;
        JObject current = InspectNativePartyStart(out menu);
        if (action == "inspect")
        {
            partyStartToken = Guid.NewGuid().ToString("N");
            partyStartPins = current;
            partyStartMenu = menu;
            partyStartDefinition = GameLogic.Instance.GetGameDef();
            return new JObject { {"ok", true}, {"readOnly", true}, {"status", "native_party_start_eligible"},
                {"inspectionToken", partyStartToken}, {"pins", current} };
        }
        if (partyStartMenu != menu || !object.ReferenceEquals(partyStartDefinition, GameLogic.Instance.GetGameDef()))
            throw new InvalidOperationException("Native Party Select owner or adventure changed.");
        string error = null;
        try
        {
            partyStartClaim.Submit(partyStartToken, Str(command, "inspectionToken"), partyStartPins, current,
                delegate { menu.EnterFahrul(); });
        }
        catch (Exception ex) { error = ex.ToString(); }
        return new JObject {
            {"ok", error == null}, {"status", error == null ? "native_party_start_submitted" : "native_party_start_failed"},
            {"callbackAttempted", partyStartClaim.Consumed}, {"callback", "uiStartGame.EnterFahrul"},
            {"pins", current}, {"error", error == null ? new JValue((object)null) : new JValue(error)},
            {"observedGameStarted", menu != null && menu.m_GameStarted},
            {"observedCharacterCreateRootActive", menu != null && NativeCreateRootActive(menu)},
            {"completionClaimed", false},
        };
    }

    JObject NativeResumePartyStart(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "inspectionToken");
        CatalogNoLinks(root);
        string action = Str(command, "action");
        if (action != "inspect" && action != "submit") throw new ArgumentException("Use inspect or submit.");
        if (partyStartClaim.Consumed) throw new InvalidOperationException("Party Start already consumed; observe native state separately.");
        uiStartGame menu;
        JObject current = InspectNativePartyStart(out menu, false, true);
        if (action == "inspect")
        {
            partyStartToken = Guid.NewGuid().ToString("N"); partyStartPins = current;
            partyStartMenu = menu; partyStartDefinition = GameLogic.Instance.GetGameDef();
            return new JObject {{"ok", true}, {"readOnly", true}, {"status", "native_resume_party_start_eligible"},
                {"inspectionToken", partyStartToken}, {"pins", current}};
        }
        if (partyStartMenu != menu || !object.ReferenceEquals(partyStartDefinition, GameLogic.Instance.GetGameDef()))
            throw new InvalidOperationException("Native resumed Party Select owner or adventure changed.");
        string error = null;
        try { partyStartClaim.Submit(partyStartToken, Str(command, "inspectionToken"), partyStartPins, current, delegate { menu.EnterFahrul(); }); }
        catch (Exception ex) { error = ex.ToString(); }
        return new JObject {{"ok", error == null}, {"status", error == null ? "native_resume_party_start_submitted" : "native_resume_party_start_failed"},
            {"callbackAttempted", partyStartClaim.Consumed}, {"callback", "uiStartGame.EnterFahrul"}, {"pins", current},
            {"error", error == null ? new JValue((object)null) : new JValue(error)}, {"completionClaimed", false}};
    }
}
