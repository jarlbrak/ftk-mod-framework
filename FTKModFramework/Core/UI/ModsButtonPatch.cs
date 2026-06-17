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
    /// m_IsUpdateSelected, a frame or two later), NOT synchronously, so a button parented into the menu
    /// subtree in this postfix is present before the scan and gets auto-wired for keyboard/controller nav.
    ///
    /// CLONE SOURCE: the vertical menu buttons are scene-wired children nested BELOW m_SelectableParent
    /// (they are NOT direct children, and there is no C# field for them). The menu is a GridLayoutGroup
    /// ("ButtonRoot") whose CELLS are wrapper objects (e.g. "New"), each holding one playButton. We search
    /// the whole subtree for a real Button, skip the Resume/Continue cell (it can carry save-conditional
    /// show/hide logic), then clone that button's WRAPPER CELL into the grid so the layout gives the Mods
    /// button its own row (cloning into the single-button wrapper instead just overlaps the original).
    ///
    /// CLICK REWIRE: the source button's action is wired IN-SCENE as PERSISTENT UnityEvent listeners.
    /// UnityEvent.RemoveAllListeners() removes only RUNTIME listeners, so the persistent ones must be
    /// disabled explicitly or the clone still fires the source action (observed: clicking "Mods" opened the
    /// Resume screen / loaded a save). We disable persistent listeners, clear runtime listeners, then wire
    /// OpenModsPanel, so only our handler runs.
    ///
    /// IDEMPOTENCY: MainScreen has no Unity lifecycle methods; OnSetFocus fires on EVERY return to the
    /// title screen. A static _done guard plus a recursive name check (the clone is nested, so a shallow
    /// Find would miss it) prevent a duplicate button.
    ///
    /// Registration is automatic: Plugin.Awake calls _harmony.PatchAll(), which discovers this
    /// [HarmonyPatch] class (same as Plugin.cs's TableManager_Initialize_Patch).
    /// </summary>
    [HarmonyPatch(typeof(StartGameFE.MainScreen), "OnSetFocus")]
    internal static class MainScreen_OnSetFocus_Patch
    {
        // GameObject name of the injected button. Doubles as the duplicate-prevention key.
        private const string ModsButtonName = "ModsButton";

        // _done short-circuits after a successful inject. The recursive name check is the authoritative
        // duplicate guard (it survives even if _done were reset, e.g. on a domain reload).
        private static bool _done;

        private static void Postfix(StartGameFE.MainScreen __instance)
        {
            if (_done) return;
            if (__instance == null) return;

            // m_SelectableParent (inherited from FTKInputFocus, public Transform) roots the menu subtree the
            // nav auto-scan walks (GetComponentsInChildren<FTKSelectable>). No parent => nothing to clone and
            // no nav; bail without arming _done so a later, fully-built show can retry.
            Transform menuParent = __instance.m_SelectableParent;
            if (menuParent == null) return;

            // Duplicate guard, recursive: the clone is nested below menuParent, so a shallow Find would miss it.
            if (FindDeep(menuParent, ModsButtonName) != null)
            {
                _done = true;
                return;
            }

            GameObject clone = null;
            try
            {
                Button source = FindCloneSource(menuParent);
                if (source == null)
                {
                    Plugin.Log.LogWarning("[ftkmf] Mods button: no active UnityEngine.UI.Button found under " +
                        "m_SelectableParent to clone. Title screen left unchanged.");
                    return;
                }

                // The title menu is a GridLayoutGroup (ButtonRoot) whose CELLS are wrapper objects (e.g.
                // "Resume"), each holding the real playButton. The clone must be a NEW CELL in that grid, not a
                // second child inside the single-button wrapper (that just overlaps the original, which was the
                // bug). So clone the source button's WRAPPER (its parent) into the GRID (the wrapper's parent);
                // the GridLayoutGroup then lays the Mods cell out as its own row.
                Transform wrapper = source.transform.parent;
                Transform grid = wrapper != null ? wrapper.parent : null;
                GameObject cellToClone = wrapper != null ? wrapper.gameObject : source.gameObject;
                Transform cellParent = grid != null ? grid : menuParent;

                clone = UnityEngine.Object.Instantiate(cellToClone, cellParent);
                clone.name = ModsButtonName;

                // Place the Mods cell directly after the "Lore" (Lore Store) cell. The grid lays out by sibling
                // order and the nav scan follows the same order. Fall back to just after the source cell if the
                // Lore cell is not found.
                Transform anchorCell = grid != null ? FindCellByName(grid, "Lore") : null;
                if (anchorCell == null) anchorCell = wrapper;
                if (anchorCell != null)
                    clone.transform.SetSiblingIndex(anchorCell.GetSiblingIndex() + 1);

                // Label: a NON-"STR_" literal. FTKLocalizationUI.Start() only rewrites Text that
                // StartsWith("STR_") and only once, so "Mods" survives untouched.
                Text label = clone.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = "Mods";
                else
                    Plugin.Log.LogWarning("[ftkmf] Mods button: cloned cell had no Text child to label; " +
                        "button added without a caption.");

                // Rewire the inner Button (the clone is a cell, so its Button is a child). Disable the source's
                // PERSISTENT listeners (they survive RemoveAllListeners and would otherwise fire the source
                // action, e.g. Resume/New Game), clear runtime listeners, then wire OpenModsPanel.
                Button button = clone.GetComponentInChildren<Button>(true);
                if (button == null)
                {
                    // An unwired button is worse than none: remove it rather than leave a button that fires
                    // nothing or the wrong action.
                    Plugin.Log.LogWarning("[ftkmf] Mods button: cloned cell has no Button; removing it.");
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OpenModsPanel);

                _done = true;
                Plugin.Log.LogInfo("[ftkmf] Mods button added to the title screen (cloned cell '" +
                    cellToClone.name + "', placed in grid, click -> Mods panel).");
            }
            catch (Exception e)
            {
                // Defensive no-op: any failure must leave the title screen unaffected and leave NO partial or
                // duplicate button behind. Destroy the clone if it was already instantiated.
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);
                Plugin.Log.LogError("[ftkmf] Mods button injection failed (title screen left unchanged): " + e);
            }
        }

        /// <summary>
        /// Find a real menu Button to clone. The vertical menu buttons can be nested BELOW m_SelectableParent
        /// (not direct children), so search the whole subtree. Prefer an ACTIVE button that is not
        /// Resume/Continue (skipping it dodges any save-load wiring on that specific button); fall back to the
        /// first active button. Returns null only if the subtree has no active Button. Logs the candidate
        /// names once, so a misfire is diagnosable from the log.
        /// </summary>
        private static Button FindCloneSource(Transform menuParent)
        {
            Button[] buttons = menuParent.GetComponentsInChildren<Button>(true); // include inactive
            Button firstActive = null;
            Button preferred = null;
            System.Text.StringBuilder seen = new System.Text.StringBuilder();

            foreach (Button b in buttons)
            {
                if (b == null) continue;
                bool active = b.gameObject.activeInHierarchy;
                Transform wrapper = b.transform.parent;
                string wname = wrapper != null ? wrapper.name : "(none)";
                if (seen.Length > 0) seen.Append(", ");
                seen.Append(b.gameObject.name).Append("/").Append(wname);
                if (!active) { seen.Append("(inactive)"); continue; }

                if (firstActive == null) firstActive = b;
                // Skip the Resume/Continue cell: its wrapper may carry save-conditional show/hide logic that
                // would then govern the cloned Mods button too. Match on the wrapper (cell) name, which is
                // "Resume", since the buttons themselves are all generically named "playButton".
                if (!LooksLikeResume(b.gameObject.name) && !LooksLikeResume(wname) && preferred == null)
                    preferred = b;
            }

            Plugin.Log.LogDebug("[ftkmf] Mods button: candidates (button/cell) = [" + seen + "].");
            return preferred != null ? preferred : firstActive;
        }

        private static bool LooksLikeResume(string n)
        {
            return n != null && (n.IndexOf("Resume", StringComparison.OrdinalIgnoreCase) >= 0
                              || n.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Recursive (whole-subtree) search for a transform by exact name; null if absent.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>First DIRECT child of <paramref name="parent"/> whose name contains <paramref name="name"/>
        /// (case-insensitive), or null. Used to anchor the Mods cell's position next to a named menu cell.</summary>
        private static Transform FindCellByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
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
