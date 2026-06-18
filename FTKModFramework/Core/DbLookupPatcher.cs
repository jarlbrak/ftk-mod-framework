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
    /// <c>try { (int)Enum.Parse(typeof(FTK_*.ID), id, true) } catch (ArgumentException) { -1 }</c>.
    /// For a CUSTOM id (e.g. "synthetic_000001") that Enum.Parse over the multi-hundred-member enum
    /// THROWS and is caught internally. On Mono 3.5 a throw+catch is a full stack walk; MakeIndex
    /// calls GetIntFromID for EVERY row, so N rows * N index rebuilds = O(N^2) exceptions (the ~80s
    /// at scale). We therefore PREFIX, not postfix: when the id is one of ours we set the synthetic
    /// result and SKIP the original entirely, so the throwing Enum.Parse never runs for custom ids.
    ///
    /// This is byte-identical for vanilla ids: they are never in CustomIds, so the prefix returns
    /// true and the original runs unchanged. Custom ids are namespaced strings that can never be a
    /// valid FTK_*.ID member name, so skipping the original loses nothing.
    ///
    /// This single hook is enough for the read path too: the DB's MakeIndex() rebuilds its
    /// int-&gt;row dictionary by calling GetIntFromID(row.m_ID) for every row, so once this patch is
    /// live our rows get indexed under their synthetic ints and the normal GetEntry / GetEntryByInt
    /// paths find them.
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
                prefix: new HarmonyMethod(typeof(DbLookupPatcher), nameof(GetIntFromID_Prefix)));
        }

        // Signature mirrors the game's GetIntFromID(string _id): the parameter name must match.
        // Returns false to SKIP the original (and its throwing Enum.Parse) when we resolve a custom id;
        // returns true to let the original run unchanged for vanilla ids.
        private static bool GetIntFromID_Prefix(object __instance, string _id, ref int __result)
        {
            Dictionary<string, int> map;
            if (ContentRegistry.CustomIds.TryGetValue(__instance.GetType(), out map))
            {
                int synthetic;
                if (map.TryGetValue(_id, out synthetic)) { __result = synthetic; return false; }
            }
            return true; // vanilla id (or unknown): run the original normally.
        }
    }
}
