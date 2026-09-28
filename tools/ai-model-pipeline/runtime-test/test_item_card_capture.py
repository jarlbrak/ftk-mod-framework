"""Execute production item texture guards against missing/stale/changed native identities."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).parent
source = (root / 'ItemVisualObservation.cs').read_text()
def extract(source, signature):
    return re.search(r'    ' + re.escape(signature) + r'.*?\n    }', source, re.S).group()
methods = '\n'.join(extract(source, sig) for sig in (
    'static void ObserveNativeItemRenderCompleted(', 'JObject CaptureNativeItemCard('))
methods += extract((root/'NativeInventoryCapture.cs').read_text(), 'static JObject NativeInventoryPixelSummary(')
code = r'''using System; using System.IO; using System.Security.Cryptography; using Newtonsoft.Json.Linq;
class Obj { public int id=1; public int GetInstanceID(){return id;} }
class GameObject:Obj { public bool activeInHierarchy=true; }
class Texture2D:Obj { public int width=812,height=692; public string format="ARGB32"; public bool blank; public Action onEncode;
 public Color32[] GetPixels32(){return blank ? new[]{new Color32(),new Color32()} : new[]{new Color32(),new Color32{r=255,a=255}};}
 public byte[] EncodeToPNG(){if(onEncode!=null)onEncode();return new byte[32];} }
struct Color32 { public byte r,g,b,a; }
class Rect { public float width=406.5f,height=346.5f; }
class RectTransform { public Rect rect=new Rect(); }
class RawImage { public object texture; public RectTransform rectTransform=new RectTransform(); }
class FTKHub { public static FTKHub Instance=new FTKHub(); public int m_OffscreenPortraitAA=2; }
class OffscreenCamera:Obj { public string CameraID="Item,812,692"; public Texture2D m_Texture2D; public GameObject m_TargetObject; }
class CharacterOverworld { public int owned=1; }
class BlacksmithGearEntry { public int itemId=7,numericId=7; }
class uiInventoryItemDisplay { public RawImage m_ItemDisplay=new RawImage(); public OffscreenCamera m_OffscreenCamera;
 public GameObject gameObject=new GameObject(); public int item=7; public object m_LastOwner; public int GetCurrentItemID(){return item;} }
class uiPlayerInventory { public static uiPlayerInventory Instance; public bool m_IsShowing=true; public CharacterOverworld owner; public object transform=new object(); }
class FTKUI { public static FTKUI Instance; public uiInventoryItemDisplay m_ItemCardDisplay; }
static class Time { public static int frameCount=10; }
static class QualitySettings { public static string activeColorSpace="Linear"; }
class RuntimeModelTest {
 static RuntimeModelTest itemCardObserver; JArray itemCardRenderObservations; int itemCardObservedItemId=7; string output;
 static void CatalogNoLinks(string p){} static string Token(string p){return p;} static string Str(JObject j,string k){return (string)j[k];}
 static object Field(uiPlayerInventory inv,string name){return inv.owner;} static int OwnedAcrossEquipment(CharacterOverworld h,int id){return h.owned;}
 static string CatalogHash(string p){return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));}
 __METHODS__
 static int checks; static void Check(bool v){checks++;if(!v)throw new Exception("Check "+checks);}
 static void Reject(Action a){bool fail=false;try{a();}catch(InvalidOperationException){fail=true;}catch(IOException){fail=true;}Check(fail);}
 static void Main(string[] args){
  var p=new RuntimeModelTest{output=args[0]}; var hero=new CharacterOverworld(); var gear=new BlacksmithGearEntry();
  var tex=new Texture2D{id=4};var camera=new OffscreenCamera{id=2,m_Texture2D=tex,m_TargetObject=new GameObject{id=3}};
  var card=new uiInventoryItemDisplay{m_OffscreenCamera=camera};card.m_ItemDisplay.texture=tex;
  uiPlayerInventory.Instance=new uiPlayerInventory{owner=hero};card.m_LastOwner=uiPlayerInventory.Instance.transform;
  FTKUI.Instance=new FTKUI{m_ItemCardDisplay=card};itemCardObserver=p;
  var render=JObject.Parse("{cameraId:'Item,812,692',offscreenCameraInstanceId:2,targetInstanceId:3,textureInstanceId:4,renderFrame:10,completed:false}");
  p.itemCardRenderObservations=new JArray(render);var cmd=new JObject{{"id","capture"}};
  Reject(()=>p.CaptureNativeItemCard(cmd,card,hero,gear));
  ObserveNativeItemRenderCompleted(camera,render);Check((bool)render["completed"]);
  Action capture=()=>p.CaptureNativeItemCard(cmd,card,hero,gear);
  foreach(string key in new[]{"offscreenCameraInstanceId","textureInstanceId","renderFrame"}){
   int prior=(int)render[key];render[key]=999;Reject(capture);render[key]=prior;
  }
  render["observationError"]="failed";Reject(capture);render.Remove("observationError");
  foreach(string bad in new[]{"Item","Other,812,692","Item,812,693","Item,813,692","Item,812,692,extra"}){
   render["cameraId"]=bad;Reject(capture);
  }
  render["cameraId"]="Item,812,692";
  camera.CameraID="Item,812,693";Reject(capture);camera.CameraID="Item,812,692";
  tex.width=811;Reject(capture);tex.width=812;
  FTKHub.Instance.m_OffscreenPortraitAA=1;Reject(capture);FTKHub.Instance.m_OffscreenPortraitAA=2;
  p.itemCardRenderObservations.Add(render.DeepClone());Reject(capture);p.itemCardRenderObservations.RemoveAt(1);
  tex.blank=true;Reject(capture);tex.blank=false;tex.width=4096;Reject(capture);tex.width=812;
  tex.onEncode=()=>card.item=8;Reject(capture);card.item=7;
  tex.onEncode=()=>uiPlayerInventory.Instance.owner=new CharacterOverworld();Reject(capture);uiPlayerInventory.Instance.owner=hero;
  tex.onEncode=()=>card.m_ItemDisplay.texture=new Texture2D();Reject(capture);card.m_ItemDisplay.texture=tex;
  tex.onEncode=()=>Time.frameCount++;Reject(capture);Time.frameCount=10;tex.onEncode=null;
  Check(!File.Exists(Path.Combine(args[0],"capture.item-display.png")));
  var result=p.CaptureNativeItemCard(cmd,card,hero,gear);Check((bool)result["imageEvidenceAvailable"]);Check((string)result["cameraId"]=="Item,812,692");Check((int)result["bytes"]==32);Reject(capture);
  foreach(string key in new[]{"offscreenCameraInstanceId","targetInstanceId","textureInstanceId","renderFrame"}){
   int prior=(int)render[key];render[key]=999;ObserveNativeItemRenderCompleted(camera,render);Check(!(bool)render["completed"]);render[key]=prior;
  }
  camera.m_Texture2D=null;ObserveNativeItemRenderCompleted(camera,render);Check(!(bool)render["completed"]);
  Console.WriteLine(checks+" production item-card capture/completion guard checks passed; Unity render/Harmony integration remains a live gate.");
 }
}'''.replace('__METHODS__', methods)
with tempfile.TemporaryDirectory(prefix='ftk-item-card-') as directory:
    path = Path(directory)
    (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (path/'Program.cs').write_text(code)
    subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release','--',str(path)],check=True)
