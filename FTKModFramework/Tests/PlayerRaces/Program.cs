using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GridEditor;
using HarmonyLib;
using FTKModFramework.Core;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private static void Main(string[] args)
    {
        var native = new[] { 0, 1, 3 };
        var custom = new[] { 1000011, 1000009 };
        Check(PlayerRaceSelection.Next(3, false, native, custom) == 1000009, "native to sorted custom");
        Check(PlayerRaceSelection.Next(0, true, native, custom) == 1000011, "reverse wrap");
        Check(PlayerRaceSelection.Next(1000011, false, native, custom) == 0, "forward wrap");
        Check(PlayerRaceSelection.Next(int.MaxValue, false, native, custom) == 0, "removed identity recovery");
        Check(PlayerRaceSelection.Next(-1, false, new int[0], new int[0]) == -1, "empty choices terminate");
        int race = PlayerRaceRegistry.Register("test.races", "possum", "Possum");
        Check(race == PlayerRaceSelection.UnprobedId("test.races", "PlayerRace/possum"), "race uses unprobed allocator identity");
        Check(PlayerRaceRegistry.Register("test.races", "possum", "Possum") == race, "registration idempotent");
        var donor = new FTK_skinset { m_ID = "native" };
        Content.Skins.Rows.Add(1, donor);
        var array = new[] { FTK_skinset.ID.Native, FTK_skinset.ID.Native, FTK_skinset.ID.None, FTK_skinset.ID.Native };
        var row = new FTK_playerGameStart { m_ID = "blacksmith", m_Skinsets = array, m_DefaultSkinType = FTK_playerGameStart.SkinType.Male };
        Content.Classes.Rows.Add(row.m_ID, row);
        var body = new[] { new PlayerRendererMesh() }; var apparel = new PlayerApparelMesh[0];
        Check(!PlayerRaceRegistry.Bind(race, new FTK_playerGameStart { m_ID = row.m_ID }, FTK_skinset.ID.Native, body, apparel), "reject detached class");
        Check(!PlayerRaceRegistry.Bind(race, row, FTK_skinset.ID.None, body, apparel), "reject missing donor");
        Check(PlayerRaceRegistry.Bind(race, row, FTK_skinset.ID.Native, body, apparel), "bind vanilla class");
        var selected = (FTK_playerGameStart.SkinType)race;
        var id = PlayerRaceRegistry.ResolveId(row, selected);
        var clone = Content.Skins.GetEntry(id);
        Check(clone != donor && PlayerRaceRegistry.GetPlan(row, clone) != null, "cloned skinset owns race plan");
        Check(ReferenceEquals(array, row.m_Skinsets) && array.Length == 4 && donor.m_ID == "native", "native array and donor unchanged");
        Check(PlayerRaceRegistry.Bind(race, row, FTK_skinset.ID.Native, body, apparel) && Content.Skins.Rows.Count == 2, "repeat bind does not clone again");
        Check(PlayerRaceRegistry.ResolveId(row, (FTK_playerGameStart.SkinType)int.MaxValue) == FTK_skinset.ID.Native, "unknown race falls back safely");
        Check(PlayerRaceRegistry.ResolveId(row, (FTK_playerGameStart.SkinType)(-2)) == FTK_skinset.ID.Native, "invalid negative falls back safely");
        var recreated = new FTK_playerGameStart { m_ID = row.m_ID, m_Skinsets = array, m_DefaultSkinType = row.m_DefaultSkinType };
        Check(PlayerRaceRegistry.ResolveId(recreated, selected) == id && PlayerRaceRegistry.GetPlan(recreated, clone) != null,
            "recreated vanilla class row resolves retained race skinset and plan");
        recreated.m_DefaultSkinType = FTK_playerGameStart.SkinType.Undead;
        Check(PlayerRaceRegistry.ResolveId(recreated, (FTK_playerGameStart.SkinType)int.MaxValue) == FTK_skinset.ID.Native,
            "removed race with empty default finds another valid native skinset");
        FTK_loreExtraUnlockDB.Instance.Locked.Add(3);
        int choice;
        Check(PlayerRaceRegistry.TryCycle(row, 1, false, out choice) && choice == race, "locked native choice excluded");
        var preview = new uiQuickPlayerCreate { Row = row, m_SkinType = selected };
        FTK_skinset result = null;
        Check(!PlayerRacePreviewSkinPatch.Prefix(preview, ref result) && result == clone, "preview resolves race");
        preview.m_SkinType = FTK_playerGameStart.SkinType.Male;
        Check(PlayerRacePreviewSkinPatch.Prefix(preview, ref result), "native preview unchanged");
        var code = new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, typeof(uiQuickPlayerCreate).GetField("Row")),
            new CodeInstruction(OpCodes.Ldfld, typeof(FTK_playerGameStart).GetField("m_Skinsets")), new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, typeof(uiQuickPlayerCreate).GetField("m_SkinType")), new CodeInstruction(OpCodes.Ldelem_I4), new CodeInstruction(OpCodes.Ret) };
        var patched = PlayerRaceSetClassPatch.Transpiler(code).ToArray();
        Check(code[2].opcode == OpCodes.Ldfld && code[5].opcode == OpCodes.Ldelem_I4, "transpiler leaves input intact");
        Check(PlayerRaceSetClassPatch.Transpiler(patched).Count() == patched.Length, "transpiler idempotent");
        var method = new DynamicMethod("RaceLookup", typeof(FTK_skinset.ID), new[] { typeof(uiQuickPlayerCreate) }, typeof(Program).Module, true);
        var il = method.GetILGenerator();
        foreach (var op in patched)
            if (op.operand is FieldInfo field) il.Emit(op.opcode, field);
            else if (op.operand is MethodInfo call) il.Emit(op.opcode, call);
            else il.Emit(op.opcode);
        var lookup = (Func<uiQuickPlayerCreate, FTK_skinset.ID>)method.CreateDelegate(typeof(Func<uiQuickPlayerCreate, FTK_skinset.ID>));
        preview.m_SkinType = selected;
        Check(lookup(preview) == id && preview.m_SkinType == selected, "transpiled native lookup keeps custom selection during avatar creation");
        preview.m_SkinType = (FTK_playerGameStart.SkinType)int.MaxValue;
        Check(lookup(preview) == FTK_skinset.ID.Native && preview.m_SkinType == row.m_DefaultSkinType, "transpiled lookup normalizes removed race before avatar creation");
        preview.m_SkinType = (FTK_playerGameStart.SkinType)int.MaxValue;
        PlayerRacePreviewTextPatch.Prefix(preview);
        Check(preview.m_SkinType == row.m_DefaultSkinType, "text ingress normalizes missing identity");
        preview.m_SkinType = selected;
        PlayerRaceSaveSettingsPatch.Prefix(preview);
        Check(preview.m_SkinType == selected, "preferences preserve installed race");
        PlayerRaceRegistry.Disable(race);
        Check(PlayerRaceRegistry.ResolveId(row, selected) == FTK_skinset.ID.Native && (int)selected == race, "world lookup preserves saved race identity");
        PlayerRaceSaveSettingsPatch.Prefix(preview);
        Check(preview.m_SkinType == row.m_DefaultSkinType, "preferences do not persist missing preview race");
        bool rejected = false;
        try { PlayerRaceSetClassPatch.Transpiler(new[] { new CodeInstruction(OpCodes.Ret) }).ToArray(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "changed native shape rejected");
        VerifyCollision(false);
        VerifyCollision(true);
        if (args.Length == 1) NativeLookup.Verify(args[0]);
        Console.WriteLine("PASS " + _checks + " player race checks");
    }

    private static void VerifyCollision(bool reverse)
    {
        string guid = "test.collision." + reverse;
        var seen = new Dictionary<int, string>();
        for (int i = 0; i < 250000; i++)
        {
            string key = "race" + i;
            int id = PlayerRaceSelection.UnprobedId(guid, "PlayerRace/" + key);
            string previous;
            if (!seen.TryGetValue(id, out previous)) { seen.Add(id, key); continue; }
            string first = reverse ? key : previous;
            string second = reverse ? previous : key;
            Check(PlayerRaceRegistry.Register(guid, first, first) == id, "initial colliding race registration");
            bool rejected = false;
            try { PlayerRaceRegistry.Register(guid, second, second); } catch (InvalidOperationException) { rejected = true; }
            string name;
            Check(rejected && !PlayerRaceRegistry.TryName(id, out name), "both colliding races disabled in either order");
            rejected = false;
            try { PlayerRaceRegistry.Register(guid, first, first); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "colliding race cannot reactivate in same session");
            return;
        }
        throw new Exception("Collision fixture did not find a pair");
    }
}
