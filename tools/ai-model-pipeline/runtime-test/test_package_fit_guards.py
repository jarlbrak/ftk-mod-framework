"""Execute exact production class/config guards against the game's Newtonsoft DLL."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft', type=Path, required=True)
args = parser.parse_args()
source_root = Path(__file__).parent

def method(file, signature):
    text = (source_root / file).read_text()
    return re.search(r'    ' + re.escape(signature) + r'.*?\n    }', text, re.S).group()

methods = '\n'.join([
    method('ExactClassObservation.cs', 'static FTK_playerGameStart ExactClassRow('),
    method('PackageGearFixture.cs', 'string PackageFitFile('),
    method('PackageGearFixture.cs', 'Dictionary<string, string> PackageFitItems('),
    method('PackageGearFixture.cs', 'static JObject PackageFitStableStats('),
    method('BlacksmithGearFixture.cs', 'static bool BlacksmithGrantPreservedEquals('),
])
code = r'''using System; using System.IO; using System.Reflection; using System.Collections.Generic;
using System.Security.Cryptography; using Newtonsoft.Json.Linq; using GridEditor;
namespace GridEditor {
 public class FTK_playerGameStart { public enum ID { None=-1, blacksmith=0, hunter=1 } public string m_ID; }
 public class FTK_playerGameStartDB {
  public static FTK_playerGameStartDB DB = new FTK_playerGameStartDB();
  public FTK_playerGameStart[] rows = {new FTK_playerGameStart {m_ID="blacksmith"},new FTK_playerGameStart {m_ID="hunter"},new FTK_playerGameStart {m_ID="paladin"}};
  public bool mismatch; public static FTK_playerGameStartDB GetDB(){return DB;} public int GetCount(){return rows.Length;}
  public FTK_playerGameStart GetEntryByInt(int id){return rows[id];}
  public FTK_playerGameStart GetEntryByIndex(int id){return mismatch?new FTK_playerGameStart{m_ID=rows[id].m_ID}:rows[id];}
  public FTK_playerGameStart GetEntryByStringID(string key){foreach(var row in rows)if(row.m_ID==key)return row;return null;}
  public int GetIntFromID(string key){for(int i=0;i<rows.Length;i++)if(rows[i].m_ID==key)return i;return -1;}
 }
}
namespace FTKModFramework.Core { public static class ContentRegistry {
 public static int customId=2; public static bool TryGetSyntheticId(string key,out int id,params Type[] db){id=customId;return key=="paladin";}
}}
class Program {
 const BindingFlags Statics=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 string root, packageGearConfigHash;
 static Assembly CatalogAssembly(string name){return typeof(Program).Assembly;}
 static string Str(JObject o,string k){return o[k]==null?null:(string)o[k];}
 static void CatalogKeys(JObject o,params string[] keys){foreach(var p in o.Properties())if(Array.IndexOf(keys,p.Name)<0)throw new ArgumentException("key");}
 static void CatalogNoLinks(string p){for(var current=new FileInfo(p) as FileSystemInfo;current!=null;current=Directory.GetParent(current.FullName))if(current.Exists && (current.Attributes&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("link");}
 static string CatalogHash(string p){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant();}
 __METHODS__
 static int checks;
 static void Check(bool value){if(!value)throw new Exception("Check "+checks);checks++;}
 static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected);}
 static void Main(string[] args){
  Check(ExactClassRow("blacksmith",0).m_ID=="blacksmith");
  Check(ExactClassRow("paladin",2).m_ID=="paladin");
  Reject(()=>ExactClassRow("blacksmith",1)); Reject(()=>ExactClassRow("hunter",0));
  Reject(()=>ExactClassRow("missing",2)); Reject(()=>ExactClassRow("blacksmith",-1));
  FTK_playerGameStartDB.DB.mismatch=true; Reject(()=>ExactClassRow("blacksmith",0)); FTK_playerGameStartDB.DB.mismatch=false;
  FTKModFramework.Core.ContentRegistry.customId=1;Reject(()=>ExactClassRow("paladin",2));FTKModFramework.Core.ContentRegistry.customId=2;
  var p=new Program{root=args[0]}; Directory.CreateDirectory(p.root);
  var packages=new JArray();
  foreach(var name in new[]{"paladin","thief"}) {
   string directory=Path.Combine(p.root,name);Directory.CreateDirectory(directory);
   string manifest=Path.Combine(directory,"manifest.json"),content=Path.Combine(directory,"content.json");
   File.WriteAllText(manifest,"{modGuid:'com.ftkmf."+name+"',version:'1.0.0'}");
   File.WriteAllText(content,"{entries:[{kind:'class',id:'"+name+"'},{kind:'item',id:'"+name+"_boots'}]}");
   packages.Add(new JObject{{"modGuid","com.ftkmf."+name},{"version","1.0.0"},{"manifest",name+"/manifest.json"},{"manifestSha256",CatalogHash(manifest)},{"content",name+"/content.json"},{"contentSha256",CatalogHash(content)}});
  }
  string config=Path.Combine(p.root,"model-test-package-gear.json");
  Action write=()=>File.WriteAllText(config,new JObject{{"packages",packages}}.ToString());write();
  string pin=CatalogHash(config);Check(p.PackageFitItems(pin).Count==2);
  Reject(()=>p.PackageFitItems(new string('0',64)));
  packages[0]["version"]="other";write();Reject(()=>p.PackageFitItems(CatalogHash(config)));p.packageGearConfigHash=null;Reject(()=>p.PackageFitItems(CatalogHash(config)));
  packages[0]["version"]="1.0.0";packages[1]["modGuid"]="com.ftkmf.paladin";write();Reject(()=>p.PackageFitItems(CatalogHash(config)));
  packages[1]["modGuid"]="com.ftkmf.thief";write();
  string contentFile=Path.Combine(p.root,"paladin/content.json"); File.WriteAllText(contentFile,"{entries:[{kind:'item',id:'thief_wrong'}]}");
  Reject(()=>p.PackageFitItems(CatalogHash(config)));packages[0]["contentSha256"]=CatalogHash(contentFile);write();Reject(()=>p.PackageFitItems(CatalogHash(config)));
  File.WriteAllText(contentFile,"{entries:[{kind:'item',id:'paladin_x'},{kind:'item',id:'paladin_x'}]}");packages[0]["contentSha256"]=CatalogHash(contentFile);write();Reject(()=>p.PackageFitItems(CatalogHash(config)));
  Reject(()=>p.PackageFitFile("../escape.json",new string('0',64)));Reject(()=>p.PackageFitFile(contentFile,CatalogHash(contentFile)));
  var before=JObject.Parse("{m_PlayerLevel:3,m_PlayerXP:4,m_CharacterClass:2,m_AugmentedVitality:0.1,m_BaseVitality:0.6,m_Gold:20,_ModQuickness:0,m_BaseQuickness:0.56,m_AugmentedQuickness:0,m_ModVitality:0.3,m_HealthCurrent:40}");
  var after=JObject.Parse(before.ToString());after["m_ModVitality"]=0.4;after["m_HealthCurrent"]=30;
  Check(BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));
  after["_ModQuickness"]=0.01;Check(BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));
  after["m_BaseQuickness"]=0.57;Check(!BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));after["m_BaseQuickness"]=0.56;
  after["m_AugmentedQuickness"]=0.01;Check(!BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));after["m_AugmentedQuickness"]=0;
  after["_OtherQuickness"]=0.01;Check(!BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));after.Remove("_OtherQuickness");
  after["m_Gold"]=21;Check(!BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));
  after=JObject.Parse(before.ToString());after["m_AugmentedVitality"]=0.2;Check(!BlacksmithGrantPreservedEquals(PackageFitStableStats(before),PackageFitStableStats(after)));
  Console.WriteLine(checks+" package fit guard checks passed against "+typeof(JToken).Assembly.FullName);
 }
}'''.replace('__METHODS__', methods)
with tempfile.TemporaryDirectory(prefix='ftk-fit-guards-') as directory:
    root = Path(directory).resolve()
    (root/'Guard.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (root/'Program.cs').write_text(code)
    subprocess.run(['dotnet','run','--project',str(root/'Guard.csproj'),'-c','Release','--',str(root/'fixture')],check=True)
