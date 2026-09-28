"""Execute production fit file-pinning and uncertainty guards with failed native calls."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft', type=Path, required=True)
args = parser.parse_args()
source = (Path(__file__).parent / 'PackageFitSession.cs').read_text()

def method(signature):
    return re.search(r'    '+re.escape(signature)+r'.*?\n    }', source, re.S).group()

methods = '\n'.join(method(x) for x in ['void PackageFitCheckFiles(', 'void PackageFitBeginMutation(',
    'JObject PackageFitFailureReceipt(', 'JObject GuardPackageFitCommand(', 'void PackageFitCommandBoundary(',
    'static JArray PackageFitDifferences(', 'static void PackageFitCollectDifferences('])
methods += '\n' + re.search(r'    static bool BlacksmithGrantPreservedEquals\(.*?\n    }', (Path(__file__).parent / 'BlacksmithGearFixture.cs').read_text(), re.S).group()
code = r'''using System; using System.Collections.Generic; using System.IO; using System.Security.Cryptography; using Newtonsoft.Json.Linq;
class CharacterOverworld { public int items; }
class Program {
 JObject packageFitFiles, packageFitFailure, packageFitMutationBefore, packageFitMutationDiagnostics; bool packageFitUncertain, packageFitMutationVerified; CharacterOverworld packageFitMutationHero;
 string packageFitMutationOperation; int mode;
 static void CatalogNoLinks(string p){}
 static string CatalogHash(string p){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant();}
 static JObject PackageFitMutationSnapshot(CharacterOverworld hero){return new JObject{{"preserved",new JObject{{"items",hero.items}}},{"stats",new JObject{{"m_Gold",10}}}};}
 JObject BlacksmithAppearanceFixture(JObject command,bool fit){return PackageGearFixture(command,true);}
 JObject PackageGearFixture(JObject command,bool equip){
  if(mode==0)throw new ArgumentException("preflight rejected");
  var hero=new CharacterOverworld();PackageFitBeginMutation(hero,"test");hero.items++;
  if(mode==1)throw new InvalidOperationException("native failed after Add");
  if(mode==2)throw new InvalidOperationException("postcondition failed after native return");
  packageFitMutationVerified=true;if(mode==4)throw new InvalidOperationException("receipt failure");return new JObject{{"ok",true}};
 }
 __METHODS__
 static int checks;static void Check(bool value){if(!value)throw new Exception("Check "+checks);checks++;}
 static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}catch(IOException){rejected=true;}Check(rejected);}
 static void Main(string[] args){
  var p=new Program();string file=Path.Combine(args[0],"model.glb");File.WriteAllText(file,"initial");
  p.packageFitFiles=new JObject{{file,CatalogHash(file)}};p.PackageFitCheckFiles();checks++;
  File.WriteAllText(file,"changed");Reject(()=>p.PackageFitCheckFiles());
  File.Delete(file);Reject(()=>p.PackageFitCheckFiles());
  p.mode=0;Reject(()=>p.GuardPackageFitCommand(new JObject(),0));Check(!p.packageFitUncertain);
  p.mode=1;var failed=p.GuardPackageFitCommand(new JObject(),0);Check(!(bool)failed["ok"] && (bool)failed["uncertain"]);
  Check((int)failed["failure"]["partialState"]["items"]==1);
  Check((int)failed["failure"]["before"]["preserved"]["items"]==0);
  Check((string)failed["failure"]["differences"][0]["path"]=="$.preserved.items");
  var differences=PackageFitDifferences(JObject.Parse("{a:null,b:[1,2],c:{gold:10}}"),JObject.Parse("{b:[1,3],c:{gold:11},d:null}"));
  Check(differences.Count==4);Check((bool)differences[0]["beforePresent"] && !(bool)differences[0]["afterPresent"]);
  Check((string)differences[1]["path"]=="$.b[1]");Check((string)differences[2]["path"]=="$.c.gold");
  Reject(()=>p.GuardPackageFitCommand(new JObject(),0));Reject(()=>p.GuardPackageFitCommand(new JObject(),1));Reject(()=>p.GuardPackageFitCommand(new JObject(),2));
  foreach(string op in new[]{"package-gear-grant","package-gear-equip","package-gear-appearance","blacksmith-gear-grant","native-save-exit","native-inventory","return-to-title"})Reject(()=>p.PackageFitCommandBoundary(op));
  foreach(string op in new[]{"package-gear-state","class-appearance-roster","item-visual-state"}){p.PackageFitCommandBoundary(op);checks++;}
  p=new Program{mode=2};failed=p.GuardPackageFitCommand(new JObject(),1);Check((bool)failed["requiresFreshSession"] && p.packageFitUncertain);
  p=new Program{mode=4};failed=p.GuardPackageFitCommand(new JObject(),1);Check(p.packageFitUncertain);
  p=new Program{mode=3};Check((bool)p.GuardPackageFitCommand(new JObject(),1)["ok"] && !p.packageFitUncertain);
  p.PackageFitBeginMutation(new CharacterOverworld(),"appearance");Reject(()=>p.PackageFitBeginMutation(new CharacterOverworld(),"restore"));
  Console.WriteLine(checks+" fit session failure/file pin checks passed against "+typeof(JToken).Assembly.FullName);
 }
}'''.replace('__METHODS__', methods)
with tempfile.TemporaryDirectory(prefix='ftk-fit-session-') as directory:
    root = Path(directory).resolve()
    (root/'Session.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(args.newtonsoft.resolve(strict=True)))+'</HintPath></Reference></ItemGroup></Project>')
    (root/'Program.cs').write_text(code)
    subprocess.run(['dotnet','run','--project',str(root/'Session.csproj'),'-c','Release','--',str(root)],check=True)
