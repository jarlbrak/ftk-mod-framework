"""Exercise actual initial file capture/discovery guards without invoking a game loader."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--newtonsoft',type=Path,required=True);args=p.parse_args()
root=Path(__file__).parent

def method(file,signature):
    return re.search(r'    '+re.escape(signature)+r'.*?\n    }',(root/file).read_text(),re.S).group()

methods='\n'.join([method('PackageFitSession.cs',x) for x in ['static object PackageFitMember(', 'void PackageFitBeforeLoad(', 'static void PackageFitDiscoveryPostfix(', 'void PackageFitCheckFiles(']]+[method('PackageGearFixture.cs','string PackageFitFile(')])
code=r'''using System;using System.IO;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Security.Cryptography;using Newtonsoft.Json.Linq;
class Manifest {public string ModGuid,FolderPath,Version;}
class Discovered {public Manifest Manifest;public List<string> ContentFilePaths;public string BehaviorDllPath=null;}
class RuntimeModelTest {
 const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static RuntimeModelTest packageFitObserver;string root,packageFitRoot,packageFitSourceError;bool packageFitLoading,packageFitDiscovered;int packageFitLoads;
 JObject packageFitFiles,packageFitSources;
 string PackageFitConfiguredRoot(){return Path.Combine(root,"active");}
 void PackageFitItems(string hash){}
 static void CatalogNoLinks(string path){}
 static string Str(JObject o,string key){return o[key]==null?null:(string)o[key];}
 static string CatalogHash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();}
 __METHODS__
 static int checks;static void Check(bool value){if(!value)throw new Exception("check "+checks);checks++;}
 static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected);}
 static List<Discovered> Prepare(string root){
  var mods=new List<Discovered>();var packages=new JArray();
  foreach(string name in new[]{"paladin","thief"}){
   string folder=Path.Combine(root,"active/"+name);Directory.CreateDirectory(Path.Combine(folder,"assets"));
   File.WriteAllText(Path.Combine(folder,"manifest.json"),"manifest");File.WriteAllText(Path.Combine(folder,"content.json"),"content");File.WriteAllText(Path.Combine(folder,"assets/model.glb"),"original");
   packages.Add(new JObject{{"modGuid","com.ftkmf."+name},{"version","1.0.0"},{"manifest","active/"+name+"/manifest.json"},{"manifestSha256",CatalogHash(Path.Combine(folder,"manifest.json"))},{"content","active/"+name+"/content.json"},{"contentSha256",CatalogHash(Path.Combine(folder,"content.json"))}});
   mods.Add(new Discovered{Manifest=new Manifest{ModGuid="com.ftkmf."+name,FolderPath=folder,Version="1.0.0"},ContentFilePaths=new List<string>{Path.Combine(folder,"content.json")}});
  }
  File.WriteAllText(Path.Combine(root,"model-test-package-gear.json"),new JObject{{"packages",packages}}.ToString());return mods;
 }
 static RuntimeModelTest Start(string root){var p=new RuntimeModelTest{root=root};p.PackageFitBeforeLoad(Path.Combine(root,"active"));packageFitObserver=p;return p;}
 static void Main(string[] args){
  string root=args[0];var mods=Prepare(root);var p=Start(root);Check(p.packageFitFiles.Count==6 && p.packageFitLoading);
  PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitDiscovered && p.packageFitSourceError==null);
  Reject(()=>p.PackageFitBeforeLoad(Path.Combine(root,"active")));
  p=Start(root);mods[0].Manifest.FolderPath=Path.Combine(root,"dormant/paladin");PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null && !p.packageFitDiscovered);
  mods=Prepare(root);p=Start(root);mods[0].Manifest.Version="old";PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null);
  mods=Prepare(root);p=Start(root);mods[0].ContentFilePaths.Add("extra.json");PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null);
  mods=Prepare(root);p=Start(root);mods.RemoveAt(0);PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null);
  mods=Prepare(root);p=Start(root);File.WriteAllText(Path.Combine(root,"active/paladin/assets/model.glb"),"replaced after capture");PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null);
  mods=Prepare(root);p=Start(root);PackageFitDiscoveryPostfix(Path.Combine(root,"wrong-root"),mods);Check(p.packageFitSourceError!=null);
  mods=Prepare(root);p=Start(root);PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);PackageFitDiscoveryPostfix(Path.Combine(root,"active"),mods);Check(p.packageFitSourceError!=null);
  p=new RuntimeModelTest{root=root};Reject(()=>p.PackageFitBeforeLoad(Path.Combine(root,"dormant")));
  Console.WriteLine(checks+" fit initial-source/discovery checks passed.");
 }
}'''.replace('__METHODS__',methods)
with tempfile.TemporaryDirectory(prefix='ftk-fit-discovery-') as directory:
    tmp=Path(directory).resolve()
    (tmp/'Discovery.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (tmp/'Program.cs').write_text(code)
    subprocess.run(['dotnet','run','--project',str(tmp/'Discovery.csproj'),'-c','Release','--',str(tmp/'fixture')],check=True)
