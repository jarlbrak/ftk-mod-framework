"""Exercise the real passive callback observer with Unity/Harmony boundary stubs."""
from pathlib import Path
import re
import subprocess
import tempfile

root = Path(__file__).parent
source = (root/'CameraFollowDiagnostic.cs').read_text()
plugin = (root/'Plugin.cs').read_text()
assert plugin.index('MODEL TEST REFUSED') < plugin.index('InstallCameraFollowDiagnostic();')
assert 'RemoveNullInputCallerDiagnostic();RemoveCameraFollowDiagnostic();' in plugin
prefix = source[source.index('static void ObserveCameraCallback0'):source.index('static JObject CameraFollowDiagnosticState')]
code = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', prefix)
for forbidden in ('__result', '__exception', 'ref bool', 'out bool', 'return false', '.Invoke(', '.Follow(', '.EndFollow(', '.FocusOverworldCamera(', '.SetCameraTarget(', 'Camera.main', 'Input.'):
    assert forbidden not in code, forbidden
assert 'ReferenceEquals(observer, null)' in prefix
assert 'if (mainThread)' in prefix
sdkline = subprocess.check_output(['dotnet', '--list-sdks'], text=True).strip().splitlines()[-1]
sdkversion, sdkparent = sdkline.split(' [')
jsondll = Path(sdkparent.rstrip(']'))/sdkversion/'Newtonsoft.Json.dll'
stubs = r'''
using System;using System.IO;using System.Reflection;using System.Threading;using Newtonsoft.Json.Linq;
namespace HarmonyLib {
 public static class Priority {public const int Last=0;}
 public class HarmonyMethod {public int priority;public MethodInfo method;public HarmonyMethod(MethodInfo m){method=m;}}
 public class Harmony {
  public static int installs,removals,failAt;public static MethodInfo follow;public Harmony(string id){}
  public void Patch(MethodInfo m,HarmonyMethod prefix,object a,object b,object c,object d){
   installs++;if(m.Name=="Follow")follow=m;if((prefix==null?((HarmonyMethod)a):prefix).method.ReturnType!=typeof(void)||b!=null||c!=null||d!=null||(prefix!=null&&a!=null))throw new Exception("nonpassive patch");if(installs==failAt)throw new Exception("patch unavailable");}
  public void Unpatch(MethodInfo m,MethodInfo prefix){removals++;}
 }
}
namespace UnityEngine {
 public class Object {public int id=7; public static int reads;public static bool fail;
  public int GetInstanceID(){reads++;if(fail)throw new Exception("Unity unavailable");return id;}
 }
 public class Transform:Object {}
 public class GameObject:Object {public Transform transform=new Transform();}
 public static class Time {public static int frameCount=123;}
}
public struct FTKPlayerID {public int m_TurnIndex,m_PhotonID;}
public class CharacterOverworld:UnityEngine.Object {public FTKPlayerID m_FTKPlayerID;public bool m_IsUseMouse=true;}
public class uiPlayerMainHud:UnityEngine.Object {public CharacterOverworld m_Cow=new CharacterOverworld();public void OnPortraiteClick(){}}
public class FTKUI:UnityEngine.Object {public void FocusOverworldCamera(FTKPlayerID id){}}
public class FollowHelper:UnityEngine.Object {public bool Snap=true;public UnityEngine.GameObject FollowTarget=new UnityEngine.GameObject();public void SetCameraTarget(bool optional){}}
public class RtsCamera:UnityEngine.Object {public int calls;public void Follow(UnityEngine.Transform t,bool s,bool o){}public void Follow(UnityEngine.GameObject t,bool s,bool o){} public void EndFollow(){calls++;throw new InvalidOperationException("original");}}
namespace GridEditor {public class HexLand:UnityEngine.Object {public int m_ParentIndex,m_Index,m_Type;}}
public class Movement:UnityEngine.Object {
 public int m_Mode,m_PickingMode,m_ActionPoints=5,m_ActionPointsCurrent=5,calls;public bool m_LockedInput,m_UsingThumbStick,throwInBody;
 public CharacterOverworld m_CharacterOverworld=new CharacterOverworld();public GridEditor.HexLand m_PathStart,m_LastAdded,m_CursorHex;
 public System.Collections.Generic.List<GridEditor.HexLand> m_HexList=new System.Collections.Generic.List<GridEditor.HexLand>(),m_HexListPartial=new System.Collections.Generic.List<GridEditor.HexLand>();
 private void TrackCheckClickPath(GridEditor.HexLand hex,bool force,bool right,bool controller){calls++;if(throwInBody)throw new InvalidOperationException("original movement");m_ActionPointsCurrent--;}
 private void TrackingPathFinished(bool walk){}
 public void NativeClick(GridEditor.HexLand hex){TrackCheckClickPath(hex,false,false,false);}
}
class Log {public void LogWarning(string s){}}
public sealed partial class RuntimeModelTest {
 public static bool operator ==(RuntimeModelTest a,RuntimeModelTest b){throw new Exception("Unity owner equality");}
 public static bool operator !=(RuntimeModelTest a,RuntimeModelTest b){throw new Exception("Unity owner equality");}
 public override bool Equals(object o){return ReferenceEquals(this,o);}public override int GetHashCode(){return base.GetHashCode();}
 const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,Statics=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 string sessionId="test-session",output;Log Logger=new Log();
 static int checks;static void Check(bool v){if(!v)throw new Exception("Check "+checks);checks++;}
 public static void Main(string[] args){
  var p=new RuntimeModelTest{output=args[0]};
  foreach(string f in new[]{null,"true","0"}){Environment.SetEnvironmentVariable(CameraTraceFlag,f);p.InstallCameraFollowDiagnostic();Check(HarmonyLib.Harmony.installs==0);}
  ObserveCameraCallback4(new RtsCamera());Check(!(bool)CameraFollowDiagnosticState()["enabled"]);
  Environment.SetEnvironmentVariable(CameraTraceFlag,"1");p.InstallCameraFollowDiagnostic();Check(HarmonyLib.Harmony.installs==8&&p.cameraTraceEnabled);
  Check(HarmonyLib.Harmony.follow.GetParameters()[0].ParameterType==typeof(UnityEngine.Transform));
  ObserveCameraCallback0(new uiPlayerMainHud());ObserveCameraCallback1(new FTKUI(),new FTKPlayerID{m_TurnIndex=2,m_PhotonID=9});ObserveCameraCallback2(new FollowHelper(),false);ObserveCameraCallback3(new RtsCamera(),new UnityEngine.Transform{id=88},true,false);
  var camera=new RtsCamera();for(int i=0;i<30;i++)ObserveCameraCallback4(camera);
  string file=Path.Combine(args[0],"test-session.camera-follow.jsonl");Check(File.ReadAllLines(file).Length==20);
  var state=CameraFollowDiagnosticState();Check((long)state["methods"][4]["total"]==30&&(int)state["methods"][4]["latest"]["frame"]==123);Check((string)state["methods"][1]["latest"]["heroIdentity"]=="2:9");Check((int)state["methods"][3]["latest"]["targetInstanceId"]==88);
  Check(File.ReadAllText(file).Contains("callerStack"));
  int reads=UnityEngine.Object.reads;
  var worker=new Thread(()=>ObserveCameraCallback4(camera));worker.Start();worker.Join();
  Check(UnityEngine.Object.reads==reads);Check(!(bool)CameraFollowDiagnosticState()["methods"][4]["latest"]["mainThread"]);
  UnityEngine.Object.fail=true;bool original=false;try{ObserveCameraCallback4(camera);camera.EndFollow();}catch(InvalidOperationException e){original=e.Message=="original";}Check(original&&camera.calls==1&&p.cameraTraceReadFailures>0);UnityEngine.Object.fail=false;
  p.output=Path.Combine(args[0],"missing");ObserveCameraCallback2(new FollowHelper(),true);Check(p.cameraTraceWriteFailures==1);
  p.output=args[0];var move=new Movement();var hex=new GridEditor.HexLand{m_ParentIndex=13,m_Index=11};
  for(int i=0;i<70;i++)move.m_HexListPartial.Add(hex);
  ObserveCameraCallback5(move,hex,false,false,false);Check(move.calls==0&&move.m_ActionPointsCurrent==5);
  move.NativeClick(hex);ObserveCameraCallback6(move,hex,false,false,false);ObserveCameraCallback7(move,true);
  state=CameraFollowDiagnosticState();Check((string)state["methods"][6]["latest"]["phase"]=="postfix");
  Check((int)state["methods"][5]["latest"]["movement"]["actionPointsCurrent"]==5&&(int)state["methods"][6]["latest"]["movement"]["actionPointsCurrent"]==4);
  Check((bool)state["methods"][5]["latest"]["movement"]["partialPath"]["truncated"]&&((JArray)state["methods"][5]["latest"]["movement"]["partialPath"]["hexes"]).Count==64);
  Check((bool)state["methods"][7]["latest"]["walk"]&&move.calls==1);
  move.throwInBody=true;original=false;try{ObserveCameraCallback5(move,hex,false,false,false);move.NativeClick(hex);ObserveCameraCallback6(move,hex,false,false,false);}catch(InvalidOperationException e){original=e.Message=="original movement";}
  Check(original&&move.calls==2&&(long)CameraFollowDiagnosticState()["methods"][6]["total"]==1);
  p.RemoveCameraFollowDiagnostic();Check(HarmonyLib.Harmony.removals==8&&ReferenceEquals(cameraTraceObserver,null));
  var bad=new RuntimeModelTest{output=args[0]};HarmonyLib.Harmony.failAt=11;bad.InstallCameraFollowDiagnostic();Check(ReferenceEquals(cameraTraceObserver,null)&&!bad.cameraTraceEnabled&&bad.cameraTraceError!=null);Check(HarmonyLib.Harmony.removals==16);
  Console.WriteLine(checks+" camera trace checks passed; actual Harmony callbacks remain a live gate.");
 }
}
'''
with tempfile.TemporaryDirectory(prefix='ftk-camera-trace-') as directory:
    path=Path(directory)
    (path/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{jsondll}</HintPath></Reference></ItemGroup></Project>')
    (path/'Observer.cs').write_text(source)
    (path/'Program.cs').write_text(stubs)
    subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release','--',str(path)],check=True)
