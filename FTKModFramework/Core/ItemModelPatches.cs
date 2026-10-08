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
        private static Dictionary<int, EnemyRendererMesh[]> OffHands = new Dictionary<int, EnemyRendererMesh[]>();
        private static Dictionary<int, EnemyRendererMesh[]> Displays = new Dictionary<int, EnemyRendererMesh[]>();
        private static Dictionary<int, bool[]> HelmetHair = new Dictionary<int, bool[]>();
        internal static bool SupportsHelmetHairVisibility(FTK_itembase item)
        {
            FTK_items helmet = item as FTK_items;
            // The attached helmet hook also applies policy to direct wearable-prefab instances.
            return helmet != null && helmet.m_ObjectType == FTK_itembase.ObjectType.helmet;
        }
        internal static IEnumerable<int> ReloadHelmetHairItems() { return HelmetHair.Keys; }
        internal static void RegisterHelmetHairVisibility(int id, bool top, bool bottom)
        { HelmetHair[id] = new[] { top, bottom }; }
        internal static void ApplyHelmetHairVisibility(FTK_itembase.ID id, GameObject instance)
        {
            bool[] visibility;
            if (instance == null || !HelmetHair.TryGetValue((int)id, out visibility)) return;
            Helmet helmet = instance.GetComponent<Helmet>();
            if (helmet == null) return;
            helmet.m_IsHairTopOn = visibility[0];
            helmet.m_IsHairBottomOn = visibility[1];
        }
        internal static void ApplyAttachedHelmetHairVisibility(FTK_itembase.ID id, CharacterEventListener avatar)
        {
            bool[] visibility;
            if (avatar == null || avatar.m_Helmet == null ||
                !HelmetHair.TryGetValue((int)id, out visibility)) return;
            avatar.m_Helmet.m_IsHairTopOn = visibility[0];
            avatar.m_Helmet.m_IsHairBottomOn = visibility[1];
            if (avatar.m_TargetHairTop != null) avatar.m_TargetHairTop.gameObject.SetActive(visibility[0]);
            if (avatar.m_TargetHairBottom != null) avatar.m_TargetHairBottom.gameObject.SetActive(visibility[1]);
        }
        internal static IEnumerable<KeyValuePair<int, EnemyRendererMesh[]>> ReloadPlans(bool display)
        { return display ? Displays : Models; }
        internal static IEnumerable<KeyValuePair<int, EnemyRendererMesh[]>> ReloadOffHandPlans()
        { return OffHands; }
        internal static int ReloadModelCount { get { return Models.Count; } }
        internal static int ReloadOffHandCount { get { return OffHands.Count; } }
        internal static int ReloadDisplayCount { get { return Displays.Count; } }
        internal static Action SuspendForReload()
        {
            Dictionary<int, EnemyRendererMesh[]> models = Models, offHands = OffHands, displays = Displays;
            Dictionary<int, bool[]> helmetHair = HelmetHair;
            HelmetHair = new Dictionary<int, bool[]>();
            Models = new Dictionary<int, EnemyRendererMesh[]>();
            OffHands = new Dictionary<int, EnemyRendererMesh[]>();
            Displays = new Dictionary<int, EnemyRendererMesh[]>();
            return delegate { Models = models; OffHands = offHands; Displays = displays; HelmetHair = helmetHair; };
        }
        internal static void RegisterDisplay(int id, EnemyRendererMesh[] meshes) { Displays[id] = (EnemyRendererMesh[])meshes.Clone(); }
        internal static void RegisterOffHand(int id, EnemyRendererMesh[] meshes) { OffHands[id] = (EnemyRendererMesh[])meshes.Clone(); }
        internal static void Register(int id, EnemyRendererMesh[] meshes) { Models[id] = (EnemyRendererMesh[])meshes.Clone(); }
        internal static bool TryGetHeadFallbackPath(int id, out string path)
        {
            path = null;
            EnemyRendererMesh[] models;
            if (!Models.TryGetValue(id, out models) || models == null || models.Length != 1 ||
                models[0] == null || models[0].RendererKind != EnemyRendererKind.MeshRenderer) return false;
            path = models[0].RendererPath;
            return true;
        }
        internal static void Apply(FTK_itembase.ID id, GameObject instance)
        {
            Apply(id, instance, Models, "item:");
        }
        internal static void ApplyOffHand(FTK_itembase.ID id, GameObject instance)
        {
            Apply(id, instance, OffHands, "item-offhand:");
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
        private static void Postfix(FTK_itembase.ID _weaponID, GameObject __result)
        {
            ItemModelRegistry.Apply(_weaponID, __result);
            Weapon weapon = __result == null ? null : __result.GetComponent<Weapon>();
            if (weapon != null) ItemModelRegistry.ApplyOffHand(_weaponID, weapon.m_OffHand);
        }
    }
    [HarmonyPatch(typeof(FTKHub), "CreateShield")]
    internal static class ItemShieldModelPatch
    {
        private static void Postfix(FTK_itembase.ID _shieldID, GameObject __result) { ItemModelRegistry.Apply(_shieldID, __result); }
    }
    [HarmonyPatch(typeof(FTKHub), "CreateHelmet")]
    internal static class ItemHelmetModelPatch
    {
        internal static void Postfix(FTK_itembase.ID _helmetID, GameObject __result)
        {
            if (!ItemHeadProfileRegistry.HasProfiles((int)_helmetID))
                ItemModelRegistry.Apply(_helmetID, __result);
            ItemModelRegistry.ApplyHelmetHairVisibility(_helmetID, __result);
        }
    }

    // UpdateHelmet creates a fresh attached instance, including the direct wearable-prefab route.
    [HarmonyPatch(typeof(CharacterEventListener), "UpdateHelmet")]
    internal static class ItemHeadProfileAttachedPatch
    {
        private static bool Prefix(CharacterEventListener __instance, out bool __state)
        {
            __state = false;
            try
            {
                HeadFaceResources current = __instance == null ? null : __instance.GetComponent<HeadFaceResources>();
                if (current == null || current.Restore()) { __state = true; return true; }
                Plugin.Log.LogError("[head-face] helmet update blocked: prior face references diverged");
                return false;
            }
            catch (Exception e) { Plugin.Log.LogError("[head-face] helmet update blocked: " + e.Message); return false; }
        }
        private static void Postfix(CharacterEventListener __instance, bool __state)
        {
            if (!__state) return;
            try { HeadProfileCoordinator.OnHelmetUpdated(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning("[item-head-profile] attached helmet: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CharacterEventListener), "SetVisible")]
    internal static class ItemHeadProfileVisibilityPatch
    {
        private static void Postfix(CharacterEventListener __instance, bool _v)
        {
            if (!_v) return;
            try { HeadProfileCoordinator.OnHelmetUpdated(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning("[head-profile] visible avatar: " + e.Message); }
        }
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
