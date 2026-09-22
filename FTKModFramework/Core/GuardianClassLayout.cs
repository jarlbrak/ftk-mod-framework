using System;

namespace FTKModFramework.Core
{
    // Layout-local Y increases upward. The heading and list move together only as far as
    // needed to clear the ability label, preserving their native spacing.
    internal static class GuardianClassLayout
    {
        internal static float Shift(float abilityBottom, float headingTop, float gap)
        {
            if (float.IsNaN(abilityBottom) || float.IsInfinity(abilityBottom) ||
                float.IsNaN(headingTop) || float.IsInfinity(headingTop) ||
                float.IsNaN(gap) || float.IsInfinity(gap) || gap < 0) return 0;
            return Math.Min(0, abilityBottom - headingTop - gap);
        }
    }
}
