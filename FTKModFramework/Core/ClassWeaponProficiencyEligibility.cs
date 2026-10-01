using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ClassWeaponProficiencyEligibility
    {
        internal static bool Allows(CharacterOverworld cow, int action)
        {
            if (!ClassProficiencyRegistry.IsWeaponAction(action)) return true;
            if (cow == null || cow.m_CharacterStats == null) return false;
            try
            {
                int classId = (int)cow.m_CharacterStats.m_CharacterClass;
                int weaponId = (int)cow.m_WeaponID;
                if (ClassProficiencyRegistry.Allows(classId, weaponId, action)) return true;
                foreach (int granted in ItemProficiencyRuntime.EquippedActions(cow))
                    if (granted == action) return true;
                FTK_weaponStats2 row = Content.Db<FTK_weaponStats2DB>().GetEntryByInt(weaponId);
                FTK_proficiencyTable proficiency = FTK_proficiencyTableDB.Get((FTK_proficiencyTable.ID)action);
                if (row == null || row.m_Prefab == null || proficiency == null ||
                    string.IsNullOrEmpty(proficiency.m_ID)) return false;
                Weapon weapon = row.m_Prefab.GetComponentInChildren<Weapon>(true);
                if (weapon == null || weapon.m_ProficiencyEffects == null) return false;
                foreach (var effect in weapon.m_ProficiencyEffects)
                    if (effect.Key.m_ID == proficiency.m_ID) return true;
            }
            catch { return false; }
            return false;
        }
    }
}
