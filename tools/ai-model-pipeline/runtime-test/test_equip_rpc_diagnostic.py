"""Execute production equip breadcrumbs with passive native-cache stand-ins."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).parent
source=(root/'EquipRpcDiagnostic.cs').read_text()
fixture=(root/'BlacksmithGearFixture.cs').read_text()
method=re.search(r'    static void EquipOwnedBlacksmithItem\(.*?\n    }',fixture,re.S).group()
assert method.index('"before-force-equip"') < method.index('hero.ForceEquip') < method.index('"after-force-equip"')
assert 'catch' not in method and 'finally' not in method
for forbidden in ['RefreshRpc', 'GetFunctionPointer', 'PrepareMethod', '.Invoke(', 'harmony.Patch', 'view.prefix ', 'view.isMine']:
    assert forbidden not in source, forbidden
plugin=(root/'Plugin.cs').read_text()
assert plugin.index('MODEL TEST REFUSED') < plugin.index('equipRpcDiagnosticEnabled =')
assert 'Environment.GetEnvironmentVariable(EquipRpcDiagnosticFlag) == "1"' in plugin
package=(root/'PackageGearFixture.cs').read_text()
assert package.count('CreateEquipRpcTrace(command, changed.Count)')==2
stubs=r'''using System; using System.Collections.Generic; using System.IO; using System.Reflection; using Newtonsoft.Json.Linq;
namespace UnityEngine { public class Component {public int GetInstanceID(){return 42;}} public static class Time {public static int frameCount=15;} }
namespace GridEditor {public class FTK_itembase {public enum ID{Armor=20}}}
namespace HarmonyLib {public class Patches{public string[] Owners=new[]{"test-owner"};} public static class Harmony{public static Patches GetPatchInfo(MethodInfo m){return new Patches();}}}
class ItemContainer{public int count;}
class PlayerInventory{public enum ContainerID{Backpack,Body} public ItemContainer bag=new ItemContainer{count=1},body=new ItemContainer();public ItemContainer Get(ContainerID c){return c==ContainerID.Backpack?bag:body;}}
class Stats{public int m_CharacterClass=11;} class Row{public string m_ID="gladiator";}
class CharacterOverworld:UnityEngine.Component {
 public PlayerInventory m_PlayerInventory=new PlayerInventory();public Stats m_CharacterStats=new Stats();public PhotonView view=new PhotonView();public Action before;public Exception failure;public int calls;
 public T GetComponent<T>() where T:class{return view as T;} public Row GetDBEntry(){return new Row();}
 public void ForceEquip(GridEditor.FTK_itembase.ID item,bool swap){calls++;if(before!=null)before();if(failure!=null)throw failure;m_PlayerInventory.bag.count--;m_PlayerInventory.body.count++;}
 public void EquipItem(GridEditor.FTK_itembase.ID item,bool swap){} public void EquipItemRPC(GridEditor.FTK_itembase.ID item,bool swap){}
}
class PhotonView:UnityEngine.Component {public int viewID=1001,ownerId=1,prefixBackup=-1;public UnityEngine.Component[] RpcMonoBehaviours;public List<UnityEngine.Component> ObservedComponents=new List<UnityEngine.Component>();public int prefix{get{throw new Exception("Mutating getter read");}}}
class PhotonNetwork{public static NetworkingPeer networkingPeer=new NetworkingPeer();}
class NetworkingPeer{public Dictionary<Type,List<MethodInfo>> monoRPCMethodsCache=new Dictionary<Type,List<MethodInfo>>();public void ExecuteRpc(object data,object sender){}}
class Extensions{public static Dictionary<MethodInfo,ParameterInfo[]> ParametersOfMethods=new Dictionary<MethodInfo,ParameterInfo[]>();}
class Log{public void LogWarning(string s){}}
public sealed partial class RuntimeModelTest {
 const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,Statics=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 string output,sessionId="session",packageGearConfigHash="hash";Log Logger=new Log();
 static string Str(JObject o,string k){return (string)o[k];}
 class BlacksmithGearEntry{public string stringId="armor";public int numericId=20,ownedCount=1;public GridEditor.FTK_itembase.ID itemId=GridEditor.FTK_itembase.ID.Armor;public PlayerInventory.ContainerID container=PlayerInventory.ContainerID.Body;}
 static int ExactCount(ItemContainer c,GridEditor.FTK_itembase.ID id){return c.count;}
 static int OwnedAcrossEquipment(CharacterOverworld h,GridEditor.FTK_itembase.ID id){return h.m_PlayerInventory.bag.count+h.m_PlayerInventory.body.count;}
 __METHOD__
 static int checks;static void Check(bool v){checks++;if(!v)throw new Exception("Check "+checks);}
 static void Main(string[] args){
  var p=new RuntimeModelTest{output=args[0]};var command=new JObject{{"id","request"}};var entry=new BlacksmithGearEntry();string path=Path.Combine(args[0],"session.equip-rpc.jsonl");
  Check(p.CreateEquipRpcTrace(command,0)==null);var ordinary=new CharacterOverworld();EquipOwnedBlacksmithItem(ordinary,entry);Check(ordinary.calls==1&&!File.Exists(path));
  p.equipRpcDiagnosticEnabled=true;var hero=new CharacterOverworld();hero.view.RpcMonoBehaviours=new UnityEngine.Component[]{hero};hero.view.ObservedComponents.Add(hero);
  var method=typeof(CharacterOverworld).GetMethod("EquipItemRPC");var cache=PhotonNetwork.networkingPeer.monoRPCMethodsCache;cache[typeof(CharacterOverworld)]=new List<MethodInfo>{method};Extensions.ParametersOfMethods[method]=method.GetParameters();
  hero.before=()=>{var line=JObject.Parse(File.ReadAllLines(path)[0]);Check((string)line["phase"]=="before-force-equip");Check((string)line["commandId"]=="request"&&(int)line["arguments"][0]==20&&!(bool)line["arguments"][1]);Check((int)line["heroInstanceId"]==42);};
  EquipOwnedBlacksmithItem(hero,entry,p.CreateEquipRpcTrace(command,0));Check(hero.calls==1);var lines=File.ReadAllLines(path);Check(lines.Length==2);var before=JObject.Parse(lines[0]);Check(before["snapshotError"]==null);Check((string)JObject.Parse(lines[1])["phase"]=="after-force-equip");
  var candidate=before["rpcSnapshot"]["cachedComponents"][0]["equipCandidates"][0];Check((bool)candidate["parameterCachePresent"]);Check((string)candidate["harmonyOwners"][0]=="test-owner"&&((string)candidate["managedIlSha256"]).Length==64);Check(cache.Count==1&&Extensions.ParametersOfMethods.Count==1&&hero.view.prefixBackup==-1);
  EquipOwnedBlacksmithItem(hero,entry,p.CreateEquipRpcTrace(command,1));Check(hero.calls==1&&File.ReadAllLines(path).Length==2);
  var failed=new CharacterOverworld{failure=new InvalidOperationException("native failure")};Exception caught=null;try{EquipOwnedBlacksmithItem(failed,entry,p.CreateEquipRpcTrace(command,2));}catch(Exception e){caught=e;}Check(ReferenceEquals(caught,failed.failure)&&failed.calls==1);Check(File.ReadAllLines(path).Length==3);Check((string)JObject.Parse(File.ReadAllLines(path)[2])["phase"]=="before-force-equip");
  PhotonNetwork.networkingPeer=null;var noCache=new CharacterOverworld();EquipOwnedBlacksmithItem(noCache,entry,p.CreateEquipRpcTrace(command,3));Check(noCache.calls==1);Check(JObject.Parse(File.ReadAllLines(path)[3])["snapshotError"]!=null);
  p.output=Path.Combine(args[0],"missing");var noFile=new CharacterOverworld();EquipOwnedBlacksmithItem(noFile,entry,p.CreateEquipRpcTrace(command,4));Check(noFile.calls==1);
  Console.WriteLine(checks+" equip RPC diagnostic checks passed; native metadata remains a live gate.");
 }
}'''.replace('__METHOD__',method)
with tempfile.TemporaryDirectory(prefix='ftk-equip-rpc-') as directory:
    path=Path(directory)
    (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (path/'Observer.cs').write_text(source)
    (path/'Program.cs').write_text(stubs)
    subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release','--',str(path)],check=True)
