using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

// Isolated-test-only native UI bridge. It never writes save data or calls a
// save API. The one mutating route requires the visible Options Menu's exact
// serialized Save/Exit control and dispatches the menu's UI callback once.
public sealed partial class RuntimeModelTest
{
    bool nativeSaveExitConsumed;
    string nativeSaveExitToken;
    uiOptionsMenu nativeSaveExitMenu;
    Button nativeSaveExitControl;
    UnityEngine.Object nativeSaveExitCallbackTarget;
    string nativeSaveExitCallbackMethod;

    static Button NativeSaveExitControl(uiOptionsMenu menu)
    {
        System.Reflection.FieldInfo saveOptions = menu.GetType().GetField("m_SaveOptions", Members);
        object panel = saveOptions == null ? null : saveOptions.GetValue(menu);
        System.Reflection.FieldInfo exit = panel == null ? null : panel.GetType().GetField("m_SaveExit", Members);
        return exit == null ? null : exit.GetValue(panel) as Button;
    }

    static bool NativeSaveExitControlActive(UnityEngine.Object control)
    {
        Component component = control as Component;
        GameObject gameObject = control as GameObject;
        if (component != null) gameObject = component.gameObject;
        return gameObject != null && gameObject.activeInHierarchy;
    }

    // Read-only field and callback metadata for a visibly open native menu. This
    // exists so an unfamiliar shipped control shape can be anchored before the
    // mutating route is expanded. It neither invokes a callback nor writes data.
    JObject NativeSaveExitState(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        uiOptionsMenu menu = uiOptionsMenu.Instance;
        if (menu == null || !SceneOwner(menu) || !menu.gameObject.activeInHierarchy || !menu.m_Showing)
            throw new InvalidOperationException("Visible native Options Menu is required.");
        JArray fields = new JArray();
        System.Reflection.BindingFlags declared = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly;
        for (Type type = menu.GetType(); type != null; type = type.BaseType)
        {
            foreach (System.Reflection.FieldInfo field in type.GetFields(declared))
            {
                if (field.Name.IndexOf("save", StringComparison.OrdinalIgnoreCase) < 0) continue;
                object value = field.GetValue(menu);
                UnityEngine.Object unity = value as UnityEngine.Object;
                Component component = unity as Component;
                GameObject gameObject = unity as GameObject;
                if (component != null) gameObject = component.gameObject;
                fields.Add(new JObject {
                    {"declaringType", type.FullName}, {"name", field.Name}, {"fieldType", field.FieldType.FullName},
                    {"valueType", value == null ? null : value.GetType().FullName},
                    {"isUnityObject", unity != null}, {"instanceId", unity == null ? 0 : unity.GetInstanceID()},
                    {"activeInHierarchy", gameObject != null && gameObject.activeInHierarchy}
                });
            }
        }
        JArray callbacks = new JArray();
        for (Type type = menu.GetType(); type != null; type = type.BaseType)
        {
            foreach (System.Reflection.MethodInfo method in type.GetMethods(declared))
            {
                if (method.Name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 && method.GetParameters().Length == 0)
                    callbacks.Add(new JObject {{"declaringType", type.FullName}, {"name", method.Name}});
            }
        }
        return new JObject {{"ok", true}, {"readOnly", true}, {"status", "native_save_exit_metadata"},
            {"menuType", menu.GetType().FullName}, {"menuInstanceId", menu.GetInstanceID()},
            {"showing", menu.m_Showing}, {"saveFields", fields}, {"saveCallbacks", callbacks}};
    }

    JObject InspectNativeSaveExit(out uiOptionsMenu menu, out List<Button> candidates, out UnityEngine.Object callbackTarget, out string callbackMethod)
    {
        RequireSinglePlayer();
        menu = uiOptionsMenu.Instance;
        candidates = new List<Button>();
        callbackTarget = null;
        callbackMethod = null;
        if (menu == null || !SceneOwner(menu) || !menu.gameObject.activeInHierarchy || !menu.m_Showing
            || FTKHub.Instance == null || GameLogic.Instance == null || !GameLogic.Instance.IsSinglePlayer()
            || EncounterSession.Instance == null || EncounterSession.Instance.m_IsInCombat)
            throw new InvalidOperationException("Visible native Options Menu outside combat is required.");
        Button control = NativeSaveExitControl(menu);
        if (control == null || !NativeSaveExitControlActive(control) || !control.IsInteractable()
            || control.onClick.GetPersistentEventCount() != 1)
            throw new InvalidOperationException("Exact visible native Save/Exit UI control and callback are required.");
        callbackTarget = control.onClick.GetPersistentTarget(0) as UnityEngine.Object;
        callbackMethod = control.onClick.GetPersistentMethodName(0);
        if (callbackTarget == null || string.IsNullOrEmpty(callbackMethod))
            throw new InvalidOperationException("Exact visible native Save/Exit UI callback is required.");
        candidates.Add(control);
        JArray observed = new JArray { new JObject {
            {"buttonInstanceId", control.GetInstanceID()},
            {"controlType", control.GetType().FullName},
            {"name", control.name}, {"callbackTargetType", callbackTarget.GetType().FullName}, {"callback", callbackMethod}
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
        UnityEngine.Object callbackTarget;
        string callbackMethod;
        JObject current = InspectNativeSaveExit(out menu, out candidates, out callbackTarget, out callbackMethod);
        Button control = candidates[0];
        if (action == "inspect")
        {
            nativeSaveExitToken = Guid.NewGuid().ToString("N");
            nativeSaveExitMenu = menu;
            nativeSaveExitControl = control;
            nativeSaveExitCallbackTarget = callbackTarget;
            nativeSaveExitCallbackMethod = callbackMethod;
            current["inspectionToken"] = nativeSaveExitToken;
            current["selectedButtonInstanceId"] = control.GetInstanceID();
            return new JObject { {"ok", true}, {"readOnly", true}, {"status", "native_save_exit_eligible"}, {"pins", current} };
        }
        if (nativeSaveExitMenu != menu || nativeSaveExitControl != control || nativeSaveExitCallbackTarget != callbackTarget || nativeSaveExitCallbackMethod != callbackMethod
            || Str(command, "inspectionToken") != nativeSaveExitToken
            || Int(command, "buttonInstanceId", 0) != control.GetInstanceID()
            || !candidates.Contains(control))
            throw new InvalidOperationException("Exact inspected native Save/Exit control changed.");
        nativeSaveExitConsumed = true;
        control.onClick.Invoke();
        return new JObject {
            {"ok", true}, {"status", "native_save_exit_submitted"},
            {"callback", callbackTarget.GetType().FullName + "." + callbackMethod + " for the inspected m_SaveGameButton control"},
            {"buttonInstanceId", control.GetInstanceID()}, {"menuInstanceId", menu.GetInstanceID()},
            {"scope", "One exact native Save/Exit UI callback. Save completion and process exit must be observed separately."}
        };
    }
}
