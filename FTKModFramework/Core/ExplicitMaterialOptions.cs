using System;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Options applied only to transaction-owned replacement material copies.</summary>
    internal static class ExplicitMaterialOptions
    {
        private const string AuthoredMainPrefix = "ftkmf_authored_main_palette:";
        private const string AuthoredPalettePrefix = "ftkmf_authored_palette:";
        internal static bool HasAuthoredPalette(Material material)
        {
            return HasAuthoredMainPalette(material) || (material.name != null &&
                material.name.StartsWith(AuthoredPalettePrefix, System.StringComparison.Ordinal));
        }
        internal static void PreservePalette(Material material)
        {
            // Race callers opt in only for required body renderers with an authored texture.
            // The marker survives avatar cloning and later owned-material privatization.
            if (!HasAuthoredPalette(material)) material.name = AuthoredPalettePrefix + material.name;
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        }
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

        // Missing Standard metallic workflow properties fail before renderer commit.
        internal static void ValidateMetallicGloss(Material material)
        {
            foreach (string property in new[] { "_MetallicGlossMap", "_GlossMapScale", "_SmoothnessTextureChannel" })
                if (material == null || !material.HasProperty(property))
                    throw new InvalidOperationException("Authored metallic/gloss mask requires shader property " + property);
        }

        internal static void ValidateMetallicGlossPng(byte[] bytes, out int width, out int height)
        {
            PngStructure.Validate(bytes, 16 * 1024 * 1024, 4096, out width, out height);
            // Requiring explicit RGBA prevents an RGB export from silently becoming fully smooth.
            if (bytes[24] != 8 || bytes[25] != 6)
                throw new InvalidOperationException("Metallic/gloss mask requires an 8-bit RGBA PNG (R metallic, A smoothness).");
        }

        internal static void ApplyMetallicGloss(Material privateMaterial, Texture2D texture)
        {
            ValidateMetallicGloss(privateMaterial);
            privateMaterial.SetTexture("_MetallicGlossMap", texture);
            privateMaterial.SetFloat("_GlossMapScale", 1f);
            privateMaterial.SetFloat("_SmoothnessTextureChannel", 0f);
            privateMaterial.EnableKeyword("_METALLICGLOSSMAP");
            privateMaterial.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }

        internal static void ApplyMatte(Material privateMaterial, bool matte)
        {
            if (!matte) return;
            // The swap owns this copy; native templates and unrelated renderers keep their materials.
            privateMaterial.DisableKeyword("_METALLICGLOSSMAP");
            privateMaterial.DisableKeyword("_SPECGLOSSMAP");
            privateMaterial.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (privateMaterial.HasProperty("_MetallicGlossMap")) privateMaterial.SetTexture("_MetallicGlossMap", null);
            if (privateMaterial.HasProperty("_SpecGlossMap")) privateMaterial.SetTexture("_SpecGlossMap", null);
            if (privateMaterial.HasProperty("_Metallic")) privateMaterial.SetFloat("_Metallic", 0f);
            if (privateMaterial.HasProperty("_Glossiness")) privateMaterial.SetFloat("_Glossiness", 0f);
            if (privateMaterial.HasProperty("_GlossMapScale")) privateMaterial.SetFloat("_GlossMapScale", 0f);
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
