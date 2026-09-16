using System;
using System.Collections.Generic;
using Google2u;
using GridEditor;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static bool partyClassUncertain;
    static int partyClassSubmittedFrame = -1;
    NativePartyStartPolicy partyClassClaim;
    string partyClassToken;
    JObject partyClassPins;
    uiQuickPlayerCreate partyClassOwner;
    uiStartGame partyClassMenu;
    object partyClassDefinition;
    List<FTK_playerGameStart> partyClassRows;

    JObject NativePartyClass(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "inspectionToken", "classKey", "targetClassId", "ownerInstanceId", "direction");
        CatalogNoLinks(root);
        string action = Str(command, "action"), direction = Str(command, "direction"), key = Str(command, "classKey");
        if (action != "inspect" && action != "submit") throw new ArgumentException("Use inspect or submit.");
        NativePartyClassPolicy.RequireSelectionOpen(partyStartClaim.Consumed, partyClassUncertain,
            UnityEngine.Time.frameCount, partyClassSubmittedFrame);
        int target = LeaseObservationPin.ExactId(command, "targetClassId", true);
        int ownerId = LeaseObservationPin.ExactId(command, "ownerInstanceId", true);
        uiStartGame menu;
        JObject pins = InspectNativePartyStart(out menu, true);
        uiQuickPlayerCreate owner = null;
        foreach (uiQuickPlayerCreate candidate in menu.m_CreateUIs)
            if (candidate.GetInstanceID() == ownerId) owner = candidate;
        if (owner == null || !owner.m_Interactable || owner.m_SerializedData != null
            || owner.m_Mode.ToString() != "PlayerInfo")
            throw new InvalidOperationException("Exact interactive native PlayerInfo preview owner required.");
        FTK_playerGameStartDB db = FTK_playerGameStartDB.GetDB();
        Type registry = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry", true);
        object[] registration = { key, 0, new[] { typeof(FTK_playerGameStartDB) } };
        if (string.IsNullOrEmpty(key) || !(bool)registry.GetMethod("TryGetSyntheticId", Statics).Invoke(null, registration)
            || (int)registration[1] != target || target < 0 || target >= db.GetCount()
            || db.GetEntryByInt(target).m_ID != key)
            throw new InvalidOperationException("Exact registered custom class key and ID required.");
        if (db.GetCount() < 1 || db.GetCount() > 512) throw new InvalidOperationException("Class DB exceeds bounded route.");
        bool[] visible = new bool[db.GetCount()];
        var rows = new List<FTK_playerGameStart>();
        JArray route = new JArray();
        string build = FTKVersion.Instance.m_BuildType.ToString();
        for (int i = 0; i < visible.Length; i++)
        {
            FTK_playerGameStart row = db.GetEntryByIndex(i);
            if (row == null || !object.ReferenceEquals(row, db.GetEntryByInt(i)) || db.GetIntFromID(row.m_ID) != i)
                throw new InvalidOperationException("Native class index/ID/row join differs.");
            bool dlc = row.m_DLC == FTK_dlc.ID.None || PublishPlatform.Instance.HasDLC(FTK_dlcDB.Get(row.m_DLC));
            bool usable = dlc && ((build == "Release" || build == "Experimental") ? row.m_Release
                : build == "PublicTest" ? row.m_PublicTest : build == "Development" && row.m_Development);
            bool revealed = db.IsReveal((FTK_playerGameStart.ID)i, true);
            visible[i] = usable && revealed;
            rows.Add(row);
            route.Add(new JObject { {"index", i}, {"key", row.m_ID}, {"dlc", (int)row.m_DLC}, {"hasDlc", dlc},
                {"usable", usable}, {"revealed", revealed}, {"unlocked", db.IsUnlock((FTK_playerGameStart.ID)i, true)},
                {"defaultSkin", (int)row.m_DefaultSkinType} });
        }
        if (!visible[target] || !db.IsUnlock((FTK_playerGameStart.ID)target, true))
            throw new InvalidOperationException("Target custom class must be usable, revealed and unlocked.");
        int next = NativePartyClassPolicy.Next(visible, owner.m_ClassID, direction);
        string method = direction == "right" ? "OnClassClick" : "OnClassClickLeft";
        if (owner.m_ClassButtons == null || owner.m_ClassButtons.Length > 8) throw new InvalidOperationException("Native class arrows unavailable.");
        JArray arrowCandidates = new JArray();
        int[] identities = new int[owner.m_ClassButtons.Length];
        bool[] eligible = new bool[identities.Length];
        int[] callStates = new int[identities.Length];
        for (int index = 0; index < identities.Length; index++)
        {
            Button candidate = owner.m_ClassButtons[index];
            int count = candidate == null ? 0 : candidate.onClick.GetPersistentEventCount();
            if (count > 8) throw new InvalidOperationException("Native arrow callback list exceeds bound.");
            bool matching = false;
            JArray callbacks = new JArray();
            for (int listener = 0; listener < count; listener++)
            {
                matching |= candidate.onClick.GetPersistentTarget(listener) == owner
                    && candidate.onClick.GetPersistentMethodName(listener) == method;
                UnityEngine.Object callbackOwner = candidate.onClick.GetPersistentTarget(listener);
                callbacks.Add(new JObject { {"targetInstanceId", callbackOwner == null ? 0 : callbackOwner.GetInstanceID()},
                    {"method", candidate.onClick.GetPersistentMethodName(listener)} });
            }
            bool sole = count == 1 && matching;
            string controlName = candidate == null ? null : candidate.gameObject.name;
            bool explicitDirectional = NativePartyClassPolicy.ExplicitDirectionalControl(controlName, direction);
            int state = -1;
            if (count == 1)
            {
                object calls = NativeCreateField(candidate.onClick, "m_PersistentCalls");
                var entries = NativeCreateField(calls, "m_Calls") as System.Collections.IList;
                if (entries != null && entries.Count == 1) state = Convert.ToInt32(NativeCreateField(entries[0], "m_CallState"));
            }
            bool ownerChild = candidate != null && candidate.transform.IsChildOf(owner.transform);
            bool active = candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy;
            bool interactable = candidate != null && candidate.IsInteractable();
            Graphic graphic = candidate == null ? null : candidate.targetGraphic;
            bool graphicActive = graphic != null && graphic.isActiveAndEnabled && graphic.transform.IsChildOf(owner.transform);
            bool canvasActive = graphic != null && graphic.canvas != null && graphic.canvas.isActiveAndEnabled;
            float graphicColorAlpha = graphic == null ? 0f : graphic.color.a;
            float rendererAlpha = graphic == null ? 0f : graphic.canvasRenderer.GetAlpha();
            bool alphaFactorsPositive = NativePartyClassPolicy.GraphicVisible(true, true, false, graphicColorAlpha);
            JArray alphaGroups = new JArray();
            int depth = 0;
            for (UnityEngine.Transform parent = graphic == null ? null : graphic.transform; parent != null; parent = parent.parent)
            {
                if (++depth > 32) throw new InvalidOperationException("Native graphic ancestry exceeds bound.");
                UnityEngine.CanvasGroup[] groups = parent.GetComponents<UnityEngine.CanvasGroup>();
                if (groups.Length > 8) throw new InvalidOperationException("Native canvas groups exceed bound.");
                bool ignoreParents = false;
                foreach (UnityEngine.CanvasGroup group in groups)
                {
                    if (!group.gameObject.activeInHierarchy) continue;
                    // Test factors separately: multiplying tiny positive values
                    // can underflow and is not a native effective-alpha readback.
                    alphaFactorsPositive &= NativePartyClassPolicy.GraphicVisible(true, true, false, group.alpha);
                    ignoreParents |= group.ignoreParentGroups;
                    alphaGroups.Add(new JObject { {"instanceId", group.GetInstanceID()}, {"alpha", group.alpha},
                        {"ignoreParentGroups", group.ignoreParentGroups} });
                }
                if (ignoreParents) break;
            }
            bool culled = graphic == null || graphic.canvasRenderer.cull;
            bool graphicVisible = alphaFactorsPositive
                && NativePartyClassPolicy.GraphicVisible(graphicActive, canvasActive, culled, rendererAlpha);
            identities[index] = candidate == null ? 0 : candidate.GetInstanceID(); callStates[index] = state;
            eligible[index] = NativePartyClassPolicy.EligibleArrow(explicitDirectional, sole, state,
                ownerChild, active, interactable, graphicVisible);
            JArray reasons = new JArray();
            if (!explicitDirectional) reasons.Add("not_explicit_directional_control");
            if (!sole) reasons.Add("callback_not_exact_single_owner_direction");
            if (state != 1 && state != 2) reasons.Add("persistent_callback_not_enabled");
            if (!ownerChild) reasons.Add("not_owner_child");
            if (!active) reasons.Add("button_inactive_or_disabled");
            if (!interactable) reasons.Add("button_not_interactable");
            if (!graphicVisible) reasons.Add("target_graphic_not_visible");
            arrowCandidates.Add(new JObject { {"index", index}, {"instanceId", identities[index]},
                {"name", controlName}, {"explicitDirectionalControl", explicitDirectional}, {"matchingCallback", matching},
                {"callbacks", callbacks},
                {"persistentCount", count}, {"callState", state}, {"ownerChild", ownerChild},
                {"active", active}, {"interactable", interactable}, {"graphicActive", graphicActive},
                {"targetGraphicInstanceId", graphic == null ? 0 : graphic.GetInstanceID()},
                {"canvasInstanceId", graphic == null || graphic.canvas == null ? 0 : graphic.canvas.GetInstanceID()},
                {"canvasActive", canvasActive}, {"graphicColorAlpha", graphicColorAlpha}, {"rendererAlpha", rendererAlpha},
                {"alphaFactorsPositive", alphaFactorsPositive}, {"culled", culled},
                {"alphaGroups", alphaGroups}, {"visible", graphicVisible}, {"eligible", eligible[index]}, {"reasons", reasons} });
        }
        int selectedArrow = NativePartyClassPolicy.UniqueEligibleArrow(identities, eligible);
        if (selectedArrow < 0)
        {
            partyClassToken = null; partyClassPins = null; partyClassClaim = null;
            return new JObject { {"ok", false}, {"readOnly", true}, {"callbackAttempted", false},
                {"error", selectedArrow == -2 ? "Multiple distinct eligible native class arrows." : "No eligible native class arrow."},
                {"arrowCandidates", arrowCandidates} };
        }
        Button arrow = owner.m_ClassButtons[selectedArrow];
        int callState = callStates[selectedArrow];
        pins["ownerInstanceId"] = ownerId; pins["targetClassId"] = target; pins["classKey"] = key;
        pins["direction"] = direction; pins["nextClassId"] = next; pins["route"] = route; pins["build"] = build;
        pins["arrowInstanceId"] = arrow.GetInstanceID(); pins["arrowCallState"] = callState;
        pins["arrowName"] = arrow.gameObject.name;
        pins["arrowCandidates"] = arrowCandidates;
        if (action == "inspect")
        {
            partyClassToken = Guid.NewGuid().ToString("N"); partyClassPins = pins;
            partyClassOwner = owner; partyClassMenu = menu; partyClassRows = rows;
            partyClassDefinition = GameLogic.Instance.GetGameDef(); partyClassClaim = new NativePartyStartPolicy();
            return new JObject { {"ok", true}, {"readOnly", true}, {"inspectionToken", partyClassToken},
                {"pins", pins}, {"reachedTarget", owner.m_ClassID == target} };
        }
        if (partyClassClaim == null || partyClassOwner != owner || partyClassMenu != menu
            || !object.ReferenceEquals(partyClassDefinition, GameLogic.Instance.GetGameDef()) || partyClassRows.Count != rows.Count)
            throw new InvalidOperationException("Class inspection owner/adventure changed.");
        for (int i = 0; i < rows.Count; i++)
            if (!object.ReferenceEquals(rows[i], partyClassRows[i])) throw new InvalidOperationException("Native class row identity changed.");
        bool attempted = false;
        int beforeClass = owner.m_ClassID, beforeAvatar = owner.m_Avatar.GetInstanceID();
        string error = null;
        try
        {
            partyClassClaim.Submit(partyClassToken, Str(command, "inspectionToken"), partyClassPins, pins, delegate {
                if (owner.m_ClassID == target) return;
                partyClassUncertain = true; attempted = true;
                partyClassSubmittedFrame = UnityEngine.Time.frameCount;
                if (direction == "right") owner.OnClassClick(); else owner.OnClassClickLeft();
                if (uiStartGame.Instance != menu || !menu.m_CreateUIs.Contains(owner)
                    || !menu.m_CreateCharacterRoot.m_Players.Contains(owner)
                    || owner.m_ClassID != next || !NativeCreateCandidateReady(NativeCreateCandidate(owner))
                    || owner.m_Avatar.GetInstanceID() == beforeAvatar || owner.m_SkinType != rows[next].m_DefaultSkinType)
                    throw new InvalidOperationException("Native class callback did not yield the predicted reciprocal new preview.");
                partyClassUncertain = false;
            });
        }
        catch (Exception ex) { error = ex.ToString(); }
        return new JObject { {"ok", error == null}, {"callbackAttempted", attempted}, {"callback", method},
            {"beforeClassId", beforeClass}, {"beforeAvatarInstanceId", beforeAvatar}, {"pins", pins},
            {"observedPreview", NativeCreateCandidate(owner)}, {"reachedTarget", error == null && owner.m_ClassID == target},
            {"uncertain", partyClassUncertain}, {"error", error == null ? new JValue((object)null) : new JValue(error)} };
    }
}
