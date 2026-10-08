using System;
using System.Collections.Generic;
using System.Reflection.Emit;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n,Type[] args){} }
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyTranspiler:Attribute {}
 public class CodeInstruction { public OpCode opcode; public object operand; public List<Label> labels=new List<Label>(); public List<object> blocks=new List<object>(); public CodeInstruction(OpCode op,object value=null){opcode=op;operand=value;} }
}
namespace UnityEngine {
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
 public class GameObject { public Dictionary<Type,object> Components=new Dictionary<Type,object>(); public bool Throw; public T AddComponent<T>() where T:MonoBehaviour,new(){var c=new T();c.gameObject=this;Components[typeof(T)]=c;return c;} }
 public class MonoBehaviour { public GameObject gameObject; public T GetComponent<T>() where T:class {if(gameObject.Throw)throw new InvalidOperationException("guard");object v;return gameObject.Components.TryGetValue(typeof(T),out v)?v as T:null;} }
 public class SkinnedMeshRenderer {}
}
public class CharacterEventListener:UnityEngine.MonoBehaviour { public void DeathFallOff(){} }
public class FallOffLimb:UnityEngine.MonoBehaviour { public UnityEngine.SkinnedMeshRenderer m_Renderer; public int Calls; public UnityEngine.Vector3 Direction; public Exception Failure; public void FallOff(UnityEngine.Vector3 v){Calls++;Direction=v;if(Failure!=null)throw Failure;} }
namespace FTKModFramework.Core { public class EnemyMeshResources:UnityEngine.MonoBehaviour { public bool Applied=true,VisualResourcesOnly,Valid=true,Owned=true; public bool ValidLease(){return Valid;} public bool Owns(UnityEngine.SkinnedMeshRenderer r){return Owned;} } public static class Plugin { public static Logger Log=new Logger(); } public class Logger {public void LogInfo(string x){}public void LogWarning(string x){} } }
