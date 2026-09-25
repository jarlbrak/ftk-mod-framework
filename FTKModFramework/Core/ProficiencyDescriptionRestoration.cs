using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ProficiencyDescriptionRestoration
    {
        internal static void Apply(FTK_proficiencyTable row, string[] description)
        {
            if (row == null || description == null || description.Length < 2 || row.m_FullSlots ||
                row.m_ProficiencyPrefab != null || row.m_IgnoresArmor) return;
            string authored;
            int id;
            if (!Localization.TryGetProficiencyDescription(row.m_ID, out authored) || string.IsNullOrEmpty(authored) ||
                !ContentRegistry.TryGetSyntheticId(row.m_ID, out id, typeof(FTK_proficiencyTableDB)) ||
                !object.ReferenceEquals(Content.Db<FTK_proficiencyTableDB>().GetEntryByInt(id), row)) return;
            // GetBattleButtonInfo returns a fresh two-line array. Its target label stays native.
            description[1] = authored;
        }
    }
}
