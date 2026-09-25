using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(FTK_playerGameStart), "GetNextValidSkinType")]
    internal static class PlayerRaceNextPatch
    {
        private static bool Prefix(FTK_playerGameStart __instance, FTK_playerGameStart.SkinType _st,
            ref FTK_playerGameStart.SkinType __result)
        {
            int selected;
            if (!PlayerRaceRegistry.TryCycle(__instance, (int)_st, false, out selected)) return true;
            __result = (FTK_playerGameStart.SkinType)selected;
            return false;
        }
    }

    [HarmonyPatch(typeof(FTK_playerGameStart), "GetPrevValidSkinType")]
    internal static class PlayerRacePreviousPatch
    {
        private static bool Prefix(FTK_playerGameStart __instance, FTK_playerGameStart.SkinType _st,
            ref FTK_playerGameStart.SkinType __result)
        {
            int selected;
            if (!PlayerRaceRegistry.TryCycle(__instance, (int)_st, true, out selected)) return true;
            __result = (FTK_playerGameStart.SkinType)selected;
            return false;
        }
    }

    [HarmonyPatch(typeof(FTK_playerGameStart), "GetSkinTypeDisplayName")]
    internal static class PlayerRaceNamePatch
    {
        private static bool Prefix(FTK_playerGameStart.SkinType _st, ref string __result)
        {
            string name;
            if (!PlayerRaceRegistry.TryName((int)_st, out name)) return true;
            __result = name;
            return false;
        }
    }

    [HarmonyPatch(typeof(uiQuickPlayerCreate), "GetSkinset")]
    internal static class PlayerRacePreviewSkinPatch
    {
        internal static bool Prefix(uiQuickPlayerCreate __instance, ref FTK_skinset __result)
        {
            FTK_playerGameStart row = __instance.GetClassDBEntry();
            if (row == null || !PlayerRaceRegistry.NeedsResolution(row, (int)__instance.m_SkinType)) return true;
            __result = Content.Db<FTK_skinsetDB>().GetEntry(PlayerRaceRegistry.ResolvePreviewId(row, __instance));
            return false;
        }
    }

    [HarmonyPatch(typeof(uiQuickPlayerCreate), "_getSkinSetDB")]
    internal static class PlayerRaceHelmetSkinPatch
    {
        private static bool Prefix(uiQuickPlayerCreate __instance, ref FTK_skinset __result)
        {
            return PlayerRacePreviewSkinPatch.Prefix(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "GetSkinset")]
    internal static class PlayerRaceWorldSkinPatch
    {
        private static bool Prefix(CharacterOverworld __instance, ref FTK_skinset __result)
        {
            FTK_playerGameStart row = __instance.GetDBEntry();
            if (row == null || !PlayerRaceRegistry.NeedsResolution(row, (int)__instance.m_SkinType)) return true;
            __result = Content.Db<FTK_skinsetDB>().GetEntry(PlayerRaceRegistry.ResolveId(row, __instance.m_SkinType));
            return false;
        }
    }

    [HarmonyPatch(typeof(uiQuickPlayerCreate), "UpdateCustomizeText")]
    internal static class PlayerRacePreviewTextPatch
    {
        internal static void Prefix(uiQuickPlayerCreate __instance)
        {
            __instance.m_SkinType = PlayerRaceRegistry.NormalizePreview(__instance.GetClassDBEntry(), __instance.m_SkinType);
        }
    }

    [HarmonyPatch(typeof(uiQuickPlayerCreate), "SaveSettings")]
    internal static class PlayerRaceSaveSettingsPatch
    {
        internal static void Prefix(uiQuickPlayerCreate __instance) { PlayerRacePreviewTextPatch.Prefix(__instance); }
    }

    [HarmonyPatch(typeof(uiQuickPlayerCreate), "SetClass")]
    internal static class PlayerRaceSetClassPatch
    {
        // Only replace the verified array lookup. Keep the game's class validation, avatar recreation,
        // hierarchy placement and UI refresh intact, with the selected race visible throughout.
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            var skinsets = AccessTools.Field(typeof(FTK_playerGameStart), "m_Skinsets");
            var selected = AccessTools.Field(typeof(uiQuickPlayerCreate), "m_SkinType");
            var resolve = AccessTools.Method(typeof(PlayerRaceRegistry), "ResolvePreviewId");
            foreach (CodeInstruction instruction in code)
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, resolve)) return code;
            int match = -1;
            for (int i = 0; i + 3 < code.Count; i++)
                if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, skinsets) &&
                    code[i + 1].opcode == OpCodes.Ldarg_0 && code[i + 2].opcode == OpCodes.Ldfld &&
                    Equals(code[i + 2].operand, selected) && code[i + 3].opcode == OpCodes.Ldelem_I4)
                {
                    if (match >= 0) throw new InvalidOperationException("SetClass race lookup is ambiguous.");
                    match = i;
                }
            if (match < 0) throw new InvalidOperationException("SetClass race lookup shape changed; race patch cannot be installed safely.");
            code[match] = new CodeInstruction(code[match]);
            code[match + 2] = new CodeInstruction(code[match + 2]);
            code[match + 2].opcode = OpCodes.Nop;
            code[match + 2].operand = null;
            code[match + 3] = new CodeInstruction(code[match + 3]);
            code[match].opcode = OpCodes.Nop;
            code[match].operand = null;
            code[match + 3].opcode = OpCodes.Call;
            code[match + 3].operand = resolve;
            return code;
        }
    }
}
