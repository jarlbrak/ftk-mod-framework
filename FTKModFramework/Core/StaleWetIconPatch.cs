using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Stale Wet icon fix (fix.stale-wet-icon). Vanilla SetStatusIcons writes m_wet only in
    /// its in-combat branch and its else-branch hides every other combat-only icon, so a Wet icon
    /// shown when combat ends stays until the next combat. The postfix evaluates the same predicate
    /// and hides the icon only while it is active, so it does nothing on most calls and allocates
    /// nothing. It reads local HUD state and changes only this player's display.</summary>
    [HarmonyPatch(typeof(uiPlayerMainHudStatus), "SetStatusIcons")]
    internal static class StaleWetIconPatch
    {
        private static void Postfix(uiPlayerMainHudStatus __instance, CharacterOverworld _cow)
        {
            int handle = FrameworkTweaks.StaleWetIcon;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                if (__instance == null || __instance.m_wet == null || _cow == null || _cow.m_CharacterStats == null) return;
                GameObject icon = __instance.m_wet.gameObject;
                if (FrameworkTweaks.HideStaleWetIcon(Tweaks.Registry, handle,
                        (bool)_cow.m_CurrentDummy, _cow.m_CharacterStats.m_IsInCombat, icon.activeSelf))
                    icon.SetActive(false);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }
}
