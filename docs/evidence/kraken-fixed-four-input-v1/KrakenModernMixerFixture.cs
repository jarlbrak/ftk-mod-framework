using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Experimental input convention. This sampler owns no controller or gameplay component.
    sealed class KrakenModernInputMixer
    {
        const float Tolerance=1e-5f;
        public const string Mode="two-clip-raw-v1",FixedMode="fixed-four-raw-v1";
        static readonly string[] FixedClips={"krakenIdle","krakenDamage","krakenDisappear","kraken_appear"};
        string selectedMode=Mode;bool Fixed{get{return selectedMode==FixedMode;}}
        GameObject root;Animator animator;Avatar avatar;PlayableGraph graph;AnimationMixerPlayable mixer;
        AnimationClipPlayable[] inputs=new AnimationClipPlayable[2];
        readonly List<SampleNode> nodes=new List<SampleNode>(),sourceNodes=new List<SampleNode>();
        readonly Dictionary<string,AnimationClip> clips=new Dictionary<string,AnimationClip>();
        int rootId,animatorId,resetCount,eligibleFrames,excludedFrames,failedMainFrames,failedRepeatFrames;float maximumMainError,maximumRepeatError;bool graphDisposed=true;
        string immutableHash,sourceHash;JObject identity;
        sealed class Input
        {public AnimationClip clip;public string role;public float weight,seconds;public JObject view;}
        static void ReadTree(Transform node,string path,List<SampleNode> result)
        {
            if(result.Count>=256)throw new InvalidOperationException("Mixer source hierarchy exceeds256 transforms.");
            result.Add(new SampleNode{target=node,path=path});
            foreach(Transform child in node)ReadTree(child,path==""?child.name:path+"/"+child.name,result);
        }
        static string HashTree(List<SampleNode> values)
        {
            JArray result=new JArray();foreach(SampleNode node in values)
            {if(node.target==null)throw new InvalidOperationException("Mixer source/input transform disappeared.");result.Add(new JObject{{"path",node.path},{"id",node.target.GetInstanceID()},{"trs",SampleMatrix(Matrix4x4.TRS(node.target.localPosition,node.target.localRotation,node.target.localScale))}});}
            using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(result.ToString()))).Replace("-","").ToLowerInvariant();
        }
        static float Finite(JToken token)
        {if(token==null || (token.Type!=JTokenType.Integer && token.Type!=JTokenType.Float))throw new InvalidOperationException("Mixer scalar unavailable.");float v=(float)token;if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Nonfinite mixer scalar.");return v;}
        static string ExpectedClip(int hash)
        {
            if(hash==Animator.StringToHash("Base Layer.IDLE"))return "krakenIdle";
            if(hash==Animator.StringToHash("Base Layer.OverworldAppear"))return "kraken_appear";
            if(hash==Animator.StringToHash("Base Layer.DAMAGED") || hash==Animator.StringToHash("Base Layer.DAMAGEDHEAVY"))return "krakenDamage";
            if(hash==Animator.StringToHash("Base Layer.DEATH") || hash==Animator.StringToHash("Base Layer.DEATHLIGHT"))return "krakenDisappear";
            throw new InvalidOperationException("Unsupported native mixer input state.");
        }
        Input CaptureInput(JObject native,string role)
        {
            JObject state=native[role]as JObject;JArray observed=native[role+"Clips"]as JArray;
            if(state==null || observed==null || observed.Count!=1)throw new InvalidOperationException("Exactly one clip per active native state required.");
            JObject clipView=observed[0]as JObject;string name=ExpectedClip((int)state["fullPathHash"]);AnimationClip clip;
            if(clipView==null || (string)clipView["name"]!=name || !clips.TryGetValue(name,out clip) || (int)clipView["instanceId"]!=clip.GetInstanceID())throw new InvalidOperationException("Native observed clip identity changed.");
            float phase=Finite(state["normalizedTime"]),length=Finite(clipView["length"]),weight=Finite(clipView["weight"]);
            bool loop=(bool)state["loop"];
            if(Finite(state["speed"])!=1 || Finite(state["speedMultiplier"])!=1 || loop!=clip.isLooping || length!=clip.length || length<=0 || weight<0 || weight>1)
                throw new InvalidOperationException("Unsupported native clip clock/weight convention.");
            double mapped=loop?phase-Math.Floor(phase):Math.Max(0,Math.Min(1,phase));float seconds=(float)(mapped*length);
            return new Input{clip=clip,role=role,weight=weight,seconds=seconds,view=new JObject{{"role",role},{"clip",name},{"clipInstanceId",clip.GetInstanceID()},
                {"state",state.DeepClone()},{"rawWeight",weight},{"clipLength",length},{"loop",loop},{"mappedPhase",mapped},{"sampleSeconds",seconds},{"requestedUnwrappedSeconds",(double)phase*length}}};
        }
        void Replace(int slot,AnimationClip clip)
        {
            if(inputs[slot].IsValid() && inputs[slot].GetAnimationClip()==clip)return;
            if(inputs[slot].IsValid()){graph.Disconnect(mixer,slot);graph.DestroyPlayable(inputs[slot]);}
            inputs[slot]=AnimationClipPlayable.Create(graph,clip);inputs[slot].SetSpeed(0);inputs[slot].SetApplyFootIK(false);
            if(!graph.Connect(inputs[slot],0,mixer,slot))throw new InvalidOperationException("Owned mixer input connection failed.");
        }
        public void Create(GameObject source,RuntimeAnimatorController native,string id,string requestedMode)
        {
            if(requestedMode!=Mode && requestedMode!=FixedMode)throw new ArgumentException("Exact mixer mode required.");selectedMode=requestedMode;inputs=new AnimationClipPlayable[Fixed?4:2];
            ReadTree(source.transform,"",sourceNodes);sourceHash=HashTree(sourceNodes);
            foreach(AnimationClip clip in native.animationClips)
                if(clip!=null && (clip.name=="krakenIdle" || clip.name=="krakenDamage" || clip.name=="krakenDisappear" || clip.name=="kraken_appear"))
                {if(clips.ContainsKey(clip.name) && clips[clip.name]!=clip)throw new InvalidOperationException("Ambiguous native mixer clip.");clips[clip.name]=clip;}
            if(clips.Count!=4)throw new InvalidOperationException("Four exact native mixer clip identities required.");
            root=new GameObject("FTK_KRAKEN_MODERN_MIXER_"+id);root.SetActive(false);rootId=root.GetInstanceID();CopySampleTree(source.transform,root.transform,"",nodes);AssertTransformOnly(root,nodes.Count);
            immutableHash=HashTree(nodes);JObject rest=KrakenControllerPose(nodes,KrakenMainPaths);
            avatar=source.GetComponent<Animator>().avatar;if(avatar==null || !avatar.isValid || avatar.isHuman)throw new InvalidOperationException("Native modern Avatar missing.");
            animator=root.AddComponent<Animator>();animatorId=animator.GetInstanceID();animator.avatar=avatar;animator.runtimeAnimatorController=null;
            animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create();graphDisposed=false;graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);mixer=AnimationMixerPlayable.Create(graph,inputs.Length,false);
            if(Fixed){for(int i=0;i<inputs.Length;i++){Replace(i,clips[FixedClips[i]]);mixer.SetInputWeight(i,i==0?1:0);inputs[i].SetTime(0);}}
            else{Replace(0,clips["krakenIdle"]);Replace(1,clips["krakenIdle"]);mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,0);}
            AnimationPlayableOutput output=AnimationPlayableOutput.Create(graph,"kraken-modern-input-mixer",animator);output.SetSourcePlayable(mixer);output.SetWeight(1);
            root.SetActive(true);animator.Rebind();graph.Play();for(int i=0;i<inputs.Length;i++)inputs[i].SetTime(0);graph.Evaluate(0);Audit();
            identity=new JObject{{"mode",selectedMode},{"rootInstanceId",rootId},{"animatorInstanceId",animatorId},{"avatarInstanceId",avatar.GetInstanceID()},{"avatarIsHuman",avatar.isHuman},
                {"rest",rest},{"immutableRestLocalTrsSha256",immutableHash},{"sourcePrefabLocalTrsSha256",sourceHash},{"transformCount",nodes.Count},
                {"initializedAfterRebindAndEvaluate",animator.isInitialized},{"normalizeWeights",false},{"inputConvention","Reset every owned local TRS to immutable modern prefab rest before each zero-delta manual evaluation; experimental, not assumed native WriteDefaults equivalence."}};
            if(Fixed){identity["fixedClipOrder"]=new JArray(FixedClips);identity["fixedClipInstanceIds"]=ClipIds();identity["bindingInitialization"]="All four clip nodes connected before Animator.Rebind; fixed topology throughout run.";}
        }
        void Audit()
        {
            AssertTransformOnly(root,nodes.Count,animator);
            if(root==null || !root.activeInHierarchy || animator==null || !animator.enabled || !animator.isInitialized || animator.avatar!=avatar || animator.runtimeAnimatorController!=null || animator.fireEvents || animator.applyRootMotion || animator.cullingMode!=AnimatorCullingMode.AlwaysAnimate
                || !graph.IsValid() || graph.GetTimeUpdateMode()!=DirectorUpdateMode.Manual || graph.GetPlayableCount()!=inputs.Length+1 || graph.GetOutputCount()!=1 || !mixer.IsValid() || mixer.GetInputCount()!=inputs.Length)
                throw new InvalidOperationException("Controller-free mixer ownership/configuration changed.");
            for(int i=0;i<inputs.Length;i++)if(!inputs[i].IsValid() || inputs[i].GetApplyFootIK() || inputs[i].GetSpeed()!=0 || !mixer.GetInput(i).GetHandle().Equals(inputs[i].GetHandle()) || (Fixed && inputs[i].GetAnimationClip()!=clips[FixedClips[i]]))throw new InvalidOperationException("Mixer input graph changed.");
        }
        JObject Evaluate(Input first,Input next)
        {
            ResetSampleTree(nodes);resetCount++;if(HashTree(nodes)!=immutableHash)throw new InvalidOperationException("Full sampler rest reset failed.");
            if(Fixed)
            {
                if(next!=null && next.clip==first.clip)throw new InvalidOperationException("Fixed-four mode cannot represent two active clocks for the same native clip.");
                for(int i=0;i<inputs.Length;i++){inputs[i].SetTime(0);mixer.SetInputWeight(i,0);}
                int slot=Array.IndexOf(FixedClips,first.clip.name);inputs[slot].SetTime(first.seconds);mixer.SetInputWeight(slot,first.weight);
                if(next!=null){slot=Array.IndexOf(FixedClips,next.clip.name);inputs[slot].SetTime(next.seconds);mixer.SetInputWeight(slot,next.weight);}
            }
            else
            {
                Replace(0,first.clip);Replace(1,next==null?clips["krakenIdle"]:next.clip);
                inputs[0].SetTime(first.seconds);inputs[1].SetTime(next==null?0:next.seconds);
                mixer.SetInputWeight(0,first.weight);mixer.SetInputWeight(1,next==null?0:next.weight);
            }
            Audit();graph.Evaluate(0);Audit();
            return KrakenControllerPose(nodes,KrakenMainPaths);
        }
        JArray ClipIds(){JArray result=new JArray();foreach(AnimationClipPlayable input in inputs)result.Add(input.GetAnimationClip().GetInstanceID());return result;}
        JArray InputWeights(){JArray result=new JArray();for(int i=0;i<inputs.Length;i++)result.Add(mixer.GetInputWeight(i));return result;}
        JArray InputTimes(){JArray result=new JArray();foreach(AnimationClipPlayable input in inputs)result.Add(input.GetTime());return result;}
        static float Compare(JObject actual,JObject expected)
        {
            float max=0;foreach(JProperty path in ((JObject)expected["locals"]).Properties())
                foreach(string kind in new[]{"locals","prefabRootModels"})
                {JToken a=actual[kind][path.Name],b=expected[kind][path.Name];if(kind=="locals"){a=a["matrix"];b=b["matrix"];}max=Math.Max(max,(float)SamplePoseDelta(a,b));}
            return max;
        }
        public JObject Step(KrakenControllerSurface modern,KrakenControllerSurface old,JObject frozenNative)
        {
            Audit();string beforeModern=HashTree(modern.nodes),beforeOld=HashTree(old.nodes);
            Input first=CaptureInput(frozenNative,"current"),next=(bool)frozenNative["inTransition"]?CaptureInput(frozenNative,"next"):null;
            if(Math.Abs(first.weight+(next==null?0:next.weight)-1)>Tolerance)throw new InvalidOperationException("Native raw mixer weights must sum to one.");
            bool appearance=(first.clip.name=="kraken_appear" && first.weight>0) || (next!=null && next.clip.name=="kraken_appear" && next.weight>0);
            bool zeroAppearance=(first.clip.name=="kraken_appear" && first.weight==0) || (next!=null && next.clip.name=="kraken_appear" && next.weight==0);
            JObject initial=Evaluate(first,next),repeat=Evaluate(first,next);float repeatError=Compare(initial,repeat),error=appearance?0:Compare(initial,(JObject)frozenNative["pose"]);
            string afterModern=HashTree(modern.nodes),afterOld=HashTree(old.nodes),afterSource=HashTree(sourceNodes);
            if(beforeModern!=afterModern || beforeOld!=afterOld || afterSource!=sourceHash)throw new InvalidOperationException("Mixer modified native source/input transforms.");
            if(appearance)excludedFrames++;else{eligibleFrames++;maximumMainError=Math.Max(maximumMainError,error);if(error>Tolerance)failedMainFrames++;}
            maximumRepeatError=Math.Max(maximumRepeatError,repeatError);if(repeatError>Tolerance)failedRepeatFrames++;
            JObject result=new JObject{{"mode",selectedMode},{"eligibleMainComparison",!appearance},{"appearanceZeroWeightBoundary",zeroAppearance},
                {"scope",appearance?"appearance_contribution_excluded_no_native_mixed_equivalence":"main_whole_controller_comparison_including_history"},
                {"inputs",next==null?new JArray(first.view):new JArray(first.view,next.view)},{"inactiveNextClipMetadataIgnored",next==null},
                {"samplerRootInstanceId",rootId},{"samplerAnimatorInstanceId",animatorId},{"avatarInstanceId",avatar.GetInstanceID()},
                {"runtimeControllerAssigned",false},{"fireEvents",false},{"applyRootMotion",false},{"footIK",false},{"playableIK",new JValue((object)null)},{"playableIKControlAvailable",false},{"avatarIsHuman",avatar.isHuman},{"graphMode","Manual"},{"playableCount",graph.GetPlayableCount()},
                {"inputWeights",InputWeights()},{"playableTimes",InputTimes()},{"resetCount",resetCount},
                {"firstPose",initial},{"repeatedPose",repeat},{"repeatMaximumError",repeatError},{"repeatPassed",repeatError<=Tolerance},
                {"mainMaximumError",appearance?new JValue((object)null):new JValue(error)},{"mainComparisonPassed",appearance?new JValue((object)null):new JValue(error<=Tolerance)},
                {"nativeModernBeforeSha256",beforeModern},{"nativeModernAfterSha256",afterModern},{"nativeOldBeforeSha256",beforeOld},{"nativeOldAfterSha256",afterOld},{"sourcePrefabAfterSha256",afterSource}};
            if(Fixed)result["fixedClipInstanceIds"]=ClipIds();return result;
        }
        public JObject View(){return new JObject{{"mode",selectedMode},{"identity",identity},{"eligibleFrames",eligibleFrames},{"excludedAppearanceFrames",excludedFrames},{"resets",resetCount},{"failedMainFrames",failedMainFrames},{"failedRepeatFrames",failedRepeatFrames},{"maximumMainError",maximumMainError},{"maximumRepeatError",maximumRepeatError},{"reportedNumericalComparisonPassed",eligibleFrames>0 && failedMainFrames==0 && failedRepeatFrames==0},{"graphDisposed",graphDisposed},{"targetUnityNull",root==null},
            {"sourceUnchanged",sourceNodes.Count>0 && HashTree(sourceNodes)==sourceHash},{"boundary","Mechanics completion is not comparison acceptance. All numerical mismatches preserved; independent paired verifier required. No gameplay adapter, attack callback, endpoint policy or skin claim."}};}
        public void Dispose(){try{if(graph.IsValid())graph.Destroy();}finally{graphDisposed=!graph.IsValid();if(root!=null)UnityEngine.Object.Destroy(root);}}
        public bool Clean(){return graphDisposed && root==null;}
    }
}
