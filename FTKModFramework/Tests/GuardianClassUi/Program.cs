using System;
using System.Collections.Generic;
using System.Reflection;
using FTKModFramework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T:class { return gameObject.GetComponent<T>(); }
    }
    public class MonoBehaviour:Component { }
    public class GameObject
    {
        public string name;
        public RectTransform transform;
        readonly Dictionary<Type,object> components=new Dictionary<Type,object>();
        public GameObject(string name,Transform parent=null)
        {this.name=name;transform=new RectTransform{gameObject=this,parent=parent};if(parent!=null)parent.children.Add(transform);}
        public T AddComponent<T>() where T:Component,new(){T c=new T{gameObject=this};components[typeof(T)]=c;return c;}
        public T GetComponent<T>() where T:class {object c;return components.TryGetValue(typeof(T),out c)?c as T:null;}
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
    }
    public struct Rect {public float xMin,yMax;}
    public class Transform:Component
    {
        public Transform parent;
        public Vector3 localPosition;
        public readonly List<Transform> children=new List<Transform>();
        public Transform Find(string name){return children.Find(c=>c.gameObject.name==name);}
        public Vector3 TransformPoint(Vector3 p){p+=localPosition;return parent==null?p:parent.TransformPoint(p);}
        public Vector3 InverseTransformPoint(Vector3 p){if(parent!=null)p=parent.InverseTransformPoint(p);return p-localPosition;}
    }
    public class RectTransform:Transform {public Rect rect;}
    public enum TextAnchor {UpperLeft,MiddleLeft}
}
namespace UnityEngine.UI
{
    public class Text:Component
    {
        public string text;
        public TextAnchor alignment=TextAnchor.MiddleLeft;
        public float preferredHeight;
        public RectTransform rectTransform {get{return (RectTransform)transform;}}
    }
}
namespace HarmonyLib {public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}}}
namespace GridEditor {public class FTK_playerGameStart {public enum ID {None}}}
public class uiSelectCharacterInfo:Component {public Text m_ClassAbility,m_StartingItems;}
namespace FTKModFramework.Core
{
    internal static class GuardianRuntime {internal static bool IsGuardianClass(int id){return id==1;}}
    internal static class OverworldAilmentImmunity
    {
        internal static bool IsRegistered(int id){return id==1;}
        internal static string DisplayName(int id){return id==1?"Cleansing March":null;}
    }
    internal static class Plugin {internal static Logger Log=new Logger();}
    internal class Logger {internal void LogWarning(string message){}}
}
internal static class Program
{
    static int checks;
    static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
    static void Main()
    {
        GameObject root=new GameObject("info"), display=new GameObject("DisplayRoot",root.transform);
        var info=root.AddComponent<uiSelectCharacterInfo>();
        info.m_ClassAbility=new GameObject("charAbility",display.transform).AddComponent<Text>();
        info.m_ClassAbility.transform.localPosition=new Vector3(18,-231,0);
        info.m_ClassAbility.preferredHeight=132;
        var group=new GameObject("Image (1)",display.transform);
        group.transform.localPosition=new Vector3(226,-306,0);
        var header=new GameObject("charItemsHeader",group.transform).AddComponent<Text>();
        header.rectTransform.rect=new Rect{yMax=18};
        info.m_StartingItems=new GameObject("charItems",display.transform).AddComponent<Text>();
        info.m_StartingItems.transform.localPosition=new Vector3(18,-381,0);
        var state=root.AddComponent<GuardianClassLayoutState>();
        state.Arrange(info);
        Check(group.transform.localPosition.y==-387,"heading clears top-anchored132unit text plus6unit gap");
        Check(info.m_StartingItems.transform.localPosition.y==-462,"item list shifts with heading preserving native spacing");
        Check(display.transform.localPosition.y==0,"shared parent and other stats stay unchanged");
        Check(info.m_ClassAbility.alignment==TextAnchor.UpperLeft,"expanded label is top-anchored");
        state.Arrange(info);
        Check(group.transform.localPosition.y==-387 && info.m_StartingItems.transform.localPosition.y==-462,"repeated Guardian arrange does not accumulate displacement");
        typeof(GuardianClassInfoPatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{info});
        Check(group.transform.localPosition.y==-306 && info.m_StartingItems.transform.localPosition.y==-381,"native refresh prefix restores both positions before vanilla reuse");
        Check(info.m_ClassAbility.alignment==TextAnchor.MiddleLeft,"native refresh restores original text alignment");
        state.Restore();Check(group.transform.localPosition.y==-306,"restoration is idempotent");
        info.m_ClassAbility.preferredHeight=30;state.Arrange(info);
        Check(group.transform.localPosition.y==-306,"short existing label never moves equipment upward");
        state.Restore();group.name="unsupported";
        bool rejected=false;try{state.Arrange(info);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && info.m_ClassAbility.alignment==TextAnchor.MiddleLeft && info.m_StartingItems.transform.localPosition.y==-381,"unknown native hierarchy fails before layout mutation");
        group.name="Image (1)";
        info.m_ClassAbility.text="native";
        var show=typeof(GuardianClassInfoPatch).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
        show.Invoke(null,new object[]{info,(GridEditor.FTK_playerGameStart.ID)1});
        Check(info.m_ClassAbility.text.Contains("Cleansing March: immune to\nPoison/Curse while exploring."),"class card names the registered passive and exact scope");
        string once=info.m_ClassAbility.text;
        show.Invoke(null,new object[]{info,(GridEditor.FTK_playerGameStart.ID)1});
        Check(info.m_ClassAbility.text==once,"repeated class UI refresh does not duplicate passive text");
        Console.WriteLine("PASS "+checks+" actual class UI layout lifecycle checks (Unity stand-ins; no live fit proof)");
    }
}
