using System;
using System.Collections.Generic;
using System.Linq;
using FTKModFramework.Core;
using UnityEngine;

// Executes production plan creation, target resolution and transaction dispatch. Unity objects,
// the existing strict loader/transaction and the existing lease implementation are stand-ins.
class Program
{
    static int checks;
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    static PlayerRendererMesh[] Body(){return new[]{new PlayerRendererMesh("body","body.glb"),new PlayerRendererMesh("hair","hair.glb")};}
    static PlayerApparelMesh[] Clothes(){return new[]{new PlayerApparelMesh("armor(Clone)","nativeArmor","armor.glb","armor.png"),new PlayerApparelMesh("boots(Clone)","nativeBoots","boots.glb")};}
    static PlayerMeshPlan Plan()
    {PlayerMeshPlan plan;string error;Check(PlayerMeshPlan.TryCreate(Body(),Clothes(),out plan,out error),"plan creation");return plan;}
    static CharacterEventListener Avatar(params Transform[] extras)
    {
        var avatar=new CharacterEventListener();avatar.transform.children.Add(Target("body","nativeBody"));avatar.transform.children.Add(Target("hair","nativeHair"));
        avatar.transform.children.AddRange(extras);return avatar;
    }
    static Transform Target(string path,string mesh){return new Transform{path=path,renderers=new[]{new SkinnedMeshRenderer{sharedMesh=mesh==null?null:new Mesh{name=mesh}}}};}
    static void Rejected(PlayerMeshPlan plan,CharacterEventListener avatar,string name)
    {ExplicitEnemyMeshSwap.calls=0;Check(!plan.Apply("test",avatar),name);Check(ExplicitEnemyMeshSwap.calls==0,name+" no transaction");}
    static void Main()
    {
        var plan=Plan();var empty=Avatar();ExplicitEnemyMeshSwap.calls=0;
        Check(plan.Apply("test",empty),"absent apparel keeps body");Check(ExplicitEnemyMeshSwap.calls==1,"one body transaction");Check(ExplicitEnemyMeshSwap.last.Length==2,"both required paths kept");
        var full=Avatar(Target("armor(Clone)","nativeArmor"),Target("boots(Clone)","nativeBoots"));ExplicitEnemyMeshSwap.calls=0;
        Check(plan.Apply("test",full),"present apparel accepted");Check(ExplicitEnemyMeshSwap.calls==1 && ExplicitEnemyMeshSwap.last.Length==4,"one combined body+apparel transaction");
        Check(ExplicitEnemyMeshSwap.last[2].TextureFileName=="armor.png","conditional texture forwarded");
        var bootsOnly=Avatar(Target("boots(Clone)","nativeBoots"));ExplicitEnemyMeshSwap.calls=0;
        Check(plan.Apply("test",bootsOnly) && ExplicitEnemyMeshSwap.last.Length==3,"independent absent armor still styles boots");
        Rejected(plan,Avatar(Target("armor(Clone)","differentNative")),"wrong native identity");
        Rejected(plan,Avatar(Target("armor(Clone)","NativeArmor")),"case sensitive native identity");
        Rejected(plan,Avatar(Target("armor(Clone)",null)),"null native mesh");
        Rejected(plan,Avatar(new Transform{path="armor(Clone)"}),"present transform without SMR is not absent");
        Rejected(plan,Avatar(Target("armor(Clone)","nativeArmor"),Target("armor(Clone)","nativeArmor")),"ambiguous transform path");
        var multi=Target("armor(Clone)","nativeArmor");multi.renderers=new[]{multi.renderers[0],multi.renderers[0]};
        Rejected(plan,Avatar(multi),"multiple SMRs at path");
        var missing=Avatar();missing.transform.children.RemoveAt(1);Rejected(plan,missing,"missing required hair");
        var noBody=new CharacterEventListener();Rejected(plan,noBody,"apparel never makes required body optional");
        // Input arrays may be reused by callers; the registration keeps its own descriptor snapshot.
        var inputBody=Body();var inputClothes=Clothes();PlayerMeshPlan snapshot;string error;
        Check(PlayerMeshPlan.TryCreate(inputBody,inputClothes,out snapshot,out error),"snapshot creation");inputBody[0]=new PlayerRendererMesh("other","other.glb");inputClothes[0]=new PlayerApparelMesh("other","other","other.glb");
        Check(snapshot.Apply("test",full) && ExplicitEnemyMeshSwap.last.Length==4 && ExplicitEnemyMeshSwap.last[0].RendererPath=="body","snapshot immune to array replacement");
        PlayerMeshPlan invalid;
        Check(!PlayerMeshPlan.TryCreate(new PlayerRendererMesh[0],Clothes(),out invalid,out error),"conditional only rejected");
        Check(!PlayerMeshPlan.TryCreate(null,Clothes(),out invalid,out error),"null body rejected");
        Check(!PlayerMeshPlan.TryCreate(Body(),null,out invalid,out error),"null apparel array rejected");
        Check(!PlayerMeshPlan.TryCreate(Body(),new[]{new PlayerApparelMesh("armor(Clone)"," ","armor.glb")},out invalid,out error),"blank native identity rejected");
        Check(!PlayerMeshPlan.TryCreate(Body(),new[]{new PlayerApparelMesh("body","nativeBody","armor.glb")},out invalid,out error),"duplicate required conditional path rejected");
        // A successfully dressed clone no longer has native mesh names: repeated application must
        // check Applied AND retention, without resolving native identities or starting a second swap.
        var dressed=Avatar(Target("armor(Clone)","customAlreadyApplied"));var lease=new EnemyMeshResources{Applied=true,retain=true};dressed.owner=lease;
        ExplicitEnemyMeshSwap.calls=0;Check(plan.Apply("test",dressed),"applied lease retained before identity checks");Check(lease.retains==1 && ExplicitEnemyMeshSwap.calls==0,"no second lease transaction");
        lease.retain=false;Check(!plan.Apply("test",dressed),"failed retention is not success");Check(ExplicitEnemyMeshSwap.calls==0,"missing lease cannot reapply");
        lease.Applied=false;lease.retains=0;Check(!plan.Apply("test",dressed) && lease.retains==0,"owner presence alone not success");
        var resourceOnly = new EnemyMeshResources { Applied=true,retain=true,VisualResourcesOnly=true }; full.owner=resourceOnly;
        ExplicitEnemyMeshSwap.calls=0;Check(plan.Apply("test",full)&&ExplicitEnemyMeshSwap.calls==1,"resource-only lease must perform actual player plan");
        resourceOnly.retain=false;ExplicitEnemyMeshSwap.calls=0;Check(!plan.Apply("test",full)&&ExplicitEnemyMeshSwap.calls==0,"invalid resource-only lease rejected");full.owner=null;
        // Strict decoding failure is propagated from the single combined transaction. Decoder and
        // rollback implementation itself are not tested by this stand-in.
        ExplicitEnemyMeshSwap.result=false;ExplicitEnemyMeshSwap.calls=0;
        Check(!plan.Apply("test",full) && ExplicitEnemyMeshSwap.calls==1 && ExplicitEnemyMeshSwap.last.Length==4,"combined strict failure propagated");
        Console.WriteLine("PASS: "+checks+" player plan assertions (Unity/strict transaction/lease stand-ins; no live gate).");
    }
}
namespace UnityEngine
{
    public class Mesh{public string name;}
    public class SkinnedMeshRenderer{public Mesh sharedMesh;}
    public class Transform
    {
        public string path=".";public List<Transform> children=new List<Transform>();public SkinnedMeshRenderer[] renderers=new SkinnedMeshRenderer[0];
        public T[] GetComponents<T>(){return renderers.Cast<T>().ToArray();}
        public T[] GetComponentsInChildren<T>(bool inactive){return new[]{this}.Concat(children).Cast<T>().ToArray();}
    }
}
public class CharacterEventListener
{
    public Transform transform=new Transform();public EnemyMeshResources owner;
    public T GetComponent<T>()where T:class{return owner as T;}
}
namespace FTKModFramework.Core
{
    public class EnemyMeshResources{public bool Applied,retain,VisualResourcesOnly;public bool ValidLease(){return EnsureRetained();}public int retains;public bool EnsureRetained(){retains++;return retain;}}
    static class Plugin{internal static readonly LogStub Log=new LogStub();}
    class LogStub{public void LogInfo(string text){}public void LogWarning(string text){}}
    static class ExplicitEnemyMeshSwap
    {
        public static int calls;public static EnemyRendererMesh[] last;public static bool result=true;
        public static bool ValidateAssignments(EnemyRendererMesh[] items,out string error)
        {
            error=null;var paths=new HashSet<string>();foreach(var item in items)if(item==null || !paths.Add(item.RendererPath)){error="duplicate/null";return false;}
            return items.Length>0;
        }
        public static string RelativePath(Transform root,Transform child){return child.path;}
        public static bool Apply(string id,CharacterEventListener avatar,EnemyRendererMesh[] items){calls++;last=items;return result;}
    }
}
