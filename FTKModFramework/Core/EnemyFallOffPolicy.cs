using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Controls the native <see cref="FallOffLimb"/> hand-off for a successfully explicitly assigned enemy body.
    /// The default preserves every native fall-off behavior.
    /// </summary>
    public enum EnemyFallOffPolicy
    {
        PreserveNative = 0,
        PreserveCustomBody = 1
    }

    /// <summary>
    /// CEL-local proof that one exact native fall-off renderer received a live explicit mesh lease.
    /// It is configured only after the all-or-nothing renderer transaction commits.
    /// </summary>
    internal sealed class EnemyFallOffMarker : MonoBehaviour
    {
        [NonSerialized] private EnemyFallOffPolicy _policy;
        [NonSerialized] private SkinnedMeshRenderer _renderer;

        internal static void Install(CharacterEventListener cel, EnemyFallOffPolicy policy)
        {
            if (cel == null || policy != EnemyFallOffPolicy.PreserveCustomBody) return;

            FallOffLimb fallOff = cel.GetComponent<FallOffLimb>();
            EnemyMeshResources resources = cel.GetComponent<EnemyMeshResources>();
            if (fallOff == null || fallOff.m_Renderer == null || resources == null || !resources.Applied ||
                resources.VisualResourcesOnly || !resources.ValidLease() || !resources.Owns(fallOff.m_Renderer))
            {
                Plugin.Log.LogInfo("[enemy-falloff] preserve-custom-body unavailable on this explicit clone; native fall-off retained.");
                return;
            }

            EnemyFallOffMarker marker = cel.GetComponent<EnemyFallOffMarker>();
            if (marker == null) marker = cel.gameObject.AddComponent<EnemyFallOffMarker>();
            marker._policy = policy;
            marker._renderer = fallOff.m_Renderer;
        }

        internal bool Matches(FallOffLimb fallOff)
        {
            if (_policy != EnemyFallOffPolicy.PreserveCustomBody || fallOff == null || fallOff.m_Renderer == null ||
                _renderer != fallOff.m_Renderer) return false;

            CharacterEventListener cel = GetComponent<CharacterEventListener>();
            EnemyMeshResources resources = cel == null ? null : cel.GetComponent<EnemyMeshResources>();
            return resources != null && resources.Applied && !resources.VisualResourcesOnly && resources.ValidLease() &&
                resources.Owns(_renderer);
        }
    }

    /// <summary>
    /// Suppresses only FallOffLimb's custom-body hand-off after a verified explicit body assignment.
    /// CharacterEventListener's remaining death cleanup and native ragdoll handling still run.
    /// </summary>
    [HarmonyPatch(typeof(FallOffLimb), "FallOff", new Type[] { typeof(Vector3) })]
    internal static class EnemyFallOffPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(FallOffLimb __instance)
        {
            try
            {
                EnemyFallOffMarker marker = __instance == null ? null : __instance.GetComponent<EnemyFallOffMarker>();
                return marker == null || !marker.Matches(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[enemy-falloff] native fall-off retained after guard error: " + e.Message);
                return true;
            }
        }
    }
}
