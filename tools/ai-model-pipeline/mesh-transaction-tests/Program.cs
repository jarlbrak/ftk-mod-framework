using System;
using System.Collections.Generic;
using System.Reflection;
using FTKModFramework.Core;
using UnityEngine;
using UObject=UnityEngine.Object;
namespace UnityEngine
{
    public class Object
    {
        public string name; public bool destroyed;
        public static readonly List<Object> created=new List<Object>();
        public Object(){created.Add(this);}
        public static void Destroy(Object value){value.destroyed=true;}
        public static bool operator ==(Object a,Object b){return ReferenceEquals(a,b)||(ReferenceEquals(a,null)&&!ReferenceEquals(b,null)&&b.destroyed)||(ReferenceEquals(b,null)&&!ReferenceEquals(a,null)&&a.destroyed);}
        public static bool operator !=(Object a,Object b){return !(a==b);}
        public override bool Equals(object value){return ReferenceEquals(this,value);}
        public override int GetHashCode(){return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);}
    }
    public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool all)where T:class=>gameObject.GetComponentsInChildren<T>(all);public T[] GetComponents<T>()where T:class=>gameObject.components.FindAll(c=>c is T).ConvertAll(c=>c as T).ToArray();public T GetComponentInParent<T>()where T:class {for(var t=transform;t!=null;t=t.parent){var v=t.gameObject.GetComponent<T>();if(v!=null)return v;}return null;}}
    public class MonoBehaviour:Component{}
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator *(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);}
    public static class Time { public static float deltaTime=.25f; }
    public class SerializeField:Attribute{}
    public class GameObject:Object
    {
        public Transform transform;public List<Component> components=new List<Component>();
        public GameObject(string name){this.name=name;transform=new Transform{gameObject=this,name=name};}
        public T AddComponent<T>()where T:Component,new(){var value=new T{gameObject=this};components.Add(value);return value;}
        public T GetComponent<T>()where T:class {foreach(var c in components)if(c!=null&&c is T)return c as T;return null;}
        public T[] GetComponentsInChildren<T>(bool all)where T:class {var result=new List<T>();if(transform is T)result.Add(transform as T);foreach(var c in components)if(c!=null&&c is T)result.Add(c as T);foreach(var child in transform.children)result.AddRange(child.gameObject.GetComponentsInChildren<T>(all));return result.ToArray();}
    }
    public class Transform:Component {public Transform parent;public List<Transform> children=new List<Transform>();public int pose=73;}
    public struct Matrix4x4 {public int value;}
    public struct Bounds {public int value;}
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;}public Color(float value){r=g=b=value;a=1;}public static Color black=>new Color(0);}
    public class Material:Object
    {
        public Dictionary<string,Vector2> offsets=new Dictionary<string,Vector2>();public Color color; public bool emission=true;public Dictionary<string,Object> textures=new Dictionary<string,Object>();
        public Material(){}
        public Material(Material source){name=source.name;color=source.color;emission=source.emission;foreach(var entry in source.textures)textures[entry.Key]=entry.Value;}
        public void SetTextureOffset(string name,Vector2 value){offsets[name]=value;}public bool HasProperty(string name)=>name!="missing";
        public Object mainTexture {get=>textures.GetValueOrDefault("_MainTex");set=>textures["_MainTex"]=value;}
        public void EnableKeyword(string name){emission=true;}
        public void DisableKeyword(string name){emission=false;}
        public void SetColor(string name,Color value){if(name=="_Color")color=value;}
        public void SetTexture(string name,Object texture){textures[name]=texture;}
    }
    public class Renderer:Component
    {
        Material[] mats;public bool failOnce;public int failCount;public int assignThenFailCount;public bool enabled=true;
        public Material[] sharedMaterials {get=>mats;set {if(assignThenFailCount>0){assignThenFailCount--;if(assignThenFailCount==1)mats=value;throw new Exception("injected live-assigned setter failure");}if(failCount>0){failCount--;throw new Exception("injected repeated material setter failure");}if(failOnce){failOnce=false;throw new Exception("injected renderer commit failure");}mats=value;}}
    }
    public class SkinnedMeshRenderer:Renderer {public Mesh sharedMesh;public Transform[] bones;public Bounds localBounds;}
    public class MeshRenderer:Renderer {}
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class Mesh:Object {public Matrix4x4[] bindposes=new[]{new Matrix4x4{value=19}};}
    public enum TextureFormat{RGBA32}
    public class Texture2D:Object {public static bool loadResult=true; public Texture2D(int w,int h){}public Texture2D(int w,int h,TextureFormat f,bool m){}public bool LoadImage(byte[] bytes)=>loadResult;}
}
public class CharacterEventListener:Component{}
public class ScrollingUVs:MonoBehaviour {public int materialIndex;public Vector2 uvAnimationRate=new Vector2(1,0);public string textureName="_MainTex";private Vector2 uvOffset=new Vector2(0,0);}
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type type,string method){}}}
namespace FTKModFramework.Core
{
    static class Plugin {public static Logger Log=new Logger();public class Logger {public string lastWarning;public void LogWarning(string v){lastWarning=v;}public void LogInfo(string v){}public void LogError(string v){} }}
    static class CustomModelLoader {public static string ResolveModelPath(string file)=>file;}
    static class RuntimeGltfMeshLoader
    {
        public static int calls;
        public static int staticCalls;
        public static int[] lastMap;
        public static Mesh LoadSkinnedGlb(string file,Transform[] bones,Matrix4x4[] bind,bool strict,int[] map=null){lastMap=map;calls++;if(!strict)throw new Exception("must be strict");return file=="bad.glb"?null:new Mesh{name=file,bindposes=bind};}
        public static Mesh LoadStaticGlb(string file,bool strict){staticCalls++;if(!strict)throw new Exception("must be strict");return file=="bad-static.glb"?null:new Mesh{name=file};}
    }
}
static class Program
{
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static CharacterEventListener Avatar(out SkinnedMeshRenderer a,out SkinnedMeshRenderer b)
    {
        var root=new GameObject("avatar");var cel=root.AddComponent<CharacterEventListener>();a=Part(root,"a");b=Part(root,"b");return cel;
    }
    static SkinnedMeshRenderer Part(GameObject root,string name)
    {
        var child=new GameObject(name);child.transform.parent=root.transform;root.transform.children.Add(child.transform);
        var smr=child.AddComponent<SkinnedMeshRenderer>();smr.sharedMesh=new Mesh{name="native"};smr.bones=new[]{child.transform};smr.localBounds=new Bounds{value=31};
        smr.sharedMaterials=new[]{new Material{name="native",color=new Color(.25f)},new Material{name="native slot 2"}};return smr;
    }
    static MeshRenderer StaticPart(GameObject root,string name,out MeshFilter filter)
    {
        var child=new GameObject(name);child.transform.parent=root.transform;root.transform.children.Add(child.transform);
        filter=child.AddComponent<MeshFilter>();filter.sharedMesh=new Mesh{name="native static"};
        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{new Material{name="native static",color=new Color(.25f)}};return renderer;
    }
    static EnemyRendererMesh[] Plan(string second="b.glb")=>new[]{new EnemyRendererMesh("a","a.glb",null,true),new EnemyRendererMesh("b",second)};
    static void CheckNewAssetsDestroyed(int start)
    {foreach(var asset in UObject.created.GetRange(start,UObject.created.Count-start))if(asset is Mesh||asset is Material||asset is Texture2D)Check(asset.destroyed,"Failed transaction releases every new asset");}
    static EnemyMeshResources SerializedClone(EnemyMeshResources owner,CharacterEventListener target)
    {
        var clone=target.gameObject.AddComponent<EnemyMeshResources>();
        foreach(string field in new[]{"_leaseId","_targets","Applied","VisualResourcesOnly"}){var info=typeof(EnemyMeshResources).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);info.SetValue(clone,info.GetValue(owner));}return clone;
    }
    static void MultiSlotChecks()
    {
        var cel=Avatar(out var a,out var b);var native=a.sharedMaterials;var sc=a.gameObject.AddComponent<ScrollingUVs>();sc.materialIndex=1;sc.uvAnimationRate=new Vector2(-.5f,.2f);sc.textureName="_DetailTex";var second=a.gameObject.AddComponent<ScrollingUVs>();second.materialIndex=1;second.textureName="_OtherTex";second.uvAnimationRate=new Vector2(0,-2);
        var descriptors=new[]{new EnemyRendererMaterial(1,0,null,true),new EnemyRendererMaterial(0,1)};
        var assignment=EnemyRendererMesh.WithNativeMaterialSlots("a","two.glb",descriptors);descriptors[0]=null;
        var copy=assignment.MaterialSlots;copy[0]=null;
        Check(assignment.MaterialSlots[0]!=null,"Descriptor arrays are immutable snapshots");
        Check(ExplicitEnemyMeshSwap.Apply("cube",cel,new[]{assignment}),"Two mapped native slots applied");
        Check(a.sharedMaterials.Length==2&&RuntimeGltfMeshLoader.lastMap[0]==1&&RuntimeGltfMeshLoader.lastMap[1]==0,"Explicit primitive-to-native bijection passed through");
        Check(!a.sharedMaterials[0].emission&&a.sharedMaterials[1].emission&&native[0].emission,"Per-slot emission and prefab isolation");
        var owner=cel.GetComponent<EnemyMeshResources>();var phase=typeof(ScrollingUVs).GetField("uvOffset",BindingFlags.NonPublic|BindingFlags.Instance);
        int start=UObject.created.Count;a.enabled=false;Check(!ExplicitScrollingUvs.Prefix(sc),"Owned disabled renderer uses faithful prefix");
        var off=(Vector2)phase.GetValue(sc);Check(off.x==-.125f&&off.y==.05f&&UObject.created.Count==start&&a.sharedMaterials[1].offsets.Count==0,"Disabled accumulation without allocation/write");
        a.enabled=true;ExplicitScrollingUvs.Prefix(sc);Check(a.sharedMaterials[1].offsets["_DetailTex"].x==-.25f&&!a.sharedMaterials[0].offsets.ContainsKey("_DetailTex"),"Resumed negative-rate phase writes exact native slot/property");
        start=UObject.created.Count;ExplicitScrollingUvs.Prefix(second);Check(a.sharedMaterials[1].offsets["_OtherTex"].y==-.5f&&((Vector2)phase.GetValue(sc)).y==.1f&&UObject.created.Count==start,"Multiple native scrollers share one private material set but retain separate phases/properties");
        var clone=Avatar(out var ca,out var cb);ca.sharedMaterials=(Material[])a.sharedMaterials.Clone();var cs=ca.gameObject.AddComponent<ScrollingUVs>();cs.materialIndex=1;cs.textureName="_DetailTex";cs.uvAnimationRate=new Vector2(2,0);phase.SetValue(cs,new Vector2(3,4));
        var lease=SerializedClone(owner,clone);typeof(EnemyMeshResources).GetField("_targets",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lease,new Renderer[]{ca});
        foreach(string field in new[]{"_scrollMaterials","_scrollCounts"})typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lease,typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner));
        typeof(EnemyMeshResources).GetField("_scrollRenderers",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lease,new Renderer[]{ca});typeof(EnemyMeshResources).GetField("_scrollers",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lease,new[]{cs});lease.EnsureRetained();
        cs.materialIndex=3;start=UObject.created.Count;Check(ExplicitScrollingUvs.Prefix(cs)&&UObject.created.Count==start&&ca.sharedMaterials[1]==a.sharedMaterials[1],"Mutable invalid clone scroller rejected before any allocation/assignment");cs.materialIndex=1;
        ca.failOnce=true;start=UObject.created.Count;Check(ExplicitScrollingUvs.Prefix(cs),"Failed clone private commit falls back before phase mutation");CheckNewAssetsDestroyed(start);Check(((Vector2)phase.GetValue(cs)).x==3&&ca.sharedMaterials[1]==a.sharedMaterials[1],"Failure preserves phase/material and source");
        ca.failCount=2;start=UObject.created.Count;Check(ExplicitScrollingUvs.Prefix(cs),"Double setter failure falls back before phase update");Check(UObject.created.GetRange(start,UObject.created.Count-start).TrueForAll(x=>!(x is Material)||!x.destroyed),"Double setter failure retains uncertain copies");Check(((Vector2)phase.GetValue(cs)).x==3,"Double setter failure preserves phase");
        Check(!ExplicitScrollingUvs.Prefix(cs),"Successful clone initializes private set");var privateMat=ca.sharedMaterials[1];Check(privateMat!=a.sharedMaterials[1]&&privateMat.offsets["_DetailTex"].x==3.5f&&a.sharedMaterials[1].offsets["_DetailTex"].x==-.25f,"Source and clone different phases never crosswrite");
        start=UObject.created.Count;ExplicitScrollingUvs.Prefix(cs);Check(UObject.created.Count==start&&ca.sharedMaterials[1]==privateMat,"Repeated scrolling no allocation");
        var grand=Avatar(out var ga,out var gb);ga.sharedMaterials=(Material[])ca.sharedMaterials.Clone();var gs=ga.gameObject.AddComponent<ScrollingUVs>();gs.materialIndex=1;gs.textureName="_DetailTex";gs.uvAnimationRate=new Vector2(3,0);phase.SetValue(gs,new Vector2(1,0));var grandOwner=SerializedClone(lease,grand);
        foreach(string field in new[]{"_scrollMaterials","_scrollCounts"})typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(grandOwner,typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lease));
        typeof(EnemyMeshResources).GetField("_targets",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(grandOwner,new Renderer[]{ga});typeof(EnemyMeshResources).GetField("_scrollRenderers",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(grandOwner,new Renderer[]{ga});typeof(EnemyMeshResources).GetField("_scrollers",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(grandOwner,new[]{gs});grandOwner.EnsureRetained();
        Check(!ExplicitScrollingUvs.Prefix(gs),"Grandclone inherits privatized parent's serialized provenance");var grandMat=ga.sharedMaterials[1];Check(grandMat!=privateMat&&grandMat.offsets["_DetailTex"].x==1.75f&&privateMat.offsets["_DetailTex"].x==4f,"Three generations retain independent offsets/materials");
        var reverse=Avatar(out var ra,out var rb);ra.sharedMaterials=(Material[])a.sharedMaterials.Clone();var reverseOwner=SerializedClone(owner,reverse);
        foreach(string field in new[]{"_scrollMaterials","_scrollCounts"})typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(reverseOwner,typeof(EnemyMeshResources).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner));
        typeof(EnemyMeshResources).GetField("_targets",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(reverseOwner,new Renderer[]{ra});typeof(EnemyMeshResources).GetField("_scrollRenderers",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(reverseOwner,new Renderer[]{ra});reverseOwner.EnsureRetained();var reverseMat=reverseOwner.ScrollingMaterials(ra,true)[1];reverseOwner.Release();Check(!reverseMat.destroyed&&owner.Applied,"Clone-first release leaves source lease valid; explicit clones remain until final lease release");
        a.sharedMaterials=new[]{new Material(),new Material()};var old=(Vector2)phase.GetValue(sc);Check(ExplicitScrollingUvs.Prefix(sc)&&((Vector2)phase.GetValue(sc)).x==old.x,"Unknown material references not adopted or phase advanced");
        owner.Release();Check(!privateMat.destroyed,"Clone private resources retained under shared lease");lease.Release();Check(!privateMat.destroyed&&!grandMat.destroyed,"Grandclone retains all explicit lease allocations after ancestors release");grandOwner.Release();Check(privateMat.destroyed&&reverseMat.destroyed&&grandMat.destroyed&&!native[0].destroyed,"Last lease destroys private clones but never prefab materials");
        var fail=Avatar(out a,out b);var bad=a.gameObject.AddComponent<ScrollingUVs>();bad.materialIndex=2;start=UObject.created.Count;Check(!ExplicitEnemyMeshSwap.Apply("bad",fail,new[]{assignment}),"Unsupported scroller slot rejects before mutation");CheckNewAssetsDestroyed(start);
        var textureFail=Avatar(out a,out b);var original=a.sharedMaterials;start=UObject.created.Count;
        var missingTexture=EnemyRendererMesh.WithNativeMaterialSlots("a","two.glb",new[]{new EnemyRendererMaterial(0,0),new EnemyRendererMaterial(1,1,"does-not-exist.png")});Check(!ExplicitEnemyMeshSwap.Apply("missing",textureFail,new[]{missingTexture})&&ReferenceEquals(a.sharedMaterials,original),"Missing second-slot texture rejects atomic set");CheckNewAssetsDestroyed(start);
        string error;Check(!ExplicitEnemyMeshSwap.ValidateAssignments(new[]{EnemyRendererMesh.WithNativeMaterialSlots("a","x",new[]{new EnemyRendererMaterial(0,0),new EnemyRendererMaterial(0,1)})},out error),"Duplicate primitive mapping rejected");
    }
    static void SingleSlotScrollingChecks()
    {
        var cel=Avatar(out var a,out var b);
        var aMesh=a.sharedMesh;var bMesh=b.sharedMesh;var aMaterials=a.sharedMaterials;var bMaterials=b.sharedMaterials;
        var aBones=a.bones;var bBones=b.bones;
        var scroller=b.gameObject.AddComponent<ScrollingUVs>();scroller.materialIndex=1;
        int start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("cubeA-default",cel,Plan()),"Default single-slot cubeA native index1 rejects complete transaction");
        Check(Plugin.Log.lastWarning.Contains("WithNativeMaterialSlots"),"Incompatible single-slot diagnostic directs authors to native slot opt-in");
        Check(a.sharedMesh==aMesh&&b.sharedMesh==bMesh&&ReferenceEquals(a.sharedMaterials,aMaterials)&&ReferenceEquals(b.sharedMaterials,bMaterials)
            &&ReferenceEquals(a.bones,aBones)&&ReferenceEquals(b.bones,bBones)&&a.localBounds.value==31&&b.localBounds.value==31,
            "Late incompatible single-slot selection leaves all earlier and current renderer state unchanged");
        Check(cel.GetComponent<EnemyMeshResources>()==null,"Single-slot scroller preflight failure creates no ownership lease");
        CheckNewAssetsDestroyed(start);
        scroller.materialIndex=0;scroller.textureName="missing";start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("bad-property",cel,Plan()),"Ordinary single-slot validates native scrolling property too");
        CheckNewAssetsDestroyed(start);
        scroller.textureName="_MainTex";
        Check(ExplicitEnemyMeshSwap.Apply("compatible-single",cel,Plan()),"Compatible ordinary slot0 native scroller applies");
        Check(b.sharedMaterials.Length==1&&!ExplicitScrollingUvs.Prefix(scroller),"Compatible single-slot scroller uses owned material prefix");
        var sourceMaterial=b.sharedMaterials[0];
        var clone=ScrollClone(cel.GetComponent<EnemyMeshResources>(),b,out var cloneCel,out var cloneRenderer,out var cloneScroller);
        cloneScroller.materialIndex=0;
        typeof(ScrollingUVs).GetField("uvOffset",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(cloneScroller,new Vector2(3,0));
        Check(!ExplicitScrollingUvs.Prefix(cloneScroller)&&cloneRenderer.sharedMaterials.Length==1&&cloneRenderer.sharedMaterials[0]!=sourceMaterial,
            "Compatible single-slot clone privatizes its own scrolling material");
        Check(sourceMaterial.offsets["_MainTex"].x==.25f&&cloneRenderer.sharedMaterials[0].offsets["_MainTex"].x==3.25f,
            "Single-slot clone phase never writes the source material");
        cel.GetComponent<EnemyMeshResources>().Release();clone.Release();
        Check(!aMesh.destroyed&&!bMesh.destroyed&&!aMaterials[0].destroyed&&!bMaterials[1].destroyed,"Single-slot owned cleanup preserves native assets");
    }
    static object Field(EnemyMeshResources owner,string name)=>typeof(EnemyMeshResources).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    static void LegacyChecks()
    {
        var cel=Avatar(out var a,out var b);var native=a.sharedMaterials;var oldMesh=a.sharedMesh;
        LegacyVisualResources.Materials(cel,a,m=>m.color=new Color(.7f));
        var owner=cel.GetComponent<EnemyMeshResources>();var tinted=a.sharedMaterials;var leaseId=Field(owner,"_leaseId");
        Check(owner.VisualResourcesOnly&&owner.Applied&&owner.ValidLease(),"Tint acquires valid resource-only owner");
        Check(tinted.Length==native.Length&&native[0].color.r==.25f&&tinted[0].color.r==.7f,"Tint prepares explicit copies without native mutation");
        var clone=Avatar(out var ca,out var cb);ca.sharedMaterials=(Material[])tinted.Clone();var inherited=SerializedClone(owner,clone);inherited.SetTargets(new Renderer[]{ca});Check(inherited.EnsureRetained(),"Inherited tint owner acquires same lease");
        LegacyVisualResources.Materials(clone,ca,m=>m.color=new Color(.2f));
        Check(tinted[0].color.r==.7f&&ca.sharedMaterials[0]!=tinted[0]&&ca.sharedMaterials[0].color.r==.2f,"Retained clone tint uses new private copies");
        int start=UObject.created.Count;b.failOnce=true;
        Check(!ExplicitEnemyMeshSwap.Apply("tint-fail",cel,Plan()),"Tint-only owner does not bypass failing strict plan");
        Check(owner.VisualResourcesOnly&&owner.Applied&&owner.ValidLease()&&Equals(leaseId,Field(owner,"_leaseId"))&&ReferenceEquals(a.sharedMaterials,tinted)&&a.sharedMesh==oldMesh&&!owner.Owns(b),"Existing lease/targets/flag/renderer rollback survives failed append");CheckNewAssetsDestroyed(start);
        Check(ExplicitEnemyMeshSwap.Apply("tint-success",cel,Plan()),"Tint-only owner permits real explicit plan");
        Check(!owner.VisualResourcesOnly&&Equals(leaseId,Field(owner,"_leaseId"))&&owner.Owns(b),"Full commit clears only resource-only flag, same lease");
        var explicitMesh=a.sharedMesh;owner.Release();Check(!explicitMesh.destroyed&&!tinted[0].destroyed,"Other clone retains both old and appended allocations");inherited.Release();Check(explicitMesh.destroyed&&tinted[0].destroyed&&!native[0].destroyed,"Final owner releases union without native resources");

        cel=Avatar(out a,out b);native=a.sharedMaterials;var mesh=new Mesh{name="legacy"};LegacyVisualResources.Mesh(cel,a,mesh);owner=cel.GetComponent<EnemyMeshResources>();
        Check(a.sharedMesh==mesh&&owner.VisualResourcesOnly&&owner.ValidLease(),"Permissive decoded mesh registered before rebind");
        string file=System.IO.Path.GetTempFileName();System.IO.File.WriteAllBytes(file,new byte[]{1});
        Texture2D.loadResult=false;start=UObject.created.Count;LegacyVisualResources.OptionalTexture("row",cel,a,file);
        Check(a.sharedMesh==mesh&&ReferenceEquals(a.sharedMaterials,native),"LoadImage false keeps applied mesh and prior materials");CheckNewAssetsDestroyed(start);
        Texture2D.loadResult=true;LegacyVisualResources.OptionalTexture("row",cel,a,file);var textured=a.sharedMaterials;var texture=(Texture2D)textured[0].textures["_MainTex"];
        Check(texture!=null&&textured.Length==native.Length&&textured[0]!=native[0]&&!native[0].textures.ContainsKey("_MainTex"),"Optional texture copies all slots and preserves native appearance");
        a.failOnce=true;start=UObject.created.Count;LegacyVisualResources.OptionalTexture("row",cel,a,file);
        Check(ReferenceEquals(a.sharedMaterials,textured)&&a.sharedMesh==mesh&&owner.ValidLease(),"Optional texture setter failure keeps mesh and prior appearance");CheckNewAssetsDestroyed(start);
        owner.Release();Check(mesh.destroyed&&texture.destroyed&&textured[0].destroyed&&!native[0].destroyed,"Singular mesh/PNG/materials disposed by last owner");

        cel=Avatar(out a,out b);a.sharedMaterials=new Material[]{a.sharedMaterials[0],null};LegacyVisualResources.Materials(cel,a,m=>m.color=new Color(.3f));
        Check(a.sharedMaterials.Length==2&&a.sharedMaterials[1]==null,"Null slots preserved without null material allocations");cel.GetComponent<EnemyMeshResources>().Release();
        cel=Avatar(out a,out b);LegacyVisualResources.Materials(cel,a,m=>{});owner=cel.GetComponent<EnemyMeshResources>();var prior=a.sharedMaterials;
        start=UObject.created.Count;a.failCount=2;try{LegacyVisualResources.Materials(cel,a,m=>{});throw new Exception("expected failure");}catch(Exception e){Check(e.Message.Contains("injected"),"Double setter failure propagated");}
        var uncertain=UObject.created.GetRange(start,UObject.created.Count-start).FindAll(x=>x is Material);
        Check(uncertain.Count==2&&uncertain.TrueForAll(x=>!x.destroyed)&&owner.ValidLease()&&owner.VisualResourcesOnly,"Uncertain rollback retains new assets in existing lease");
        owner.Release();Check(uncertain.TrueForAll(x=>x.destroyed)&&prior[0].destroyed,"Uncertain resources release only at final owner");
        cel=Avatar(out a,out b);owner=cel.gameObject.AddComponent<EnemyMeshResources>();owner.VisualResourcesOnly=true;
        int calls=RuntimeGltfMeshLoader.calls;Check(!ExplicitEnemyMeshSwap.Apply("fake",cel,Plan())&&RuntimeGltfMeshLoader.calls==calls,"Resource-only flag without real lease never authorizes plan");
        cel=Avatar(out a,out b);a.failCount=2;start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("uncertain-fresh",cel,Plan()),"Fresh explicit rollback uncertainty is a failed plan");
        owner=cel.GetComponent<EnemyMeshResources>();var freshAssets=UObject.created.GetRange(start,UObject.created.Count-start).FindAll(x=>x is Mesh||x is Material);
        Check(owner!=null&&owner.ValidLease()&&owner.VisualResourcesOnly&&!owner.Applied&&freshAssets.TrueForAll(x=>!x.destroyed),"Fresh incomplete rollback keeps real owner, never claims completed plan");
        owner.Release();Check(freshAssets.TrueForAll(x=>x.destroyed),"Fresh uncertain assets wait for owner release");
        cel=Avatar(out a,out b);var sc=a.gameObject.AddComponent<ScrollingUVs>();sc.materialIndex=1;LegacyVisualResources.Materials(cel,a,m=>{});owner=cel.GetComponent<EnemyMeshResources>();
        var metadata=new Dictionary<string,object>();foreach(string field in new[]{"_targets","_scrollRenderers","_scrollMaterials","_scrollCounts","_scrollers"})metadata[field]=Field(owner,field);
        var extra=new Material();var batch=owner.Append(new UObject[]{extra},new Renderer[]{b},false);owner.AddScrollingTarget(a,new Material[]{extra},new ScrollingUVs[]{sc});owner.Applied=false;owner.VisualResourcesOnly=false;batch.Rollback(true);
        foreach(var item in metadata)Check(ReferenceEquals(item.Value,Field(owner,item.Key)),"Rollback restores prior target/scroll serialized metadata: "+item.Key);
        Check(owner.Applied&&owner.VisualResourcesOnly&&extra.destroyed&&!owner.Owns(b)&&!ExplicitScrollingUvs.Prefix(sc),"Rollback restores resource-only completion state and private provenance");owner.Release();
        LegacyScrollingChecks();
    }
    static EnemyMeshResources ScrollClone(EnemyMeshResources source,SkinnedMeshRenderer sourceRenderer,out CharacterEventListener cel,out SkinnedMeshRenderer renderer,out ScrollingUVs scroller)
    {
        cel=Avatar(out renderer,out var other);renderer.sharedMaterials=(Material[])sourceRenderer.sharedMaterials.Clone();scroller=renderer.gameObject.AddComponent<ScrollingUVs>();scroller.materialIndex=1;
        var owner=SerializedClone(source,cel);owner.SetTargets(new Renderer[]{renderer});
        foreach(var pair in new[]{new KeyValuePair<string,object>("_scrollRenderers",new Renderer[]{renderer}),new KeyValuePair<string,object>("_scrollMaterials",Field(source,"_scrollMaterials")),new KeyValuePair<string,object>("_scrollCounts",Field(source,"_scrollCounts")),new KeyValuePair<string,object>("_scrollers",new ScrollingUVs[]{scroller})})typeof(EnemyMeshResources).GetField(pair.Key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,pair.Value);
        return owner;
    }
    static void LegacyScrollingChecks()
    {
        var cel=Avatar(out var a,out var b);var sc=a.gameObject.AddComponent<ScrollingUVs>();sc.materialIndex=1;var phase=typeof(ScrollingUVs).GetField("uvOffset",BindingFlags.NonPublic|BindingFlags.Instance);
        LegacyVisualResources.Materials(cel,a,m=>{});var owner=cel.GetComponent<EnemyMeshResources>();var original=a.sharedMaterials;
        Check(!ExplicitScrollingUvs.Prefix(sc)&&((Vector2)phase.GetValue(sc)).x==.25f,"Legacy registered native scroller advances enabled phase");
        var clone=ScrollClone(owner,a,out var cloneCel,out var ca,out var cs);ca.enabled=false;int start=UObject.created.Count;
        Check(!ExplicitScrollingUvs.Prefix(cs)&&UObject.created.Count==start&&((Vector2)phase.GetValue(cs)).x==.25f&&ca.sharedMaterials[0]==original[0],"Disabled clone accumulates phase without private allocation");
        ca.enabled=true;Check(!ExplicitScrollingUvs.Prefix(cs)&&ca.sharedMaterials[0]!=original[0],"Enabled clone gets explicit private materials");
        var broken=ScrollClone(owner,a,out var brokenCel,out var ba,out var bs);ba.assignThenFailCount=2;start=UObject.created.Count;
        Check(ExplicitScrollingUvs.Prefix(bs),"Live assigned scroller copy with failed restoration falls back");
        var liveCopies=ba.sharedMaterials;Check(liveCopies[0]!=original[0]&&!liveCopies[0].destroyed&&((Vector2)phase.GetValue(bs)).x==0,"Still-assigned copies remain alive and phase untouched after uncertain rollback");
        Check(broken.ScrollingMaterials(ba,false)==null,"Uncertain assigned materials cannot masquerade as accepted private provenance");
        broken.Release();Check(!liveCopies[0].destroyed,"Source lease retains uncertain scrolling copies after clone release");
        var privateMat=ca.sharedMaterials[0];var grand=ScrollClone(clone,ca,out var grandCel,out var ga,out var gs);
        Check(!ExplicitScrollingUvs.Prefix(gs)&&ga.sharedMaterials[0]!=privateMat&&ga.sharedMaterials[0]!=original[0],"Grandclone privatizes from current serialized provenance");
        var grandMat=ga.sharedMaterials[0];float before=((Vector2)phase.GetValue(gs)).x;gs.materialIndex=8;Check(ExplicitScrollingUvs.Prefix(gs)&&((Vector2)phase.GetValue(gs)).x==before,"Invalid index falls back before phase");gs.materialIndex=1;gs.textureName="missing";Check(ExplicitScrollingUvs.Prefix(gs)&&((Vector2)phase.GetValue(gs)).x==before,"Invalid property falls back before phase");
        owner.Release();clone.Release();Check(!original[0].destroyed&&!privateMat.destroyed&&!grandMat.destroyed,"Grandclone keeps all lease material allocations");grand.Release();Check(original[0].destroyed&&privateMat.destroyed&&grandMat.destroyed&&liveCopies[0].destroyed,"Final grandclone releases complete private and uncertain material union");
        cel=Avatar(out a,out b);sc=a.gameObject.AddComponent<ScrollingUVs>();sc.materialIndex=8;LegacyVisualResources.Materials(cel,a,m=>{});before=((Vector2)phase.GetValue(sc)).x;Check(ExplicitScrollingUvs.Prefix(sc)&&((Vector2)phase.GetValue(sc)).x==before,"Unsupported legacy layout never enrolls scroller or advances its phase");cel.GetComponent<EnemyMeshResources>().Release();
    }
    static void StaticRendererChecks()
    {
        var cel=Avatar(out var a,out var b);var rigid=StaticPart(cel.gameObject,"rigid",out var filter);var nativeMesh=filter.sharedMesh;var nativeMaterials=rigid.sharedMaterials;
        var assignment=EnemyRendererMesh.ForStaticRenderer("rigid","rigid.glb",null,true);int calls=RuntimeGltfMeshLoader.staticCalls;
        Check(ExplicitEnemyMeshSwap.Apply("rigid",cel,new[]{assignment},(r,m)=>m.color=new Color(.7f)),"Static MeshRenderer assignment applies");
        Check(RuntimeGltfMeshLoader.staticCalls==calls+1&&filter.sharedMesh!=nativeMesh&&filter.sharedMesh.name=="rigid.glb","Static GLB is loaded and assigned through MeshFilter");
        Check(rigid.sharedMaterials.Length==1&&rigid.sharedMaterials[0]!=nativeMaterials[0]&&!rigid.sharedMaterials[0].emission&&nativeMaterials[0].emission,"Static material is private and honors emission opt-out");
        var owner=cel.GetComponent<EnemyMeshResources>();Check(owner.Owns(rigid)&&owner.ValidLease(),"Static renderer is retained by the explicit resource lease");var customMesh=filter.sharedMesh;var customMaterial=rigid.sharedMaterials[0];owner.Release();
        Check(customMesh.destroyed&&customMaterial.destroyed&&!nativeMesh.destroyed&&!nativeMaterials[0].destroyed,"Static resources release without adopting native mesh/material");

        cel=Avatar(out a,out b);rigid=StaticPart(cel.gameObject,"rigid",out filter);nativeMesh=filter.sharedMesh;nativeMaterials=rigid.sharedMaterials;var nativeSkinned=a.sharedMesh;var skinnedMaterials=a.sharedMaterials;rigid.failOnce=true;int start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("rigid-rollback",cel,new[]{new EnemyRendererMesh("a","a.glb"),EnemyRendererMesh.ForStaticRenderer("rigid","rigid.glb")}),"Static material commit failure rejects the complete mixed transaction");
        Check(a.sharedMesh==nativeSkinned&&ReferenceEquals(a.sharedMaterials,skinnedMaterials)&&filter.sharedMesh==nativeMesh&&ReferenceEquals(rigid.sharedMaterials,nativeMaterials),"Mixed rollback restores skinned and static native state");CheckNewAssetsDestroyed(start);

        cel=Avatar(out a,out b);start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("wrong-static-type",cel,new[]{EnemyRendererMesh.ForStaticRenderer("a","rigid.glb")}),"Static descriptor rejects a skinned target path");
        Check(a.sharedMesh.name=="native","Wrong static target keeps native mesh");CheckNewAssetsDestroyed(start);

        cel=Avatar(out a,out b);rigid=StaticPart(cel.gameObject,"rigid",out filter);rigid.gameObject.AddComponent<SkinnedMeshRenderer>();start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("ambiguous-static-type",cel,new[]{EnemyRendererMesh.ForStaticRenderer("rigid","rigid.glb")}),"Static descriptor rejects a transform that also has a skinned renderer");
        Check(filter.sharedMesh.name=="native static","Ambiguous static target keeps native mesh");CheckNewAssetsDestroyed(start);
    }
    static void Main()
    {
        var cel=Avatar(out var a,out var b);var am=a.sharedMesh;var bm=b.sharedMesh;var ab=a.bones;var bb=b.bones;var amat=a.sharedMaterials;var bmat=b.sharedMaterials;
        int start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("row",cel,Plan(),(r,m)=>{m.color=new Color(.8f);if(r==b)throw new Exception("material preparation failed");}),"Material preparation rejects whole set");
        Check(a.sharedMesh==am&&b.sharedMesh==bm&&ReferenceEquals(a.sharedMaterials,amat)&&ReferenceEquals(b.sharedMaterials,bmat)&&ReferenceEquals(a.bones,ab)&&ReferenceEquals(b.bones,bb),"Preflight leaves all original references intact");
        Check(cel.GetComponent<EnemyMeshResources>()==null&&amat[0].color.r==.25f&&amat[0].emission,"Preflight never acquires lease or edits native materials");CheckNewAssetsDestroyed(start);
        start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("row",cel,Plan("bad.glb")),"Late decode failure rejects earlier prepared target");CheckNewAssetsDestroyed(start);
        b.failOnce=true;start=UObject.created.Count;
        Check(!ExplicitEnemyMeshSwap.Apply("row",cel,Plan(),(r,m)=>m.color=new Color(.8f)),"Late commit failure rolls back");
        Check(a.sharedMesh==am&&b.sharedMesh==bm&&ReferenceEquals(a.sharedMaterials,amat)&&ReferenceEquals(b.sharedMaterials,bmat)&&ReferenceEquals(a.bones,ab)&&ReferenceEquals(b.bones,bb),"Commit rollback restores whole set");CheckNewAssetsDestroyed(start);
        int prepares=0;
        Check(ExplicitEnemyMeshSwap.Apply("row",cel,Plan(),(r,m)=>{Check(a.sharedMesh==am&&b.sharedMesh==bm,"Both originals remain until preparation complete");m.color=new Color(.8f);prepares++;}),"Prepared transaction succeeds");
        Check(prepares==2&&a.sharedMaterials.Length==1&&b.sharedMaterials.Length==1&&a.sharedMaterials[0].color.r==.8f&&!a.sharedMaterials[0].emission&&b.sharedMaterials[0].emission,"Private settings/emission and single primitive material applied");
        Check(amat[0].color.r==.25f&&amat[0].emission&&bmat[0].color.r==.25f,"Native material state unchanged");
        Check(a.bones[0]==ab[0]&&b.bones[0]==bb[0]&&a.transform.pose==73&&cel.transform.pose==73&&a.localBounds.value==31,"Bone identities, pose and native bounds preserved");
        var owner=cel.GetComponent<EnemyMeshResources>();var asset=a.sharedMesh;int calls=RuntimeGltfMeshLoader.calls;
        Check(owner.OwnsExplicitMesh(a),"Portrait eligibility accepts actual leased explicit mesh");
        owner.VisualResourcesOnly=true;Check(!owner.OwnsExplicitMesh(a),"Portrait eligibility rejects visual-only owner even with renderer membership");owner.VisualResourcesOnly=false;
        a.sharedMesh=am;Check(!owner.OwnsExplicitMesh(a),"Portrait eligibility rejects native mesh substituted on owned renderer");a.sharedMesh=asset;
        owner.Applied=false;Check(!owner.OwnsExplicitMesh(a),"Portrait eligibility rejects unapplied owner");owner.Applied=true;
        Check(ExplicitEnemyMeshSwap.Apply("row",cel,Plan(),(r,m)=>throw new Exception("must not reprepare"))&&RuntimeGltfMeshLoader.calls==calls,"Applied owner skips all loading/material work");
        var copy=Avatar(out var ca,out var cb);var retained=SerializedClone(owner,copy);
        Check(ExplicitEnemyMeshSwap.Apply("row",copy,Plan(),(r,m)=>throw new Exception("must not reprepare"))&&RuntimeGltfMeshLoader.calls==calls,"Serialized live-clone lease retained before load or tint");
        var stale=Avatar(out var sa,out var sb);var staleOwner=SerializedClone(owner,stale); // Deliberately never retained before every real owner releases.
        owner.Release();Check(!asset.destroyed,"Source disposal retains clone assets");retained.Release();Check(asset.destroyed,"Last clone disposal destroys owned assets");retained.Release();Check(asset.destroyed,"Repeated release harmless");
        var missing=Avatar(out ca,out cb);missing.gameObject.AddComponent<EnemyMeshResources>();Check(!ExplicitEnemyMeshSwap.Apply("row",missing,Plan()),"Component presence without Applied is not success");
        Check(!ExplicitEnemyMeshSwap.Apply("row",stale,Plan())&&!staleOwner.Applied&&RuntimeGltfMeshLoader.calls==calls,"Nonzero serialized lease whose source was disposed rejects without reload");
        var inactive=Avatar(out ca,out cb);Check(ExplicitEnemyMeshSwap.Apply("row",inactive,Plan()),"Inactive owner fixture prepared");var inactiveOwner=inactive.GetComponent<EnemyMeshResources>();var inactiveAsset=ca.sharedMesh;UObject.Destroy(inactiveOwner);EnemyMeshResources.PruneDestroyedOwners();Check(inactiveAsset.destroyed,"Never-active destroyed owner pruned without callback");
        MultiSlotChecks();
        SingleSlotScrollingChecks();
        LegacyChecks();
        StaticRendererChecks();
        Console.WriteLine("PASS "+checks+" actual transaction/lease assertions (Unity boundary stand-ins; no live proof).");
    }
}
