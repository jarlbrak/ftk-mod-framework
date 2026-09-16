from pathlib import Path
import json,hashlib,shutil,argparse
import model_live as m

def pin(p):
 p=Path(p).resolve();return {'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
def save(p,x):
 with Path(p).open('x')as f:json.dump(x,f,indent=2)
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--session',required=True);ap.add_argument('--scenario',required=True,choices=['appear','damaged','damaged-heavy','death','death-light']);a=ap.parse_args()
 receipt_path=Path('scratch/current-deployment-backup.txt').read_text().strip();receipt=json.loads(Path(receipt_path).read_text())
 def check():
  assert json.loads((m.ROOT/'model-test-session.json').read_text())['session']==a.session
  assert pin(m.ROOT/'BepInEx/plugins/FtkRuntimeModelTest.dll')['sha256']=='c87a894ef225e7e89f7809984c68bacb17bffbabeefc4615d4c3b90f3df3793f'
  for rel,sha in receipt['new'].items():assert pin(m.ROOT/rel)['sha256']==sha,rel
 check();before=m.cmd('fixture-state');assert before['strictReady']['ok']
 out=Path('scratch')/('kraken-modern-mixer-'+a.scenario+'-live-v1');out.mkdir();save(out/'before-ready.json',before);shutil.copy2(receipt_path,out/'deployment.json')
 for label in ['first','repeat']:
  check();ready=m.cmd('fixture-state');assert ready['strictReady']==before['strictReady']
  key=m.send('kraken-controller-fixture',scenario=a.scenario,modernInputMixer='two-clip-raw-v1');shutil.copy2(m.ROOT/'model-test-command.json',out/(label+'-request.json'))
  print(json.dumps({'label':label,'id':key,'state':'submitted'}),flush=True)
  result=m.result(key,timeout=600);save(out/(label+'.json'),result);print(json.dumps({'label':label,'id':key,'ok':result.get('ok'),'error':result.get('error')}),flush=True)
  assert result['ok'],result.get('error')
 after=m.cmd('fixture-state');save(out/'after-ready.json',after);assert after['strictReady']==before['strictReady']
 manifest={'schema':'kraken-modern-input-mixer-v1','evidence':{}}
 for label in ['first','repeat']:
  manifest['evidence'][label]=pin(out/(label+'.json'));manifest['evidence'][label+'Request']=pin(out/(label+'-request.json'))
 manifest['evidence']['assets']=pin(m.ROOT/'FTK.app/Contents/Resources/Data/resources.assets');manifest['evidence']['gameAssembly']=pin(m.ROOT/'FTK.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll');save(out/'manifest.json',manifest)
 print(str(out/'manifest.json'),flush=True)
if __name__=='__main__':main()
