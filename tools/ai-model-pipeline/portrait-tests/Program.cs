using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core;
using UnityEngine;
using GridEditor;

namespace UnityEngine
{
    public class Component { public GameObject gameObject; public Transform transform { get{return gameObject.transform;} } public int GetInstanceID(){return GetHashCode();} }
    public class Texture2D {}
    public struct Scene {public bool valid; public bool IsValid(){return valid;} }
    public class GameObject
    {
        public Transform transform; public Scene scene;public CharacterEventListener cel;
        public GameObject(string name,bool live=false){transform=new Transform{name=name,gameObject=this};scene=new Scene{valid=live};}
        public T GetComponent<T>()where T:class{return cel as T;}
        public int GetInstanceID(){return GetHashCode();}
    }
    public class Transform:Component
    {
        public string name;public Transform parent;public List<Transform> children=new List<Transform>();
        public Transform Add(string name){var node=new GameObject(name).transform;node.parent=this;children.Add(node);return node;}
        public T[] GetComponentsInChildren<T>(bool inactive)where T:class
        {var all=new List<T>();all.Add(this as T);foreach(var child in children)all.AddRange(child.GetComponentsInChildren<T>(inactive));return all.ToArray();}
    }
}
public class CharacterDummy {}
public class EnemyDummy:CharacterDummy {public CharacterEventListener m_EventListener;public FTK_enemyCombat m_EnemyCombat;public string m_EnemyType;}
public class CharacterEventListener:Component {public enum DisplayLayer{Default} public CharacterDummy m_Dummy;public OffscreenCamera m_OffscreenCamera;}
public class OffscreenCamera:Component {public GameObject m_TargetObject;}
namespace GridEditor
{
    public class FTK_enemyCombat {public string m_ID;public CharacterEventListener m_EnemyAsset;}
    public class FTK_enemyCombatDB {public Dictionary<int,FTK_enemyCombat> rows=new Dictionary<int,FTK_enemyCombat>();public FTK_enemyCombat GetEntryByInt(int i){FTK_enemyCombat r;return rows.TryGetValue(i,out r)?r:null;} }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string name,Type[] args){} }
}
namespace FTKModFramework.Core
{
    static class LegacyKrakenPortrait {internal static int calls;internal static Transform result;internal static bool TryFrame(OffscreenCamera c,CharacterEventListener s,CharacterEventListener t,out Transform marker){calls++;marker=result;return marker!=null;}}
    static class Content {public static FTK_enemyCombatDB db=new FTK_enemyCombatDB();public static T Db<T>()where T:class{return db as T;} }
    static class ContentRegistry {public static Dictionary<string,int> ids=new Dictionary<string,int>();public static bool TryGetSyntheticId(string name,out int id,params Type[] types){return ids.TryGetValue(name,out id);} }
    static class Plugin {public static Logger Log=new Logger();public class Logger{public void LogInfo(string s){}public void LogWarning(string s){} } }
    static class EnemyVisualPatch { public static int calls; public static void ApplyPortraitMeshes(string id,CharacterEventListener cel){calls++;} }
    static class ExplicitEnemyMeshSwap {public static string RelativePath(Transform root,Transform child){if(root==child)return ".";var names=new List<string>();while(child!=null&&child!=root){names.Add(child.name);child=child.parent;}if(child!=root)return null;names.Reverse();return string.Join("/",names);}}
}
static class Program
{
    static int checks;const string Path="Root/Head/EncounterCam";
    static void Check(bool pass,string name){checks++;if(!pass)throw new Exception(name);}
    static CharacterEventListener Avatar(bool live)
    {
        var go=new GameObject("Avatar",live);var cel=new CharacterEventListener{gameObject=go};go.cel=cel;
        go.transform.Add("Root").Add("Head").Add("EncounterCam");go.transform.Add("CameraRoot").Add("PortraitCam");return cel;
    }
    static EnemyPortraitRegistry.Scope BeginRow(OffscreenCamera camera,FTK_enemyCombat row)
    {
        object[] args={camera,row,"PortraitCam",null};typeof(EnemyRowPortraitPatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);return (EnemyPortraitRegistry.Scope)args[3];
    }
    static EnemyPortraitRegistry.Scope BeginAvatar(OffscreenCamera camera,CharacterEventListener avatar)
    {
        object[] args={camera,avatar,"PortraitCam",null};typeof(EnemyAvatarPortraitPatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);return (EnemyPortraitRegistry.Scope)args[3];
    }
    static void Main()
    {
        var source=Avatar(false);var row=new FTK_enemyCombat{m_ID="custom",m_EnemyAsset=source};Content.db.rows[100]=row;ContentRegistry.ids[row.m_ID]=100;
        Check(EnemyPortraitRegistry.Register(row,Path),"Register exact custom row");
        Check(!EnemyPortraitRegistry.Register(new FTK_enemyCombat{m_ID="custom",m_EnemyAsset=source},Path),"Reject different row object");
        Check(!EnemyPortraitRegistry.Register(new FTK_enemyCombat{m_ID="vanilla",m_EnemyAsset=source},Path),"Reject vanilla row");
        foreach(string path in new[]{".","..","/Root","Root/","Root//Head","Root/../Head","Root\\Head","Root/\nHead"})
            Check(!EnemyPortraitRegistry.ValidPath(path),"Reject unsafe/nonchild path");
        Check(!EnemyPortraitRegistry.Register(row,"Root/Missing"),"Bad registration fails");
        Check(EnemyPortraitRegistry.ForRow(row).path==Path,"Bad registration preserves previous");
        var camera=new OffscreenCamera();var clone=Avatar(true);clone.m_OffscreenCamera=camera;camera.m_TargetObject=clone.gameObject;
        var rowScope=BeginRow(camera,row);Check(EnemyPortraitRegistry.Current(camera)!=null,"Row scope selected");
        var avatarScope=BeginAvatar(camera,source);Check(EnemyPortraitRegistry.Current(camera)!=null,"Direct nested CEL row forwarding");
        string first="PortraitCam",fallback="Fallback";EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);
        Check(first=="EncounterCam"&&fallback==null,"Validated marker arguments selected");
        Check(LegacyKrakenPortrait.calls==0,"Explicit marker wins over route framing");
        var nested=BeginAvatar(camera,source);Check(EnemyPortraitRegistry.Current(camera)==null,"Unresolved same-source nested CEL does not inherit row scope");
        EnemyPortraitRegistry.End(nested);Check(EnemyPortraitRegistry.Current(camera)!=null,"Nested scope restored");
        var failure=new Exception("render failed");var returned=typeof(EnemyAvatarPortraitPatch).GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{failure,avatarScope});
        Check(object.ReferenceEquals(failure,returned)&&EnemyPortraitRegistry.Current(camera)!=null,"Finalizer preserves exception and outer scope");
        var unresolvedRow=BeginRow(camera,new FTK_enemyCombat{m_ID="vanilla",m_EnemyAsset=source});
        Check(EnemyPortraitRegistry.Current(camera)==null,"Unresolved nested row does not inherit selection");
        EnemyPortraitRegistry.End(unresolvedRow);Check(EnemyPortraitRegistry.Current(camera)!=null,"Nested row restores outer selection");
        EnemyPortraitRegistry.End(rowScope);Check(EnemyPortraitRegistry.Current(camera)==null,"Outer scope removed");
        var live=Avatar(true);live.m_Dummy=new EnemyDummy{m_EventListener=live,m_EnemyCombat=row,m_EnemyType=row.m_ID};
        var liveScope=BeginAvatar(camera,live);Check(EnemyPortraitRegistry.Current(camera)!=null,"Verified live dummy source selected");
        first="EncounterCam";fallback="Fallback";EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);Check(first=="EncounterCam"&&fallback=="Fallback","Nonportrait arguments unchanged");
        clone.transform.Add("Other").Add("EncounterCam");first="PortraitCam";fallback="Fallback";EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);
        Check(first=="PortraitCam"&&fallback=="Fallback","Duplicate leaf preserves both arguments");
        camera.m_TargetObject=source.gameObject;first="PortraitCam";fallback="Fallback";EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);
        Check(first=="PortraitCam"&&fallback=="Fallback","Native prefab never selected as owned clone");
        EnemyPortraitRegistry.End(liveScope);
        ((EnemyDummy)live.m_Dummy).m_EventListener=source;Check(EnemyPortraitRegistry.ForAvatar(live)==null,"Stale dummy/avatar pairing rejected");
        var otherCamera=new OffscreenCamera();rowScope=BeginRow(camera,row);Check(EnemyPortraitRegistry.Current(otherCamera)==null,"Camera scopes independent");EnemyPortraitRegistry.End(rowScope);
        // Preview authority is independent of marker registration and confined to one direct row forwarding.
        var plain=new FTK_enemyCombat{m_ID="plain",m_EnemyAsset=source};Content.db.rows[101]=plain;ContentRegistry.ids[plain.m_ID]=101;
        camera.m_TargetObject=clone.gameObject;
        rowScope=BeginRow(camera,plain);avatarScope=BeginAvatar(camera,source);
        Check(EnemyPortraitRegistry.Current(camera).path==null,"Row without marker retains mesh authority");
        typeof(EnemyPortraitMeshPatch).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,source});Check(EnemyVisualPatch.calls==1,"Exact forwarded source permits preview through actual postfix");
        first="PortraitCam";fallback="Fallback";EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);
        Check(first=="PortraitCam"&&fallback=="Fallback","No marker registration preserves native arguments");
        LegacyKrakenPortrait.result=clone.transform.Add("GeneratedFrame");first="PortraitCam";
        EnemyPortraitRegistry.SelectOnClone(camera,ref first,ref fallback);
        Check(first=="GeneratedFrame"&&fallback==null,"Owned custom no-marker route can select generated frame");
        LegacyKrakenPortrait.result=null;
        EnemyPortraitRegistry.ApplyMeshesOnClone(camera,live);Check(EnemyVisualPatch.calls==1,"Wrong source rejected");
        clone.m_OffscreenCamera=otherCamera;EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==1,"Wrong owner rejected");clone.m_OffscreenCamera=camera;
        clone.gameObject.scene=new Scene{valid=false};EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==1,"Prefab target rejected");clone.gameObject.scene=new Scene{valid=true};
        camera.m_TargetObject=source.gameObject;EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==1,"Source target rejected");camera.m_TargetObject=clone.gameObject;
        nested=BeginAvatar(camera,source);EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==1,"Unknown nested capture cannot apply");EnemyPortraitRegistry.End(nested);
        EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==2,"Outer authority restored");
        Content.db.rows[101]=new FTK_enemyCombat{m_ID="plain",m_EnemyAsset=source};EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==2,"Replaced DB row rejected");Content.db.rows[101]=plain;
        EnemyPortraitRegistry.End(avatarScope);EnemyPortraitRegistry.End(rowScope);
        ((EnemyDummy)live.m_Dummy).m_EventListener=live;liveScope=BeginAvatar(camera,live);
        EnemyPortraitRegistry.ApplyMeshesOnClone(camera,source);Check(EnemyVisualPatch.calls==2,"Live avatar lane does not reload explicit meshes");EnemyPortraitRegistry.End(liveScope);
        Check(PortraitMarkerFixture.Validate(new JValue(Path))==Path,"Profile exact path accepted");
        foreach(JToken token in new JToken[]{null,new JValue((object)null),new JValue(true),new JValue("."),new JValue("Root/../Head")})
        {bool rejected=false;try{PortraitMarkerFixture.Validate(token);}catch(ArgumentException){rejected=true;}Check(rejected,"Profile invalid path/type rejected");}
        Check(PortraitMarkerFixture.Resolve(source.transform,Path).name=="EncounterCam","Profile marker resolve");
        Console.WriteLine("PASS "+checks+" portrait assertions (Unity/DB/Harmony stand-ins; no live framing proof).");
    }
}
