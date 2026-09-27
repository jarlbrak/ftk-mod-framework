using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Skip intro tweak (convenience.skip-intro). SplashScreen.GetAnyButton is private and
    /// called only by SplashScreen.DisplayScene, which leaves each intro video early when it returns
    /// true. A postfix is used so the vanilla input poll always runs and nothing here can skip the
    /// original; with the tweak off, faulted or uninitialized the result is left untouched. The
    /// framework's own ModSplash is a separate title-screen card and does not pass through here.</summary>
    [HarmonyPatch(typeof(SplashScreen), "GetAnyButton")]
    internal static class SkipIntroPatch
    {
        private static void Postfix(ref bool __result)
        {
            int handle = FrameworkTweaks.SkipIntro;
            if (!Tweaks.IsOn(handle)) return;
            bool pressed;
            try
            {
                pressed = FrameworkTweaks.SkipIntroAnyButton(Tweaks.Registry, handle, __result);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            __result = pressed;
        }
    }
}
