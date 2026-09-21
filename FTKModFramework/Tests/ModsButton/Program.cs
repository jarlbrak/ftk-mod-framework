using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonyLib { public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n){} } }
namespace UnityEngine.Events { public enum UnityEventCallState { Off } }
namespace UnityEngine
{
    public class Object
    {
        public static GameObject Instantiate(GameObject source,Transform parent)
        {
            GameObject copy=new GameObject(source.name,parent);
            foreach(Button ignored in source.GetComponentsInChildren<Button>(true))
            {
                GameObject child=new GameObject("playButton",copy.transform);
                child.button=new Button{gameObject=child};child.text=new Text{gameObject=child};
            }
            return copy;
        }
        public static void Destroy(GameObject value){value.transform.parent.children.Remove(value.transform);}
    }
    public class GameObject
    {
        public string name;public bool activeInHierarchy=true;public Transform transform;public Button button;public Text text;
        public GameObject(string name,Transform parent=null)
        {this.name=name;transform=new Transform{gameObject=this,parent=parent};if(parent!=null)parent.children.Add(transform);}
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class
        {
            List<T> found=new List<T>();
            if(typeof(T)==typeof(Transform))found.Add(transform as T);
            if(button is T)found.Add(button as T);if(text is T)found.Add(text as T);
            foreach(Transform child in transform.children)found.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));
            return found.ToArray();
        }
        public T GetComponentInChildren<T>(bool inactive) where T:class
        {T[] found=GetComponentsInChildren<T>(inactive);return found.Length==0?null:found[0];}
    }
    public class Transform
    {
        public GameObject gameObject;public Transform parent;public List<Transform> children=new List<Transform>();
        public string name { get{return gameObject.name;} }
        public int childCount { get{return children.Count;} }
        public Transform GetChild(int i){return children[i];}
        public int GetSiblingIndex(){return parent.children.IndexOf(this);}
        public void SetSiblingIndex(int i){parent.children.Remove(this);parent.children.Insert(Math.Min(i,parent.children.Count),this);}
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class{return gameObject.GetComponentsInChildren<T>(inactive);}
    }
}
namespace UnityEngine.UI
{
    public class Button
    {
        public GameObject gameObject;public Transform transform {get{return gameObject.transform;}}
        public Click onClick=new Click();
    }
    public class Text {public GameObject gameObject;public string text;}
    public class Click
    {
        public int GetPersistentEventCount(){return 0;}
        public void SetPersistentListenerState(int i,UnityEngine.Events.UnityEventCallState state){}
        public void RemoveAllListeners(){}public void AddListener(Action action){}
    }
}
namespace StartGameFE { public class MainScreen {public Transform m_SelectableParent;} }
namespace FTKModFramework
{
    public static class Plugin {public static LogSink Log=new LogSink();}
    public class LogSink {public void LogWarning(string s){}public void LogInfo(string s){}public void LogDebug(string s){}public void LogError(string s){throw new Exception(s);}}
}
namespace FTKModFramework.Core.UI {public static class ModsPanel {public static void Open(){}}}
class Program
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Show(StartGameFE.MainScreen screen)
    {typeof(FTKModFramework.Core.UI.MainScreen_OnSetFocus_Patch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{screen});}
    static StartGameFE.MainScreen Screen()
    {
        GameObject parent=new GameObject("menu"),grid=new GameObject("ButtonRoot",parent.transform),cell=new GameObject("New",grid.transform),button=new GameObject("playButton",cell.transform);
        button.button=new Button{gameObject=button};button.text=new Text{gameObject=button};
        return new StartGameFE.MainScreen{m_SelectableParent=parent.transform};
    }
    static int Count(StartGameFE.MainScreen screen)
    {int count=0;foreach(Transform t in screen.m_SelectableParent.GetComponentsInChildren<Transform>(true))if(t.name=="ModsButton")count++;return count;}
    static void Main()
    {
        Show(null);Show(new StartGameFE.MainScreen());
        var first=Screen();Show(first);Check(Count(first)==1,"first screen receives button");
        Show(first);Check(Count(first)==1,"repeat focus does not duplicate nested cell");
        var second=Screen();Show(second);Check(Count(second)==1,"recreated screen receives its own button");
        Check(Count(first)==1,"old instance stays unchanged");
        Show(second);Check(Count(second)==1,"recreated screen remains idempotent");
        var late=new StartGameFE.MainScreen();Show(late);late.m_SelectableParent=Screen().m_SelectableParent;Show(late);
        Check(Count(late)==1,"missing parent does not suppress later initialization");
        Console.WriteLine(checks+" Mods button lifecycle checks passed");
    }
}
