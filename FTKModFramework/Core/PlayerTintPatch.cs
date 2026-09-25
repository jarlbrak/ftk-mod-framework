using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(CharacterEventListener), "SetTintColor")]
    internal static class PlayerTintPatch
    {
        private static EnemyMeshResources Owner(Renderer renderer)
        {
            for (Transform node = renderer.transform; node != null; node = node.parent)
            {
                EnemyMeshResources owner = node.GetComponent<EnemyMeshResources>();
                if (owner != null && owner.Owns(renderer)) return owner;
            }
            return null;
        }

        internal static bool Prefix(CharacterEventListener __instance)
        {
            Renderer[] renderers = __instance.GetComponentsInChildren<Renderer>(true);
            bool custom = false;
            foreach (Renderer renderer in renderers) if (Owner(renderer) != null) { custom = true; break; }
            if (!custom) return true;

            Color main, skin, hair;
            if (__instance.m_CharacterOverworld != null)
            {
                main = __instance.m_CharacterOverworld.m_CharacterStats.m_ColorMain;
                skin = __instance.m_CharacterOverworld.m_CharacterStats.m_ColorSkin;
                hair = __instance.m_CharacterOverworld.m_CharacterStats.m_ColorHair;
            }
            else
            {
                main = __instance.m_uiQuickPlayerCreate.m_ImageColorMain.color;
                skin = __instance.m_uiQuickPlayerCreate.m_ImageColorSkin.color;
                hair = __instance.m_uiQuickPlayerCreate.m_ImageColorHair.color;
            }
            foreach (Renderer renderer in renderers)
            {
                EnemyMeshResources owner = Owner(renderer);
                try
                {
                    // Native .materials creates implicit copies. Explicit targets must instead use
                    // tracked per-avatar copies, including items tinted before the body swap.
                    Material[] materials = owner == null ? renderer.materials : owner.PrivateMaterials(renderer, true);
                    if (materials == null) throw new InvalidOperationException("Tint material provenance changed");
                    foreach (Material material in materials)
                    {
                        if (owner != null && ExplicitMaterialOptions.HasAuthoredPalette(material)) continue;
                        string name = material.name;
                        if (name.Contains("_main")) material.SetColor("_Color", main);
                        else if (name.Contains("_skin")) material.SetColor("_Color", skin);
                        else if (name.Contains("_hair")) material.SetColor("_Color", hair);
                    }
                }
                catch (Exception e)
                {
                    // Unknown custom references are never adopted or implicitly cloned on failure.
                    Plugin.Log.LogWarning("[model-mesh] tint skipped incompatible renderer: " + e.Message);
                }
            }
            return false;
        }
    }
}
