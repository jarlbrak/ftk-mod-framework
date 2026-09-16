using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    static string CatalogHash(string path)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    static void CatalogKeys(JObject value,params string[] allowed)
    {
        if(value==null)throw new ArgumentException("Expected profile object.");
        foreach(JProperty property in value.Properties())if(Array.IndexOf(allowed,property.Name)<0)throw new ArgumentException("Unknown profile field: "+property.Name);
    }
    static void CatalogNoLinks(string path)
    {
        for(string value=Path.GetFullPath(path);!string.IsNullOrEmpty(value);value=Path.GetDirectoryName(value))
            if((File.GetAttributes(value)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Catalog symlink path refused.");
    }
    string CatalogAsset(MethodInfo resolve,string name,string extension)
    {
        AssetName(name,extension);
        if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}"+System.Text.RegularExpressions.Regex.Escape(extension)+"$"))throw new ArgumentException("Invalid catalog asset basename.");
        string path=Path.GetFullPath((string)resolve.Invoke(null,new object[]{name}));
        string expected=Path.GetFullPath(Path.Combine(root,"BepInEx/plugins/FTKModFramework_content/models"))+Path.DirectorySeparatorChar;
        if(path!=expected+name)throw new InvalidOperationException("Model resolver must target exact isolated content directory.");
        CatalogNoLinks(path);long bytes=new FileInfo(path).Length;
        if(bytes<1 || bytes>64*1024*1024)throw new InvalidOperationException("Asset size must be 1..64 MiB.");
        return path;
    }
    static Assembly CatalogAssembly(string name)
    {
        foreach(Assembly value in AppDomain.CurrentDomain.GetAssemblies())if(value.GetName().Name==name)return value;
        throw new InvalidOperationException("Required assembly absent: "+name);
    }
    JArray CatalogProfiles(bool player)
    {
        string path=Path.Combine(root,player?"model-test-player-profiles.json":"model-test-profiles.json");CatalogNoLinks(path);
        if(new FileInfo(path).Length>1024*1024)throw new InvalidOperationException("Catalog exceeds 1 MiB.");
        JObject doc=JObject.Parse(File.ReadAllText(path));CatalogKeys(doc,"version","profiles");
        if(doc["version"]==null || doc["version"].Type!=JTokenType.Integer || (int)doc["version"]!=1)throw new ArgumentException("Catalog version1 required.");
        JArray profiles=doc["profiles"]as JArray;
        if(profiles==null || profiles.Count<1 || profiles.Count>(player?128:512))throw new ArgumentException("Expected1..512 profiles.");
        Type content=null;
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {Type found=assembly.GetType("RuntimeModelTestContent",false);if(found!=null)content=found;}
        if(content==null)throw new InvalidOperationException("Model test content plugin absent.");
        object instance=content.GetField("instance",Statics).GetValue(null);
        if(instance==null || !JToken.DeepEquals(profiles,content.GetField(player?"playerProfiles":"profiles",Members).GetValue(instance)as JToken))
            throw new InvalidOperationException("Catalog must equal content plugin's loaded profiles.");
        return profiles;
    }
    JObject CatalogProfile(JObject profile,MethodInfo load,MethodInfo resolve)
    {
        CatalogKeys(profile,"key","baseEnemy","displayName","combatProfile","renderers","resourcePrefab","minimumBaseHealth","visualScale","portraitMarkerPath","fallOffPolicy");
        float? visualScale=profile["visualScale"]==null?(float?)null:VisualScaleFixture.Validate(profile["visualScale"]);
        bool preserveCustomBody=FallOffPolicyFixture.Read(profile["fallOffPolicy"]);
        int? minimumBaseHealth=null;
        if(profile["minimumBaseHealth"]!=null)
        {
            JToken minimum=profile["minimumBaseHealth"];
            if(minimum.Type!=JTokenType.Integer || (long)minimum<1 || (long)minimum>1000)
                throw new ArgumentException("minimumBaseHealth must be an integer1..1000.");
            minimumBaseHealth=(int)minimum;
        }
        string key=Str(profile,"key"),baseEnemy=Str(profile,"baseEnemy");
        FTK_enemyCombatDB db=FTK_enemyCombatDB.GetDB();
        FTK_enemyCombat row=db.GetEntryByStringID(key),template=db.GetEntryByStringID(baseEnemy);
        if(row==null || template==null || row.m_ID!=key || template.m_ID!=baseEnemy || row.m_EnemyAsset==null || row.m_WeaponAsset==null)
            throw new InvalidOperationException("Exact registered/template rows with native CEL and weapon required.");
        CharacterEventListener expectedCel=template.m_EnemyAsset;
        string resourcePath=null;
        if(profile["resourcePrefab"]!=null)
        {
            if(profile["resourcePrefab"].Type!=JTokenType.String)throw new ArgumentException("resourcePrefab must be a nonempty Resources path string.");
            resourcePath=(string)profile["resourcePrefab"];
            if(!System.Text.RegularExpressions.Regex.IsMatch(resourcePath,"^[A-Za-z0-9_-]{1,120}$"))
                throw new ArgumentException("resourcePrefab must be an extension-free Resources basename, max120 characters.");
            GameObject prefab=Resources.Load(resourcePath,typeof(GameObject))as GameObject;
            if(prefab==null || prefab.scene.IsValid() || prefab.transform.parent!=null)throw new InvalidOperationException("Exact Resources prefab asset unavailable.");
            CharacterEventListener[] listeners=prefab.GetComponentsInChildren<CharacterEventListener>(true);
            if(listeners.Length!=1 || listeners[0]!=prefab.GetComponent<CharacterEventListener>() || prefab.GetComponent<Animator>()==null)throw new InvalidOperationException("Resource prefab must contain exactly one root CEL and a root Animator.");
            expectedCel=listeners[0];
        }
        if(row.m_EnemyAsset!=expectedCel)throw new InvalidOperationException("Registered CEL must match exact template/resource prefab identity.");
        CharacterEventListener cel=row.m_EnemyAsset;
        if(cel.gameObject.scene.IsValid())throw new InvalidOperationException("Catalog CEL must be prefab asset, not scene object.");
        JObject decode=CatalogRenderers(cel,profile["renderers"]as JArray,null,load,resolve,true);
        bool ok=(bool)decode["ok"];JArray results=(JArray)decode["renderers"];
        JObject profileResult=new JObject{{"key",key},{"baseEnemy",baseEnemy},{"celInstanceId",cel.GetInstanceID()},
            {"resolvedPrefabInstanceId",cel.gameObject.GetInstanceID()},{"resolvedPrefabName",cel.gameObject.name},
            {"controllerCompatibility","pending_native_combat_validation"},{"ok",ok},{"renderers",results}};
        if(profile["portraitMarkerPath"]!=null)
        {
            string markerPath=PortraitMarkerFixture.Validate(profile["portraitMarkerPath"]);
            Transform marker=PortraitMarkerFixture.Resolve(cel.transform,markerPath);
            profileResult["portraitMarker"]=new JObject{{"path",markerPath},{"name",marker.name},{"sourceInstanceId",marker.GetInstanceID()},{"status","source_marker_validated_live_framing_pending"}};
        }
        if(resourcePath!=null)profileResult["resourcePrefab"]=resourcePath;
        if(preserveCustomBody)profileResult["fallOffPolicy"]=FallOffPolicyFixture.PreserveCustomBody;
        if(visualScale.HasValue)
        {
            profileResult["visualScale"]=visualScale.Value;
            profileResult["nativePrefabRootLocalScale"]=Vec(cel.transform.localScale);
            profileResult["visualScaleStatus"]="profile_request_only_native_spawn_scale_measurement_pending";
            profileResult["visualScaleProvenance"]="requested custom-clone visual scale; native prefab scale is read-only metadata, not measured final spawned scale or decode/animation acceptance";
        }
        if(minimumBaseHealth.HasValue)profileResult["healthFixture"]=new JObject{
            {"minimumBaseHealth",minimumBaseHealth.Value},{"nativeTemplateBaseHealth",template.m_HealthTotal},
            {"actualBaseHealth",row.m_HealthTotal},{"expectedBaseHealth",Math.Max(template.m_HealthTotal,minimumBaseHealth.Value)},
            {"computedHealth",null},{"computedHealthStatus","requires_native_spawn_context"},
            {"matchesRequestedCloneFixture",row.m_HealthTotal==Math.Max(template.m_HealthTotal,minimumBaseHealth.Value)},
            {"provenance","custom enemy clone base-health fixture; actual spawned HP depends on native scaling; separate from runtime_decode_preflight"}};
        return profileResult;
    }
    JObject PlayerCatalogProfile(JObject profile,MethodInfo load,MethodInfo resolve)
    {
        CatalogKeys(profile,"key","baseClass","displayName","skinset","defaultSkinType","renderers","startingArmor","apparel");
        JArray apparel=profile["apparel"]as JArray;
        if(profile["apparel"]!=null && (apparel==null || apparel.Count>16))throw new ArgumentException("Optional apparel must be an array0..16.");
        string key=Str(profile,"key"),skinName=Str(profile,"skinset");
        FTK_playerGameStart row=FTK_playerGameStartDB.GetDB().GetEntryByStringID(key);
        FTK_skinset skin=FTK_skinsetDB.GetDB().GetEntryByStringID(skinName);
        if(row==null || row.m_ID!=key || skin==null || skin.m_ID!=skinName || skin.m_Avatar==null)throw new InvalidOperationException("Exact registered class/skinset/native avatar required.");
        int skinId=FTK_skinsetDB.GetDB().GetIntFromID(skinName);
        if(row.m_Skinsets==null || row.m_Skinsets.Length!=7)throw new InvalidOperationException("Exact seven-slot class skin fixture required.");
        foreach(FTK_skinset.ID id in row.m_Skinsets)if((int)id!=skinId)throw new InvalidOperationException("Class skinset fixture mismatch.");
        CharacterEventListener cel=skin.m_Avatar;
        if(cel.gameObject.scene.IsValid() || cel.transform.parent!=null)throw new InvalidOperationException("Player catalog avatar must be an unparented prefab asset.");
        JObject result=CatalogRenderers(cel,profile["renderers"]as JArray,apparel,load,resolve);
        result["key"]=key;result["skinset"]=skinName;result["classId"]=FTK_playerGameStartDB.GetDB().GetIntFromID(key);
        result["celInstanceId"]=cel.GetInstanceID();result["nativePrefabName"]=cel.name;
        int absent=0;foreach(JObject renderer in (JArray)result["renderers"])if((string)renderer["status"]=="conditional_absent_on_base_prefab_not_decoded")absent++;
        result["conditionalAbsentCount"]=absent;result["allConfiguredAssignmentsDecoded"]=absent==0 && (bool)result["ok"];
        result["boundary"]="Base skinset prefab runtime decode only. Native Stitcher can add or replace apparel at avatar creation; absent conditional paths are not decoded and do not establish whether the actual outfit is styled. No outfit, live binding, controller or animation acceptance.";
        return result;
    }
    static string CatalogNativeName(JToken token)
    {
        if(token==null || token.Type!=JTokenType.String)throw new ArgumentException("Exact expectedNativeMeshName string required.");
        string name=(string)token;if(name.Trim().Length==0 || name.Length>160)throw new ArgumentException("Native mesh name must be nonblank, max160.");
        foreach(char value in name)if(char.IsControl(value))throw new ArgumentException("Control character in native mesh name.");
        return name;
    }
    static bool CatalogStaticRenderer(JObject assignment)
    {
        JToken value=assignment==null?null:assignment["rendererKind"];
        if(value==null)return false;
        if(value.Type!=JTokenType.String)throw new ArgumentException("rendererKind must be a string when present.");
        if((string)value=="SkinnedMeshRenderer")return false;
        if((string)value=="MeshRenderer")return true;
        throw new ArgumentException("rendererKind must be SkinnedMeshRenderer or MeshRenderer.");
    }
    JObject CatalogRenderers(CharacterEventListener cel,JArray assignments,JArray apparel,MethodInfo load,MethodInfo resolve,bool enemyOptions=false)
    {
        if(assignments==null || assignments.Count<1 || assignments.Count>16)throw new ArgumentException("Expected1..16 renderers.");
        JArray results=new JArray();HashSet<string> paths=new HashSet<string>();bool ok=true;
        List<JToken> combined=new List<JToken>();foreach(JToken token in assignments)combined.Add(token);
        if(apparel!=null)foreach(JToken token in apparel)combined.Add(token);
        foreach(JToken token in combined)
        {
            Mesh decoded=null;JObject result=new JObject();
            try
            {
                JObject assignment=token as JObject;bool optional=apparel!=null && apparel.Contains(token);
                CatalogKeys(assignment,optional?new[]{"rendererPath","glbFile","textureFile","expectedNativeMeshName"}:enemyOptions?new[]{"rendererPath","glbFile","textureFile","disableNativeEmission","materialSlots","rendererKind"}:new[]{"rendererPath","glbFile","textureFile"});
                MaterialSlotFixture.Slot[] materialSlots=enemyOptions?MaterialSlotFixture.Read(assignment):null;
                bool staticRenderer=enemyOptions&&CatalogStaticRenderer(assignment);
                if(staticRenderer&&materialSlots!=null)throw new InvalidOperationException("Static MeshRenderer assignments do not support materialSlots.");
                if(enemyOptions && materialSlots==null)result["disableNativeEmission"]=RendererEmissionFixture.Read(assignment["disableNativeEmission"]);
                if(assignment["rendererPath"]==null || assignment["rendererPath"].Type!=JTokenType.String || assignment["glbFile"]==null || assignment["glbFile"].Type!=JTokenType.String)throw new ArgumentException("Assignment path and GLB must be strings.");
                string expectedNative=null;
                if(optional){expectedNative=CatalogNativeName(assignment["expectedNativeMeshName"]);result["expectedNativeMeshName"]=expectedNative;}
                result["conditionalApparel"]=optional;
                string path=Str(assignment,"rendererPath"),name=Str(assignment,"glbFile");result["celRelativeRendererPath"]=path;result["glbFile"]=name;result["rendererKind"]=staticRenderer?"MeshRenderer":"SkinnedMeshRenderer";
                if(string.IsNullOrEmpty(path) || path.Length>512 || !paths.Add(path))throw new ArgumentException("Invalid/duplicate renderer path.");
                if(path!=".")foreach(string part in path.Split('/'))if(part=="" || part=="." || part==".." || part.IndexOf('\\')>=0)throw new ArgumentException("Invalid path segment.");
                string modelPath=CatalogAsset(resolve,name,".glb");
                result["modelBytes"]=new FileInfo(modelPath).Length;result["modelSha256"]=CatalogHash(modelPath);
                if(assignment["textureFile"]!=null && assignment["textureFile"].Type!=JTokenType.Null)
                {
                    if(assignment["textureFile"].Type!=JTokenType.String)throw new ArgumentException("Texture filename must be a string or null.");
                    string texturePath=CatalogAsset(resolve,Str(assignment,"textureFile"),".png");
                    result["textureBytes"]=new FileInfo(texturePath).Length;result["textureSha256"]=CatalogHash(texturePath);
                }
                Transform target=cel.transform;
                if(path!=".")foreach(string part in path.Split('/'))
                {
                    if(part=="" || part=="." || part==".." || part.IndexOf('\\')>=0)throw new ArgumentException("Invalid path segment.");
                    Transform next=null;
                    foreach(Transform child in target)if(child.name==part){if(next!=null)throw new ArgumentException("Ambiguous native path segment.");next=child;}
                    if(next==null){if(optional){target=null;break;}throw new ArgumentException("Missing native path segment.");}target=next;
                }
                if(target==null){result["status"]="conditional_absent_on_base_prefab_not_decoded";result["ok"]=true;results.Add(result);continue;}
                Renderer renderer;MeshFilter meshFilter=null;Mesh original;int nonnull=0;
                if(staticRenderer)
                {
                    MeshRenderer[] matches=target.GetComponents<MeshRenderer>();MeshFilter[] filters=target.GetComponents<MeshFilter>();
                    if(matches.Length!=1 || filters.Length!=1 || target.GetComponents<SkinnedMeshRenderer>().Length!=0 || filters[0].sharedMesh==null)throw new InvalidOperationException("Exact one native MeshRenderer plus MeshFilter with mesh and no SkinnedMeshRenderer required.");
                    renderer=matches[0];meshFilter=filters[0];original=meshFilter.sharedMesh;
                    if(renderer.sharedMaterials==null || renderer.sharedMaterials.Length!=1 || renderer.sharedMaterials[0]==null)throw new InvalidOperationException("Static target requires exactly one usable native material slot.");
                    result["nativeMesh"]=original.name;result["rendererInstanceId"]=renderer.GetInstanceID();result["meshFilterInstanceId"]=meshFilter.GetInstanceID();result["materialSlots"]=renderer.sharedMaterials.Length;result["nonnullMaterialSlots"]=1;
                    MethodInfo staticLoad=load.DeclaringType.GetMethod("LoadStaticGlb",Statics,null,new[]{typeof(string),typeof(bool)},null);
                    if(staticLoad==null)throw new MissingMethodException("Exact strict static GLB decoder required.");
                    decoded=staticLoad.Invoke(null,new object[]{name,true})as Mesh;
                }
                else
                {
                    SkinnedMeshRenderer[] matches=target.GetComponents<SkinnedMeshRenderer>();
                    if(matches.Length!=1 || matches[0].sharedMesh==null)throw new InvalidOperationException("Exact one native SMR with mesh required.");
                    SkinnedMeshRenderer skinned=matches[0];renderer=skinned;original=skinned.sharedMesh;
                    if(optional && !string.Equals(original.name,expectedNative,StringComparison.Ordinal))throw new InvalidOperationException("Exact conditional native mesh name mismatch.");
                    result["nativeMesh"]=original.name;result["rendererInstanceId"]=skinned.GetInstanceID();result["boneSignature"]=BoneSignature(skinned);
                    result["bones"]=skinned.bones.Length;result["nativeBindposes"]=original.bindposes.Length;
                    foreach(Material material in skinned.sharedMaterials)if(material!=null)nonnull++;
                    result["materialSlots"]=skinned.sharedMaterials.Length;result["nonnullMaterialSlots"]=nonnull;
                    if(materialSlots==null)decoded=load.Invoke(null,new object[]{name,skinned.bones,original.bindposes,true})as Mesh;
                    else
                    {
                        if(skinned.sharedMaterials.Length!=materialSlots.Length || nonnull!=materialSlots.Length)throw new InvalidOperationException("Multi-slot mode must preserve all nonnull native slots.");
                        Material[] nativeMaterials=skinned.sharedMaterials;JArray nativeSlots=new JArray();
                        for(int slotIndex=0;slotIndex<nativeMaterials.Length;slotIndex++)
                        {
                            Material mat=nativeMaterials[slotIndex];JObject material=new JObject{{"slot",slotIndex},{"instanceId",mat.GetInstanceID()},{"shader",mat.shader==null?null:mat.shader.name},{"name",mat.name}};
                            if(mat.HasProperty("_MainTex")){Texture texture=mat.GetTexture("_MainTex");Vector2 offset=mat.GetTextureOffset("_MainTex"),scale=mat.GetTextureScale("_MainTex");material["textureId"]=texture==null?0:texture.GetInstanceID();material["textureName"]=texture==null?null:texture.name;material["offset"]=new JArray(offset.x,offset.y);material["scale"]=new JArray(scale.x,scale.y);}
                            nativeSlots.Add(material);
                        }
                        result["nativeMaterialMetadata"]=nativeSlots;
                        int[] mapping=new int[materialSlots.Length];JArray options=new JArray();
                        foreach(MaterialSlotFixture.Slot slot in materialSlots)
                        {
                            mapping[slot.PrimitiveIndex]=slot.NativeMaterialSlot;
                            JObject option=new JObject{{"primitiveIndex",slot.PrimitiveIndex},{"nativeMaterialSlot",slot.NativeMaterialSlot},{"disableNativeEmission",slot.DisableNativeEmission}};
                            if(slot.TextureFile!=null){string texture=CatalogAsset(resolve,slot.TextureFile,".png");option["textureFile"]=slot.TextureFile;option["textureSha256"]=CatalogHash(texture);option["textureBytes"]=new FileInfo(texture).Length;}
                            options.Add(option);
                        }
                        MethodInfo multi=load.DeclaringType.GetMethod("LoadSkinnedGlb",Statics,null,new[]{typeof(string),typeof(Transform[]),typeof(Matrix4x4[]),typeof(bool),typeof(int[])},null);
                        if(multi==null)throw new MissingMethodException("Exact strict5arg multi-primitive decoder required.");
                        decoded=multi.Invoke(null,new object[]{name,skinned.bones,original.bindposes,true,mapping})as Mesh;
                        Material[] afterMaterials=skinned.sharedMaterials;if(afterMaterials.Length!=nativeMaterials.Length)throw new InvalidOperationException("Native material array changed during decode preflight.");
                        for(int i=0;i<nativeMaterials.Length;i++)if(afterMaterials[i]!=nativeMaterials[i])throw new InvalidOperationException("Native material identity changed during decode preflight.");
                        result["materialSlotAssignments"]=options;
                        if(decoded!=null && decoded.subMeshCount!=materialSlots.Length)throw new InvalidOperationException("Decoded submesh count differs from complete native slot map.");
                    }
                }
                if(decoded==null)throw new InvalidOperationException("Strict Mono runtime GLB decoder returned null; see loader log.");
                if(materialSlots!=null)result["decodedMaterialGeometry"]=MaterialGeometry(decoded);
                result["vertices"]=decoded.vertexCount;result["triangles"]=decoded.triangles.Length/3;if(!staticRenderer)result["decodedBindposes"]=decoded.bindposes.Length;
                if(staticRenderer){if(meshFilter.sharedMesh!=original)throw new InvalidOperationException("Unexpected native static mesh identity change.");}
                else if(((SkinnedMeshRenderer)renderer).sharedMesh!=original)throw new InvalidOperationException("Unexpected native mesh identity change.");
                result["status"]="strict_decoded";result["ok"]=true;
            }
            catch(Exception error){ok=false;result["ok"]=false;result["error"]=error.ToString();}
            finally{if(decoded!=null)UnityEngine.Object.Destroy(decoded);}
            results.Add(result);
        }
        return new JObject{{"ok",ok},{"renderers",results}};
    }
    IEnumerator CatalogPreflight(string id,bool player=false)
    {
        JArray results=new JArray();JArray profiles=null;string error=null;int passed=0;
        MiniHexDungeon pinnedDungeon=null;int pinnedLevel=-1,pinnedRoom=-1;string catalogHash=null;
        try
        {
            MethodInfo load=null,resolve=null;
            try
            {
                ReadyContext initial=RequireReadyPreparation();pinnedDungeon=initial.dungeon;pinnedLevel=pinnedDungeon.m_Level;pinnedRoom=pinnedDungeon.m_RoomIndex;
                CatalogNoLinks(root);profiles=CatalogProfiles(player);catalogHash=CatalogHash(Path.Combine(root,player?"model-test-player-profiles.json":"model-test-profiles.json"));
                Assembly core=CatalogAssembly("FTKModFramework");
                load=core.GetType("FTKModFramework.Core.RuntimeGltfMeshLoader",true).GetMethod("LoadSkinnedGlb",Statics,null,new[]{typeof(string),typeof(Transform[]),typeof(Matrix4x4[]),typeof(bool)},null);
                resolve=core.GetType("FTKModFramework.Core.CustomModelLoader",true).GetMethod("ResolveModelPath",Statics,null,new[]{typeof(string)},null);
                if(load==null || resolve==null)throw new MissingMethodException("Exact strict4arg decoder/model resolver required.");
            }
            catch(Exception failure){error=failure.ToString();}
            if(error==null)foreach(JToken token in profiles)
            {
                try{ReadyContext current=RequireReadyPreparation();CatalogNoLinks(root);
                    if(current.dungeon!=pinnedDungeon || current.dungeon.m_Level!=pinnedLevel || current.dungeon.m_RoomIndex!=pinnedRoom)throw new InvalidOperationException("Ready fixture changed during catalog run.");}
                catch(Exception failure){error=failure.ToString();break;}
                JObject result;
                try{result=player?PlayerCatalogProfile(token as JObject,load,resolve):CatalogProfile(token as JObject,load,resolve);}
                catch(Exception failure){result=new JObject{{"ok",false},{"error",failure.ToString()},{"profile",token}};}
                results.Add(result);if((bool)result["ok"])passed++;
                yield return null;
            }
            Finish(id,new JObject{{"ok",error==null && profiles!=null && passed==profiles.Count},{"provenance","runtime_decode_preflight"},
                {"scope",player?"player-skinset-prefab-assets":"enemy-prefab-assets"},{"session",sessionId},{"root",root},{"catalogSha256",catalogHash},{"requested",profiles==null?0:profiles.Count},{"completed",results.Count},{"passed",passed},{"error",error},{"profiles",results},
                {"note","Temporary strict-decoded meshes destroyed; native prefab never instantiated or edited. No live binding, texture decode/application, animation or artistic validation."}});
        }
        finally{busy=false;}
    }
}
