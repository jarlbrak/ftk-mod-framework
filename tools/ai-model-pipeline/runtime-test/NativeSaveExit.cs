using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

// Isolated-test-only native UI bridge. It never writes save data or calls a
// save API. The one mutating route requires the visible Options Menu's exact
// persistent Save/Exit callback and dispatches that button's UnityEvent once.
public sealed partial class RuntimeModelTest
{
    bool nativeSaveExitConsumed;
    string nativeSaveExitToken;
    uiOptionsMenu nativeSaveExitMenu;
    Button nativeSaveExitButton;
    System.Reflection.MethodInfo nativeSaveExitCallback;

    static Button NativeSaveExitButton(uiOptionsMenu menu)
    {
        System.Reflection.FieldInfo field = menu.GetType().GetField("m_SaveExit", Members);
        object value = field == null ? null : field.GetValue(menu);
        Button button = value as Button;
        Component component = value as Component;
        GameObject gameObject = value as GameObject;
        if (button == null && component != null) button = component.GetComponent<Button>();
        if (button == null && gameObject != null) button = gameObject.GetComponent<Button>();
        return button;
    }

    JObject InspectNativeSaveExit(out uiOptionsMenu menu, out List<Button> candidates, out System.Reflection.MethodInfo callback)
    {
        RequireSinglePlayer();
        menu = uiOptionsMenu.Instance;
        candidates = new List<Button>();
        callback = null;
        if (menu == null || !SceneOwner(menu) || !menu.gameObject.activeInHierarchy || !menu.m_Showing
            || FTKHub.Instance == null || GameLogic.Instance == null || !GameLogic.Instance.IsSinglePlayer()
            || EncounterSession.Instance == null || EncounterSession.Instance.m_IsInCombat)
            throw new InvalidOperationException("Visible native Options Menu outside combat is required.");
        Button button = NativeSaveExitButton(menu);
        callback = menu.GetType().GetMethod("OnSaveExit", Members, null, Type.EmptyTypes, null);
        if (button == null || !SceneOwner(button) || !button.gameObject.activeInHierarchy
            || !button.isActiveAndEnabled || !button.IsInteractable()
            || callback == null || callback.DeclaringType != menu.GetType())
            throw new InvalidOperationException("Exact visible native Save/Exit UI control and callback are required.");
        candidates.Add(button);
        JArray observed = new JArray { new JObject {
            {"buttonInstanceId", button.GetInstanceID()},
            {"parentInstanceId", button.transform.parent == null ? 0 : button.transform.parent.GetInstanceID()},
            {"name", button.name}, {"callback", callback.Name}
        }};
        return new JObject {
            {"menuInstanceId", menu.GetInstanceID()},
            {"showing", menu.m_Showing},
            {"saveExitCandidates", observed},
            {"inCombat", EncounterSession.Instance.m_IsInCombat}
        };
    }

    JObject NativeSaveExit(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "inspectionToken", "buttonInstanceId");
        CatalogNoLinks(root);
        string action = Str(command, "action");
        if (action != "inspect" && action != "submit") throw new ArgumentException("Use inspect or submit.");
        if (nativeSaveExitConsumed) throw new InvalidOperationException("Native Save/Exit already consumed for this process.");
        uiOptionsMenu menu;
        List<Button> candidates;
        System.Reflection.MethodInfo callback;
        JObject current = InspectNativeSaveExit(out menu, out candidates, out callback);
        Button button = candidates[0];
        if (action == "inspect")
        {
            nativeSaveExitToken = Guid.NewGuid().ToString("N");
            nativeSaveExitMenu = menu;
            nativeSaveExitButton = button;
            nativeSaveExitCallback = callback;
            current["inspectionToken"] = nativeSaveExitToken;
            current["selectedButtonInstanceId"] = button.GetInstanceID();
            return new JObject { {"ok", true}, {"readOnly", true}, {"status", "native_save_exit_eligible"}, {"pins", current} };
        }
        if (nativeSaveExitMenu != menu || nativeSaveExitButton != button || nativeSaveExitCallback != callback
            || Str(command, "inspectionToken") != nativeSaveExitToken
            || Int(command, "buttonInstanceId", 0) != button.GetInstanceID()
            || !candidates.Contains(button))
            throw new InvalidOperationException("Exact inspected native Save/Exit control changed.");
        nativeSaveExitConsumed = true;
        callback.Invoke(menu, null);
        return new JObject {
            {"ok", true}, {"status", "native_save_exit_submitted"},
            {"callback", "uiOptionsMenu.OnSaveExit for the inspected m_SaveExit control"},
            {"buttonInstanceId", button.GetInstanceID()}, {"menuInstanceId", menu.GetInstanceID()},
            {"scope", "One exact native Save/Exit UnityEvent. Save completion and process exit must be observed separately."}
        };
    }
}
