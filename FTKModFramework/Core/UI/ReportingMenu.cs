using System;
using System.Reflection;
using FTKModFramework.Core.HotReload;
using FTKModFramework.Core.Reporting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    // Keep Options as the native pause/focus owner for manual reporting.
    internal static class ReportingMenu
    {
        private static readonly FieldInfo InputField = typeof(FTKInput).GetField("gFTKInput", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static uiOptionsMenu owner;
        private static ReportingPanel panel;
        private static StartGameFE.MainScreen title;
        private static int titleFrames;
        private static Button disabledEntry;
        private static bool priorInteractable;
        internal static ReportingPanel CurrentPanel { get { return panel; } }
        private static FTKInput Input { get { return InputField == null ? null : InputField.GetValue(null) as FTKInput; } }

        internal static void TitleFocused(StartGameFE.MainScreen observed)
        { title = observed; titleFrames = 0; }

        internal static void Bind(uiOptionsMenu menu)
        {
            try
            {
                if (!menu || !menu.m_ReportBugs || !menu.m_ReportBugs.gameObject.scene.IsValid() ||
                    !menu.m_ReportBugs.GetComponent<FTKSelectable>() || !menu.m_ReportBugs.GetComponentInChildren<Text>(true) ||
                    !menu.m_MainOptions || !menu.m_MainOptions.m_SubBlocker || !menu.m_AudioOptions)
                    throw new InvalidOperationException();
                if (owner != menu)
                {
                    if (panel) UnityEngine.Object.Destroy(panel.gameObject);
                    panel = null;
                }
                owner = menu;
                if (disabledEntry == menu.m_ReportBugs)
                { disabledEntry.interactable = priorInteractable; disabledEntry = null; }
            }
            catch
            {
                Disable(menu);
                owner = null;
            }
        }

        private static void Disable(uiOptionsMenu menu)
        {
            if (!menu || !menu.m_ReportBugs) return;
            if (disabledEntry != menu.m_ReportBugs)
            { disabledEntry = menu.m_ReportBugs; priorInteractable = disabledEntry.interactable; }
            disabledEntry.interactable = false;
        }

        internal static void Tick()
        {
            try
            {
                FTKInput input = Input;
                if (!input) return;
                if (panel && panel.gameObject.activeInHierarchy) { panel.ObserveDraftReadiness(); return; }
                uiStartGame start = uiStartGame.Instance;
                if (start && start.m_GameStarted) ReportingRuntime.ObservePhase("session_or_transition");
                bool ready = title && title.gameObject.activeInHierarchy && title.m_HasInputFocus &&
                    input.InputFocus == title && title.m_CurrentSelected && !input.m_WaitingForPopup &&
                    ModSplash.Finished && !HotReloadBoundary.NavigationLocked && start && !start.m_GameStarted;
                if (!ready) { titleFrames = 0; return; }
                if (++titleFrames >= 2) ReportingRuntime.ObservePhase("title");
            }
            catch
            {
                Plugin.Log.LogWarning("Reporting menu is unavailable in this context.");
            }
        }

        internal static bool Open()
        {
            FTKInput input = Input;
            if (!owner || !input || !owner.m_Showing || input.InputFocus != owner.m_MainOptions ||
                !owner.m_MainOptions.m_HasInputFocus || input.m_WaitingForPopup || HotReloadBoundary.NavigationLocked)
                return false;
            FTKInputState rollback = new FTKInputState();
            FTKInputState parentSaved = owner.m_MainOptions.m_InputState;
            FTKInputFocus parentFocus = parentSaved == null ? null : parentSaved.m_InputFocus;
            bool blocker = owner.m_MainOptions.m_SubBlocker.gameObject.activeSelf;
            try
            {
                if (!panel) panel = ReportingPanel.Create(owner, owner.m_ReportBugs);
                if (!panel.HasUnfinishedSubmission)
                    panel.BeginReport(ReportingRuntime.CreateReport(false));
                owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(true);
                input.SetFocus(panel, null, true);
                if (input.InputFocus != panel || !panel.m_HasInputFocus) throw new InvalidOperationException();
                return true;
            }
            catch
            {
                if (parentSaved != null) parentSaved.m_InputFocus = parentFocus;
                owner.m_MainOptions.m_InputState = parentSaved;
                if (input.InputFocus != owner.m_MainOptions) rollback.Restore();
                owner.m_MainOptions.m_InputState = parentSaved;
                if (panel) { panel.gameObject.SetActive(false); panel.m_InputState = null; }
                owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(blocker);
                Disable(owner);
                Plugin.Log.LogWarning("Reporting could not open. The native menu remains available.");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(StartGameFE.MainScreen), "OnSetFocus")]
    internal static class ReportingTitlePatch
    { private static void Postfix(StartGameFE.MainScreen __instance) { ReportingMenu.TitleFocused(__instance); } }

    [HarmonyPatch(typeof(uiOptionsMenu), "Show")]
    internal static class ReportingOptionsPatch
    { private static void Postfix(uiOptionsMenu __instance) { ReportingMenu.Bind(__instance); } }

    [HarmonyPatch(typeof(uiOptionsMain), "OnReportBugs")]
    internal static class ReportingEntryPatch
    { private static bool Prefix() { try { ReportingMenu.Open(); } catch { Plugin.Log.LogWarning("Reporting is unavailable in this context."); } return false; } }

    [HarmonyPatch(typeof(SerializeGO), "ShowBugForm", new[] { typeof(Action) })]
    internal static class ReportingLegacyCapturePatch
    { private static bool Prefix() { return false; } }

    [HarmonyPatch(typeof(uiSystemDialog), "ShowBugForm", new[] { typeof(Action) })]
    internal static class ReportingLegacyFormPatch
    { private static bool Prefix() { return false; } }
}
