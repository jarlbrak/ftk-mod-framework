using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Contains registered identities only. Native UI owns button instances and their lifecycle.
    internal static class ClassProficiencyRegistry
    {
        private static Dictionary<int, List<int>> classes = new Dictionary<int, List<int>>();
        private static Dictionary<int, Dictionary<int, List<int>>> weapons =
            new Dictionary<int, Dictionary<int, List<int>>>();
        internal static int Count
        {
            get
            {
                HashSet<int> ids = new HashSet<int>(classes.Keys);
                foreach (int id in weapons.Keys) ids.Add(id);
                return ids.Count;
            }
        }

        internal static void Attach(int classId, int[] proficiencies)
        {
            List<int> actions;
            if (!classes.TryGetValue(classId, out actions))
            {
                actions = new List<int>();
                classes.Add(classId, actions);
            }
            foreach (int proficiency in proficiencies)
                if (!actions.Contains(proficiency)) actions.Add(proficiency);
        }

        internal static int[] Get(int classId)
        {
            List<int> actions;
            return classes.TryGetValue(classId, out actions) ? actions.ToArray() : new int[0];
        }

        internal static bool AttachWeapon(int classId, int[] weaponIds, int[] proficiencies)
        {
            Dictionary<int, List<int>> byWeapon;
            if (!weapons.TryGetValue(classId, out byWeapon))
                byWeapon = new Dictionary<int, List<int>>();
            foreach (int weaponId in weaponIds)
            {
                List<int> existing;
                byWeapon.TryGetValue(weaponId, out existing);
                HashSet<int> combined = existing == null ? new HashSet<int>() : new HashSet<int>(existing);
                foreach (int action in proficiencies) combined.Add(action);
                if (combined.Count > 16) return false;
            }
            if (!weapons.ContainsKey(classId)) weapons.Add(classId, byWeapon);
            foreach (int weaponId in weaponIds)
            {
                List<int> existing;
                if (!byWeapon.TryGetValue(weaponId, out existing))
                {
                    existing = new List<int>();
                    byWeapon.Add(weaponId, existing);
                }
                foreach (int action in proficiencies)
                    if (!existing.Contains(action)) existing.Add(action);
            }
            return true;
        }

        internal static int[] Get(int classId, int weaponId)
        {
            List<int> result = new List<int>(Get(classId));
            Dictionary<int, List<int>> byWeapon;
            List<int> conditional;
            if (weapons.TryGetValue(classId, out byWeapon) &&
                byWeapon.TryGetValue(weaponId, out conditional))
                foreach (int action in conditional)
                    if (!result.Contains(action)) result.Add(action);
            return result.ToArray();
        }

        internal static bool IsWeaponAction(int action)
        {
            foreach (Dictionary<int, List<int>> byWeapon in weapons.Values)
                foreach (List<int> actions in byWeapon.Values)
                    if (actions.Contains(action)) return true;
            return false;
        }

        internal static KeyValuePair<int, int[]>[] GetWeaponGrants(int weaponId)
        {
            List<int> classIds = new List<int>(weapons.Keys);
            classIds.Sort();
            List<KeyValuePair<int, int[]>> grants = new List<KeyValuePair<int, int[]>>();
            foreach (int classId in classIds)
            {
                List<int> actions;
                if (weapons[classId].TryGetValue(weaponId, out actions))
                    grants.Add(new KeyValuePair<int, int[]>(classId, actions.ToArray()));
            }
            return grants.ToArray();
        }

        internal static bool Allows(int classId, int weaponId, int action)
        {
            List<int> unconditional;
            if (classes.TryGetValue(classId, out unconditional) && unconditional.Contains(action)) return true;
            Dictionary<int, List<int>> byWeapon;
            List<int> conditional;
            return weapons.TryGetValue(classId, out byWeapon) &&
                byWeapon.TryGetValue(weaponId, out conditional) && conditional.Contains(action);
        }

        internal static Action SuspendForReload()
        {
            Dictionary<int, List<int>> previous = classes;
            Dictionary<int, Dictionary<int, List<int>>> previousWeapons = weapons;
            classes = new Dictionary<int, List<int>>();
            weapons = new Dictionary<int, Dictionary<int, List<int>>>();
            return delegate { classes = previous; weapons = previousWeapons; };
        }
    }
}
