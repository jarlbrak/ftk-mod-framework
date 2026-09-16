using System;
using Newtonsoft.Json.Linq;

// Optional enemy renderer option. Missing preserves native emission; explicit null is invalid.
internal static class RendererEmissionFixture
{
    internal static bool Read(JToken token)
    {
        if (token == null) return false;
        if (token.Type != JTokenType.Boolean)
            throw new InvalidOperationException("disableNativeEmission must be a boolean when present.");
        return (bool)token;
    }
}
