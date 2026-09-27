using System.Collections.Generic;

namespace FTKModFramework.Core.UI
{
    /// <summary>Text and paging for the Tweaks tab. Keep this file free of UnityEngine and Plugin
    /// so the game-free Tests/Tweaks harness can compile it with the registry. The panel renders
    /// exactly what this returns, using these heights, so the page budget holds on screen.</summary>
    internal static class ModsPanelTweaks
    {
        internal const string TabTitle = "Tweaks";
        internal const string Intro = "Optional fixes and conveniences for the base game. Choices save immediately.";
        internal const string ResetCaption = "Reset all to defaults";
        internal const string ResetDone = "Every tweak follows its default again.";
        internal const string SaveFailed = "Could not save that choice. BepInEx/LogOutput.log has the details.";
        internal const string Unavailable = "Tweaks are unavailable this launch because their settings could not be read. Every tweak is off and the game runs unchanged. BepInEx/LogOutput.log has the details.";
        internal const string Empty = "No tweaks are available in this version of the framework.";
        internal const string NextRun = "Changes apply to the next run.";

        // Heights in canvas units. The panel's root layout puts ItemGap between blocks and each row
        // block puts LineGap between its button and lines. PageBudget is the stacked view's body
        // height left after the title, tabs, intro row, page buttons and footer.
        internal const int ButtonHeight = 44;
        internal const int HeadingHeight = 32;
        internal const int ShortLine = 26;
        internal const int TallLine = 48;
        internal const int LineGap = 2;
        internal const int ItemGap = 10;
        internal const int PageBudget = 630;

        internal sealed class Line
        {
            internal string Text;
            internal int Height;
            /// <summary>Drawn in the warning color: the faulted marker.</summary>
            internal bool Warning;
        }

        internal sealed class Row
        {
            internal int Handle;
            internal TweakCategory Category;
            internal string Caption;
            internal readonly List<Line> Lines = new List<Line>();

            /// <summary>Block height: the button plus each line with its gap.</summary>
            internal int Height
            {
                get
                {
                    int height = ButtonHeight;
                    foreach (Line line in Lines) height += LineGap + line.Height;
                    return height;
                }
            }
        }

        /// <summary>One entry on a page: a category heading or a row, never both.</summary>
        internal sealed class Item
        {
            internal string Heading;
            internal Row Row;
        }

        internal static string Caption(string title, bool on, TweakPreference preference)
        {
            return title + ": " + (on ? "On" : "Off") + (preference == TweakPreference.Default ? " (default)" : "");
        }

        internal static string ScopeLabel(TweakScope scope)
        {
            return scope == TweakScope.Session
                ? "Shared rules: solo and local play; online co-op off until supported"
                : "Only you";
        }

        /// <summary>A Local fault is off for the rest of the process. A Session fault leaves the
        /// current run's rules alone and drops the tweak from the next capture.</summary>
        internal static string FaultLabel(TweakScope scope)
        {
            return scope == TweakScope.Session
                ? "Turned off after an error, starting with the next run. The current run keeps its rules."
                : "Turned off after an error until the game restarts.";
        }

        internal static string CategoryHeading(TweakCategory category, bool continued)
        {
            string name = category == TweakCategory.Fix ? "Fixes"
                : category == TweakCategory.Information ? "Information" : "Convenience";
            return continued ? name + " (continued)" : name;
        }

        /// <summary>The message shown instead of rows, or null when rows should render.</summary>
        internal static string Status(bool initialized, int count)
        {
            if (!initialized) return Unavailable;
            return count == 0 ? Empty : null;
        }

        internal static Row BuildRow(TweakRegistry registry, int handle)
        {
            TweakDescriptor descriptor = registry.Get(handle);
            Row row = new Row { Handle = handle, Category = descriptor.Category,
                Caption = Caption(descriptor.Title, registry.PreferredOn(handle), registry.Preference(handle)) };
            if (!string.IsNullOrEmpty(descriptor.Summary)) row.Lines.Add(new Line { Text = descriptor.Summary, Height = TallLine });
            string scope = ScopeLabel(descriptor.Scope);
            if (descriptor.Scope == TweakScope.Session) scope += ". " + NextRun;
            row.Lines.Add(new Line { Text = scope, Height = ShortLine });
            if (!string.IsNullOrEmpty(descriptor.BalanceNote))
                row.Lines.Add(new Line { Text = "Balance: " + descriptor.BalanceNote, Height = TallLine });
            if (registry.IsFaulted(handle))
                row.Lines.Add(new Line { Text = FaultLabel(descriptor.Scope), Height = ShortLine, Warning = true });
            return row;
        }

        /// <summary>Rows in the registry's display order, paged. Empty when the registry is not
        /// initialized or has nothing registered; the caller shows Status instead.</summary>
        internal static List<List<Item>> Pages(TweakRegistry registry, int budget)
        {
            List<Row> rows = new List<Row>();
            if (registry.IsInitialized)
                foreach (int handle in registry.DisplayOrder) rows.Add(BuildRow(registry, handle));
            return Paginate(rows, budget);
        }

        /// <summary>Fills pages up to the budget. Every page starts with its category heading, marked
        /// continued when the previous page ended in the same category, and a heading also opens each
        /// new category. A row taller than the budget still gets its own page, so none is dropped.</summary>
        internal static List<List<Item>> Paginate(IList<Row> rows, int budget)
        {
            List<List<Item>> pages = new List<List<Item>>();
            List<Item> page = new List<Item>();
            int used = 0;
            bool hasPrevious = false;
            TweakCategory previous = TweakCategory.Fix;
            foreach (Row row in rows)
            {
                bool sameCategory = hasPrevious && previous == row.Category;
                int rowCost = row.Height + ItemGap;
                int headingCost = HeadingHeight + ItemGap;
                bool heading = page.Count == 0 || !sameCategory;
                if (page.Count > 0 && used + rowCost + (heading ? headingCost : 0) > budget)
                {
                    pages.Add(page);
                    page = new List<Item>();
                    used = 0;
                    heading = true;
                }
                if (heading)
                {
                    page.Add(new Item { Heading = CategoryHeading(row.Category, sameCategory) });
                    used += headingCost;
                }
                page.Add(new Item { Row = row });
                used += rowCost;
                previous = row.Category;
                hasPrevious = true;
            }
            if (page.Count > 0) pages.Add(page);
            return pages;
        }
    }
}
