using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Options applied only to transaction-owned replacement material copies.</summary>
    internal static class ExplicitMaterialOptions
    {
        internal static void Apply(Material privateMaterial, bool disableNativeEmission)
        {
            if (!disableNativeEmission) return;
            privateMaterial.DisableKeyword("_EMISSION");
            if (privateMaterial.HasProperty("_EmissionColor")) privateMaterial.SetColor("_EmissionColor", Color.black);
            if (privateMaterial.HasProperty("_EmissionMap")) privateMaterial.SetTexture("_EmissionMap", null);
        }
    }
}
