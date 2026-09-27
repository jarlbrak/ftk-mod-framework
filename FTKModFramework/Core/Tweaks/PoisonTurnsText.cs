namespace FTKModFramework.Core
{
    /// <summary>The count and tooltip text for information.poison-turns, kept free of game types so
    /// Tests/Tweaks can cover it. CharacterStats.EndTurnActionSequence adds 1 to the private
    /// m_PoisonTimeCounter at each end turn while the character is alive and not waiting to respawn,
    /// and when it reaches PoisonTimeRounds (3) drops one poison level and resets it to 0. Gaining
    /// poison (UpdatePoison) never resets it.</summary>
    internal sealed class PoisonTurnsText
    {
        /// <summary>CharacterStats.PoisonTimeRounds, a private constant property in the game.</summary>
        internal const int RoundsPerLevel = 3;

        // One-entry cache. uiToolTipManager.Update calls GetMoreToolTip every frame while a tooltip
        // is visible, so the same inputs must return the same string instead of building a new one.
        private string _vanilla;
        private int _remaining;
        private string _result;

        /// <summary>End turns until the poison is gone: (3 - counter) for the current level plus 3 for
        /// each level stacked above it. A level added mid-countdown joins the queue without resetting
        /// the counter. Returns 0 when not poisoned.</summary>
        /// <param name="level">CharacterStats.m_PoisonLvl.</param>
        /// <param name="counter">CharacterStats.m_PoisonTimeCounter on the owning client.</param>
        internal static int Remaining(int level, int counter)
        {
            if (level <= 0) return 0;
            // Between end turns the counter is 0 to 2. It holds 3 only inside EndTurnActionSequence,
            // between its increment and the level drop, which the clamp reads as "this level is done".
            if (counter < 0) counter = 0;
            else if (counter > RoundsPerLevel) counter = RoundsPerLevel;
            return RoundsPerLevel - counter + RoundsPerLevel * (level - 1);
        }

        internal static string Line(int remaining)
        {
            return remaining == 1 ? "1 end turn left" : remaining.ToString() + " end turns left";
        }

        /// <summary>The vanilla detail text with the count on a new line, or the vanilla text alone
        /// when there is nothing to count or no vanilla text to extend.</summary>
        internal string Append(string vanillaText, int remaining)
        {
            if (remaining <= 0 || string.IsNullOrEmpty(vanillaText)) return vanillaText;
            if (_result != null && remaining == _remaining && string.Equals(vanillaText, _vanilla)) return _result;
            _result = vanillaText + "\n" + Line(remaining);
            _vanilla = vanillaText;
            _remaining = remaining;
            return _result;
        }
    }
}
