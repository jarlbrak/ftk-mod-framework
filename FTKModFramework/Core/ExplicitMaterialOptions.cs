using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Options applied only to transaction-owned replacement material copies.</summary>
    internal static class ExplicitMaterialOptions
    {
        private const string AuthoredMainPrefix = "ftkmf_authored_main_palette:";
        internal static bool HasAuthoredMainPalette(Material material)
        {
            return material.name != null && material.name.StartsWith(AuthoredMainPrefix, System.StringComparison.Ordinal);
        }
        internal static void PreserveMainPalette(Material material)
        {
            if (material.name == null || !material.name.Contains("_main")) return;
            // Only called for explicit player/equipment texture slots. The marker survives
            // native avatar clones and owned material privatization without another registry.
            if (!HasAuthoredMainPalette(material)) material.name = AuthoredMainPrefix + material.name;
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        }

        internal static void Apply(Material privateMaterial, bool disableNativeEmission)
        {
            if (!disableNativeEmission) return;
            privateMaterial.DisableKeyword("_EMISSION");
            if (privateMaterial.HasProperty("_EmissionColor")) privateMaterial.SetColor("_EmissionColor", Color.black);
            if (privateMaterial.HasProperty("_EmissionMap")) privateMaterial.SetTexture("_EmissionMap", null);
        }
    }
}
