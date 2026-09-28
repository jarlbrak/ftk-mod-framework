"""Exercise production input wrappers with noisy hardware and active/idle synthetic timelines."""
from pathlib import Path
import re
import subprocess
import tempfile

root=Path(__file__).resolve().parents[1]
source=(root/'FTKModFramework/Agent/NativeInput.cs').read_text()
assert 'InputIsolated = Background && Environment.GetEnvironmentVariable(InputIsolationEnvFlag) == "1"' in source
assert 'if (framework && type.Namespace != null' in source and 'type.Namespace == typeof(NativeInput).Namespace' in source
assert 'Vector3 mouse = SuppressHardware() ? _isolatedMouse : Input.mousePosition;' in source
finish=source[source.index('        private static void Finish('):source.index('        private static string Guard(')]
assert finish.index('_isolatedMouse = new Vector3(_timeline.Current.X') < finish.index('_timeline = null;')
wrappers=source[source.index('        private static bool Synthetic()'):source.index('        private static void AcknowledgeText(')]
prefix=re.search(r'        private static bool BeforeInputFieldUpdate\(.*?\n        }',source,re.S).group()
program=r'''using System;using System.Collections.Generic;
enum KeyCode{None=0,A=97,Alpha1=49,Backspace=8,Return=13,KeypadEnter=271,Tab=9,Mouse0=323,Mouse6=329}
struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero{get{return new Vector2(0,0);}}}
static class Time{public static int frameCount=10;}
class InputField{}class BaseEventData{}
static class Plugin{public static Log Log=new Log();}class Log{public void LogError(string s){}}
static class Input{
 public static int reads;static bool B(){reads++;return true;}static float F(){reads++;return 99;}
 public static bool GetKey(KeyCode k){return B();}public static bool GetKeyDown(KeyCode k){return B();}public static bool GetKeyUp(KeyCode k){return B();}
 public static bool GetKey(string k){return B();}public static bool GetKeyDown(string k){return B();}public static bool GetKeyUp(string k){return B();}
 public static bool GetMouseButton(int k){return B();}public static bool GetMouseButtonDown(int k){return B();}public static bool GetMouseButtonUp(int k){return B();}
 public static bool GetButton(string k){return B();}public static bool GetButtonDown(string k){return B();}public static bool GetButtonUp(string k){return B();}
 public static float GetAxis(string k){return F();}public static float GetAxisRaw(string k){return F();}
 public static Vector3 mousePosition{get{reads++;return new Vector3(999,888,0);}}public static Vector2 mouseScrollDelta{get{reads++;return new Vector2(0,99);}}
 public static bool mousePresent{get{return B();}}public static bool anyKey{get{return B();}}public static bool anyKeyDown{get{return B();}}
 public static string inputString{get{reads++;return "hardware";}}
}
class Frame{public float X=640,Y=400;public HashSet<int> Keys=new HashSet<int>(),Buttons=new HashSet<int>();}
class Timeline{
 public Frame Current=new Frame(),Previous=new Frame();public float Scroll=3;public string Text="synthetic";
 public bool Key(int k){return Current.Keys.Contains(k);}public bool KeyDown(int k){return Key(k);}public bool KeyUp(int k){return Key(k);}
 public bool Button(int k){return Current.Buttons.Contains(k);}public bool ButtonDown(int k){return Button(k);}public bool ButtonUp(int k){return Button(k);}
 public string InputString(params int[] keys){return Text;}
}
class Program{
 static bool _available,InputIsolated;static int _startFrame=0;static long _reads,_syntheticReads;static Timeline _timeline;
 static Vector3 _isolatedMouse=new Vector3(640,400,0);static void Tick(){}static void AcknowledgeText(int count){}static void Finish(string state,string error){_timeline=null;}
 static bool DeliverInputFieldEvents(InputField f,BaseEventData e){return true;}
 __WRAPPERS__
 __PREFIX__
 static int checks;static void Check(bool ok){checks++;if(!ok)throw new Exception("Check "+checks);}
 static bool[] Buttons(){return new[]{GetKey(KeyCode.A),GetKeyDown(KeyCode.A),GetKeyUp(KeyCode.A),GetKey("A"),GetKeyDown("A"),GetKeyUp("A"),GetMouseButton(0),GetMouseButtonDown(0),GetMouseButtonUp(0),GetButton("MouseButton0"),GetButtonDown("MouseButton0"),GetButtonUp("MouseButton0"),AnyKey(),AnyKeyDown()};}
 static void Main(){
  _available=true;InputIsolated=true;_timeline=null;Input.reads=0;
  foreach(bool x in Buttons())Check(!x);
  Check(GetAxis("Mouse X")==0&&GetAxisRaw("anything")==0);Check(MouseScrollDelta().y==0);
  Check(MousePosition().x==640&&MousePosition().y==400);Check(InputString()==""&&MousePresent());
  Check(!BeforeInputFieldUpdate(new InputField(),new BaseEventData()));Check(Input.reads==0);
  _timeline=new Timeline();_timeline.Current.Keys.Add(97);_timeline.Current.Buttons.Add(0);_timeline.Previous.X=635;
  foreach(bool x in Buttons())Check(x);
  Check(GetAxis("Mouse X")==5&&GetAxisRaw("MouseAxis3")==3);Check(MouseScrollDelta().y==3);
  Check(InputString()=="synthetic"&&BeforeInputFieldUpdate(new InputField(),new BaseEventData()));Check(Input.reads==0);
  _timeline=null;Check(!GetKey(KeyCode.A)&&InputString()==""&&Input.reads==0);
  InputIsolated=false;foreach(bool x in Buttons())Check(x);Check(GetAxis("Mouse X")==99&&MousePosition().x==999&&InputString()=="hardware");Check(BeforeInputFieldUpdate(new InputField(),new BaseEventData()));Check(Input.reads>0);
  InputIsolated=true;_available=false;Input.reads=0;Check(GetKey(KeyCode.A)&&InputString()=="hardware"&&Input.reads==2);Check(BeforeInputFieldUpdate(new InputField(),new BaseEventData()));
  Console.WriteLine(checks+" noisy-hardware idle/active/default/unavailable wrapper checks passed");
 }
}'''.replace('__WRAPPERS__',wrappers).replace('__PREFIX__',prefix)
with tempfile.TemporaryDirectory(prefix='ftk-input-isolation-') as temp:
    p=Path(temp)
    (p/'test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>')
    (p/'Program.cs').write_text(program)
    subprocess.run(['dotnet','run','--project',str(p/'test.csproj'),'-c','Release'],check=True)
print('Explicit background opt-in, agent-namespace exclusion and retained-pointer source boundaries passed')
