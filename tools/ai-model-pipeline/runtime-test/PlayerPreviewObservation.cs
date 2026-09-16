using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class PreviewObservation
    {
        public uiQuickPlayerCreate preview;
        public CharacterEventListener cel;
        public JObject profile;
        public string catalogHash,skinset;
    }
    JObject PreviewCoreIdentity()
    {
        System.Reflection.Assembly assembly=CatalogAssembly("FTKModFramework");
        string path=Path.GetFullPath(assembly.Location);
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("Preview observation requires owned-root Core assembly.");
        CatalogNoLinks(path);return ScaleIdentity(assembly);
    }
    PreviewObservation ResolveNativePreview(JObject command)
    {
        CatalogNoLinks(root);RequireSinglePlayer();
        string key=Str(command,"classKey"),skinName=Str(command,"skinset");
        if(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(skinName))throw new ArgumentException("Exact registered classKey and skinset required.");
        JArray profiles=CatalogProfiles(true);string hash=CatalogHash(Path.Combine(root,"model-test-player-profiles.json"));
        if(Str(command,"catalogSha256")!=hash)throw new InvalidOperationException("Player catalog pin differs.");
        JObject profile=null;foreach(JObject candidate in profiles)if((string)candidate["key"]==key){if(profile!=null)throw new InvalidOperationException("Duplicate profile key.");profile=candidate;}
        if(profile==null || (string)profile["skinset"]!=skinName)throw new InvalidOperationException("Exact loaded custom profile class/skinset required.");
        FTK_playerGameStart row=FTK_playerGameStartDB.GetDB().GetEntryByStringID(key);
        FTK_skinset skin=FTK_skinsetDB.GetDB().GetEntryByStringID(skinName);
        if(row==null || row.m_ID!=key || skin==null || skin.m_ID!=skinName)throw new InvalidOperationException("Registered class or skinset unavailable.");
        int classId=FTK_playerGameStartDB.GetDB().GetIntFromID(key),ownerId=LeaseObservationPin.ExactId(command,"ownerInstanceId",false);
        uiStartGame menu=uiStartGame.Instance;if(menu==null || menu.m_CreateUIs==null)return null;
        if(menu.m_CreateUIs.Count>8)throw new InvalidOperationException("Native preview list exceeds8.");
        uiQuickPlayerCreate selected=null;HashSet<int> seen=new HashSet<int>();
        foreach(uiQuickPlayerCreate candidate in menu.m_CreateUIs)
        {
            if(candidate==null)continue;
            if(!seen.Add(candidate.GetInstanceID()))throw new InvalidOperationException("Duplicate native menu UI reference.");
            if(!SceneOwner(candidate) || !candidate.gameObject.activeInHierarchy || candidate.m_ClassID!=classId || (ownerId!=0 && candidate.GetInstanceID()!=ownerId))continue;
            if(selected!=null)throw new InvalidOperationException("Selected class has multiple active previews; provide exact ownerInstanceId.");selected=candidate;
        }
        if(selected==null)return null;
        int skinType=(int)(selected.m_SkinType==FTK_playerGameStart.SkinType.None?row.m_DefaultSkinType:selected.m_SkinType);
        if(row.m_Skinsets==null || skinType<0 || skinType>=row.m_Skinsets.Length || FTK_skinsetDB.GetDB().GetEntry(row.m_Skinsets[skinType])!=skin)
            throw new InvalidOperationException("Actual selected native skinset differs from profile.");
        CharacterEventListener cel=selected.m_Avatar;
        if(cel==null || !SceneOwner(cel) || !cel.gameObject.activeInHierarchy || cel.m_uiQuickPlayerCreate!=selected || selected.m_CharacterPos==null || cel.transform.parent!=selected.m_CharacterPos)return null;
        return new PreviewObservation{preview=selected,cel=cel,profile=profile,catalogHash=hash,skinset=skinName};
    }
    JArray PreviewAssets(JObject profile)
    {
        JArray assets=new JArray();HashSet<string> seen=new HashSet<string>();
        System.Reflection.MethodInfo resolve=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.CustomModelLoader",true).GetMethod("ResolveModelPath",Statics,null,new[]{typeof(string)},null);
        foreach(string group in new[]{"renderers","apparel"})
        {
            JArray assignments=profile[group]as JArray;if(assignments==null)continue;
            foreach(JObject assignment in assignments)foreach(string field in new[]{"glbFile","textureFile"})
            {
                string name=(string)assignment[field];if(field=="textureFile" && string.IsNullOrEmpty(name))continue;if(!seen.Add(name))continue;
                string path=CatalogAsset(resolve,name,field=="glbFile"?".glb":".png");
                assets.Add(new JObject{{"file",name},{"sha256",CatalogHash(path)},{"bytes",new FileInfo(path).Length}});
            }
        }
        return assets;
    }
    static JArray PreviewInventory(uiQuickPlayerCreate preview)
    {
        JArray slots=new JArray();if(preview.m_PlayerInventory==null)return slots;
        foreach(PlayerInventory.ContainerID slot in Enum.GetValues(typeof(PlayerInventory.ContainerID)))
        {
            ItemContainer container=preview.m_PlayerInventory.Get(slot);JArray items=new JArray();
            if(container!=null && container.m_ItemCounts!=null)foreach(KeyValuePair<FTK_itembase.ID,int> entry in container.m_ItemCounts)
                items.Add(new JObject{{"item",(int)entry.Key},{"count",entry.Value}});
            slots.Add(new JObject{{"slot",slot.ToString()},{"slotId",(int)slot},{"containerAvailable",container!=null},{"items",items}});
        }
        return slots;
    }
    JObject PlayerPreviewState(JObject command)
    {
        CatalogKeys(command,"id","session","op","classKey","skinset","catalogSha256","ownerInstanceId");
        JObject core=PreviewCoreIdentity();PreviewObservation observed=ResolveNativePreview(command);
        if(observed==null)return new JObject{{"ok",true},{"status","unavailable_native_preview_absent_or_rebuilding"},{"root",root},{"coreIdentity",core},
            {"note","No matching active reciprocal avatar in the actual native menu. No UI or avatar created, selected, or modified."}};
        uiQuickPlayerCreate preview=observed.preview;CharacterEventListener cel=observed.cel;JArray assets=PreviewAssets(observed.profile);
        SkinnedMeshRenderer[] smrs=cel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if(smrs.Length>64)throw new InvalidOperationException("Preview renderer limit64.");
        JArray renderers=new JArray();foreach(SkinnedMeshRenderer smr in smrs)renderers.Add(Snapshot(smr,true));
        MeshRenderer[] rigid=cel.GetComponentsInChildren<MeshRenderer>(true);if(rigid.Length>64)throw new InvalidOperationException("Preview rigid renderer limit64.");
        JArray accessories=new JArray();foreach(MeshRenderer renderer in rigid)
        {
            MeshFilter filter=renderer.GetComponent<MeshFilter>();Mesh mesh=filter==null?null:filter.sharedMesh;
            accessories.Add(new JObject{{"rendererInstanceId",renderer.GetInstanceID()},{"celRelativePath",Relative(renderer.transform,cel.transform)},
                {"meshInstanceId",mesh==null?0:mesh.GetInstanceID()},{"mesh",mesh==null?null:mesh.name},{"active",renderer.gameObject.activeInHierarchy},{"enabled",renderer.enabled},{"isVisible",renderer.isVisible}});
        }
        // Synchronous re-resolution guards the same native reference join before publishing.
        PreviewObservation after=ResolveNativePreview(command);
        if(after==null || after.preview!=preview || after.cel!=cel || !JToken.DeepEquals(core,PreviewCoreIdentity()) || !JToken.DeepEquals(assets,PreviewAssets(after.profile)))throw new InvalidOperationException("Native preview or Core identity changed during observation.");
        return new JObject{{"ok",true},{"status","observed_actual_native_player_preview"},{"root",root},{"coreIdentity",core},
            {"classKey",Str(command,"classKey")},{"classId",preview.m_ClassID},{"skinType",(int)preview.m_SkinType},{"skinset",observed.skinset},
            {"catalogSha256",observed.catalogHash},{"profile",observed.profile.DeepClone()},{"menuInstanceId",uiStartGame.Instance.GetInstanceID()},
            {"ownerInstanceId",preview.GetInstanceID()},{"celInstanceId",cel.GetInstanceID()},{"pedestalInstanceId",preview.m_CharacterPos.GetInstanceID()},
            {"nativeMenuMembership",true},{"reciprocalPreviewReference",true},{"parentIsNativePedestal",true},
            {"classLabel",preview.m_PlayerClass==null?null:preview.m_PlayerClass.text},{"turnIndex",preview.m_TurnIndex},
            {"lease",ReadLease(cel)},{"assetFiles",assets},{"inventory",PreviewInventory(preview)},{"renderers",renderers},{"nativeRigidAccessories",accessories},
            {"outfit",preview.m_CustomOutfit==null?null:new JObject{{"armorId",(int)preview.m_CustomOutfit.m_ArmorID},{"helmetId",(int)preview.m_CustomOutfit.m_HelmetID},{"backpackId",(int)preview.m_CustomOutfit.m_BackpackID}}},
            {"scope","Read-only actual native menu selection and assembled avatar. Profile conditions, absent alternatives and active native accessories require separate comparison; no full outfit, visual or lifecycle acceptance."}};
    }
}
