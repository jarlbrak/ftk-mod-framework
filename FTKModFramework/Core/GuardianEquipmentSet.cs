using System;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>One armor family and its legal matching armaments. All rows must be registered custom content.</summary>
    public sealed class GuardianEquipmentSet
    {
        public FTK_itembase Head { get; private set; }
        public FTK_itembase Body { get; private set; }
        public FTK_itembase Feet { get; private set; }
        public FTK_itembase OneHand { get; private set; }
        public FTK_itembase Shield { get; private set; }
        public FTK_itembase TwoHand { get; private set; }
        public GuardianProfile Minor { get; private set; }
        public GuardianProfile Core { get; private set; }
        public GuardianEquipmentBonuses Completion { get; private set; }
        public FTK_proficiencyTable[] CoreProficiencies { get { return (FTK_proficiencyTable[])proficiencies.Clone(); } }
        private readonly FTK_proficiencyTable[] proficiencies;
        private readonly FTK_proficiencyTable[] armorSources;
        public FTK_proficiencyTable[] ArmorDamageSources { get { return (FTK_proficiencyTable[])armorSources.Clone(); } }
        public float ArmorDamageMultiplier { get; private set; }

        public GuardianEquipmentSet(FTK_itembase head, FTK_itembase body, FTK_itembase feet,
            FTK_itembase oneHand, FTK_itembase shield, FTK_itembase twoHand,
            GuardianProfile minor, GuardianProfile core, GuardianEquipmentBonuses completion,
            FTK_proficiencyTable[] coreProficiencies)
            : this(head, body, feet, oneHand, shield, twoHand, minor, core, completion,
                coreProficiencies, null, 1f) { }

        public GuardianEquipmentSet(FTK_itembase head, FTK_itembase body, FTK_itembase feet,
            FTK_itembase oneHand, FTK_itembase shield, FTK_itembase twoHand,
            GuardianProfile minor, GuardianProfile core, GuardianEquipmentBonuses completion,
            FTK_proficiencyTable[] coreProficiencies, FTK_proficiencyTable[] armorDamageSources,
            float armorDamageMultiplier)
        {
            if (armorDamageMultiplier < 1f || armorDamageMultiplier > 1.5f ||
                float.IsNaN(armorDamageMultiplier) || float.IsInfinity(armorDamageMultiplier))
                throw new ArgumentOutOfRangeException("armorDamageMultiplier");
            ArmorDamageMultiplier = armorDamageMultiplier;
            armorSources = armorDamageSources == null ? new FTK_proficiencyTable[0] :
                (FTK_proficiencyTable[])armorDamageSources.Clone();
            if (armorSources.Length > 8 || (armorSources.Length == 0) != (armorDamageMultiplier == 1f))
                throw new ArgumentException("Armor sources and multiplier must be supplied together.");
            foreach (FTK_proficiencyTable source in armorSources)
                if (source == null) throw new ArgumentNullException("armorDamageSources");
            if (head == null || body == null || feet == null || oneHand == null || shield == null || twoHand == null ||
                minor == null || core == null || completion == null) throw new ArgumentNullException("Guardian set member");
            Head = head; Body = body; Feet = feet; OneHand = oneHand; Shield = shield; TwoHand = twoHand;
            Minor = minor; Core = core; Completion = completion;
            proficiencies = coreProficiencies == null ? new FTK_proficiencyTable[0] : (FTK_proficiencyTable[])coreProficiencies.Clone();
            foreach (FTK_proficiencyTable proficiency in proficiencies)
                if (proficiency == null) throw new ArgumentNullException("coreProficiencies");
        }
    }
}
