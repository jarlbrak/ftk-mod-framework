using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    const string KrakenControllerAssets="e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117";
    const string KrakenControllerGameAssembly="94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8";
    static string KrakenScenarioTrigger(string scenario)
    {
        switch(scenario)
        {
            case "intro":return "Intro";
            case "appear":return "Appear";
            case "damaged":return "Damaged";
            case "damaged-heavy":return "DamagedHeavy";
            case "death":return "Death";
            case "death-light":return "DeathLight";
            default:throw new ArgumentException("Exact fixed scenario required: appear, damaged, damaged-heavy, death, death-light.");
        }
    }
    static string KrakenScenarioState(string trigger)
    {
        switch(trigger)
        {
            case "Intro":return "INTRO";
            case "Appear":return "OverworldAppear";
            case "Damaged":return "DAMAGED";
            case "DamagedHeavy":return "DAMAGEDHEAVY";
            case "Death":return "DEATH";
            case "DeathLight":return "DEATHLIGHT";
            default:throw new InvalidOperationException("Unsupported controller trigger.");
        }
    }
    static JObject KrakenControllerState(AnimatorStateInfo state)
    {
        if(float.IsNaN(state.normalizedTime) || float.IsInfinity(state.normalizedTime))throw new InvalidOperationException("Nonfinite controller state clock.");
        return new JObject{{"fullPathHash",state.fullPathHash},{"shortNameHash",state.shortNameHash},
            {"normalizedTime",state.normalizedTime},{"length",state.length},{"loop",state.loop},{"speed",state.speed},{"speedMultiplier",state.speedMultiplier}};
    }
    static JArray KrakenControllerClips(AnimatorClipInfo[] clips)
    {
        JArray result=new JArray();foreach(AnimatorClipInfo clip in clips)
        {
            if(clip.clip==null || float.IsNaN(clip.weight) || float.IsInfinity(clip.weight))throw new InvalidOperationException("Invalid controller clip info.");
            result.Add(new JObject{{"name",clip.clip.name},{"instanceId",clip.clip.GetInstanceID()},{"length",clip.clip.length},{"weight",clip.weight}});
        }
        return result;
    }
    static JObject KrakenControllerPose(List<SampleNode> nodes,string[] paths)
    {
        Transform top=SampleTransform(nodes,"");JObject locals=new JObject(),models=new JObject();
        List<string> all=new List<string>();all.Add("Root_M");all.AddRange(paths);
        foreach(string path in all)
        {
            Transform target=SampleTransform(nodes,path);
            locals[path]=new JObject{{"position",Vec(target.localPosition)},{"rotation",Quat(target.localRotation)},{"scale",Vec(target.localScale)},
                {"matrix",SampleMatrix(Matrix4x4.TRS(target.localPosition,target.localRotation,target.localScale))},{"instanceId",target.GetInstanceID()}};
            models[path]=SampleMatrix(KrakenLocalChain(target,top));
        }
        return new JObject{{"locals",locals},{"prefabRootModels",models}};
    }
    // Observation only: missing native nodes stay missing; never synthesize a driver rig.
    static JObject KrakenExistingDrivers(List<SampleNode> nodes)
    {
        JArray existing=new JArray(),missing=new JArray();List<string> present=new List<string>();
        foreach(SampleNode node in nodes)existing.Add(node.path);
        foreach(string path in KrakenMainPaths)
        {
            bool found=false;foreach(SampleNode node in nodes)if(node.path==path){found=true;break;}
            if(found)present.Add(path);else missing.Add(path);
        }
        return new JObject{{"scope","owned_native_controller_input_observation_no_adapter"},
            {"status",missing.Count==0?"available":"unavailable_missing_native_paths"},
            {"existingPaths",existing},{"missingPaths",missing},{"requiredPaths",new JArray(KrakenMainPaths)},
            {"pose",KrakenControllerPose(nodes,present.ToArray())}};
    }
    sealed class KrakenControllerSurface
    {
        public GameObject root;public Animator animator;public PlayableGraph graph;public AnimatorControllerPlayable controller;
        public readonly List<SampleNode> nodes=new List<SampleNode>();
        public int rootId;public bool graphDisposed=true,observeExistingDrivers;public JObject rest,initialization,driverRest;
        Avatar avatar;int targetHash;string[] posePaths;bool triggerSent;
        public void Create(GameObject source,RuntimeAnimatorController native,string name,string target,string[] paths)
        {
            targetHash=Animator.StringToHash("Base Layer."+target);posePaths=paths;
            Animator sourceAnimator=source.GetComponent<Animator>();
            if(sourceAnimator==null || sourceAnimator.avatar==null || !sourceAnimator.avatar.isValid || sourceAnimator.runtimeAnimatorController!=native)
                throw new InvalidOperationException("Exact native Animator/Avatar/controller identity required.");
            avatar=sourceAnimator.avatar;root=new GameObject(name);root.SetActive(false);rootId=root.GetInstanceID();
            CopySampleTree(source.transform,root.transform,"",nodes);AssertTransformOnly(root,nodes.Count);
            rest=KrakenControllerPose(nodes,paths);
            if(observeExistingDrivers)driverRest=KrakenExistingDrivers(nodes);
            animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=null;animator.avatar=avatar;
            animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create();graphDisposed=false;graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            controller=AnimatorControllerPlayable.Create(graph,native);
            AnimationPlayableOutput output=AnimationPlayableOutput.Create(graph,"kraken-native-controller-fixture",animator);
            output.SetSourcePlayable(controller);output.SetWeight(1);
            if(controller.GetParameterCount()!=21)throw new InvalidOperationException("Expected exact21 native controller trigger parameters.");
            HashSet<string> parameters=new HashSet<string>();
            for(int i=0;i<controller.GetParameterCount();i++)
            {
                AnimatorControllerParameter parameter=controller.GetParameter(i);
                if(parameter.type!=AnimatorControllerParameterType.Trigger || parameter.defaultBool || !parameters.Add(parameter.name))
                    throw new InvalidOperationException("Native parameter shape/default changed.");
            }
            foreach(string trigger in new[]{"Appear","Damaged","DamagedHeavy","Death","DeathLight"})
                if(!parameters.Contains(trigger))throw new InvalidOperationException("Required whitelisted trigger missing.");
            if(target=="INTRO" && !parameters.Contains("Intro"))throw new InvalidOperationException("Pinned Intro trigger unavailable.");
            AssertTransformOnly(root,nodes.Count,animator);root.SetActive(true);
            bool before=animator.isInitialized;animator.Rebind();graph.Play();graph.Evaluate(0);
            initialization=new JObject{{"initializedBefore",before},{"initializedAfter",animator.isInitialized},{"sequence","activate, Rebind, graph.Play, Evaluate(0)"}};
            Audit();if(controller.IsInTransition(0) || controller.GetCurrentAnimatorStateInfo(0).fullPathHash!=Animator.StringToHash("Base Layer.IDLE"))
                throw new InvalidOperationException("Native default must be stable IDLE before any trigger.");
        }
        public void Audit()
        {
            AssertTransformOnly(root,nodes.Count,animator);
            if(root==null || !root.activeInHierarchy || animator==null || !animator.enabled || !animator.isInitialized
                || animator.avatar!=avatar || animator.runtimeAnimatorController!=null || animator.fireEvents || animator.applyRootMotion
                || animator.cullingMode!=AnimatorCullingMode.AlwaysAnimate || !graph.IsValid() || !controller.IsValid()
                || graph.GetTimeUpdateMode()!=DirectorUpdateMode.Manual || graph.GetOutputCount()!=1 || controller.GetLayerCount()!=1)
                throw new InvalidOperationException("Owned controller surface invariants changed.");
            int idleHash=Animator.StringToHash("Base Layer.IDLE");
            int current=controller.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if(current!=idleHash && current!=targetHash)throw new InvalidOperationException("Unexpected native controller current state; no attack states permitted.");
            if(controller.IsInTransition(0))
            {
                int next=controller.GetNextAnimatorStateInfo(0).fullPathHash;
                if(next!=idleHash && next!=targetHash)throw new InvalidOperationException("Unexpected native controller next state.");
            }
        }
        public void Trigger(string trigger)
        {Audit();if(triggerSent)throw new InvalidOperationException("Only one scenario trigger is permitted.");triggerSent=true;controller.SetTrigger(trigger);}
        public void Step(float seconds){Audit();graph.Evaluate(seconds);Audit();}
        public JObject View()
        {
            Audit();bool transition=controller.IsInTransition(0);JObject transitionInfo=null;
            if(transition)
            {
                AnimatorTransitionInfo info=controller.GetAnimatorTransitionInfo(0);
                transitionInfo=new JObject{{"fullPathHash",info.fullPathHash},{"nameHash",info.nameHash},{"normalizedTime",info.normalizedTime},
                    {"duration",info.duration},{"durationUnit",info.durationUnit.ToString()},{"anyState",info.anyState}};
            }
            JObject result=new JObject{{"rootInstanceId",rootId},{"animatorInstanceId",animator.GetInstanceID()},{"animatorInitialized",animator.isInitialized},
                {"avatarName",avatar.name},{"avatarInstanceId",avatar.GetInstanceID()},{"runtimeControllerAssigned",false},{"fireEvents",false},{"applyRootMotion",false},
                {"graphMode",graph.GetTimeUpdateMode().ToString()},{"graphPlayableCount",graph.GetPlayableCount()},{"graphOutputCount",graph.GetOutputCount()},
                {"layerName",controller.GetLayerName(0)},{"layerWeight",controller.GetLayerWeight(0)},{"current",KrakenControllerState(controller.GetCurrentAnimatorStateInfo(0))},
                {"next",transition?KrakenControllerState(controller.GetNextAnimatorStateInfo(0)):null},{"inTransition",transition},{"transition",transitionInfo},
                {"currentClips",KrakenControllerClips(controller.GetCurrentAnimatorClipInfo(0))},{"nextClips",KrakenControllerClips(controller.GetNextAnimatorClipInfo(0))},
                {"pose",KrakenControllerPose(nodes,posePaths)}};
            if(observeExistingDrivers)result["existingDrivers"]=KrakenExistingDrivers(nodes);return result;
        }
        public void Dispose()
        {
            if(graph.IsValid())graph.Destroy();graphDisposed=!graph.IsValid();
            if(root!=null)UnityEngine.Object.Destroy(root);
        }
    }
    IEnumerator KrakenControllerFixture(string id,JObject command)
    {
        const float stepSeconds=1f/60f;const int triggerAfterStep=15;int steps=Str(command,"scenario")=="intro"?360:240;
        KrakenControllerSurface modern=new KrakenControllerSurface(),old=new KrakenControllerSurface();
        KrakenModernInputMixer modernMixer=null;KrakenEndpointFixturePlan endpoint=null;KrakenOwnedSkinProbe skin=null;KrakenSkinArm skinArm=null;
        bool composed=Str(command,"endpointPolicy")==KrakenEndpointFixturePlan.ComposedPolicy;
        KrakenAdapterReadyPin pin=null;string error=null,scenario=Str(command,"scenario"),trigger=null;JObject identity=null;
        JArray frames=new JArray(),engineErrors=new JArray(),cleanupErrors=new JArray();bool loopExited=false,sameReady=false;
        Application.LogCallback handler=delegate(string condition,string stack,LogType type)
        {
            if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && engineErrors.Count<32)
                engineErrors.Add(new JObject{{"type",type.ToString()},{"condition",condition},{"stack",stack}});
        };
        Application.logMessageReceived+=handler;
        try
        {
            try
            {
                if(command["modernInputMixer"]!=null)
                {
                    if(command["modernInputMixer"].Type!=JTokenType.String || ((string)command["modernInputMixer"]!=KrakenModernInputMixer.Mode && (string)command["modernInputMixer"]!=KrakenModernInputMixer.FixedMode && (string)command["modernInputMixer"]!=KrakenModernInputMixer.BankMode) || command["endpointPolicy"]!=null || command["observeExistingDrivers"]!=null || krakenSkinArm!=null)
                        throw new ArgumentException("Exact modern mixer mode must be separate from endpoint, skin arm and driver observation modes.");
                    modernMixer=new KrakenModernInputMixer();
                }
                if(composed){if(command["observeExistingDrivers"]!=null)throw new ArgumentException("Composed policy excludes independent driver observation mode.");modernMixer=new KrakenModernInputMixer();}
                skinArm=ConsumeKrakenSkin(command);
                foreach(JProperty property in command.Properties())if(Array.IndexOf(new[]{"id","session","op","scenario","endpointPolicy","observeExistingDrivers","modernInputMixer"},property.Name)<0)
                    throw new ArgumentException("Unsupported controller fixture input: "+property.Name);
                if(command["observeExistingDrivers"]!=null)
                {
                    if(command["observeExistingDrivers"].Type!=JTokenType.Boolean)throw new ArgumentException("observeExistingDrivers must be boolean.");
                    old.observeExistingDrivers=(bool)command["observeExistingDrivers"];
                }
                if(command["endpointPolicy"]!=null)
                {
                    if(command["endpointPolicy"].Type!=JTokenType.String || ((string)command["endpointPolicy"]!="main-appearance-local-v1" && !composed))
                        throw new ArgumentException("Exact legacy or fixed-main-appearance-local-v1 endpoint policy required.");
                    endpoint=new KrakenEndpointFixturePlan();
                }
                if(scenario=="intro" && Str(command,"modernInputMixer")!=KrakenModernInputMixer.BankMode)throw new ArgumentException("Intro requires explicit two-bank-five input-only mode.");
                trigger=KrakenScenarioTrigger(scenario);pin=new KrakenAdapterReadyPin(sessionId);CatalogNoLinks(root);
                string assets=Path.Combine(root,"FTK.app/Contents/Resources/Data/resources.assets");
                if(!File.Exists(assets))assets=Path.Combine(root,"FTK_Data/resources.assets");
                if(!File.Exists(assets) || CatalogHash(assets)!=KrakenControllerAssets)
                    throw new InvalidOperationException("Unsupported resources.assets: native graph whitelist proof must be renewed.");
                JObject assembly=ScaleIdentity(typeof(CharacterEventListener).Assembly);
                if(!Path.GetFullPath((string)assembly["location"]).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)
                    || (string)assembly["assemblyFileSha256"]!=KrakenControllerGameAssembly)
                    throw new InvalidOperationException("Unsupported game assembly: native behaviour lifecycle audit must be renewed.");
                FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID("krakenHead");
                GameObject oldSource=Resources.Load("enkrakenhead",typeof(GameObject))as GameObject;
                if(row==null || row.m_ID!="krakenHead" || row.m_EnemyAsset==null || row.m_WeaponAsset==null || oldSource==null)
                    throw new InvalidOperationException("Exact native Kraken source rows unavailable.");
                GameObject modernSource=row.m_EnemyAsset.gameObject;RuntimeAnimatorController native=row.m_WeaponAsset.m_AnimationController;
                if(native==null || native is AnimatorOverrideController || native.name!="krakenHeadController" || modernSource.scene.IsValid() || oldSource.scene.IsValid()
                    || modernSource.transform.parent!=null || oldSource.transform.parent!=null)throw new InvalidOperationException("Exact native prefab assets/controller required.");
                identity=new JObject{{"resourcesSha256",KrakenControllerAssets},{"gameAssembly",assembly},{"controllerName",native.name},
                    {"controllerInstanceId",native.GetInstanceID()},{"sourceControllerId",5973},{"modernSourceInstanceId",modernSource.GetInstanceID()},{"oldSourceInstanceId",oldSource.GetInstanceID()},
                    {"trigger",trigger},{"targetState",KrakenScenarioState(trigger)},{"idleStateHash",Animator.StringToHash("Base Layer.IDLE")},
                    {"targetStateHash",Animator.StringToHash("Base Layer."+KrakenScenarioState(trigger))},{"sourceProof",scenario=="intro"?"Pinned controller5973 INTRO1746749458 has no behaviour range; one Intro trigger, only IDLE/INTRO states; no custom creation or machine callbacks":"21 default-false triggers; five one-trigger paths exclude all three attack SMB ranges; no custom creation or machine callbacks"}};
                modern.Create(modernSource,native,"FTK_KRAKEN_CONTROLLER_MODERN_"+id,KrakenScenarioState(trigger),KrakenMainPaths);
                old.Create(oldSource,native,"FTK_KRAKEN_CONTROLLER_OLD_"+id,KrakenScenarioState(trigger),KrakenOldPaths);
                if(modernMixer!=null)modernMixer.Create(modernSource,native,id,composed?KrakenModernInputMixer.FixedMode:(string)command["modernInputMixer"]);
                if(old.observeExistingDrivers)identity["oldExistingDriverRest"]=old.driverRest;
                if(endpoint!=null)endpoint.Create(modernSource,oldSource,native,scenario,id,composed);
                if(skinArm!=null){skin=new KrakenOwnedSkinProbe();skin.Create(this,skinArm,endpoint,oldSource,id);}
                if(engineErrors.Count>0)throw new InvalidOperationException("Engine error during owned graph creation/initialization.");
            }
            catch(Exception ex){error=ex.ToString();}
            if(error==null)for(int step=0;step<=steps;step++)
            {
                try
                {
                    pin.Check(sessionId);
                    if(step>0)
                    {
                        if(step==triggerAfterStep+1){modern.Trigger(trigger);old.Trigger(trigger);}
                        modern.Step(stepSeconds);old.Step(stepSeconds);
                    }
                    if(engineErrors.Count>0)throw new InvalidOperationException("Engine error during isolated controller graph evaluation.");
                    JObject frame=new JObject{{"step",step},{"graphElapsedSeconds",step*(double)stepSeconds},{"manualDeltaSeconds",step==0?0:stepSeconds},
                        {"unityFrame",Time.frameCount},{"unityTime",Time.time},{"unityUnscaledTime",Time.unscaledTime},{"triggerIssued",step>triggerAfterStep},
                        {"modern",modern.View()},{"old",old.View()}};
                    if(modernMixer!=null)frame["modernInputMixer"]=modernMixer.Step(modern,old,(JObject)frame["modern"]);
                    if(endpoint!=null)frame["endpointPolicy"]=endpoint.Step(modern,old,composed?(JObject)frame["modernInputMixer"].DeepClone():null);
                    if(skin!=null)
                    {
                        frame["originalSkinProbe"]=skin.Step(step,(JObject)frame["endpointPolicy"]);
                        if(!JToken.DeepEquals(frame["modern"],modern.View()) || !JToken.DeepEquals(frame["old"],old.View()))
                            throw new InvalidOperationException("Original probe changed native controller observation.");
                        pin.Check(sessionId);if(engineErrors.Count>0)throw new InvalidOperationException("Engine error during original skin probe.");
                    }
                    frames.Add(frame);
                }
                catch(Exception ex){error=ex.ToString();}
                if(error!=null)break;yield return null;
            }
            loopExited=true;
        }
        finally
        {
            if(modernMixer!=null)try{modernMixer.Dispose();}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            if(skin!=null)try{skin.Dispose();}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            if(endpoint!=null)try{endpoint.Dispose();}catch(Exception ex){cleanupErrors.Add(ex.ToString());}
            foreach(KrakenControllerSurface surface in new[]{modern,old})
                try{surface.Dispose();}catch(Exception ex){cleanupErrors.Add(ex.ToString());if(surface.root!=null)UnityEngine.Object.Destroy(surface.root);}
            if(!loopExited){Application.logMessageReceived-=handler;busy=false;}
        }
        try
        {
            // Keep the observer attached until native deferred target destruction has completed.
            yield return null;yield return null;
            try{if(pin!=null){pin.Check(sessionId);sameReady=true;}}catch(Exception ex){if(error==null)error=ex.ToString();}
            bool cleanup=modern.graphDisposed && old.graphDisposed && modern.root==null && old.root==null && cleanupErrors.Count==0 && (endpoint==null || endpoint.Clean()) && (skin==null || skin.Clean()) && (modernMixer==null || modernMixer.Clean());
            JObject result=new JObject{{"ok",error==null && engineErrors.Count==0 && sameReady && cleanup && frames.Count==steps+1 && (skin==null || skin.Complete())},{"error",error},
                {"provenance",composed?"owned_fixed_sampler_endpoint_composition_diagnostic":endpoint==null?"owned_native_controller_graph_observation_no_adapter":"owned_controller_endpoint_policy_diagnostic"},{"endpointPolicy",endpoint==null?null:command["endpointPolicy"]},{"endpointRests",endpoint==null?null:endpoint.rests},{"pureEndpointChecks",endpoint==null?0:endpoint.pureChecks},{"scenario",scenario},{"identity",identity},
                {"pinnedReady",pin==null?null:pin.View()},{"sameReadyAfter",sameReady},{"stepSeconds",stepSeconds},{"triggerAfterStep",triggerAfterStep},
                {"expectedFrames",steps+1},{"modernRest",modern.rest},{"oldRest",old.rest},{"modernInitialization",modern.initialization},{"oldInitialization",old.initialization},
                {"frames",frames},{"originalSkinProbe",skin==null?null:skin.View()},{"engineErrors",engineErrors},{"cleanup",new JObject{{"graphsDisposedBeforeTargets",modern.graphDisposed&&old.graphDisposed},
                    {"endpointSurfacesClean",endpoint==null || endpoint.Clean()},{"modernUnityNull",modern.root==null},{"oldUnityNull",old.root==null},{"errors",cleanupErrors}}},
                {"limitations",skin!=null?"Owned endpoint numerical policy plus separately labeled original five-marker skin probe. Numerical fields retain their prior meaning. Original probe BakeMesh and image evidence require independent numerical and visual review; no native mesh or gameplay support claim.":endpoint!=null?"Isolated endpoint policy diagnostic: two owned native controller surfaces, four independent clip sampling surfaces and one separate Transform-only output tree. Policy writes only to the output tree; native graph inputs remain unchanged. Observed pure-state samples are checked against both native graphs. Mixed-clock endpoints and their raw-weight local blend are an authored policy, not native mixed-pose equivalence. No live CEL, gameplay callbacks, attack states, mesh deformation or visual acceptance. Native jaw LOCAL is preserved; jaw model equivalence and visual continuity are not established. Successful completion does not establish scenario coverage or cross-run repeatability. Native engine ScriptableObject creation remains an opaque engine boundary.":"Actual controller graph observation on two owned Transform/Animator surfaces only. No adapter writes, live CEL, gameplay callbacks, attack states, mesh deformation or visual acceptance. Current/next clip weights are raw API values, not inferred combined blend weights. Successful completion is not transition coverage or a repeatability comparison. Native engine ScriptableObject creation is opaque; game-defined creation/machine callbacks were independently excluded by pinned-source audit."}};
            if(composed){result["compositionInputProvenance"]="internal_fixed_four_sampler_same_native_frame_not_separately_submitted_command";result["limitations"]="Owned composed-output experiment: fixed-four controller-free sampler supplies frozen main-only input; appearance roles retain independent full-strength endpoints with one raw-alpha local blend. Native controller/root/jaw inputs remain read-only. No attack clips, live synchronization, avatar ownership or production adapter claim. Numerical mismatch and optional original skin evidence require independent verification.";}
            if(modernMixer!=null){result["modernInputMixer"]=modernMixer.View();((JObject)result["cleanup"])["modernInputMixerClean"]=modernMixer.Clean();}
            Finish(id,result);
        }
        finally{Application.logMessageReceived-=handler;busy=false;}
    }
}
