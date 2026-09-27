using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FTKModFramework.Core.Performance;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static int assertions, accepted, rejected;
    private static double largestError;
    private static void Check(bool value, string name) { assertions++; if (!value) throw new Exception(name); }
    private static int Bits(float value) { return BitConverter.ToInt32(BitConverter.GetBytes(value), 0); }
    private static void Main()
    {
        Check(!(bool)AccessTools.Method(typeof(WaterMeshPatch), "Prepare").Invoke(null, null), "disabled feature skips patch preparation");
        WaterMeshPerformance.Initialize();
        Check((bool)AccessTools.Method(typeof(WaterMeshPatch), "Prepare").Invoke(null, null), "enabled feature prepares patches");
        CacheChecks(); GuardChecks(); TranspilerChecks(); EmittedBranchesCheck();
        Console.WriteLine("Water mesh checks passed: " + assertions + "; differential accepted=" + accepted + ", fallback=" + rejected + ", maximum component error=" + largestError.ToString("R"));
    }
    private static void CacheChecks()
    {
        Vector3[] vertices = { new Vector3(1, 0, 2), new Vector3(1, 10, 2), new Vector3(5, 0, 9) };
        WaterNoiseCache.Clear(); Mathf.Calls = 0;
        float zero = WaterNoiseCache.Sample(0, 0, vertices, 0);
        Check(Mathf.Calls == 1 && Bits(zero) == Bits(0f), "first zero arguments require evaluation");
        WaterNoiseCache.Sample(0, 0, vertices, 1);
        WaterNoiseCache.Sample(0, 0, vertices, 0);
        Check(Mathf.Calls == 1, "same arguments reused across callbacks");
        float changed = WaterNoiseCache.Sample(3, 5, vertices, 1);
        Check(Mathf.Calls == 2 && Bits(changed) == Bits(3 * .125f + 5 * .375f), "changed duplicate arguments use native");
        vertices[1].x = -12;
        WaterNoiseCache.Sample(4, 8, vertices, 1);
        Check(Mathf.Calls == 3, "in-place topology mutation is guarded");
        WaterNoiseCache.Sample(-0f, 0, vertices, 0);
        Check(Mathf.Calls == 4, "signed zero distinguished");
        float nan = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x7fc00001)), 0);
        WaterNoiseCache.Sample(nan, 0, vertices, 0); WaterNoiseCache.Sample(nan, 0, vertices, 1);
        Check(Mathf.Calls == 6, "nonfinite arguments always use native");
        float otherNan = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x7fc00002)), 0);
        WaterNoiseCache.Sample(otherNan, 0, vertices, 1);
        Check(Mathf.Calls == 7, "different NaN payloads use native");
        WaterNoiseCache.Sample(1, 2, null, 0); WaterNoiseCache.Sample(1, 2, vertices, -1); WaterNoiseCache.Sample(1, 2, vertices, 3);
        Check(Mathf.Calls == 10, "invalid cache coordinates use native");
        int beforeInfinity = Mathf.Calls;
        WaterNoiseCache.Sample(float.PositiveInfinity, 0, vertices, 0);
        WaterNoiseCache.Sample(float.PositiveInfinity, 0, vertices, 0);
        WaterNoiseCache.Sample(0, float.NegativeInfinity, vertices, 1);
        Check(Mathf.Calls == beforeInfinity + 3, "infinite arguments always use native");
        WaterNoiseCache.Clear();
        for (int i = 0; i < 150; i++) WaterNoiseCache.Sample(1, 1, new Vector3[1], 0);
        Check(WaterNoiseCache.RetainedVertices == 128, "array retention bounded");
        WaterNoiseCache.Clear();
        WaterNoiseCache.Sample(1, 1, new Vector3[WaterNoiseCache.MaximumVertices], 0);
        WaterNoiseCache.Sample(1, 1, new Vector3[10], 0);
        Check(WaterNoiseCache.RetainedVertices == WaterNoiseCache.MaximumVertices && WaterNoiseCache.RetainedBytes <= WaterNoiseCache.MaximumRetainedBytes, "vertex and byte budgets bounded");
        SceneManager.Unload();
        Check(WaterNoiseCache.RetainedVertices == 0 && WaterNoiseCache.RetainedBytes == 0, "scene unload clears retained arrays");
        WaterMeshPerformance.Shutdown(); WaterNoiseCache.Sample(1, 2, vertices, 0);
        Check(WaterNoiseCache.RetainedVertices == 0, "shutdown prevents repopulation while patches remain installed");
        Check(!WaterFaceGuard.CanReuse(new Vector3(1, 0, 0), new Vector3(-1, 0, 1), new Vector3(0, 0, -1), new Vector3(0, 1, 0), new Vector3(0, -1, 0)), "shutdown disables normal reuse");
        WaterMeshPerformance.Initialize(); WaterMeshPerformance.Initialize();
    }
    private static void GuardChecks()
    {
        Differential(new Vector3(), new Vector3(1, 0, 0), new Vector3(0, 0, 1));
        float[] unusual = { 0, -0f, 1e-30f, 1e-5f, 0.001f, 1, 1e6f, 1e20f, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        foreach (float scale in unusual)
        {
            Differential(new Vector3(), new Vector3(scale, 0, 0), new Vector3(0, 0, scale));
            Differential(new Vector3(scale, scale, scale), new Vector3(scale, scale, scale), new Vector3(scale, scale, scale));
            Differential(new Vector3(), new Vector3(1, scale, 0), new Vector3(2, scale, 0));
        }
        Random random = new Random(734251);
        for (int i = 0; i < 50000; i++)
        {
            float scale = (float)Math.Pow(10, random.NextDouble() * 16 - 8);
            Vector3 a = RandomVector(random, scale), b = RandomVector(random, scale), c = RandomVector(random, scale);
            if (i % 3 == 0) c = a + (b - a) * (float)random.NextDouble() + RandomVector(random, scale * 1e-6f);
            Differential(a, b, c);
        }
        Check(accepted > 1000 && rejected > 1000, "adversarial suite exercises reuse and fallback");
    }
    private static Vector3 RandomVector(Random random, float scale)
    {
        return new Vector3((float)(random.NextDouble() * 2 - 1) * scale, (float)(random.NextDouble() * 2 - 1) * scale, (float)(random.NextDouble() * 2 - 1) * scale);
    }
    private static void Differential(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 e0 = b - a, e1 = c - b, e2 = a - c, cross = Vector3.Cross(e1, e0);
        Vector3 first = cross.normalized * -1f;
        bool reuse = WaterFaceGuard.CanReuse(e0, e1, e2, cross, first);
        if (reuse) accepted++; else rejected++;
        Vector3[] originals = { Vector3.Cross(e2, e1).normalized * -1f, Vector3.Cross(e0, e2).normalized * -1f };
        foreach (Vector3 original in originals)
        {
            Vector3 candidate = reuse ? first : original;
            if (!reuse)
            {
                Check(Bits(candidate.x) == Bits(original.x) && Bits(candidate.y) == Bits(original.y) && Bits(candidate.z) == Bits(original.z), "fallback exact");
                continue;
            }
            double error = Math.Max(Math.Abs((double)candidate.x - original.x), Math.Max(Math.Abs((double)candidate.y - original.y), Math.Abs((double)candidate.z - original.z)));
            largestError = Math.Max(largestError, error);
            Check(!double.IsNaN(error) && !double.IsInfinity(error) && error <= 0.0001, "accepted normal error bounded by differential ceiling");
            Check((candidate.x == 0 && candidate.y == 0 && candidate.z == 0) == (original.x == 0 && original.y == 0 && original.z == 0), "zero classification preserved");
        }
    }

    private static void TranspilerChecks()
    {
        foreach (Type owner in new[] { typeof(WaterDistort), typeof(LakeDistort) })
        {
            List<CodeInstruction> source = Fixture(owner), rewritten;
            Check(Rewrite(source, owner, out rewritten) && rewritten.Count > source.Count, "supported IL accepted: " + owner.Name);
            Check(source.FindAll(c => c.labels.Count != 0).Count == 0, "successful rewrite does not mutate input labels");
            Check(rewritten.FindAll(c => c.Calls(AccessTools.Method(typeof(WaterNoiseCache), "Sample"))).Count == 1, "noise call replaced once");
            Check(rewritten.FindAll(c => c.opcode == OpCodes.Brfalse).Count == 2, "both redundant normals retain fallback branches");
            for (int mutation = 0; mutation < 9; mutation++)
            {
                List<CodeInstruction> changed = Fixture(owner);
                int cross = changed.FindIndex(c => c.Calls(AccessTools.Method(typeof(Vector3), "Cross")));
                int noise = changed.FindIndex(c => c.Calls(AccessTools.Method(typeof(Mathf), "PerlinNoise")));
                if (mutation == 0) changed[cross + 4].operand = 1f;
                if (mutation == 1) changed[cross + 7].operand = 999;
                if (mutation == 2) changed[cross - 1].operand = 20;
                if (mutation == 3) changed[cross].labels.Add(NewGenerator().DefineLabel());
                if (mutation == 4) changed[noise - 9].operand = AccessTools.Field(typeof(Vector3), "y");
                if (mutation == 5) changed[noise].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
                if (mutation >= 6)
                {
                    int raw = (int)changed[cross + 10].operand;
                    if (mutation == 6) changed.InsertRange(changed.Count - 1, new[] { L(raw), new CodeInstruction(OpCodes.Pop) });
                    if (mutation == 7) changed.InsertRange(changed.Count - 1, new[] { new CodeInstruction(OpCodes.Ldloca, raw), new CodeInstruction(OpCodes.Pop) });
                    if (mutation == 8) changed.InsertRange(changed.Count - 1, new[] { L(10), S(raw) });
                }
                Check(!Rewrite(changed, owner, out rewritten) && ReferenceEquals(rewritten, changed), "modified IL retained unchanged: " + mutation);
            }
        }
    }
    private static void EmittedBranchesCheck()
    {
        DynamicMethod method = new DynamicMethod("water_rewrite", typeof(Vector3[]), new[] { typeof(WaterDistort), typeof(Vector3), typeof(Vector3), typeof(Vector3) }, typeof(Program).Module, true);
        ILGenerator generator = method.GetILGenerator();
        generator.DeclareLocal(typeof(int));
        for (int i = 1; i < 40; i++) generator.DeclareLocal(typeof(Vector3));
        List<CodeInstruction> source = Fixture(typeof(WaterDistort));
        source.InsertRange(0, new[] {
            new CodeInstruction(OpCodes.Ldarg_1), S(10), new CodeInstruction(OpCodes.Ldarg_2), S(11), new CodeInstruction(OpCodes.Ldarg_3), S(12) });
        source.RemoveAt(source.Count - 1);
        source.Add(new CodeInstruction(OpCodes.Ldc_I4_3)); source.Add(new CodeInstruction(OpCodes.Newarr, typeof(Vector3)));
        for (int i = 0; i < 3; i++)
        {
            source.Add(new CodeInstruction(OpCodes.Dup)); source.Add(new CodeInstruction(OpCodes.Ldc_I4, i)); source.Add(L(17 + i * 2)); source.Add(new CodeInstruction(OpCodes.Stelem, typeof(Vector3)));
        }
        source.Add(new CodeInstruction(OpCodes.Ret));
        List<CodeInstruction> rewritten;
        Check(WaterMeshPatch.TryRewrite(source, generator, AccessTools.Method(typeof(WaterDistort), "OnWillRenderObject"), out rewritten), "executable fixture accepted");
        foreach (CodeInstruction instruction in rewritten)
        {
            foreach (Label label in instruction.labels) generator.MarkLabel(label);
            object operand = instruction.operand;
            if (operand == null) generator.Emit(instruction.opcode);
            else if (operand is Label) generator.Emit(instruction.opcode, (Label)operand);
            else if (operand is LocalBuilder) generator.Emit(instruction.opcode, (LocalBuilder)operand);
            else if (operand is MethodInfo) generator.Emit(instruction.opcode, (MethodInfo)operand);
            else if (operand is FieldInfo) generator.Emit(instruction.opcode, (FieldInfo)operand);
            else if (operand is Type) generator.Emit(instruction.opcode, (Type)operand);
            else if (operand is float) generator.Emit(instruction.opcode, (float)operand);
            else if (operand is int && instruction.opcode.OperandType == OperandType.InlineVar) generator.Emit(instruction.opcode, (short)(int)operand);
            else if (operand is int) generator.Emit(instruction.opcode, (int)operand);
            else throw new Exception("Unexpected fixture operand");
        }
        var run = (Func<WaterDistort, Vector3, Vector3, Vector3, Vector3[]>)method.CreateDelegate(typeof(Func<WaterDistort, Vector3, Vector3, Vector3, Vector3[]>));
        WaterDistort.v = new Vector3[1];
        Vector3 a = new Vector3(), b = new Vector3(1, .03f, 0), c = new Vector3(0, .07f, 1);
        Vector3[] optimized = run(new WaterDistort(), a, b, c);
        Check(Bits(optimized[0].x) == Bits(optimized[1].x) && Bits(optimized[0].z) == Bits(optimized[2].z), "emitted branch reuses first normal");
        WaterMeshPerformance.Shutdown();
        Vector3[] fallback = run(new WaterDistort(), a, b, c);
        Vector3 expected = Vector3.Cross(a - c, c - b).normalized * -1f;
        Check(Bits(fallback[1].x) == Bits(expected.x) && Bits(fallback[1].y) == Bits(expected.y) && Bits(fallback[1].z) == Bits(expected.z), "emitted fallback evaluates original expression");
        WaterMeshPerformance.Initialize();
    }
    private static ILGenerator NewGenerator()
    {
        ILGenerator generator = new DynamicMethod("fixture", typeof(void), Type.EmptyTypes).GetILGenerator();
        for (int i = 0; i < 40; i++) generator.DeclareLocal(typeof(Vector3));
        return generator;
    }
    private static bool Rewrite(List<CodeInstruction> source, Type owner, out List<CodeInstruction> result)
    {
        return WaterMeshPatch.TryRewrite(source, NewGenerator(), AccessTools.Method(owner, "OnWillRenderObject"), out result);
    }
    private static CodeInstruction L(int local) { return new CodeInstruction(OpCodes.Ldloc, local); }
    private static CodeInstruction S(int local) { return new CodeInstruction(OpCodes.Stloc, local); }
    private static List<CodeInstruction> Fixture(Type owner)
    {
        List<CodeInstruction> code = new List<CodeInstruction> { new CodeInstruction(OpCodes.Nop) };
        bool lake = owner == typeof(LakeDistort);
        if (lake) code.Add(L(30));
        code.Add(new CodeInstruction(lake ? OpCodes.Ldfld : OpCodes.Ldsfld, AccessTools.Field(lake ? typeof(LakeDistort.MeshInfo) : owner, "v")));
        code.Add(L(0)); code.Add(new CodeInstruction(OpCodes.Ldelema, typeof(Vector3))); code.Add(new CodeInstruction(OpCodes.Ldobj, typeof(Vector3))); code.Add(S(1));
        code.Add(new CodeInstruction(OpCodes.Ldloca, 1));
        foreach (string axis in new[] { "x", "z" })
        {
            code.Add(new CodeInstruction(OpCodes.Ldloca, 1)); code.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Vector3), axis)));
            code.Add(new CodeInstruction(OpCodes.Ldarg_0)); code.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(owner, "m_PerlinCursor"))); code.Add(new CodeInstruction(OpCodes.Mul));
        }
        code.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Mathf), "PerlinNoise")));
        code.Add(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Vector3), "y")));
        for (int edge = 0; edge < 3; edge++)
        {
            code.Add(L(10 + (edge + 1) % 3)); code.Add(L(10 + edge)); code.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Subtraction"))); code.Add(S(13 + edge));
        }
        int[] left = { 14, 15, 13 }, right = { 13, 14, 15 };
        for (int corner = 0; corner < 3; corner++)
        {
            code.Add(L(left[corner])); code.Add(L(right[corner])); code.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "Cross"))); code.Add(S(16 + corner * 2));
            code.Add(new CodeInstruction(OpCodes.Ldloca, 16 + corner * 2)); code.Add(new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector3), "normalized")));
            code.Add(new CodeInstruction(OpCodes.Ldc_R4, -1f)); code.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Multiply"))); code.Add(S(17 + corner * 2));
        }
        code.Add(new CodeInstruction(OpCodes.Ret)); return code;
    }
}
