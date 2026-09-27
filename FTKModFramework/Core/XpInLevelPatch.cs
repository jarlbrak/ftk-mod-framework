using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>XP within the level tweak (information.xp-in-level). GetXpDisplayString feeds only the
    /// HUD XP text (uiPlayerMainHud.SetXpDisplay, run when the HUD is flagged for update rather than
    /// every frame) and the stats panel (uiPlayerStats.UpdateDisplay, run when it opens). A postfix
    /// lets vanilla build its string first and only replaces the result, so no game state changes.
    /// With the tweak off, faulted or uninitialized it returns before reading or allocating anything.</summary>
    [HarmonyPatch(typeof(CharacterStats), "GetXpDisplayString")]
    internal static class XpInLevelPatch
    {
        private static void Postfix(CharacterStats __instance, ref string __result)
        {
            int handle = FrameworkTweaks.XpInLevel;
            if (!Tweaks.IsOn(handle)) return;
            string text;
            try
            {
                // Read through m_CharacterOverworld as vanilla does, so the numbers describe the same
                // character as the string being replaced. The window indexing mirrors GetXpPercent.
                CharacterStats stats = __instance.m_CharacterOverworld.m_CharacterStats;
                GameFlow flow = GameFlow.Instance;
                int level = stats.m_PlayerLevel;
                int maxLevel = flow.m_MaxCharacterLevels;
                int start = 0, end = 0;
                if (level < maxLevel)
                {
                    int[] thresholds = flow.m_LevelXpValues;
                    end = thresholds[level];
                    if (level > 0) start = thresholds[level - 1];
                }
                text = FrameworkTweaks.XpInLevelDisplay(Tweaks.Registry, handle, __result,
                    level, maxLevel, stats.m_PlayerXP, start, end);
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
