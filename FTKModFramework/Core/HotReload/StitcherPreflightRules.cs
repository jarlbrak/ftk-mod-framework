using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.HotReload
{
    // Mirrors the inspected native Stitcher routing without creating avatar consumers.
    internal static class StitcherPreflightRules
    {
        internal static string FlattenedPath(string prefabName, int activeRenderers)
        {
            if (string.IsNullOrEmpty(prefabName) || activeRenderers != 1)
                throw new InvalidOperationException("Native garment requires exactly one active source renderer.");
            return prefabName + "(Clone)";
        }

        internal static int[] MapBones(string[] source, string[] target)
        {
            if (source == null || source.Length == 0 || target == null || target.Length == 0)
                throw new InvalidOperationException("Missing native garment or avatar bone catalog.");
            Dictionary<string, int> catalog = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < target.Length; i++)
            {
                if (string.IsNullOrEmpty(target[i]) || catalog.ContainsKey(target[i]))
                    throw new InvalidOperationException("Missing or duplicate native avatar transform name.");
                catalog.Add(target[i], i);
            }
            int[] result = new int[source.Length];
            for (int i = 0; i < source.Length; i++)
                if (string.IsNullOrEmpty(source[i]) || !catalog.TryGetValue(source[i], out result[i]))
                    throw new InvalidOperationException("Native avatar lacks garment bone: " + source[i]);
            return result;
        }
    }
}
