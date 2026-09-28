using System;

namespace FTKModFramework.Core
{
    /// <summary>What one SetStatusIcons postfix call observes for fix.player-status-icons. Every value
    /// is read from the HUD or the character as it stands after vanilla ran, so the decision needs no
    /// memory of its own: a change it made is seen on the next call, and a repeat call changes nothing.</summary>
    internal struct PlayerStatusIconState
    {
        /// <summary>Both aliments/taunt and aliments/petrified were found under the status root, with
        /// the taunt icon's uiToolTipGeneral. Without them the tweak does nothing.</summary>
        internal bool ChildrenFound { get; set; }
        /// <summary>(bool)CharacterOverworld.m_CurrentDummy.</summary>
        internal bool HasCurrentDummy { get; set; }
        /// <summary>CharacterStats.m_IsInCombat.</summary>
        internal bool InCombat { get; set; }
        /// <summary>CharacterDummy.Taunting: m_SufferingProficiencies holds Category.Taunt.</summary>
        internal bool Taunting { get; set; }
        /// <summary>CharacterDummy.Petrified: m_SufferingProficiencies holds Category.Petrify.</summary>
        internal bool Petrified { get; set; }
        /// <summary>The taunt icon's activeSelf.</summary>
        internal bool TauntShown { get; set; }
        /// <summary>The petrified icon's activeSelf.</summary>
        internal bool PetrifiedShown { get; set; }
        /// <summary>The taunt tooltip already holds the STR_skillsTaunt keys.</summary>
        internal bool TauntTooltipRewritten { get; set; }
        /// <summary>m_Stunned has a uiToolTipGeneral. Without one the Dazed tooltip is skipped.</summary>
        internal bool StunTooltipFound { get; set; }
        /// <summary>m_SufferingProficiencies holds Category.Stunned.</summary>
        internal bool StunnedStatus { get; set; }
        /// <summary>m_SufferingProficiencies holds Category.Dazed.</summary>
        internal bool DazedStatus { get; set; }
        /// <summary>The stunned tooltip currently holds the STR_statusDazed keys.</summary>
        internal bool StunTooltipShowsDazed { get; set; }
    }

    /// <summary>The writes one call makes. None means the call touches nothing.</summary>
    [Flags]
    internal enum PlayerStatusIconChange
    {
        None = 0,
        ShowTaunt = 1,
        HideTaunt = 2,
        ShowPetrified = 4,
        HidePetrified = 8,
        RewriteTauntTooltip = 16,
        StunTooltipDazed = 32,
        StunTooltipStunned = 64,
    }

    /// <summary>fix.player-status-icons (Spec #269). The player HUD's aliments grid ships taunt and
    /// petrified icons that uiPlayerMainHudStatus has no field for, so they never show. The decision
    /// mirrors SetStatusIcons' branches: an icon shows only under (bool)m_CurrentDummy &amp;&amp;
    /// m_IsInCombat and its status, and is hidden otherwise. It asks for a write only where the
    /// observed state differs, so SetActive runs only on a change.
    ///
    /// Off, faulted or uninitialized still hides an icon this tweak left shown and restores the
    /// Stunned tooltip, because vanilla never touches either icon and would leave them as they are.
    /// With nothing of the tweak's showing, off changes nothing.</summary>
    internal static class PlayerStatusIcons
    {
        internal const string TauntPath = "aliments/taunt";
        internal const string PetrifiedPath = "aliments/petrified";

        /// <summary>The taunt icon ships with STR_statusTaunt and STR_statusTauntInfo, which are in no
        /// text table. These TextInfo keys name the taunt skill ("Enemy Taunt") in every language.</summary>
        internal const string TauntInfo = "STR_skillsTaunt";
        internal const string TauntDetail = "STR_skillsTauntInfo";

        /// <summary>TextInfo keys for Dazed. CharacterDummy.Stunned is true for Stunned or Dazed, and
        /// no Dazed sprite exists, so Dazed shows as the stunned icon with its own name.</summary>
        internal const string DazedInfo = "STR_statusDazed";
        internal const string DazedDetail = "STR_statusDazedInfo";

        internal static PlayerStatusIconChange Decide(bool on, PlayerStatusIconState state)
        {
            if (!state.ChildrenFound) return PlayerStatusIconChange.None;
            bool combat = on && state.HasCurrentDummy && state.InCombat;
            PlayerStatusIconChange change = PlayerStatusIconChange.None;

            bool taunt = combat && state.Taunting;
            if (taunt != state.TauntShown)
                change |= taunt ? PlayerStatusIconChange.ShowTaunt : PlayerStatusIconChange.HideTaunt;

            bool petrified = combat && state.Petrified;
            if (petrified != state.PetrifiedShown)
                change |= petrified ? PlayerStatusIconChange.ShowPetrified : PlayerStatusIconChange.HidePetrified;

            if (on && !state.TauntTooltipRewritten) change |= PlayerStatusIconChange.RewriteTauntTooltip;

            if (state.StunTooltipFound)
            {
                // Only Dazed alone is renamed; Stunned, with or without Dazed, keeps vanilla's keys.
                bool dazed = combat && state.DazedStatus && !state.StunnedStatus;
                if (dazed != state.StunTooltipShowsDazed)
                    change |= dazed ? PlayerStatusIconChange.StunTooltipDazed : PlayerStatusIconChange.StunTooltipStunned;
            }
            return change;
        }
    }
}
