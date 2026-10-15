using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Profiling;
using Newtonsoft.Json.Linq;
namespace FtkResourcePrototype {
public partial class Plugin {
 List<GameObject> fixtureRoots=new List<GameObject>();List<UnityEngine.Object> fixtureOriginals=new List<UnityEngine.Object>();List<Texture2D> fixtureTextures=new List<Texture2D>();
 static IEnumerable<CodeInstruction> ReadableControl(IEnumerable<CodeInstruction> source){var code=new List<CodeInstruction>(source);int count=0;for(int i=1;i<code.Count;i++){MethodInfo m=code[i].operand as MethodInfo;if(m!=null && m.Name=="LoadImage" && m.DeclaringType==typeof(ImageConversion) && m.GetParameters().Length==3){if(code[i-1].opcode!=OpCodes.Ldc_I4_1)throw new Exception("Unexpected flag");code[i-1].opcode=OpCodes.Ldc_I4_0;count++;}}if(count!=1)throw new Exception("Expected one texture upload");return code;}
 JObject CreateTextures(bool readable,bool legacy){if(fixtureRoots.Count!=0)throw new Exception("Clear prior fixture first");fixtureTextures.Clear();Assembly core=null;foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="FTKModFramework")core=a;if(core==null)throw new Exception("Framework missing");
 Type assignment=core.GetType("FTKModFramework.Core.EnemyRendererMesh",true);MethodInfo make=assignment.GetMethod("ForStaticRenderer");MethodInfo apply=core.GetType(legacy?"FTKModFramework.Core.LegacyVisualResources":"FTKModFramework.Core.ExplicitEnemyMeshSwap",true).GetMethod(legacy?"OptionalTexture":"ApplyToObject",BindingFlags.NonPublic|BindingFlags.Static);var harmony=new Harmony("com.ftkmf.resource-fixture-readable");
 long before=Profiler.GetTotalAllocatedMemoryLong();
 try{if(readable)harmony.Patch(apply,null,null,new HarmonyMethod(typeof(Plugin).GetMethod("ReadableControl",BindingFlags.NonPublic|BindingFlags.Static)));
 Shader shader=Shader.Find("Unlit/Texture");if(shader==null)throw new Exception("Missing fixture shader");
 for(int i=0;i<20;i++){GameObject go=new GameObject("ResourceFixture"+i);go.SetActive(false);fixtureRoots.Add(go);Mesh original=new Mesh();fixtureOriginals.Add(original);original.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};original.triangles=new[]{0,1,2};go.AddComponent<MeshFilter>().sharedMesh=original;Material material=new Material(shader);fixtureOriginals.Add(material);MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
 Array assignments=Array.CreateInstance(assignment,1);assignments.SetValue(make.Invoke(null,new object[]{".","resource-fixture.glb","resource-fixture.png",false}),0);if(legacy)apply.Invoke(null,new object[]{"resource-fixture",go.AddComponent<CharacterEventListener>(),r,"resource-fixture.png"});else if(!(bool)apply.Invoke(null,new object[]{"resource-fixture",go,assignments,null,false}))throw new Exception("Apply failed");Texture2D texture=r.sharedMaterial.mainTexture as Texture2D;if(texture==null)throw new Exception("Texture missing");fixtureTextures.Add(texture);
 // Enable and disable once so Unity schedules ownership teardown for these owned fixture roots.
 if(!legacy)go.SetActive(true);r.enabled=false;
 }
 JObject result=TextureState();result["allocatedBefore"]=before;result["allocatedAfter"]=Profiler.GetTotalAllocatedMemoryLong();result["readableControl"]=readable;result["loader"]=legacy?"legacy":"explicit";return result;
 }catch{ClearTextures();throw;}finally{if(readable)harmony.Unpatch(apply,HarmonyPatchType.Transpiler,harmony.Id);}}
 JObject TextureState(){JArray rows=new JArray();foreach(Texture2D t in fixtureTextures){if(t==null){rows.Add(new JObject{{"destroyed",true}});continue;}bool readable=true;try{t.GetPixel(0,0);}catch(UnityException){readable=false;}rows.Add(new JObject{{"destroyed",false},{"instanceId",t.GetInstanceID()},{"readable",readable},{"width",t.width},{"height",t.height},{"mips",t.mipmapCount},{"format",t.format.ToString()}});}JObject result=new JObject{{"textures",rows},{"unityAllocated",Profiler.GetTotalAllocatedMemoryLong()},{"managedBytes",GC.GetTotalMemory(false)}};if(fixtureTextures.Count>0 &&fixtureTextures[0]!=null)result["gpuSha256"]=GpuHash(fixtureTextures[0]);return result;}
 string GpuHash(Texture texture){RenderTexture old=RenderTexture.active;RenderTexture rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);Texture2D readback=null;try{Graphics.Blit(texture,rt);RenderTexture.active=rt;readback=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);readback.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readback.Apply();using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(readback.GetRawTextureData())).Replace("-","").ToLowerInvariant();}finally{RenderTexture.active=old;if(readback!=null)Destroy(readback);RenderTexture.ReleaseTemporary(rt);}}
 JObject ClearTextures(){foreach(GameObject go in fixtureRoots)if(go!=null)Destroy(go);foreach(UnityEngine.Object obj in fixtureOriginals)if(obj!=null)Destroy(obj);fixtureRoots.Clear();fixtureOriginals.Clear();return new JObject{{"cleanupRequested",true}};}
}}
namespace FtkResourcePrototype {
public partial class Plugin {
 JObject CreateSharedTextures(bool readable){if(fixtureRoots.Count!=0)throw new Exception("Clear prior fixture first");fixtureTextures.Clear();Assembly core=null;foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="FTKModFramework")core=a;
 Type assignment=core.GetType("FTKModFramework.Core.EnemyRendererMesh",true);MethodInfo make=assignment.GetMethod("ForStaticRenderer");MethodInfo apply=core.GetType("FTKModFramework.Core.ExplicitEnemyMeshSwap",true).GetMethod("ApplyToObject",BindingFlags.NonPublic|BindingFlags.Static);var harmony=new Harmony("com.ftkmf.resource-fixture-readable");long before=Profiler.GetTotalAllocatedMemoryLong();
 try{if(readable)harmony.Patch(apply,null,null,new HarmonyMethod(typeof(Plugin).GetMethod("ReadableControl",BindingFlags.NonPublic|BindingFlags.Static)));
 Shader shader=Shader.Find("Unlit/Texture");if(shader==null)throw new Exception("Missing fixture shader");GameObject root=new GameObject("SharedResourceFixture");root.SetActive(false);fixtureRoots.Add(root);Array assignments=Array.CreateInstance(assignment,20);List<MeshRenderer> renderers=new List<MeshRenderer>();
 for(int i=0;i<20;i++){GameObject go=new GameObject("Part"+i);go.transform.parent=root.transform;Mesh original=new Mesh();fixtureOriginals.Add(original);original.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};original.triangles=new[]{0,1,2};go.AddComponent<MeshFilter>().sharedMesh=original;Material material=new Material(shader);fixtureOriginals.Add(material);MeshRenderer r=go.AddComponent<MeshRenderer>();r.enabled=false;r.sharedMaterial=material;renderers.Add(r);assignments.SetValue(make.Invoke(null,new object[]{go.name,"resource-fixture.glb","resource-fixture.png",false}),i);}
 if(!(bool)apply.Invoke(null,new object[]{"resource-fixture",root,assignments,null,false}))throw new Exception("Apply failed");foreach(MeshRenderer r in renderers){Texture2D texture=r.sharedMaterial.mainTexture as Texture2D;if(texture==null)throw new Exception("Texture missing");fixtureTextures.Add(texture);}root.SetActive(true);JObject result=TextureState();result["allocatedBefore"]=before;result["allocatedAfter"]=Profiler.GetTotalAllocatedMemoryLong();result["readableControl"]=readable;result["loader"]="shared-explicit";return result;
 }catch{ClearTextures();throw;}finally{if(readable)harmony.Unpatch(apply,HarmonyPatchType.Transpiler,harmony.Id);}}
}}
