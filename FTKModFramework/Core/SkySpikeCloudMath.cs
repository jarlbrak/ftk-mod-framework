namespace FTKModFramework.Core
{
    internal static class SkySpikeCloudMath
    {
        internal static bool TryPlanarCoordinate(float position, float min, float max, out float uv)
        {
            uv = 0f;
            if (!SkySpikeAssetContract.Finite(position) || !SkySpikeAssetContract.Finite(min) ||
                !SkySpikeAssetContract.Finite(max)) return false;
            double span = (double)max - min;
            if (span <= 0.00001) return false;
            double value = ((double)position - min) / span;
            if (value < 0.0 || value > 1.0) return false;
            uv = (float)value;
            return true;
        }

        // Two broad noise cells per texture period give roughly 25-unit billows. UV projection may
        // stretch or rotate that estimate; reject extreme inputs instead of creating dense repetition.
        internal static bool TryTextureScale(float worldSpan, float uvMin, float uvMax, out float scale)
        {
            scale = 1f;
            if (!SkySpikeAssetContract.Finite(worldSpan) || !SkySpikeAssetContract.Finite(uvMin) ||
                !SkySpikeAssetContract.Finite(uvMax) || worldSpan <= 0.001f) return false;
            double span = (double)uvMax - uvMin;
            if (span <= 0.00001) return false;
            double value = worldSpan / (span * 50.0);
            if (value < 0.001 || value > 128.0) return false;
            scale = (float)value;
            return true;
        }
    }
}
