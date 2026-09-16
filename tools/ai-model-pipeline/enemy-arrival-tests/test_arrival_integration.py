"""Source-bound checks plus execution of the exact C# capture wrapper with fake transport/framebuffer work."""
import pathlib, subprocess, tempfile, unittest
ROOT=pathlib.Path(__file__).resolve().parents[3]
SOURCE=ROOT/'tools/ai-model-pipeline/runtime-test/EnemySpawnCapture.cs'
PLUGIN=ROOT/'tools/ai-model-pipeline/runtime-test/Plugin.cs'
DOTNET='/opt/homebrew/opt/dotnet/libexec/dotnet'
class Integration(unittest.TestCase):
    def test_exact_wrapper_disposes_on_cancel_failure_and_completion(self):
        source=SOURCE.read_text();start=source.index('    IEnumerator SpawnCaptureRun(')
        body=source[start:source.rfind('\n}')]
        harness='''using System;using System.Collections;using System.IO;
class SkinnedMeshRenderer{}
class Log {public void LogError(string s){}}
class Ticket {public bool terminal;public string status;}
class Inner:IEnumerator,IDisposable {public int moves,disposals;public bool fail;public object Current{get{return null;}}public bool MoveNext(){moves++;if(fail)throw new Exception("setup-failure");return moves==1;}public void Reset(){throw new Exception();}public void Dispose(){disposals++;}}
class RuntimeModelTest {
string output=Path.GetTempPath();bool busy=true;Ticket spawnCapture=new Ticket();Log Logger=new Log();Inner inner=new Inner();int failures;string activeId;
IEnumerator Capture(string id,SkinnedMeshRenderer renderer,int seconds,int fps,int width,bool fixedStep,string provenance,bool material,bool arrival){activeId=id;return inner;}
void SpawnCaptureFail(string error){failures++;spawnCapture.terminal=true;spawnCapture.status="failed";File.WriteAllText(Path.Combine(output,activeId+".json"),"failure");}
'''+body+'''
static void Require(bool v){if(!v)throw new Exception("wrapper lifecycle assertion failed");}
static void Main(){
var normal=new RuntimeModelTest();var n=normal.SpawnCaptureRun(Guid.NewGuid().ToString("N"),new SkinnedMeshRenderer());Require(n.MoveNext());Require(!n.MoveNext());Require(normal.inner.disposals==1&&!normal.busy&&normal.failures==0&&normal.spawnCapture.terminal);
var cancelled=new RuntimeModelTest();var c=cancelled.SpawnCaptureRun(Guid.NewGuid().ToString("N"),new SkinnedMeshRenderer());Require(c.MoveNext());((IDisposable)c).Dispose();Require(cancelled.inner.disposals==1&&!cancelled.busy&&cancelled.failures==1);
var failed=new RuntimeModelTest();failed.inner.fail=true;var f=failed.SpawnCaptureRun(Guid.NewGuid().ToString("N"),new SkinnedMeshRenderer());Require(!f.MoveNext());Require(failed.inner.disposals==1&&!failed.busy&&failed.failures==1);
var existing=new RuntimeModelTest();string id=Guid.NewGuid().ToString("N");string path=Path.Combine(existing.output,id+".json");File.WriteAllText(path,"preserved");var e=existing.SpawnCaptureRun(id,new SkinnedMeshRenderer());Require(e.MoveNext());((IDisposable)e).Dispose();Require(existing.inner.disposals==1&&existing.failures==0&&File.ReadAllText(path)=="preserved");
Console.WriteLine("Exact extracted production iterator lifecycle PASS");}}
'''
        folder=pathlib.Path(tempfile.mkdtemp(prefix='arrival-wrapper-',dir=ROOT/'scratch'))
        (folder/'test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        (folder/'Program.cs').write_text(harness)
        result=subprocess.run([DOTNET,'run','--project',str(folder/'test.csproj'),'-c','Release'],capture_output=True,text=True)
        self.assertEqual(result.returncode,0,result.stdout+result.stderr)
        self.assertIn('lifecycle PASS',result.stdout)
    def test_no_native_action_or_ownership_calls(self):
        source=SOURCE.read_text()
        for forbidden in ['.EnsureRetained(','.Release(','.Destroy(','.Instantiate(','.PlayAttackSequence(','.TakeSecondaryDamage(','.InitEnemyDummyForCombat(','.OnLeftClick(','.SetActive(','.PruneDestroyedOwners(']:
            self.assertNotIn(forbidden,source)
    def test_opt_in_capture_default_and_pending_prefix(self):
        source=PLUGIN.read_text()
        self.assertIn('bool materialObservation=false,bool arrivalObservation=false',source)
        self.assertLess(source.index('SpawnCaptureTick();'),source.index('if (busy || Time.realtimeSinceStartup'))
        self.assertIn('if(arrivalObservation)spawnCapture.partialFrames=frames;',source)
        self.assertIn('Renderer destroyed during capture.',source)
    def test_frozen_arrival_has_no_unreviewed_hud_source(self):
        # A later independently reviewed watcher may coexist in current source. The arrival-only candidate stays frozen.
        import json
        receipt=ROOT/'scratch/candidate-enemy-arrival-helper-fbd04bed/receipt.json'
        if not receipt.exists():self.skipTest('local preserved arrival-only candidate unavailable')
        for pin in json.loads(receipt.read_text())['sourcePins']:self.assertNotIn('EnemyLifetime',pin['path'])
if __name__=='__main__':unittest.main()
