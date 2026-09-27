using System;

namespace FTKModFramework.Core.UI
{
    /// <summary>Which reporting control receives focus when the panel gains input focus. Keep this
    /// file free of UnityEngine so the game-free ReportingSubmission harness can exercise the policy.</summary>
    internal static class ReportingPanelFocus
    {
        internal enum Control { Description, SaveDraft, Details, Drafts, Diagnostics, Primary, Secondary, Back }

        // Primary sends or retries a public GitHub submission and Secondary can discard a pending
        // local copy, so a single confirm on opening must reach neither. The diagnostics toggle
        // changes what a send shares, so it is not a default either.
        private static readonly Control[] Safe = { Control.Description, Control.SaveDraft, Control.Details, Control.Drafts };

        /// <summary>The first usable safe control, else Back. Back is shown in every view and only
        /// leaves or returns, so it is the fallback even while it is briefly not interactable.</summary>
        internal static Control Default(Predicate<Control> usable)
        {
            foreach (Control control in Safe) if (usable(control)) return control;
            return Control.Back;
        }
    }
}
