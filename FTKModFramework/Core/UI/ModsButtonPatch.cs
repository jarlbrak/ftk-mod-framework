using System;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace FTKModFramework.Core.UI
{
    /// <summary>
    /// Injects a native "Mods" button onto the For The King title screen by cloning one of the
    /// existing menu buttons, relabelling it, and rewiring its click.
    ///
    /// HOOK CHOICE: Postfix of StartGameFE.MainScreen.OnSetFocus(). Verified against the real
    /// Assembly-CSharp (MainScreen : uiScreen : FTKInputFocus : MonoBehaviour). The focus pipeline
    /// FTKInput.SetFocus runs OnPreSetFocus(); SetFocus(_setCurrent); OnSetFocus() in that order, and
    /// the screen subtree is activated INSIDE uiScreen.SetFocus (base.gameObject.SetActive(true)). So at
    /// OnSetFocus time the menu buttons are already active and clonable (strictly safer than
    /// OnPreSetFocus, where the subtree is not yet active). The nav auto-scan
    /// FTKInputFocus.SetupNavigation() runs DEFERRED in FTKInputFocus.Update() (gated by
    /// m_IsUpdateSelected, a frame or two later), NOT synchronously, so a button parented under
    /// m_SelectableParent in this postfix is present before the scan and gets auto-wired for
    /// keyboard/controller navigation.
    ///
    /// IDEMPOTENCY: MainScreen has no Unity lifecycle methods; OnSetFocus fires on EVERY return to the
    /// title screen and the menu buttons persist (only their active state is toggled). So this postfix
    /// runs repeatedly. A static _done guard plus a Find("ModsButton") check under the menu parent
    /// prevent a duplicate button.
    ///
    /// Registration is automatic: Plugin.Awake calls _harmony.PatchAll(), which discovers this
    /// [HarmonyPatch] class (same as Plugin.cs's TableManager_Initialize_Patch).
    /// </summary>
    [HarmonyPatch(typeof(StartGameFE.MainScreen), "OnSetFocus")]
    internal static class MainScreen_OnSetFocus_Patch
    {
        // GameObject name of the injected button. Doubles as the duplicate-prevention key (the Find
        // check below locates it by this name).
        private const string ModsButtonName = "ModsButton";

        // _done short-circuits the common path after a successful inject. It is NOT the only guard:
        // the Find("ModsButton") check is authoritative against duplicates even if _done were reset
        // (e.g. on a domain reload), and it is what we rely on when an earlier attempt failed.
        private static bool _done;

        private static void Postfix(StartGameFE.MainScreen __instance)
        {
            if (_done) return;
            if (__instance == null) return;

            // m_SelectableParent (inherited from FTKInputFocus, public Transform) is both the parent the
            // nav auto-scan walks (GetComponentsInChildren<FTKSelectable>) and where we look for an
            // existing ModsButton. No parent => nothing to clone under and no nav; bail without arming
            // _done so a later, fully-built show can retry.
            Transform menuParent = __instance.m_SelectableParent;
            if (menuParent == null) return;

            // Duplicate guard: if a previous show already injected the button, do nothing.
            if (menuParent.Find(ModsButtonName) != null)
            {
                _done = true;
                return;
            }

            GameObject clone = null;
            try
            {
                GameObject source = FindCloneSource(menuParent, __instance.m_ResumeButton);
                if (source == null)
                {
                    Plugin.Log.LogWarning("[ftkmf] Mods button: no clonable menu button found under " +
                        "m_SelectableParent (and no m_ResumeButton fallback). Title screen left unchanged.");
                    return;
                }

                // Clone the source button GameObject under the same menu parent. Instantiating with the
                // parent keeps it in the FTKSelectable subtree the nav scan walks, and inherits the
                // source's FTKSelectable/Button/layout for free.
                clone = UnityEngine.Object.Instantiate(source, menuParent);
                clone.name = ModsButtonName;

                // Place it just below the source by default. The final index is COSMETIC (the spec's
                // Open Question): the auto-nav scan wires up/down by sibling order regardless, so any
                // in-list position is navigable. Sit directly under the source for a sensible default.
                clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

                // Label: a NON-"STR_" literal. FTKLocalizationUI.Start() only rewrites Text that
                // StartsWith("STR_") and only once, so "Mods" survives untouched even if the cloned
                // subtree carries that component.
                Text label = clone.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = "Mods";
                else
                    Plugin.Log.LogWarning("[ftkmf] Mods button: cloned button had no Text child to " +
                        "label; button added without a caption.");

                // Rewire the click: drop the inherited handler(s) from the source button, then point at
                // our placeholder. #18 replaces OpenModsPanel with code that opens the real panel.
                Button button = clone.GetComponent<Button>();
                if (button != null)
                {
                    // The source button's click is wired IN-SCENE as PERSISTENT UnityEvent listeners (e.g.
                    // Resume/New Game). UnityEvent.RemoveAllListeners() drops only RUNTIME listeners, so the
                    // persistent ones survive a naive clear and the clone would still fire the source action
                    // (observed: clicking "Mods" opened the Resume screen). Disable every persistent listener
                    // first, THEN clear runtime listeners, THEN wire our own, so only OpenModsPanel runs.
                    for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                        button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(OpenModsPanel);
                }
                else
                {
                    Plugin.Log.LogWarning("[ftkmf] Mods button: clone has no Button component; click " +
                        "not wired. (Clone source should always carry a Button.)");
                }

                _done = true;
                Plugin.Log.LogInfo("[ftkmf] Mods button added to the title screen (cloned '" +
                    source.name + "').");
            }
            catch (Exception e)
            {
                // Defensive no-op: any failure must leave the title screen unaffected and, critically,
                // leave NO partial or duplicate button behind. If we already Instantiated the clone
                // before throwing, destroy it here.
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);
                Plugin.Log.LogError("[ftkmf] Mods button injection failed (title screen left " +
                    "unchanged): " + e);
            }
        }

        /// <summary>
        /// Robustly pick a GameObject to clone for the Mods button. Preferred: the first ACTIVE child of
        /// the menu parent that carries a UnityEngine.UI.Button (the stock front-end menu buttons).
        /// Fallback: m_ResumeButton (a Transform we always have a ref to; may be inactive when no save
        /// exists, but is still a valid clone source). Deliberately makes no assumption that a specific
        /// field exists for the menu buttons (there is none: they are scene-wired children).
        /// </summary>
        private static GameObject FindCloneSource(Transform menuParent, Transform resumeButton)
        {
            int count = menuParent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = menuParent.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.GetComponent<Button>() != null)
                    return child.gameObject;
            }

            if (resumeButton != null)
                return resumeButton.gameObject;

            return null;
        }

        /// <summary>
        /// Title-button click target: opens the real Mods panel (<see cref="ModsPanel"/>), which builds itself
        /// lazily on first call and shows via FTKInput.Instance.SetFocus. Fully guarded: any failure to build
        /// or show the panel is logged and swallowed so the title screen stays usable (no soft-lock, no
        /// unhandled exception bubbling out of the button click).
        /// </summary>
        private static void OpenModsPanel()
        {
            try
            {
                ModsPanel.Open();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[ftkmf] Failed to open the Mods panel (title screen left usable): " + e);
            }
        }
    }
}
