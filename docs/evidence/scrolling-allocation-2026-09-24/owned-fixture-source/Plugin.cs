using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.Profiling;
using Newtonsoft.Json.Linq;
namespace FtkResourcePrototype {
[BepInPlugin("com.ftkmf.resource-prototype", "Isolated resource probe", "0.1.0")]
[BepInDependency("com.ftkmf.runtime-model-test")]
public partial class Plugin : BaseUnityPlugin {
 string root,last; float next; int oldCap,oldTextureLimit; bool ready;
 void Awake(){enabled=false;root=Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
 PluginInfo helper;
 if(Environment.GetEnvironmentVariable("FTK_RESOURCE_PROBE")!="1" || Environment.GetEnvironmentVariable("FTK_MODEL_TEST")!="1" || Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT")!=root || new DirectoryInfo(root).Parent.Name!="scratch" || !Chainloader.PluginInfos.TryGetValue("com.ftkmf.runtime-model-test",out helper)||helper.Instance==null||!helper.Instance.enabled)return;
 oldTextureLimit=QualitySettings.masterTextureLimit;oldCap=Application.targetFrameRate;ready=true;enabled=true;string p=Path.Combine(root,"resource-command.json");if(File.Exists(p))last=File.ReadAllText(p);
 }
 void OnDestroy(){if(ready){Application.targetFrameRate=oldCap;QualitySettings.masterTextureLimit=oldTextureLimit;}}
 void Update(){if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+0.5f;string p=Path.Combine(root,"resource-command.json");if(!File.Exists(p))return;string body=File.ReadAllText(p);if(body==last)return;last=body;
 try{JObject c=JObject.Parse(body);string id=(string)c["id"];if(string.IsNullOrEmpty(id)||id.Length>80)return;foreach(char x in id)if(!char.IsLetterOrDigit(x)&&x!='-'&&x!='_')return;string o=Path.Combine(root,"resource-"+id+".json");if(File.Exists(o))return;
 JObject r=new JObject();string action=(string)c["action"];
 if(action=="foliage-benchmark")r=FoliageBenchmark();
 else if(action=="scroll-fixture")r=ScrollFixture();
 else if(action=="cap"){int cap=(int)c["fps"];if(cap!=-1&&cap!=30&&cap!=60&&cap!=90)throw new Exception("Unsupported cap");Application.targetFrameRate=cap;}
 else if(action=="portrait-drain-stats")r=PortraitDrainStats();else if(action=="portrait-mode")r=PortraitMode((bool)c["enabled"]);else if(action=="portrait-track")r=PortraitTrack();else if(action=="portrait-create")r=PortraitCreate();else if(action=="portrait-run")r=PortraitRun((string)c["enemy"],(int)c["count"]);else if(action=="portrait-player")r=PortraitPlayer();else if(action=="portrait-state")r=PortraitState();else if(action=="portrait-destroy")r=PortraitDestroy();else if(action=="portrait-cleanup-baseline")r=PortraitCleanupBaseline();else if(action=="texture-limit"){int limit=(int)c["limit"];if(limit<0||limit>1)throw new Exception("Unsupported texture limit");QualitySettings.masterTextureLimit=limit;r["masterTextureLimit"]=QualitySettings.masterTextureLimit;}else if(action=="texture-clone"){if(fixtureRoots.Count!=1)throw new Exception("Expected one source");GameObject clone=UnityEngine.Object.Instantiate(fixtureRoots[0]);fixtureRoots.Add(clone);foreach(MeshRenderer renderer in clone.GetComponentsInChildren<MeshRenderer>(true))fixtureTextures.Add((Texture2D)renderer.sharedMaterial.mainTexture);r=TextureState();}else if(action=="texture-destroy-source"){if(fixtureRoots.Count!=2)throw new Exception("Expected source and clone");Destroy(fixtureRoots[0]);fixtureRoots.RemoveAt(0);r["sourceDestroyRequested"]=true;}else if(action=="shared-create")r=CreateSharedTextures((bool)c["readable"]);else if(action=="texture-create")r=CreateTextures((bool)c["readable"],(string)c["loader"]=="legacy");else if(action=="texture-clear")r=ClearTextures();else if(action=="texture-state")r=TextureState();else if(action=="inventory")r=Inventory();else throw new Exception("Unsupported action");
 r["id"]=id;r["targetFrameRate"]=Application.targetFrameRate;r["utc"]=DateTime.UtcNow.ToString("o");File.WriteAllText(o,r.ToString());
 }catch(Exception e){Logger.LogWarning(e);}}
 JObject Inventory(){JArray types=new JArray();foreach(Type t in new[]{typeof(Texture2D),typeof(RenderTexture),typeof(Mesh),typeof(Material),typeof(AudioClip)}){List<JObject> rows=new List<JObject>();long total=0;foreach(UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(t)){long n=Profiler.GetRuntimeMemorySizeLong(obj);total+=n;JObject row=new JObject{{"name",obj.name},{"bytes",n},{"instance",obj.GetInstanceID()}};Texture tex=obj as Texture;if(tex!=null){row["width"]=tex.width;row["height"]=tex.height;}Texture2D tx=obj as Texture2D;if(tx!=null){row["format"]=tx.format.ToString();row["mips"]=tx.mipmapCount;}RenderTexture rt=obj as RenderTexture;if(rt!=null){row["created"]=rt.IsCreated();row["depth"]=rt.depth;row["format"]=rt.format.ToString();}rows.Add(row);}rows.Sort((a,b)=>((long)b["bytes"]).CompareTo((long)a["bytes"]));types.Add(new JObject{{"type",t.Name},{"count",rows.Count},{"bytes",total},{"objects",new JArray(rows.ToArray())}});}
 return new JObject{{"types",types},{"managedBytes",GC.GetTotalMemory(false)},{"monoHeap",Profiler.GetMonoHeapSizeLong()},{"unityAllocated",Profiler.GetTotalAllocatedMemoryLong()},{"unityReserved",Profiler.GetTotalReservedMemoryLong()},{"unityUnusedReserved",Profiler.GetTotalUnusedReservedMemoryLong()}};
 }
}}
