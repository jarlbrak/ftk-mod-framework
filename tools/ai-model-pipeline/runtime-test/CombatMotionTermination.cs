using System;
using Newtonsoft.Json.Linq;

// Distinguish the narrow hierarchy-resolution failure from ownership or other failures.
internal sealed class AvatarControllerResolutionException : InvalidOperationException
{
    internal AvatarControllerResolutionException() : base("Avatar controller ambiguous or absent.") { }
}

internal static class CombatMotionTermination
{
    internal static bool AfterObservedDeath(JArray events, Exception error)
    {
        if (!(error is AvatarControllerResolutionException) || events == null) return false;
        foreach (JObject item in events)
        {
            JToken exception = item["exception"];
            if ((string)item["nativeMethod"] == "CharacterEventListener.CombatTrigger entry"
                && (string)item["trigger"] == "Death" && (bool?)item["finalized"] == true
                && exception != null && exception.Type == JTokenType.Null) return true;
        }
        return false;
    }
}
