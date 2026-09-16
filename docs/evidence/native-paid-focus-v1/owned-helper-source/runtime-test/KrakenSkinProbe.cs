using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Fixed reviewed original asset contract; archived authoring/provenance hashes are inside the pinned manifest.
    const string KrakenSkinManifest="kraken-owned-skin-probe-v1.manifest.json";
    const string KrakenSkinManifestHash="df046f883286b3cf1723ec7bd87454e99e131a1673221c09aced61fd7d833e17";
    const string GloamfinSkinManifest="gloamfin-kraken-blockout-v1.manifest.json";
    const string GloamfinSkinManifestHash="8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e";
    static readonly int[] KrakenSkinImageSteps={0,16,28,40,41,80,104,105,112,119,120,240};
    static readonly int[] KrakenSkinBakeSteps={0,28,80,112};
    sealed class KrakenSkinArm
    {
        public JObject request,manifest,capturePlan;public int[] imageSteps,bakeSteps;public string root,session,scenario,variant,manifestFile,manifestHash,endpointPolicy;
        public KrakenAdapterReadyPin pin;
    }
    KrakenSkinArm krakenSkinArm;
    string KrakenSkinFile(string name)
    {
        if(name!=Path.GetFileName(name) || string.IsNullOrEmpty(name) || name.IndexOfAny(new[]{'/','\\',':'})>=0)throw new ArgumentException("Fixed probe basename required.");
        string path=Path.Combine(root,"BepInEx/plugins/FTKModFramework_content/models/"+name);CatalogNoLinks(path);
        if(new FileInfo(path).Length<1 || new FileInfo(path).Length>4*1024*1024)throw new InvalidOperationException("Probe file outside1..4MiB bound.");return path;
    }
    JObject KrakenSkinPreflight(string variant)
    {
        bool organic=variant=="gloamfin-v1";
        if(variant!=null && !organic)throw new ArgumentException("Only explicit gloamfin-v1 or absent original variant allowed.");
        string manifestPath=KrakenSkinFile(organic?GloamfinSkinManifest:KrakenSkinManifest);
        if(CatalogHash(manifestPath)!=(organic?GloamfinSkinManifestHash:KrakenSkinManifestHash))throw new InvalidOperationException("Exact reviewed original probe manifest required.");
        JObject manifest=JObject.Parse(File.ReadAllText(manifestPath));
        foreach(string kind in new[]{"glb","png"})
        {
            JObject asset=(JObject)manifest[kind];string path=KrakenSkinFile((string)asset["file"]);
            if(CatalogHash(path)!=(string)asset["sha256"])throw new InvalidOperationException("Original probe asset hash mismatch: "+kind);
        }
        if(organic)
        {
            int vertices=(int)manifest["vertexCount"],indices=(int)manifest["indexCount"];
            if((string)manifest["variant"]!=variant || vertices<1 || vertices>8192 || indices<3 || indices>49152 || indices%3!=0 || (int)manifest["triangleCount"]!=indices/3)
                throw new InvalidOperationException("Pinned organic geometry counts outside bounded contract.");
        }
        return manifest;
    }
    JObject ArmKrakenSkin(JObject command)
    {
        CatalogKeys(command,"id","session","op","scenario","manifestSha256","variant","capturePlanSha256","endpointPolicy");
        string policy="main-appearance-local-v1";if(command["endpointPolicy"]!=null){if(command["endpointPolicy"].Type!=JTokenType.String || (string)command["endpointPolicy"]!=KrakenEndpointFixturePlan.ComposedPolicy)throw new ArgumentException("Only explicit composed endpoint arm policy supported; omit for legacy.");policy=(string)command["endpointPolicy"];}
        string variant=null;if(command["variant"]!=null){if(command["variant"].Type!=JTokenType.String || (string)command["variant"]!="gloamfin-v1")throw new ArgumentException("Only explicit gloamfin-v1 variant allowed.");variant=(string)command["variant"];}
        if(policy==KrakenEndpointFixturePlan.ComposedPolicy && variant!="gloamfin-v1")throw new ArgumentException("Composed skin fixture requires explicit original Gloamfin variant.");
        string manifestHash=variant==null?KrakenSkinManifestHash:GloamfinSkinManifestHash,manifestFile=variant==null?KrakenSkinManifest:GloamfinSkinManifest;
        if(krakenSkinArm!=null)throw new InvalidOperationException("One probe arm already pending; explicitly stop it first.");
        string scenario=Str(command,"scenario");
        if(Str(command,"manifestSha256")!=manifestHash)throw new ArgumentException("Exact fixed manifest SHA256 required.");
        KrakenAdapterReadyPin pin=new KrakenAdapterReadyPin(sessionId);JObject manifest=KrakenSkinPreflight(variant);
        string requestedPlan=null;if(command["capturePlanSha256"]!=null){if(command["capturePlanSha256"].Type!=JTokenType.String)throw new ArgumentException("capturePlanSha256 must be string.");requestedPlan=(string)command["capturePlanSha256"];}
        JObject capturePlan=KrakenCapturePlan(variant,scenario,requestedPlan,manifest),selection=capturePlan==null?null:KrakenCaptureScenario(capturePlan,scenario);
        int[] imageSteps=selection==null?KrakenSkinImageSteps:KrakenCaptureSteps(selection["imageSteps"],12),bakeSteps=selection==null?KrakenSkinBakeSteps:KrakenCaptureSteps(selection["bakeSteps"],4);
        foreach(int step in bakeSteps)if(Array.IndexOf(imageSteps,step)<0)throw new InvalidOperationException("Bake step must also be an image step.");
        krakenSkinArm=new KrakenSkinArm{endpointPolicy=policy,capturePlan=capturePlan,imageSteps=imageSteps,bakeSteps=bakeSteps,request=(JObject)command.DeepClone(),manifest=manifest,root=root,session=sessionId,scenario=scenario,pin=pin,variant=variant,manifestFile=manifestFile,manifestHash=manifestHash};
        JObject result=new JObject{{"ok",true},{"status","one-shot-original-skin-probe-armed"},{"scenario",scenario},{"manifestSha256",manifestHash},
            {"pinnedReady",pin.View()},{"imageSteps",new JArray(imageSteps)},{"bakeSteps",new JArray(bakeSteps)},
            {"note","Consumed before allocation by the next matching endpoint-policy controller fixture. No render or skin allocation occurred."}};
        if(capturePlan!=null)result["capturePlanSha256"]=GloamfinCapturePlanHash;
        if(variant!=null)result["variant"]=variant;if(policy==KrakenEndpointFixturePlan.ComposedPolicy)result["endpointPolicy"]=policy;return result;
    }
    JObject StopKrakenSkin(){bool pending=krakenSkinArm!=null;krakenSkinArm=null;return new JObject{{"ok",true},{"clearedPendingArm",pending}};}
    KrakenSkinArm ConsumeKrakenSkin(JObject command)
    {
        KrakenSkinArm arm=krakenSkinArm;
        if(arm!=null && (arm.endpointPolicy==KrakenEndpointFixturePlan.ComposedPolicy || Str(command,"endpointPolicy")==KrakenEndpointFixturePlan.ComposedPolicy) && arm.endpointPolicy!=Str(command,"endpointPolicy"))throw new InvalidOperationException("Composed/legacy arm policy mismatch; pending arm preserved.");
        krakenSkinArm=null;if(arm==null)return null;
        if(arm.root!=root || arm.session!=sessionId || arm.scenario!=Str(command,"scenario") || Str(command,"endpointPolicy")!=arm.endpointPolicy)
            throw new InvalidOperationException("Probe arm scope mismatch; stale arm cleared without skin execution.");
        arm.pin.Check(sessionId);JObject current=KrakenSkinPreflight(arm.variant);
        if(arm.capturePlan!=null && !JToken.DeepEquals(arm.capturePlan,KrakenCapturePlan(arm.variant,arm.scenario,GloamfinCapturePlanHash,current)))throw new InvalidOperationException("Companion plan changed after arm.");
        if(!JToken.DeepEquals(arm.manifest,current))throw new InvalidOperationException("Probe manifest changed since arming.");return arm;
    }
    sealed class KrakenOwnedSkinProbe
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<GameObject> ownedObjects=new List<GameObject>();
        public JObject identity;public JArray captures=new JArray(),cleanupErrors=new JArray();
        GameObject rendererObject,cameraObject;SkinnedMeshRenderer renderer;Camera camera;RenderTexture target;Texture2D pixels;
        Mesh mesh,baked;Material material;Texture2D palette;KrakenEndpointFixturePlan endpoint;Transform rendererSource;
        int layer;Matrix4x4 cameraWorld;string directory;RuntimeModelTest owner;Vector3[] original;BoneWeight[] weights;Matrix4x4[] binds;
        Transform[] bones;bool disposed;int[] imageSteps,bakeSteps;
        T Own<T>(T value)where T:UnityEngine.Object{owned.Add(value);return value;}
        static JObject BoundsView(Bounds b){return new JObject{{"center",Vec(b.center)},{"size",Vec(b.size)}};}
        static JArray Vertices(Vector3[] values){JArray result=new JArray();foreach(Vector3 value in values){SampleMatrix(Matrix4x4.TRS(value,Quaternion.identity,Vector3.one));result.Add(Vec(value));}return result;}
        static void EmptyLayer(int chosen,SkinnedMeshRenderer allowed)
        {
            foreach(Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
                if(r!=allowed && r.gameObject.scene.IsValid() && r.gameObject.layer==chosen)throw new InvalidOperationException("Probe culling layer contains another renderer.");
            foreach(CanvasRenderer r in Resources.FindObjectsOfTypeAll<CanvasRenderer>())
                if(r.gameObject.scene.IsValid() && r.gameObject.layer==chosen)throw new InvalidOperationException("Probe culling layer contains a CanvasRenderer.");
            foreach(Terrain t in Resources.FindObjectsOfTypeAll<Terrain>())
                if(t.gameObject.scene.IsValid() && t.gameObject.layer==chosen)throw new InvalidOperationException("Probe culling layer contains Terrain.");
        }
        public void Create(RuntimeModelTest host,KrakenSkinArm arm,KrakenEndpointFixturePlan plan,GameObject nativeOld,string id)
        {
            owner=host;endpoint=plan;directory=Path.Combine(host.output,id+"-original-skin");
            if(Directory.Exists(directory))throw new InvalidOperationException("Probe evidence directory already exists.");
            Directory.CreateDirectory(directory);
            SkinnedMeshRenderer[] natives=nativeOld.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(natives.Length!=1 || natives[0].sharedMesh==null)throw new InvalidOperationException("Exact single old121260 binding renderer required.");
            SkinnedMeshRenderer native=natives[0];Transform[] nativeBones=native.bones;
            if(nativeBones.Length!=5)throw new InvalidOperationException("Exact five old bones required.");
            bones=new Transform[5];JArray paletteOrder=new JArray();
            for(int i=0;i<5;i++)
            {
                if(nativeBones[i]==null || Relative(nativeBones[i],nativeOld.transform)!=KrakenOldPaths[i])throw new InvalidOperationException("Old probe bone order/path changed.");
                bones[i]=plan.OutputNode(KrakenOldPaths[i]);paletteOrder.Add(new JObject{{"path",KrakenOldPaths[i]},{"name",bones[i].name},{"instanceId",bones[i].GetInstanceID()}});
            }
            string rendererPath=Relative(native.transform,nativeOld.transform);rendererSource=plan.OutputNode(rendererPath=="."?"":rendererPath);
            Matrix4x4[] nativeBinds=native.sharedMesh.bindposes;
            MethodInfo load=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.RuntimeGltfMeshLoader",true).GetMethod("LoadSkinnedGlb",Statics,null,new[]{typeof(string),typeof(Transform[]),typeof(Matrix4x4[]),typeof(bool)},null);
            mesh=Own((Mesh)load.Invoke(null,new object[]{(string)arm.manifest["glb"]["file"],bones,nativeBinds,true}));
            int expectedVertices=arm.variant==null?120:(int)arm.manifest["vertexCount"],expectedIndices=arm.variant==null?120:(int)arm.manifest["indexCount"];
            if(mesh==null || mesh.vertexCount!=expectedVertices || mesh.triangles.Length!=expectedIndices || mesh.subMeshCount!=1 || mesh.bindposes.Length!=5)
                throw new InvalidOperationException("Original probe decode failed or exceeded exact bounded mesh contract.");
            original=mesh.vertices;weights=mesh.boneWeights;binds=mesh.bindposes;
            if(weights.Length!=original.Length)throw new InvalidOperationException("Probe weights missing.");
            for(int i=0;i<5;i++)KrakenOldAdapterPlan.Near(binds[i],nativeBinds[i],"probe original IBM matches native binding metadata");
            if(arm.variant!=null)ValidateOrganicWeights(arm.manifest,weights,mesh.triangles,original.Length,binds);
            palette=Own(new Texture2D(2,2,TextureFormat.RGBA32,false));
            if(!palette.LoadImage(File.ReadAllBytes(host.KrakenSkinFile((string)arm.manifest["png"]["file"]))) || palette.width>512 || palette.height>512)
                throw new InvalidOperationException("Original palette decode failed or too large.");
            Shader shader=Shader.Find("Unlit/Texture");if(shader==null)throw new InvalidOperationException("Exact unlit texture shader unavailable.");
            material=Own(new Material(shader));material.name="FTK_original_kraken_probe_material";material.mainTexture=palette;
            rendererObject=new GameObject("FTK_ORIGINAL_KRAKEN_SKIN_"+id);ownedObjects.Add(rendererObject);rendererObject.SetActive(false);
            renderer=rendererObject.AddComponent<SkinnedMeshRenderer>();renderer.enabled=false;
            for(layer=31;layer>=8;layer--){try{EmptyLayer(layer,renderer);break;}catch(InvalidOperationException){ }}
            if(layer<8)throw new InvalidOperationException("No empty diagnostic culling layer available.");
            rendererObject.layer=layer;renderer.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.bones=bones;
            renderer.rootBone=native.rootBone==null?null:plan.OutputNode(Relative(native.rootBone,nativeOld.transform));renderer.updateWhenOffscreen=true;renderer.quality=SkinQuality.Bone4;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.localBounds=new Bounds(Vector3.zero,new Vector3(1000,1000,1000)); // Owned diagnostic culling envelope only; actual Bake bounds recorded separately.
            // Cancel arbitrary native prefab placement through one endpoint-owned parent. All output local
            // transforms stay intact; both the camera and standalone SMR derive their placement from that tree.
            plan.EnableOriginPresentation(id);
            cameraObject=new GameObject("FTK_ORIGINAL_KRAKEN_CAMERA_"+id);ownedObjects.Add(cameraObject);cameraObject.SetActive(false);
            camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<layer;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.06f,.06f,.08f,1);camera.orthographic=true;camera.useOcclusionCulling=false;camera.renderingPath=RenderingPath.Forward;camera.allowHDR=false;camera.allowMSAA=false;
            imageSteps=arm.imageSteps;bakeSteps=arm.bakeSteps;
            JObject framing=arm.capturePlan==null?(JObject)arm.manifest["camera"]:(JObject)KrakenCaptureScenario(arm.capturePlan,arm.scenario)["camera"];
            Vector3 position=JsonVector((JArray)framing["position"]),look=JsonVector((JArray)framing["lookAt"]),up=JsonVector((JArray)framing["up"]);
            camera.transform.position=plan.output.transform.TransformPoint(position);
            camera.transform.rotation=Quaternion.LookRotation(plan.output.transform.TransformDirection(look-position),plan.output.transform.TransformDirection(up));
            camera.orthographicSize=(float)framing["orthographicSize"];camera.nearClipPlane=(float)framing["nearClipPlane"];camera.farClipPlane=(float)framing["farClipPlane"];camera.aspect=1;
            if(camera.orthographicSize<=0 || camera.nearClipPlane<=0 || camera.farClipPlane<=camera.nearClipPlane)throw new InvalidOperationException("Invalid fixed camera framing.");
            cameraWorld=camera.transform.localToWorldMatrix;SampleMatrix(camera.projectionMatrix);
            target=Own(new RenderTexture(512,512,24,RenderTextureFormat.ARGB32));target.name="FTK_original_probe_RT";
            if(!target.Create())throw new InvalidOperationException("Owned render target creation failed.");camera.targetTexture=target;
            pixels=Own(new Texture2D(512,512,TextureFormat.RGB24,false));baked=Own(new Mesh());baked.name="FTK_original_probe_bake";
            // Activate only our Transform-only output and separately owned disabled renderer/camera.
            plan.output.SetActive(true);rendererObject.SetActive(true);cameraObject.SetActive(true);SyncRenderer();
            JArray ibms=new JArray();foreach(Matrix4x4 bind in binds)ibms.Add(SampleMatrix(bind));
            identity=new JObject{{"armRequest",arm.request},{"armReady",arm.pin.View()},{"manifestFile",arm.manifestFile},{"manifestSha256",arm.manifestHash},{"manifest",arm.manifest},
                {"meshInstanceId",mesh.GetInstanceID()},{"meshName",mesh.name},{"vertexCount",mesh.vertexCount},{"indexCount",mesh.triangles.Length},
                {"rendererInstanceId",renderer.GetInstanceID()},{"nativeBindingRendererName",native.name},{"rendererPath",rendererPath},{"rootBoneId",renderer.rootBone==null?0:renderer.rootBone.GetInstanceID()},{"rendererLocalBounds",BoundsView(renderer.localBounds)},{"bones",paletteOrder},{"bindposes",ibms},
                {"shader",shader.name},{"fogObserved",new JObject{{"enabled",RenderSettings.fog},{"mode",RenderSettings.fogMode.ToString()},{"density",RenderSettings.fogDensity},{"start",RenderSettings.fogStartDistance},{"end",RenderSettings.fogEndDistance}}},{"cullingLayer",layer},{"cameraInstanceId",camera.GetInstanceID()},{"cameraWorld",SampleMatrix(cameraWorld)},{"cameraProjection",SampleMatrix(camera.projectionMatrix)},
                {"presentation",plan.Presentation()},{"framing",framing},{"width",512},{"height",512},{"imageSteps",new JArray(imageSteps)},{"bakeSteps",new JArray(bakeSteps)},
                {"scope","Original rigid five-bone markers only. No native surface, continuous anatomy, mesh seams or live adapter support claim."}};
            if(arm.variant!=null){identity["variant"]=arm.variant;identity["scope"]="Original organic blockout with full five-bone authored weights. Owned endpoint fixture only; no native surface, live adapter or final art acceptance.";identity["weightProof"]=arm.manifest["weightProof"].DeepClone();}
            if(arm.capturePlan!=null){identity["capturePlanSha256"]=GloamfinCapturePlanHash;identity["capturePlanFile"]=GloamfinCapturePlanFile;identity["capturePlan"]=arm.capturePlan;}
            Audit();
        }
        static void ValidateOrganicWeights(JObject manifest,BoneWeight[] weights,int[] indices,int count,Matrix4x4[] binds)
        {
            int[] histogram=new int[4],positive=new int[5];int soft=0;
            foreach(int index in indices)if(index<0||index>=count)throw new InvalidOperationException("Organic index outside original vertex array.");
            foreach(BoneWeight weight in weights)
            {
                float[] values={weight.weight0,weight.weight1,weight.weight2,weight.weight3};int[] joints={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};
                float sum=0;int influences=0;bool[] seen=new bool[5];
                for(int i=0;i<4;i++)
                {
                    if(float.IsNaN(values[i])||float.IsInfinity(values[i])||values[i]<0||joints[i]<0||joints[i]>=5)throw new InvalidOperationException("Organic weight invalid.");
                    sum+=values[i];if(values[i]>0){if(seen[joints[i]])throw new InvalidOperationException("Duplicate positive organic joint.");seen[joints[i]]=true;influences++;positive[joints[i]]++;}
                }
                if(Math.Abs(sum-1)>1e-6 || influences<1 || influences>2)throw new InvalidOperationException("Organic normalized one/two influence contract violated.");
                histogram[influences-1]++;if(influences>1)soft++;
            }
            JObject proof=(JObject)manifest["weightProof"];
            if(soft<=0 || soft!=(int)proof["softVertexCount"])throw new InvalidOperationException("Organic soft weight proof mismatch.");
            for(int i=0;i<4;i++)if(histogram[i]!=(int)proof["influenceCountHistogram"][(i+1).ToString()])throw new InvalidOperationException("Organic weight histogram mismatch.");
            for(int i=0;i<5;i++)
            {
                if(positive[i]<=0||positive[i]!=(int)proof["positiveVertexCountByBone"][i])throw new InvalidOperationException("Organic positive palette proof mismatch.");
                for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(Math.Abs(binds[i][r,c]-(double)manifest["bindposes"][i][r][c])>1e-5)throw new InvalidOperationException("Organic pinned inverse bind mismatch.");
            }
        }
        static Vector3 JsonVector(JArray v){if(v==null || v.Count!=3)throw new InvalidOperationException("Fixed vector3 required.");Vector3 result=new Vector3((float)v[0],(float)v[1],(float)v[2]);SampleMatrix(Matrix4x4.TRS(result,Quaternion.identity,Vector3.one));return result;}
        void SyncRenderer(){KrakenOldAdapterPlan.Decompose(rendererSource.localToWorldMatrix).Set(rendererObject.transform);}
        void Audit()
        {
            endpoint.Presentation();
            if(disposed || renderer==null || camera==null || renderer.enabled || camera.enabled || renderer.sharedMesh!=mesh || renderer.sharedMaterial!=material
                || camera.targetTexture!=target || camera.cullingMask!=(1<<layer) || rendererObject.layer!=layer)throw new InvalidOperationException("Owned skin isolation invariant changed.");
            if(rendererObject.GetComponents<Component>().Length!=2 || cameraObject.GetComponents<Component>().Length!=2)throw new InvalidOperationException("Unexpected component on probe objects.");
            for(int i=0;i<5;i++)if(renderer.bones[i]!=bones[i])throw new InvalidOperationException("Probe bone binding changed.");
            KrakenOldAdapterPlan.Near(camera.transform.localToWorldMatrix,cameraWorld,"immutable probe camera");EmptyLayer(layer,renderer);
        }
        public JObject Step(int step,JObject endpointFrame)
        {
            Audit();SyncRenderer();if(Array.IndexOf(imageSteps,step)<0)return null;
            JObject before=KrakenControllerPose(endpoint.OutputNodes(),KrakenOldPaths);RenderTexture previous=RenderTexture.active;
            JObject record=new JObject{{"step",step},{"unityFrame",Time.frameCount},{"appearanceWeight",endpointFrame["appearanceWeight"]},{"output",endpointFrame["output"].DeepClone()},
                {"presentation",endpoint.Presentation()},{"outputTopLocalToWorld",SampleMatrix(endpoint.output.transform.localToWorldMatrix)},{"rendererLocalToWorld",SampleMatrix(renderer.localToWorldMatrix)},{"rendererWorldToLocal",SampleMatrix(renderer.worldToLocalMatrix)},
                {"cameraWorldToCamera",SampleMatrix(camera.worldToCameraMatrix)},{"cameraProjection",SampleMatrix(camera.projectionMatrix)}};
            captures.Add(record);
            try
            {
                EmptyLayer(layer,renderer);renderer.enabled=true;
                if(Array.IndexOf(bakeSteps,step)>=0)
                {
                    renderer.BakeMesh(baked);baked.RecalculateBounds();Vector3[] vertices=baked.vertices;
                    if(vertices.Length!=original.Length)throw new InvalidOperationException("Original probe BakeMesh vertex count changed.");
                    record["bakedVerticesRendererLocal"]=Vertices(vertices);record["bakedBounds"]=BoundsView(baked.bounds);
                    JArray transforms=new JArray();foreach(Transform bone in bones)transforms.Add(SampleMatrix(bone.localToWorldMatrix));record["boneWorldMatrices"]=transforms;
                }
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply(false);
                string file=step.ToString("D3")+".png",path=Path.Combine(directory,file);
                if(File.Exists(path))throw new InvalidOperationException("Probe image already exists.");File.WriteAllBytes(path,pixels.EncodeToPNG());
                record["image"]=new JObject{{"file",file},{"path",path},{"sha256",CatalogHash(path)},{"width",512},{"height",512}};
                record["captured"]=true;
            }
            catch(Exception ex){record["captured"]=false;record["error"]=ex.ToString();throw;}
            finally{try{if(renderer!=null)renderer.enabled=false;}finally{RenderTexture.active=previous;}}
            if(!JToken.DeepEquals(before,KrakenControllerPose(endpoint.OutputNodes(),KrakenOldPaths)))throw new InvalidOperationException("Skin render changed endpoint output transforms.");
            Audit();record["outputUnchanged"]=true;record["rendererDisabledBeforeYield"]=true;record["renderTextureActiveRestored"]=RenderTexture.active==previous;return record;
        }
        public void Dispose()
        {
            disposed=true;
            try{if(renderer!=null)renderer.enabled=false;}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            try{if(camera!=null){camera.enabled=false;camera.targetTexture=null;}}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            foreach(GameObject value in ownedObjects)try{if(value!=null)UnityEngine.Object.Destroy(value);}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            try{if(target!=null)target.Release();}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            foreach(UnityEngine.Object value in owned)try{if(value!=null)UnityEngine.Object.Destroy(value);}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
        }
        public JObject Cleanup()
        {
            bool allNull=true;foreach(UnityEngine.Object value in owned)allNull&=value==null;foreach(GameObject value in ownedObjects)allNull&=value==null;
            return new JObject{{"allOwnedUnityNull",allNull},{"endpointOwnedPresentationUnityNull",endpoint==null || endpoint.PresentationClean()},{"errors",cleanupErrors},{"disposed",disposed}};
        }
        public bool Clean(){return disposed && (bool)Cleanup()["allOwnedUnityNull"] && cleanupErrors.Count==0;}
        public bool Complete(){int bakes=0;foreach(JObject capture in captures){if((bool?)capture["captured"]!=true)return false;if(capture["bakedVerticesRendererLocal"]!=null)bakes++;}return captures.Count==12 && bakes==4;}
        public JObject View(){return new JObject{{"identity",identity},{"captures",captures},{"cleanup",Cleanup()},{"expectedImages",12},{"expectedBakeSamples",4},
            {"limitations","Owned original skin experiment only. Bake vertices require independent skin-formula verification; PNGs require visual inspection. No native geometry export, automatic rig support, or gameplay acceptance."}};}
    }
}
