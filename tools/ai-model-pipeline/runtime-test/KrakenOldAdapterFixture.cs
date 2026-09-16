using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Explicit owned diagnostic only. No component is attached to any native avatar.
    sealed class KrakenAdapterReadyPin
    {
        readonly MiniHexDungeon dungeon;readonly int level,room;readonly string session;
        readonly object encounter,master;
        public KrakenAdapterReadyPin(string value)
        {
            dungeon=RequireReadyPreparation().dungeon;level=dungeon.m_Level;room=dungeon.m_RoomIndex;session=value;
            encounter=Instance(typeof(EncounterSession));master=Instance(typeof(EncounterSessionMC));
        }
        public void Check(string value)
        {
            ReadyContext current=RequireReadyPreparation();
            if(current.dungeon!=dungeon || dungeon.m_Level!=level || dungeon.m_RoomIndex!=room || session!=value
                || !object.ReferenceEquals(encounter,Instance(typeof(EncounterSession)))
                || !object.ReferenceEquals(master,Instance(typeof(EncounterSessionMC))))
                throw new InvalidOperationException("Adapter fixture Ready dungeon/level/room/session changed.");
        }
        public JObject View(){return new JObject{{"session",session},{"dungeonInstanceId",dungeon.GetInstanceID()},{"level",level},{"room",room}};}
    }
    // Compose actual Unity locals in the requested model space without subtracting prefab world placement.
    static Matrix4x4 KrakenLocalChain(Transform target,Transform top)
    {
        Matrix4x4 result=Matrix4x4.identity;int count=0;
        while(target!=top)
        {
            if(target==null || top==null || ++count>256)throw new InvalidOperationException("Adapter target is not under exact owned model root.");
            result=Matrix4x4.TRS(target.localPosition,target.localRotation,target.localScale)*result;
            target=target.parent;
        }
        SampleMatrix(result);return result;
    }
    sealed class KrakenOldAdapterPlan
    {
        const float Tolerance=0.00001f;
        static readonly string[] Sources={KrakenMainPaths[1],KrakenMainPaths[2],KrakenMainPaths[3],KrakenMainPaths[4]};
        readonly List<SampleNode> modern,old;
        readonly Transform modernTop,oldTop,sharedRoot;
        readonly Transform[] targets=new Transform[5];readonly int[] targetIds=new int[5];
        readonly int rootId;
        readonly Matrix4x4[] sourceRest=new Matrix4x4[4],targetRest=new Matrix4x4[5];
        readonly Matrix4x4 jawRest,oldTopRest,modernTopRest;
        public struct Trs
        {
            public Vector3 position,scale;public Quaternion rotation;
            public Matrix4x4 Matrix(){return Matrix4x4.TRS(position,rotation,scale);}
            public void Set(Transform target){target.localPosition=position;target.localRotation=rotation;target.localScale=scale;}
        }
        public static Trs Read(Transform target){return new Trs{position=target.localPosition,rotation=target.localRotation,scale=target.localScale};}
        public static float Difference(Matrix4x4 a,Matrix4x4 b)
        {
            float max=0;for(int i=0;i<16;i++)
            {
                if(float.IsNaN(a[i]) || float.IsInfinity(a[i]) || float.IsNaN(b[i]) || float.IsInfinity(b[i]))
                    throw new InvalidOperationException("Nonfinite adapter comparison input.");
                float delta=Math.Abs(a[i]-b[i]);
                if(float.IsNaN(delta) || float.IsInfinity(delta))throw new InvalidOperationException("Nonfinite adapter comparison delta.");
                max=Math.Max(max,delta);
            }
            return max;
        }
        public static void Near(Matrix4x4 a,Matrix4x4 b,string label)
        {
            float error=Difference(a,b);
            if(error>Tolerance)throw new InvalidOperationException("Adapter check failed: "+label+"; maximumError="
                +error.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; actual="+SampleMatrix(a)+"; expected="+SampleMatrix(b));
        }
        public static Trs Decompose(Matrix4x4 value)
        {
            SampleMatrix(value); // Reject every NaN/infinite element before arithmetic.
            Vector3 x=new Vector3(value.m00,value.m10,value.m20),y=new Vector3(value.m01,value.m11,value.m21),z=new Vector3(value.m02,value.m12,value.m22);
            Vector3 scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);
            if(scale.x<1e-8f || scale.y<1e-8f || scale.z<1e-8f || Vector3.Dot(Vector3.Cross(x,y),z)<=0)
                throw new InvalidOperationException("Adapter requires nonsingular positive determinant TRS.");
            x/=scale.x;y/=scale.y;z/=scale.z;
            if(Math.Abs(Vector3.Dot(x,y))>Tolerance || Math.Abs(Vector3.Dot(x,z))>Tolerance || Math.Abs(Vector3.Dot(y,z))>Tolerance)
                throw new InvalidOperationException("Adapter refuses sheared matrix.");
            Trs result=new Trs{position=new Vector3(value.m03,value.m13,value.m23),scale=scale,rotation=Quaternion.LookRotation(z,y)};
            SampleMatrix(result.Matrix());Near(value,result.Matrix(),"TRS reconstruction");return result;
        }
        public static Matrix4x4 Rest(List<SampleNode> nodes,string path)
        {
            Matrix4x4 result=Matrix4x4.identity;
            while(path!="")
            {
                SampleNode found=null;foreach(SampleNode node in nodes)if(node.path==path){if(found!=null)throw new InvalidOperationException("Ambiguous rest path.");found=node;}
                if(found==null)throw new InvalidOperationException("Missing rest path.");
                result=Matrix4x4.TRS(found.position,found.rotation,found.scale)*result;
                int slash=path.LastIndexOf('/');path=slash<0?"":path.Substring(0,slash);
            }
            return result;
        }
        public KrakenOldAdapterPlan(List<SampleNode> modernNodes,List<SampleNode> oldNodes)
        {
            modern=modernNodes;old=oldNodes;modernTop=SampleTransform(modern,"");oldTop=SampleTransform(old,"");
            sharedRoot=SampleTransform(old,"Root_M");rootId=sharedRoot.GetInstanceID();oldTopRest=Read(oldTop).Matrix();modernTopRest=Read(modernTop).Matrix();
            for(int i=0;i<5;i++)
            {
                targets[i]=SampleTransform(old,KrakenOldPaths[i]);targetIds[i]=targets[i].GetInstanceID();targetRest[i]=Rest(old,KrakenOldPaths[i]);
                Decompose(targetRest[i]);if(i<4){sourceRest[i]=Rest(modern,Sources[i]);Decompose(sourceRest[i]);}
            }
            jawRest=Read(targets[4]).Matrix();
            Identities();
        }
        void Identities()
        {
            Near(oldTopRest,Read(oldTop).Matrix(),"old top placement unchanged");
            Near(modernTopRest,Read(modernTop).Matrix(),"modern top placement unchanged");
            if(sharedRoot==null || sharedRoot.GetInstanceID()!=rootId || SampleTransform(old,"Root_M")!=sharedRoot)
                throw new InvalidOperationException("Adapter shared root identity changed.");
            for(int i=0;i<5;i++)if(targets[i]==null || targets[i].GetInstanceID()!=targetIds[i] || SampleTransform(old,KrakenOldPaths[i])!=targets[i]
                || targets[i].parent!=(i==0?sharedRoot:targets[i==4?1:i-1]))throw new InvalidOperationException("Adapter target identity/parent changed.");
        }
        public JObject RestView()
        {
            JObject source=new JObject(),target=new JObject();for(int i=0;i<5;i++){target[KrakenOldPaths[i]]=SampleMatrix(targetRest[i]);if(i<4)source[Sources[i]]=SampleMatrix(sourceRest[i]);}
            return new JObject{{"capturedBeforeAnimatorInitialization",true},{"oldTopLocal",SampleMatrix(oldTopRest)},{"modernTopLocal",SampleMatrix(modernTopRest)},{"modernRestModels",source},{"oldRestModels",target},{"jawRestLocal",SampleMatrix(jawRest)},
                {"sharedRootInstanceId",rootId},{"targetInstanceIds",new JArray(targetIds)},{"tolerance",Tolerance}};
        }
        JObject Apply(Matrix4x4[] source,bool injectCommitFailure)
        {
            Identities();Trs rootBefore=Read(sharedRoot);Matrix4x4 rootModel=KrakenLocalChain(sharedRoot,oldTop);
            Decompose(rootModel);Trs[] before=new Trs[5],pending=new Trs[5];Matrix4x4[] desired=new Matrix4x4[5];
            for(int i=0;i<5;i++)before[i]=Read(targets[i]);
            // Resolve all outputs from independent source snapshots, never previous target writes.
            for(int i=0;i<4;i++){Decompose(source[i]);desired[i]=source[i]*sourceRest[i].inverse*targetRest[i];}
            desired[4]=desired[1]*jawRest;
            for(int i=0;i<5;i++)pending[i]=Decompose((i==0?rootModel:desired[i==4?1:i-1]).inverse*desired[i]);
            float max=0;
            try
            {
                for(int i=0;i<5;i++){pending[i].Set(targets[i]);if(injectCommitFailure && i==1)
                    {
                        if(Difference(before[0].Matrix(),Read(targets[0]).Matrix())<=Tolerance || Difference(before[1].Matrix(),Read(targets[1]).Matrix())<=Tolerance)
                            throw new InvalidOperationException("Injected rollback requires two actually changed target locals.");
                        throw new InvalidOperationException("Injected owned adapter commit failure.");
                    }}
                Identities();Near(rootBefore.Matrix(),Read(sharedRoot).Matrix(),"shared root unchanged");
                Near(rootModel,KrakenLocalChain(sharedRoot,oldTop),"shared root model unchanged");
                for(int i=0;i<5;i++)
                {
                    Matrix4x4 actual=KrakenLocalChain(targets[i],oldTop);
                    Near(actual,desired[i],"actual Unity target model "+KrakenOldPaths[i]);max=Math.Max(max,Difference(actual,desired[i]));
                }
                Near(jawRest,Read(targets[4]).Matrix(),"main jaw rest local");
            }
            catch
            {
                for(int i=0;i<5;i++)before[i].Set(targets[i]);
                for(int i=0;i<5;i++)Near(before[i].Matrix(),Read(targets[i]).Matrix(),"rollback restored target");
                Near(rootBefore.Matrix(),Read(sharedRoot).Matrix(),"rollback leaves root untouched");throw;
            }
            JObject models=new JObject(),worldProducts=new JObject();float worldDelta=0;
            for(int i=0;i<5;i++)
            {
                Matrix4x4 local=KrakenLocalChain(targets[i],oldTop),world=oldTop.worldToLocalMatrix*targets[i].localToWorldMatrix;
                models[KrakenOldPaths[i]]=SampleMatrix(local);worldProducts[KrakenOldPaths[i]]=SampleMatrix(world);
                worldDelta=Math.Max(worldDelta,Difference(local,world));
            }
            return new JObject{{"ok",true},{"modelReadbackMethod","actual-local-TRS-chain-excluding-owned-top"},{"worldProductDiagnostic",worldProducts},{"maximumWorldProductDifference",worldDelta},{"maximumModelReadbackError",max},{"targetModels",models},{"targetLocals",LocalSampleMatrices(old,KrakenOldPaths)},
                {"sharedRootUnchanged",true},{"targetIdentitiesUnchanged",true},{"jawRestLocal",true}};
        }
        public JObject Sample()
        {
            Matrix4x4[] source=new Matrix4x4[4];for(int i=0;i<4;i++)source[i]=KrakenLocalChain(SampleTransform(modern,Sources[i]),modernTop);
            JObject first=Apply(source,false);JObject repeated=Apply(source,false);
            double delta=0;foreach(string path in KrakenOldPaths)delta=Math.Max(delta,SamplePoseDelta(first["targetLocals"][path],repeated["targetLocals"][path]));
            if(delta>Tolerance)throw new InvalidOperationException("Repeated adapter inputs drifted.");
            first["repeatMaximumLocalError"]=delta;first["sourceSnapshotsBeforeWrites"]=true;return first;
        }
        public JObject NeutralAndNegativeChecks()
        {
            JObject neutral=Apply(sourceRest,false);
            for(int i=0;i<5;i++)Near(targetRest[i],KrakenLocalChain(targets[i],oldTop),"neutral rest recovery");
            Trs[] before=new Trs[5];for(int i=0;i<5;i++)before[i]=Read(targets[i]);
            Matrix4x4[] changedSource=(Matrix4x4[])sourceRest.Clone();for(int i=0;i<changedSource.Length;i++)changedSource[i].m03+=.125f*(i+1);
            bool rollback=false;try{Apply(changedSource,true);}catch(InvalidOperationException ex){rollback=ex.Message=="Injected owned adapter commit failure.";}
            if(!rollback)throw new InvalidOperationException("Injected commit rollback test failed.");
            for(int i=0;i<5;i++)Near(before[i].Matrix(),Read(targets[i]).Matrix(),"injected rollback readback");
            Matrix4x4[] bad={Matrix4x4.identity,Matrix4x4.identity,Matrix4x4.identity};bad[0].m00=0;bad[1].m01=.5f;bad[2].m00=float.NaN;
            foreach(Matrix4x4 value in bad){bool rejected=false;try{Decompose(value);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new InvalidOperationException("Invalid TRS accepted.");}
            Matrix4x4 nonfinite=Matrix4x4.identity;nonfinite.m00=float.NaN;
            bool comparisonRejected=false;try{Near(nonfinite,Matrix4x4.identity,"negative nonfinite readback");}catch(InvalidOperationException){comparisonRejected=true;}
            if(!comparisonRejected)throw new InvalidOperationException("Nonfinite readback comparison accepted.");
            ResetSampleTree(old); // Restore exact saved locals before Animator initialization.
            return new JObject{{"neutral",neutral},{"injectedCommitRollback",rollback},{"singularShearNonfiniteRejected",true},{"nonfiniteReadbackRejected",comparisonRejected},{"exactSavedLocalsRestored",true}};
        }
    }
}
