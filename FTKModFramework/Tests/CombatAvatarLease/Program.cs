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
        public static T Instantiate<T>(T value) where T:Object { return (T)NextClone; }
    }
    public class GameObject:Object {public bool Retained;}
}
public class CharacterEventListener:UnityEngine.Object {public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();}
public class CharacterOverworld {public CharacterEventListener m_Avatar;}
public class CharacterDummy
{
    public CharacterOverworld m_CharacterOverworld;
    public CharacterEventListener m_EventListener;
}
namespace FTKModFramework.Core
{
    internal static class EnemyMeshResources
    {
        internal static int Calls;
        internal static bool Fail;
        internal static void RetainHierarchy(UnityEngine.GameObject value){Calls++;if(Fail)throw new Exception("retain failure");value.Retained=true;}
    }
    internal static class Plugin {internal static Logger Log=new Logger();}
    internal class Logger {internal void LogWarning(string message){}}
}
internal static class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void LaterFailure(){throw new ApplicationException("native initialization failed");}
    static List<CodeInstruction> CloneSequence()
    {
        return new List<CodeInstruction>{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,typeof(CharacterDummy).GetField("m_CharacterOverworld")),
            new CodeInstruction(OpCodes.Ldfld,typeof(CharacterOverworld).GetField("m_Avatar")),
            new CodeInstruction(OpCodes.Call,typeof(UnityEngine.Object).GetMethod("Instantiate").MakeGenericMethod(typeof(CharacterEventListener))),
            new CodeInstruction(OpCodes.Stfld,typeof(CharacterDummy).GetField("m_EventListener"))};
    }
    static Action<CharacterDummy> Compile(List<CodeInstruction> code)
    {
        var method=new DynamicMethod("RetainBeforeNativeFailure",typeof(void),new[]{typeof(CharacterDummy)},typeof(Program).Module,true);
        var il=method.GetILGenerator();
        foreach(var instruction in code)
        {
            if(instruction.operand is FieldInfo field)il.Emit(instruction.opcode,field);
            else if(instruction.operand is MethodInfo call)il.Emit(instruction.opcode,call);
            else il.Emit(instruction.opcode);
        }
        return (Action<CharacterDummy>)method.CreateDelegate(typeof(Action<CharacterDummy>));
    }
    static void Main(string[] args)
    {
        if(args.Length==1)NativeAssignment.Verify(args[0]);
        var source=CloneSequence();source.Add(new CodeInstruction(OpCodes.Ret));
        var patched=PlayerCombatMeshLeasePatch.Transpiler(source).ToList();
        Check(source.Count==7&&patched.Count==9,"Input sequence remains unchanged");
        Check(patched[6].opcode==OpCodes.Ldarg_0&&((MethodInfo)patched[7].operand).Name=="RetainClone","Retention follows assignment immediately");
        Check(PlayerCombatMeshLeasePatch.Transpiler(patched).Count()==9,"Repeated transpilation is idempotent");
        var cow=new CharacterOverworld{m_Avatar=new CharacterEventListener()};var dummy=new CharacterDummy{m_CharacterOverworld=cow};
        UnityEngine.Object.NextClone=new CharacterEventListener();Compile(patched)(dummy);
        Check(dummy.m_EventListener!=cow.m_Avatar&&dummy.m_EventListener.gameObject.Retained&&!cow.m_Avatar.gameObject.Retained,"Only assigned clone retained without Awake");
        var throwing=CloneSequence();throwing.Add(new CodeInstruction(OpCodes.Call,typeof(Program).GetMethod("LaterFailure")));throwing.Add(new CodeInstruction(OpCodes.Ret));
        UnityEngine.Object.NextClone=new CharacterEventListener();bool failed=false;
        try{Compile(PlayerCombatMeshLeasePatch.Transpiler(throwing).ToList())(dummy);}catch(ApplicationException e){failed=e.Message=="native initialization failed";}
        Check(failed&&dummy.m_EventListener.gameObject.Retained,"Later native exception propagates after clone retained");
        UnityEngine.Object.NextClone=new CharacterEventListener();
        EnemyMeshResources.Fail=true;Compile(patched)(dummy);EnemyMeshResources.Fail=false;
        Check(dummy.m_EventListener==UnityEngine.Object.NextClone&&!dummy.m_EventListener.gameObject.Retained&&EnemyMeshResources.Calls==3,"Retention observer error leaves native assignment intact and returns normally");
        foreach(var bad in new[]{new List<CodeInstruction>{new CodeInstruction(OpCodes.Ret)},CloneSequence().Concat(CloneSequence()).ToList()})
        {bool rejected=false;try{PlayerCombatMeshLeasePatch.Transpiler(bad).ToList();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Missing or ambiguous assignment fails before patching");}
        var foreign=CloneSequence();foreign[4]=new CodeInstruction(OpCodes.Call,typeof(Program).GetMethod("LaterFailure"));bool mismatch=false;
        try{PlayerCombatMeshLeasePatch.Transpiler(foreign).ToList();}catch(InvalidOperationException){mismatch=true;}Check(mismatch,"Non-Instantiate assignment is never intercepted");
        Check(typeof(PlayerCombatMeshLeasePatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static)==null,"Combat lease route has no finalizer wrapper");
        Console.WriteLine("PASS "+checks+" executable combat clone IL checks (Unity stand-ins; no live proof)");
    }
}
