namespace FTKModFramework.Core
{
    /// <summary>The XP text for information.xp-in-level, kept free of game types so Tests/Tweaks can
    /// cover it. The level window mirrors CharacterStats.GetXpPercent, which drives the HUD bar: the
    /// window starts at m_LevelXpValues[level - 1] (0 at level 0) and ends at m_LevelXpValues[level].</summary>
    internal static class XpInLevelText
    {
        /// <summary>"{xp in level} / {level span} ({total})" below the max level, the total alone at
        /// it, or null when the window is not a positive span so the caller keeps vanilla's text.</summary>
        /// <param name="level">CharacterStats.m_PlayerLevel, the number the HUD shows as the level.</param>
        /// <param name="maxLevel">GameFlow.m_MaxCharacterLevels.</param>
        /// <param name="totalXp">CharacterStats.m_PlayerXP, which is cumulative.</param>
        /// <param name="levelStartXp">The window start. Unused at the max level.</param>
        /// <param name="levelEndXp">The window end. Unused at the max level.</param>
        internal static string Format(int level, int maxLevel, int totalXp, int levelStartXp, int levelEndXp)
        {
            // Vanilla treats any level at or above m_MaxCharacterLevels as max (GetXpDisplayString,
            // uiPlayerMainHud.SetXpDisplay). UpdateXP keeps XP below m_LevelXpValues[m_MaxCharacterLevels],
            // so vanilla's "N / N" there is never the real total.
            if (level >= maxLevel) return totalXp.ToString();
            int span = levelEndXp - levelStartXp;
            if (span <= 0) return null;
            // The level never drops, but a town respawn penalty or LoseAllXP can take XP below the
            // window start, and the text can refresh before CharacterStats.Update applies a level-up.
            // uiTransitionBar writes GetXpPercent into a UnityEngine.UI.Slider, which clamps to its
            // range, so the bar shows empty or full in those cases and the text clamps to match.
            int inLevel = totalXp - levelStartXp;
            if (inLevel < 0) inLevel = 0;
            else if (inLevel > span) inLevel = span;
            return inLevel.ToString() + " / " + span.ToString() + " (" + totalXp.ToString() + ")";
        }
    }
}
