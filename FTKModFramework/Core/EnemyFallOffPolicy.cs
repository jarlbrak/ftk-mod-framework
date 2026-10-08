using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
    [HarmonyPatch(typeof(CharacterEventListener), "DeathFallOff", new Type[0])]
    internal static class EnemyFallOffPatch
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo native = typeof(FallOffLimb).GetMethod("FallOff", new Type[] { typeof(Vector3) });
            int match = -1;
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if ((code[i].opcode == OpCodes.Callvirt || code[i].opcode == OpCodes.Call) &&
                    native.Equals(code[i].operand))
                {
                    match = i;
                    count++;
                }
            }
            if (count != 1)
            {
                Plugin.Log.LogWarning("[enemy-falloff] exact death call site unavailable; native fall-off retained.");
                return code;
            }

            // Keep the native Vector3 method unpatched on the shipped Mono runtime.
            // The static call consumes the same limb and direction, retaining branch and exception metadata.
            code[match].opcode = OpCodes.Call;
            code[match].operand = typeof(EnemyFallOffPatch).GetMethod("FallOff", BindingFlags.Static | BindingFlags.NonPublic);
            return code;
        }

        internal static void FallOff(FallOffLimb limb, Vector3 direction)
        {
            bool preserve = false;
            try
            {
                EnemyFallOffMarker marker = limb == null ? null : limb.GetComponent<EnemyFallOffMarker>();
                preserve = marker != null && marker.Matches(limb);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[enemy-falloff] native fall-off retained after guard error: " + e.Message);
            }
            // Native failures must retain their original exception behavior.
            if (!preserve) limb.FallOff(direction);
        }
    }
}
