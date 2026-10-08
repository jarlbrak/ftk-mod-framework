using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core.SaveCompatibility
{
    // The browser previews saves before OnStart checks their content. A removed class can therefore
    // have no display row. Keep the preview readable without changing the save's admission decision.
    [HarmonyPatch(typeof(StartGameFE.ResumeBrowser), "RefreshGameDetail", new Type[0])]
    internal static class SaveContentPreview
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo native = typeof(FTK_playerGameStart).GetMethod("GetDisplayName",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            MethodInfo replacement = typeof(SaveContentPreview).GetMethod("ClassDisplayName",
                BindingFlags.Static | BindingFlags.NonPublic);
            int match = -1;
            int nativeCount = 0;
            int replacementCount = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (native != null && native.Equals(code[i].operand))
                {
                    nativeCount++;
                    if (code[i].opcode == OpCodes.Callvirt) match = i;
                }
                if (replacement != null && replacement.Equals(code[i].operand) && code[i].opcode == OpCodes.Call)
                    replacementCount++;
            }
            if (nativeCount == 0 && replacementCount == 1) return code;
            if (native == null || native.ReturnType != typeof(string) || replacement == null ||
                nativeCount != 1 || match < 0 || replacementCount != 0)
            {
                Plugin.Log.LogWarning("[save-preview] Exact class display call unavailable; native preview retained.");
                return code;
            }

            // Both calls consume the same class row and return a string. Retain the instruction's
            // labels and exception blocks so branches and native cleanup keep their original targets.
            code[match].opcode = OpCodes.Call;
            code[match].operand = replacement;
            return code;
        }

        internal static string ClassDisplayName(FTK_playerGameStart entry)
        {
            return entry == null ? "Unavailable class" : entry.GetDisplayName();
        }
    }
}
