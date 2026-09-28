using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject PlayerStudio(JObject command)
    {
        CatalogKeys(command,"id","session","op","source","ownerInstanceId","celInstanceId","view","framing","lighting","nativeMaterialDiagnostic");
        RequireSinglePlayer();CatalogNoLinks(root);CatalogNoLinks(output);
        string source=Str(command,"source"),view=Str(command,"view");
        JObject materialDiagnostic=command["nativeMaterialDiagnostic"] as JObject;
        if(command["nativeMaterialDiagnostic"]!=null && (materialDiagnostic==null || source!="inventory"))
            throw new ArgumentException("nativeMaterialDiagnostic requires an object and source inventory");
        string lighting=Str(command,"lighting")??"supplemental";
        if(lighting!="supplemental" && lighting!="native")throw new ArgumentException("lighting must be supplemental or native");
        string framing=command["framing"]==null?"body":Str(command,"framing");
        if(framing!="body" && framing!="head")throw new ArgumentException("framing must be body or head");
        if(view!="front" && view!="three-quarter" && view!="side" && view!="other-side" && view!="rear-quarter" && view!="back")throw new ArgumentException("view must be front, three-quarter, side, other-side, rear-quarter or back");
        int ownerId=LeaseObservationPin.ExactId(command,"ownerInstanceId",false);
        int celId=LeaseObservationPin.ExactId(command,"celInstanceId",false);
        int renderCelId=celId;OffscreenCamera inventoryCamera=null;
        if(source=="preview")PreviewRaceRequireStudioReady(ownerId);
        CharacterEventListener avatar=null;JToken equipment=null;JObject appearance=null;
        if(source=="preview")
        {
            foreach(uiQuickPlayerCreate preview in Resources.FindObjectsOfTypeAll<uiQuickPlayerCreate>())
                if(preview!=null && SceneOwner(preview) && preview.GetInstanceID()==ownerId)
                {
                    avatar=preview.m_Avatar;
                    if(avatar==null || avatar.m_uiQuickPlayerCreate!=preview)throw new InvalidOperationException("Preview avatar is not reciprocal");
                    equipment=PreviewInventory(preview);break;
                }
        }
        else if(source=="world" || source=="inventory")
        {
            if(FTKHub.Instance!=null)foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
                if(cow!=null && SceneOwner(cow) && cow.GetInstanceID()==ownerId)
                {
                    if(cow.m_CharacterStats==null || cow.m_CharacterStats.m_IsInCombat || !cow.IsOwner)
                        throw new InvalidOperationException("Owned noncombat world hero required");
                    avatar=cow.m_Avatar;
                    if(avatar==null || avatar.m_CharacterOverworld!=cow)throw new InvalidOperationException("World avatar is not reciprocal");
                    appearance=BlacksmithAppearanceStudio(ownerId,celId);
                    equipment=EquipmentView(cow);
                    if(source=="inventory")
                    {
                        if(avatar.GetInstanceID()!=celId)throw new InvalidOperationException("Inventory source avatar identity changed");
                        uiPlayerInventory inventory=uiPlayerInventory.Instance;
                        if(inventory==null || !SceneOwner(inventory) || !inventory.gameObject.activeInHierarchy ||
                            !inventory.m_IsShowing || inventory.m_IsUpdatePaperDoll || inventory.m_InventoryOwner!=cow)
                            throw new InvalidOperationException("Settled native inventory for the exact owner required");
                        // Native UpdatePaperDoll owns this clone; never create or refresh one for the capture.
                        inventoryCamera=typeof(uiPlayerInventory).GetField("m_OffscreenCamera",Members).GetValue(inventory)as OffscreenCamera;
                        if(inventoryCamera==null || !SceneOwner(inventoryCamera) || !inventoryCamera.m_IsRendering ||
                            inventoryCamera.m_TargetObject==null || inventory.m_PaperDoll==null ||
                            inventoryCamera.m_RenderTexture==null || inventory.m_PaperDoll.texture!=inventoryCamera.m_RenderTexture)
                            throw new InvalidOperationException("Existing native inventory render stream required");
                        avatar=inventoryCamera.m_TargetObject.GetComponent<CharacterEventListener>();
                        if(avatar==null || avatar.m_OffscreenCamera!=inventoryCamera || avatar.m_CharacterOverworld!=cow || avatar==cow.m_Avatar)
                            throw new InvalidOperationException("Native inventory clone is not reciprocal");
                        renderCelId=avatar.GetInstanceID();
                    }
                    break;
                }
        }
        else throw new ArgumentException("source must be preview, world or inventory");
        if(avatar==null || avatar.GetInstanceID()!=renderCelId || !SceneOwner(avatar) || !avatar.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Exact active native avatar unavailable");
        string path=Path.Combine(output,Token(Str(command,"id"))+".png");
        JObject result=RenderPlayerStudioAvatar(avatar,path,view,null,0f,framing=="head",lighting=="supplemental",materialDiagnostic);
        result["source"]=source;result["ownerInstanceId"]=ownerId;result["celInstanceId"]=renderCelId;
        if(inventoryCamera!=null)
        {
            result["sourceCelInstanceId"]=celId;result["inventoryCameraInstanceId"]=inventoryCamera.GetInstanceID();
            result["inventorySelection"]="Current native inventory stream for exact world owner and source avatar; rendered clone identity recorded at capture";
            result["scope"]="Synchronous snapshot of the existing visible native inventory clone and current pose, with studio camera and the reported lighting mode. Bone-based framing; no native geometry export. Inventory presentation evidence only, not world, combat, animation or lifecycle acceptance.";
        }
        result["equipment"]=equipment;
        result["appearanceFixture"]=appearance;
        if(materialDiagnostic!=null)
        {
            result["diagnosticCapture"]=true;result["artAcceptanceEligible"]=false;
            result["scope"]="Diagnostic material-slot identification only. One native inventory-clone material slot was temporarily magenta during the studio camera render and restored. Never ordinary art acceptance or native presentation evidence.";
        }
        return result;
    }

    JObject RenderPlayerStudioAvatar(CharacterEventListener avatar,string path,string view,Vector3? fixedForward=null,float minimumSpan=0f,bool headFraming=false,bool supplementalLighting=true,JObject materialDiagnostic=null)
    {
        RequireSinglePlayer();CatalogNoLinks(root);CatalogNoLinks(output);
        if(view!="front" && view!="three-quarter" && view!="side" && view!="other-side" && view!="rear-quarter" && view!="back")throw new ArgumentException("Invalid studio view");
        if(avatar==null || !SceneOwner(avatar) || !avatar.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Active native avatar required for studio render");
        Renderer[] renderers=avatar.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0 || renderers.Length>128)throw new InvalidOperationException("Avatar renderer count outside studio limit");
        int drawableRenderers=0;
        foreach(Renderer renderer in renderers)
            if(renderer!=null && renderer.enabled && renderer.gameObject.activeInHierarchy)drawableRenderers++;
        // An active world avatar can be hidden while the inventory displays a separate native avatar.
        if(drawableRenderers==0)
            throw new InvalidOperationException("Selected avatar has no enabled active renderers. Close inventory or other native UI hiding this avatar and observe it again before capture.");
        // Frame only from permitted live bone transforms, never native vertices or mesh bounds.
        Vector3 up=fixedForward.HasValue?Vector3.up:avatar.transform.up.normalized;
        float yaw=view=="back"?180f:view=="rear-quarter"?135f:view=="side"?90f:view=="other-side"?-90f:view=="three-quarter"?25f:0f;
        Vector3 direction=Quaternion.AngleAxis(yaw,up)*(fixedForward.HasValue?fixedForward.Value:avatar.transform.forward.normalized);
        Vector3 right=Vector3.Cross(up,direction).normalized;
        float low=float.MaxValue,high=float.MinValue,left=float.MaxValue,rightmost=float.MinValue,near=float.MaxValue,far=float.MinValue;int bones=0;Transform headBone=null;
        foreach(SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(renderer.bones!=null)foreach(Transform bone in renderer.bones)
            {
                if(bone==null)continue;
                if(headFraming && bone.name=="Head_M")
                {
                    if(headBone!=null && headBone!=bone)throw new InvalidOperationException("Ambiguous native head bone");
                    headBone=bone;
                }
                Vector3 offset=bone.position-avatar.transform.position;
                float height=Vector3.Dot(offset,up),horizontal=Vector3.Dot(offset,right),depth=Vector3.Dot(offset,direction);
                low=Math.Min(low,height);high=Math.Max(high,height);
                left=Math.Min(left,horizontal);rightmost=Math.Max(rightmost,horizontal);
                near=Math.Min(near,depth);far=Math.Max(far,depth);bones++;
            }
        float span=high-low;
        if(bones==0 || float.IsNaN(span) || float.IsInfinity(span) || span<.05f || span>100f)
            throw new InvalidOperationException("Usable native bone framing unavailable");
        float centerHeight=low+span*.5f;
        Vector3 center=avatar.transform.position+up*centerHeight;
        if(fixedForward.HasValue)
        {
            // Combat lunges move the animated bones independently of the CEL root.
            center+=right*((left+rightmost)*.5f)+direction*((near+far)*.5f);
            span=Math.Max(span,Math.Max((rightmost-left)/.75f,(far-near)*.5f));
        }
        if(headFraming)
        {
            if(headBone==null)throw new InvalidOperationException("Exact native Head_M bone required for head framing");
            center=headBone.position+up*(span*.05f);
            span*=.40f;
        }
        span=Math.Max(span,minimumSpan);
        if(float.IsNaN(span) || float.IsInfinity(span) || span>100f)
            throw new InvalidOperationException("Usable native bone extent unavailable");
        int layer=-1;Renderer[] sceneRenderers=UnityEngine.Object.FindObjectsOfType<Renderer>();
        for(int candidate=31;candidate>=8;candidate--)
        {
            bool used=false;foreach(Renderer renderer in sceneRenderers)
                if(renderer!=null && renderer.gameObject.layer==candidate){used=true;break;}
            if(!used){layer=candidate;break;}
        }
        if(layer<0)throw new InvalidOperationException("No unused render layer available");
        if(File.Exists(path))throw new InvalidOperationException("Studio output already exists");
        Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
        foreach(Renderer renderer in renderers)if(renderer!=null && !layers.ContainsKey(renderer.gameObject))layers.Add(renderer.gameObject,renderer.gameObject.layer);
        Dictionary<SkinnedMeshRenderer,bool> offscreenUpdates=new Dictionary<SkinnedMeshRenderer,bool>();
        foreach(SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(renderer!=null){offscreenUpdates.Add(renderer,renderer.updateWhenOffscreen);renderer.updateWhenOffscreen=true;}
        GameObject cameraObject=null,keyObject=null,fillObject=null;RenderTexture target=null;Texture2D image=null;
        RenderTexture prior=RenderTexture.active;byte[] png=null;JObject studioCamera=null,diagnosticReceipt=null;JArray studioApparel=null;
        try
        {
            foreach(GameObject part in layers.Keys)part.layer=layer;
            cameraObject=new GameObject("FTK test player studio camera");Camera camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.18f,1f);
            camera.cullingMask=1<<layer;camera.orthographic=true;camera.orthographicSize=span*.85f;
            camera.nearClipPlane=.01f;camera.farClipPlane=span*10f;camera.useOcclusionCulling=false;
            camera.transform.position=center+direction*span*4f;camera.transform.LookAt(center,up);
            target=new RenderTexture(768,1024,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            if(supplementalLighting)
            {
                keyObject=new GameObject("FTK test studio key");Light key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.cullingMask=1<<layer;key.intensity=1f;
                key.transform.rotation=camera.transform.rotation*Quaternion.Euler(25f,-25f,0);
                fillObject=new GameObject("FTK test studio fill");Light fill=fillObject.AddComponent<Light>();fill.type=LightType.Directional;fill.cullingMask=1<<layer;fill.intensity=.4f;
                fill.transform.rotation=camera.transform.rotation*Quaternion.Euler(-10f,50f,0);
            }
            if(materialDiagnostic==null)camera.Render();
            else diagnosticReceipt=RenderStudioMaterialDiagnostic(avatar,materialDiagnostic,delegate { camera.Render(); });
            RenderTexture.active=target;
            studioCamera=new JObject{{"frame",Time.frameCount},{"worldToCameraMatrix",Matrix(camera.worldToCameraMatrix)},
                {"projectionMatrix",Matrix(camera.projectionMatrix)},{"position",Vec(camera.transform.position)},
                {"rotation",Quat(camera.transform.rotation)},
                {"matrixConvention","Row-major arrays; Unity camera matrices, not GPU-adjusted projection. PNG origin is top-left."}};
            studioApparel=OriginalApparelMaterials(avatar.gameObject);
            image=new Texture2D(768,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply();png=image.EncodeToPNG();
        }
        finally
        {
            foreach(KeyValuePair<GameObject,int> part in layers)if(part.Key!=null)part.Key.layer=part.Value;
            foreach(KeyValuePair<SkinnedMeshRenderer,bool> entry in offscreenUpdates)if(entry.Key!=null)entry.Key.updateWhenOffscreen=entry.Value;
            RenderTexture.active=prior;
            if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
            if(keyObject!=null)UnityEngine.Object.DestroyImmediate(keyObject);
            if(fillObject!=null)UnityEngine.Object.DestroyImmediate(fillObject);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
        File.WriteAllBytes(path,png);
        return new JObject{{"ok",true},{"png",path},{"sha256",CatalogHash(path)},{"view",view},
            {"coreIdentity",PreviewCoreIdentity()},{"helperIdentity",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},
            {"lighting",supplementalLighting?"supplemental":"native"},{"addedLightCount",supplementalLighting?2:0},{"studioCamera",studioCamera},
            {"studioOriginalApparelMaterials",studioApparel},
            {"nativeMaterialDiagnostic",diagnosticReceipt},
            {"width",768},{"height",1024},{"framingSpan",span},{"fixedFacing",fixedForward.HasValue},{"layersRestored",true},
            {"drawableRendererCount",drawableRenderers},{"framingCenter",Vec(center)},
            {"framing",headFraming?"head":"body"},{"headBoneInstanceId",headBone==null?0:headBone.GetInstanceID()},
            {"framingBasis",headFraming?"live-head-bone":fixedForward.HasValue?"live-bone-projected-extents":"root-horizontal-and-live-bone-height"},
            {"boneHorizontalSpan",rightmost-left},{"boneDepthSpan",far-near},
            {"scope","Synchronous render of existing native avatar and current pose; bone-based framing, no native geometry export. Lighting mode records whether temporary key/fill lights supplement existing lighting. Native mode only omits those lights; studio camera and temporary layer isolation remain. Presentation image, not gameplay acceptance."}};
    }
}
