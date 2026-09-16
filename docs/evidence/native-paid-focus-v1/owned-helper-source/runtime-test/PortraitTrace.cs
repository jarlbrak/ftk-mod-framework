using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using HarmonyLib;
using GridEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class PortraitScope
    {
        public string overload,identity,first,fallback;public bool pose,captured,forwardRow;
        public FTK_enemyCombat row;public CharacterEventListener source;public Texture2D texture;
        public JObject sourceView,marker,record;public JArray callers;public EnemyLifetimeSnapshot lifetime;
    }
    readonly PortraitTraceState<PortraitScope,JObject> portraitTrace=new PortraitTraceState<PortraitScope,JObject>();
    static RuntimeModelTest portraitObserver;
    bool portraitHooks,portraitArmed;string portraitNonce,portraitRoot,portraitArmId;
    FTK_enemyCombat portraitRow;CharacterEventListener portraitRowSource;JObject portraitProfile,portraitEvidence;int portraitErrors;
    bool PortraitEnabled()
    {
        if(!portraitArmed)return false;
        if(portraitNonce!=sessionId || portraitRoot!=root || Environment.GetEnvironmentVariable("FTK_MODEL_TEST")!="1"
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT"))
            || Path.GetFullPath(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT")).TrimEnd(Path.DirectorySeparatorChar)!=root
            || Path.GetFullPath(BepInEx.Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar)!=root)
        {portraitArmed=false;portraitTrace.Clear();return false;}
        if(portraitRow==null || portraitRow.m_EnemyAsset!=portraitRowSource || !object.ReferenceEquals(FTK_enemyCombatDB.GetDB().GetEntryByStringID(portraitRow.m_ID),portraitRow)){portraitArmed=false;return false;}
        return portraitTrace.Count<PortraitTraceState<PortraitScope,JObject>.Capacity;
    }
    void InstallPortraitHooks()
    {
        if(portraitHooks)return;
        Harmony harmony=new Harmony("com.ftkmf.runtime-model-test.passive-portrait");
        Type[] common={typeof(Texture2D),typeof(string),typeof(string),typeof(CharacterEventListener.DisplayLayer),typeof(int),typeof(bool)};
        List<Type> rowArgs=new List<Type>();rowArgs.Add(typeof(FTK_enemyCombat));rowArgs.AddRange(common);
        List<Type> avatarArgs=new List<Type>();avatarArgs.Add(typeof(CharacterEventListener));avatarArgs.AddRange(common);avatarArgs.Add(typeof(bool));
        MethodInfo row=typeof(OffscreenCamera).GetMethod("Snapshot",Members,null,rowArgs.ToArray(),null);
        MethodInfo avatar=typeof(OffscreenCamera).GetMethod("Snapshot",Members,null,avatarArgs.ToArray(),null);
        MethodInfo position=typeof(OffscreenCamera).GetMethod("SetTargetPosition",Members,null,new[]{typeof(string),typeof(string)},null);
        MethodInfo render=typeof(OffscreenCamera).GetMethod("DoRender",Members,null,new[]{typeof(bool)},null);
        if(row==null || avatar==null || position==null || render==null)throw new InvalidOperationException("Exact native portrait methods unavailable.");
        harmony.Patch(row,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitRowPrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitScopeFinalizer",Statics)),null);
        harmony.Patch(avatar,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitAvatarPrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitScopeFinalizer",Statics)),null);
        HarmonyMethod marker=new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitMarkerPrefix",Statics));marker.priority=Priority.Last;
        harmony.Patch(position,marker);harmony.Patch(render,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PortraitRenderPrefix",Statics)));
        portraitHooks=true;portraitObserver=this;
    }
    JObject PortraitWatch(JObject command)
    {
        CatalogKeys(command,"id","session","op","enemy");string key=Str(command,"enemy");JObject profile=null;
        foreach(JObject candidate in CatalogProfiles(false))if(Str(candidate,"key")==key){if(profile!=null)throw new ArgumentException("Ambiguous model profile.");profile=candidate;}
        FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID(key);
        if(profile==null || row==null || row.m_ID!=key || row.m_EnemyAsset==null)throw new ArgumentException("Exact loaded registered enemy model required.");
        Type registry=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry",true);
        object[] args={key,0,new[]{typeof(FTK_enemyCombatDB)}};
        if(!(bool)registry.GetMethod("TryGetSyntheticId",Statics).Invoke(null,args)
            || !object.ReferenceEquals(FTK_enemyCombatDB.GetDB().GetEntryByInt((int)args[1]),row))throw new ArgumentException("Exact synthetic row required.");
        JArray assets=new JArray();
        foreach(JObject renderer in (JArray)profile["renderers"])
            foreach(string field in new[]{"glbFile","textureFile"})
            {
                string file=Str(renderer,field);if(file==null)continue;
                AssetName(file,field=="glbFile"?".glb":".png");string path=Path.Combine(root,"BepInEx/plugins/FTKModFramework_content/models/"+file);CatalogNoLinks(path);
                assets.Add(new JObject{{"field",field},{"file",file},{"sha256",CatalogHash(path)}});
            }
        JObject evidence=new JObject{{"helper",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},{"framework",ScaleIdentity(CatalogAssembly("FTKModFramework"))},
            {"gameAssembly",ScaleIdentity(typeof(OffscreenCamera).Assembly)},{"assets",assets},{"rowSourceCelInstanceId",row.m_EnemyAsset.GetInstanceID()}};
        InstallPortraitHooks();portraitArmed=false;portraitTrace.Clear();portraitErrors=0;
        portraitEvidence=evidence;portraitRow=row;portraitRowSource=row.m_EnemyAsset;portraitProfile=(JObject)profile.DeepClone();portraitNonce=sessionId;portraitRoot=root;portraitArmId=Str(command,"id");portraitArmed=true;
        return PortraitWatchState();
    }
    JObject PortraitWatchState()
    {
        if(portraitArmed)PortraitEnabled();JArray records=new JArray();foreach(JObject value in portraitTrace.Read())records.Add(value.DeepClone());
        return new JObject{{"ok",true},{"provenance","passive_native_portrait_before_DoRender"},{"armed",portraitArmed},{"capacity",8},
            {"saturated",portraitTrace.Count>=8},{"armCommandId",portraitArmId},{"watchSession",portraitNonce},{"enemy",portraitRow==null?null:portraitRow.m_ID},
            {"profile",portraitProfile},{"armedEvidence",portraitEvidence},{"telemetryErrors",portraitErrors},{"records",records},
            {"limitations","Passive post-pose/post-placement scalar observation only. No forced render, geometry dump, lease acquisition, camera/material mutation or cache/occlusion conclusion. Unknown identities are not attributed to the watched row."}};
    }
    JObject PortraitWatchStop(){portraitArmed=false;return PortraitWatchState();}
    static JArray PortraitCallers()
    {
        JArray result=new JArray();StackFrame[] frames=new StackTrace(false).GetFrames();if(frames==null)return result;
        for(int i=0;i<Math.Min(frames.Length,24);i++){MethodBase m=frames[i].GetMethod();if(m!=null)result.Add((m.DeclaringType==null?"":m.DeclaringType.FullName+".")+m.Name);}
        return result;
    }
    static void PortraitRowPrefix(OffscreenCamera __instance,FTK_enemyCombat _ec,Texture2D _texture,string _camName,string _camName2,bool _isPoseState,out PortraitTraceState<PortraitScope,JObject>.Token __state)
    {
        __state=null;RuntimeModelTest observer=portraitObserver;if(observer==null)return;
        try
        {
            if(!observer.PortraitEnabled())return;
            PortraitScope scope=new PortraitScope{overload="row",row=_ec,source=_ec==null?null:_ec.m_EnemyAsset,texture=_texture,first=_camName,fallback=_camName2,pose=_isPoseState,
                identity=object.ReferenceEquals(_ec,observer.portraitRow)?"exact_watched_row":"unmatched_row",forwardRow=true};
            __state=observer.portraitTrace.Begin(__instance.GetInstanceID(),scope);scope.callers=PortraitCallers();
        }catch
        {
            observer.portraitErrors++;
            if(__state==null)try{__state=observer.portraitTrace.Begin(__instance.GetInstanceID(),null);}catch{ }
        }
    }
    static void PortraitAvatarPrefix(OffscreenCamera __instance,CharacterEventListener _avatar,Texture2D _texture,string _camName,string _camName2,bool _isPoseState,out PortraitTraceState<PortraitScope,JObject>.Token __state)
    {
        __state=null;RuntimeModelTest observer=portraitObserver;if(observer==null)return;
        try
        {
            if(!observer.PortraitEnabled())return;PortraitScope prior=observer.portraitTrace.Current(__instance.GetInstanceID());
            PortraitScope scope=new PortraitScope{overload="CEL",source=_avatar,texture=_texture,first=_camName,fallback=_camName2,pose=_isPoseState,identity="unknown"};
            // Always push first: telemetry failure cannot inherit an unrelated outer identity.
            __state=observer.portraitTrace.Begin(__instance.GetInstanceID(),scope);scope.callers=PortraitCallers();
            if(prior!=null && prior.forwardRow && prior.source==_avatar && object.ReferenceEquals(prior.row,observer.portraitRow))
            {scope.row=prior.row;scope.identity="exact_row_forwarded_to_native_source";}
            else if(_avatar!=null)
            {
                EnemyDummy dummy=_avatar.m_Dummy as EnemyDummy;
                if(dummy!=null && dummy.m_EventListener==_avatar && object.ReferenceEquals(dummy.m_EnemyCombat,observer.portraitRow) && dummy.m_EnemyType==observer.portraitRow.m_ID)
                {scope.row=dummy.m_EnemyCombat;scope.identity="verified_live_enemy_dummy";}
            }
            if(object.ReferenceEquals(scope.row,observer.portraitRow))scope.sourceView=PortraitCel(_avatar,false);
            try{scope.lifetime=observer.EnemyLifetimeBegin(scope,__instance);}catch(Exception error){observer.EnemyLifetimeError("native-prefix",error);}
        }catch
        {
            observer.portraitErrors++;
            if(__state==null)try{__state=observer.portraitTrace.Begin(__instance.GetInstanceID(),null);}catch{ }
        }
    }
    static Exception PortraitScopeFinalizer(Exception __exception,PortraitTraceState<PortraitScope,JObject>.Token __state)
    {
        try
        {
            if(portraitObserver!=null && __state!=null)
            {
                PortraitScope scope=portraitObserver.portraitTrace.Current(__state.camera);
                if(scope!=null)
                {
                    PortraitFinalization.Complete(scope.record,__exception);
                    try{portraitObserver.EnemyLifetimeFinalized(scope.lifetime,__exception);}catch(Exception error){portraitObserver.EnemyLifetimeError("native-finalizer-observation",error);}
                }
            }
        }catch{ }
        finally{try{if(portraitObserver!=null)portraitObserver.portraitTrace.End(__state);}catch{ }}
        return __exception;
    }
    static void PortraitMarkerPrefix(OffscreenCamera __instance,string _camName,string _camName2)
    {
        RuntimeModelTest o=portraitObserver;if(o==null)return;
        try
        {
            if(!o.PortraitEnabled())return;PortraitScope s=o.portraitTrace.Current(__instance.GetInstanceID());
            if(s==null || !object.ReferenceEquals(s.row,o.portraitRow))return;
            s.marker=new JObject{{"firstArgumentAtLastPrefix",_camName},{"fallbackArgumentAtLastPrefix",_camName2},
                {"scope","Arguments observed after ordinary-priority marker selectors, before native placement; not an independent proof of native traversal result."}};
            Transform target=__instance.m_TargetObject==null?null:__instance.m_TargetObject.transform;
            if(target!=null){bool truncated;List<Transform> nodes=PortraitNodes(target,out truncated);JArray candidates=new JArray();
                foreach(Transform t in nodes)if(t.name==_camName || t.name==_camName2)candidates.Add(PortraitTransform(t,target));
                s.marker["matchingNamedTransformsBeforePlacement"]=candidates;s.marker["truncated"]=truncated;}
        }catch{o.portraitErrors++;}
    }
    static void PortraitRenderPrefix(OffscreenCamera __instance)
    {
        RuntimeModelTest o=portraitObserver;if(o==null)return;
        try
        {
            if(!o.PortraitEnabled())return;PortraitScope s=o.portraitTrace.Current(__instance.GetInstanceID());
            if(s==null || s.captured || !object.ReferenceEquals(s.row,o.portraitRow) || s.first!="PortraitCam")return;
            s.captured=true;JObject record=new JObject{{"session",o.sessionId},{"frame",Time.frameCount},{"time",Time.time},{"overload",s.overload},{"identityResolution",s.identity},
                {"row",s.row.m_ID},{"sourceCelInstanceId",s.source==null?0:s.source.GetInstanceID()},{"callerMethods",s.callers},{"poseRequested",s.pose},{"marker",s.marker},
                {"capturePoint","Immediately before native DoRender, after native pose sampling and marker placement"},{"source",s.sourceView}};
            // Reserve the bounded record before reading target telemetry so failures remain visible.
            if(!o.portraitTrace.Add(record))return;s.record=record;
            try
            {
                Camera camera=__instance.GetComponent<Camera>();GameObject target=__instance.m_TargetObject;
                CharacterEventListener cel=target==null?null:target.GetComponent<CharacterEventListener>();
                record["target"]=PortraitCel(cel,true);
                try{o.EnemyLifetimeBeforeRender(s.lifetime,__instance);}catch(Exception error){o.EnemyLifetimeError("native-pre-render",error);}
                record["textureRequested"]=PortraitTexture(s.texture);record["textureCurrent"]=PortraitTexture(__instance.m_Texture2D);
                record["offscreenCameraInstanceId"]=__instance.GetInstanceID();record["renderTexture"]=PortraitTexture(__instance.m_RenderTexture);
                record["camera"]=camera==null?null:new JObject{{"instanceId",camera.GetInstanceID()},{"worldToCamera",SampleMatrix(camera.worldToCameraMatrix)},
                    {"projection",SampleMatrix(camera.projectionMatrix)},{"localToWorld",SampleMatrix(camera.transform.localToWorldMatrix)},
                    {"position",Vec(camera.transform.position)},{"rotation",Quat(camera.transform.rotation)},{"fieldOfView",camera.fieldOfView},
                    {"aspect",camera.aspect},{"orthographic",camera.orthographic},{"orthographicSize",camera.orthographicSize},{"near",camera.nearClipPlane},{"far",camera.farClipPlane},
                    {"pixelRect",new JArray(camera.pixelRect.x,camera.pixelRect.y,camera.pixelRect.width,camera.pixelRect.height)}};
                record["telemetryComplete"]=true;
            }catch(Exception ex){record["telemetryComplete"]=false;record["telemetryError"]=ex.GetType().Name+": "+ex.Message;o.portraitErrors++;}
        }catch{o.portraitErrors++;}
    }
    static JObject PortraitTexture(Texture value){return value==null?null:new JObject{{"instanceId",value.GetInstanceID()},{"name",value.name},{"width",value.width},{"height",value.height}};}
    static List<Transform> PortraitNodes(Transform root,out bool truncated)
    {
        List<Transform> nodes=new List<Transform>();Queue<Transform> pending=new Queue<Transform>();pending.Enqueue(root);truncated=false;
        while(pending.Count>0 && nodes.Count<256)
        {
            Transform t=pending.Dequeue();nodes.Add(t);
            for(int i=0;i<t.childCount;i++){if(nodes.Count+pending.Count>=256){truncated=true;break;}pending.Enqueue(t.GetChild(i));}
        }
        truncated|=pending.Count>0;return nodes;
    }
    static JObject PortraitTransform(Transform t,Transform root)
    {
        return new JObject{{"instanceId",t.GetInstanceID()},{"name",t.name},{"path",Relative(t,root)},{"localPosition",Vec(t.localPosition)},
            {"localRotation",Quat(t.localRotation)},{"localScale",Vec(t.localScale)},{"localMatrix",SampleMatrix(Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale))},
            {"modelMatrix",SampleMatrix(KrakenLocalChain(t,root))},{"worldMatrix",SampleMatrix(t.localToWorldMatrix)}};
    }
    static JObject PortraitCel(CharacterEventListener cel,bool poses)
    {
        if(cel==null)return null;bool truncated;List<Transform> nodes=PortraitNodes(cel.transform,out truncated);JArray renderers=new JArray();int boneBudget=256;
        foreach(Transform t in nodes)
        {
            SkinnedMeshRenderer[] found=t.GetComponents<SkinnedMeshRenderer>();
            foreach(SkinnedMeshRenderer r in found)
            {
                if(renderers.Count>=32){truncated=true;break;}
                Mesh mesh=r.sharedMesh;Transform[] bones=r.bones;JArray materials=new JArray(),boneData=new JArray();Material[] shared=r.sharedMaterials;
                for(int i=0;i<Math.Min(shared.Length,16);i++)
                {Material m=shared[i];materials.Add(m==null?null:new JObject{{"instanceId",m.GetInstanceID()},{"name",m.name},{"shader",m.shader==null?null:m.shader.name},
                    {"mainTexture",m.HasProperty("_MainTex")?PortraitTexture(m.GetTexture("_MainTex")):null}});}
                bool bonesTruncated=false;
                if(poses)for(int i=0;i<bones.Length;i++)
                {if(boneBudget--<=0){bonesTruncated=true;break;}boneData.Add(bones[i]==null?null:PortraitTransform(bones[i],cel.transform));}
                renderers.Add(new JObject{{"instanceId",r.GetInstanceID()},{"path",Relative(r.transform,cel.transform)},{"enabled",r.enabled},{"active",r.gameObject.activeInHierarchy},
                    {"sharedMeshId",mesh==null?0:mesh.GetInstanceID()},{"meshName",mesh==null?null:mesh.name},{"vertexCount",mesh==null?0:mesh.vertexCount},
                    {"subMeshCount",mesh==null?0:mesh.subMeshCount},{"rendererWorld",SampleMatrix(r.localToWorldMatrix)},
                    {"rootBoneId",r.rootBone==null?0:r.rootBone.GetInstanceID()},{"boneCount",bones.Length},{"bones",boneData},{"bonesTruncated",bonesTruncated},
                    {"sharedMaterials",materials},{"materialsTruncated",shared.Length>16}});
            }
            if(renderers.Count>=32){truncated=true;break;}
        }
        JObject result=new JObject{{"instanceId",cel.GetInstanceID()},{"name",cel.name},{"rootWorld",SampleMatrix(cel.transform.localToWorldMatrix)},
            {"rootLocal",SampleMatrix(Matrix4x4.TRS(cel.transform.localPosition,cel.transform.localRotation,cel.transform.localScale))},
            {"resourceLease",ReadLease(cel)},{"renderers",renderers},{"traversalOrRenderersTruncated",truncated},{"nodeCount",nodes.Count}};
        Animator animator=cel.m_Animator;JArray clips=new JArray();
        if(animator!=null && animator.runtimeAnimatorController!=null)
        {
            AnimationClip[] all=animator.runtimeAnimatorController.animationClips;
            for(int i=0;i<Math.Min(all.Length,256);i++)if(all[i].name=="portrait")clips.Add(new JObject{{"instanceId",all[i].GetInstanceID()},{"name",all[i].name},{"length",all[i].length}});
            result["animator"]=new JObject{{"instanceId",animator.GetInstanceID()},{"controller",animator.runtimeAnimatorController.name},{"portraitNamedClips",clips},
                {"clipsTruncated",all.Length>256},{"poseSamplingEvidence","Native pose path searches lowercase portrait and samples at one second; no SampleAnimation hook observes the call itself."}};
        }
        return result;
    }
}
