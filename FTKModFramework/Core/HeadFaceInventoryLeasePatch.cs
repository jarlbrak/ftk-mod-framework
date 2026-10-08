using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // The installed method stores Instantiate(_avatar) in local 0 before fallible animator setup.
    // Retain immediately after that store, including clones whose later initialization throws.
    [HarmonyPatch(typeof(OffscreenCamera), "InstantiateTarget",
        new Type[] { typeof(CharacterEventListener), typeof(CharacterEventListener.DisplayLayer), typeof(int), typeof(bool) })]
    internal static class HeadFaceInventoryLeasePatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo retain = typeof(HeadFaceInventoryLeasePatch).GetMethod("RetainClone", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (CodeInstruction instruction in code)
                if (instruction.opcode == OpCodes.Call && object.Equals(instruction.operand, retain)) return code;
            int match = -1;
            for (int i = 0; i + 1 < code.Count; i++)
            {
                MethodInfo instantiate = code[i].operand as MethodInfo;
                if (code[i].opcode != OpCodes.Call || instantiate == null ||
                    instantiate.DeclaringType != typeof(UnityEngine.Object) || instantiate.Name != "Instantiate" ||
                    !instantiate.IsGenericMethod || instantiate.ReturnType != typeof(CharacterEventListener) ||
                    instantiate.GetParameters().Length != 1 || code[i + 1].opcode != OpCodes.Stloc_0) continue;
                if (match >= 0) throw new InvalidOperationException("Ambiguous offscreen avatar clone assignment");
                match = i + 1;
            }
            if (match < 0) throw new InvalidOperationException("Native offscreen avatar clone assignment unavailable");
            code.Insert(match + 1, new CodeInstruction(OpCodes.Ldloc_0));
            code.Insert(match + 2, new CodeInstruction(OpCodes.Call, retain));
            return code;
        }

        private static void RetainClone(CharacterEventListener clone)
        {
            try
            {
                if (clone == null) return;
                EnemyMeshResources.RetainHierarchy(clone.gameObject);
                HeadFaceResources.RetainHierarchy(clone.gameObject);
                foreach (EnemyMeshResources owner in clone.GetComponentsInChildren<EnemyMeshResources>(true))
                    if (owner.HasLease && !owner.ValidLease()) throw new InvalidOperationException("Inherited model lease unavailable");
                foreach (HeadFaceResources owner in clone.GetComponentsInChildren<HeadFaceResources>(true))
                    if (owner.HasLease && !owner.Active) throw new InvalidOperationException("Inherited face lease unavailable");
            }
            catch (Exception error)
            {
                if (clone != null) UnityEngine.Object.Destroy(clone.gameObject);
                throw new InvalidOperationException("Offscreen avatar clone lease rejected", error);
            }
        }
    }
}
