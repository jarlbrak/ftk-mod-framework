using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // SKY SPIKE ONLY: the hull-band overhang rule on plain slice data, kept free of UnityEngine so it can be
    // checked offline against the model measurements. Not a supported API.
    //
    // The hull band (vertices at or below deck + clearance) is cut into equal height slices. Wings, boarding
    // planks and similar deck-level overhangs make a few slices much wider on one axis than the hull around
    // them, so on each axis a slice wider than flare x the median slice width is treated as overhang and left
    // out of that axis's range. The other axis still counts the slice.
    internal static class SkySpikeHullMath
    {
        // Occupied slices have counts[s] > 0. Returns false when no slice is occupied. With fewer than three
        // occupied slices, or flare <= 0, nothing is excluded and median is reported as 0.
        internal static bool KeepRange(int[] counts, float[] min, float[] max, float flare,
            out float keptMin, out float keptMax, out float median, out int excluded)
        {
            keptMin = float.MaxValue;
            keptMax = float.MinValue;
            median = 0f;
            excluded = 0;
            List<float> widths = new List<float>();
            for (int s = 0; s < counts.Length; s++)
                if (counts[s] > 0) widths.Add(max[s] - min[s]);
            if (widths.Count == 0) return false;

            float limit = float.MaxValue;
            if (flare > 0f && widths.Count >= 3)
            {
                widths.Sort();
                int mid = widths.Count / 2;
                median = widths.Count % 2 == 1 ? widths[mid] : (widths[mid - 1] + widths[mid]) * 0.5f;
                limit = flare * median;
            }
            for (int s = 0; s < counts.Length; s++)
            {
                if (counts[s] == 0) continue;
                if (max[s] - min[s] > limit) { excluded++; continue; }
                if (min[s] < keptMin) keptMin = min[s];
                if (max[s] > keptMax) keptMax = max[s];
            }
            // Unreachable while the median slice itself is kept (flare >= 1); guards flare below 1.
            if (keptMax < keptMin)
            {
                excluded = 0;
                for (int s = 0; s < counts.Length; s++)
                {
                    if (counts[s] == 0) continue;
                    if (min[s] < keptMin) keptMin = min[s];
                    if (max[s] > keptMax) keptMax = max[s];
                }
            }
            return true;
        }
    }
}
