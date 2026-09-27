using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace FtkResourcePrototype {
public partial class Plugin {
 static readonly string[] foliageKeywords={"_AFS_GRASS_APPROXTRANS","AFS_SH_AMBIENT","AFS_COLOR_AMBIENT","AFS_GRADIENT_AMBIENT"};
 static readonly string[] foliageVectors={"_AfsDirectSunDir","_AfsDirectSunCol","_AfsSpecFade","_AfsSkyColor","_AfsGroundColor","_AfsEquatorColor","_AfsAmbientColor","afs_SHAr","afs_SHAg","afs_SHAb","afs_SHBr","afs_SHBg","afs_SHBb","afs_SHC","_AfsWavingTint","_AfsTreeColor","_AfsTerrainTrees","_AfsBillboardCameraForward","_AfsBillboardShadowCameraForward"};
 static void FoliageEnable(string name){if(!Shader.IsKeywordEnabled(name))Shader.EnableKeyword(name);}
 static void FoliageDisable(string name){if(Shader.IsKeywordEnabled(name))Shader.DisableKeyword(name);}
 static IEnumerable<CodeInstruction> FoliageRedirect(IEnumerable<CodeInstruction> source, MethodBase __originalMethod){
  var list=new List<CodeInstruction>(source);int n=0;
  MethodInfo enable=AccessTools.Method(typeof(Shader),"EnableKeyword",new[]{typeof(string)}),disable=AccessTools.Method(typeof(Shader),"DisableKeyword",new[]{typeof(string)});
  foreach(var c in list){if(c.opcode==OpCodes.Call && (Equals(c.operand,enable)||Equals(c.operand,disable))){c.operand=AccessTools.Method(typeof(Plugin),Equals(c.operand,enable)?"FoliageEnable":"FoliageDisable");n++;}}
  int expected=__originalMethod.Name=="afsLightingSettings"?2:9;
  if(n!=expected)throw new Exception("Unexpected foliage keyword call count: "+n+" expected "+expected);
  return list;
 }
 static JObject FoliageOutput(){JObject r=new JObject();foreach(string key in foliageKeywords)r[key]=Shader.IsKeywordEnabled(key);foreach(string key in foliageVectors){Vector4 v=Shader.GetGlobalVector(key);r[key]=new JArray(v.x,v.y,v.z,v.w);}r["_AfsRainamount"]=Shader.GetGlobalFloat("_AfsRainamount");return r;}
 JObject FoliageBenchmark(){
  SetupAdvancedFoliageShader subject=null;int active=0;
  foreach(var c in Resources.FindObjectsOfTypeAll<SetupAdvancedFoliageShader>())if(c.isActiveAndEnabled){subject=c;active++;}
  if(active!=1)throw new Exception("Expected exactly one active foliage controller, got "+active);
  if(subject.BillboardAdjustToCamera && Camera.main==null)throw new Exception("Missing main camera");
  var methods=new[]{AccessTools.Method(typeof(SetupAdvancedFoliageShader),"afsLightingSettings"),AccessTools.Method(typeof(SetupAdvancedFoliageShader),"UpdateLightingForClassicBillboards")};
  var patch=AccessTools.Method(typeof(Plugin),"FoliageRedirect");var harmony=new Harmony("com.ftkmf.resource-foliage-benchmark");
  bool[] keywords=new bool[foliageKeywords.Length];for(int i=0;i<keywords.Length;i++)keywords[i]=Shader.IsKeywordEnabled(foliageKeywords[i]);
  FieldInfo[] fields=typeof(SetupAdvancedFoliageShader).GetFields(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);object[] values=new object[fields.Length];for(int i=0;i<fields.Length;i++){object v=fields[i].GetValue(subject);values[i]=v is Array?((Array)v).Clone():v;}
  Action update=subject.Update;JArray rows=new JArray();bool installed=false;JObject expectedOutput=null;bool outputsMatch=true;bool repair=true;
  try{
   for(int round=0;round<8;round++){
    bool candidate=round%4==1||round%4==2;
    if(candidate!=installed){foreach(var method in methods){if(candidate)harmony.Patch(method,null,null,new HarmonyMethod(patch));else harmony.Unpatch(method,patch);}installed=candidate;}
    for(int i=0;i<fields.Length;i++)fields[i].SetValue(subject,values[i] is Array?((Array)values[i]).Clone():values[i]);
    for(int i=0;i<32;i++)update();
    int count=round<4?250:1000;int gc0=GC.CollectionCount(0);long heap0=GC.GetTotalMemory(false);long start=Stopwatch.GetTimestamp();
    for(int i=0;i<count;i++)update();
    long elapsed=Stopwatch.GetTimestamp()-start;long heap1=GC.GetTotalMemory(false);int gc1=GC.CollectionCount(0);
    JObject output=FoliageOutput();if(expectedOutput==null)expectedOutput=output;else outputsMatch&=JToken.DeepEquals(expectedOutput,output);
    rows.Add(new JObject{{"round",round},{"candidate",candidate},{"count",count},{"milliseconds",elapsed*1000.0/Stopwatch.Frequency},{"heapDelta",heap1-heap0},{"gc0Delta",gc1-gc0},{"output",output}});
   }
   foreach(var method in methods)harmony.Patch(method,null,null,new HarmonyMethod(patch));installed=true;
   update();JObject prior=FoliageOutput();
   foreach(string keyword in foliageKeywords){bool state=Shader.IsKeywordEnabled(keyword);if(state)Shader.DisableKeyword(keyword);else Shader.EnableKeyword(keyword);update();repair &= (bool)prior[keyword]==Shader.IsKeywordEnabled(keyword);}
   return new JObject{{"rows",rows},{"outputsMatch",outputsMatch},{"externalKeywordRepair",repair},{"ambientMode",RenderSettings.ambientMode.ToString()},{"componentName",subject.name},{"grassApproxTrans",subject.GrassApproxTrans},{"billboardAdjustToCamera",subject.BillboardAdjustToCamera},{"semantics","Synchronous bound-delegate native Update microbenchmark; patch install and warmup excluded. Heap delta is not allocated bytes; no overall FPS or CPU utilization claim."}};
  }finally{
   foreach(var method in methods)harmony.Unpatch(method,patch);
   for(int i=0;i<fields.Length;i++)fields[i].SetValue(subject,values[i]);
   update();
   for(int i=0;i<keywords.Length;i++){if(keywords[i])Shader.EnableKeyword(foliageKeywords[i]);else Shader.DisableKeyword(foliageKeywords[i]);}
  }
 }
}}
