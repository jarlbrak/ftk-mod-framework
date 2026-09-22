using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    internal static class ItemModelRegistry
    {
        private static Dictionary<int, EnemyRendererMesh[]> Models = new Dictionary<int, EnemyRendererMesh[]>();
        private static Dictionary<int, EnemyRendererMesh[]> Displays = new Dictionary<int, EnemyRendererMesh[]>();
        internal static IEnumerable<KeyValuePair<int, EnemyRendererMesh[]>> ReloadPlans(bool display)
        { return display ? Displays : Models; }
        internal static int ReloadModelCount { get { return Models.Count; } }
        internal static int ReloadDisplayCount { get { return Displays.Count; } }
        internal static Action SuspendForReload()
        {
            Dictionary<int, EnemyRendererMesh[]> models = Models, displays = Displays;
            Models = new Dictionary<int, EnemyRendererMesh[]>();
            Displays = new Dictionary<int, EnemyRendererMesh[]>();
            return delegate { Models = models; Displays = displays; };
        }
        internal static void RegisterDisplay(int id, EnemyRendererMesh[] meshes) { Displays[id] = (EnemyRendererMesh[])meshes.Clone(); }
        internal static void Register(int id, EnemyRendererMesh[] meshes) { Models[id] = (EnemyRendererMesh[])meshes.Clone(); }
        internal static void Apply(FTK_itembase.ID id, GameObject instance)
        {
            Apply(id, instance, Models, "item:");
        }
        internal static void ApplyDisplay(FTK_itembase.ID id, GameObject instance)
        {
            Apply(id, instance, Displays, "item-display:");
        }
        private static void Apply(FTK_itembase.ID id, GameObject instance,
            Dictionary<int, EnemyRendererMesh[]> registry, string scope)
        {
            EnemyRendererMesh[] assignments;
            if (instance == null || !registry.TryGetValue((int)id, out assignments)) return;
            try
            {
                if (!ExplicitEnemyMeshSwap.ApplyToObject(scope + (int)id, instance, assignments, null, true))
                    Plugin.Log.LogError("[item-model] custom equipment mesh failed for " + id + "; native fallback is not art acceptance.");
            }
            catch (Exception e) { Plugin.Log.LogError("[item-model] " + id + ": " + e); }
        }
    }

    // FTKHub returns new detached instances. Never patch cached prefab templates.
    [HarmonyPatch(typeof(FTKHub), "CreateWeapon", new Type[] { typeof(FTK_itembase.ID) })]
    internal static class ItemWeaponModelPatch
    {
        private static void Postfix(FTK_itembase.ID _weaponID, GameObject __result) { ItemModelRegistry.Apply(_weaponID, __result); }
    }
    [HarmonyPatch(typeof(FTKHub), "CreateShield")]
    internal static class ItemShieldModelPatch
    {
        private static void Postfix(FTK_itembase.ID _shieldID, GameObject __result) { ItemModelRegistry.Apply(_shieldID, __result); }
    }
    [HarmonyPatch(typeof(FTKHub), "CreateHelmet")]
    internal static class ItemHelmetModelPatch
    {
        private static void Postfix(FTK_itembase.ID _helmetID, GameObject __result) { ItemModelRegistry.Apply(_helmetID, __result); }
    }

    // Inventory and shop cards render this fresh loot clone through OffscreenCamera. Icon
    // sprites only cover the small UI slots; they cannot replace this native 3D preview.
    [HarmonyPatch(typeof(FTKHub), "CreateLootDisplayObject", new Type[] { typeof(FTK_itembase.ID) })]
    internal static class ItemLootDisplayModelPatch
    {
        internal static void Postfix(FTK_itembase.ID _item, Transform __result)
        {
            if (__result != null) ItemModelRegistry.ApplyDisplay(_item, __result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Weapon), "Break")]
    internal static class ItemBreakModelLeasePatch
    {
        private static void Prefix(Weapon __instance)
        {
            try
            {
                EnemyMeshResources owner = __instance.GetComponent<EnemyMeshResources>();
                if (owner == null) return;
                foreach (Renderer renderer in __instance.GetComponentsInChildren<Renderer>(true))
                    if (owner.Owns(renderer) && !owner.RetainForDetachedRenderer(renderer))
                        Plugin.Log.LogError("[item-model] could not retain detached renderer " + renderer.name);
            }
            catch (Exception e) { Plugin.Log.LogError("[item-model] break resource retention: " + e); }
        }
    }
}
