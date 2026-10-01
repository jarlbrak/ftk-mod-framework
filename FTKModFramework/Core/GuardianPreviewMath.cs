namespace FTKModFramework.Core
{
    // Native maximum-damage preview: the committed attack rounds the complete damage product
    // once, then applies Frozen to the resulting damage channel and rounds again.
    internal static class GuardianPreviewMath
    {
        internal static int EffectiveType(int weaponType, int overrideType, int noneType)
        {
            return overrideType == noneType ? weaponType : overrideType;
        }

        internal static int Calculate(int weaponMax, float action, float resistance, float reckoning,
            int profilePercent, float armor, float frozen)
        {
            if (weaponMax < 0 || action < 0f || resistance < 0f || reckoning < 0f ||
                profilePercent < 0 || armor < 0f || frozen < 0f) return 0;
            float product = weaponMax * action * resistance * reckoning *
                (profilePercent / 100f) * armor;
            int damage = Round(product);
            return frozen == 1f ? damage : Round(damage * frozen);
        }

        private static int Round(float value)
        {
            if (value <= 0f) return 0;
            if (value >= int.MaxValue) return int.MaxValue;
            return (int)(value + 0.5f);
        }
    }
}
