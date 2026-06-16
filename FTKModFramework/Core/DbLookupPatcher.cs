using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Teaches a content DB to resolve our synthetic string IDs.
    ///
    /// Every FTK content table implements <c>int GetIntFromID(string id)</c> as
    /// <c>(int)Enum.Parse(typeof(FTK_*.ID), id)</c>, returning -1 for anything not in the
    /// vanilla enum. We postfix it: if the vanilla lookup failed and the id is one of ours,
    /// return the synthetic int we minted for it.
    ///
    /// That single hook is enough for the read path too: the DB's MakeIndex() rebuilds its
    /// int-&gt;row dictionary by calling GetIntFromID(row.m_ID) for every row, so once this
    /// patch is live our rows get indexed under their synthetic ints and the normal
    /// GetEntry / GetEntryByInt paths find them.
    /// </summary>
    public static class DbLookupPatcher
    {
        private static readonly HashSet<Type> Patched = new HashSet<Type>();
        private static Harmony _harmony;

        public static void Init(Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void EnsurePatched(Type dbType)
        {
            if (_harmony == null)
                throw new InvalidOperationException("DbLookupPatcher.Init(harmony) must be called first.");
            if (Patched.Contains(dbType)) return;
            Patched.Add(dbType);

            MethodInfo getIntFromId = dbType.GetMethod(
                "GetIntFromID", Reflect.All, null, new[] { typeof(string) }, null);

            if (getIntFromId == null)
            {
                Plugin.Log.LogWarning("DbLookupPatcher: " + dbType.Name + " has no GetIntFromID(string); custom IDs may not resolve.");
                return;
            }

            _harmony.Patch(getIntFromId,
                postfix: new HarmonyMethod(typeof(DbLookupPatcher), nameof(GetIntFromID_Postfix)));
        }

        // Signature mirrors the game's GetIntFromID(string _id): the parameter name must match.
        private static void GetIntFromID_Postfix(object __instance, string _id, ref int __result)
        {
            if (__result >= 0) return; // vanilla resolved it
            Dictionary<string, int> map;
            if (ContentRegistry.CustomIds.TryGetValue(__instance.GetType(), out map))
            {
                int synthetic;
                if (map.TryGetValue(_id, out synthetic)) __result = synthetic;
            }
        }
    }
}
