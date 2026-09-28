using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Stale combat icon fixes (fix.stale-wet-icon, fix.stale-group-shield-icon). Vanilla
    /// SetStatusIcons writes m_wet and m_GroupShield only in its in-combat branch, and its else-branch
    /// hides every other combat-only icon, so either icon shown when combat ends stays until the next
    /// combat. The postfix evaluates the same predicate and hides an icon only while it is active, so
    /// it does nothing on most calls and allocates nothing. Each icon has its own handle and its own
    /// try block, so a fault disables only that tweak. It reads local HUD state and changes only this
    /// player's display.</summary>
    [HarmonyPatch(typeof(uiPlayerMainHudStatus), "SetStatusIcons")]
    internal static class StaleCombatIconPatch
    {
        private static void Postfix(uiPlayerMainHudStatus __instance, CharacterOverworld _cow)
        {
            int wet = FrameworkTweaks.StaleWetIcon;
            if (Tweaks.IsOn(wet))
            {
                try
                {
                    if (__instance != null) HideIfStale(__instance.m_wet, _cow, wet);
                }
                catch (Exception e)
                {
                    Tweaks.Fault(wet, e);
                }
            }

            int shield = FrameworkTweaks.StaleGroupShieldIcon;
            if (Tweaks.IsOn(shield))
            {
                try
                {
                    if (__instance != null) HideIfStale(__instance.m_GroupShield, _cow, shield);
                }
                catch (Exception e)
                {
                    Tweaks.Fault(shield, e);
                }
            }
        }

        private static void HideIfStale(Component image, CharacterOverworld cow, int handle)
        {
            if (image == null || cow == null || cow.m_CharacterStats == null) return;
            GameObject icon = image.gameObject;
            if (FrameworkTweaks.HideStaleCombatIcon(Tweaks.Registry, handle,
                    (bool)cow.m_CurrentDummy, cow.m_CharacterStats.m_IsInCombat, icon.activeSelf))
                icon.SetActive(false);
        }
    }
}
