using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(CharacterDummy), "CreateAvatar", new Type[] { typeof(bool) })]
    internal static class PlayerCombatMeshLeasePatch
    {
        // Retain at the native assignment, before SetVisible and the remaining initialization can
        // throw. A Harmony finalizer on this method regresses combat entry on the shipped Mono.
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            FieldInfo listener = typeof(CharacterDummy).GetField("m_EventListener");
            FieldInfo cow = typeof(CharacterDummy).GetField("m_CharacterOverworld");
            FieldInfo avatar = typeof(CharacterOverworld).GetField("m_Avatar");
            MethodInfo retain = typeof(PlayerCombatMeshLeasePatch).GetMethod("RetainClone", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (CodeInstruction instruction in code)
                if (instruction.opcode == OpCodes.Call && object.Equals(instruction.operand, retain)) return code;
            int match = -1;
            for (int i = 5; i < code.Count; i++)
            {
                MethodInfo instantiate = code[i - 1].operand as MethodInfo;
                if (code[i].opcode != OpCodes.Stfld || !object.Equals(code[i].operand, listener) ||
                    code[i - 1].opcode != OpCodes.Call || instantiate == null ||
                    instantiate.DeclaringType != typeof(UnityEngine.Object) || instantiate.Name != "Instantiate" ||
                    !instantiate.IsGenericMethod || instantiate.ReturnType != typeof(CharacterEventListener) ||
                    instantiate.GetParameters().Length != 1 ||
                    code[i - 2].opcode != OpCodes.Ldfld || !object.Equals(code[i - 2].operand, avatar) ||
                    code[i - 3].opcode != OpCodes.Ldfld || !object.Equals(code[i - 3].operand, cow) ||
                    code[i - 4].opcode != OpCodes.Ldarg_0 || code[i - 5].opcode != OpCodes.Ldarg_0) continue;
                if (match >= 0) throw new InvalidOperationException("Ambiguous native combat avatar clone assignment");
                match = i;
            }
            if (match < 0) throw new InvalidOperationException("Native combat avatar clone assignment unavailable");
            code.Insert(match + 1, new CodeInstruction(OpCodes.Ldarg_0));
            code.Insert(match + 2, new CodeInstruction(OpCodes.Call, retain));
            return code;
        }

        private static void RetainClone(CharacterDummy dummy)
        {
            try
            {
                if (dummy != null && dummy.m_CharacterOverworld != null && dummy.m_EventListener != null)
                    EnemyMeshResources.RetainHierarchy(dummy.m_EventListener.gameObject);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[player-mesh] combat lease hook: " + e.Message); }
        }
    }
}
