"""Run the opt-in production observer with stubbed patch installation, preserving original exceptions."""
from pathlib import Path
import re
import subprocess
import tempfile

root = Path(__file__).parent
source = (root/'NullInputCallerDiagnostic.cs').read_text()
plugin = (root/'Plugin.cs').read_text()
assert plugin.index('MODEL TEST REFUSED') < plugin.index('InstallNullInputCallerDiagnostic();')
assert 'void OnDestroy(){RemoveNullInputCallerDiagnostic();' in plugin
prefix = source[source.index('static void ObserveNullInputCaller'):source.index('void RemoveNullInputCallerDiagnostic')]
code_only = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', prefix)
for forbidden in ['ref string', 'out string', '__result', '__exception', 'return false', 'UnityEngine', 'FTKInput.', 'GetComponent', 'Time.frameCount']:
    assert forbidden not in code_only, forbidden
assert 'static void ObserveNullInputCaller(string __1)' in prefix
assert 'ReferenceEquals(observer, null)' in prefix
assert 'observer == null' not in prefix
assert '!ReferenceEquals(nullInputCallerObserver, null)' in source
stubs = r'''using System; using System.Collections.Generic; using System.IO; using System.Reflection;
namespace Rewired { public class Player {} }
namespace HarmonyLib {
 public static class Priority { public const int Last=0; }
 public class HarmonyMethod { public int priority; public MethodInfo method; public HarmonyMethod(MethodInfo m){method=m;} }
 public class Harmony {
  public static int installs,removals; public static bool fail; public static MethodInfo observed; public Harmony(string id){}
  public void Patch(MethodInfo m,HarmonyMethod prefix,object postfix,object transpiler,object finalizer,object manipulator){
   installs++;observed=m;if(postfix!=null||finalizer!=null)throw new Exception("Unexpected patch");if(fail)throw new Exception("Patch unavailable");}
  public void Unpatch(MethodInfo m,MethodInfo prefix){removals++;}
 }
}
public class FTKInput {
 public int calls;
 public bool GetButton(string action){throw new Exception("Wrong overload");}
 public bool GetButton(Rewired.Player player,string action){calls++;return new Dictionary<string,int>().ContainsKey(action);}
}
class Log { public bool fail; public List<string> warnings=new List<string>();public void LogInfo(string s){if(fail)throw new Exception("log unavailable");} public void LogWarning(string s){if(fail)throw new Exception("log unavailable");warnings.Add(s);} }
public sealed partial class RuntimeModelTest {
 public static bool operator ==(RuntimeModelTest a,RuntimeModelTest b){throw new Exception("Unity equality dispatched");}
 public static bool operator !=(RuntimeModelTest a,RuntimeModelTest b){throw new Exception("Unity equality dispatched");}
 public override bool Equals(object value){return ReferenceEquals(this,value);}
 public override int GetHashCode(){return base.GetHashCode();}
 const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 const BindingFlags Statics=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 string sessionId="test-session",output; Log Logger=new Log();
 static int checks;static void Check(bool v){checks++;if(!v)throw new Exception("Check "+checks);}
 static void OriginalCall(FTKInput input){ObserveNullInputCaller(null);input.GetButton(new Rewired.Player(),null);}
 public static void Main(string[] args){
  var p=new RuntimeModelTest{output=args[0]};
  foreach(string flag in new[]{null,"0","true"}){Environment.SetEnvironmentVariable(NullInputCallerFlag,flag);p.InstallNullInputCallerDiagnostic();Check(HarmonyLib.Harmony.installs==0);}
  ObserveNullInputCaller(null);Check(p.Logger.warnings.Count==0);
  Environment.SetEnvironmentVariable(NullInputCallerFlag,"1");p.InstallNullInputCallerDiagnostic();Check(HarmonyLib.Harmony.installs==1);
  var parameters=HarmonyLib.Harmony.observed.GetParameters();Check(parameters.Length==2&&parameters[0].ParameterType==typeof(Rewired.Player)&&parameters[1].ParameterType==typeof(string));
  ObserveNullInputCaller("Confirm");ObserveNullInputCaller("");Check(p.Logger.warnings.Count==0);
  var input=new FTKInput();bool threw=false;try{OriginalCall(input);}catch(ArgumentNullException){threw=true;}Check(threw&&input.calls==1);
  string file=Path.Combine(args[0],"test-session.null-input-caller.log");string text=File.ReadAllText(file);
  Check(text.Contains("OriginalCall")&&text.Contains("session=test-session")&&text.Contains("action=null"));
  for(int i=0;i<20;i++)ObserveNullInputCaller(null);Check(p.Logger.warnings.Count==8);
  p.RemoveNullInputCallerDiagnostic();Check(HarmonyLib.Harmony.removals==1&&ReferenceEquals(nullInputCallerObserver,null));ObserveNullInputCaller(null);Check(p.Logger.warnings.Count==8);
  var failed=new RuntimeModelTest{output=Path.Combine(args[0],"absent"),Logger=new Log{fail=true}};
  nullInputCallerObserver=failed;threw=false;try{OriginalCall(input);}catch(ArgumentNullException){threw=true;}Check(threw&&input.calls==2);failed.RemoveNullInputCallerDiagnostic();
  HarmonyLib.Harmony.fail=true;p.InstallNullInputCallerDiagnostic();Check(ReferenceEquals(nullInputCallerObserver,null)&&p.nullInputCallerHarmony==null);Check(HarmonyLib.Harmony.removals==2);
  Console.WriteLine(checks+" null-input observer guard checks passed; Harmony/native integration remains a live gate.");
 }
}'''
with tempfile.TemporaryDirectory(prefix='ftk-null-input-') as directory:
    path=Path(directory)
    (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    (path/'Observer.cs').write_text(source)
    (path/'Program.cs').write_text(stubs)
    subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release','--',str(path)],check=True)
