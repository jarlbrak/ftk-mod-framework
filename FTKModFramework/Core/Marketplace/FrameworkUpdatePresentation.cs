using System;

namespace FTKModFramework.Core.Marketplace
{
    internal static class FrameworkUpdatePresentation
    {
        internal static bool IsDowngrade(string mode, FrameworkRelease release, string runningVersion)
        {
            return mode == "pinned" && release != null && new Version(release.Version).CompareTo(new Version(runningVersion)) < 0;
        }
        internal static string EffectiveTarget(string mode, FrameworkRelease release, string runningVersion)
        {
            if (mode == "pinned" && release != null) return release.Tag;
            if (release != null && release.Available && new Version(release.Version).CompareTo(new Version(runningVersion)) > 0) return release.Tag;
            return "Keep v" + runningVersion + " until a newer compatible release is available";
        }
    }
}
