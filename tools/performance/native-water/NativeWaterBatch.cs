using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;

// Isolated experiment: native math owns no meshes and leaves game clocks and uploads intact.
internal static class NativeWaterBatch
{
    const string Owner = "com.ftkmf.native-water-experiment";
    static readonly Harmony Patches = new Harmony(Owner);
    static bool enabled;
    static long completed, fallback;
    internal static void SetEnabled(bool value)
    {
        if (!value) { Clear(); return; }
        if (enabled) return;
        if (!NativeWaterInterop.Ready) throw new InvalidOperationException("Successful numeric self-tests are required before activation.");
        foreach (Type type in new[] { typeof(WaterDistort), typeof(LakeDistort) })
        {
            Patches info = Harmony.GetPatchInfo(AccessTools.Method(type, "OnWillRenderObject"));
            if (info != null)
            {
                foreach (string owner in info.Owners)
                    if (owner != "com.ftkmf.framework") throw new InvalidOperationException("Remove other experimental owners before native water: " + owner);
                if (info.Prefixes.Count != 0 || info.Postfixes.Count != 0 || info.Finalizers.Count != 0 || info.Transpilers.Count > 1)
                    throw new InvalidOperationException("Unreviewed water patch shape.");
                foreach (Patch patch in info.Transpilers)
                {
                    if (patch.PatchMethod.DeclaringType.FullName != "FTKModFramework.Core.Performance.WaterMeshPatch") throw new InvalidOperationException("Unreviewed framework transpiler.");
                    string digest;
                    using (SHA256 algorithm = SHA256.Create())
                    using (FileStream stream = File.OpenRead(patch.PatchMethod.Module.Assembly.Location)) digest = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (digest != "7e16f411cf704dddb38d24b2547354a02ab2b6b869ac8f4f74a14d54ccad929e" &&
                        digest != "aac00e1e03a1fb53ba9bfbd8b4017c2f82bfecfe03d4531a6f1906d2993a2223" &&
                        digest != "b307c20a76bec2d0cacabbd9f17c167e3f722f142e39ac9ee6bdbe15801c75c8")
                        throw new InvalidOperationException("Unreviewed framework binary; re-review the incoming method body before changing the pinned hash.");
                }
            }
        }
        try
        {
            foreach (Type type in new[] { typeof(WaterDistort), typeof(LakeDistort) })
                Patches.Patch(AccessTools.Method(type, "OnWillRenderObject"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(NativeWaterBatch), "Transpile")));
            enabled = true;
        }
        catch { Clear(); throw; }
    }
    internal static void Clear() { enabled = false; Patches.UnpatchSelf(); }
    internal static Dictionary<string, object> Inventory()
    {
        return new Dictionary<string, object> { { "enabled", enabled }, { "completed", completed }, { "fallback", fallback }, { "nativeWaterCalls", NativeWaterInterop.WaterCalls }, { "nativeError", NativeWaterInterop.Error },
            { "semantics", "Process-lifetime callback totals. Native batch replaces only vertex and normal arithmetic. Native cursor increment/wrap, mesh-info lookup and both original mesh uploads remain. False returns fall through to original managed loops without prior native writes." } };
    }
    static bool Compute(Vector3[] vertices, Vector3[] normals, int[] triangles, float cursor, float height)
    {
        if (enabled && NativeWaterInterop.ComputeWater(vertices, normals, triangles, cursor, height)) { completed++; return true; }
        fallback++; return false;
    }
    static int Local(CodeInstruction code, string op)
    {
        if (code.opcode.Name == op || code.opcode.Name == op + ".s")
        {
            LocalBuilder local = code.operand as LocalBuilder;
            if (local != null) return local.LocalIndex;
            if (code.operand is int) return (int)code.operand;
            if (code.operand is byte) return (byte)code.operand;
            if (code.operand is short) return (short)code.operand;
        }
        string name = code.opcode.Name;
        if (name.StartsWith(op + ".") && name.Length == op.Length + 2 && name[name.Length - 1] >= '0' && name[name.Length - 1] <= '3') return name[name.Length - 1] - '0';
        return -1;
    }
    static List<CodeInstruction> Load(List<CodeInstruction> code, FieldInfo field)
    {
        int at = code.FindIndex(c => Equals(c.operand, field) && (c.opcode == OpCodes.Ldfld || c.opcode == OpCodes.Ldsfld));
        if (at < 0) throw new InvalidOperationException("Missing exact array field " + field.Name);
        if (field.IsStatic) return new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldsfld, field) };
        if (at == 0 || Local(code[at - 1], "ldloc") < 0) throw new InvalidOperationException("Missing mesh-info local for " + field.Name);
        return new List<CodeInstruction> { new CodeInstruction(code[at - 1].opcode, code[at - 1].operand), new CodeInstruction(OpCodes.Ldfld, field) };
    }
    static bool Boundary(CodeInstruction code) { return code.labels.Count != 0 || code.blocks.Count != 0; }
    [HarmonyAfter("com.ftkmf.framework")]
    [HarmonyPriority(Priority.Last)]
    static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> incoming, ILGenerator generator, MethodBase original)
    {
        Type owner = original.DeclaringType;
        if (owner != typeof(WaterDistort) && owner != typeof(LakeDistort)) throw new InvalidOperationException("Unexpected water type.");
        var code = new List<CodeInstruction>();
        foreach (CodeInstruction instruction in incoming)
        {
            if (instruction.blocks.Count != 0) throw new InvalidOperationException("Unexpected exception region.");
            var copy = new CodeInstruction(instruction.opcode, instruction.operand); copy.labels.AddRange(instruction.labels); code.Add(copy);
        }
        Type arrays = owner == typeof(WaterDistort) ? owner : owner.GetNestedType("MeshInfo", BindingFlags.NonPublic | BindingFlags.Public);
        FieldInfo vf = AccessTools.Field(arrays, "v"), nf = AccessTools.Field(arrays, "n"), tf = AccessTools.Field(arrays, "tri");
        if (vf == null || vf.FieldType != typeof(Vector3[]) || nf == null || nf.FieldType != typeof(Vector3[]) || tf == null || tf.FieldType != typeof(int[])) throw new InvalidOperationException("Unexpected array schema.");
        var v = Load(code, vf); var n = Load(code, nf); var tri = Load(code, tf);
        MethodInfo getMesh = AccessTools.PropertyGetter(typeof(MeshFilter), "mesh");
        MethodInfo setVertices = AccessTools.PropertySetter(typeof(Mesh), "vertices"), setNormals = AccessTools.PropertySetter(typeof(Mesh), "normals");
        int va = code.FindIndex(c => c.Calls(setVertices)), na = code.FindIndex(c => c.Calls(setNormals));
        int vu = va - v.Count - 2, nu = na - n.Count - 2;
        int field = code.FindIndex(c => Equals(c.operand, vf));
        int body = field - (vf.IsStatic ? 0 : 1), init = body - 3;
        if (init < 1 || va <= body || nu <= va || na != code.Count - 2 || code.FindLastIndex(c => c.Calls(setVertices)) != va || code.FindLastIndex(c => c.Calls(setNormals)) != na ||
            Local(code[vu], "ldloc") != 0 || !code[vu + 1].Calls(getMesh) || Local(code[nu], "ldloc") != 0 || !code[nu + 1].Calls(getMesh) ||
            code[vu - 1].opcode != OpCodes.Blt || code[nu - 1].opcode != OpCodes.Blt ||
            code[init].opcode != OpCodes.Ldc_I4_0 || Local(code[init + 1], "stloc") < 0 || code[init + 2].opcode != OpCodes.Br)
            throw new InvalidOperationException("Unknown arithmetic-loop boundary.");
        int index = Local(code[init + 1], "stloc");
        int condition = vu - v.Count - 4;
        if (Local(code[body + v.Count], "ldloc") != index || Local(code[condition], "ldloc") != index ||
            !(code[vu - 1].operand is Label) || !code[body].labels.Contains((Label)code[vu - 1].operand) ||
            !(code[init + 2].operand is Label) || !code[condition].labels.Contains((Label)code[init + 2].operand))
            throw new InvalidOperationException("Unexpected vertex-loop control flow.");
        for (int i = init; i < body; i++) if (Boundary(code[i])) throw new InvalidOperationException("Unexpected incoming branch at vertex initializer.");
        FieldInfo cursor = AccessTools.Field(owner, "m_PerlinCursor"), height = AccessTools.Field(owner, "m_HeightScale");
        if (cursor == null || height == null || !code.GetRange(0, init).Exists(c => c.opcode == OpCodes.Stfld && Equals(c.operand, cursor)))
            throw new InvalidOperationException("Native cursor advancement not established before arithmetic.");
        for (int i = init; i <= na; i++)
        {
            if (code[i].opcode == OpCodes.Stfld && !Equals(code[i].operand, AccessTools.Field(typeof(Vector3), "y"))) throw new InvalidOperationException("Unexpected field write in water arithmetic.");
            MethodInfo call = code[i].operand as MethodInfo;
            if (call == null) continue;
            string type = call.DeclaringType.FullName;
            if (call == getMesh || call == setVertices || call == setNormals || type == typeof(Vector3).FullName ||
                (type == typeof(Mathf).FullName && call.Name == "PerlinNoise") ||
                (type == "FTKModFramework.Core.Performance.WaterNoiseCache" && call.Name == "Sample") ||
                (type == "FTKModFramework.Core.Performance.WaterFaceGuard" && call.Name == "CanReuse")) continue;
            throw new InvalidOperationException("Unexpected call inside water arithmetic: " + call);
        }
        LocalBuilder success = generator.DeclareLocal(typeof(bool));
        Label uploadVertices = generator.DefineLabel(), uploadNormals = generator.DefineLabel();
        code[vu].labels.Add(uploadVertices); code[nu].labels.Add(uploadNormals);
        code.InsertRange(va + 1, new[] { new CodeInstruction(OpCodes.Ldloc, success), new CodeInstruction(OpCodes.Brtrue, uploadNormals) });
        var batch = new List<CodeInstruction>(); batch.AddRange(v); batch.AddRange(n); batch.AddRange(tri);
        batch.Add(new CodeInstruction(OpCodes.Ldarg_0)); batch.Add(new CodeInstruction(OpCodes.Ldfld, cursor));
        batch.Add(new CodeInstruction(OpCodes.Ldarg_0)); batch.Add(new CodeInstruction(OpCodes.Ldfld, height));
        batch.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeWaterBatch), "Compute")));
        batch.Add(new CodeInstruction(OpCodes.Stloc, success)); batch.Add(new CodeInstruction(OpCodes.Ldloc, success)); batch.Add(new CodeInstruction(OpCodes.Brtrue, uploadVertices));
        code.InsertRange(init, batch);
        return code;
    }
}
