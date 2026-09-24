using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Class-owned immunity is read from synced class identity. No per-tile context can cover
    // curse application reliably because the native curse guard runs inside an RPC.
    internal static class OverworldAilmentImmunity
    {
        private static Dictionary<int, string> classes = new Dictionary<int, string>();

        internal static int ReloadClassCount { get { return classes.Count; } }

        internal static bool RegisterClass(int classId, string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName.Trim().Length == 0) return false;
            if (!classes.ContainsKey(classId)) classes.Add(classId, displayName.Trim());
            return true;
        }

        internal static bool IsRegistered(int classId) { return classes.ContainsKey(classId); }

        internal static string DisplayName(int classId)
        {
            string name;
            return classes.TryGetValue(classId, out name) ? name : null;
        }

        internal static bool Protects(int classId, bool inCombat, ProficiencyBase.Category category)
        {
            return !inCombat && classes.ContainsKey(classId) &&
                (category == ProficiencyBase.Category.Poison || category == ProficiencyBase.Category.Curse);
        }

        internal static Action SuspendForReload()
        {
            Dictionary<int, string> prior = classes;
            classes = new Dictionary<int, string>();
            return delegate { classes = prior; };
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "HasImmunity")]
    internal static class CharacterStats_OverworldAilmentImmunity_Patch
    {
        private static void Postfix(CharacterStats __instance, ProficiencyBase.Category __0, ref bool __result)
        {
            if (__result || __instance == null || __instance.m_CharacterOverworld == null) return;
            try
            {
                if (OverworldAilmentImmunity.Protects((int)__instance.m_CharacterClass,
                    __instance.m_IsInCombat, __0)) __result = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[overworld-ailment-immunity] " + e.Message); }
        }
    }
}
