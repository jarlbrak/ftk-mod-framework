using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    sealed class SampleNode
    {
        public Transform target;
        public Vector3 position,scale;
        public Quaternion rotation;
        public string path;
    }
    static readonly string[] KrakenMainPaths={"Root_M/base","Root_M/base/body","Root_M/base/body/neck",
        "Root_M/base/body/neck/head","Root_M/base/body/neck/head/topHead","Root_M/base/body/neck/eye"};
    static readonly string[] KrakenOldPaths={"Root_M/joint1","Root_M/joint1/neck","Root_M/joint1/neck/head",
        "Root_M/joint1/neck/head/topHead","Root_M/joint1/neck/jaw"};
    static void CopySampleTree(Transform source,Transform destination,string path,List<SampleNode> nodes)
    {
        if(nodes.Count>=256)throw new InvalidOperationException("Fixture hierarchy exceeds256 transforms.");
        destination.localPosition=source.localPosition;destination.localRotation=source.localRotation;destination.localScale=source.localScale;
        nodes.Add(new SampleNode{target=destination,path=path,position=source.localPosition,rotation=source.localRotation,scale=source.localScale});
        HashSet<string> siblings=new HashSet<string>();
        foreach(Transform child in source)
        {
            if(!siblings.Add(child.name))throw new InvalidOperationException("Ambiguous source hierarchy sibling name.");
            if(nodes.Count>=256)throw new InvalidOperationException("Fixture hierarchy exceeds256 transforms.");
            GameObject copy=new GameObject(child.name);copy.transform.SetParent(destination,false);
            CopySampleTree(child,copy.transform,path==""?child.name:path+"/"+child.name,nodes);
        }
    }
    static void ResetSampleTree(List<SampleNode> nodes)
    {
        foreach(SampleNode node in nodes)
        {node.target.localPosition=node.position;node.target.localRotation=node.rotation;node.target.localScale=node.scale;}
    }
    static Transform SampleTransform(List<SampleNode> nodes,string path)
    {
        Transform found=null;foreach(SampleNode node in nodes)if(node.path==path)
        {if(found!=null)throw new InvalidOperationException("Ambiguous sample path.");found=node.target;}
        if(found==null)throw new InvalidOperationException("Required sample path absent: "+path);return found;
    }
    static void AssertTransformOnly(GameObject root,int count,Animator allowedAnimator=null)
    {
        Component[] components=root.GetComponentsInChildren<Component>(true);
        if(components.Length!=count+(allowedAnimator==null?0:1))throw new InvalidOperationException("Fixture component count changed.");
        foreach(Component component in components)if(!(component is Transform) && component!=allowedAnimator)throw new InvalidOperationException("Unexpected fixture component detected.");
    }
    sealed class KrakenPlayableSampler
    {
        public PlayableGraph graph;
        public AnimationClipPlayable playable;
        public Animator animator;
        public Avatar nativeAvatar;
        public void Create(GameObject root,Avatar avatar,AnimationClip clip)
        {
            if(avatar==null || !avatar.isValid)throw new InvalidOperationException("Valid native Avatar required; no generic fallback is automatic.");
            nativeAvatar=avatar;animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController=null;animator.avatar=avatar;
            animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create();graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetSpeed(0);
            AnimationPlayableOutput output=AnimationPlayableOutput.Create(graph,"kraken-single-clip-fixture",animator);
            output.SetSourcePlayable(playable);output.SetWeight(1);
        }
        public JObject InitializeAfterActivation()
        {
            if(!animator.gameObject.activeInHierarchy)throw new InvalidOperationException("Activate owned root before Animator initialization.");
            bool before=animator.isInitialized;
            animator.Rebind();graph.Play();playable.SetTime(0);graph.Evaluate(0);
            return new JObject{{"rootActive",animator.gameObject.activeInHierarchy},{"initializedBeforeRebind",before},
                {"initializedAfterRebindAndEvaluate",animator.isInitialized},{"nativeAvatarValid",nativeAvatar.isValid},
                {"nativeAvatarName",nativeAvatar.name},{"sequence","activate, Rebind, graph.Play, SetTime(0), Evaluate(0)"}};
        }
        public void Audit()
        {
            if(animator==null || !animator.enabled || !animator.isInitialized || animator.runtimeAnimatorController!=null || animator.avatar!=nativeAvatar || animator.fireEvents || animator.applyRootMotion
                || animator.cullingMode!=AnimatorCullingMode.AlwaysAnimate || !graph.IsValid() || graph.GetTimeUpdateMode()!=DirectorUpdateMode.Manual
                || graph.GetPlayableCount()!=1 || graph.GetOutputCount()!=1 || playable.GetApplyFootIK())
                throw new InvalidOperationException("Manual controller-free clip fixture invariants changed.");
        }
        public void Sample(float time){Audit();playable.SetTime(time);graph.Evaluate(0);Audit();}
        public JObject View()
        {
            return new JObject{{"animatorInstanceId",animator.GetInstanceID()},{"animatorEnabled",animator.enabled},{"animatorInitialized",animator.isInitialized},{"avatarInstanceId",nativeAvatar.GetInstanceID()},
                {"avatarName",nativeAvatar.name},{"avatarValid",nativeAvatar.isValid},{"runtimeControllerAssigned",animator.runtimeAnimatorController!=null},
                {"fireEvents",animator.fireEvents},{"applyRootMotion",animator.applyRootMotion},{"cullingMode",animator.cullingMode.ToString()},
                {"graphValid",graph.IsValid()},{"graphUpdateMode",graph.GetTimeUpdateMode().ToString()},{"playableCount",graph.GetPlayableCount()},
                {"outputCount",graph.GetOutputCount()},{"clipInstanceId",playable.GetAnimationClip().GetInstanceID()},{"footIK",playable.GetApplyFootIK()}};
        }
        public void Destroy(){if(graph.IsValid())graph.Destroy();}
    }
    static JArray SampleMatrix(Matrix4x4 matrix)
    {
        JArray rows=new JArray();for(int r=0;r<4;r++)
        {
            JArray row=new JArray();for(int c=0;c<4;c++)
            {float value=matrix[r,c];if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Nonfinite sampled matrix.");row.Add(value);}
            rows.Add(row);
        }
        return rows;
    }
    static JObject LocalSampleMatrices(List<SampleNode> nodes,string[] paths)
    {
        JObject output=new JObject();foreach(string path in paths)
        {Transform target=SampleTransform(nodes,path);output[path]=SampleMatrix(Matrix4x4.TRS(target.localPosition,target.localRotation,target.localScale));}
        return output;
    }
    static double SamplePoseDelta(JToken first,JToken next)
    {
        JArray a=first as JArray,b=next as JArray;
        if(a!=null && b!=null)
        {
            if(a.Count!=b.Count)throw new InvalidOperationException("Sample matrix dimensions changed.");
            double result=0;for(int i=0;i<a.Count;i++)result=Math.Max(result,SamplePoseDelta(a[i],b[i]));return result;
        }
        return Math.Abs((double)first-(double)next);
    }
    static JObject KrakenVariation(JArray frames)
    {
        JObject deltas=new JObject();double maximum=0,articulation=0;HashSet<double> sampleTimes=new HashSet<double>();
        foreach(JToken frame in frames)sampleTimes.Add((double)frame["time"]);
        if(frames.Count>0)
        {
            foreach(string field in new[]{"modernRootAnimated","oldRootAnimated"})
            {
                double change=0;for(int i=1;i<frames.Count;i++)change=Math.Max(change,SamplePoseDelta(frames[0][field],frames[i][field]));
                deltas[field]=change;maximum=Math.Max(maximum,change);
            }
            foreach(string field in new[]{"driverLocals","oldTargetLocals"})
            {
                JObject paths=new JObject();
                foreach(JProperty path in ((JObject)frames[0][field]).Properties())
                {
                    double change=0;for(int i=1;i<frames.Count;i++)change=Math.Max(change,SamplePoseDelta(path.Value,frames[i][field][path.Name]));
                    paths[path.Name]=change;maximum=Math.Max(maximum,change);articulation=Math.Max(articulation,change);
                }
                deltas[field]=paths;
            }
        }
        return new JObject{{"status",sampleTimes.Count<2?"insufficient_samples":maximum>1e-5?"pose_variation_observed":"constant_sampled_poses"},
            {"sampledFrames",frames.Count},{"distinctSampleTimes",sampleTimes.Count},{"comparison","maximum absolute matrix element delta from first sampled frame"},{"threshold",1e-5},
            {"maximumDelta",maximum},{"maximumLocalArticulationDelta",articulation},{"localArticulationObserved",articulation>1e-5},
            {"deltas",deltas},{"note","Successful API calls/cleanup alone are not motion evidence; no controller or visual acceptance inferred."}};
    }
    IEnumerator KrakenSampleFixture(string id,JObject command,bool adapterDiagnostic=false)
    {
        GameObject modernCopy=null,oldCopy=null;List<SampleNode> modern=new List<SampleNode>(),old=new List<SampleNode>();
        JArray frames=new JArray(),times=command["times"]as JArray;string clipName=Str(command,"clip"),error=null;AnimationClip clip=null;
        JObject identity=new JObject();int modernId=0,oldId=0;bool sampleLoopExited=false;
        string method=Str(command,"method")??"sample-animation";
        KrakenPlayableSampler modernPlayable=null,oldPlayable=null;JArray cleanupErrors=new JArray();
        bool graphsDisposed=true;
        KrakenOldAdapterPlan adapter=null;KrakenAdapterReadyPin adapterPin=null;JObject adapterChecks=null;bool sameReadyAfter=false;
        try
        {
            try
            {
                RequireReadyPreparation();CatalogNoLinks(root);
                if(adapterDiagnostic)
                {
                    if(method!="clip-playable" || clipName=="kraken_appear")throw new ArgumentException("Adapter fixture requires one of the four main clips and method clip-playable; appearance is unsupported.");
                    foreach(JProperty property in command.Properties())if(Array.IndexOf(new[]{"id","op","session","clip","times","method"},property.Name)<0)
                        throw new ArgumentException("Unsupported adapter input (including blends): "+property.Name);
                    adapterPin=new KrakenAdapterReadyPin(sessionId);
                }
                if(method!="sample-animation" && method!="clip-playable")throw new ArgumentException("method must be sample-animation or clip-playable.");
                if(Array.IndexOf(new[]{"krakenAttack","krakenIdle","krakenDamage","krakenDisappear","kraken_appear"},clipName)<0)
                    throw new ArgumentException("Exact one supported Kraken clip name required.");
                if(times==null || times.Count<1 || times.Count>32)throw new ArgumentException("Expected1..32 explicit sample times.");
                FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID("krakenHead");
                GameObject oldPrefab=Resources.Load("enkrakenhead",typeof(GameObject))as GameObject;
                if(row==null || row.m_ID!="krakenHead" || row.m_EnemyAsset==null || row.m_WeaponAsset==null || oldPrefab==null)
                    throw new InvalidOperationException("Exact native Kraken sources unavailable.");
                GameObject modernPrefab=row.m_EnemyAsset.gameObject;
                if(modernPrefab.scene.IsValid() || oldPrefab.scene.IsValid() || modernPrefab.transform.parent!=null || oldPrefab.transform.parent!=null)
                    throw new InvalidOperationException("Both sources must be unparented native prefab assets.");
                RuntimeAnimatorController controller=row.m_WeaponAsset.m_AnimationController;
                if(controller==null)throw new InvalidOperationException("Native Kraken weapon controller unavailable.");
                foreach(AnimationClip candidate in controller.animationClips)if(candidate!=null && candidate.name==clipName)
                {if(clip!=null && clip!=candidate)throw new InvalidOperationException("Ambiguous clip name.");clip=candidate;}
                if(clip==null)throw new InvalidOperationException("Clip not present in exact native weapon controller.");
                if(clip.length<=0 || clip.length>60 || float.IsNaN(clip.length))throw new InvalidOperationException("Clip length must be finite0..60 seconds.");
                foreach(JToken token in times)
                {
                    if(token.Type!=JTokenType.Integer && token.Type!=JTokenType.Float)throw new ArgumentException("Sample times must be numeric.");
                    double time=(double)token;if(double.IsNaN(time)||double.IsInfinity(time)||time<0||time>clip.length)
                        throw new ArgumentException("Sample times must be finite and within0..clip.length.");
                }
                modernCopy=new GameObject("FTK_MODEL_TEST_KRAKEN_MODERN_"+id);modernCopy.SetActive(false);
                oldCopy=new GameObject("FTK_MODEL_TEST_KRAKEN_OLD_"+id);oldCopy.SetActive(false);
                modernId=modernCopy.GetInstanceID();oldId=oldCopy.GetInstanceID();
                CopySampleTree(modernPrefab.transform,modernCopy.transform,"",modern);CopySampleTree(oldPrefab.transform,oldCopy.transform,"",old);
                SampleTransform(modern,"Root_M");SampleTransform(old,"Root_M");
                foreach(string path in KrakenMainPaths)SampleTransform(modern,path);foreach(string path in KrakenOldPaths)SampleTransform(old,path);
                AssertTransformOnly(modernCopy,modern.Count);AssertTransformOnly(oldCopy,old.Count);
                if(adapterDiagnostic){adapter=new KrakenOldAdapterPlan(modern,old);adapterChecks=adapter.NeutralAndNegativeChecks();}
                if(method=="clip-playable")
                {
                    Animator modernSource=modernPrefab.GetComponent<Animator>(),oldSource=oldPrefab.GetComponent<Animator>();
                    if(modernSource==null || oldSource==null)throw new InvalidOperationException("Native source Animators required for Avatar identity.");
                    modernPlayable=new KrakenPlayableSampler();oldPlayable=new KrakenPlayableSampler();
                    modernPlayable.Create(modernCopy,modernSource.avatar,clip);oldPlayable.Create(oldCopy,oldSource.avatar,clip);
                }
                // Activate only audited copied components; no native source objects or controllers are activated.
                AssertTransformOnly(modernCopy,modern.Count,modernPlayable==null?null:modernPlayable.animator);
                AssertTransformOnly(oldCopy,old.Count,oldPlayable==null?null:oldPlayable.animator);
                modernCopy.SetActive(true);oldCopy.SetActive(true);
                identity=new JObject{{"modernSource","native-enemy:krakenHead"},{"oldSource","Resources:enkrakenhead"},
                    {"modernPrefabInstanceId",modernPrefab.GetInstanceID()},{"oldPrefabInstanceId",oldPrefab.GetInstanceID()},
                    {"controllerName",controller.name},{"controllerInstanceId",controller.GetInstanceID()},{"clipName",clip.name},
                    {"clipInstanceId",clip.GetInstanceID()},{"clipLength",clip.length},{"modernTransformCount",modern.Count},{"oldTransformCount",old.Count}};
                if(method=="clip-playable")
                {
                    identity["modernInitialization"]=modernPlayable.InitializeAfterActivation();
                    identity["oldInitialization"]=oldPlayable.InitializeAfterActivation();
                    modernPlayable.Audit();oldPlayable.Audit();
                    AssertTransformOnly(modernCopy,modern.Count,modernPlayable.animator);AssertTransformOnly(oldCopy,old.Count,oldPlayable.animator);
                }
            }
            catch(Exception ex){error=ex.ToString();}
            if(error==null)foreach(JToken token in times)
            {
                try
                {
                    RequireReadyPreparation();if(adapterDiagnostic)adapterPin.Check(sessionId);float time=(float)token;
                    ResetSampleTree(modern);ResetSampleTree(old);
                    if(!modernCopy.activeInHierarchy || !oldCopy.activeInHierarchy)throw new InvalidOperationException("Fixture roots must remain active during sampling.");
                    if(method=="clip-playable"){modernPlayable.Sample(time);oldPlayable.Sample(time);}
                    else{clip.SampleAnimation(modernCopy,time);clip.SampleAnimation(oldCopy,time);}
                    AssertTransformOnly(modernCopy,modern.Count,modernPlayable==null?null:modernPlayable.animator);
                    AssertTransformOnly(oldCopy,old.Count,oldPlayable==null?null:oldPlayable.animator);
                    Transform modernRoot=SampleTransform(modern,"Root_M");
                    JObject pose=new JObject{{"clip",clipName},{"time",time},{"modernRootAnimated",SampleMatrix(adapterDiagnostic?KrakenLocalChain(modernRoot,modernCopy.transform):modernCopy.transform.worldToLocalMatrix*modernRoot.localToWorldMatrix)},
                        {"driverLocals",LocalSampleMatrices(modern,KrakenMainPaths)},
                        {"oldRootAnimated",SampleMatrix(adapterDiagnostic?KrakenLocalChain(SampleTransform(old,"Root_M"),oldCopy.transform):oldCopy.transform.worldToLocalMatrix*SampleTransform(old,"Root_M").localToWorldMatrix)},
                        {"oldTargetLocals",LocalSampleMatrices(old,KrakenOldPaths)},
                        {"fullRestResetBeforeSample",true},{"fixtureTransformOnly",method=="sample-animation"},
                        {"samplingMethod",method},{"modernPlayable",modernPlayable==null?null:modernPlayable.View()},{"oldPlayable",oldPlayable==null?null:oldPlayable.View()},
                        {"modernFixtureActive",modernCopy.activeInHierarchy},{"oldFixtureActive",oldCopy.activeInHierarchy}};
                    if(adapterDiagnostic)pose["oldAdapter"]=adapter.Sample();
                    frames.Add(pose);
                }
                catch(Exception ex){error=ex.ToString();}
                if(error!=null)break;
                yield return null;
            }
            sampleLoopExited=true;
        }
        finally
        {
            // Graphs must be gone before destroying their output targets, including partial setup failures.
            foreach(KrakenPlayableSampler sampler in new[]{modernPlayable,oldPlayable})if(sampler!=null)
            {
                try{sampler.Destroy();if(sampler.graph.IsValid())graphsDisposed=false;}
                catch(Exception ex){graphsDisposed=false;cleanupErrors.Add(ex.ToString());}
            }
            if(modernCopy!=null)UnityEngine.Object.Destroy(modernCopy);if(oldCopy!=null)UnityEngine.Object.Destroy(oldCopy);
            if(!sampleLoopExited)busy=false;
        }
        // Observe native deferred destruction; never destroy source assets or invoke cleanup hooks.
        yield return null;yield return null;
        if(adapterDiagnostic)try{adapterPin.Check(sessionId);sameReadyAfter=true;}catch(Exception ex){if(error==null)error=ex.ToString();}
        try{Finish(id,new JObject{{"ok",error==null && graphsDisposed && modernCopy==null && oldCopy==null},{"error",error},
            {"adapterRest",adapter==null?null:adapter.RestView()},{"adapterChecks",adapterChecks},
            {"pinnedReady",adapterPin==null?null:adapterPin.View()},{"sameReadyAfter",adapterDiagnostic?(JToken)new JValue(sameReadyAfter):new JValue((object)null)},
            {"provenance",adapterDiagnostic?"isolated_old_kraken_adapter_single_clip_diagnostic":method=="clip-playable"?"isolated_controller_free_manual_clip_playable_native_avatar":"isolated_transform_only_single_clip_sampling"},{"samplingMethod",method},{"coordinateConvention","independent-modern-root-and-local-drivers"},
            {"appearanceSamplesOnly",clipName=="kraken_appear"},{"identity",identity},{"frames",frames},{"poseVariation",KrakenVariation(frames)},
            {"cleanup",new JObject{{"graphsDisposedBeforeTargets",graphsDisposed},{"errors",cleanupErrors},{"modernFixtureInstanceId",modernId},{"oldFixtureInstanceId",oldId},{"modernUnityNull",modernCopy==null},{"oldUnityNull",oldCopy==null}}},
            {"limitations",adapterDiagnostic?"Owned Transform/Animator adapter mechanics only. Four independent main clips, no controller or transitions. No skinned renderer, palette mutation, appearance application, native events, live avatar adaptation, or visual acceptance. Appearance and blended inputs rejected.":"SampleAnimation native event internals opaque. Clip-playable mode uses one controller-free Animator per copy, native Avatar, fireEvents=false, footIK=false, manual zero-delta evaluation; no CEL or gameplay behaviours. Native Avatar may not bind modern paths. No Animator reference comparison, controller transitions, crossfade, root extraction from live old avatar, or adapter application. Appearance remains separate baseline data, not adapted main-state evidence."}});}
        finally{busy=false;}
    }
}
