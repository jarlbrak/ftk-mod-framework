using System;
using Newtonsoft.Json.Linq;

// Process-lifetime claim: an uncertain native callback is never retried.
internal sealed class NativePartyStartPolicy
{
    public bool Consumed { get; private set; }
    internal static void RequireNativePreview(int localPhotonId, int previewPhotonId, int turn, int count)
    {
        if (localPhotonId <= 0 || previewPhotonId != localPhotonId || count < 1 || count > 3
            || turn < 0 || turn >= count)
            throw new InvalidOperationException("Preview is not a valid local native Party Select slot.");
    }
    public void Submit(string expectedToken, string token, JObject expected, JObject actual, Action callback)
    {
        if (Consumed) throw new InvalidOperationException("Party Start was already submitted in this process.");
        if (string.IsNullOrEmpty(token) || token != expectedToken || expected == null
            || !JToken.DeepEquals(expected, actual))
            throw new InvalidOperationException("Party Start inspection is absent or stale.");
        Consumed = true;
        callback();
    }
}
