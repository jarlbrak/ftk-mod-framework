using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Name the achievements House Rules disable tweak (information.house-rules-achievements).
    /// GameDifficulty.GetDynamicDifficultyText(Rules2) is the rules summary on the game setup screen
    /// (GameConfig.RefreshDiff, run on each difficulty or House Rules change), the waiting room and
    /// the resume browser. Each caller passes the rules of the run it describes to that run's own
    /// GameDifficulty, so the postfix compares the two with the same Rules2.IsEasier that
    /// GameFlow.IsDifficultyEasier applies in game, rather than reading GameFlow's current run. It
    /// only extends the returned string. With the tweak off, faulted or uninitialized it returns
    /// before reading anything.</summary>
    [HarmonyPatch(typeof(GameDifficulty), "GetDynamicDifficultyText", new[] { typeof(GameFlow.Rules2) })]
    internal static class HouseRulesAchievementsPatch
    {
        private static void Postfix(GameDifficulty __instance, GameFlow.Rules2 _rules, ref string __result)
        {
            int handle = FrameworkTweaks.HouseRulesAchievements;
            if (!Tweaks.IsOn(handle)) return;
            string text;
            try
            {
                GameFlow.Rules2 baseline = __instance.m_CustomizableRules;
                bool easier = _rules != null && baseline != null && GameFlow.Rules2.IsEasier(_rules, baseline);
                text = FrameworkTweaks.HouseRulesDifficultyText(Tweaks.Registry, handle, __result, easier);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            __result = text;
        }
    }
}
