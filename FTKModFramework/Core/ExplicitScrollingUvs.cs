using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Exact native ScrollingUVs semantics for opted-in owned material slots, without getter-created unowned copies.</summary>
    [HarmonyPatch(typeof(ScrollingUVs), "LateUpdate")]
    internal static class ExplicitScrollingUvs
    {
        private static readonly FieldInfo Offset = typeof(ScrollingUVs).GetField("uvOffset", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static void Validate(ScrollingUVs scroller, Material[] materials)
        {
            if (Offset == null || Offset.FieldType != typeof(Vector2) || scroller.GetType() != typeof(ScrollingUVs) ||
                scroller.materialIndex < 0 || scroller.materialIndex >= materials.Length ||
                string.IsNullOrEmpty(scroller.textureName) || !materials[scroller.materialIndex].HasProperty(scroller.textureName))
                throw new InvalidOperationException("Unsupported native ScrollingUVs slot/property contract");
        }

        internal static bool Prefix(ScrollingUVs __instance)
        {
            Renderer renderer = __instance.GetComponent<Renderer>();
            if (renderer == null) return true;
            EnemyMeshResources owner = renderer.GetComponentInParent<EnemyMeshResources>();
            if (owner == null || !owner.EnsureRetained() || !owner.OwnsScroller(__instance)) return true;
            Material[] materials;
            try
            {
                materials = owner.ScrollingMaterials(renderer, false);
                if (materials == null) throw new InvalidOperationException("Assigned scrolling materials changed; unknown materials are not adopted");
                Validate(__instance, materials);
                if (renderer.enabled) materials = owner.ScrollingMaterials(renderer, true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[model-mesh] scrolling compatibility left native: " + e.Message);
                return true; // No phase update performed; native behavior remains responsible for this mismatched target.
            }
            // Native method accumulates even when the renderer is disabled. Component scheduling remains native.
            Vector2 offset = (Vector2)Offset.GetValue(__instance);
            offset += __instance.uvAnimationRate * Time.deltaTime;
            Offset.SetValue(__instance, offset);
            if (renderer.enabled) materials[__instance.materialIndex].SetTextureOffset(__instance.textureName, offset);
            return false;
        }
    }
}
