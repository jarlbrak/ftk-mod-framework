using System;
using Newtonsoft.Json.Linq;

// Test-content routing only. Renderer metadata never turns the singular API into explicit assignment.
internal static class LegacyBodyFixture
{
    internal static bool IsLegacy(JObject profile)
    {
        JToken kind = profile["bindingKind"];
        if (kind == null) return false;
        if (kind.Type != JTokenType.String || (string)kind != "legacy-singular")
            throw new InvalidOperationException("Only explicit opt-in bindingKind legacy-singular is supported.");
        return true;
    }

    internal static float[] Tint(JObject profile)
    {
        JArray rgba = profile["tint"] as JArray;
        if (rgba == null || rgba.Count != 4) throw new InvalidOperationException("Legacy tint requires four RGBA numbers.");
        float[] result = new float[4];
        for (int i = 0; i < 4; i++)
        {
            if (rgba[i].Type != JTokenType.Float && rgba[i].Type != JTokenType.Integer)
                throw new InvalidOperationException("Legacy tint components must be numbers.");
            double value = (double)rgba[i];
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                throw new InvalidOperationException("Legacy tint components must be finite within 0..1.");
            result[i] = (float)value;
        }
        return result;
    }

    internal static void Validate(JObject profile)
    {
        if (!IsLegacy(profile))
        {
            if (profile["tint"] != null) throw new InvalidOperationException("tint is only supported by the legacy singular fixture.");
            return;
        }
        Tint(profile);
        JArray renderers = profile["renderers"] as JArray;
        if (renderers == null || renderers.Count != 1 || !(renderers[0] is JObject))
            throw new InvalidOperationException("Legacy singular fixture requires one expected renderer.");
        JObject body = (JObject)renderers[0];
        if (body["disableNativeEmission"] != null || body["materialSlots"] != null || body["rendererKind"] != null)
            throw new InvalidOperationException("Legacy singular API has no renderer-kind, emission or material-slot options.");
        if (profile["resourcePrefab"] != null || profile["portraitMarkerPath"] != null)
            throw new InvalidOperationException("Legacy lifetime fixture preserves native prefab and portrait selection.");
    }
}
