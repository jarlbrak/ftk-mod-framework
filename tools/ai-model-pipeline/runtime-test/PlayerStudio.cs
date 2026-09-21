using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject PlayerStudio(JObject command)
    {
        CatalogKeys(command,"id","session","op","source","ownerInstanceId","celInstanceId","view");
        RequireSinglePlayer();CatalogNoLinks(root);CatalogNoLinks(output);
        string source=Str(command,"source"),view=Str(command,"view");
        if(view!="front" && view!="three-quarter" && view!="back")throw new ArgumentException("view must be front, three-quarter or back");
        int ownerId=LeaseObservationPin.ExactId(command,"ownerInstanceId",false);
        int celId=LeaseObservationPin.ExactId(command,"celInstanceId",false);
        if(source=="preview")PreviewRaceRequireStudioReady(ownerId);
        CharacterEventListener avatar=null;JToken equipment=null;
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
        else if(source=="world")
        {
            if(FTKHub.Instance!=null)foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
                if(cow!=null && SceneOwner(cow) && cow.GetInstanceID()==ownerId)
                {
                    if(cow.m_CharacterStats==null || cow.m_CharacterStats.m_IsInCombat || !cow.IsOwner)
                        throw new InvalidOperationException("Owned noncombat world hero required");
                    avatar=cow.m_Avatar;
                    if(avatar==null || avatar.m_CharacterOverworld!=cow)throw new InvalidOperationException("World avatar is not reciprocal");
                    equipment=EquipmentView(cow);break;
                }
        }
        else throw new ArgumentException("source must be preview or world");
        if(avatar==null || avatar.GetInstanceID()!=celId || !SceneOwner(avatar) || !avatar.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Exact active native avatar unavailable");
        Renderer[] renderers=avatar.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0 || renderers.Length>128)throw new InvalidOperationException("Avatar renderer count outside studio limit");
        // Frame only from permitted live bone transforms, never native vertices or mesh bounds.
        Vector3 up=avatar.transform.up.normalized;float low=float.MaxValue,high=float.MinValue;int bones=0;
        foreach(SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(renderer.bones!=null)foreach(Transform bone in renderer.bones)
            {
                if(bone==null)continue;
                float height=Vector3.Dot(bone.position-avatar.transform.position,up);
                low=Math.Min(low,height);high=Math.Max(high,height);bones++;
            }
        float span=high-low;
        if(bones==0 || float.IsNaN(span) || float.IsInfinity(span) || span<.05f || span>100f)
            throw new InvalidOperationException("Usable native bone framing unavailable");
        int layer=-1;Renderer[] sceneRenderers=UnityEngine.Object.FindObjectsOfType<Renderer>();
        for(int candidate=31;candidate>=8;candidate--)
        {
            bool used=false;foreach(Renderer renderer in sceneRenderers)
                if(renderer!=null && renderer.gameObject.layer==candidate){used=true;break;}
            if(!used){layer=candidate;break;}
        }
        if(layer<0)throw new InvalidOperationException("No unused render layer available");
        string path=Path.Combine(output,Token(Str(command,"id"))+".png");
        if(File.Exists(path))throw new InvalidOperationException("Studio output already exists");
        JObject core=PreviewCoreIdentity(),helper=ScaleIdentity(typeof(RuntimeModelTest).Assembly);
        Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
        foreach(Renderer renderer in renderers)if(renderer!=null && !layers.ContainsKey(renderer.gameObject))layers.Add(renderer.gameObject,renderer.gameObject.layer);
        GameObject cameraObject=null,keyObject=null,fillObject=null;RenderTexture target=null;Texture2D image=null;
        RenderTexture prior=RenderTexture.active;byte[] png=null;
        try
        {
            foreach(GameObject part in layers.Keys)part.layer=layer;
            Vector3 center=avatar.transform.position+up*(low+span*.5f);
            float yaw=view=="back"?180f:view=="three-quarter"?25f:0f;
            Vector3 direction=Quaternion.AngleAxis(yaw,up)*avatar.transform.forward.normalized;
            cameraObject=new GameObject("FTK test player studio camera");Camera camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.18f,1f);
            camera.cullingMask=1<<layer;camera.orthographic=true;camera.orthographicSize=span*.85f;
            camera.nearClipPlane=.01f;camera.farClipPlane=span*10f;camera.useOcclusionCulling=false;
            camera.transform.position=center+direction*span*4f;camera.transform.LookAt(center,up);
            target=new RenderTexture(768,1024,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            keyObject=new GameObject("FTK test studio key");Light key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.cullingMask=1<<layer;key.intensity=1f;
            key.transform.rotation=camera.transform.rotation*Quaternion.Euler(25f,-25f,0);
            fillObject=new GameObject("FTK test studio fill");Light fill=fillObject.AddComponent<Light>();fill.type=LightType.Directional;fill.cullingMask=1<<layer;fill.intensity=.4f;
            fill.transform.rotation=camera.transform.rotation*Quaternion.Euler(-10f,50f,0);
            camera.Render();RenderTexture.active=target;
            image=new Texture2D(768,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply();png=image.EncodeToPNG();
        }
        finally
        {
            foreach(KeyValuePair<GameObject,int> part in layers)if(part.Key!=null)part.Key.layer=part.Value;
            RenderTexture.active=prior;
            if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
            if(keyObject!=null)UnityEngine.Object.DestroyImmediate(keyObject);
            if(fillObject!=null)UnityEngine.Object.DestroyImmediate(fillObject);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
        File.WriteAllBytes(path,png);
        return new JObject{{"ok",true},{"png",path},{"sha256",CatalogHash(path)},{"source",source},{"view",view},
            {"ownerInstanceId",ownerId},{"celInstanceId",celId},{"equipment",equipment},{"coreIdentity",core},{"helperIdentity",helper},
            {"width",768},{"height",1024},{"layersRestored",true},
            {"scope","Synchronous render of existing native avatar and current pose; bone-based framing, no native geometry export. Studio lights add to existing ambient lighting. Presentation image, not gameplay acceptance."}};
    }
}
