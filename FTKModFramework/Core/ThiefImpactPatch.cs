using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    internal struct ThiefImpactState
    {
        internal bool Captured;
        internal int PreviousHealth;
    }

    [HarmonyPatch(typeof(CharacterDummy), "RespondToHit")]
    internal static class ThiefImpactPatch
    {
        private static void Prefix(CharacterDummy __instance, out ThiefImpactState __state)
        {
            __state = new ThiefImpactState();
            // Pending precision feedback targets enemies. Native self and friendly
            // responses need neither a health read nor damage-dependent feedback.
            if (!(__instance is EnemyDummy)) return;
            __state.Captured = true;
            __state.PreviousHealth = __instance == null ? 0 : __instance.GetCurrentHealth();
        }

        private static void Postfix(CharacterDummy __instance, ThiefImpactState __state)
        {
            if (!__state.Captured) return;
            try { ThiefRuntime.OnImpact(__instance, __state.PreviousHealth); }
            catch (Exception e) { Plugin.Log.LogError("[thief] artifact impact failed: " + e); }
        }
    }
}
