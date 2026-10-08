using System;
using GridEditor;
using UnityEngine;
using FTKModFramework.Core;

internal static class Program
{
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static ItemRendererMesh Model(string name) { return new ItemRendererMesh("head", name + ".glb", name + ".png", true); }
    static void Main()
    {
        var female = new FTK_skinset { m_ID = "Female" };
        var male = new FTK_skinset { m_ID = "Male" };
        var custom = new FTK_skinset { m_ID = "Custom" };
        Content.Db<FTK_skinsetDB>().Rows[0] = female;
        Content.Db<FTK_skinsetDB>().Rows[1] = male;
        Content.Db<FTK_skinsetDB>().Rows[2] = custom;
        var helmet = new FTK_items { m_ID = "head", m_ObjectType = FTK_itembase.ObjectType.helmet };
        Content.Db<FTK_itemsDB>().Rows[(int)FTK_itembase.ID.Helmet] = helmet;
        ContentRegistry.Bindings[typeof(FTK_itemsDB).Name + helmet.m_ID] = (int)FTK_itembase.ID.Helmet;
        var profiles = new[] { ItemHeadProfile.ForNativeSkinset(FTK_skinset.ID.Female, Model("female")),
            ItemHeadProfile.ForCustomRace("com.example.race", "possum", Model("possum")) };
        Check(!Content.SetItemHeadProfilesFromGlb(new FTK_items { m_ID = helmet.m_ID, m_ObjectType = FTK_itembase.ObjectType.helmet }, profiles), "detached row rejected");
        Check(!Content.SetItemHeadProfilesFromGlb(helmet, profiles), "head profiles require exact default fallback");
        Check(Content.SetItemMeshesFromGlb(helmet, Model("default")), "default model registered");
        Check(Content.SetItemHeadProfilesFromGlb(helmet, profiles), "valid registration");
        var row = new FTK_playerGameStart { m_ID = "class" };
        EnemyRendererMesh selected;
        Check(ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, female, out selected) && selected.GlbFileName == "female.glb", "native exact skinset");
        Check(!ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, male, out selected), "unsupported native falls back");
        Check(!ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, custom, out selected), "unregistered custom does not match donor");
        PlayerRaceRegistry.BoundRow = row; PlayerRaceRegistry.BoundSkin = custom;
        Check(ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, custom, out selected) && selected.GlbFileName == "possum.glb", "registered custom exact reference");
        Check(!ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, new FTK_playerGameStart { m_ID = "class" }, custom, out selected), "other class does not match race");
        Check(!Content.SetItemHeadProfilesFromGlb(helmet, new[] { profiles[0], profiles[0] }), "duplicate native rejected");
        Check(!Content.SetItemHeadProfilesFromGlb(helmet, new[] { profiles[1], profiles[1] }), "duplicate race rejected");
        var syntheticSkin = (FTK_skinset.ID)500;
        Content.Db<FTK_skinsetDB>().Rows[500] = new FTK_skinset { m_ID = "Synthetic" };
        Check(!Content.SetItemHeadProfilesFromGlb(helmet,
            ItemHeadProfile.ForNativeSkinset(syntheticSkin, Model("synthetic"))),
            "custom synthetic skinset cannot use native selector");
        var oversized = new ItemHeadProfile[257];
        for (int i = 0; i < oversized.Length; i++) oversized[i] = profiles[0];
        Check(!Content.SetItemHeadProfilesFromGlb(helmet, oversized), "profile count capped before registry mutation");
        Check(ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, female, out selected) && selected.GlbFileName == "female.glb", "failed registration preserves snapshot");
        profiles[0] = ItemHeadProfile.ForNativeSkinset(FTK_skinset.ID.Male, Model("mutated"));
        Check(ItemHeadProfileRegistry.TrySelect((int)FTK_itembase.ID.Helmet, row, female, out selected) && selected.GlbFileName == "female.glb", "caller array mutation isolated");
        var avatarObject = new GameObject("avatar");
        var avatar = avatarObject.AddComponent<CharacterEventListener>();
        var attached = new GameObject("helmet");
        avatar.m_Helmet = attached.AddComponent<Helmet>();
        avatar.m_CharacterOverworld = new CharacterOverworld { Row = row, Skin = female };
        avatar.m_CharacterOverworld.m_PlayerInventory.m_ContainerHead.Item = FTK_itembase.ID.Helmet;
        ExplicitEnemyMeshSwap.Calls = 0;
        ItemHeadProfileRegistry.Apply(avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.LastRoot == attached && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "female.glb", "attached direct prefab selection");
        ExplicitEnemyMeshSwap.Calls = 0;
        ItemHelmetModelPatch.Postfix(FTK_itembase.ID.Helmet, attached);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "FTKHub clone defers default before attached profile selection");
        avatar.m_CharacterOverworld.Skin = male;
        ItemHeadProfileRegistry.Apply(avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "default.glb", "unsupported skinset receives default on attached helmet");
        avatar.m_CharacterOverworld.Skin = female;
        ExplicitEnemyMeshSwap.Calls = 0;
        ExplicitEnemyMeshSwap.NextApplyResult = false;
        ItemHeadProfileRegistry.Apply(avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 2 && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "default.glb", "failed profile transaction falls back to default");
        Check(Content.SetHelmetHairVisibility(helmet, true, false), "hair policy registered");
        var hairTop = new GameObject("top"); var hairBottom = new GameObject("bottom");
        avatar.m_TargetHairTop = hairTop.AddComponent<SkinnedMeshRenderer>();
        avatar.m_TargetHairBottom = hairBottom.AddComponent<SkinnedMeshRenderer>();
        ItemModelRegistry.ApplyAttachedHelmetHairVisibility(FTK_itembase.ID.Helmet, avatar);
        Check(avatar.m_Helmet.m_IsHairTopOn && !avatar.m_Helmet.m_IsHairBottomOn && hairTop.activeSelf && !hairBottom.activeSelf,
            "direct wearable attached hair policy updates native target visibility");
        var previewObject = new GameObject("preview");
        var preview = previewObject.AddComponent<CharacterEventListener>();
        var previewHelmet = new GameObject("previewHelmet");
        preview.m_Helmet = previewHelmet.AddComponent<Helmet>();
        preview.m_uiQuickPlayerCreate = new uiQuickPlayerCreate { Row = row, Skin = female };
        preview.m_uiQuickPlayerCreate.m_PlayerInventory.m_ContainerHead.Item = FTK_itembase.ID.Helmet;
        ExplicitEnemyMeshSwap.Calls = 0;
        ItemHeadProfileRegistry.Apply(preview);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.LastRoot == previewHelmet &&
            ExplicitEnemyMeshSwap.Last[0].GlbFileName == "female.glb", "preview attached helmet selects exact native profile");
        avatar.m_CharacterOverworld.m_HideHelmet = true;
        ExplicitEnemyMeshSwap.Calls = 0;
        ItemHeadProfileRegistry.Apply(avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "hidden helmet does not modify skinset or cosmetic helmet");
        var planes = new[] { new HeadPlane(1,0,0,.2f), new HeadPlane(-1,0,0,.2f),
            new HeadPlane(0,1,0,.2f), new HeadPlane(0,-1,0,.2f) };
        var occlusion = new HeadFaceOcclusion("playerCat", planes, HeadHairMode.ClipStrictHead, HeadHairMode.Preserve);
        planes[0] = new HeadPlane(0,0,1,.3f);
        Check(occlusion.Planes[0].X == 1f, "face plane array copied at declaration");
        var returned = occlusion.Planes; returned[0] = planes[0];
        Check(occlusion.Planes[0].X == 1f, "face plane getter cannot mutate declaration");
        bool rejected = false;
        try { new HeadPlane(float.NaN,0,0,0); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "nonfinite plane rejected");
        rejected = false;
        try { new HeadFaceOcclusion("playerCat", occlusion.Planes, HeadHairMode.HideRenderer, HeadHairMode.Preserve); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "upper hair wholesale hide rejected");
        Console.WriteLine("PASS " + checks + " head profile assertions (actual registry, stubbed Unity; no live proof)");
    }
}
