namespace FTKModFramework.Core
{
    /// <summary>The House Rules line for information.house-rules-achievements, kept free of game
    /// types so Tests/Tweaks can cover it. When GameFlow.IsDifficultyEasier() holds,
    /// sPlayerAchievement_trigger reverts every achievement whose main.db sAchievement row has
    /// HouseRulesEasyEnabled = 0, and sPlayerStatistic_trigger restores every such sStatistic value.
    /// In this build those are ACH_STORY_KILL_VEXOR_EASY, _NORMAL and _HARD (3 of 101) and the 15
    /// STAT_GAMEWIN_* statistics (of 254).</summary>
    internal static class HouseRulesText
    {
        internal const string Line = "Easier House Rules: the three Defeat Vexor achievements and win statistics won't be recorded.";

        /// <summary>The dynamic difficulty text with the line after it, joined with "\n" as
        /// GameDifficulty.GetDynamicDifficultyText joins its own lines. Rules that are not easier
        /// keep the text as it is.</summary>
        internal static string Append(string vanillaText, bool easier)
        {
            if (!easier) return vanillaText;
            if (string.IsNullOrEmpty(vanillaText)) return Line;
            return vanillaText + "\n" + Line;
        }
    }
}
