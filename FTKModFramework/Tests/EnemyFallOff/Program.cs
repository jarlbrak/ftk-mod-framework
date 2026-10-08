using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using FTKModFramework.Core;
using HarmonyLib;
using UnityEngine;
class Program {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Main(){
  var go=new GameObject();var cel=go.AddComponent<CharacterEventListener>();var limb=go.AddComponent<FallOffLimb>();limb.m_Renderer=new SkinnedMeshRenderer();var direction=new Vector3(2,3,4);
  EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==1&&limb.Direction.x==2&&limb.Direction.y==3&&limb.Direction.z==4,"vanilla native call once with original direction");
  var resources=go.AddComponent<EnemyMeshResources>();EnemyFallOffMarker.Install(cel,EnemyFallOffPolicy.PreserveNative);EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==2,"native policy passes through");
  EnemyFallOffMarker.Install(cel,EnemyFallOffPolicy.PreserveCustomBody);EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==2,"verified custom body skips only falloff");
  resources.Valid=false;EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==3,"expired lease passes through");resources.Valid=true;
  resources.VisualResourcesOnly=true;EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==4,"visual-only marker passes through");resources.VisualResourcesOnly=false;
  resources.Owned=false;EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==5,"unowned renderer passes through");resources.Owned=true;
  limb.m_Renderer=new SkinnedMeshRenderer();EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==6,"changed renderer passes through");
  go.Throw=true;EnemyFallOffPatch.FallOff(limb,direction);Check(limb.Calls==7,"guard exception falls through once");go.Throw=false;
  var failure=new InvalidOperationException("native");limb.Failure=failure;try{EnemyFallOffPatch.FallOff(limb,direction);throw new Exception("missing native failure");}catch(InvalidOperationException e){Check(object.ReferenceEquals(e,failure)&&limb.Calls==8,"native exception preserved");}
  var native=typeof(FallOffLimb).GetMethod("FallOff");var call=new CodeInstruction(OpCodes.Callvirt,native);var label=new DynamicMethod("label",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();call.labels.Add(label);var block=new object();call.blocks.Add(block);
  var after=new CodeInstruction(OpCodes.Nop);var result=new List<CodeInstruction>(EnemyFallOffPatch.Transpiler(new[]{call,after}));Check(result[0].opcode==OpCodes.Call&&result[0].operand.Equals(typeof(EnemyFallOffPatch).GetMethod("FallOff",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)),"exact native call replaced with stack-compatible wrapper");Check(result[0].labels[0].Equals(label)&&object.ReferenceEquals(result[0].blocks[0],block)&&object.ReferenceEquals(result[1],after),"branch exception and surrounding cleanup metadata preserved");
  var missing=new CodeInstruction(OpCodes.Nop);Check(object.ReferenceEquals(new List<CodeInstruction>(EnemyFallOffPatch.Transpiler(new[]{missing}))[0],missing),"missing call leaves native body unchanged");
  var a=new CodeInstruction(OpCodes.Callvirt,native);var b=new CodeInstruction(OpCodes.Callvirt,native);EnemyFallOffPatch.Transpiler(new[]{a,b});Check(a.opcode==OpCodes.Callvirt&&b.opcode==OpCodes.Callvirt&&a.operand.Equals(native),"ambiguous calls leave native body unchanged");
  Console.WriteLine(checks+" checks passed");
 }
}
