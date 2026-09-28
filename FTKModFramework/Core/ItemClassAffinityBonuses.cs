using System;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Small additive equipment bonuses that apply only to one specified class while the item is
    /// equipped. Attribute values use the native scale, where 0.01 is one stat point.
    /// </summary>
    public sealed class ItemClassAffinityBonuses
    {
        public int Armor { get; private set; }
        public int Resistance { get; private set; }
        public float Vitality { get; private set; }
        public float Speed { get; private set; }
        public int Reflect { get; private set; }

        public ItemClassAffinityBonuses(int armor = 0, int resistance = 0, float vitality = 0f,
            float speed = 0f, int reflect = 0)
        {
            if (armor < 0 || armor > 1) throw new ArgumentOutOfRangeException("armor", "Class affinity adds at most 1 Armor per item.");
            if (resistance < 0 || resistance > 1) throw new ArgumentOutOfRangeException("resistance", "Class affinity adds at most 1 Resistance per item.");
            if (reflect < 0 || reflect > 1) throw new ArgumentOutOfRangeException("reflect", "Class affinity adds at most 1 Reflect per item.");
            if (!IsSinglePoint(vitality)) throw new ArgumentOutOfRangeException("vitality", "Class affinity adds at most 1 Vitality point per item in 0.01 steps.");
            if (!IsSinglePoint(speed)) throw new ArgumentOutOfRangeException("speed", "Class affinity adds at most 1 Speed point per item in 0.01 steps.");
            if (armor == 0 && resistance == 0 && reflect == 0 && vitality == 0f && speed == 0f)
                throw new ArgumentException("At least one class affinity bonus must be nonzero.");

            Armor = armor;
            Resistance = resistance;
            Vitality = vitality;
            Speed = speed;
            Reflect = reflect;
        }

        private static bool IsSinglePoint(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 0.01f) return false;
            float points = value * 100f;
            return Math.Abs(points - (float)Math.Round(points)) < 0.0001f;
        }

        internal bool SameAs(ItemClassAffinityBonuses other)
        {
            return other != null && Armor == other.Armor && Resistance == other.Resistance &&
                Math.Abs(Vitality - other.Vitality) < 0.000001f && Math.Abs(Speed - other.Speed) < 0.000001f &&
                Reflect == other.Reflect;
        }
    }

    internal sealed class ItemClassAffinityTotal
    {
        internal int Armor;
        internal int Resistance;
        internal float Vitality;
        internal float Speed;
        internal int Reflect;
    }
}
