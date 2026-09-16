using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    const string MaterialLifecycleCore="19ece392cf3d58fe43b6178733a9a5ecd85c51943f9cd1769060b88fb572b735";
    sealed class MaterialLineage
    {
        public readonly JArray frames=new JArray(),assertions=new JArray(),cleanupErrors=new JArray();
        readonly List<int> pinnedIds=new List<int>();readonly List<string> pinnedNames=new List<string>(),pinnedTypes=new List<string>();
        public readonly List<GameObject> roots=new List<GameObject>();
        public readonly List<UnityEngine.Object> original=new List<UnityEngine.Object>(),pinned=new List<UnityEngine.Object>();
        readonly Type ownerType;readonly MethodInfo resources,targets,scrollTarget,prefix;
        readonly FieldInfo leaseId,acquired,applied,scrollRenderers,scrollMaterials,scrollCounts,scrollers,offset;
        readonly IDictionary leases;readonly int layer;readonly string id;readonly int order;
        readonly List<int> parents=new List<int>();
        readonly List<SkinnedMeshRenderer> renderers=new List<SkinnedMeshRenderer>();
        readonly List<ScrollingUVs> scrolling=new List<ScrollingUVs>();
        readonly Dictionary<int,JObject> previous=new Dictionary<int,JObject>();
        public int lease;public bool transferred,registrationAttempted;public Mesh mesh;public Texture2D texture0,texture1;
        public MaterialLineage(Assembly core,string commandId,int destructionOrder)
        {
            id=commandId;order=destructionOrder;ownerType=core.GetType("FTKModFramework.Core.EnemyMeshResources",true);
            resources=ownerType.GetMethod("SetResources",Members,null,new[]{typeof(UnityEngine.Object[])},null);
            targets=ownerType.GetMethod("SetTargets",Members,null,new[]{typeof(Renderer[])},null);
            scrollTarget=ownerType.GetMethod("AddScrollingTarget",Members,null,new[]{typeof(Renderer),typeof(Material[]),typeof(ScrollingUVs[])},null);
            prefix=core.GetType("FTKModFramework.Core.ExplicitScrollingUvs",true).GetMethod("Prefix",Statics,null,new[]{typeof(ScrollingUVs)},null);
            leaseId=Field(ownerType,"_leaseId",typeof(int));acquired=Field(ownerType,"_acquired",typeof(bool));applied=Field(ownerType,"Applied",typeof(bool));
            scrollRenderers=Field(ownerType,"_scrollRenderers",typeof(Renderer[]));scrollMaterials=Field(ownerType,"_scrollMaterials",typeof(Material[]));scrollCounts=Field(ownerType,"_scrollCounts",typeof(int[]));scrollers=Field(ownerType,"_scrollers",typeof(ScrollingUVs[]));offset=Field(typeof(ScrollingUVs),"uvOffset",typeof(Vector2));
            leases=ownerType.GetField("Leases",Statics).GetValue(null)as IDictionary;
            if(resources==null || targets==null || scrollTarget==null || prefix==null || prefix.ReturnType!=typeof(bool) || leases==null)throw new InvalidOperationException("Exact reviewed material owner API unavailable.");
            HarmonyLib.Patches patches=HarmonyLib.Harmony.GetPatchInfo(typeof(ScrollingUVs).GetMethod("LateUpdate",Members));
            if(patches==null || patches.Prefixes.Count!=1 || patches.Prefixes[0].PatchMethod!=prefix || patches.Postfixes.Count!=0 || patches.Transpilers.Count!=0 || patches.Finalizers.Count!=0)
                throw new InvalidOperationException("Exact single reviewed native scrolling prefix must be installed before allocation.");
            layer=-1;for(int value=31;value>=0;value--)if(CameraExcludes(value)){layer=value;break;}
            if(layer<0)throw new InvalidOperationException("No layer excluded by all active cameras.");
        }
        static FieldInfo Field(Type type,string name,Type expected)
        {FieldInfo f=type.GetField(name,Members);if(f==null || f.FieldType!=expected)throw new InvalidOperationException("Exact field unavailable: "+name);return f;}
        static bool CameraExcludes(int value)
        {foreach(Camera camera in Camera.allCameras)if(camera!=null && camera.enabled && camera.gameObject.activeInHierarchy && (camera.cullingMask&(1<<value))!=0)return false;return true;}
        void Check(string name,bool pass,object actual,object expected)
        {LeaseAssert(assertions,name,pass,actual,expected);if(!pass)throw new InvalidOperationException("Material lifecycle assertion failed: "+name);}
        public void Guard()
        {
            if(!CameraExcludes(layer))throw new InvalidOperationException("Owned fixture layer became camera-visible.");
            foreach(GameObject root in roots)if(root!=null)
            {
                Component[] components=root.GetComponentsInChildren<Component>(true);
                if(root.transform.parent!=null || root.transform.childCount!=1 || components.Length!=5)throw new InvalidOperationException("Owned material fixture shape changed.");
                foreach(Component c in components)if(c==null || (c.GetType()!=typeof(Transform) && c.GetType()!=typeof(SkinnedMeshRenderer) && c.GetType()!=typeof(ScrollingUVs) && c.GetType()!=ownerType))throw new InvalidOperationException("Unexpected owned fixture component.");
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.gameObject.layer!=layer)throw new InvalidOperationException("Owned fixture layer changed.");
            }
        }
        Material MakeMaterial(Shader shader,Texture2D texture,string name)
        {Material material=new Material(shader);original.Add(material);material.name=name;material.mainTexture=texture;if(!material.HasProperty("_MainTex"))throw new InvalidOperationException("Texture shader property missing.");return material;}
        public void Create()
        {
            GameObject root=new GameObject("FTK_MATERIAL_LINEAGE_"+id+"_"+order);roots.Add(root);parents.Add(-1);root.SetActive(false);root.layer=layer;
            GameObject bone=new GameObject("owned-bone");bone.layer=layer;bone.transform.SetParent(root.transform,false);
            SkinnedMeshRenderer renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.enabled=false;renderers.Add(renderer);
            mesh=new Mesh();original.Add(mesh);mesh.name="original-two-triangle-fixture";mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,new Vector3(0,0,.1f),new Vector3(1,0,.1f),new Vector3(0,1,.1f)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up};mesh.subMeshCount=2;mesh.SetTriangles(new[]{0,1,2},0);mesh.SetTriangles(new[]{3,4,5},1);
            BoneWeight w=new BoneWeight{boneIndex0=0,weight0=1};mesh.boneWeights=new[]{w,w,w,w,w,w};mesh.bindposes=new[]{Matrix4x4.identity};mesh.RecalculateBounds();
            renderer.bones=new[]{bone.transform};renderer.rootBone=bone.transform;renderer.sharedMesh=mesh;
            texture0=new Texture2D(2,2);original.Add(texture0);texture0.name="owned-checker";texture0.SetPixels(new[]{Color.white,Color.black,Color.black,Color.white});texture0.Apply();
            texture1=new Texture2D(2,2);original.Add(texture1);texture1.name="owned-stripes";texture1.SetPixels(new[]{Color.red,Color.red,Color.blue,Color.blue});texture1.Apply();
            Shader shader=Shader.Find("Unlit/Texture")??Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Known texture shader unavailable.");
            Material[] materials={MakeMaterial(shader,texture0,"owned-slot0"),MakeMaterial(shader,texture1,"owned-slot1")};renderer.sharedMaterials=materials;
            ScrollingUVs scroller=root.AddComponent<ScrollingUVs>();scrolling.Add(scroller);scroller.materialIndex=1;scroller.textureName="_MainTex";scroller.uvAnimationRate=new Vector2(0,.2f);
            Component owner=root.AddComponent(ownerType);registrationAttempted=true;resources.Invoke(owner,new object[]{original.ToArray()});lease=(int)leaseId.GetValue(owner);transferred=lease!=0 && leases.Contains(lease);
            targets.Invoke(owner,new object[]{new Renderer[]{renderer}});scrollTarget.Invoke(owner,new object[]{renderer,materials,new[]{scroller}});applied.SetValue(owner,true);
            PinResources();renderer.enabled=true;root.SetActive(true);Guard();Check("source-five-assets",ResourceCount()==5,ResourceCount(),5);
        }
        public void Clone(int parent)
        {
            // Disable only the owned source renderer synchronously so its clone is disabled at birth.
            bool prior=renderers[parent].enabled;GameObject clone;
            try{renderers[parent].enabled=false;clone=UnityEngine.Object.Instantiate(roots[parent])as GameObject;}
            finally{renderers[parent].enabled=prior;}
            if(clone==null)throw new InvalidOperationException("Owned clone missing.");roots.Add(clone);parents.Add(parent);
            SkinnedMeshRenderer renderer=clone.GetComponent<SkinnedMeshRenderer>();ScrollingUVs scroller=clone.GetComponent<ScrollingUVs>();renderers.Add(renderer);scrolling.Add(scroller);
            scroller.uvAnimationRate=new Vector2(0,.2f+.11f*(roots.Count-1));Guard();
            Check("clone-disabled-at-birth-"+(roots.Count-1),!renderer.enabled,renderer.enabled,false);
            Check("clone-mesh-shared",renderer.sharedMesh==mesh,renderer.sharedMesh.GetInstanceID(),mesh.GetInstanceID());
            Component owner=clone.GetComponent(ownerType);Check("native-Awake-retained",(bool)acquired.GetValue(owner) && (int)leaseId.GetValue(owner)==lease,leaseId.GetValue(owner),lease);
        }
        public void Enable(int index){renderers[index].enabled=true;}
        public int ResourceCount(){return leases.Contains(lease)?ReadResources().Length:0;}
        UnityEngine.Object[] ReadResources(){object value=leases[lease];return (UnityEngine.Object[])value.GetType().GetField("resources",Members).GetValue(value);}
        void PinResources(){if(!leases.Contains(lease))return;foreach(UnityEngine.Object item in ReadResources())if(!pinned.Contains(item)){if(item==null)throw new InvalidOperationException("Lease resource disappeared while owners remain.");pinned.Add(item);pinnedIds.Add(item.GetInstanceID());pinnedNames.Add(item.name);pinnedTypes.Add(item.GetType().FullName);}}
        public void DestroyRoot(int index){if(roots[index]!=null)UnityEngine.Object.Destroy(roots[index]);}
        public void Capture(string phase,int expectedOwners,int expectedResources,bool consecutive)
        {
            Guard();PinResources();Check(phase+"-references",LeaseReferences(leases,lease)==expectedOwners,LeaseReferences(leases,lease),expectedOwners);
            Check(phase+"-resources",ResourceCount()==expectedResources,ResourceCount(),expectedResources);
            JArray owners=new JArray();HashSet<int> enabledMaterials=new HashSet<int>();
            for(int index=0;index<roots.Count;index++)if(roots[index]!=null)
            {
                Component owner=roots[index].GetComponent(ownerType);SkinnedMeshRenderer renderer=renderers[index];ScrollingUVs scroller=scrolling[index];Material[] materials=renderer.sharedMaterials;
                Renderer[] mapped=(Renderer[])ownerType.GetField("_targets",Members).GetValue(owner);Renderer[] sr=(Renderer[])scrollRenderers.GetValue(owner);ScrollingUVs[] ss=(ScrollingUVs[])scrollers.GetValue(owner);int[] counts=(int[])scrollCounts.GetValue(owner);Material[] serialized=(Material[])scrollMaterials.GetValue(owner);
                Check("remapped-owner-"+index,mapped.Length==1 && mapped[0]==renderer && sr.Length==1 && sr[0]==renderer && ss.Length==1 && ss[0]==scroller && counts.Length==1 && counts[0]==2,mapped.Length,1);
                Check("acquired-applied-"+index,(bool)acquired.GetValue(owner) && (bool)applied.GetValue(owner) && (int)leaseId.GetValue(owner)==lease,leaseId.GetValue(owner),lease);
                Check("material-slots-"+index,materials.Length==2 && serialized.Length==2,materials.Length,2);
                IDictionary privateMap=ownerType.GetField("_privateScrolling",Members).GetValue(owner)as IDictionary;
                int privateCount=privateMap==null?0:privateMap.Count;
                Check("private-map-state-"+index,privateCount==(renderer.enabled?1:0),privateCount,renderer.enabled?1:0);
                if(renderer.enabled){Material[] privateMaterials=privateMap[renderer]as Material[];Check("private-map-exact-materials",privateMaterials!=null && privateMaterials.Length==2 && privateMaterials[0]==materials[0] && privateMaterials[1]==materials[1],privateMaterials==null?0:privateMaterials.Length,2);}
                Check("owned-bone-remap-"+index,renderer.sharedMesh==mesh && renderer.bones.Length==1 && renderer.bones[0]==roots[index].transform.GetChild(0) && renderer.rootBone==renderer.bones[0],renderer.bones.Length,1);
                Vector2 uv=(Vector2)offset.GetValue(scroller);JArray ids=new JArray(),offsets=new JArray();
                for(int slot=0;slot<2;slot++)
                {
                    Material material=materials[slot];Check("material-owned-"+index+"-"+slot,material!=null && pinned.Contains(material) && serialized[slot]==material,material==null?0:material.GetInstanceID(),"lease + serialized segment");
                    Check("texture-shared-"+index+"-"+slot,material.mainTexture==(slot==0?texture0:texture1),material.mainTexture.GetInstanceID(),slot==0?texture0.GetInstanceID():texture1.GetInstanceID());
                    Vector2 value=material.GetTextureOffset("_MainTex");ids.Add(material.GetInstanceID());offsets.Add(new JArray(value.x,value.y));
                    if(slot==0)Check("untargeted-slot0",value==Vector2.zero,value,Vector2.zero);
                    if(renderer.enabled)Check("enabled-material-independent",enabledMaterials.Add(material.GetInstanceID()),material.GetInstanceID(),"unique enabled owner");
                }
                if(renderer.enabled)Check("enabled-offset-follows-phase",(materials[1].GetTextureOffset("_MainTex")-uv).sqrMagnitude<1e-10f,materials[1].GetTextureOffset("_MainTex"),uv);
                JObject row=new JObject{{"index",index},{"parentIndex",parents[index]},{"root",roots[index].GetInstanceID()},{"owner",owner.GetInstanceID()},{"renderer",renderer.GetInstanceID()},{"scroller",scroller.GetInstanceID()},{"rendererEnabled",renderer.enabled},{"scrollerEnabled",scroller.enabled},{"phase",new JArray(uv.x,uv.y)},{"rate",new JArray(scroller.uvAnimationRate.x,scroller.uvAnimationRate.y)},{"privateMapCount",privateCount},{"property","_MainTex"},{"materialIndex",scroller.materialIndex},{"textureIds",new JArray(materials[0].mainTexture.GetInstanceID(),materials[1].mainTexture.GetInstanceID())},{"materialIds",ids},{"serializedMaterialIds",ids.DeepClone()},{"offsets",offsets},{"sharedMeshId",renderer.sharedMesh.GetInstanceID()},{"boneInstanceId",renderer.bones[0].GetInstanceID()},{"boneLocalPosition",Vec(renderer.bones[0].localPosition)},{"boneLocalRotation",new JArray(renderer.bones[0].localRotation.x,renderer.bones[0].localRotation.y,renderer.bones[0].localRotation.z,renderer.bones[0].localRotation.w)},{"boneLocalScale",Vec(renderer.bones[0].localScale)},{"localRotation",new JArray(roots[index].transform.localRotation.x,roots[index].transform.localRotation.y,roots[index].transform.localRotation.z,roots[index].transform.localRotation.w)},{"localScale",Vec(roots[index].transform.localScale)},{"localPosition",Vec(roots[index].transform.localPosition)}};
                JObject before;
                if(consecutive && previous.TryGetValue(index,out before))
                {
                    Check("consecutive-native-frames",(int)before["frame"]+1==Time.frameCount,Time.frameCount,(int)before["frame"]+1);
                    Vector2 old=new Vector2((float)before["phase"][0],(float)before["phase"][1]);Vector2 expected=old+scroller.uvAnimationRate*Time.deltaTime;
                    float phaseError=MaterialLifecycleTiming.Error((int)before["frame"],Time.frameCount,Time.deltaTime,old.x,old.y,scroller.uvAnimationRate.x,scroller.uvAnimationRate.y,uv.x,uv.y);
                    row["phaseMaximumError"]=phaseError;Check("native-phase-delta",phaseError<=MaterialLifecycleTiming.Tolerance,uv,expected);
                    Check("steady-material-no-allocation",JToken.DeepEquals(before["materialIds"],ids),ids,before["materialIds"]);
                }
                row["frame"]=Time.frameCount;previous[index]=row;owners.Add(row);
            }
            frames.Add(new JObject{{"phase",phase},{"frame",Time.frameCount},{"time",Time.time},{"realtime",Time.realtimeSinceStartup},{"deltaTime",Time.deltaTime},{"leaseId",lease},{"references",LeaseReferences(leases,lease)},{"resourceCount",ResourceCount()},{"owners",owners},{"sampling","EndOfFrame; inherited offsets on disabled clones may reflect ancestor writes"}});
        }
        public void Rejection()
        {
            ScrollingUVs scroller=scrolling[2];SkinnedMeshRenderer renderer=renderers[2];bool enabled=scroller.enabled;int slot=scroller.materialIndex;Vector2 phase=(Vector2)offset.GetValue(scroller);Material[] before=renderer.sharedMaterials;int count=ResourceCount();Component owner=roots[2].GetComponent(ownerType);Material[] serialized=(Material[])scrollMaterials.GetValue(owner);
            try
            {
                scroller.enabled=false;scroller.materialIndex=99;bool fallback=(bool)prefix.Invoke(null,new object[]{scroller});
                Check("rejection-native-fallback",fallback,fallback,true);Check("rejection-no-phase-write",phase==(Vector2)offset.GetValue(scroller),offset.GetValue(scroller),phase);
                Material[] after=renderer.sharedMaterials;Check("rejection-no-material-write",after.Length==2 && after[0]==before[0] && after[1]==before[1],after.Length,2);
                Check("rejection-no-provenance-write",object.ReferenceEquals(serialized,scrollMaterials.GetValue(owner)),true,true);Check("rejection-no-allocation",ResourceCount()==count,ResourceCount(),count);
            }
            finally{scroller.materialIndex=slot;scroller.enabled=enabled;}
        }
        public void Cleanup()
        {
            // Never dispose transferred assets by hand: final Unity-null must come from real production lifecycle.
            try
            {
                // Resolve any registration that succeeded before reflection returned an exception.
                if(!transferred && roots.Count>0 && roots[0]!=null)
                {Component owner=roots[0].GetComponent(ownerType);if(owner!=null){int found=(int)leaseId.GetValue(owner);if(found!=0 && leases.Contains(found)){lease=found;transferred=true;}}}
                PinResources();
            }catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            foreach(GameObject root in roots)try{if(root!=null)UnityEngine.Object.Destroy(root);}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            if(!registrationAttempted)foreach(UnityEngine.Object item in original)try{if(item!=null)UnityEngine.Object.Destroy(item);}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
        }
        public bool Disposed()
        {foreach(GameObject root in roots)if(root!=null)return false;foreach(UnityEngine.Object item in pinned)if(item!=null)return false;foreach(UnityEngine.Object item in original)if(item!=null)return false;return !leases.Contains(lease) && cleanupErrors.Count==0;}
        public JObject Result(){JArray objects=new JArray();for(int i=0;i<pinned.Count;i++)objects.Add(new JObject{{"instanceId",pinnedIds[i]},{"name",pinnedNames[i]},{"type",pinnedTypes[i]},{"unityNull",pinned[i]==null}});return new JObject{{"order",order==0?"descendants-first":"source-first-grandclone-survives"},{"frames",frames},{"assertions",assertions},{"leaseId",lease},{"pinnedResourceCount",pinned.Count},{"resourceDisposal",objects},{"cleanupErrors",cleanupErrors},{"leaseAbsent",!leases.Contains(lease)},{"disposed",Disposed()}};}
    }
    IEnumerator MaterialLifecycleFixture(string id,JObject command)
    {
        JArray cases=new JArray(),engineErrors=new JArray(),cleanupErrors=new JArray();string error=null;bool sameReady=false,mainExited=false;
        KrakenAdapterReadyPin ready=null;JObject core=null;JArray party=null;List<MaterialLineage> lineages=new List<MaterialLineage>();
        Application.LogCallback handler=delegate(string message,string stack,LogType type){if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && engineErrors.Count<32)engineErrors.Add(new JObject{{"type",type.ToString()},{"message",message},{"stack",stack}});};
        Application.logMessageReceived+=handler;
        try
        {
            try
            {
                CatalogKeys(command,"id","session","op");CatalogNoLinks(root);ready=new KrakenAdapterReadyPin(sessionId);core=PreviewCoreIdentity();
                if((string)core["assemblyFileSha256"]!=MaterialLifecycleCore)throw new InvalidOperationException("Only reviewed Core19ece is authorized for this fixed lifetime fixture.");
                party=MaterialFixtureParty();
            }catch(Exception ex){error=ex.ToString();}
            for(int order=0;order<2 && error==null;order++)
            {
                MaterialLineage lineage=null;
                try{lineage=new MaterialLineage(CatalogAssembly("FTKModFramework"),id,order);lineages.Add(lineage);}catch(Exception ex){error=ex.ToString();}
                for(int step=0;step<=10 && error==null;step++)
                {
                    int references=step==0?1:step<=2?2:step<=4?3:step<=6?4:10-step;
                    int resourceCount=step<=1?5:step<=3?7:step<10?9:0;
                    string phase=new[]{"source-enabled","clone-disabled","clone-enabled","grandclone-disabled","grandclone-enabled","never-enabled-renderer","prefix-rejection-only","destroy-never-enabled","first-destruction","survivor-after-second-destruction","last-owner-destruction"}[step];
                    try
                    {
                        MaterialFixtureCheck(ready,core,party);
                        if(step==0)lineage.Create();else if(step==1)lineage.Clone(0);else if(step==2)lineage.Enable(1);else if(step==3)lineage.Clone(1);else if(step==4)lineage.Enable(2);else if(step==5)lineage.Clone(2);else if(step==6)lineage.Rejection();else if(step==7)lineage.DestroyRoot(3);else if(step==8)lineage.DestroyRoot(order==0?2:0);else if(step==9)lineage.DestroyRoot(1);else lineage.DestroyRoot(order==0?0:2);
                    }catch(Exception ex){error=ex.ToString();}
                    float start=Time.realtimeSinceStartup;
                    for(int sample=0;sample<2 && error==null;sample++)
                    {
                        yield return new WaitForEndOfFrame();
                        try
                        {
                            MaterialFixtureCheck(ready,core,party);
                            if(Time.realtimeSinceStartup-start>5 || Time.deltaTime<=0 || float.IsNaN(Time.deltaTime) || float.IsInfinity(Time.deltaTime))throw new InvalidOperationException("Native scheduled observation exceeded deadline or game clock did not advance.");
                            lineage.Capture(phase,references,resourceCount,sample==1);
                        }catch(Exception ex){error=ex.ToString();}
                    }
                }
            }
            mainExited=true;
        }
        finally
        {
            foreach(MaterialLineage lineage in lineages)try{lineage.Cleanup();}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            if(!mainExited){Application.logMessageReceived-=handler;busy=false;}
        }
        try
        {
            // Observe production OnDestroy/pruning, never invoke it ourselves.
            for(int frame=0;frame<4;frame++)
            {
                yield return null;
                try{MaterialFixtureCheck(ready,core,party);}catch(Exception ex){if(error==null)error=ex.ToString();}
            }
            bool clean=true;foreach(MaterialLineage lineage in lineages){cases.Add(lineage.Result());if(!lineage.Disposed())clean=false;}
            try{MaterialFixtureCheck(ready,core,party);sameReady=true;}catch(Exception ex){if(error==null)error=ex.ToString();}
            Finish(id,new JObject{{"ok",error==null && clean && sameReady && cases.Count==2 && engineErrors.Count==0 && cleanupErrors.Count==0},{"error",error},{"framework",core},{"pinnedReady",ready==null?null:ready.View()},{"pinnedParty",party},{"sameReadyAfter",sameReady},{"cases",cases},{"engineErrors",engineErrors},{"cleanupErrors",cleanupErrors},{"allOwnedResourcesDisposed",clean},
                {"provenance","owned_original_no_CEL_native_scheduled_material_lineage_fixture"},{"limits","Explicit production owner setup is fixture preparation, not public GLB binding. Native Awake/LateUpdate/OnDestroy and actual Plugin.Update pruning are exercised; no manual ownership or leased-resource cleanup. Prefix rejection is a separate direct-call eligibility check, not native scheduling or failed-commit rollback. Disabled renderers can observe ancestor writes until private allocation. No live avatar, art, controller or gameplay acceptance."}});
        }
        finally{Application.logMessageReceived-=handler;busy=false;}
    }
    static JArray MaterialFixtureParty()
    {JArray ids=new JArray();foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)if(cow!=null)ids.Add(cow.GetInstanceID());return ids;}
    void MaterialFixtureCheck(KrakenAdapterReadyPin ready,JObject core,JArray party)
    {if(ready==null || core==null || party==null)throw new InvalidOperationException("Lifetime fixture pin unavailable.");CatalogNoLinks(root);ready.Check(sessionId);if(!JToken.DeepEquals(core,PreviewCoreIdentity()) || !JToken.DeepEquals(party,MaterialFixtureParty()))throw new InvalidOperationException("Pinned Core/party identity changed.");}
}
