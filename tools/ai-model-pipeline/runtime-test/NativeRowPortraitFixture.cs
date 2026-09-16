using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    void RowPortraitAssetPins()
    {
        foreach(JObject asset in (JArray)portraitEvidence["assets"])
        {
            string name=(string)asset["file"];AssetName(name,(string)asset["field"]=="glbFile"?".glb":".png");
            string path=Path.Combine(root,"BepInEx/plugins/FTKModFramework_content/models/"+name);CatalogNoLinks(path);
            if(CatalogHash(path)!=(string)asset["sha256"])throw new InvalidOperationException("Portrait asset changed since watch arm.");
        }
    }
    static JObject RowPortraitSource(CharacterEventListener source)
    {
        bool truncated;List<Transform> nodes=PortraitNodes(source.transform,out truncated);
        if(truncated)throw new InvalidOperationException("Source hierarchy exceeds256.");
        JArray transforms=new JArray();foreach(Transform node in nodes)transforms.Add(PortraitTransform(node,source.transform));
        JObject cel=PortraitCel(source,true);
        if((bool)cel["traversalOrRenderersTruncated"])throw new InvalidOperationException("Source renderer inventory truncated.");
        foreach(JObject renderer in (JArray)cel["renderers"])if((bool)renderer["bonesTruncated"] || (bool)renderer["materialsTruncated"])throw new InvalidOperationException("Source renderer detail truncated.");
        return new JObject{{"cel",cel},{"transforms",transforms}};
    }
    static void RowPortraitUiPrefab(uiActiveTimePortrait prefab)
    {
        if(prefab==null || prefab.m_RawImage==null || prefab.m_HudRawImage==null || prefab.m_HudBG==null || prefab.m_SelectFrame==null)
            throw new InvalidOperationException("Native UI prefab dependencies absent.");
        foreach(Transform target in new[]{prefab.m_RawImage.transform,prefab.m_HudRawImage.transform,prefab.m_HudBG.transform,prefab.m_SelectFrame.transform,
            prefab.m_BG==null?null:prefab.m_BG.transform,prefab.m_HudSelectFrame==null?null:prefab.m_HudSelectFrame.transform,prefab.m_ProficiencyRoot})
            if(target!=null && target!=prefab.transform && !target.IsChildOf(prefab.transform))throw new InvalidOperationException("Native UI field points outside clone subtree.");
        Component[] components=prefab.GetComponentsInChildren<Component>(true);
        if(components.Length>256)throw new InvalidOperationException("Native UI prefab exceeds256components.");
        foreach(Component component in components)
        {
            if(component==null)throw new InvalidOperationException("Missing native UI script.");Type type=component.GetType();
            if(type!=typeof(RectTransform) && type!=typeof(Transform) && type!=typeof(CanvasRenderer) && type!=typeof(CanvasGroup)
                && type!=typeof(Image) && type!=typeof(RawImage) && type!=typeof(Text) && type!=typeof(Mask) && type!=typeof(RectMask2D)
                && type!=typeof(uiActiveTimePortrait))throw new InvalidOperationException("Unaudited UI component: "+type.FullName);
        }
    }
    static GameObject RowUi(string name,Transform parent)
    {
        GameObject value=new GameObject(name,typeof(RectTransform));if(parent!=null)value.transform.SetParent(parent,false);return value;
    }
    static JObject RowPortraitCacheAfter(IDictionary table,Dictionary<string,OffscreenCamera> before,string key,int width,int height,out OffscreenCamera selected)
    {
        selected=table==null?null:table[key]as OffscreenCamera;
        if(table==null || table.Count!=before.Count+(before.ContainsKey(key)?0:1))throw new InvalidOperationException("Native portrait cache count changed unexpectedly.");
        foreach(KeyValuePair<string,OffscreenCamera> prior in before)
            if(!table.Contains(prior.Key)||!object.ReferenceEquals(table[prior.Key],prior.Value))throw new InvalidOperationException("Existing native cache entry changed.");
        if(selected==null || !selected.gameObject.scene.IsValid() || selected.CameraID!=key || selected.m_RenderTexture==null || selected.m_RenderTexture.width!=width || selected.m_RenderTexture.height!=height
            || selected.m_IsRendering || selected.m_RenderOneFrame!=0 || selected.m_TargetObject!=null || selected.m_Texture2D!=null)
            throw new InvalidOperationException("Exact native portrait cache entry must be idle after Initialize.");
        return new JObject{{"key",key},{"createdByNativeInitialize",!before.ContainsKey(key)},{"cameraInstanceId",selected.GetInstanceID()},
            {"renderTextureInstanceId",selected.m_RenderTexture.GetInstanceID()},{"entriesBefore",before.Count},{"entriesAfter",table.Count},
            {"existingEntriesUnchanged",true},{"targetAndTextureClear",true},{"ownership","Persistent native cache and RenderTexture; observed only, never destroyed or claimed as preview-owned."}};
    }
    IEnumerator NativeRowPortraitFixture(string id,JObject command)
    {
        GameObject owned=null,clone=null;Texture2D pixels=null;uiEnemyEncounterPortrait ui=null;OffscreenCamera camera=null;
        CharacterEventListener source=null;FTK_enemyCombat row=null;KrakenAdapterReadyPin pin=null;RenderTexture active=RenderTexture.active;
        JObject before=null,after=null,trace=null,pngResult=null,uiResult=null,evidence=null,leaseResult=null;
        List<WatchedResource> resources=new List<WatchedResource>();HashSet<int> priorLeases=new HashSet<int>(),priorTextures=new HashSet<int>();int leaseId=0;
        string error=null;JArray cleanupErrors=new JArray();bool invoked=false,returned=false,sourceUnchanged=false,readyUnchanged=false,unknownTarget=false;
        int expectedWidth=0,expectedHeight=0;JObject dimensions=null,cacheResult=null;
        IDictionary cameras=null;string cameraKey=null;Dictionary<string,OffscreenCamera> cacheBefore=new Dictionary<string,OffscreenCamera>();
        int uiId=0,textureId=0,cloneId=0;Texture originalTexture=null,originalHudTexture=null;
        try
        {
            try
            {
                CatalogKeys(command,"id","session","op","enemy","traceArmCommandId");CatalogNoLinks(root);CatalogNoLinks(output);
                pin=new KrakenAdapterReadyPin(sessionId);pin.Check(sessionId);
                if(Str(command,"session")!=sessionId || !PortraitEnabled() || portraitTrace.Count!=0 || portraitErrors!=0
                    || Str(command,"traceArmCommandId")!=portraitArmId || Str(command,"enemy")!=portraitRow.m_ID)
                    throw new InvalidOperationException("Exact prearmed empty portrait-watch in current isolated session required.");
                RowPortraitAssetPins();row=portraitRow;source=row.m_EnemyAsset;FTK_enemyCombat.ID enumId=FTK_enemyCombat.GetEnum(row.m_ID);
                if(enumId==FTK_enemyCombat.ID.None || !object.ReferenceEquals(FTK_enemyCombatDB.Get(enumId),row) || source==null || source.gameObject.scene.IsValid())
                    throw new InvalidOperationException("Exact synthetic row enum/native source asset required.");
                if(GameLogic.Instance.GetGameDef()==null || FTK_enemyScaleDB.GetDB().IsContainID(row.m_ID))throw new InvalidOperationException("Game definition and non-scaled custom row required for read-only native level display.");
                if(HauntManager.IsScourgeActive(HauntManager.Scourge.Deimos))throw new InvalidOperationException("Deimos unknown-portrait branch excluded.");
                uiActiveTimePortrait prefab=FTKUI.Instance.m_EncounterMenu.m_EnemyPanel.m_ActiveTimePortraitPrefab;RowPortraitUiPrefab(prefab);
                RectTransform rawRect=prefab.m_RawImage.rectTransform;int aa=uiActiveTime.Instance.m_OffscreenEnemyPortraitAA;
                dimensions=new JObject{{"rectWidth",rawRect.rect.width},{"rectHeight",rawRect.rect.height},{"aa",aa},
                    {"anchorMin",new JArray(rawRect.anchorMin.x,rawRect.anchorMin.y)},{"anchorMax",new JArray(rawRect.anchorMax.x,rawRect.anchorMax.y)},
                    {"sizeDelta",new JArray(rawRect.sizeDelta.x,rawRect.sizeDelta.y)},
                    {"derivation","Native Initialize casts each RawImage rect dimension to int before multiplying AA. Equal anchors make this child rectangle independent of constructed parent size."}};
                if(rawRect==prefab.transform || rawRect.anchorMin!=rawRect.anchorMax || rawRect.rect.width!=rawRect.sizeDelta.x || rawRect.rect.height!=rawRect.sizeDelta.y)
                    throw new InvalidOperationException("Native RawImage must be a fixed-anchor child with intrinsic dimensions; parent-dependent layout excluded.");
                expectedWidth=RowPortraitOwnership.NativeDimension(rawRect.rect.width,aa);expectedHeight=RowPortraitOwnership.NativeDimension(rawRect.rect.height,aa);
                cameraKey="Portrait,"+expectedWidth+","+expectedHeight;dimensions["width"]=expectedWidth;dimensions["height"]=expectedHeight;dimensions["cameraKey"]=cameraKey;
                cameras=typeof(OffscreenCamManager).GetField("m_CameraTable",Members).GetValue(OffscreenCamManager.Instance)as IDictionary;
                if(cameras==null || cameras.Count>32)throw new InvalidOperationException("Initialized native camera cache with at most32 entries required.");
                foreach(DictionaryEntry entry in cameras)
                {if(!(entry.Key is string)||!(entry.Value is OffscreenCamera))throw new InvalidOperationException("Unexpected native cache entry.");cacheBefore.Add((string)entry.Key,(OffscreenCamera)entry.Value);}
                dimensions["matchingCameraExists"]=cameras.Contains(cameraKey);
                camera=cameras[cameraKey]as OffscreenCamera;
                if(cameras.Contains(cameraKey) && (camera==null || !camera.gameObject.scene.IsValid() || camera.CameraID!=cameraKey || camera.m_RenderTexture==null || camera.m_RenderTexture.width!=expectedWidth || camera.m_RenderTexture.height!=expectedHeight || camera.m_IsRendering || camera.m_RenderOneFrame!=0 || camera.m_TargetObject!=null || camera.m_Texture2D!=null))
                    throw new InvalidOperationException("Existing cached portrait camera must be idle with no target/texture.");
                before=RowPortraitSource(source);originalTexture=prefab.m_RawImage.texture;originalHudTexture=prefab.m_HudRawImage.texture;
                Type ownerType;IDictionary leases=WatchLeaseTable(out ownerType);foreach(object key in leases.Keys)priorLeases.Add((int)key);
                evidence=new JObject{{"request",command.DeepClone()},{"helper",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},
                    {"framework",ScaleIdentity(CatalogAssembly("FTKModFramework"))},{"gameAssembly",ScaleIdentity(typeof(uiEnemyEncounterPortrait).Assembly)},
                    {"armedEvidence",portraitEvidence.DeepClone()},{"uiPrefabInstanceId",prefab.GetInstanceID()},{"offscreenCameraInstanceId",camera==null?0:camera.GetInstanceID()}};
                Texture2D[] existingTextures=Resources.FindObjectsOfTypeAll<Texture2D>();
                if(existingTextures.Length>65536)throw new InvalidOperationException("Texture identity inventory exceeds65536.");
                foreach(Texture2D existing in existingTextures)if(existing!=null)priorTextures.Add(existing.GetInstanceID());
                owned=RowUi("FTK_OWNED_ROW_PORTRAIT_"+id,null);owned.SetActive(false);ui=owned.AddComponent<uiEnemyEncounterPortrait>();uiId=ui.GetInstanceID();
                ui.m_PortraitTarget=RowUi("PortraitTarget",owned.transform).transform;
                ui.m_EnemyLevelParent=RowUi("EnemyLevelParent",owned.transform);
                ui.m_EnemyLevel=RowUi("EnemyLevel",ui.m_EnemyLevelParent.transform).AddComponent<Text>();
                invoked=true;ui.Initialize(row.m_ID);returned=true;
                cacheResult=RowPortraitCacheAfter(cameras,cacheBefore,cameraKey,expectedWidth,expectedHeight,out camera);
                uiActiveTimePortrait portrait=typeof(uiEnemyEncounterPortrait).GetField("m_Portrait",Members).GetValue(ui)as uiActiveTimePortrait;
                if(portrait==null || !portrait.transform.IsChildOf(owned.transform) || portrait.m_RawImage==null)throw new InvalidOperationException("Native portrait not created under exact owned UI.");
                Texture2D created=portrait.m_RawImage.texture as Texture2D;
                if(created==null || !RowPortraitOwnership.Fresh(created.GetInstanceID(),created.width,created.height,priorTextures,expectedWidth,expectedHeight))
                    throw new InvalidOperationException("Fresh owned Texture2D with exact native row dimensions required.");
                pixels=created;textureId=pixels.GetInstanceID();
                foreach(JObject record in portraitTrace.Read())
                    if((string)record["row"]==row.m_ID && (string)record["session"]==sessionId
                        && (string)record["identityResolution"]=="exact_row_forwarded_to_native_source"
                        && (int)record["sourceCelInstanceId"]==source.GetInstanceID()
                        && (int)record["textureCurrent"]["instanceId"]==textureId && (int)record["textureRequested"]["instanceId"]==textureId)
                    {if(trace!=null)throw new InvalidOperationException("Ambiguous constructed row trace.");trace=record;}
                if(portraitTrace.Count!=1 || portraitErrors!=0 || !PortraitFinalization.Successful(trace))throw new InvalidOperationException("Exactly one complete finalized row-forwarded native trace required.");
                bool nativeCaller=false;foreach(JToken caller in (JArray)trace["callerMethods"])if((string)caller=="uiEnemyEncounterPortrait.Initialize")nativeCaller=true;
                if(!nativeCaller || trace["poseRequested"].Type!=JTokenType.Boolean || !(bool)trace["poseRequested"] || (bool)trace["target"]["traversalOrRenderersTruncated"])
                    throw new InvalidOperationException("Exact native UI pose caller and complete target inventory required.");
                foreach(JObject assignment in (JArray)portraitProfile["renderers"])
                {
                    int matches=0;foreach(JObject renderer in (JArray)trace["target"]["renderers"])
                        if((string)renderer["path"]==(string)assignment["rendererPath"])
                        {
                            matches++;
                            if((string)renderer["meshName"]!="ftkmf_glb_"+(string)assignment["glbFile"] || (bool)renderer["bonesTruncated"] || (bool)renderer["materialsTruncated"])
                                throw new InvalidOperationException("Expected complete custom GLB renderer identity missing in native portrait clone.");
                        }
                    if(matches!=1)throw new InvalidOperationException("Each configured row renderer must match exactly once on native portrait clone.");
                }
                JObject lease=trace["target"]["resourceLease"]as JObject;
                if(lease==null || !(bool)lease["available"] || !(bool)lease["present"] || !(bool)lease["applied"] || !(bool)lease["acquired"])
                    throw new InvalidOperationException("Constructed native portrait must hold applied custom mesh lease.");
                leaseId=(int)lease["leaseId"];leases=WatchLeaseTable(out ownerType);
                if(priorLeases.Contains(leaseId) || !leases.Contains(leaseId))throw new InvalidOperationException("Exact newly-created portrait lease required.");
                object leaseEntry=leases[leaseId];UnityEngine.Object[] values=leaseEntry.GetType().GetField("resources",Members).GetValue(leaseEntry)as UnityEngine.Object[];
                if(values==null || values.Length==0 || values.Length>256)throw new InvalidOperationException("Bounded owned lease resources required.");
                foreach(UnityEngine.Object value in values)
                {if(value==null || !(value is Mesh || value is Material || value is Texture))throw new InvalidOperationException("Unknown portrait owned resource.");resources.Add(new WatchedResource{value=value,instanceId=value.GetInstanceID(),name=value.name,type=value.GetType().FullName});}
                CharacterEventListener[] candidates=Resources.FindObjectsOfTypeAll<CharacterEventListener>();if(candidates.Length>4096)throw new InvalidOperationException("CEL inventory exceeds bound.");
                foreach(CharacterEventListener candidate in candidates)if(candidate!=null && candidate.GetInstanceID()==(int)trace["target"]["instanceId"]){clone=candidate.gameObject;cloneId=candidate.GetInstanceID();}
                if(clone==null)throw new InvalidOperationException("Trace-identified deferred native clone unavailable.");
                uiResult=new JObject{{"ownedUiInstanceId",uiId},{"portraitInstanceId",portrait.GetInstanceID()},
                    {"rawTexture",PortraitTexture(portrait.m_RawImage.texture)},{"hudTexture",PortraitTexture(portrait.m_HudRawImage.texture)},
                    {"sameHudTexture",portrait.m_HudRawImage.texture==pixels},{"levelText",ui.m_EnemyLevel.text},{"levelParentActive",ui.m_EnemyLevelParent.activeSelf},
                    {"rawUv",PortraitRect(portrait.m_RawImage.uvRect)},{"hudUv",PortraitRect(portrait.m_HudRawImage.uvRect)},
                    {"rawColor",PortraitColor(portrait.m_RawImage.color)},{"hudColor",PortraitColor(portrait.m_HudRawImage.color)}};
                byte[] png=pixels.EncodeToPNG();if(png==null || png.Length<24 || png.Length>4*1024*1024)throw new InvalidOperationException("PNG encoding failed/exceeds bound.");
                pin.Check(sessionId);RowPortraitAssetPins();after=RowPortraitSource(source);sourceUnchanged=JToken.DeepEquals(before,after);
                if(rawRect.anchorMin!=rawRect.anchorMax || RowPortraitOwnership.NativeDimension(rawRect.rect.width,uiActiveTime.Instance.m_OffscreenEnemyPortraitAA)!=expectedWidth || RowPortraitOwnership.NativeDimension(rawRect.rect.height,uiActiveTime.Instance.m_OffscreenEnemyPortraitAA)!=expectedHeight)throw new InvalidOperationException("Native row dimensions changed during call.");
                if(!sourceUnchanged || prefab.m_RawImage.texture!=originalTexture || prefab.m_HudRawImage.texture!=originalHudTexture || portrait.m_RawImage.texture!=pixels || portrait.m_HudRawImage.texture!=pixels || camera.m_TargetObject!=null || camera.m_Texture2D!=null)
                    throw new InvalidOperationException("Native source/UI texture/camera postcondition mismatch.");
                string path=Path.Combine(output,Token(id)+".row-portrait.png"),temporary=path+".tmp";
                if(File.Exists(path)||File.Exists(temporary))throw new IOException("Owned row portrait output exists.");
                using(FileStream stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))stream.Write(png,0,png.Length);
                File.Move(temporary,path);pngResult=new JObject{{"path",path},{"sha256",CatalogHash(path)},{"bytes",png.Length},{"width",expectedWidth},{"height",expectedHeight},{"textureInstanceId",textureId},{"exportFrame",Time.frameCount}};
            }
            catch(Exception failure){error=failure.ToString();}
        }
        finally
        {
            // Native exceptions may precede assignment capture; recover only textures on our own constructed UI.
            try
            {
                if(ui!=null && pixels==null)
                {
                    uiActiveTimePortrait portrait=typeof(uiEnemyEncounterPortrait).GetField("m_Portrait",Members).GetValue(ui)as uiActiveTimePortrait;
                    if(portrait!=null && portrait.transform.IsChildOf(owned.transform) && portrait.m_RawImage!=null)
                    {
                        Texture2D candidate=portrait.m_RawImage.texture as Texture2D;
                        if(candidate!=null && RowPortraitOwnership.Fresh(candidate.GetInstanceID(),candidate.width,candidate.height,priorTextures,expectedWidth,expectedHeight)){pixels=candidate;textureId=candidate.GetInstanceID();}
                    }
                }
            }
            catch(Exception failure){cleanupErrors.Add("texture ownership recovery: "+failure);}
            try
            {
                // Never call shared Stop on an unidentified partial target. Native normal success already stopped it.
                if(invoked && cameras!=null && cameraKey!=null)camera=cameras[cameraKey]as OffscreenCamera;
                if(camera!=null && camera.m_TargetObject!=null)unknownTarget=true;
                if(camera!=null && camera.m_Texture2D==pixels && pixels!=null)camera.m_Texture2D=null;
            }
            catch(Exception failure){cleanupErrors.Add("camera observation: "+failure);}
            try{if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
            catch(Exception failure){cleanupErrors.Add("owned texture destruction: "+failure);}
            try{if(owned!=null)UnityEngine.Object.Destroy(owned);}
            catch(Exception failure){cleanupErrors.Add("owned UI destruction: "+failure);}
            finally{RenderTexture.active=active;}
        }
        try
        {
            for(int step=0;step<4;step++)yield return null;
            try{pin.Check(sessionId);readyUnchanged=true;if(source!=null){after=RowPortraitSource(source);sourceUnchanged=JToken.DeepEquals(before,after);}}
            catch(Exception failure){cleanupErrors.Add(failure.ToString());}
            JArray observations=new JArray();bool allNull=resources.Count>0;
            foreach(WatchedResource resource in resources){bool gone=resource.value==null;allNull&=gone;observations.Add(new JObject{{"instanceId",resource.instanceId},{"name",resource.name},{"type",resource.type},{"unityNull",gone}});}
            bool absent=false;try{Type ownerType;absent=leaseId!=0 && !WatchLeaseTable(out ownerType).Contains(leaseId);}catch(Exception failure){cleanupErrors.Add(failure.ToString());}
            leaseResult=new JObject{{"leaseId",leaseId},{"wasNew",leaseId!=0&&!priorLeases.Contains(leaseId)},{"leaseAbsent",absent},{"allPinnedResourcesUnityNull",allNull},{"resources",observations},
                {"scope","Only exact newly-created row portrait lease assets; read-only refs, no lease retain/release/prune calls."}};
            bool cleanup=owned==null && pixels==null && clone==null && !unknownTarget && cleanupErrors.Count==0 && absent && allNull;
            Finish(id,new JObject{{"ok",error==null&&returned&&cleanup&&readyUnchanged&&sourceUnchanged},{"error",error},
                {"provenance","constructed_owned_native_uiEnemyEncounterPortrait_Initialize_string_call"},{"nativeInitializeInvoked",invoked},{"nativeInitializeReturned",returned},
                {"evidence",evidence},{"pinnedReady",pin==null?null:pin.View()},{"sameReadyAfter",readyUnchanged},{"sourceBefore",before},{"sourceAfter",after},{"sourceUnchanged",sourceUnchanged},
                {"passiveTrace",trace},{"portraitUi",uiResult},{"nativeDimensions",dimensions},{"nativeCameraCache",cacheResult},{"png",pngResult},{"leaseObservation",leaseResult},
                {"cleanup",new JObject{{"ownedUiUnityNull",owned==null},{"ownedTextureUnityNull",pixels==null},{"nativeCloneUnityNull",clone==null},{"cloneCelInstanceId",cloneId},
                    {"unknownPartialCameraTarget",unknownTarget},{"errors",cleanupErrors},{"complete",cleanup}}},
                {"limitations","Constructed native UI caller only, not an opened encounter menu or live EnemyDummy/HUD. One native Initialize; native portrait cache performs Snapshot; an absent exact entry may be created only by native Initialize and persists as native-owned cache. PNG is current owned UI texture. No retries. Unknown partial camera targets are not destroyed; cleanup-unproven fails. Exact source prefab TRS/mesh/material refs and Ready are compared. Lease disposal only for recorded newly-created preview resources; no art, cache reuse or all-process resource claim."}});
        }
        finally{resources.Clear();busy=false;}
    }
}
