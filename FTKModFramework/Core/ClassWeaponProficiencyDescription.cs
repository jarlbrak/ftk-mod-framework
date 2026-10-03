using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class ClassWeaponProficiencyDescription
    {
        internal static string[] Lines(int weaponId)
        {
            List<string> lines = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (KeyValuePair<int, int[]> grant in ClassProficiencyRegistry.GetWeaponGrants(weaponId))
            {
                FTK_playerGameStart heroClass = Content.Db<FTK_playerGameStartDB>().GetEntryByInt(grant.Key);
                if (heroClass == null) continue;
                string className = heroClass.GetDisplayName();
                if (string.IsNullOrEmpty(className) || className.Trim().Length == 0) continue;
                foreach (int action in grant.Value)
                {
                    FTK_proficiencyTable row = FTK_proficiencyTableDB.Get((FTK_proficiencyTable.ID)action);
                    if (row == null) continue;
                    string name = row.GetLocalizedDisplayName();
                    if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) continue;
                    string line = className + " Skill: " + name;
                    if (seen.Add(line)) lines.Add(line);
                }
            }
            return lines.ToArray();
        }
    }
}
