using System;
using GridEditor;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    // A private modifier row is keyed to the custom item ID. It starts blank rather than
    // cloning the native template's modifier row, so omitted bonuses stay absent.
    internal sealed class ItemModifierEntry
    {
#pragma warning disable CS0649
        [JsonProperty("armor")] public int Armor;
        [JsonProperty("resistance")] public int Resistance;
        [JsonProperty("vitality")] public float Vitality;
        [JsonProperty("speed")] public float Speed;
        [JsonProperty("awareness")] public float Awareness;
        [JsonProperty("talent")] public float Talent;
        [JsonProperty("focusCapacity")] public int FocusCapacity;
        [JsonProperty("reflect")] public int Reflect;
#pragma warning restore CS0649

        internal void Validate()
        {
            if (Armor < 0 || Armor > 100 || Resistance < 0 || Resistance > 100 ||
                Reflect < 0 || Reflect > 100 || FocusCapacity < 0 || FocusCapacity > 10 ||
                !ValidStat(Vitality) || !ValidStat(Speed) ||
                !ValidStat(Awareness) || !ValidStat(Talent))
                throw new ArgumentException("item modifier outside supported range");
        }

        internal void Apply(FTK_characterModifier modifier)
        {
            modifier.m_ModDefensePhysical = Armor;
            modifier.m_ModDefenseMagic = Resistance;
            modifier.m_ModVitality = Vitality;
            modifier.m_ModQuickness = Speed;
            modifier.m_ModAwareness = Awareness;
            modifier.m_ModTalent = Talent;
            modifier.m_ExtraFocus = FocusCapacity;
            modifier.m_ReflectDamage = Reflect;
        }

        private static bool ValidStat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 1;
        }
    }
}
