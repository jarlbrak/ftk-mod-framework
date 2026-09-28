"""Execute the production diagnostic with fake Unity objects and failure injection."""
import argparse
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft',type=Path,required=True)
args=parser.parse_args()
source=Path(__file__).with_name('StudioMaterialDiagnostic.cs')
code=r'''
using System; using System.Collections.Generic; using UnityEngine; using Newtonsoft.Json.Linq;
namespace UnityEngine {
 public class Object { public static int destroyed; public int GetInstanceID(){return 7;} public static void DestroyImmediate(Object o){destroyed++;} }
 public class Transform { public string path; }
 public class GameObject { public bool activeInHierarchy=true; }
 public class Mesh { public string name="native"; public int subMeshCount=3; }
 public class Shader { public string name="Standard"; public bool isSupported=true; }
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
 public class Material:Object {
  public string name; public Color color; public Shader shader; public int renderQueue;public string[] shaderKeywords;
  public string missingProperty;public string renderType; public Dictionary<string,float> floats=new Dictionary<string,float>();
  public Dictionary<string,Color> colors=new Dictionary<string,Color>();public Dictionary<string,Object> maps=new Dictionary<string,Object>();
  public Material(Shader shader){this.shader=shader;maps["_MainTex"]=new Object();maps["_EmissionMap"]=new Object();}
  public bool HasProperty(string property){return property!=missingProperty;}public void SetColor(string p,Color c){colors[p]=c;}
  public void SetFloat(string p,float f){floats[p]=f;}public float GetFloat(string p){return floats[p];}
  public void SetTexture(string p,Object o){maps[p]=o;}public Object GetTexture(string p){return maps[p];}
  public void SetOverrideTag(string p,string s){renderType=s;}public string GetTag(string p,bool search){return renderType;}
 }
 public class SkinnedMeshRenderer:Object {
  public Transform transform=new Transform{path="player_Busker"}; public Mesh sharedMesh=new Mesh(); public bool enabled=true;
  public GameObject gameObject=new GameObject(); public int writes,failWrite; public bool failAfter; public Material[] assigned;
  public Material[] sharedMaterials {get{return (Material[])assigned.Clone();} set{writes++;if(writes==failWrite && !failAfter)throw new Exception("setter failure");assigned=(Material[])value.Clone();if(writes==failWrite && failAfter)throw new Exception("setter failure after assignment");}}
 }
 public static class Time {public static int frameCount=12;}
}
class CharacterOverworld {public bool IsOwner=true; public CharacterEventListener m_Avatar;}
class OffscreenCamera {public GameObject m_TargetObject;}
class CharacterEventListener:UnityEngine.Object {
 public CharacterOverworld m_CharacterOverworld; public OffscreenCamera m_OffscreenCamera;
 public GameObject gameObject=new GameObject();public Transform transform=new Transform();public SkinnedMeshRenderer[] renderers;
 public T[] GetComponentsInChildren<T>(bool inactive){return (T[])(object)renderers;}
}
class LeaseObservationPin {public static int ExactId(JObject o,string k,bool optional){if(o[k]==null || o[k].Type!=JTokenType.Integer || (int)o[k]==0)throw new ArgumentException();return (int)o[k];}}
public sealed partial class RuntimeModelTest {
 bool packageFitUncertain;JObject packageFitFailure;
 static void CatalogKeys(JObject o,params string[] keys){foreach(var p in o.Properties())if(Array.IndexOf(keys,p.Name)<0)throw new ArgumentException("unknown field");}
 static string Str(JObject o,string k){return o[k]==null?null:(string)o[k];}
 static string Relative(Transform t,Transform root){return t.path;}
 static JObject ItemMaterialObservation(Material m){return new JObject{{"name",m.name}};}
 static int checks;static void Check(bool ok){if(!ok)throw new Exception("check "+checks);checks++;}
 static void Reject(Action a){bool failed=false;try{a();}catch(Exception){failed=true;}Check(failed);}
 static JObject Request(){return JObject.Parse("{inventoryCelInstanceId:7,rendererPath:'player_Busker',materialIndex:1,expectedMaterialName:'hair (Instance)'}");}
 static CharacterEventListener Avatar(){var a=new CharacterEventListener();a.m_CharacterOverworld=new CharacterOverworld{m_Avatar=new CharacterEventListener()};a.m_OffscreenCamera=new OffscreenCamera{m_TargetObject=a.gameObject};a.renderers=new[]{new SkinnedMeshRenderer{assigned=new[]{new Material(new Shader()){name="body"},new Material(new Shader()){name="hair (Instance)"},new Material(new Shader()){name="skin"}}}};return a;}
 static void Invalid(Action<CharacterEventListener,JObject> change){var p=new RuntimeModelTest();var a=Avatar();var r=Request();change(a,r);Reject(()=>p.RenderStudioMaterialDiagnostic(a,r,()=>{throw new Exception("must not render");}));Check(a.renderers[0].writes==0);}
 public static void Main(){
  var p=new RuntimeModelTest();var a=Avatar();var r=Request();var renderer=a.renderers[0];var original=renderer.sharedMaterials;int renders=0;UnityEngine.Object.destroyed=0;
  var receipt=p.RenderStudioMaterialDiagnostic(a,r,()=>{renders++;Check(renderer.assigned[1]!=original[1]);Check(renderer.assigned[0]==original[0] && renderer.assigned[2]==original[2]);
   var m=renderer.assigned[1];Check(m.shader==original[1].shader);Check(m.color.r==1 && m.color.g==0 && m.color.b==1 && m.color.a==1);
   var e=m.colors["_EmissionColor"];Check(e.r==1 && e.g==0 && e.b==1 && e.a==1);
   Check(m.GetFloat("_Metallic")==0 && m.GetFloat("_Glossiness")==0);Check(m.GetFloat("_Mode")==0 && m.GetFloat("_SrcBlend")==1 && m.GetFloat("_DstBlend")==0 && m.GetFloat("_ZWrite")==1);
   Check(m.renderQueue==2000 && m.renderType=="Opaque");Check(m.shaderKeywords.Length==1 && m.shaderKeywords[0]=="_EMISSION");
   Check(m.maps.Count==10);foreach(var entry in m.maps)Check(entry.Value==null);
   Check(original[1].maps["_MainTex"]!=null && original[1].floats.Count==0 && original[1].colors.Count==0);});
  Check(renders==1 && renderer.writes==2);Check(renderer.assigned[1]==original[1]);Check((bool)receipt["sharedMaterialsRestored"] && !(bool)receipt["artAcceptanceEligible"]);Check(UnityEngine.Object.destroyed==1 && !p.packageFitUncertain);
  Reject(()=>p.RenderStudioMaterialDiagnostic(a,r,()=>{throw new Exception("render failed");}));Check(renderer.assigned[1]==original[1] && UnityEngine.Object.destroyed==2 && !p.packageFitUncertain);
  a=Avatar();renderer=a.renderers[0];original=renderer.sharedMaterials;renderer.failWrite=1;renderer.failAfter=true;
  Reject(()=>p.RenderStudioMaterialDiagnostic(a,r,()=>{throw new Exception("not reached");}));Check(renderer.assigned[1]==original[1] && renderer.writes==2 && !p.packageFitUncertain);
  Invalid((x,q)=>q["inventoryCelInstanceId"]=8);Invalid((x,q)=>q.Remove("inventoryCelInstanceId"));
  Invalid((x,q)=>x.m_CharacterOverworld.m_Avatar=x);Invalid((x,q)=>x.m_CharacterOverworld.IsOwner=false);Invalid((x,q)=>x.m_OffscreenCamera.m_TargetObject=new GameObject());
  Invalid((x,q)=>q["rendererPath"]="other");Invalid((x,q)=>x.renderers=new[]{x.renderers[0],x.renderers[0]});
  Invalid((x,q)=>x.renderers[0].enabled=false);Invalid((x,q)=>x.renderers[0].gameObject.activeInHierarchy=false);Invalid((x,q)=>x.renderers[0].sharedMesh.name="ftkmf_custom");
  Invalid((x,q)=>q["materialIndex"]=-1);Invalid((x,q)=>q["materialIndex"]=32);Invalid((x,q)=>q["materialIndex"]=1.1);Invalid((x,q)=>q["materialIndex"]=3);Invalid((x,q)=>x.renderers[0].sharedMesh.subMeshCount=1);
  Invalid((x,q)=>q["expectedMaterialName"]="Hair (Instance)");Invalid((x,q)=>x.renderers[0].assigned[1]=null);Invalid((x,q)=>q["unknown"]=true);
  Invalid((x,q)=>x.renderers[0].assigned[1].shader=null);Invalid((x,q)=>x.renderers[0].assigned[1].shader.isSupported=false);
  Invalid((x,q)=>x.renderers[0].assigned[1].shader.name="UI/Default");Invalid((x,q)=>x.renderers[0].assigned[1].missingProperty="_ZWrite");
  a=Avatar();renderer=a.renderers[0];renderer.failWrite=2;int beforeDestroy=UnityEngine.Object.destroyed;
  Reject(()=>p.RenderStudioMaterialDiagnostic(a,r,()=>{}));Check(p.packageFitUncertain && p.packageFitFailure!=null);Check(UnityEngine.Object.destroyed==beforeDestroy+1);
  a=Avatar();Reject(()=>p.RenderStudioMaterialDiagnostic(a,r,()=>{}));Check(a.renderers[0].writes==0);
  Console.WriteLine(checks+" material diagnostic identity and rollback checks passed");
 }
}
'''
with tempfile.TemporaryDirectory(prefix='ftk-studio-material-') as directory:
    root=Path(directory)
    (root/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="'+escape(str(source.resolve()))+'" Link="Production.cs"/><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (root/'Program.cs').write_text(code)
    subprocess.run(['dotnet','run','--project',str(root/'Test.csproj'),'-c','Release'],check=True)
