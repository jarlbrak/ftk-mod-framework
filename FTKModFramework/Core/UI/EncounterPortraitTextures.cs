using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    // Native None portraits immediately replace their new texture with a shared unknown image.
    // Skip only that discarded allocation. Known enemies, including Deimos, retain the native path.
    [HarmonyPatch(typeof(uiEnemyEncounterPortrait), "Initialize", new[] { typeof(string) })]
    internal static class UnknownEncounterPortraitAllocationPatch
    {
        internal static bool Enabled;
        private static bool warned;
        private static bool Prepare() { return Enabled; }

        [HarmonyPriority(Priority.Last)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase __originalMethod)
        {
            List<CodeInstruction> input = new List<CodeInstruction>(instructions), result;
            if (TryRewrite(input, generator, __originalMethod, out result)) return result;
            if (!warned)
            {
                warned = true;
                Plugin.Log.LogWarning("Unknown encounter portrait optimization skipped: unsupported instructions; native allocation retained.");
            }
            return input;
        }

        internal static bool TryRewrite(List<CodeInstruction> input, ILGenerator generator, MethodBase method,
            out List<CodeInstruction> result)
        {
            result = input;
            if (method != AccessTools.Method(typeof(uiEnemyEncounterPortrait), "Initialize", new[] { typeof(string) })) return false;
            MethodBody body = method.GetMethodBody();
            if (body == null) return false;
            Type[] locals = new Type[body.LocalVariables.Count];
            for (int i = 0; i < locals.Length; i++) locals[i] = body.LocalVariables[i].LocalType;
            int start;
            if (!FindAllocation(input, locals, out start)) return false;
            Label after = generator.DefineLabel();
            result = new List<CodeInstruction>();
            foreach (CodeInstruction instruction in input) result.Add(new CodeInstruction(instruction));
            result[start + AllocationShape().Count].labels.Add(after);
            result.InsertRange(start, new[] { new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldc_I4, (int)FTK_enemyCombat.ID.None), new CodeInstruction(OpCodes.Beq, after) });
            return true;
        }

        internal static bool FindAllocation(List<CodeInstruction> input, Type[] locals, out int start)
        {
            start = -1;
            try { return FindAllocationCore(input, locals, out start); }
            catch { start = -1; return false; }
        }

        private static bool FindAllocationCore(List<CodeInstruction> input, Type[] locals, out int start)
        {
            start = -1;
            if (locals.Length < 4 || locals[0] != typeof(FTK_enemyCombat.ID) || locals[1] != typeof(RawImage) ||
                locals[2] != typeof(Rect) || locals[3] != typeof(Rect)) return false;
            if (input.Count < 3 || input[0].opcode != OpCodes.Ldarg_1 || input[1].opcode != OpCodes.Call ||
                !Equals(input[1].operand, AccessTools.Method(typeof(FTK_enemyCombat), "GetEnum", new[] { typeof(string) })) ||
                input[2].opcode != OpCodes.Stloc_0) return false;
            List<CodeInstruction> shape = AllocationShape();
            for (int i = 3; i + shape.Count + 2 < input.Count; i++)
            {
                if (!Matches(input, i, shape)) continue;
                if (start != -1) return false;
                start = i;
            }
            if (start < 4) return false;
            if (input[start - 4].opcode != OpCodes.Ldarg_0 || input[start - 3].opcode != OpCodes.Ldfld ||
                !Equals(input[start - 3].operand, AccessTools.Field(typeof(uiEnemyEncounterPortrait), "m_Portrait")) ||
                input[start - 2].opcode != OpCodes.Ldfld ||
                !Equals(input[start - 2].operand, AccessTools.Field(typeof(uiActiveTimePortrait), "m_RawImage")) ||
                input[start - 1].opcode != OpCodes.Stloc_1) return false;
            int end = start + shape.Count;
            if (input[end].opcode != OpCodes.Ldloc_0 || input[end + 1].opcode != OpCodes.Ldc_I4_M1 ||
                input[end + 2].opcode != OpCodes.Beq) return false;
            for (int i = 0; i < input.Count; i++)
            {
                if (input[i].blocks.Count != 0) return false;
                if (i >= start && i < end)
                { if (input[i].labels.Count != 0) return false; continue; }
                int local = Local(input[i]);
                // Skipped rectangle stores must have no observers outside the matched fragment.
                if (local == 2 || local == 3) return false;
                if (i > 2 && i < start && local == 0 && input[i].opcode != OpCodes.Ldloc_0) return false;
            }
            return true;
        }

        private static bool Matches(List<CodeInstruction> input, int start, List<CodeInstruction> expected)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                CodeInstruction a = input[start + i], b = expected[i];
                if (a.opcode != b.opcode) return false;
                if (b.opcode == OpCodes.Ldloca_S)
                { if (Local(a) != (int)b.operand) return false; }
                else if (!Equals(a.operand, b.operand)) return false;
            }
            return true;
        }
        private static int Local(CodeInstruction instruction)
        {
            OpCode op = instruction.opcode;
            if (op == OpCodes.Ldloc_0 || op == OpCodes.Stloc_0) return 0;
            if (op == OpCodes.Ldloc_1 || op == OpCodes.Stloc_1) return 1;
            if (op == OpCodes.Ldloc_2 || op == OpCodes.Stloc_2) return 2;
            if (op == OpCodes.Ldloc_3 || op == OpCodes.Stloc_3) return 3;
            if (op != OpCodes.Ldloc && op != OpCodes.Ldloc_S && op != OpCodes.Stloc && op != OpCodes.Stloc_S &&
                op != OpCodes.Ldloca && op != OpCodes.Ldloca_S) return -1;
            LocalBuilder builder = instruction.operand as LocalBuilder;
            if (builder != null) return builder.LocalIndex;
            LocalVariableInfo variable = instruction.operand as LocalVariableInfo;
            return variable == null ? Convert.ToInt32(instruction.operand) : variable.LocalIndex;
        }
        internal static List<CodeInstruction> AllocationShape()
        {
            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldloc_1), new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Graphic), "rectTransform")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RectTransform), "rect")),
                new CodeInstruction(OpCodes.Stloc_2), new CodeInstruction(OpCodes.Ldloca_S, 2),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rect), "width")), new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(uiActiveTime), "Instance")),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(uiActiveTime), "m_OffscreenEnemyPortraitAA")), new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Graphic), "rectTransform")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RectTransform), "rect")),
                new CodeInstruction(OpCodes.Stloc_3), new CodeInstruction(OpCodes.Ldloca_S, 3),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rect), "height")), new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(uiActiveTime), "Instance")),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(uiActiveTime), "m_OffscreenEnemyPortraitAA")), new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Ldc_I4_5), new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(Texture2D), new[] {
                    typeof(int), typeof(int), typeof(TextureFormat), typeof(bool), typeof(bool) })),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(RawImage), "texture")),
                new CodeInstruction(OpCodes.Ldloc_1), new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RawImage), "texture")),
                new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Texture), "wrapMode")) };
        }
    }
}
