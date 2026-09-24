using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core.Performance
{
    [HarmonyPatch]
    internal static class WaterMeshPatch
    {
        private static bool warned;
        private static bool Prepare() { return WaterMeshPerformance.Enabled; }
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WaterDistort), "OnWillRenderObject");
            yield return AccessTools.Method(typeof(LakeDistort), "OnWillRenderObject");
        }
        // Inspect earlier transforms. A later or equally prioritized third-party transform can still conflict.
        [HarmonyPriority(Priority.Last)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            List<CodeInstruction> incoming = new List<CodeInstruction>(instructions);
            List<CodeInstruction> rewritten;
            if (TryRewrite(incoming, generator, __originalMethod, out rewritten)) return rewritten;
            if (!warned)
            {
                warned = true;
                Plugin.Log.LogWarning("Water mesh optimization skipped: an unsupported method body was found; original instructions retained.");
            }
            return incoming;
        }

        internal static bool TryRewrite(List<CodeInstruction> input, ILGenerator generator, MethodBase method, out List<CodeInstruction> result)
        {
            result = input;
            try
            {
                if (method == null || method.Name != "OnWillRenderObject" || method.GetParameters().Length != 0 ||
                    (method.DeclaringType != typeof(WaterDistort) && method.DeclaringType != typeof(LakeDistort))) return false;
                // Preflight every edit before creating labels or changing instruction objects.
                int noise = FindSingleCall(input, AccessTools.Method(typeof(Mathf), "PerlinNoise", new[] { typeof(float), typeof(float) }));
                List<CodeInstruction> noiseArguments;
                if (!MatchNoise(input, noise, method.DeclaringType, out noiseArguments)) return false;
                int first;
                int[] edges;
                int raw, normal;
                if (!MatchNormals(input, out first, out edges, out raw, out normal)) return false;
                List<CodeInstruction> code = Copy(input);
                LocalBuilder reuse = generator.DeclareLocal(typeof(bool));
                // Native Start creates instance meshes. Keep its vertex/normal writes and uploads untouched.
                // Only pure noise calls and the two redundant normal expressions are changed.
                for (int corner = 2; corner >= 1; corner--)
                {
                    int start = first + corner * 9;
                    Label original = generator.DefineLabel(), done = generator.DefineLabel();
                    code[start].labels.Add(original);
                    code[start + 8].labels.Add(done);
                    code.InsertRange(start, new[] {
                        new CodeInstruction(OpCodes.Ldloc, reuse), new CodeInstruction(OpCodes.Brfalse, original),
                        LoadLocal(normal), new CodeInstruction(OpCodes.Br, done) });
                }
                code.InsertRange(first + 9, new[] {
                    LoadLocal(edges[0]), LoadLocal(edges[1]), LoadLocal(edges[2]), LoadLocal(raw), LoadLocal(normal),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WaterFaceGuard), "CanReuse")),
                    new CodeInstruction(OpCodes.Stloc, reuse) });
                // The vertex loop precedes the normal loop; insert after the later edits retain its index.
                if (noise >= first) return false;
                code[noise].operand = AccessTools.Method(typeof(WaterNoiseCache), "Sample");
                code.InsertRange(noise, noiseArguments);
                result = code;
                return true;
            }
            catch (Exception)
            {
                // A different compiler, game build or preceding transpiler must retain its original body.
                return false;
            }
        }

        private static bool MatchNoise(List<CodeInstruction> code, int call, Type owner, out List<CodeInstruction> arguments)
        {
            arguments = null;
            if (call < 17) return false;
            int vector = Local(code[call - 12], "stloc"), index = Local(code[call - 15], "ldloc");
            FieldInfo vertices = AccessTools.Field(owner == typeof(WaterDistort) ? owner : owner.GetNestedType("MeshInfo", BindingFlags.NonPublic | BindingFlags.Public), "v");
            FieldInfo cursor = AccessTools.Field(owner, "m_PerlinCursor");
            if (vector < 0 || index < 0 || vertices == null || vertices.FieldType != typeof(Vector3[]) || cursor == null || cursor.FieldType != typeof(float)) return false;
            if (!Is(code[call - 16], owner == typeof(WaterDistort) ? OpCodes.Ldsfld : OpCodes.Ldfld, vertices) ||
                !Is(code[call - 14], OpCodes.Ldelema, typeof(Vector3)) || !Is(code[call - 13], OpCodes.Ldobj, typeof(Vector3)) ||
                Local(code[call - 11], "ldloca") != vector || Local(code[call - 10], "ldloca") != vector ||
                !Is(code[call - 9], OpCodes.Ldfld, AccessTools.Field(typeof(Vector3), "x")) || code[call - 8].opcode != OpCodes.Ldarg_0 ||
                !Is(code[call - 7], OpCodes.Ldfld, cursor) || code[call - 6].opcode != OpCodes.Mul ||
                Local(code[call - 5], "ldloca") != vector || !Is(code[call - 4], OpCodes.Ldfld, AccessTools.Field(typeof(Vector3), "z")) ||
                code[call - 3].opcode != OpCodes.Ldarg_0 || !Is(code[call - 2], OpCodes.Ldfld, cursor) || code[call - 1].opcode != OpCodes.Mul) return false;
            for (int i = call - 15; i <= call; i++) if (HasBoundary(code[i])) return false;
            arguments = new List<CodeInstruction>();
            if (owner == typeof(LakeDistort))
            {
                int meshInfo = Local(code[call - 17], "ldloc");
                if (meshInfo < 0 || HasBoundary(code[call - 16])) return false;
                arguments.Add(LoadLocal(meshInfo));
            }
            arguments.Add(new CodeInstruction(owner == typeof(WaterDistort) ? OpCodes.Ldsfld : OpCodes.Ldfld, vertices));
            arguments.Add(LoadLocal(index));
            return true;
        }

        private static bool MatchNormals(List<CodeInstruction> code, out int first, out int[] edges, out int raw, out int normal)
        {
            first = -1; edges = null; raw = normal = -1;
            MethodInfo cross = AccessTools.Method(typeof(Vector3), "Cross", new[] { typeof(Vector3), typeof(Vector3) });
            MethodInfo normalize = AccessTools.PropertyGetter(typeof(Vector3), "normalized");
            MethodInfo multiply = AccessTools.Method(typeof(Vector3), "op_Multiply", new[] { typeof(Vector3), typeof(float) });
            MethodInfo subtract = AccessTools.Method(typeof(Vector3), "op_Subtraction", new[] { typeof(Vector3), typeof(Vector3) });
            List<int> sites = new List<int>();
            for (int i = 0; i < code.Count; i++) if (code[i].Calls(cross)) sites.Add(i - 2);
            if (sites.Count != 3 || sites[0] < 12 || sites[1] != sites[0] + 9 || sites[2] != sites[1] + 9) return false;
            first = sites[0];
            for (int corner = 0; corner < 3; corner++)
            {
                int start = sites[corner];
                if (start + 8 >= code.Count || Local(code[start], "ldloc") < 0 || Local(code[start + 1], "ldloc") < 0 ||
                    Local(code[start + 3], "stloc") < 0 || Local(code[start + 4], "ldloca") != Local(code[start + 3], "stloc") ||
                    !code[start + 5].Calls(normalize) || !Is(code[start + 6], OpCodes.Ldc_R4, -1f) ||
                    !code[start + 7].Calls(multiply) || Local(code[start + 8], "stloc") < 0) return false;
                for (int i = start; i <= start + 8; i++) if (HasBoundary(code[i])) return false;
            }
            edges = new[] { Local(code[first + 1], "ldloc"), Local(code[first], "ldloc"), Local(code[first + 9], "ldloc") };
            if (edges[0] == edges[1] || edges[0] == edges[2] || edges[1] == edges[2] ||
                Local(code[first + 10], "ldloc") != edges[1] || Local(code[first + 18], "ldloc") != edges[0] || Local(code[first + 19], "ldloc") != edges[2]) return false;
            int[] vertices = new int[3];
            for (int edge = 0; edge < 3; edge++)
            {
                int start = first - 12 + edge * 4;
                if (Local(code[start], "ldloc") < 0 || Local(code[start + 1], "ldloc") < 0 ||
                    !code[start + 2].Calls(subtract) || Local(code[start + 3], "stloc") != edges[edge]) return false;
                vertices[edge] = Local(code[start + 1], "ldloc");
                for (int i = start; i <= start + 3; i++) if (HasBoundary(code[i])) return false;
            }
            for (int edge = 0; edge < 3; edge++)
                if (Local(code[first - 12 + edge * 4], "ldloc") != vertices[(edge + 1) % 3]) return false;
            // Skipping a temporary's store is safe only when no other instruction observes that local.
            // This also rejects extra uses introduced by an earlier transpiler.
            for (int corner = 1; corner < 3; corner++)
            {
                int start = sites[corner], temporary = Local(code[start + 3], "stloc");
                for (int i = 0; i < code.Count; i++)
                {
                    if (i == start + 3 || i == start + 4) continue;
                    if (Local(code[i], "ldloc") == temporary || Local(code[i], "ldloca") == temporary || Local(code[i], "stloc") == temporary) return false;
                }
            }
            raw = Local(code[first + 3], "stloc"); normal = Local(code[first + 8], "stloc");
            // No local used by an earlier expression may be overwritten by a later expression.
            HashSet<int> locals = new HashSet<int>();
            foreach (int local in vertices) if (!locals.Add(local)) return false;
            foreach (int local in edges) if (!locals.Add(local)) return false;
            for (int corner = 0; corner < 3; corner++)
            {
                if (!locals.Add(Local(code[sites[corner] + 3], "stloc")) || !locals.Add(Local(code[sites[corner] + 8], "stloc"))) return false;
            }
            return true;
        }
        private static int FindSingleCall(List<CodeInstruction> code, MethodInfo method)
        {
            int found = -1;
            for (int i = 0; i < code.Count; i++) if (code[i].Calls(method)) { if (found >= 0) return -1; found = i; }
            return found;
        }
        private static bool Is(CodeInstruction code, OpCode opcode, object operand) { return code.opcode == opcode && Equals(code.operand, operand); }
        private static bool HasBoundary(CodeInstruction code) { return code.labels.Count != 0 || code.blocks.Count != 0; }
        private static CodeInstruction LoadLocal(int index) { return new CodeInstruction(OpCodes.Ldloc, index); }
        private static int Local(CodeInstruction code, string operation)
        {
            string name = code.opcode.Name;
            if (name == operation || name == operation + ".s")
            {
                LocalBuilder builder = code.operand as LocalBuilder;
                if (builder != null) return builder.LocalIndex;
                LocalVariableInfo variable = code.operand as LocalVariableInfo;
                if (variable != null) return variable.LocalIndex;
                if (code.operand is int) return (int)code.operand;
                if (code.operand is byte) return (byte)code.operand;
                if (code.operand is short) return (short)code.operand;
            }
            if (name.Length == operation.Length + 2 && name.StartsWith(operation + ".") && name[name.Length - 1] >= '0' && name[name.Length - 1] <= '3') return name[name.Length - 1] - '0';
            return -1;
        }
        private static List<CodeInstruction> Copy(List<CodeInstruction> original)
        {
            List<CodeInstruction> result = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original)
            {
                CodeInstruction copy = new CodeInstruction(instruction.opcode, instruction.operand);
                copy.labels.AddRange(instruction.labels); copy.blocks.AddRange(instruction.blocks); result.Add(copy);
            }
            return result;
        }
    }
}
