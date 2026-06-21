using System;
using System.Collections.Generic;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Compile-time-typed adventure-cache helpers used ONLY by the agent test bridge. These live in the Agent
    /// assembly area (not Core) so the harness is self-contained: the bridge needs to force the game's adventure
    /// cache to exist and resolve previews for list_adventures / start_run, but that is a test-tooling concern,
    /// not part of the shipped content API.
    ///
    /// They exist because the bridge first resolved GameCache.Cache.GameDefinitions by reflection string and got
    /// the name wrong (AccessTools "Could not find type" spam). The framework already references this type AT
    /// COMPILE TIME (Adventures.EnsureLoaded, the GameDefinitions.Initialize Harmony patch), so the bridge can
    /// call the type directly and let the compiler bind it. Each method is fully defensive (try/catch, never
    /// throws) so a bridge action degrades to a clean result, never a crash.
    /// </summary>
    internal static class AdventureCache
    {
        /// <summary>Read-only test of whether the game's adventure cache has been built. NO side effects:
        /// it only reads the private static _previews dictionary (non-null once Initialize() has run).</summary>
        internal static bool IsCacheBuilt()
        {
            try { return GameCache.Cache.GameDefinitions._previews != null; }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("AdventureCache.IsCacheBuilt: " + e.Message);
                return false;
            }
        }

        /// <summary>Force the game's adventure cache to exist, then (re)inject our previews. Initialize() is
        /// idempotent + fully synchronous, so on return the cache is built; Adventures.EnsureLoaded() then
        /// guarantees our adventures (e.g. "HollowMire") are present. Logs and swallows any failure (never throws).</summary>
        internal static void EnsureCacheBuilt()
        {
            try
            {
                if (!IsCacheBuilt())
                    GameCache.Cache.GameDefinitions.Initialize();
                Adventures.EnsureLoaded();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("AdventureCache.EnsureCacheBuilt: " + e.Message);
            }
        }

        /// <summary>Force the cache to build, then resolve a single preview by key. GetPreview does not
        /// null-guard _previews, so we ensure the cache first and re-check before calling. Returns null on any
        /// miss or failure (never throws).</summary>
        internal static GameDefinitionPreview GetPreviewSafe(string key)
        {
            try
            {
                EnsureCacheBuilt();
                if (!IsCacheBuilt()) return null;
                return GameCache.Cache.GameDefinitions.GetPreview(key);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("AdventureCache.GetPreviewSafe('" + (key ?? "?") + "'): " + e.Message);
                return null;
            }
        }

        /// <summary>Force the cache to build, then return the full list of adventure keys. Deliberate side
        /// effect (it builds the cache): use for the list_adventures ACTION, not for /state observation.
        /// Returns an empty list on failure (never throws, never null).</summary>
        internal static List<string> GetPreviewNamesForced()
        {
            try
            {
                EnsureCacheBuilt();
                if (!IsCacheBuilt()) return new List<string>();
                return GameCache.Cache.GameDefinitions.GetNames();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("AdventureCache.GetPreviewNamesForced: " + e.Message);
                return new List<string>();
            }
        }

        /// <summary>Read-only variant for /state observation: return the adventure keys ONLY if the cache is
        /// already built, else null (NEVER forces a build, so observing state stays side-effect-free). Returns
        /// null on any failure too.</summary>
        internal static List<string> GetPreviewNamesIfBuilt()
        {
            try
            {
                if (!IsCacheBuilt()) return null;
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
