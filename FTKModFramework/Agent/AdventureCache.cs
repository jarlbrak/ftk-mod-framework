using System;
using System.Collections.Generic;

namespace FTKModFramework.Agent
{
    // Observation must not initialize or inject game data as a side effect.
    internal static class AdventureCache
    {
        internal static List<string> GetPreviewNamesIfBuilt()
        {
            try
            {
                if (GameCache.Cache.GameDefinitions._previews == null) return null;
                return GameCache.Cache.GameDefinitions.GetNames();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("AdventureCache.GetPreviewNamesIfBuilt: " + e.Message);
                return null;
            }
        }
    }
}
