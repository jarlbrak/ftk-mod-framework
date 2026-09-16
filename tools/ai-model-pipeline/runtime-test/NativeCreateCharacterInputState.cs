using System;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

// Read-only focus evidence for the game's actual Party Select UI.  This
// deliberately observes the native focus graph rather than driving it: callers
// can derive a keyboard route from the current selected/selectable identities
// without the helper calling SetFocus, Select, SetClass, or a UI callback.
public sealed partial class RuntimeModelTest
{
    static string NativeCreateInputName(UnityEngine.Object value)
    {
        if (value == null) return null;
        GameObject gameObject = value as GameObject;
        if (gameObject == null)
        {
            Component component = value as Component;
            gameObject = component == null ? null : component.gameObject;
        }
        string name = gameObject == null ? value.name : gameObject.name;
        if (name == null) return null;
        return name.Length <= 120 ? name : name.Substring(0, 120);
    }

    static JObject NativeCreateSelectableState(FTKSelectable selectable)
    {
        return new JObject {
            {"present", selectable != null},
            {"instanceId", selectable == null ? 0 : selectable.GetInstanceID()},
            {"name", NativeCreateInputName(selectable)},
        };
    }

    static JArray NativeCreateSelectableNames(FTKInputFocus focus)
    {
        JArray result = new JArray();
        if (focus == null || focus.m_CurrentSelectables == null) return result;
        if (focus.m_CurrentSelectables.Count > 64)
            throw new InvalidOperationException("Native focus selectable list exceeds 64.");
        foreach (FTKSelectable selectable in focus.m_CurrentSelectables)
            result.Add(NativeCreateInputName(selectable));
        return result;
    }

    static JObject NativeCreateInputFocusState(FTKInputFocus focus)
    {
        JObject result = NativeCreateObjectState(focus);
        result["name"] = NativeCreateInputName(focus);
        result["enabled"] = focus != null && focus.enabled;
        result["hasInputFocus"] = focus != null && focus.m_HasInputFocus;
        result["currentSelected"] = NativeCreateSelectableState(focus == null ? null : focus.m_CurrentSelected);
        result["currentSelectableNames"] = NativeCreateSelectableNames(focus);
        return result;
    }

    // The shipped AssignDevice exposes m_Type as a property rather than a
    // field.  Read a field/property by name only inside the command path so a
    // version difference in this diagnostic detail cannot stop BepInEx from
    // discovering or instantiating the test plugin.
    static bool NativeCreateTryReadMember(object owner, string name, out object value)
    {
        value = null;
        if (owner == null) return false;
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Members | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                value = field.GetValue(owner);
                return true;
            }
            PropertyInfo property = type.GetProperty(name, Members | BindingFlags.DeclaredOnly);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(owner, null);
                return true;
            }
        }
        return false;
    }

    static bool NativeCreateClaimed(uiQuickPlayerCreate candidate, out string claimType)
    {
        object device = candidate == null ? null : candidate.m_ClaimDevice;
        object type;
        if (device == null || !NativeCreateTryReadMember(device, "m_Type", out type) || type == null)
        {
            claimType = null;
            return false;
        }
        claimType = Convert.ToString(type);
        try { return Convert.ToInt32(type) != 0; }
        catch (InvalidCastException) { return !string.Equals(claimType, "None", StringComparison.Ordinal); }
    }

    static JObject NativeCreateInputCandidate(uiQuickPlayerCreate candidate, out bool live)
    {
        live = candidate != null && SceneOwner(candidate) && candidate.gameObject.activeInHierarchy;
        if (candidate == null)
            return new JObject {
                {"present", false},
                {"ownerInstanceId", 0},
            };

        string claimType;
        bool claimed = NativeCreateClaimed(candidate, out claimType);
        FTKInputFocus focus = candidate.m_InputFocus;
        return new JObject {
            {"present", true},
            {"ownerInstanceId", candidate.GetInstanceID()},
            {"ownerName", NativeCreateInputName(candidate)},
            {"sceneOwned", SceneOwner(candidate)},
            {"active", candidate.gameObject.activeInHierarchy},
            {"classId", candidate.m_ClassID},
            {"label", candidate.m_PlayerClass == null ? null : candidate.m_PlayerClass.text},
            {"turnIndex", candidate.m_TurnIndex},
            {"mode", candidate.m_Mode.ToString()},
            {"claimed", claimed},
            {"claimType", claimType},
            {"interactable", candidate.m_Interactable},
            {"inputFocus", NativeCreateInputFocusState(focus)},
        };
    }

    JObject NativeCreateCharacterInputState(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);

        uiStartGame menu = uiStartGame.Instance;
        FTKInput input = FTKInput.Instance;
        FTKInputFocus rootFocus = menu == null ? null : NativeCreateField(menu, "m_CreateCharacterRoot") as FTKInputFocus;
        bool menuPresent = menu != null;
        bool createUiListPresent = menuPresent && menu.m_CreateUIs != null;
        int createUiCount = createUiListPresent ? menu.m_CreateUIs.Count : 0;
        if (createUiCount > 8) throw new InvalidOperationException("Native preview list exceeds 8.");

        JArray candidates = new JArray();
        bool hasLiveCandidate = false;
        if (createUiListPresent)
            foreach (uiQuickPlayerCreate candidate in menu.m_CreateUIs)
            {
                bool live;
                candidates.Add(NativeCreateInputCandidate(candidate, out live));
                hasLiveCandidate |= live;
            }

        bool rootActive = rootFocus != null && rootFocus.gameObject != null && rootFocus.gameObject.activeInHierarchy;
        bool actualPartySelect = menuPresent && createUiListPresent && createUiCount > 0 && rootActive && hasLiveCandidate;
        FTKInputFocus currentFocus = input == null ? null : input.m_CurrentInputFocus;
        FTKSelectable currentSelected = input == null ? null : FTKInput.GetSelectable();
        return new JObject {
            {"ok", true},
            {"status", actualPartySelect ? "observed_actual_native_party_select" : "unavailable_native_party_select"},
            {"readOnly", true},
            {"actualPartySelect", actualPartySelect},
            {"menu", NativeCreateObjectState(menu)},
            {"createUiListPresent", createUiListPresent},
            {"createUiCount", createUiListPresent ? new JValue(createUiCount) : new JValue((object)null)},
            {"input", new JObject {
                {"present", input != null},
                {"instanceId", input == null ? 0 : input.GetInstanceID()},
                {"currentFocus", NativeCreateInputFocusState(currentFocus)},
                {"currentSelected", NativeCreateSelectableState(currentSelected)},
            }},
            {"characterCreateRoot", NativeCreateInputFocusState(rootFocus)},
            {"candidates", candidates},
        };
    }
}
