using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

// Shared content/helper parser; no Unity or Core references. File existence/pins remain the caller's responsibility.
internal static class MaterialSlotFixture
{
    internal sealed class Slot
    {
        internal readonly int PrimitiveIndex, NativeMaterialSlot;
        internal readonly string TextureFile;
        internal readonly bool DisableNativeEmission;
        internal Slot(int primitive, int native, string texture, bool emission)
        { PrimitiveIndex = primitive; NativeMaterialSlot = native; TextureFile = texture; DisableNativeEmission = emission; }
    }

    internal static Slot[] Read(JObject renderer)
    {
        if (renderer == null) throw new InvalidOperationException("Expected renderer object.");
        JToken token = renderer["materialSlots"];
        if (token == null) return null;
        if (renderer.Property("textureFile") != null || renderer.Property("disableNativeEmission") != null)
            throw new InvalidOperationException("materialSlots cannot be combined with legacy textureFile/disableNativeEmission fields.");
        JArray array = token as JArray;
        if (array == null || array.Count < 2 || array.Count > 4)
            throw new InvalidOperationException("materialSlots requires2..4 descriptors.");
        Slot[] result = new Slot[array.Count];
        bool[] primitives = new bool[array.Count], native = new bool[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            JObject slot = array[i] as JObject;
            if (slot == null) throw new InvalidOperationException("Expected material slot object.");
            foreach (JProperty property in slot.Properties())
                if (property.Name != "primitiveIndex" && property.Name != "nativeMaterialSlot" &&
                    property.Name != "textureFile" && property.Name != "disableNativeEmission")
                    throw new InvalidOperationException("Unknown material slot field: " + property.Name);
            int primitive = Index(slot["primitiveIndex"], array.Count), material = Index(slot["nativeMaterialSlot"], array.Count);
            if (primitives[primitive] || native[material]) throw new InvalidOperationException("Material slot mappings must be bijective.");
            primitives[primitive] = native[material] = true;
            string texture = null;
            if (slot["textureFile"] != null)
            {
                if (slot["textureFile"].Type != JTokenType.String ||
                    !Regex.IsMatch((string)slot["textureFile"], "^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}\\.png$"))
                    throw new InvalidOperationException("Slot textureFile must be a bounded PNG basename when present.");
                texture = (string)slot["textureFile"];
            }
            result[i] = new Slot(primitive, material, texture, RendererEmissionFixture.Read(slot["disableNativeEmission"]));
        }
        return result;
    }

    private static int Index(JToken token, int count)
    {
        if (token == null || token.Type != JTokenType.Integer || (long)token < 0 || (long)token >= count)
            throw new InvalidOperationException("Slot indices must be integers covering0..N-1.");
        return (int)token;
    }
}
