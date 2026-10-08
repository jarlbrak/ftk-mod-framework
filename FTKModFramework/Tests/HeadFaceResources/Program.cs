using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FTKModFramework.Core;
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]public sealed class SerializeField:Attribute{}
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a,Object b){return ReferenceEquals(a,b)||ReferenceEquals(a,null)&&ReferenceEquals(b,null)||!ReferenceEquals(a,null)&&a.Destroyed&&ReferenceEquals(b,null)||!ReferenceEquals(b,null)&&b.Destroyed&&ReferenceEquals(a,null);}
        public static bool operator !=(Object a,Object b){return !(a==b);}
        public override bool Equals(object other){return ReferenceEquals(this,other);}
        public override int GetHashCode(){return base.GetHashCode();}
    }
    public class Mesh:Object{}
    public class GameObject:Object
    {
        public bool activeInHierarchy=true;
        public readonly List<Component> Components=new List<Component>();
        public T[] GetComponentsInChildren<T>(bool inactive)where T:class{return Components.OfType<T>().ToArray();}
    }
    public class Component:Object{public GameObject gameObject;public T[] GetComponentsInChildren<T>(bool inactive)where T:class{return gameObject.GetComponentsInChildren<T>(inactive);}}
    public class MonoBehaviour:Component{}
    public class SkinnedMeshRenderer:Component{public Mesh sharedMesh;public bool enabled=true;}
    public static class Resources
    {
        public static readonly List<SkinnedMeshRenderer> Renderers=new List<SkinnedMeshRenderer>();
        public static T[] FindObjectsOfTypeAll<T>()where T:class{return Renderers.OfType<T>().ToArray();}
    }
}
namespace FTKModFramework.Core
{
    internal static class Plugin{internal static Logger Log=new Logger();}
    internal sealed class Logger{internal void LogError(string message){} }
}
namespace FTKModFramework.Core.HotReload
{
    internal static class PaladinResourceState
    {
        internal static int Destroyed;
        internal static void DestroyTracked(UnityEngine.Object value){Destroyed++;value.Destroyed=true;}
    }
}
internal static class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static HeadFaceResources Owner(UnityEngine.GameObject go){var x=new HeadFaceResources{gameObject=go};go.Components.Add(x);return x;}
    static void Main()
    {
        var go=new UnityEngine.GameObject();var owner=Owner(go);
        var source=new UnityEngine.Mesh();var output=new UnityEngine.Mesh();
        var renderer=new UnityEngine.SkinnedMeshRenderer{gameObject=go,sharedMesh=source};go.Components.Add(renderer);
        UnityEngine.Resources.Renderers.Add(renderer);
        Check(owner.Commit(new[]{renderer},new[]{source},new[]{output}),"first face transaction commits");
        Check(owner.Active&&renderer.sharedMesh==output&&HeadFaceResources.ActiveMeshCount==1,"generated mesh leased and assigned");
        Check(!owner.Commit(new[]{renderer},new[]{source},new[]{new UnityEngine.Mesh()}),"duplicate apply rejected");
        renderer.sharedMesh=source;
        Check(!owner.Restore(),"diverged reference fails closed");
        renderer.sharedMesh=output;
        var cloneGo=new UnityEngine.GameObject();var clone=Owner(cloneGo);
        foreach(var name in new[]{"leaseId","renderers","originals","replacements"})
        {var field=typeof(HeadFaceResources).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(clone,field.GetValue(owner));}
        Check(clone.EnsureRetained(),"inherited clone acquires shared lease");
        Check(owner.Restore()&&renderer.sharedMesh==source&&!output.Destroyed,"source restores while clone retains output");
        var cloneRenderer=new UnityEngine.SkinnedMeshRenderer{gameObject=cloneGo,sharedMesh=output};cloneGo.Components.Add(cloneRenderer);
        UnityEngine.Resources.Renderers.Add(cloneRenderer);
        // Serialized renderer reference remapping in Unity is represented by a cloned array here.
        typeof(HeadFaceResources).GetField("renderers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(clone,new[]{cloneRenderer});
        Check(clone.Restore()&&cloneRenderer.sharedMesh==source&&output.Destroyed,"last clone restores then releases generated mesh");
        Check(HeadFaceResources.ActiveLeaseCount==0,"no owned face lease remains");
        // Lifecycle assertions inspect raw ownership, never the diagnostics getters that also prune.
        var raw=typeof(HeadFaceResources).GetField("leases",BindingFlags.Static|BindingFlags.NonPublic);
        var lateGo=new UnityEngine.GameObject();var lateOwner=Owner(lateGo);
        var lateSource=new UnityEngine.Mesh();var lateOutput=new UnityEngine.Mesh();
        var lateRenderer=new UnityEngine.SkinnedMeshRenderer{gameObject=lateGo,sharedMesh=lateSource};
        UnityEngine.Resources.Renderers.Add(lateRenderer);
        Check(lateOwner.Commit(new[]{lateRenderer},new[]{lateSource},new[]{lateOutput}),"late-destruction transaction commits");
        typeof(HeadFaceResources).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lateOwner,null);
        Check(((System.Collections.IDictionary)raw.GetValue(null)).Count==1&&!lateOutput.Destroyed,"OnDestroy retains mesh while native renderer still references it");
        lateRenderer.Destroyed=true;
        HeadFaceResources.Prune();
        Check(((System.Collections.IDictionary)raw.GetValue(null)).Count==0&&lateOutput.Destroyed,"active lifecycle prune releases ownerless mesh after renderer destruction");
        var inactiveGo=new UnityEngine.GameObject{activeInHierarchy=true};var inactiveOwner=Owner(inactiveGo);
        var inactiveSource=new UnityEngine.Mesh();var inactiveOutput=new UnityEngine.Mesh();
        var inactiveRenderer=new UnityEngine.SkinnedMeshRenderer{gameObject=inactiveGo,sharedMesh=inactiveSource};
        UnityEngine.Resources.Renderers.Add(inactiveRenderer);
        Check(inactiveOwner.Commit(new[]{inactiveRenderer},new[]{inactiveSource},new[]{inactiveOutput}),"inactive-clone transaction commits");
        inactiveGo.activeInHierarchy=false;inactiveOwner.Destroyed=true;
        HeadFaceResources.Prune();
        Check(((System.Collections.IDictionary)raw.GetValue(null)).Count==1&&!inactiveOutput.Destroyed,"inactive clone renderer keeps generated mesh despite missing OnDestroy");
        inactiveRenderer.Destroyed=true;
        HeadFaceResources.Prune();
        Check(((System.Collections.IDictionary)raw.GetValue(null)).Count==0&&inactiveOutput.Destroyed,"missing OnDestroy cleanup releases after last native reference dies");
        Console.WriteLine("PASS "+checks+" actual face-resource transaction checks (Unity stand-ins; no live proof)");
    }
}
