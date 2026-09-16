using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GridEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class EnemyLifetimeOwner
    {
        internal Component owner;internal CharacterEventListener cel;internal Diorama diorama;
        internal int id,celId,leaseId;internal bool observedActive;
    }
    sealed class EnemyLifetimeLease
    {internal int id;internal readonly List<WatchedResource> resources=new List<WatchedResource>();}
    sealed class EnemyLifetimeSnapshot
    {
        internal JObject record;internal int leaseId,sourceOwnerId;internal FTKPlayerID fid;
        internal Texture2D texture;internal OffscreenCamera camera;internal RenderTexture renderTexture;internal int textureId,cameraId,renderTextureId;
    }
    sealed class EnemyLifetimeWatch
    {
        internal bool armed,captureAllowed=true,currentRowReferenceMatches=true;internal string armId,catalogHash;internal LeaseObservationPin pin;internal JObject profile,assets,prefab,registration,helper,content,gameAssembly;internal string bindingKind;internal int lastFullPinFrame,currentFrame;internal bool historyComplete=true,prefabMeasuredEqual=true,readoutInvalid;internal JObject prefabNow;
        internal FTK_enemyCombat row;internal CharacterEventListener prefabCel;
        internal readonly Dictionary<int,EnemyLifetimeOwner> owners=new Dictionary<int,EnemyLifetimeOwner>();
        internal readonly Dictionary<int,EnemyLifetimeLease> leases=new Dictionary<int,EnemyLifetimeLease>();
        internal readonly List<WatchedResource> nativeResources=new List<WatchedResource>();
        internal readonly List<EnemyLifetimeSnapshot> snapshots=new List<EnemyLifetimeSnapshot>();
        internal readonly JArray errors=new JArray(),changes=new JArray();internal JObject lastState;
    }
    EnemyLifetimeWatch enemyLifetime;
    JObject EnemyLifetimeRegistration(string enemy)
    {
        Type type=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.EnemyVisualPatch",true);
        IDictionary table=(IDictionary)type.GetField("_visuals",Statics).GetValue(null);if(!table.Contains(enemy))throw new InvalidOperationException("Enemy visual registration absent.");
        object visual=table[enemy];Type valueType=visual.GetType();Array assignments=(Array)valueType.GetField("rendererMeshes",Members).GetValue(visual);
        JObject result=new JObject{{"enemy",enemy},{"explicitAssignmentCount",assignments==null?0:assignments.Length}};
        foreach(string field in new[]{"glbMesh","glbTexture","meshBundle","meshName","meshTextureName"})result[field]=new JValue((object)(string)valueType.GetField(field,Members).GetValue(visual));
        foreach(string field in new[]{"proceduralBody","addLantern","swampAura","applyWetSkin","hideWeapon","legacyAbsoluteScale"})result[field]=(bool)valueType.GetField(field,Members).GetValue(visual);
        foreach(string field in new[]{"scale","widthBoost","smoothness","metallic","hunchDegrees"})result[field]=(float)valueType.GetField(field,Members).GetValue(visual);
        Color tint=(Color)valueType.GetField("tint",Members).GetValue(visual);result["tint"]=new JArray(tint.r,tint.g,tint.b,tint.a);
        JArray descriptorArray=new JArray();if(assignments!=null){if(assignments.Length>16)throw new InvalidOperationException("Registered renderer bound16 exceeded.");foreach(object assignment in assignments)
        {if(assignment==null)throw new InvalidOperationException("Null registered assignment.");Type t=assignment.GetType();JObject descriptor=new JObject{{"rendererPath",(string)t.GetProperty("RendererPath",Members).GetValue(assignment,null)},{"glbFile",(string)t.GetProperty("GlbFileName",Members).GetValue(assignment,null)},
            {"textureFile",new JValue((object)(string)t.GetProperty("TextureFileName",Members).GetValue(assignment,null))},{"disableNativeEmission",(bool)t.GetProperty("DisableNativeEmission",Members).GetValue(assignment,null)}};
            Array slots=(Array)t.GetField("_materialSlots",Members).GetValue(assignment);if(slots!=null){if(slots.Length>4)throw new InvalidOperationException("Registered material slot bound4 exceeded.");JArray data=new JArray();foreach(object slot in slots){if(slot==null)throw new InvalidOperationException("Null registered material slot.");Type st=slot.GetType();data.Add(new JObject{{"primitiveIndex",(int)st.GetProperty("PrimitiveIndex",Members).GetValue(slot,null)},{"nativeMaterialSlot",(int)st.GetProperty("NativeMaterialSlot",Members).GetValue(slot,null)},
                {"textureFile",new JValue((object)(string)st.GetProperty("TextureFileName",Members).GetValue(slot,null))},{"disableNativeEmission",(bool)st.GetProperty("DisableNativeEmission",Members).GetValue(slot,null)}});}descriptor["materialSlots"]=data;}descriptorArray.Add(descriptor);}}
        result["renderers"]=descriptorArray;
        result["bindingKind"]=assignments!=null&&assignments.Length>0?"explicit-plural":string.IsNullOrEmpty((string)result["glbMesh"])?"no-glb":"legacy-singular";
        return result;
    }
    JObject EnemyLifetimeAssets(JObject profile)
    {
        JObject assets=new JObject();JArray assignments=profile["renderers"]as JArray;
        if(assignments==null||assignments.Count<1||assignments.Count>16)throw new InvalidOperationException("Expected1..16 explicit model renderer assignments.");
        foreach(JObject r in assignments)
        {
            List<string> files=new List<string>{Str(r,"glbFile")};string texture=Str(r,"textureFile");if(texture!=null)files.Add(texture);
            MaterialSlotFixture.Slot[] slots=MaterialSlotFixture.Read(r);if(slots!=null)foreach(var slot in slots)if(slot.TextureFile!=null)files.Add(slot.TextureFile);
            foreach(string file in files)
            {
                AssetName(file,file==Str(r,"glbFile")?".glb":".png");string path=CatalogAsset(CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.CustomModelLoader",true).GetMethod("ResolveModelPath",Statics,null,new[]{typeof(string)},null),file,file==Str(r,"glbFile")?".glb":".png");
                assets[file]=CatalogHash(path);
            }
        }
        return assets;
    }
    void EnemyLifetimePins()
    {
        if(enemyLifetime==null)throw new InvalidOperationException("No enemy lifetime watch.");
        try{EnemyLifetimePolicy.CheckIdentity(ref enemyLifetime.readoutInvalid,EnemyLifetimePinsCore);}catch{enemyLifetime.captureAllowed=false;throw;}
    }
    void EnemyLifetimePinsCore()
    {
        EnemyLifetimeWatch w=enemyLifetime;if(w==null)throw new InvalidOperationException("No enemy lifetime watch.");
        CatalogNoLinks(root);RequireSinglePlayer();w.pin.Check(root,sessionId,PreviewCoreIdentity());
        if(!JToken.DeepEquals(w.helper,ScaleIdentity(typeof(RuntimeModelTest).Assembly))||!JToken.DeepEquals(w.content,ScaleIdentity(CatalogAssembly("FtkRuntimeModelTestContent")))||!JToken.DeepEquals(w.gameAssembly,ScaleIdentity(typeof(EnemyDummy).Assembly)))throw new InvalidOperationException("Lifetime helper/content/native module pins changed.");
        w.currentRowReferenceMatches=ReferenceEquals(FTK_enemyCombatDB.GetDB().GetEntryByStringID(w.row.m_ID),w.row)&&w.row.m_EnemyAsset==w.prefabCel;
        w.captureAllowed=EnemyLifetimePolicy.Attribution(w.captureAllowed,w.currentRowReferenceMatches);
        if(w.captureAllowed && !JToken.DeepEquals(w.registration,EnemyLifetimeRegistration(w.row.m_ID)))throw new InvalidOperationException("Actual visual registration changed.");
        if(portraitArmId!=w.armId||portraitRow!=w.row||
            CatalogHash(Path.Combine(root,"model-test-profiles.json"))!=w.catalogHash||!JToken.DeepEquals(w.assets,EnemyLifetimeAssets(w.profile)))
            throw new InvalidOperationException("Enemy lifetime row/portrait/catalog/assets changed; observations cannot be attributed.");
        w.lastFullPinFrame=Time.frameCount;
        if(w.prefabCel!=null){w.prefabNow=PortraitCel(w.prefabCel,true);w.prefabMeasuredEqual=JToken.DeepEquals(w.prefab,w.prefabNow);if(!w.prefabMeasuredEqual)EnemyLifetimeError("source-prefab-invariance",new InvalidOperationException("Measured native prefab fields changed; resource readout remains diagnostic only."));}else{w.prefabNow=null;w.prefabMeasuredEqual=false;}
    }
    void EnemyLifetimeError(string at,Exception error)
    {
        EnemyLifetimeWatch w=enemyLifetime;if(w==null)return;w.armed=false;w.captureAllowed=false;
        foreach(JObject prior in w.errors)if(Str(prior,"at")==at&&Str(prior,"error")==error.GetType().Name+": "+error.Message)return;
        if(w.errors.Count<16)w.errors.Add(new JObject{{"frame",Time.frameCount},{"at",at},{"error",error.GetType().Name+": "+error.Message}});
    }
    static WatchedResource EnemyLifetimeResource(UnityEngine.Object value)
    {if(value==null)throw new InvalidOperationException("Native resource unexpectedly null.");return new WatchedResource{value=value,instanceId=value.GetInstanceID(),name=value.name,type=value.GetType().FullName};}
    static bool EnemyLifetimeContains(List<WatchedResource> resources,UnityEngine.Object value)
    {foreach(WatchedResource item in resources)if(ReferenceEquals(item.value,value))return true;return false;}
    int EnemyLifetimeResourceCount()
    {int count=enemyLifetime.nativeResources.Count;foreach(var lease in enemyLifetime.leases.Values)count+=lease.resources.Count;return count;}
    JObject EnemyLifetimeLeaseView(int id)
    {
        EnemyLifetimeWatch w=enemyLifetime;Type ownerType;IDictionary table=WatchLeaseTable(out ownerType);bool present=table.Contains(id);
        EnemyLifetimeLease watched;
        if(!w.leases.TryGetValue(id,out watched))
        {if(w.leases.Count>=8)throw new InvalidOperationException("Enemy lifetime lease limit8 exceeded.");watched=new EnemyLifetimeLease{id=id};w.leases.Add(id,watched);}
        JArray currentOwners=new JArray(),currentResources=new JArray();int references=0;
        if(present)
        {
            object lease=table[id];IList owners=lease.GetType().GetField("owners",Members).GetValue(lease)as IList;
            UnityEngine.Object[] resources=lease.GetType().GetField("resources",Members).GetValue(lease)as UnityEngine.Object[];
            if(owners==null||resources==null||owners.Count>8||resources.Length>256)throw new InvalidOperationException("Lease owners/resources missing or over bound.");
            references=(int)lease.GetType().GetField("references",Members).GetValue(lease);
            foreach(object value in owners)
            {
                Component owner=value as Component;
                if(ReferenceEquals(owner,null))throw new InvalidOperationException("Non-component lease owner.");
                int ownerId=0;foreach(var known in w.owners.Values)if(ReferenceEquals(known.owner,owner))ownerId=known.id;
                if(ownerId==0){if(owner==null)throw new InvalidOperationException("Unknown destroyed lease owner prevents complete attribution.");ownerId=owner.GetInstanceID();}currentOwners.Add(ownerId);
                if(!w.owners.ContainsKey(ownerId))
                {
                    if(w.owners.Count>=8||owner==null)throw new InvalidOperationException("Owner limit8 or newly encountered destroyed owner prevents full attribution.");
                    CharacterEventListener cel=owner.GetComponent<CharacterEventListener>();if(cel==null)throw new InvalidOperationException("Registered owner has no native CEL.");
                    w.owners.Add(ownerId,new EnemyLifetimeOwner{owner=owner,cel=cel,id=ownerId,celId=cel.GetInstanceID(),leaseId=id,observedActive=cel.gameObject.activeInHierarchy,diorama=EncounterSession.Instance==null?null:EncounterSession.Instance.m_ActiveDiorama});
                }
                else if(w.owners[ownerId].leaseId!=id||!ReferenceEquals(w.owners[ownerId].owner,owner))throw new InvalidOperationException("Owner ID/lease collision.");
            }
            foreach(UnityEngine.Object resource in resources)
            {
                if(resource==null||!(resource is Mesh||resource is Material||resource is Texture))throw new InvalidOperationException("Live model lease contains absent/unsupported resource.");
                currentResources.Add(resource.GetInstanceID());
                if(!EnemyLifetimeContains(watched.resources,resource))
                {
                    foreach(var previous in watched.resources)if(previous.instanceId==resource.GetInstanceID())throw new InvalidOperationException("Resource instance ID reused within watched lease.");
                    foreach(var other in w.leases.Values)if(other.id!=id&&EnemyLifetimeContains(other.resources,resource))throw new InvalidOperationException("One resource appears owned by multiple independent leases.");
                    if(EnemyLifetimeResourceCount()>=256)throw new InvalidOperationException("Total resource reference limit256 exceeded.");
                    if(EnemyLifetimeContains(w.nativeResources,resource))throw new InvalidOperationException("Native prefab resource unexpectedly adopted by model lease.");
                    watched.resources.Add(EnemyLifetimeResource(resource));
                }
            }
        }
        return new JObject{{"leaseId",id},{"present",present},{"references",references},{"registeredOwnerIds",currentOwners},{"currentResourceIds",currentResources},{"observedResourceUnion",EnemyLifetimeResourceViews(watched.resources)}};
    }
    static JArray EnemyLifetimeResourceViews(List<WatchedResource> resources)
    {JArray values=new JArray();foreach(var r in resources)values.Add(new JObject{{"instanceId",r.instanceId},{"name",r.name},{"type",r.type},{"unityNull",r.value==null}});return values;}
    JObject EnemyLifetimeCel(CharacterEventListener cel)
    {
        if(cel==null)throw new InvalidOperationException("Expected existing native CEL.");
        Type ownerType;IDictionary table=WatchLeaseTable(out ownerType);Component owner=cel.GetComponent(ownerType);
        if(owner==null)throw new InvalidOperationException("Public-bound native CEL has no resource owner.");
        int id=(int)ownerType.GetField("_leaseId",Members).GetValue(owner);Renderer[] targets=ownerType.GetField("_targets",Members).GetValue(owner)as Renderer[];
        if(id==0||targets==null||targets.Length>32)throw new InvalidOperationException("Missing lease ID/target table or target bound32 exceeded.");
        JArray targetIds=new JArray(),renderers=new JArray();
        foreach(Renderer target in targets)
        {if(target==null||(target.transform!=cel.transform&&!target.transform.IsChildOf(cel.transform)))throw new InvalidOperationException("Owned renderer references not remapped inside this CEL.");targetIds.Add(target.GetInstanceID());}
        int bones=0;
        foreach(JObject assignment in (JArray)enemyLifetime.profile["renderers"])
        {
            SkinnedMeshRenderer found=null;foreach(var r in cel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(Relative(r.transform,cel.transform)==Str(assignment,"rendererPath")){if(found!=null)throw new InvalidOperationException("Ambiguous model renderer.");found=r;}
            if(found==null||found.sharedMesh==null)throw new InvalidOperationException("Configured native renderer unavailable; cannot attribute body.");
            if(enemyLifetime.bindingKind=="legacy-singular")
            {SkinnedMeshRenderer heuristic=null;foreach(Transform node in cel.GetComponentsInChildren<Transform>(true))if(node.name=="enTroll01"){heuristic=node.GetComponent<SkinnedMeshRenderer>();break;}
                if(heuristic==null)heuristic=cel.GetComponentInChildren<SkinnedMeshRenderer>(true);if(heuristic!=found)throw new InvalidOperationException("Legacy native target heuristic differs from explicit observation metadata.");}
            bool owned=false;foreach(Renderer target in targets)if(target==found)owned=true;if(!owned && enemyLifetime.bindingKind!="legacy-singular")throw new InvalidOperationException("Configured GLB renderer not owned by native lease.");
            JArray boneIds=new JArray();foreach(Transform bone in found.bones){if(++bones>256||bone==null||(bone!=cel.transform&&!bone.IsChildOf(cel.transform)))throw new InvalidOperationException("Bone bound/remapping failure.");boneIds.Add(bone.GetInstanceID());}
            Material[] materials=found.sharedMaterials;if(materials.Length>16)throw new InvalidOperationException("Material slot bound16 exceeded.");JArray mats=new JArray();
            foreach(Material material in materials)
            {if(material==null){mats.Add(new JValue((object)null));continue;}JObject value=new JObject{{"id",material.GetInstanceID()},{"shaderId",material.shader==null?0:material.shader.GetInstanceID()},{"shader",material.shader==null?null:material.shader.name},{"mainTextureId",material.HasProperty("_MainTex")&&material.GetTexture("_MainTex")!=null?material.GetTexture("_MainTex").GetInstanceID():0}};
                foreach(string property in new[]{"_Color","_EmissionColor"}){value[property+"Present"]=material.HasProperty(property);if(material.HasProperty(property)){Color color=material.GetColor(property);value[property]=new JArray(color.r,color.g,color.b,color.a);}}mats.Add(value);}

            renderers.Add(new JObject{{"rendererPath",Str(assignment,"rendererPath")},{"rendererId",found.GetInstanceID()},{"meshId",found.sharedMesh.GetInstanceID()},{"meshName",found.sharedMesh.name},{"expectedCustomMesh",found.sharedMesh.name=="ftkmf_glb_"+Str(assignment,"glbFile")},{"targetMember",owned},{"boneIds",boneIds},{"boneSignature",BoneSignature(found)},{"materials",mats}});
        }
        if(!enemyLifetime.owners.ContainsKey(owner.GetInstanceID()))
        {
            if(enemyLifetime.owners.Count>=8)throw new InvalidOperationException("Owner diagnostic bound8 exceeded.");
            enemyLifetime.owners.Add(owner.GetInstanceID(),new EnemyLifetimeOwner{owner=owner,cel=cel,id=owner.GetInstanceID(),celId=cel.GetInstanceID(),leaseId=id,observedActive=cel.gameObject.activeInHierarchy,diorama=EncounterSession.Instance==null?null:EncounterSession.Instance.m_ActiveDiorama});
        }
        enemyLifetime.owners[owner.GetInstanceID()].observedActive|=cel.gameObject.activeInHierarchy;
        JObject lease=EnemyLifetimeLeaseView(id);JArray resourceIds=(JArray)lease["currentResourceIds"];
        foreach(JObject renderer in renderers)
        {bool meshOwned=false;foreach(JToken resource in resourceIds)if((int)resource==(int)renderer["meshId"])meshOwned=true;renderer["meshInLease"]=meshOwned;
            foreach(JToken token in (JArray)renderer["materials"]){JObject material=token as JObject;if(material==null)continue;bool matOwned=false,textureOwned=false;foreach(JToken resource in resourceIds){if((int)resource==(int)material["id"])matOwned=true;if((int)resource==(int)material["mainTextureId"])textureOwned=true;}material["materialInLease"]=matOwned;material["mainTextureInLease"]=textureOwned;}}
        FieldInfo resourceOnly=ownerType.GetField("VisualResourcesOnly",Members);
        JArray other=new JArray();foreach(Renderer renderer in cel.GetComponentsInChildren<Renderer>(true))
        {if(other.Count>=64)throw new InvalidOperationException("Other live renderer bound64 exceeded.");JArray materials=new JArray();Material[] shared=renderer.sharedMaterials;if(shared.Length>16)throw new InvalidOperationException("Other material slot bound16 exceeded.");foreach(Material material in shared){bool inLease=false;if(material!=null)foreach(JToken rid in resourceIds)if((int)rid==material.GetInstanceID())inLease=true;materials.Add(new JObject{{"materialId",material==null?0:material.GetInstanceID()},{"inLease",inLease}});}other.Add(new JObject{{"rendererId",renderer.GetInstanceID()},{"path",Relative(renderer.transform,cel.transform)},{"active",renderer.gameObject.activeInHierarchy},{"enabled",renderer.enabled},{"materials",materials},{"nativeScrollerCount",renderer.GetComponents<ScrollingUVs>().Length}});}
        return new JObject{{"celId",cel.GetInstanceID()},{"ownerId",owner.GetInstanceID()},{"leaseId",id},
            {"activeSelf",cel.gameObject.activeSelf},{"activeInHierarchy",cel.gameObject.activeInHierarchy},{"animRootId",cel.m_AnimRoot==null?0:cel.m_AnimRoot.GetInstanceID()},
            {"visualResourcesOnly",resourceOnly==null?new JValue((object)null):new JValue((bool)resourceOnly.GetValue(owner))},{"visualResourcesOnlyFieldPresent",resourceOnly!=null},{"allExistingRenderers",other},{"applied",(bool)ownerType.GetField("Applied",Members).GetValue(owner)},{"acquired",(bool)ownerType.GetField("_acquired",Members).GetValue(owner)},
            {"targetIds",targetIds},{"renderers",renderers},{"lease",lease}};
    }
    EnemyLifetimeSnapshot EnemyLifetimeBegin(PortraitScope scope,OffscreenCamera camera)
    {
        EnemyLifetimeWatch w=enemyLifetime;if(w==null||!w.armed||scope.identity!="verified_live_enemy_dummy")return null;
        bool nativeHud=false;foreach(JToken caller in scope.callers)if((string)caller=="uiActiveTime.FillPortraitTextures")nativeHud=true;if(!nativeHud)return null;
        EnemyLifetimePins();if(!w.captureAllowed)return null;
        if(w.snapshots.Count>=8)throw new InvalidOperationException("Native HUD snapshot limit8 exceeded.");
        EnemyLifetimeSnapshot item=new EnemyLifetimeSnapshot{camera=camera,texture=scope.texture,record=new JObject{{"session",sessionId},{"armCommandId",w.armId},{"prefixFrame",Time.frameCount},{"callerMethods",scope.callers.DeepClone()},{"nativeFinalized",false}}};w.snapshots.Add(item);scope.lifetime=item;
        EnemyLifetimePins();EnemyDummy dummy=scope.source.m_Dummy as EnemyDummy;int matches=0;
        if(dummy!=null&&EncounterSession.Instance!=null)foreach(EnemyDummy actual in EncounterSession.Instance.m_EnemyDummies.Values)if(actual==dummy)matches++;
        EnemyDummy mapped=null;if(dummy!=null&&EncounterSession.Instance!=null)EncounterSession.Instance.m_EnemyDummies.TryGetValue(dummy.FID,out mapped);
        if(matches!=1||mapped!=dummy||dummy.m_EventListener!=scope.source||!ReferenceEquals(dummy.m_EnemyCombat,w.row)||dummy.m_EnemyType!=w.row.m_ID)throw new InvalidOperationException("Exact actual native HUD enemy membership missing.");
        item.fid=dummy.FID;item.record["enemyDummyId"]=dummy.GetInstanceID();item.record["fid"]=new JObject{{"photonId",dummy.FID.m_PhotonID},{"turnIndex",dummy.FID.m_TurnIndex}};
        item.record["sourceMeasuredBefore"]=PortraitCel(scope.source,true);item.record["bindingKind"]=w.bindingKind;item.record["sourceBeforeClone"]=EnemyLifetimeCel(scope.source);item.leaseId=(int)item.record["sourceBeforeClone"]["leaseId"];item.sourceOwnerId=(int)item.record["sourceBeforeClone"]["ownerId"];
        item.textureId=scope.texture==null?0:scope.texture.GetInstanceID();item.cameraId=camera.GetInstanceID();item.renderTexture=camera.m_RenderTexture;item.renderTextureId=item.renderTexture==null?0:item.renderTexture.GetInstanceID();
        item.record["requestedHudTexture"]=PortraitTexture(scope.texture);item.record["cameraId"]=item.cameraId;item.record["nativeRenderTexture"]=PortraitTexture(item.renderTexture);
        if(scope.texture==null)throw new InvalidOperationException("Actual native HUD texture required.");
        return item;
    }
    void EnemyLifetimeBeforeRender(EnemyLifetimeSnapshot item,OffscreenCamera camera)
    {
        if(item==null)return;EnemyLifetimePins();if(!enemyLifetime.captureAllowed)throw new InvalidOperationException("Source attribution stopped after native registry replacement.");CharacterEventListener clone=camera.m_TargetObject==null?null:camera.m_TargetObject.GetComponent<CharacterEventListener>();
        item.record["beforeRenderFrame"]=Time.frameCount;item.record["cloneBeforeRender"]=EnemyLifetimeCel(clone);
        item.record["currentHudTexture"]=PortraitTexture(camera.m_Texture2D);
        EnemyLifetimePolicy.Compare(item.record);
    }
    void EnemyLifetimeFinalized(EnemyLifetimeSnapshot item,Exception error)
    {
        if(item==null)return;
        item.record["nativeFinalized"]=true;item.record["finalizedFrame"]=Time.frameCount;item.record["nativeException"]=error==null?new JValue((object)null):new JValue(error.GetType().FullName+": "+error.Message);
        if(enemyLifetime.readoutInvalid){item.record["afterStateObservationUnavailable"]="Prior identity/file pin failure; no fresh state read.";if(error!=null)EnemyLifetimeError("native-Snapshot",error);return;}
        item.record["cameraTargetCleared"]=item.camera!=null&&item.camera.m_TargetObject==null;item.record["cameraTextureCleared"]=item.camera!=null&&item.camera.m_Texture2D==null;
        if(error!=null)EnemyLifetimeError("native-Snapshot",error);
        EnemyLifetimeOwner source;if(enemyLifetime.owners.TryGetValue(item.sourceOwnerId,out source))
        {JObject after=source.cel==null?null:PortraitCel(source.cel,true);item.record["sourceMeasuredAfter"]=after;bool unchanged=EnemyLifetimePolicy.SourceUnchanged(item.record["sourceMeasuredBefore"]as JObject,after);item.record["sourceMeasuredFieldsEqual"]=unchanged;
            item.record["sourceInvarianceBoundary"]="PortraitCel emitted transforms/mesh/material references and listed controller metadata; excludes resourceLease because native clone acquisition legitimately changes it. Not every shader property.";
            if(!unchanged)EnemyLifetimeError("native-source-invariance",new InvalidOperationException("Measured source CEL fields changed across native Snapshot."));}

    }
    JObject EnemyLifetimeCurrent()
    {
        var w=enemyLifetime;JArray leases=new JArray(),owners=new JArray(),hud=new JArray();
        foreach(var lease in new List<EnemyLifetimeLease>(w.leases.Values))leases.Add(EnemyLifetimeLeaseView(lease.id));
        foreach(var owner in w.owners.Values)
        {
            bool gone=owner.owner==null,celGone=owner.cel==null;bool active=!celGone&&owner.cel.gameObject.activeInHierarchy;owner.observedActive|=active;
            bool inCleanup=false;if(owner.diorama!=null)
            {
                if(owner.diorama.m_DioramaCleanup!=null&&owner.diorama.m_DioramaCleanup.Count>512)throw new InvalidOperationException("Native diorama cleanup list missing/over512.");
                if(!celGone&&owner.diorama.m_DioramaCleanup!=null)inCleanup=owner.diorama.m_DioramaCleanup.Contains(owner.cel.gameObject);
            }
            owners.Add(new JObject{{"ownerId",owner.id},{"celId",owner.celId},{"leaseId",owner.leaseId},{"ownerUnityNull",gone},{"celUnityNull",celGone},{"activeNow",active},{"everObservedActive",owner.observedActive},{"dioramaUnityNull",owner.diorama==null},{"dioramaCleanupListAvailable",owner.diorama!=null&&owner.diorama.m_DioramaCleanup!=null},{"inObservedDioramaCleanup",inCleanup}});
        }
        foreach(var item in w.snapshots)
        {
            Texture texture=null;uiActiveTime timeline=uiActiveTime.Instance;bool present=timeline!=null&&timeline.m_PortaitTextures!=null&&timeline.m_PortaitTextures.TryGetValue(item.fid,out texture);
            hud.Add(new JObject{{"prefixFrame",item.record["prefixFrame"]},{"textureId",item.textureId},{"textureUnityNull",item.texture==null},{"dictionaryContainsFid",present},{"dictionaryMatchesPinnedTexture",present&&texture==item.texture&&texture!=null},
                {"cameraId",item.cameraId},{"renderTextureId",item.renderTextureId},{"renderTextureUnityNull",item.renderTexture==null},{"cameraStillUsesPinnedRenderTexture",item.camera!=null&&item.camera.m_RenderTexture==item.renderTexture&&item.renderTexture!=null},{"cameraUnityNull",item.camera==null},{"cameraTargetCleared",item.camera!=null&&item.camera.m_TargetObject==null},{"cameraTextureCleared",item.camera!=null&&item.camera.m_Texture2D==null}});
        }
        return new JObject{{"leases",leases},{"owners",owners},{"hudTextures",hud},{"nativeResources",EnemyLifetimeResourceViews(w.nativeResources)}};
    }
    void EnemyLifetimeTick()
    {
        var w=enemyLifetime;if(w==null||w.readoutInvalid)return;
        try
        {
            EnemyLifetimePolicy.CheckIdentity(ref w.readoutInvalid,delegate
            {RequireSinglePlayer();CatalogNoLinks(root);
                if(w.pin.root!=root||w.pin.session!=sessionId||portraitArmId!=w.armId)throw new InvalidOperationException("Lifetime identity changed.");});
            if(portraitTrace.Count>=PortraitTraceState<PortraitScope,JObject>.Capacity){w.historyComplete=false;EnemyLifetimeError("portrait-capacity",new InvalidOperationException("Portrait trace saturated; new attribution stopped, existing resource readout continues."));}
            if(portraitErrors>0)EnemyLifetimeError("portrait-telemetry",new InvalidOperationException("Existing portrait trace reported telemetry errors; new attribution stopped."));
            if(w.snapshots.Count==0)return;
            JObject current=EnemyLifetimeCurrent();
            if(!EnemyLifetimePolicy.Record(w.changes,ref w.lastState,ref w.currentFrame,current,Time.frameCount,512))
            {w.historyComplete=false;EnemyLifetimeError("history-capacity",new InvalidOperationException("Lifetime history full; fresh current readout continues without additional history."));}

        }
        catch(Exception e){EnemyLifetimeError("passive-update",e);}
    }
    JObject ArmEnemyLifetime(JObject command)
    {
        CatalogKeys(command,"id","session","op","enemy","catalogSha256","bindingKind");RequireReadyPreparation();CatalogNoLinks(root);
        if(enemyLifetime!=null||portraitArmed||(spawnCapture!=null&&!spawnCapture.terminal))throw new InvalidOperationException("Clear previous lifetime watch and stop any existing portrait arm first.");
        string hash=CatalogHash(Path.Combine(root,"model-test-profiles.json"));if(hash!=Str(command,"catalogSha256"))throw new InvalidOperationException("Exact current catalog hash required.");
        JObject profile=null;foreach(JObject p in CatalogProfiles(false))if(Str(p,"key")==Str(command,"enemy")){if(profile!=null)throw new InvalidOperationException("Ambiguous profile.");profile=p;}
        if(profile==null)throw new InvalidOperationException("Exact model profile missing.");
        JObject registration=EnemyLifetimeRegistration(Str(command,"enemy"));string binding=Str(command,"bindingKind");
        EnemyLifetimePolicy.Registration(binding,registration,profile);
        JObject assets=EnemyLifetimeAssets(profile);
        FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID(Str(command,"enemy"));if(row==null||row.m_EnemyAsset==null)throw new InvalidOperationException("Registered native row source unavailable.");
        var w=new EnemyLifetimeWatch{armed=true,armId=Str(command,"id"),catalogHash=hash,pin=new LeaseObservationPin(root,sessionId,Str(command,"id"),PreviewCoreIdentity()),profile=(JObject)profile.DeepClone(),registration=registration,bindingKind=binding,helper=ScaleIdentity(typeof(RuntimeModelTest).Assembly),content=ScaleIdentity(CatalogAssembly("FtkRuntimeModelTestContent")),gameAssembly=ScaleIdentity(typeof(EnemyDummy).Assembly),assets=assets,row=row,prefabCel=row.m_EnemyAsset};
        if(Str(w.gameAssembly,"assemblyFileSha256")!=KrakenControllerGameAssembly)throw new InvalidOperationException("Native source audit assembly mismatch.");
        w.prefab=PortraitCel(w.prefabCel,true);
        foreach(JObject renderer in (JArray)w.prefab["renderers"])if((bool)renderer["bonesTruncated"]||(bool)renderer["materialsTruncated"])throw new InvalidOperationException("Native prefab bone/material proof truncated.");
        if((bool)w.prefab["traversalOrRenderersTruncated"])throw new InvalidOperationException("Native prefab bounds exceed observer capacity.");
        if(binding=="legacy-singular"&&w.prefabCel.GetComponentsInChildren<ScrollingUVs>(true).Length!=0)throw new InvalidOperationException("First singular lifetime trial requires a no-scroller native prefab.");
        foreach(var r in w.prefabCel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            List<UnityEngine.Object> references=new List<UnityEngine.Object>();if(r.sharedMesh!=null)references.Add(r.sharedMesh);
            foreach(Material m in r.sharedMaterials)if(m!=null){references.Add(m);if(m.shader!=null)references.Add(m.shader);foreach(string prop in new[]{"_MainTex","_EmissionMap"})if(m.HasProperty(prop)&&m.GetTexture(prop)!=null)references.Add(m.GetTexture(prop));}
            foreach(UnityEngine.Object resource in references)if(!EnemyLifetimeContains(w.nativeResources,resource)){if(w.nativeResources.Count>=256)throw new InvalidOperationException("Native resource bound256 exceeded.");w.nativeResources.Add(EnemyLifetimeResource(resource));}
        }
        Animator animator=w.prefabCel.m_Animator;
        if(animator!=null)foreach(UnityEngine.Object resource in new UnityEngine.Object[]{animator.runtimeAnimatorController,animator.avatar})
            if(resource!=null&&!EnemyLifetimeContains(w.nativeResources,resource)){if(w.nativeResources.Count>=256)throw new InvalidOperationException("Native resource bound256 exceeded.");w.nativeResources.Add(EnemyLifetimeResource(resource));}
        try{PortraitWatch(new JObject{{"id",Str(command,"id")},{"session",sessionId},{"op","portrait-watch"},{"enemy",Str(command,"enemy")}});}
        catch{if(portraitArmId==w.armId)portraitArmed=false;throw;}
        if(portraitRow!=w.row||portraitRowSource!=w.prefabCel){portraitArmed=false;throw new InvalidOperationException("Native portrait arm changed source identity.");}
        enemyLifetime=w;return EnemyLifetimeState();
    }
    JObject EnemyLifetimeState()
    {
        var w=enemyLifetime;if(w==null)throw new InvalidOperationException("No enemy lifetime watch.");
        bool pinsValid=true;try{EnemyLifetimePins();EnemyLifetimeTick();}catch(Exception e){pinsValid=false;EnemyLifetimeError("state-pins",e);}
        JArray snapshots=new JArray(),hudImages=new JArray();foreach(var item in w.snapshots)
        {snapshots.Add(item.record.DeepClone());if(!pinsValid||w.readoutInvalid)continue;
            try{hudImages.Add(new JObject{{"frame",Time.frameCount},{"textureId",item.textureId},{"activeRawImages",item.texture==null?new JArray():PortraitActiveImages(item.texture)}});}
            catch(Exception error){EnemyLifetimeError("hud-image-observation",error);hudImages.Add(new JObject{{"frame",Time.frameCount},{"textureId",item.textureId},{"error",error.ToString()}});}}

        return new JObject{{"ok",w.errors.Count==0},{"readoutPinsValid",pinsValid&&!w.readoutInvalid},{"readoutPermanentlyInvalid",w.readoutInvalid},{"armed",w.armed},{"newCaptureAllowed",w.captureAllowed},{"currentRowReferenceMatches",w.currentRowReferenceMatches},{"provenance","passive_actual_enemy_HUD_lease_lifetime"},{"pin",w.pin.View()},
            {"bindingKind",w.bindingKind},{"readoutContinuesAfterDiagnosticFailure",true},{"registration",w.registration.DeepClone()},{"helper",w.helper.DeepClone()},{"content",w.content.DeepClone()},{"gameAssembly",w.gameAssembly.DeepClone()},{"lastFullPinFrame",w.lastFullPinFrame},{"profile",w.profile.DeepClone()},{"catalogSha256",w.catalogHash},{"assets",w.assets.DeepClone()},{"snapshots",snapshots},{"hudImageObservations",hudImages},{"changes",w.changes.DeepClone()},{"errors",w.errors.DeepClone()},
            {"sourcePrefabBefore",w.prefab.DeepClone()},{"sourcePrefabNow",w.prefabNow==null?null:w.prefabNow.DeepClone()},{"measuredPrefabFieldsEqual",w.prefabMeasuredEqual},{"sourceInvarianceBoundary","Only fields emitted by PortraitCel (transforms, mesh/material references, names, texture dimensions and listed controller data) were compared; no assertion about every shader property."},{"historyComplete",w.historyComplete},{"freshCurrentFrame",w.currentFrame},
            {"lastState",w.lastState==null?null:w.lastState.DeepClone()},{"limits",new JObject{{"owners",8},{"leases",8},{"resourcesIncludingNative",256},{"hudSnapshots",8},{"stateChanges",512}}},
            {"boundary","Read-only Unity wrappers and raw lease table. No retain/prune/render/Destroy/cleanup invocation. Never-active acquisition, source-first order and all-process leaks are not inferred. Native HUD output/cache lifetimes are separate. Final disposal requires observed owner/CEL disappearance and complete pinned resource union Unity-null, independently audited."}};
    }
    JObject ClearEnemyLifetime()
    {
        if(enemyLifetime!=null&&portraitArmId==enemyLifetime.armId)portraitArmed=false;
        enemyLifetime=null;return new JObject{{"ok",true},{"status","enemy-lifetime-managed-references-cleared"},{"note","No Unity object or lease mutation."}};
    }
}
