using System;
using Newtonsoft.Json.Linq;

// Pure validation shared by startup preflight and registration; no Unity/game access.
internal static class VisualScaleFixture
{
    internal static float Validate(JToken token)
    {
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            throw new InvalidOperationException("visualScale must be a finite number0.1..4.");
        double value = (double)token;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.1 || value > 4.0)
            throw new InvalidOperationException("visualScale must be a finite number0.1..4.");
        return (float)value;
    }
}
