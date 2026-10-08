using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using FTKModFramework.Core;

namespace UnityEngine
{
    public class Object
    {
        public static Object NextClone;
        public static T Instantiate<T>(T source) where T:Object { return (T)NextClone; }
        public static void Destroy(Object value) { ((GameObject)value).Destroyed=true; }
    }
    public class GameObject:Object
    {
        public bool ModelRetained, FaceRetained, Destroyed;
    }
}
public class CharacterEventListener:UnityEngine.Object
{
    public enum DisplayLayer { Inventory }
    public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
    public T[] GetComponentsInChildren<T>(bool inactive) where T:class
    {
        if(typeof(T)==typeof(EnemyMeshResources))return new[]{new EnemyMeshResources(gameObject) as T};
        if(typeof(T)==typeof(HeadFaceResources))return new[]{new HeadFaceResources(gameObject) as T};
        return new T[0];
    }
}
public class OffscreenCamera
{
    public UnityEngine.GameObject m_TargetObject;
    private void InstantiateTarget(CharacterEventListener avatar,CharacterEventListener.DisplayLayer layer,int flag,bool pose) { }
}
namespace FTKModFramework.Core
{
    internal sealed class EnemyMeshResources
    {
        internal static bool Fail;
        UnityEngine.GameObject owner;
        internal EnemyMeshResources(UnityEngine.GameObject value){owner=value;}
        internal bool HasLease {get{return true;}}
        internal bool ValidLease(){return owner.ModelRetained;}
        internal static void RetainHierarchy(UnityEngine.GameObject value){if(!Fail)value.ModelRetained=true;}
    }
    internal sealed class HeadFaceResources
    {
        UnityEngine.GameObject owner;
        internal HeadFaceResources(UnityEngine.GameObject value){owner=value;}
        internal bool HasLease {get{return true;}}
        internal bool Active {get{return owner.FaceRetained;}}
        internal static void RetainHierarchy(UnityEngine.GameObject value){value.FaceRetained=true;}
    }
    internal static class Plugin {internal static Logger Log=new Logger();}
    internal sealed class Logger {internal void LogWarning(string text){} }
}
internal static class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void LaterFailure(){throw new ApplicationException("native setup failed");}
    static List<CodeInstruction> Sequence()
    {
        return new List<CodeInstruction>{new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call,typeof(UnityEngine.Object).GetMethod("Instantiate").MakeGenericMethod(typeof(CharacterEventListener))),
            new CodeInstruction(OpCodes.Stloc_0)};
    }
    static Action<OffscreenCamera,CharacterEventListener> Compile(List<CodeInstruction> code)
    {
        var method=new DynamicMethod("OffscreenClone",typeof(void),new[]{typeof(OffscreenCamera),typeof(CharacterEventListener)},typeof(Program).Module,true);
        var il=method.GetILGenerator();il.DeclareLocal(typeof(CharacterEventListener));
        foreach(var instruction in code)
        {
            if(instruction.operand is MethodInfo call)il.Emit(instruction.opcode,call);
            else il.Emit(instruction.opcode);
        }
        return (Action<OffscreenCamera,CharacterEventListener>)method.CreateDelegate(typeof(Action<OffscreenCamera,CharacterEventListener>));
    }
    static void Main()
    {
        var source=Sequence();source.Add(new CodeInstruction(OpCodes.Call,typeof(Program).GetMethod("LaterFailure")));source.Add(new CodeInstruction(OpCodes.Ret));
        var patched=HeadFaceInventoryLeasePatch.Transpiler(source).ToList();
        Check(source.Count==5&&patched.Count==7,"input remains unchanged and exact call inserted");
        Check(patched[3].opcode==OpCodes.Ldloc_0&&((MethodInfo)patched[4].operand).Name=="RetainClone","both leases retained immediately after local clone assignment");
        Check(HeadFaceInventoryLeasePatch.Transpiler(patched).Count()==7,"patch idempotent");
        var clone=new CharacterEventListener();UnityEngine.Object.NextClone=clone;bool failed=false;
        try{Compile(patched)(new OffscreenCamera(),new CharacterEventListener());}catch(ApplicationException){failed=true;}
        Check(failed&&clone.gameObject.ModelRetained&&clone.gameObject.FaceRetained,"later native failure occurs after both leases retained");
        clone=new CharacterEventListener();UnityEngine.Object.NextClone=clone;EnemyMeshResources.Fail=true;failed=false;
        try{Compile(patched)(new OffscreenCamera(),new CharacterEventListener());}catch(InvalidOperationException){failed=true;}
        EnemyMeshResources.Fail=false;
        Check(failed&&clone.gameObject.Destroyed,"missing inherited source lease destroys clone before native setup");
        foreach(var bad in new[]{new List<CodeInstruction>{new CodeInstruction(OpCodes.Ret)},Sequence().Concat(Sequence()).ToList()})
        {bool rejected=false;try{HeadFaceInventoryLeasePatch.Transpiler(bad).ToList();}catch(InvalidOperationException){rejected=true;}Check(rejected,"absent/ambiguous clone assignment rejected");}
        Console.WriteLine("PASS "+checks+" actual offscreen clone transpiler checks (Unity stand-ins; no live proof)");
    }
}
