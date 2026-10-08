using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FTKModFramework;
using FTKModFramework.Core.SaveCompatibility;
using GridEditor;
using HarmonyLib;

internal static class Program
{
    private static int _checks;
    private static readonly MethodInfo Native = typeof(FTK_playerGameStart).GetMethod("GetDisplayName", Type.EmptyTypes);
    private static readonly MethodInfo Wrapper = typeof(SaveContentPreview).GetMethod("ClassDisplayName",
        BindingFlags.Static | BindingFlags.NonPublic);

    private static void Check(bool pass, string reason)
    {
        _checks++;
        if (!pass) throw new Exception("save content preview: " + reason);
    }

    private static Func<FTK_playerGameStart, string> Compile(IEnumerable<CodeInstruction> body)
    {
        DynamicMethod method = new DynamicMethod("PreviewClass", typeof(string),
            new[] { typeof(FTK_playerGameStart) }, typeof(Program).Module, true);
        ILGenerator il = method.GetILGenerator();
        foreach (CodeInstruction instruction in body)
        {
            MethodInfo operand = instruction.operand as MethodInfo;
            if (operand == null) il.Emit(instruction.opcode);
            else il.Emit(instruction.opcode, operand);
        }
        return (Func<FTK_playerGameStart, string>)method.CreateDelegate(typeof(Func<FTK_playerGameStart, string>));
    }

    private static void Reject(params CodeInstruction[] source)
    {
        OpCode[] opcodes = source.Select(i => i.opcode).ToArray();
        object[] operands = source.Select(i => i.operand).ToArray();
        int warnings = Plugin.Log.Warnings.Count;
        List<CodeInstruction> result = SaveContentPreview.Transpiler(source).ToList();
        Check(result.Count == source.Length && result.Select((i, n) =>
            ReferenceEquals(i, source[n]) && i.opcode == opcodes[n] && Equals(i.operand, operands[n])).All(v => v),
            "unsupported shape retains every instruction without partial rewrite");
        Check(Plugin.Log.Warnings.Count == warnings + 1, "unsupported shape emits one warning");
    }

    private static void Main()
    {
        HarmonyPatch patch = (HarmonyPatch)Attribute.GetCustomAttribute(typeof(SaveContentPreview), typeof(HarmonyPatch));
        Check(patch.info.declaringType == typeof(StartGameFE.ResumeBrowser) &&
            patch.info.methodName == "RefreshGameDetail" && patch.info.argumentTypes.Length == 0,
            "adapter targets only the exact no-argument browser detail method");

        DynamicMethod labels = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes);
        Label label = labels.GetILGenerator().DefineLabel();
        CodeInstruction call = new CodeInstruction(OpCodes.Callvirt, Native);
        call.labels.Add(label);
        ExceptionBlock block = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock);
        call.blocks.Add(block);
        CodeInstruction before = new CodeInstruction(OpCodes.Ldarg_0);
        CodeInstruction after = new CodeInstruction(OpCodes.Ret);
        List<CodeInstruction> result = SaveContentPreview.Transpiler(new[] { before, call, after }).ToList();
        Check(result[1].opcode == OpCodes.Call && Equals(result[1].operand, Wrapper), "exact callvirt uses wrapper");
        Check(ReferenceEquals(result[0], before) && ReferenceEquals(result[2], after), "surrounding instructions retained");
        Check(ReferenceEquals(result[1], call) && call.labels.Count == 1 && call.labels[0].Equals(label) &&
            call.blocks.Count == 1 && ReferenceEquals(call.blocks[0], block), "branch and exception metadata retained");
        int warnings = Plugin.Log.Warnings.Count;
        List<CodeInstruction> repeated = SaveContentPreview.Transpiler(result).ToList();
        Check(repeated.SequenceEqual(result) && Plugin.Log.Warnings.Count == warnings, "repeat transpilation is idempotent");

        Reject(new CodeInstruction(OpCodes.Nop));
        Reject(new CodeInstruction(OpCodes.Callvirt, Native), new CodeInstruction(OpCodes.Callvirt, Native));
        Reject(new CodeInstruction(OpCodes.Call, Native));
        Reject(new CodeInstruction(OpCodes.Callvirt, Native), new CodeInstruction(OpCodes.Call, Native));
        Reject(new CodeInstruction(OpCodes.Callvirt, Native), new CodeInstruction(OpCodes.Call, Wrapper));
        MethodInfo overload = typeof(FTK_playerGameStart).GetMethod("GetDisplayName", new[] { typeof(int) });
        Reject(new CodeInstruction(OpCodes.Callvirt, overload));

        Func<FTK_playerGameStart, string> preview = Compile(SaveContentPreview.Transpiler(new[] {
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Callvirt, Native), new CodeInstruction(OpCodes.Ret) }));
        Check(preview(null) == "Unavailable class", "rewritten IL accepts a missing display row");
        FTK_playerGameStart installed = new FTK_playerGameStart { Name = "Localized class name" };
        Check(preview(installed) == installed.Name && installed.Calls == 1, "installed display delegated exactly once");
        installed.Name = null;
        Check(preview(installed) == null && installed.Calls == 2, "native null display result remains unchanged");
        ApplicationException failure = new ApplicationException("native localization failed");
        installed.Failure = failure;
        bool propagated = false;
        try { preview(installed); }
        catch (ApplicationException caught) { propagated = ReferenceEquals(caught, failure); }
        Check(propagated && installed.Calls == 3, "native exception is propagated without swallowing or retry");
        Console.WriteLine("Save content preview: " + _checks + " checks passed.");
    }
}
