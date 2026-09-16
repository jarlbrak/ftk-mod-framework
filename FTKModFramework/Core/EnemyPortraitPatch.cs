using System;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(OffscreenCamera), "Snapshot", new Type[] { typeof(FTK_enemyCombat), typeof(Texture2D), typeof(string), typeof(string), typeof(CharacterEventListener.DisplayLayer), typeof(int), typeof(bool) })]
    internal static class EnemyRowPortraitPatch
    {
        private static void Prefix(OffscreenCamera __instance, FTK_enemyCombat _ec, string _camName, out EnemyPortraitRegistry.Scope __state)
        {
            __state = EnemyPortraitRegistry.Begin(__instance);
            try { if (_camName == "PortraitCam") EnemyPortraitRegistry.Set(__instance, EnemyPortraitRegistry.ForRow(_ec), true); }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy-portrait] row identity unavailable: " + ex.Message); }
        }
        private static Exception Finalizer(Exception __exception, EnemyPortraitRegistry.Scope __state)
        { EnemyPortraitRegistry.End(__state); return __exception; }
    }
    [HarmonyPatch(typeof(OffscreenCamera), "Snapshot", new Type[] { typeof(CharacterEventListener), typeof(Texture2D), typeof(string), typeof(string), typeof(CharacterEventListener.DisplayLayer), typeof(int), typeof(bool), typeof(bool) })]
    internal static class EnemyAvatarPortraitPatch
    {
        private static void Prefix(OffscreenCamera __instance, CharacterEventListener _avatar, string _camName, out EnemyPortraitRegistry.Scope __state)
        {
            __state = EnemyPortraitRegistry.Begin(__instance);
            if (_camName != "PortraitCam") return;
            try
            {
            EnemyPortraitRegistry.Selection inherited = __state.previous;
            // The native row overload calls this overload with exactly its row's shared source CEL.
            EnemyPortraitRegistry.Selection selection = __state.forwardFromRow && inherited != null && _avatar == inherited.source
                ? EnemyPortraitRegistry.Forwarded(inherited) : EnemyPortraitRegistry.ForAvatar(_avatar);
            EnemyPortraitRegistry.Set(__instance, selection);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy-portrait] avatar identity unavailable: " + ex.Message); }
        }
        private static Exception Finalizer(Exception __exception, EnemyPortraitRegistry.Scope __state)
        { EnemyPortraitRegistry.End(__state); return __exception; }
    }
    // Native InstantiateTarget has finished its portrait pose; positioning/rendering have not started.
    [HarmonyPatch(typeof(OffscreenCamera), "InstantiateTarget", new Type[] { typeof(CharacterEventListener), typeof(CharacterEventListener.DisplayLayer), typeof(int), typeof(bool) })]
    internal static class EnemyPortraitMeshPatch
    {
        private static void Postfix(OffscreenCamera __instance, CharacterEventListener _avatar)
        {
            try { EnemyPortraitRegistry.ApplyMeshesOnClone(__instance, _avatar); }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy-portrait] explicit preview meshes unavailable: " + ex.Message); }
        }
    }
    [HarmonyPatch(typeof(OffscreenCamera), "SetTargetPosition", new Type[] { typeof(string), typeof(string) })]
    internal static class EnemyPortraitPositionPatch
    {
        private static void Prefix(OffscreenCamera __instance, ref string _camName, ref string _camName2)
        {
            string first = _camName, fallback = _camName2;
            try { EnemyPortraitRegistry.SelectOnClone(__instance, ref _camName, ref _camName2); }
            catch (Exception ex) { _camName = first; _camName2 = fallback; Plugin.Log.LogWarning("[enemy-portrait] selection failed; native positioning retained: " + ex.Message); }
        }
    }
}
