using System;
using Newtonsoft.Json.Linq;

// Shared profile parser for the narrow native FallOffLimb policy. Omission keeps native behavior.
internal static class FallOffPolicyFixture
{
    internal const string PreserveCustomBody = "preserve-custom-body";

    internal static bool Read(JToken value)
    {
        if (value == null) return false;
        if (value.Type != JTokenType.String || (string)value != PreserveCustomBody)
            throw new InvalidOperationException("fallOffPolicy must be exact 'preserve-custom-body' when supplied.");
        return true;
    }

    internal static bool Validate(JObject profile, bool legacy)
    {
        bool preserveCustomBody = Read(profile == null ? null : profile["fallOffPolicy"]);
        if (preserveCustomBody && legacy)
            throw new InvalidOperationException("fallOffPolicy requires explicit plural renderer assignments.");
        return preserveCustomBody;
    }
}
