using System;

namespace FTKModFramework.Core.UI
{
    /// <summary>Which reporting control receives focus when the panel gains input focus or switches
    /// view. Keep this file free of UnityEngine so the game-free ReportingSubmission harness can
    /// exercise the policy.</summary>
    internal static class ReportingPanelFocus
    {
        internal enum View { Editor, Drafts, Leave, Delete }
        internal enum Control { Description, SaveDraft, Details, Drafts, Diagnostics, Primary, Secondary, Back, OpenDraft }

        // Editor: Primary sends or retries a public GitHub submission and Secondary can discard a
        // pending local copy, so a single confirm must reach neither. The diagnostics toggle
        // changes what a send shares, so it is not a default either.
        private static readonly Control[] EditorSafe = { Control.Description, Control.SaveDraft, Control.Details, Control.Drafts };
        // Drafts: Primary starts a new report and each row's Delete opens a confirmation; opening
        // the first listed draft only returns to the editor.
        private static readonly Control[] DraftsSafe = { Control.OpenDraft };
        // Leave: Primary saves and leaves, Secondary discards edits, Back keeps editing.
        private static readonly Control[] LeaveSafe = { };
        // Delete: Primary deletes the draft; Secondary keeps it.
        private static readonly Control[] DeleteSafe = { Control.Secondary };

        /// <summary>The first usable safe control for the view, else Back. Back is shown in every
        /// view and only leaves or returns, so it is the fallback even while it is briefly not
        /// interactable.</summary>
        internal static Control Default(View view, Predicate<Control> usable)
        {
            foreach (Control control in Safe(view)) if (usable(control)) return control;
            return Control.Back;
        }

        private static Control[] Safe(View view)
        {
            if (view == View.Drafts) return DraftsSafe;
            if (view == View.Leave) return LeaveSafe;
            if (view == View.Delete) return DeleteSafe;
            return EditorSafe;
        }
    }
}
