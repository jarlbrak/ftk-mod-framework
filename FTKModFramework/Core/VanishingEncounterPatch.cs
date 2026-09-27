using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Mark encounters that vanish tweak (information.vanishing-encounters). The overworld
    /// hover card (uiHexStatusOverworld.DisplayPoiStatus) reads MiniEncounter.GetPOIProfile once per
    /// hover, guarded by m_UpdatePanel, so this is not a per-frame path. GetPOIProfile returns a new
    /// PoiProfile on every call, so the postfix extends that profile's effect line and never stacks.
    /// GetPOIDisplayValue, which also feeds quest text, messages, the buy menu and the online HUD,
    /// is left alone. With the tweak off, faulted or uninitialized it returns before reading or
    /// allocating anything.</summary>
    [HarmonyPatch(typeof(MiniEncounter), "GetPOIProfile")]
    internal static class VanishingEncounterPatch
    {
        private static void Postfix(MiniEncounter __instance, MiniHexInfo.PoiProfile __result)
        {
            int handle = FrameworkTweaks.VanishingEncounters;
            if (!Tweaks.IsOn(handle)) return;
            if (__result == null) return;
            string effect;
            try
            {
                // m_Known first, so the database entry of an unknown encounter is never consulted.
                bool known = __instance.m_Known;
                bool destroyOnLeave = known && __instance.GetDBEntry().m_DestroyOnLeave;
                effect = FrameworkTweaks.VanishingEncounterEffect(Tweaks.Registry, handle, __result.m_Effect,
                    known, destroyOnLeave);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            __result.m_Effect = effect;
        }
    }
}
