using System;
using UnityEngine;

namespace FTKModFramework.Core.Performance
{
    internal static class WaterFaceGuard
    {
        private static float MaximumAbsolute(Vector3 value)
        {
            // Math.Max propagates NaN, so the comparisons below reject nonfinite components.
            return Math.Max(Math.Abs(value.x), Math.Max(Math.Abs(value.y), Math.Abs(value.z)));
        }

        internal static bool CanReuse(Vector3 firstEdge, Vector3 secondEdge, Vector3 thirdEdge,
            Vector3 rawCross, Vector3 firstNormal)
        {
            if (!WaterMeshPerformance.Enabled) return false;
            float scale = Math.Max(MaximumAbsolute(firstEdge), Math.Max(MaximumAbsolute(secondEdge), MaximumAbsolute(thirdEdge)));
            float area = MaximumAbsolute(rawCross);
            float normalMagnitude = MaximumAbsolute(firstNormal);
            // Stay well above Unity's normalization cutoff and reject poorly conditioned triangles.
            // These conservative limits bound the supported domain; they are not a bit-exact contract.
            return scale > 0f && scale <= 1000000f && area >= 0.001f && area <= 1e12f &&
                area >= 0.25f * scale * scale && normalMagnitude <= 1.000001f &&
                normalMagnitude >= 0.5f && MaximumAbsolute(firstEdge + secondEdge + thirdEdge) <= 0.0000005f * scale;
        }
    }
}
