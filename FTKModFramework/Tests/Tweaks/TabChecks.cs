// Game-free checks for the Tweaks tab's text and paging (ModsPanelTweaks). Rendering, layout and
// controller focus in the live panel are not covered here.
using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using FTKModFramework.Core.UI;

internal static class TabChecks
{
    private static int _checks;

    private sealed class FailingStore : ITweakPreferenceStore
    {
        public TweakPreference Read(string id) { return TweakPreference.Default; }
        public void Write(string id, TweakPreference preference) { throw new InvalidOperationException("disk full"); }
    }

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("tab: " + message);
    }

    internal static int Run()
    {
        Text();
        Status();
        PilotPage();
        Paging();
        RowStates();
        OversizedRows();
        return _checks;
    }

    private static void Text()
    {
        Check(ModsPanelTweaks.Caption("Skip intro", false, TweakPreference.Default) == "Skip intro: Off (default)", "a row following its default says so");
        Check(ModsPanelTweaks.Caption("Skip intro", true, TweakPreference.On) == "Skip intro: On", "an explicit On has no default marker");
        Check(ModsPanelTweaks.Caption("Fix it", false, TweakPreference.Off) == "Fix it: Off", "an explicit Off has no default marker");
        Check(ModsPanelTweaks.Caption("Fix it", true, TweakPreference.Default) == "Fix it: On (default)", "a default-on row reads On (default)");
        Check(ModsPanelTweaks.ScopeLabel(TweakScope.Local) == "Only you", "the Local scope label");
        Check(ModsPanelTweaks.ScopeLabel(TweakScope.Session) == "Shared rules: solo and local play; online co-op off until supported", "the Session scope label");
        Check(ModsPanelTweaks.CategoryHeading(TweakCategory.Fix, false) == "Fixes" && ModsPanelTweaks.CategoryHeading(TweakCategory.Information, false) == "Information"
            && ModsPanelTweaks.CategoryHeading(TweakCategory.Convenience, true) == "Convenience (continued)", "category headings");
        Check(ModsPanelTweaks.FaultLabel(TweakScope.Local) != ModsPanelTweaks.FaultLabel(TweakScope.Session)
            && ModsPanelTweaks.FaultLabel(TweakScope.Session).Contains("next run"), "a Session fault names the next run, a Local one does not");

        string[] fixedText = { ModsPanelTweaks.TabTitle, ModsPanelTweaks.Intro, ModsPanelTweaks.ResetCaption, ModsPanelTweaks.ResetDone,
            ModsPanelTweaks.SaveFailed, ModsPanelTweaks.Unavailable, ModsPanelTweaks.Empty, ModsPanelTweaks.NextRun,
            ModsPanelTweaks.ScopeLabel(TweakScope.Session), ModsPanelTweaks.FaultLabel(TweakScope.Local), ModsPanelTweaks.FaultLabel(TweakScope.Session) };
        foreach (string text in fixedText)
            Check(text.IndexOf('\u2014') < 0 && !text.StartsWith("STR_", StringComparison.Ordinal), "panel text is plain English without em dashes: " + text);
    }

    private static void Status()
    {
        TweakRegistry uninitialized = new TweakRegistry(null);
        uninitialized.Register(Descriptor("fix.one", TweakCategory.Fix, TweakScope.Local));
        Check(ModsPanelTweaks.Status(uninitialized.IsInitialized, uninitialized.Count) == ModsPanelTweaks.Unavailable,
            "a registry that failed to initialize shows the unavailable error");
        Check(ModsPanelTweaks.Pages(uninitialized, ModsPanelTweaks.PageBudget).Count == 0, "an uninitialized registry pages no rows");

        TweakRegistry empty = new TweakRegistry(null);
        Check(empty.Initialize(new MemoryStore()), "an empty registry initializes");
        Check(ModsPanelTweaks.Status(empty.IsInitialized, empty.Count) == ModsPanelTweaks.Empty, "an empty registry shows the empty message");
        Check(ModsPanelTweaks.Pages(empty, ModsPanelTweaks.PageBudget).Count == 0, "an empty registry pages no rows");
        Check(ModsPanelTweaks.Status(true, 1) == null, "a populated registry renders rows");
    }

    // The shipped rows are found by ID, not position, so adding a framework tweak does not move
    // another tweak's expectations. Exact positions and page counts are covered by Paging.
    private static void PilotPage()
    {
        TweakRegistry registry = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(registry);
        registry.Initialize(new MemoryStore());
        List<List<ModsPanelTweaks.Item>> pages = ModsPanelTweaks.Pages(registry, ModsPanelTweaks.PageBudget);

        Dictionary<int, string> headings = new Dictionary<int, string>();
        Dictionary<int, ModsPanelTweaks.Row> rows = new Dictionary<int, ModsPanelTweaks.Row>();
        List<int> seen = new List<int>();
        foreach (List<ModsPanelTweaks.Item> page in pages)
        {
            string heading = null;
            foreach (ModsPanelTweaks.Item item in page)
            {
                if (item.Heading != null) { heading = item.Heading; continue; }
                seen.Add(item.Row.Handle);
                rows[item.Row.Handle] = item.Row;
                headings[item.Row.Handle] = heading;
            }
        }
        Check(seen.Count == registry.Count && rows.Count == registry.Count, "every shipped tweak appears exactly once");
        for (int i = 0; i < seen.Count; i++) Check(seen[i] == registry.DisplayOrder[i], "the shipped rows keep the registry's display order");

        ShippedRow(registry, rows, headings, "fix.quest-dungeon-name", "Dungeon names in quest text: On (default)",
            "a fresh install shows the quest dungeon name fix under Fixes, on by default");
        ShippedRow(registry, rows, headings, "fix.stale-wet-icon", "Clear the Wet icon after combat: On (default)",
            "a fresh install shows the Wet icon fix under Fixes, on by default");
        ShippedRow(registry, rows, headings, "information.xp-in-level", "XP within the level: Off (default)",
            "a fresh install shows XP within the level under Information, off by default");
        ShippedRow(registry, rows, headings, "information.poison-turns", "Poison turns left: Off (default)",
            "a fresh install shows Poison turns left under Information, off by default");
        ShippedRow(registry, rows, headings, "information.sell-price", "Sell price in item details: Off (default)",
            "a fresh install shows Sell price in item details under Information, off by default");
        ShippedRow(registry, rows, headings, "convenience.skip-intro", "Skip intro: Off (default)",
            "a fresh install shows Skip intro under Convenience, off by default");
    }

    private static void ShippedRow(TweakRegistry registry, Dictionary<int, ModsPanelTweaks.Row> rows,
        Dictionary<int, string> headings, string id, string caption, string message)
    {
        int handle;
        Check(registry.TryGetHandle(id, out handle) && rows.ContainsKey(handle), "the shipped row is on the tab: " + id);
        TweakDescriptor d = registry.Get(handle);
        ModsPanelTweaks.Row row = rows[handle];
        string heading = headings[handle];
        Check(row.Caption == caption && (heading == ModsPanelTweaks.CategoryHeading(d.Category, false)
            || heading == ModsPanelTweaks.CategoryHeading(d.Category, true)), message);
        Check(row.Lines[1].Text == ModsPanelTweaks.ScopeLabel(d.Scope) && d.Scope == TweakScope.Local, id + " is labelled Local");
    }

    private static TweakRegistry Populated(MemoryStore store, int perCategory)
    {
        TweakRegistry registry = new TweakRegistry(null);
        TweakCategory[] categories = { TweakCategory.Convenience, TweakCategory.Fix, TweakCategory.Information };
        for (int i = 0; i < perCategory; i++)
            foreach (TweakCategory category in categories)
            {
                TweakScope scope = i % 3 == 0 ? TweakScope.Session : TweakScope.Local;
                string id = category.ToString().ToLowerInvariant() + ".row-" + i;
                string note = category == TweakCategory.Fix && i % 4 == 1 ? "Makes early fights a little easier." : null;
                Check(registry.Register(new TweakDescriptor(id, category, scope, "Row " + i.ToString("00"), "Summary for " + id, "Evidence",
                    note, note == null ? (bool?)null : false)) >= 0, "test descriptor registers: " + id);
            }
        Check(registry.Initialize(store), "the populated registry initializes");
        return registry;
    }

    private static int Cost(List<ModsPanelTweaks.Item> page)
    {
        int used = 0;
        foreach (ModsPanelTweaks.Item item in page)
            used += (item.Heading != null ? ModsPanelTweaks.HeadingHeight : item.Row.Height) + ModsPanelTweaks.ItemGap;
        return used;
    }

    private static void Paging()
    {
        foreach (int perCategory in new[] { 7, 10 })
        {
            TweakRegistry registry = Populated(new MemoryStore(), perCategory);
            Check(registry.Count >= 20, "paging runs with 20 or more descriptors (" + registry.Count + ")");
            List<List<ModsPanelTweaks.Item>> pages = ModsPanelTweaks.Pages(registry, ModsPanelTweaks.PageBudget);
            Check(pages.Count > 1, "20 or more rows span several pages (" + pages.Count + ")");

            List<int> seen = new List<int>();
            bool hasPrevious = false;
            TweakCategory previous = TweakCategory.Fix;
            foreach (List<ModsPanelTweaks.Item> page in pages)
            {
                Check(page.Count >= 2 && page[0].Heading != null, "every page opens with a heading and holds a row");
                Check(Cost(page) <= ModsPanelTweaks.PageBudget, "every page fits the budget (" + Cost(page) + ")");
                string expectedHeading = null;
                for (int i = 0; i < page.Count; i++)
                {
                    ModsPanelTweaks.Item item = page[i];
                    Check((item.Heading == null) != (item.Row == null), "an item is a heading or a row");
                    if (item.Heading != null)
                    {
                        Check(i + 1 < page.Count && page[i + 1].Row != null, "a heading is followed by a row");
                        ModsPanelTweaks.Row next = page[i + 1].Row;
                        bool continued = hasPrevious && previous == next.Category;
                        Check(i == 0 || !continued, "a heading inside a page opens a new category");
                        expectedHeading = ModsPanelTweaks.CategoryHeading(next.Category, continued);
                        Check(item.Heading == expectedHeading, "the heading names its category: " + item.Heading);
                        continue;
                    }
                    Check(!hasPrevious || previous == item.Row.Category || (i > 0 && page[i - 1].Heading != null),
                        "a category change always has a heading");
                    seen.Add(item.Row.Handle);
                    previous = item.Row.Category;
                    hasPrevious = true;
                }
            }
            Check(seen.Count == registry.Count, "every row appears exactly once across pages");
            for (int i = 0; i < seen.Count; i++) Check(seen[i] == registry.DisplayOrder[i], "pages keep the registry's display order");
            bool spilled = false;
            foreach (List<ModsPanelTweaks.Item> page in pages) spilled |= page[0].Heading.EndsWith(" (continued)", StringComparison.Ordinal);
            Check(spilled, "a category that spills onto the next page is marked continued");
        }
    }

    private static ModsPanelTweaks.Row Find(TweakRegistry registry, string id)
    {
        int handle;
        Check(registry.TryGetHandle(id, out handle), "the row exists: " + id);
        return ModsPanelTweaks.BuildRow(registry, handle);
    }

    private static void RowStates()
    {
        MemoryStore store = new MemoryStore();
        TweakRegistry registry = Populated(store, 7);
        int local, session;
        registry.TryGetHandle("convenience.row-1", out local);
        registry.TryGetHandle("convenience.row-0", out session);

        ModsPanelTweaks.Row row = Find(registry, "convenience.row-1");
        Check(row.Caption == "Row 01: Off (default)" && row.Lines.Count == 2 && row.Lines[0].Text == "Summary for convenience.row-1"
            && row.Lines[1].Text == "Only you", "a Local row shows caption, summary and scope");
        Check(row.Height == ModsPanelTweaks.ButtonHeight + 2 * ModsPanelTweaks.LineGap + ModsPanelTweaks.TallLine + ModsPanelTweaks.ShortLine,
            "a row's height is its button plus its lines");

        ModsPanelTweaks.Row shared = Find(registry, "convenience.row-0");
        Check(shared.Lines[1].Text == ModsPanelTweaks.ScopeLabel(TweakScope.Session) + ". " + ModsPanelTweaks.NextRun,
            "a Session row says changes apply to the next run");

        ModsPanelTweaks.Row balanced = Find(registry, "fix.row-1");
        Check(balanced.Caption == "Row 01: Off (default)" && balanced.Lines.Count == 3
            && balanced.Lines[2].Text == "Balance: Makes early fights a little easier.", "a balance note is shown");
        Check(Find(registry, "fix.row-2").Caption == "Row 02: On (default)", "a Fix without a note follows its on default");

        // Toggle saves through the store and the rebuilt row reflects it.
        int writes = store.Writes;
        Check(registry.Toggle(local) && store.Writes == writes + 1 && store.Values["convenience.row-1"] == TweakPreference.On,
            "toggling saves the choice immediately");
        Check(Find(registry, "convenience.row-1").Caption == "Row 01: On", "the refreshed row shows the explicit choice");
        Check(registry.Toggle(local) && Find(registry, "convenience.row-1").Caption == "Row 01: Off (default)", "toggling back follows the default again");

        // Reset returns every row to its default.
        registry.Toggle(local);
        registry.Toggle(session);
        int fix2;
        registry.TryGetHandle("fix.row-2", out fix2);
        registry.Toggle(fix2);
        Check(Find(registry, "fix.row-2").Caption == "Row 02: Off", "a default-on row toggles to an explicit Off");
        Check(registry.ResetAll(), "reset succeeds");
        foreach (int handle in registry.DisplayOrder)
        {
            ModsPanelTweaks.Row reset = ModsPanelTweaks.BuildRow(registry, handle);
            Check(registry.Preference(handle) == TweakPreference.Default && reset.Caption.EndsWith(" (default)"), "reset returns every row to its default");
        }

        // Faults are marked by scope and do not change the caption, which reports the player's choice.
        registry.Toggle(local);
        registry.Fault(local, new InvalidOperationException("boom"));
        registry.Fault(session, new InvalidOperationException("boom"));
        ModsPanelTweaks.Row faulted = Find(registry, "convenience.row-1");
        ModsPanelTweaks.Line marker = faulted.Lines[faulted.Lines.Count - 1];
        Check(faulted.Caption == "Row 01: On" && marker.Warning && marker.Text == ModsPanelTweaks.FaultLabel(TweakScope.Local),
            "a faulted Local row keeps the choice and shows the fault");
        ModsPanelTweaks.Row faultedShared = Find(registry, "convenience.row-0");
        Check(faultedShared.Lines[faultedShared.Lines.Count - 1].Text == ModsPanelTweaks.FaultLabel(TweakScope.Session), "a faulted Session row shows the Session fault");
        Check(!Find(registry, "convenience.row-2").Lines[1].Warning, "a healthy row shows no fault");

        // A store that cannot save reports failure, which the panel turns into a message.
        TweakRegistry failing = new TweakRegistry(null);
        int handleFailing = failing.Register(Descriptor("fix.failing", TweakCategory.Fix, TweakScope.Local));
        failing.Initialize(new FailingStore());
        Check(!failing.Toggle(handleFailing) && !failing.ResetAll(), "a failed save is reported to the tab");
        Check(ModsPanelTweaks.BuildRow(failing, handleFailing).Caption == "Title fix.failing: On (default)", "a failed save leaves the row unchanged");
    }

    private static void OversizedRows()
    {
        List<ModsPanelTweaks.Row> rows = new List<ModsPanelTweaks.Row>();
        for (int i = 0; i < 5; i++)
            rows.Add(new ModsPanelTweaks.Row { Handle = i, Category = i < 3 ? TweakCategory.Fix : TweakCategory.Convenience, Caption = "Row " + i });
        List<List<ModsPanelTweaks.Item>> pages = ModsPanelTweaks.Paginate(rows, 10);
        Check(pages.Count == 5, "a row taller than the budget still gets a page");
        for (int i = 0; i < 5; i++)
            Check(pages[i].Count == 2 && pages[i][1].Row.Handle == i, "no row is dropped when rows exceed the budget");
        Check(pages[1][0].Heading == "Fixes (continued)" && pages[3][0].Heading == "Convenience", "continued headings follow the category");
        Check(ModsPanelTweaks.Paginate(new List<ModsPanelTweaks.Row>(), ModsPanelTweaks.PageBudget).Count == 0, "no rows makes no pages");
    }

    private static TweakDescriptor Descriptor(string id, TweakCategory category, TweakScope scope)
    {
        return new TweakDescriptor(id, category, scope, "Title " + id, "Summary", "Evidence");
    }
}
