namespace FTKModFramework.Core
{
    internal static class SkySpikeRotorMath
    {
        internal static float Advance(float angle, float degreesPerSecond, float deltaTime)
        {
            if (!SkySpikeAssetContract.Finite(angle) || !SkySpikeAssetContract.Finite(degreesPerSecond) ||
                !SkySpikeAssetContract.Finite(deltaTime) || deltaTime < 0f) return angle;
            double next = ((double)angle + (double)degreesPerSecond * deltaTime) % 360.0;
            return (float)(next < 0.0 ? next + 360.0 : next);
        }
    }
}
