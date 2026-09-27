using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using GridEditor;
using FTKModFramework.Core.UI;
class Program
{
    static int count;
    static readonly Type[] Locals = { typeof(FTK_enemyCombat.ID), typeof(RawImage), typeof(Rect), typeof(Rect) };
    static void Check(bool value) { count++; if (!value) throw new Exception("Assertion " + count); }
    static List<CodeInstruction> Body()
    {
        var code = new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FTK_enemyCombat), "GetEnum")),
            new CodeInstruction(OpCodes.Stloc_0), new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(uiEnemyEncounterPortrait), "m_Portrait")),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(uiActiveTimePortrait), "m_RawImage")), new CodeInstruction(OpCodes.Stloc_1) };
        code.AddRange(UnknownEncounterPortraitAllocationPatch.AllocationShape());
        code.AddRange(new[] { new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldc_I4_M1), new CodeInstruction(OpCodes.Beq, default(Label)) });
        return code;
    }
    static void Main()
    {
        int start;
        Check(UnknownEncounterPortraitAllocationPatch.FindAllocation(Body(), Locals, out start) && start == 7);
        // Every skipped instruction and boundary is significant, including the assignment and wrap setter.
        for (int i = 0; i < Body().Count; i++)
        {
            var changed = Body(); changed[i] = new CodeInstruction(OpCodes.Nop);
            Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(changed, Locals, out start));
        }
        for (int i = 7; i < 37; i++)
        {
            var labeled = Body(); labeled[i].labels.Add(default(Label));
            Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(labeled, Locals, out start));
        }
        var exceptional = Body(); exceptional[3].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(exceptional, Locals, out start));
        foreach (var op in new[] { OpCodes.Ldloc, OpCodes.Ldloc_S, OpCodes.Ldloca, OpCodes.Ldloca_S, OpCodes.Stloc, OpCodes.Stloc_S })
            foreach (int local in new[] { 2, 3 })
            {
                var observed = Body(); observed.Add(new CodeInstruction(op, local));
                Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(observed, Locals, out start));
            }
        var overwritten = Body(); overwritten.Insert(3, new CodeInstruction(OpCodes.Stloc_0));
        Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(overwritten, Locals, out start));
        for (int i = 0; i < Locals.Length; i++)
        {
            Type[] invalid = (Type[])Locals.Clone(); invalid[i] = typeof(int);
            Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(Body(), invalid, out start));
        }
        var wrongMember = Body(); wrongMember[9].operand = AccessTools.PropertyGetter(typeof(RectTransform), "rect");
        Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(wrongMember, Locals, out start));
        var malformed = Body(); malformed[12].operand = "not a local";
        Check(!UnknownEncounterPortraitAllocationPatch.FindAllocation(malformed, Locals, out start));
        Console.WriteLine(count + " unknown-portrait IL preflight assertions passed; native execution remains a live gate.");
    }
}
