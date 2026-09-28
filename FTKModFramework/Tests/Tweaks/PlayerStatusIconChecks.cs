// Game-free checks for fix.player-status-icons (Spec #269 FR-1 and FR-2): the descriptor, the taunt
// and petrified icons in and out of combat, writes only on a change, the taunt tooltip rewritten
// once, the Dazed tooltip, a missing child, and the off path. The Harmony postfix, the child lookup
// and the in-game icons are live-gated.
using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class PlayerStatusIconChecks
{
    private const string Id = "fix.player-status-icons";
    private const PlayerStatusIconChange None = PlayerStatusIconChange.None;

    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("player status icons: " + message);
    }

    internal static int Run()
    {
        Descriptor();
        CombatIcons();
        TauntTooltipOnce();
        DazedTooltip();
        MissingChild();
        OffPath();
        NonAllocating();
        return _checks;
    }

    private sealed class Store : ITweakPreferenceStore
    {
        internal readonly Dictionary<string, TweakPreference> Values = new Dictionary<string, TweakPreference>();
        public TweakPreference Read(string id)
        {
            TweakPreference value;
            return Values.TryGetValue(id, out value) ? value : TweakPreference.Default;
        }
        public void Write(string id, TweakPreference preference) { Values[id] = preference; }
    }

    private static TweakRegistry Registry(out int handle, bool initialize = true)
    {
        var r = new TweakRegistry(null);
        FrameworkTweaks.RegisterAll(r);
        if (initialize) r.Initialize(new Store());
        r.TryGetHandle(Id, out handle);
        return r;
    }

    /// <summary>A HUD whose icons and tooltips take the writes the postfix would make.</summary>
    private sealed class FakeHud
    {
        internal bool Found = true;
        internal bool StunTooltipFound = true;
        internal bool TauntShown;
        internal bool PetrifiedShown;
        internal bool TauntRewritten;
        internal bool StunShowsDazed;
        internal int Writes;

        internal PlayerStatusIconState Observe(bool dummy, bool combat, bool taunting, bool petrified,
            bool stunned = false, bool dazed = false)
        {
            return new PlayerStatusIconState
            {
                ChildrenFound = Found,
                HasCurrentDummy = dummy,
                InCombat = combat,
                Taunting = taunting,
                Petrified = petrified,
                TauntShown = TauntShown,
                PetrifiedShown = PetrifiedShown,
                TauntTooltipRewritten = TauntRewritten,
                StunTooltipFound = StunTooltipFound,
                StunnedStatus = stunned,
                DazedStatus = dazed,
                StunTooltipShowsDazed = StunShowsDazed,
            };
        }

        internal PlayerStatusIconChange Call(TweakRegistry r, int handle, bool dummy, bool combat, bool taunting,
            bool petrified, bool stunned = false, bool dazed = false)
        {
            PlayerStatusIconChange c = FrameworkTweaks.PlayerStatusIconChanges(r, handle,
                Observe(dummy, combat, taunting, petrified, stunned, dazed));
            Check(!(Has(c, PlayerStatusIconChange.ShowTaunt) && Has(c, PlayerStatusIconChange.HideTaunt)), "never shows and hides taunt at once");
            Check(!(Has(c, PlayerStatusIconChange.ShowPetrified) && Has(c, PlayerStatusIconChange.HidePetrified)), "never shows and hides petrified at once");
            Check(!(Has(c, PlayerStatusIconChange.StunTooltipDazed) && Has(c, PlayerStatusIconChange.StunTooltipStunned)), "never sets both stun tooltips");
            if (Has(c, PlayerStatusIconChange.ShowTaunt)) { TauntShown = true; Writes++; }
            if (Has(c, PlayerStatusIconChange.HideTaunt)) { TauntShown = false; Writes++; }
            if (Has(c, PlayerStatusIconChange.ShowPetrified)) { PetrifiedShown = true; Writes++; }
            if (Has(c, PlayerStatusIconChange.HidePetrified)) { PetrifiedShown = false; Writes++; }
            if (Has(c, PlayerStatusIconChange.RewriteTauntTooltip)) { TauntRewritten = true; Writes++; }
            if (Has(c, PlayerStatusIconChange.StunTooltipDazed)) { StunShowsDazed = true; Writes++; }
            if (Has(c, PlayerStatusIconChange.StunTooltipStunned)) { StunShowsDazed = false; Writes++; }
            return c;
        }
    }

    private static bool Has(PlayerStatusIconChange value, PlayerStatusIconChange flag)
    {
        return (value & flag) != 0;
    }

    private static void Descriptor()
    {
        int handle;
        TweakRegistry r = Registry(out handle, false);
        TweakDescriptor d = r.Get(handle);
        Check(handle != TweakRegistry.InvalidHandle && d == FrameworkTweaks.PlayerStatusIconsDescriptor
            && handle == FrameworkTweaks.PlayerStatusIconsFix, "the ID resolves to the descriptor and its handle");
        Check(d.Category == TweakCategory.Fix && d.Scope == TweakScope.Local, "a Local Fix");
        Check(d.DefaultOn && d.BalanceNote == null, "on by default with no balance note");
        Check(d.Evidence.Contains("uiPlayerMainHudStatus.SetStatusIcons") && d.Evidence.Contains("m_IsInCombat")
            && d.Evidence.Contains("Category.Taunt") && d.Evidence.Contains("Category.Petrify")
            && d.Evidence.Contains("STR_skillsTaunt") && d.Evidence.Contains("Category.Dazed"),
            "the evidence names the method, predicate, statuses and keys");
        Check(!r.IsOn(handle), "off before initialization");
        r.Initialize(new Store());
        Check(r.IsOn(handle), "a fresh install turns it on");
        Check(PlayerStatusIcons.TauntPath == "aliments/taunt" && PlayerStatusIcons.PetrifiedPath == "aliments/petrified",
            "the children are found by their prefab paths");
        Check(PlayerStatusIcons.TauntInfo == "STR_skillsTaunt" && PlayerStatusIcons.TauntDetail == "STR_skillsTauntInfo"
            && PlayerStatusIcons.DazedInfo == "STR_statusDazed" && PlayerStatusIcons.DazedDetail == "STR_statusDazedInfo",
            "the tooltip keys are the verified TextInfo keys");
    }

    private static void CombatIcons()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var hud = new FakeHud { TauntRewritten = true };

        Check(hud.Call(r, handle, true, true, false, false) == None, "in combat with neither status nothing changes");
        Check(hud.Call(r, handle, true, true, true, false) == PlayerStatusIconChange.ShowTaunt && hud.TauntShown && !hud.PetrifiedShown,
            "in combat, Taunting shows the taunt icon only");
        Check(hud.Call(r, handle, true, true, true, false) == None, "a repeat call writes nothing");
        Check(hud.Call(r, handle, true, true, true, true) == PlayerStatusIconChange.ShowPetrified && hud.PetrifiedShown,
            "in combat, Petrified shows the petrified icon");
        Check(hud.Call(r, handle, true, true, false, true) == PlayerStatusIconChange.HideTaunt && !hud.TauntShown && hud.PetrifiedShown,
            "a taunt that ends hides only the taunt icon");
        Check(hud.Call(r, handle, true, true, false, false) == PlayerStatusIconChange.HidePetrified && !hud.PetrifiedShown,
            "a petrify that ends hides the petrified icon");

        // Out of combat is vanilla's else-branch: no dummy, or not in combat, hides both.
        bool[] values = { false, true };
        foreach (bool dummy in values)
            foreach (bool combat in values)
            {
                if (dummy && combat) continue;
                var shown = new FakeHud { TauntRewritten = true, TauntShown = true, PetrifiedShown = true };
                Check(shown.Call(r, handle, dummy, combat, true, true)
                    == (PlayerStatusIconChange.HideTaunt | PlayerStatusIconChange.HidePetrified),
                    "outside vanilla's combat branch both icons are hidden (dummy " + dummy + ", combat " + combat + ")");
                Check(shown.Call(r, handle, dummy, combat, true, true) == None, "and once hidden they are left alone");
            }

        var both = new FakeHud { TauntRewritten = true };
        Check(both.Call(r, handle, true, true, true, true) == (PlayerStatusIconChange.ShowTaunt | PlayerStatusIconChange.ShowPetrified),
            "both statuses show both icons in one call");
        Check(both.Writes == 2, "two changes, two writes");
    }

    private static void TauntTooltipOnce()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var hud = new FakeHud();
        Check(Has(hud.Call(r, handle, false, false, false, false), PlayerStatusIconChange.RewriteTauntTooltip) && hud.TauntRewritten,
            "the first call rewrites the taunt tooltip, in or out of combat");
        for (int i = 0; i < 5; i++)
        {
            Check(!Has(hud.Call(r, handle, i % 2 == 0, true, i % 3 == 0, false), PlayerStatusIconChange.RewriteTauntTooltip),
                "later calls never rewrite it again (" + i + ")");
        }
        var inCombat = new FakeHud();
        Check(inCombat.Call(r, handle, true, true, true, false) == (PlayerStatusIconChange.ShowTaunt | PlayerStatusIconChange.RewriteTauntTooltip),
            "a first call in combat rewrites the tooltip and shows the icon together");
    }

    private static void DazedTooltip()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var hud = new FakeHud { TauntRewritten = true };
        Check(hud.Call(r, handle, true, true, false, false, true, false) == None, "Stunned alone keeps vanilla's tooltip");
        Check(hud.Call(r, handle, true, true, false, false, false, true) == PlayerStatusIconChange.StunTooltipDazed && hud.StunShowsDazed,
            "Dazed alone names Dazed");
        Check(hud.Call(r, handle, true, true, false, false, false, true) == None, "and is not rewritten again");
        Check(hud.Call(r, handle, true, true, false, false, true, true) == PlayerStatusIconChange.StunTooltipStunned && !hud.StunShowsDazed,
            "Stunned with Dazed restores the Stunned keys");
        Check(hud.Call(r, handle, true, true, false, false, false, true) == PlayerStatusIconChange.StunTooltipDazed, "Dazed alone again");
        Check(hud.Call(r, handle, true, false, false, false, false, true) == PlayerStatusIconChange.StunTooltipStunned && !hud.StunShowsDazed,
            "leaving combat restores the Stunned keys");
        Check(hud.Call(r, handle, false, true, false, false, false, true) == None, "no dummy keeps the Stunned keys");

        var noTooltip = new FakeHud { TauntRewritten = true, StunTooltipFound = false };
        Check(noTooltip.Call(r, handle, true, true, true, false, false, true) == PlayerStatusIconChange.ShowTaunt,
            "a stunned icon without a tooltip skips only the Dazed name");
    }

    private static void MissingChild()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        bool[] values = { false, true };
        foreach (bool on in values)
            foreach (bool dummy in values)
                foreach (bool combat in values)
                    foreach (bool status in values)
                        foreach (bool shown in values)
                        {
                            var hud = new FakeHud { Found = false, TauntShown = shown, PetrifiedShown = shown, StunShowsDazed = shown };
                            Check(PlayerStatusIcons.Decide(on, hud.Observe(dummy, combat, status, status, !status, status)) == None,
                                "a missing child changes nothing (on " + on + ", dummy " + dummy + ", combat " + combat
                                + ", status " + status + ", shown " + shown + ")");
                        }
        var missing = new FakeHud { Found = false };
        Check(missing.Call(r, handle, true, true, true, true, false, true) == None && missing.Writes == 0,
            "through the registry, a missing child keeps vanilla");
    }

    private static void OffPath()
    {
        int handle;
        TweakRegistry before = Registry(out handle, false);
        var fresh = new FakeHud();
        Check(fresh.Call(before, handle, true, true, true, true, false, true) == None,
            "before initialization, with nothing shown, nothing is written, not even the tooltip");

        TweakRegistry r = Registry(out handle);
        var hud = new FakeHud();
        hud.Call(r, handle, true, true, true, true, false, true);
        Check(hud.TauntShown && hud.PetrifiedShown && hud.StunShowsDazed && hud.TauntRewritten, "on: both icons and the Dazed name show");
        Check(r.Toggle(handle) && !r.IsOn(handle), "turning it off");
        Check(hud.Call(r, handle, true, true, true, true, false, true)
            == (PlayerStatusIconChange.HideTaunt | PlayerStatusIconChange.HidePetrified | PlayerStatusIconChange.StunTooltipStunned),
            "off undoes what the tweak showed, even mid-combat");
        int writes = hud.Writes;
        Check(hud.Call(r, handle, true, true, true, true, false, true) == None && hud.Writes == writes, "then leaves the HUD alone");

        Check(r.Toggle(handle) && r.IsOn(handle), "turning it back on");
        Check(hud.Call(r, handle, true, true, true, false) == PlayerStatusIconChange.ShowTaunt, "on again, the icon returns");
        r.Fault(handle, new InvalidOperationException("hud"));
        Check(!r.IsOn(handle) && hud.Call(r, handle, true, true, true, false) == PlayerStatusIconChange.HideTaunt,
            "a fault hides what the tweak left showing");
        Check(hud.Call(r, handle, true, true, true, true, false, true) == None, "and a faulted tweak shows nothing");

        var never = new FakeHud();
        Check(never.Call(r, TweakRegistry.InvalidHandle, true, true, true, true, false, true) == None && never.Writes == 0,
            "an unregistered handle with nothing shown writes nothing");
    }

    private static void NonAllocating()
    {
        int handle;
        TweakRegistry r = Registry(out handle);
        var state = new PlayerStatusIconState { ChildrenFound = true, HasCurrentDummy = true, InCombat = true, Taunting = true,
            StunTooltipFound = true, DazedStatus = true };
        PlayerStatusIconChange sink = None;
        for (int i = 0; i < 1000; i++) sink ^= FrameworkTweaks.PlayerStatusIconChanges(r, handle, state);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) sink ^= FrameworkTweaks.PlayerStatusIconChanges(r, handle, state);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "the decision allocated " + allocated + " bytes (sink " + sink + ")");
    }
}
