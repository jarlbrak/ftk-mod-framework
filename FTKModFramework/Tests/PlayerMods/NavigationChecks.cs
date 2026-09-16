// Game-free checks for the mods panel back-stack policy. These exercise ModsPanelNavigation only:
// rendering, layout, and controller focus in the live panel are not covered here.
using System;
using System.Collections.Generic;
using FTKModFramework.Core.UI;

internal static class NavigationChecks
{
    private static void Assert(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: navigation / " + name);
    }

    private sealed class Panel
    {
        internal readonly ModsPanelNavigation Navigation = new ModsPanelNavigation();
        internal string View = "installed";
        internal int Page;
        internal int Focus = 1;
        internal bool Closed;

        internal void Navigate(string view)
        {
            ModsPanelNavigation.Frame restored;
            ModsPanelNavigation.Frame current = new ModsPanelNavigation.Frame { View = View, Page = Page, Focus = Focus };
            if (!Navigation.Enter(view, current, out restored)) { Page = 0; return; }
            if (restored != null) { View = restored.View; Page = restored.Page; Focus = restored.Focus; return; }
            View = view;
            Page = 0;
            Focus = ModsPanelNavigation.DefaultFocus(view);
        }

        internal void Back()
        {
            ModsPanelNavigation.Frame parent = Navigation.Back();
            if (parent == null) { Closed = true; return; }
            View = parent.View;
            Page = parent.Page;
            Focus = parent.Focus;
        }
    }

    internal static void Run()
    {
        Assert(ModsPanelNavigation.IsRoot("installed") && ModsPanelNavigation.IsRoot("discover") && ModsPanelNavigation.IsRoot("updates"),
            "the three tabs are navigation roots");
        Assert(!ModsPanelNavigation.IsRoot("components") && ModsPanelNavigation.IsBrowse("components"),
            "required components is a browse view reached from Installed, not a root");
        Assert(ModsPanelNavigation.IsBrowse("installed") && ModsPanelNavigation.IsBrowse("discover") && !ModsPanelNavigation.IsBrowse("settings"),
            "browse views clear the mod selection on entry");
        Assert(!ModsPanelNavigation.IsRestorable("confirm") && ModsPanelNavigation.IsRestorable("maintenance"),
            "a confirmation is never restorable");

        // Contextual Back restores the prior view and its selection state.
        Panel panel = new Panel();
        panel.Page = 3;
        panel.Navigate("components");
        Assert(panel.View == "components" && panel.Navigation.Depth == 1, "Components pushes Installed as its parent");
        panel.Back();
        Assert(panel.View == "installed" && panel.Page == 3 && !panel.Closed, "Back from Components restores Installed and its page");

        // A root tab starts a new journey.
        panel = new Panel();
        panel.Navigate("components");
        panel.Navigate("settings");
        Assert(panel.Navigation.Depth == 2, "settings stacks on components");
        panel.Navigate("discover");
        Assert(panel.Navigation.Depth == 0, "a tab root discards history");
        panel.Back();
        Assert(panel.Closed, "Back from a root with no history closes the panel");

        // Re-entering the current view is not history.
        panel = new Panel();
        panel.Page = 2;
        panel.Navigate("installed");
        Assert(panel.Navigation.Depth == 0 && panel.Page == 0, "re-entering the current view resets it without stacking");

        // Revisiting an ancestor unwinds to it rather than duplicating it.
        panel = new Panel();
        panel.Navigate("settings");
        panel.Page = 4;
        panel.Navigate("maintenance");
        panel.Navigate("settings");
        Assert(panel.View == "settings" && panel.Page == 4 && panel.Navigation.Depth == 1 && !panel.Navigation.Contains("maintenance"),
            "returning to an ancestor restores its frame and unwinds the stack above it");
        panel.Back();
        Assert(panel.View == "installed", "the ancestor's own parent is still intact");

        // Leaving a confirmation never leaves it behind for Back to re-enter.
        panel = new Panel();
        panel.Navigate("maintenance");
        panel.Navigate("confirm");
        Assert(panel.Navigation.Depth == 2, "confirm stacks on the view that opened it");
        panel.Navigate("maintenance");
        Assert(panel.View == "maintenance" && panel.Navigation.Depth == 1 && !panel.Navigation.Contains("confirm"),
            "confirming returns to the existing maintenance frame instead of duplicating it");
        panel.Back();
        Assert(panel.View == "installed" && !panel.Closed, "one Back after confirming leaves the panel on Installed");

        // A confirmation opened from a mod page is not restorable behind the next operation.
        panel = new Panel();
        panel.Navigate("confirm");
        panel.Navigate("maintenance");
        Assert(!panel.Navigation.Contains("confirm") && panel.Navigation.Depth == 1,
            "a confirmation is dropped from history when it is left");

        // No view can appear twice, and the stack cannot grow without bound.
        panel = new Panel();
        string[] loop = { "components", "settings", "maintenance", "confirm", "maintenance", "settings", "components", "search", "settings" };
        for (int round = 0; round < 25; round++)
            foreach (string view in loop) panel.Navigate(view);
        // The stack is bounded because no view can appear in it twice, so derive the limit from the
        // distinct views the loop visits rather than asserting an observed number.
        List<string> distinct = new List<string>();
        foreach (string view in loop) if (!distinct.Contains(view)) distinct.Add(view);
        Assert(panel.Navigation.Depth <= distinct.Count, "an adversarial click loop keeps the stack bounded (depth " + panel.Navigation.Depth + " of " + distinct.Count + " distinct views)");
        int steps = 0;
        while (!panel.Closed && steps < 32) { panel.Back(); steps++; }
        Assert(panel.Closed, "Back always reaches the panel exit in a bounded number of presses (" + steps + ")");

        // Controller focus is an index into the enabled controls; only restore it when they match.
        Assert(ModsPanelNavigation.ResolveFocus(3, 7, 7, 1) == 3, "focus is restored when the control count is unchanged");
        Assert(ModsPanelNavigation.ResolveFocus(3, 7, 6, 1) == 1, "a changed control count falls back to the view default");
        Assert(ModsPanelNavigation.ResolveFocus(9, 9, 4, 2) == 2, "an out-of-range focus falls back to the view default");
        Assert(ModsPanelNavigation.ResolveFocus(0, 0, 0, 1) == 0, "an empty view asks for the first control");
        Assert(ModsPanelNavigation.ResolveFocus(1, 3, 2, 5) == 0, "a fallback beyond the rebuilt view is clamped to the first control");
        Assert(ModsPanelNavigation.DefaultFocus("updates") == 2 && ModsPanelNavigation.DefaultFocus("installed") == 1 && ModsPanelNavigation.DefaultFocus("settings") == 0,
            "each view keeps its default controller focus");
    }
}
