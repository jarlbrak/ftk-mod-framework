using System;
using System.Linq;
using GridEditor;
using UnityEngine;
using FTKModFramework.Core;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static GameObject Part(GameObject root, string path, string mesh)
    {
        GameObject child = new GameObject(path);
        child.AddComponent<SkinnedMeshRenderer>().sharedMesh = new Mesh { name = mesh };
        if (root != null) root.transform.children.Add(child.transform);
        return child;
    }
    private static CharacterEventListener Avatar(bool preview = false)
    {
        GameObject root = new GameObject(".");
        Part(root, "body", "nativeBody");
        Part(root, "robeM(Clone)", "nativeM");
        CharacterEventListener avatar = root.AddComponent<CharacterEventListener>();
        if (preview) avatar.m_uiQuickPlayerCreate = new uiQuickPlayerCreate();
        else avatar.m_CharacterOverworld = new CharacterOverworld();
        (preview ? avatar.m_uiQuickPlayerCreate.m_PlayerInventory : avatar.m_CharacterOverworld.m_PlayerInventory).m_ContainerBody.Item = FTK_itembase.ID.Armor;
        return avatar;
    }
    private static void Main()
    {
        var female = new FTK_skinset { m_ID = "Female", m_Armor = Part(null, "robeF", "nativeF").GetComponent<SkinnedMeshRenderer>() };
        var male = new FTK_skinset { m_ID = "Male", m_Armor = Part(null, "robeM", "nativeM").GetComponent<SkinnedMeshRenderer>() };
        Content.Db<FTK_skinsetDB>().Rows[0] = female; Content.Db<FTK_skinsetDB>().Rows[1] = male;
        var item = new FTK_items { m_ID = "customArmor", m_ObjectSlot = FTK_itembase.ObjectSlot.equip, m_ObjectType = FTK_itembase.ObjectType.armor };
        Content.Db<FTK_itemsDB>().Rows[1] = item;
        ContentRegistry.Bindings[typeof(FTK_itemsDB).Name + item.m_ID] = 1;
        var meshes = new[] { new PlayerApparelMesh("robeM(Clone)", "nativeM", "itemM.glb"), new PlayerApparelMesh("robeF(Clone)", "nativeF", "itemF.glb") };
        Check(!Content.SetItemApparelMeshesFromGlb(new FTK_items { m_ID = item.m_ID }, FTK_skinset.ID.Female, FTK_skinset.ID.Male, meshes), "detached row rejected");
        var saved = female.m_Armor; female.m_Armor = null;
        Check(!Content.SetItemApparelMeshesFromGlb(item, FTK_skinset.ID.Female, FTK_skinset.ID.Male, meshes) && item.m_WearablePrefab == null, "missing garment fails without throwing or mutation");
        Check(Plugin.Log.LastWarning.Contains("missing native armor prefab"), "missing prefab diagnostic identifies binding failure");
        female.m_Armor = saved;
        item.m_ObjectType = FTK_itembase.ObjectType.other;
        Check(!Content.SetItemApparelMeshesFromGlb(item, FTK_skinset.ID.Female, FTK_skinset.ID.Male, meshes) && Plugin.Log.LastWarning.Contains("unsupported object type"), "type diagnostic identifies incorrect item template");
        item.m_ObjectType = FTK_itembase.ObjectType.armor;
        Check(!Content.SetItemApparelMeshesFromGlb(item, FTK_skinset.ID.Female, FTK_skinset.ID.Male, new PlayerApparelMesh("robeM(Clone)", " ", "bad.glb")), "blank native name rejected");
        Check(Content.SetItemApparelMeshesFromGlb(item, FTK_skinset.ID.Female, FTK_skinset.ID.Male, meshes), "valid custom garment registration");
        Check(item.m_WearablePrefab == female.m_Armor.gameObject && item.m_WearablePrefabM == male.m_Armor.gameObject, "only item row binds native female and male garments");
        var boots = new FTK_items { m_ID = "customBoots", m_ObjectSlot = FTK_itembase.ObjectSlot.equip, m_ObjectType = FTK_itembase.ObjectType.boots };
        Content.Db<FTK_itemsDB>().Rows[2] = boots; ContentRegistry.Bindings[typeof(FTK_itemsDB).Name + boots.m_ID] = 2;
        female.m_Boot = Part(null, "bootsF", "bootsF"); male.m_Boot = Part(null, "bootsM", "bootsM");
        Check(Content.SetItemApparelMeshesFromGlb(boots, FTK_skinset.ID.Female, FTK_skinset.ID.Male, new PlayerApparelMesh("bootsM(Clone)", "bootsM", "boots.glb")) && boots.m_WearablePrefab == female.m_Boot && boots.m_WearablePrefabM == male.m_Boot, "native equip-slot boots select boot prefab by object type");
        meshes[0] = new PlayerApparelMesh("wrong", "wrong", "wrong.glb");
        var vanilla = new FTK_playerGameStart { m_ID = "vanilla" };
        var avatar = Avatar(); ExplicitEnemyMeshSwap.Calls = 0;
        PlayerMeshRegistry.Apply(vanilla, male, avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.Last.Length == 1 && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "itemM.glb", "registered gear works on vanilla class without root lease or enabled custom class plan; registration snapshot preserved");
        ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(vanilla, male, Avatar(true));
        Check(ExplicitEnemyMeshSwap.Calls == 1, "preview inventory selects item apparel");
        var custom = new FTK_playerGameStart { m_ID = "custom", m_Skinsets = new[] { FTK_skinset.ID.Male } };
        Content.Db<FTK_playerGameStartDB>().Rows[7] = custom; ContentRegistry.Bindings[typeof(FTK_playerGameStartDB).Name + "custom"] = 7;
        Check(PlayerMeshRegistry.Register(custom, FTK_skinset.ID.Male, new[] { new PlayerRendererMesh("body", "body.glb") }, new[] { new PlayerApparelMesh("robeM(Clone)", "oldClassNative", "classDefault.glb") }), "custom class plan registered");
        ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(custom, male, Avatar());
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.Last.Length == 2 && ExplicitEnemyMeshSwap.Last.Any(e => e.GlbFileName == "itemM.glb") && !ExplicitEnemyMeshSwap.Last.Any(e => e.GlbFileName == "classDefault.glb"), "item overrides class conditional before old native identity validation in one transaction");
        avatar = Avatar(); var lease = avatar.gameObject.AddComponent<EnemyMeshResources>(); lease.Applied = true; lease.VisualResourcesOnly = true;
        ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(custom, male, avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 1, "valid resource-only tint lease does not suppress item and body transaction");
        lease.VisualResourcesOnly = false; ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(custom, male, avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 0 && lease.Retains == 1, "complete existing outfit retains instead of resolving changed native names");
        lease.VisualResourcesOnly = true; lease.Valid = false; ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(custom, male, avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "invalid resource-only lease fails closed");
        avatar = Avatar(); avatar.m_CharacterOverworld.m_PlayerInventory.m_ContainerBody.Item = FTK_itembase.ID.None;
        ExplicitEnemyMeshSwap.Calls = 0; PlayerMeshRegistry.Apply(vanilla, male, avatar);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "ordinary vanilla outfit unchanged");
        PlayerMeshPlan plan; string error; EnemyRendererMesh[] resolved; string[] skipped;
        Check(PlayerMeshPlan.TryCreate(new[] { new PlayerRendererMesh("body", "body.glb") }, new PlayerApparelMesh[0], out plan, out error), "body plan created");
        Check(!plan.TryResolve(Avatar().transform, new[] { new EnemyRendererMesh("body", "item.glb") }, out resolved, out skipped, out error), "item cannot hijack required body path");
        avatar = Avatar(); avatar.gameObject.transform.children[1].GetComponents<SkinnedMeshRenderer>()[0].sharedMesh.name = "wrong";
        bool rejected = false; try { ItemApparelRegistry.Resolve(avatar); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "wrong native garment cannot silently receive item mesh");
        var packMeshes = new[] { new PlayerRendererMesh(".", "pack.glb", "pack.png") };
        Check(!Content.SetClassBackpackMeshesFromGlb(vanilla, FTK_skinset.ID.Male, packMeshes), "vanilla backpack registration rejected");
        Check(!Content.SetClassBackpackMeshesFromGlb(new FTK_playerGameStart { m_ID = custom.m_ID }, FTK_skinset.ID.Male, packMeshes), "detached backpack class row rejected");
        Check(!Content.SetClassBackpackMeshesFromGlb(custom, FTK_skinset.ID.Female, packMeshes), "nonmember backpack skinset rejected");
        Check(Content.SetClassBackpackMeshesFromGlb(custom, FTK_skinset.ID.Male, packMeshes), "custom backpack registered");
        packMeshes[0] = new PlayerRendererMesh("changed", "changed.glb");
        var pack = new GameObject(".");
        ExplicitEnemyMeshSwap.Calls = 0;
        PlayerBackpackRegistry.Apply(custom, male, pack);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.LastRoot == pack && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "pack.glb" && ExplicitEnemyMeshSwap.Last[0].RendererKind == EnemyRendererKind.MeshRenderer, "snapshot targets independent rigid backpack root");
        Check(!Content.SetClassBackpackMeshesFromGlb(custom, FTK_skinset.ID.Male, new PlayerRendererMesh[0]), "invalid replacement registration rejected");
        PlayerBackpackRegistry.Apply(custom, male, pack);
        Check(ExplicitEnemyMeshSwap.Last[0].GlbFileName == "pack.glb", "invalid replacement keeps existing registration");
        ExplicitEnemyMeshSwap.Calls = 0;
        PlayerBackpackRegistry.Apply(vanilla, male, pack);
        PlayerBackpackRegistry.Apply(custom, female, pack);
        PlayerBackpackRegistry.Apply(custom, male, null);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "vanilla other skinset and absent native backpack unchanged");
        var rebuiltPack = new GameObject(".");
        PlayerBackpackRegistry.Apply(custom, male, rebuiltPack);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.LastRoot == rebuiltPack, "new native backpack receives independent transaction");
        ItemModelRegistry.Register((int)FTK_itembase.ID.Armor, new[] { EnemyRendererMesh.ForStaticRenderer(".", "loot.glb", "loot.png", true) });
        ExplicitEnemyMeshSwap.Calls = 0;
        ItemLootDisplayModelPatch.Postfix(FTK_itembase.ID.Unregistered, pack.transform);
        ItemLootDisplayModelPatch.Postfix(FTK_itembase.ID.Armor, null);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "loot display hook leaves vanilla and missing instances unchanged");
        ItemLootDisplayModelPatch.Postfix(FTK_itembase.ID.Armor, pack.transform);
        Check(ExplicitEnemyMeshSwap.Calls == 0, "equipped mapping is never guessed for loot display hierarchy");
        Check(!Content.SetItemDisplayMeshesFromGlb(new FTK_items { m_ID = item.m_ID }, new ItemRendererMesh("child", "display.glb", "display.png")), "display API rejects detached row");
        var displayMeshes = new[] { new ItemRendererMesh("child", "display.glb", "display.png") };
        Check(Content.SetItemDisplayMeshesFromGlb(item, displayMeshes), "exact registered item display accepted");
        displayMeshes[0] = new ItemRendererMesh("changed", "wrong.glb", "wrong.png");
        ItemLootDisplayModelPatch.Postfix(FTK_itembase.ID.Armor, pack.transform);
        Check(ExplicitEnemyMeshSwap.Calls == 1 && ExplicitEnemyMeshSwap.LastRoot == pack && ExplicitEnemyMeshSwap.Last[0].GlbFileName == "display.glb" && ExplicitEnemyMeshSwap.Last[0].RendererPath == "child", "loot preview routes exact display snapshot independently of equipped mapping");
        ItemModelRegistry.Apply(FTK_itembase.ID.Armor, pack);
        Check(ExplicitEnemyMeshSwap.Last[0].GlbFileName == "loot.glb" && ExplicitEnemyMeshSwap.Last[0].RendererPath == ".", "equipped mapping remains independent of display mapping");
        FTK_characterModifier.ID modifierId = FTK_characterModifier.ID.None;
        Check(ItemModifierEnumPatch.Prefix("vanilla", ref modifierId) && modifierId == FTK_characterModifier.ID.None, "unregistered modifier keeps native enum lookup");
        Check(ItemModifierEnumPatch.Prefix(null, ref modifierId), "null modifier keeps native handling");
        ContentRegistry.Bindings[typeof(FTK_itemsDB).Name + "itemOnly"] = 54321;
        Check(ItemModifierEnumPatch.Prefix("itemOnly", ref modifierId), "item identity alone cannot fabricate a modifier");
        ContentRegistry.Bindings[typeof(FTK_characterModifierDB).Name + "customShield"] = 54322;
        Check(!ItemModifierEnumPatch.Prefix("customShield", ref modifierId) && (int)modifierId == 54322, "native card string lookup resolves exact registered modifier integer");
        Console.WriteLine("PASS " + checks + " item apparel registration/merge assertions (Unity and DB stand-ins; no live proof)");
    }
}
