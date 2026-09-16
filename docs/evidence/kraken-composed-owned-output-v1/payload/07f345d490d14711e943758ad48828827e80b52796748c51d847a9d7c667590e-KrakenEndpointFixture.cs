using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Authored endpoint policy diagnostic only. None of these targets is a native graph input.
    sealed class KrakenEndpointFixturePlan
    {
        const float Tolerance=1e-5f;
        public const string ComposedPolicy="fixed-main-appearance-local-v1";
        bool composed;
        static readonly string[] Sources={"Root_M/base/body","Root_M/base/body/neck","Root_M/base/body/neck/head","Root_M/base/body/neck/head/topHead"};
        sealed class SampleSurface
        {
            public GameObject root;public readonly List<SampleNode> nodes=new List<SampleNode>();
            public readonly KrakenPlayableSampler sampler=new KrakenPlayableSampler();public JObject rest,initialization;
            public void Create(GameObject source,AnimationClip clip,string name,string[] paths)
            {
                root=new GameObject(name);root.SetActive(false);CopySampleTree(source.transform,root.transform,"",nodes);
                rest=KrakenControllerPose(nodes,paths);
                sampler.Create(root,source.GetComponent<Animator>().avatar,clip);root.SetActive(true);
                initialization=sampler.InitializeAfterActivation();AssertTransformOnly(root,nodes.Count,sampler.animator);sampler.Audit();
            }
            public void Sample(float seconds){ResetSampleTree(nodes);sampler.Sample(seconds);AssertTransformOnly(root,nodes.Count,sampler.animator);}
            public void Dispose(){sampler.Destroy();if(root!=null)UnityEngine.Object.Destroy(root);}
            public bool Clean(){return !sampler.graph.IsValid() && root==null;}
        }
        sealed class Pair
        {
            public AnimationClip clip;public readonly SampleSurface modern=new SampleSurface(),old=new SampleSurface();
        }
        readonly Dictionary<string,Pair> pairs=new Dictionary<string,Pair>();
        readonly List<SampleNode> outputNodes=new List<SampleNode>();
        readonly Matrix4x4[] sourceRest=new Matrix4x4[4],targetRest=new Matrix4x4[4];
        public Transform OutputNode(string path){return SampleTransform(outputNodes,path);}
        public List<SampleNode> OutputNodes(){return outputNodes;}
        public GameObject output;public JObject rests;public int pureChecks;
        GameObject presentation;Matrix4x4 presentationMatrix;JObject presentationIdentity;bool presentationEnabled;
        // Only the optional skin presentation is translated. Local/model-space endpoint evidence is unchanged.
        public void EnableOriginPresentation(string id)
        {
            if(presentationEnabled || output==null || output.transform.parent!=null)throw new InvalidOperationException("Presentation requires a fresh unparented owned output.");
            JObject before=KrakenControllerPose(outputNodes,KrakenOldPaths);
            Matrix4x4 originalTop=output.transform.localToWorldMatrix;
            Vector3 translation=-output.transform.localPosition;
            presentation=new GameObject("FTK_KRAKEN_PRESENTATION_"+id);presentationEnabled=true;
            presentation.transform.position=translation;presentation.transform.rotation=Quaternion.identity;presentation.transform.localScale=Vector3.one;
            presentationMatrix=presentation.transform.localToWorldMatrix;
            output.transform.SetParent(presentation.transform,false);
            Near(Local(output.transform),originalTop,"presentation preserves output top LOCAL");
            if(!JToken.DeepEquals(before,KrakenControllerPose(outputNodes,KrakenOldPaths)))throw new InvalidOperationException("Presentation changed endpoint locals/models.");
            Near(output.transform.localToWorldMatrix,presentationMatrix*originalTop,"shared origin presentation");
            presentationIdentity=new JObject{{"mode","owned-output-parent-translation"},{"owner","endpoint fixture plan"},
                {"parentInstanceId",presentation.GetInstanceID()},{"outputInstanceId",output.GetInstanceID()},
                {"originalOutputTopWorld",SampleMatrix(originalTop)},{"outputTopLocal",SampleMatrix(Local(output.transform))},
                {"parentLocalToWorld",SampleMatrix(presentationMatrix)},{"outputLocalsAndModelsUnchanged",true},
                {"scope","Presentation-only translation shared by output bones, renderer placement and fixed camera. Native graph and sampler transforms unchanged."}};
            Presentation();
        }
        public JObject Presentation()
        {
            if(!presentationEnabled)return null;
            if(presentation==null || output==null || presentation.transform.parent!=null || output.transform.parent!=presentation.transform
                || presentation.transform.childCount!=1 || presentation.GetComponents<Component>().Length!=1)
                throw new InvalidOperationException("Owned presentation hierarchy changed.");
            Near(presentation.transform.localToWorldMatrix,presentationMatrix,"immutable presentation parent");
            return (JObject)presentationIdentity.DeepClone();
        }
        public bool PresentationClean(){return !presentationEnabled || presentation==null;}

        static Matrix4x4 Local(Transform t){return KrakenOldAdapterPlan.Read(t).Matrix();}
        static Matrix4x4 Model(List<SampleNode> nodes,string path){return KrakenLocalChain(SampleTransform(nodes,path),SampleTransform(nodes,""));}
        static void Near(Matrix4x4 a,Matrix4x4 b,string label){KrakenOldAdapterPlan.Near(a,b,label);}
        static void Finite(float v){if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Nonfinite endpoint scalar.");}
        public void Create(GameObject modern,GameObject old,RuntimeAnimatorController controller,string scenario,string id,bool useFixedInput=false)
        {
            composed=useFixedInput;output=new GameObject("FTK_KRAKEN_ENDPOINT_OUTPUT_"+id);output.SetActive(false);CopySampleTree(old.transform,output.transform,"",outputNodes);
            AssertTransformOnly(output,outputNodes.Count);
            string target=scenario=="appear"?"kraken_appear":scenario.StartsWith("damaged",StringComparison.Ordinal)?"krakenDamage":"krakenDisappear";
            foreach(string name in new[]{"krakenIdle",target})
            {
                AnimationClip found=null;foreach(AnimationClip clip in controller.animationClips)if(clip.name==name)
                {if(found!=null && found!=clip)throw new InvalidOperationException("Ambiguous endpoint clip.");found=clip;}
                if(found==null)throw new InvalidOperationException("Missing endpoint clip "+name);
                Pair pair=new Pair{clip=found};pairs.Add(name,pair);
                pair.modern.Create(modern,found,"FTK_ENDPOINT_MODERN_"+name+id,KrakenMainPaths);
                pair.old.Create(old,found,"FTK_ENDPOINT_OLD_"+name+id,KrakenOldPaths);
                for(int i=0;i<4;i++){sourceRest[i]=KrakenOldAdapterPlan.Rest(pair.modern.nodes,Sources[i]);targetRest[i]=KrakenOldAdapterPlan.Rest(pair.old.nodes,KrakenOldPaths[i]);}
            }
            rests=new JObject{{"output",KrakenControllerPose(outputNodes,KrakenOldPaths)},{"pairs",new JObject()}};
            foreach(KeyValuePair<string,Pair> item in pairs)((JObject)rests["pairs"])[item.Key]=new JObject{{"modern",item.Value.modern.rest},{"old",item.Value.old.rest},{"modernInitialization",item.Value.modern.initialization},{"oldInitialization",item.Value.old.initialization}};
        }
        static float Compare(List<SampleNode> a,List<SampleNode> b,string[] paths,string label)
        {
            float max=0;List<string> all=new List<string>(paths);all.Add("Root_M");all.Add("");
            foreach(string path in all)
            {
                Matrix4x4 al=Local(SampleTransform(a,path)),bl=Local(SampleTransform(b,path));Near(al,bl,label+" local "+path);
                Matrix4x4 am=Model(a,path),bm=Model(b,path);Near(am,bm,label+" model "+path);
                max=Math.Max(max,Math.Max(KrakenOldAdapterPlan.Difference(al,bl),KrakenOldAdapterPlan.Difference(am,bm)));
            }
            return max;
        }
        Pair SampleSlot(AnimatorStateInfo state,AnimatorClipInfo[] clips,AnimatorStateInfo oldState,AnimatorClipInfo[] oldClips,string role,JArray samples,bool pure,KrakenControllerSurface modern,KrakenControllerSurface old)
        {
            if(clips.Length!=1 || oldClips.Length!=1 || clips[0].clip!=oldClips[0].clip || clips[0].weight!=oldClips[0].weight
                || state.fullPathHash!=oldState.fullPathHash || state.normalizedTime!=oldState.normalizedTime)
                throw new InvalidOperationException("Endpoint graph state/clip identity mismatch.");
            Pair pair;if(!pairs.TryGetValue(clips[0].clip.name,out pair) || pair.clip!=clips[0].clip)throw new InvalidOperationException("Unexpected endpoint clip identity.");
            string stateClip=state.fullPathHash==Animator.StringToHash("Base Layer.IDLE")?"krakenIdle":state.fullPathHash==Animator.StringToHash("Base Layer.OverworldAppear")?"kraken_appear":
                state.fullPathHash==Animator.StringToHash("Base Layer.DAMAGED")||state.fullPathHash==Animator.StringToHash("Base Layer.DAMAGEDHEAVY")?"krakenDamage":
                state.fullPathHash==Animator.StringToHash("Base Layer.DEATH")||state.fullPathHash==Animator.StringToHash("Base Layer.DEATHLIGHT")?"krakenDisappear":null;
            if(pair.clip.name!=stateClip)throw new InvalidOperationException("Native state-to-clip mapping changed.");
            Finite(state.normalizedTime);Finite(state.length);Finite(pair.clip.length);Finite(clips[0].weight);
            if(state.speed!=1 || state.speedMultiplier!=1 || oldState.speed!=1 || oldState.speedMultiplier!=1 || state.loop!=pair.clip.isLooping || oldState.loop!=state.loop
                || pair.clip.length<=0 || clips[0].weight<0 || clips[0].weight>1)throw new InvalidOperationException("Unsupported endpoint clock/weight contract.");
            double phase=state.normalizedTime,mapped=state.loop?phase-Math.Floor(phase):Math.Max(0,Math.Min(1,phase));
            float seconds=(float)(mapped*pair.clip.length);pair.modern.Sample(seconds);pair.old.Sample(seconds);
            float maximum=0;if(pure)
            {
                if(Math.Abs(clips[0].weight-1)>Tolerance)throw new InvalidOperationException("Pure endpoint requires full clip weight.");
                maximum=Math.Max(Compare(pair.modern.nodes,modern.nodes,KrakenMainPaths,"pure modern"),Compare(pair.old.nodes,old.nodes,KrakenOldPaths,"pure old"));pureChecks++;
            }
            samples.Add(new JObject{{"role",role},{"clip",pair.clip.name},{"clipInstanceId",pair.clip.GetInstanceID()},{"clipLength",pair.clip.length},{"loop",state.loop},
                {"state",KrakenControllerState(state)},{"rawWeight",clips[0].weight},{"requestedUnwrappedSeconds",phase*pair.clip.length},{"mappedPhase",mapped},
                {"sampleSeconds",seconds},{"modernSampler",pair.modern.sampler.View()},{"oldSampler",pair.old.sampler.View()},{"modernRootInstanceId",pair.modern.root.GetInstanceID()},{"oldRootInstanceId",pair.old.root.GetInstanceID()},{"modernPlayableTime",pair.modern.sampler.playable.GetTime()},{"oldPlayableTime",pair.old.sampler.playable.GetTime()},
                {"scope",pure?"observed_pure_graph_same_clock_check":"constructed_policy_endpoint_not_native_mixed_equivalence"},{"pureMaximumError",pure?(JToken)new JValue(maximum):new JValue((object)null)},
                {"modern",KrakenControllerPose(pair.modern.nodes,KrakenMainPaths)},{"old",KrakenControllerPose(pair.old.nodes,KrakenOldPaths)}});
            return pair;
        }
        Matrix4x4[] Main(List<SampleNode> nodes)
        {
            Matrix4x4[] result=new Matrix4x4[4];for(int i=0;i<4;i++)result[i]=Model(nodes,Sources[i])*sourceRest[i].inverse*targetRest[i];return result;
        }
        Matrix4x4[] MainSnapshot(JObject pose)
        {
            Matrix4x4[] result=new Matrix4x4[4];
            for(int i=0;i<4;i++)
            {
                JArray rows=pose["prefabRootModels"][Sources[i]]as JArray;if(rows==null || rows.Count!=4)throw new InvalidOperationException("Exact fixed input matrix required.");
                Matrix4x4 matrix=new Matrix4x4();for(int r=0;r<4;r++){JArray row=rows[r]as JArray;if(row==null || row.Count!=4)throw new InvalidOperationException("Fixed matrix dimensions.");for(int c=0;c<4;c++){JToken value=row[c];if(value.Type!=JTokenType.Float && value.Type!=JTokenType.Integer)throw new InvalidOperationException("Numeric fixed input required.");matrix[r,c]=(float)value;Finite(matrix[r,c]);}}
                result[i]=matrix*sourceRest[i].inverse*targetRest[i];
            }
            return result;
        }
        static KrakenOldAdapterPlan.Trs[] Locals(Matrix4x4[] models,Matrix4x4 root)
        {
            KrakenOldAdapterPlan.Trs[] result=new KrakenOldAdapterPlan.Trs[4];KrakenOldAdapterPlan.Decompose(root);
            for(int i=0;i<4;i++)result[i]=KrakenOldAdapterPlan.Decompose((i==0?root:models[i-1]).inverse*models[i]);return result;
        }
        static JArray Matrices(Matrix4x4[] values){JArray result=new JArray();foreach(Matrix4x4 value in values)result.Add(SampleMatrix(value));return result;}
        public JObject Step(KrakenControllerSurface modern,KrakenControllerSurface old,JObject fixedFrame=null)
        {
            if(composed && (fixedFrame==null || (string)fixedFrame["mode"]!=KrakenModernInputMixer.FixedMode || !(fixedFrame["firstPose"]is JObject)))throw new InvalidOperationException("Composed policy requires frozen fixed-four sample.");
            if(!composed && fixedFrame!=null)throw new InvalidOperationException("Legacy policy cannot consume fixed input.");
            modern.Audit();old.Audit();bool transition=modern.controller.IsInTransition(0);
            if(transition!=old.controller.IsInTransition(0))throw new InvalidOperationException("Endpoint transition disagreement.");
            JObject beforeModern=KrakenControllerPose(modern.nodes,KrakenMainPaths),beforeOld=KrakenControllerPose(old.nodes,KrakenOldPaths);
            JArray samples=new JArray();AnimatorClipInfo[] current=modern.controller.GetCurrentAnimatorClipInfo(0);
            Pair first=SampleSlot(modern.controller.GetCurrentAnimatorStateInfo(0),current,old.controller.GetCurrentAnimatorStateInfo(0),old.controller.GetCurrentAnimatorClipInfo(0),"current",samples,!transition,modern,old);
            Pair second=null;AnimatorClipInfo[] next=null;
            if(transition)
            {
                next=modern.controller.GetNextAnimatorClipInfo(0);
                second=SampleSlot(modern.controller.GetNextAnimatorStateInfo(0),next,old.controller.GetNextAnimatorStateInfo(0),old.controller.GetNextAnimatorClipInfo(0),"next",samples,false,modern,old);
                if(Math.Abs(current[0].weight+next[0].weight-1)>Tolerance)throw new InvalidOperationException("Raw endpoint weights must sum to one.");
            }
            if(composed)
            {
                JArray inputs=fixedFrame["inputs"]as JArray;if(inputs==null || inputs.Count!=samples.Count)throw new InvalidOperationException("Composed input roles differ from frozen native frame.");
                for(int i=0;i<samples.Count;i++)foreach(string key in new[]{"role","clip","clipInstanceId","state","rawWeight","sampleSeconds","mappedPhase"})
                    if(!JToken.DeepEquals(samples[i][key],inputs[i][key]))throw new InvalidOperationException("Composed native clock/identity changed: "+key);
            }
            Pair appearance=first.clip.name=="kraken_appear"?first:second!=null&&second.clip.name=="kraken_appear"?second:null;
            Pair main=appearance==first?second:first;float alpha=appearance==null?0:appearance==first?current[0].weight:next[0].weight;
            if(appearance!=null && transition && (main==null || main==appearance))throw new InvalidOperationException("Exactly one main and one appearance endpoint required.");
            Matrix4x4 root=Model(old.nodes,"Root_M");Matrix4x4[] mainModels=appearance==null?(composed?MainSnapshot((JObject)fixedFrame["firstPose"]):Main(modern.nodes)):main==null?null:Main(main.modern.nodes);
            Matrix4x4[] appearanceModels=null;if(appearance!=null){appearanceModels=new Matrix4x4[4];for(int i=0;i<4;i++)appearanceModels[i]=Model(appearance.old.nodes,KrakenOldPaths[i]);}
            KrakenOldAdapterPlan.Trs[] a=mainModels==null?null:Locals(mainModels,root),b=appearanceModels==null?null:Locals(appearanceModels,root),pending=new KrakenOldAdapterPlan.Trs[4];
            for(int i=0;i<4;i++)
            {
                if(b==null)pending[i]=a[i];else if(a==null)pending[i]=b[i];else
                {
                    Quaternion from=a[i].rotation,to=b[i].rotation;
                    double[] q=KrakenEndpointRotation.Interpolate(new double[]{from.x,from.y,from.z,from.w},new double[]{to.x,to.y,to.z,to.w},alpha);
                    pending[i]=new KrakenOldAdapterPlan.Trs{position=Vector3.Lerp(a[i].position,b[i].position,alpha),scale=Vector3.Lerp(a[i].scale,b[i].scale,alpha),rotation=new Quaternion((float)q[0],(float)q[1],(float)q[2],(float)q[3])};
                }
                KrakenOldAdapterPlan.Decompose(pending[i].Matrix());
            }
            List<KrakenOldAdapterPlan.Trs> rollback=new List<KrakenOldAdapterPlan.Trs>();foreach(SampleNode node in outputNodes)rollback.Add(KrakenOldAdapterPlan.Read(node.target));
            JObject firstOutput=null;
            try
            {
                for(int repeat=0;repeat<2;repeat++)
                {
                    // Mirror actual native locals onto a separate output; policy then writes only four mapped joints.
                    foreach(SampleNode node in outputNodes)KrakenOldAdapterPlan.Read(SampleTransform(old.nodes,node.path)).Set(node.target);
                    for(int i=0;i<4;i++)pending[i].Set(SampleTransform(outputNodes,KrakenOldPaths[i]));
                    AssertTransformOnly(output,outputNodes.Count);
                    Near(Local(SampleTransform(outputNodes,"Root_M")),Local(SampleTransform(old.nodes,"Root_M")),"output mirrors native root");
                    Near(Local(SampleTransform(outputNodes,KrakenOldPaths[4])),Local(SampleTransform(old.nodes,KrakenOldPaths[4])),"native jaw LOCAL preserved");
                    for(int i=0;i<4;i++)
                    {
                        Near(Local(SampleTransform(outputNodes,KrakenOldPaths[i])),pending[i].Matrix(),"output local readback");
                        if(alpha==0)Near(Model(outputNodes,KrakenOldPaths[i]),mainModels[i],"alpha zero endpoint");
                        if(alpha==1 || mainModels==null)Near(Model(outputNodes,KrakenOldPaths[i]),appearanceModels[i],"alpha one endpoint");
                    }
                    JObject pose=KrakenControllerPose(outputNodes,KrakenOldPaths);
                    if(repeat==0)firstOutput=pose;else if(!JToken.DeepEquals(firstOutput,pose))throw new InvalidOperationException("Repeated output commit drifted.");
                }
                if(!JToken.DeepEquals(beforeModern,KrakenControllerPose(modern.nodes,KrakenMainPaths)) || !JToken.DeepEquals(beforeOld,KrakenControllerPose(old.nodes,KrakenOldPaths)))
                    throw new InvalidOperationException("Native graph input changed during endpoint policy.");
            }
            catch{for(int i=0;i<outputNodes.Count;i++)rollback[i].Set(outputNodes[i].target);throw;}
            JArray mainLocals=new JArray(),appearanceLocals=new JArray();if(a!=null)foreach(KrakenOldAdapterPlan.Trs v in a)mainLocals.Add(SampleMatrix(v.Matrix()));if(b!=null)foreach(KrakenOldAdapterPlan.Trs v in b)appearanceLocals.Add(SampleMatrix(v.Matrix()));
            JObject result=new JObject{{"samples",samples},{"appearanceWeight",alpha},{"branch",appearance==null?(composed?"fixed_modern_sampler":"whole_modern_controller"):"authored_endpoint_local_blend"},
                {"mainEndpointModels",mainModels==null?null:Matrices(mainModels)},{"appearanceEndpointModels",appearanceModels==null?null:Matrices(appearanceModels)},
                {"mainEndpointLocals",mainLocals},{"appearanceEndpointLocals",appearanceLocals},{"actualNativeRootModel",SampleMatrix(root)},
                {"outputRootInstanceId",output.GetInstanceID()},{"output",firstOutput},{"nativeInputsUnchanged",true},{"outputMirrorsNativeRoot",true},{"nativeJawLocalPreserved",true},{"repeatIdentical",true},
                {"scope","Authored output policy only; no native mixed-pose equivalence, mesh deformation, visual continuity or live adapter claim."}};
            if(composed)result["mainInputProvenance"]=appearance==null?"frozen_fixed_four_first_pose":main==null?"appearance_only_no_main":"independent_full_strength_main_endpoint";return result;
        }
        public void Dispose()
        {
            Exception first=null;
            foreach(Pair pair in pairs.Values)foreach(SampleSurface surface in new[]{pair.modern,pair.old})
                try{surface.Dispose();}catch(Exception ex){if(first==null)first=ex;if(surface.root!=null)UnityEngine.Object.Destroy(surface.root);}
            try{if(output!=null)UnityEngine.Object.Destroy(output);}catch(Exception ex){if(first==null)first=ex;}
            try{if(presentation!=null)UnityEngine.Object.Destroy(presentation);}catch(Exception ex){if(first==null)first=ex;}
            if(first!=null)throw new InvalidOperationException("Endpoint cleanup failed.",first);
        }
        public bool Clean(){foreach(Pair pair in pairs.Values)if(!pair.modern.Clean()||!pair.old.Clean())return false;return output==null && PresentationClean();}
    }
}
