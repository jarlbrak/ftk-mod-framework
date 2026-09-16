#!/usr/bin/env python3
"""Six once-only owned input pairs; default is offline validation with no writes."""
import argparse,hashlib,json,math,re,sys,time,uuid
from pathlib import Path
BASE=Path(__file__).resolve().parent.parent
HELPER='24c9596a8e6523fc2d13112156349e3520ba2f5521fc359cb1da05619a9c9aad'
ASSETS='e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117'
ASSEMBLY='94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8'
MODE='fixed-two-bank-five-raw-v1'
SCENARIOS=('appear','damaged','damaged-heavy','death','death-light','intro')
def require(ok,message):
 if not ok:raise ValueError(message)
def read(p):return json.loads(Path(p).read_text())
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def safe(p):
 p=Path(p);require(p.is_absolute() and p.resolve()==p and not any(x.is_symlink() for x in (p,*p.parents)),'Absolute nonsymlink path required: '+str(p));return p
def pin(p):p=safe(p);return {'path':str(p),'sha256':digest(p)}
def save(p,value):
 with Path(p).open('x') as f:json.dump(value,f,indent=2,allow_nan=False)
def copy_exact(source,dest):
 data=Path(source).read_bytes()
 with Path(dest).open('xb') as f:f.write(data)
 return hashlib.sha256(data).hexdigest()
def finite_positive(value):
 value=float(value);require(math.isfinite(value) and value>0,'Finite positive timeout required');return value
class Suite:
 def __init__(self,args):
  self.a=args;self.root=safe(args.root);require(self.root.is_dir() and self.root.parent.name=='scratch','Owned game root directly under scratch required')
  self.output=safe(args.output);require('scratch' in self.output.parts and not self.output.exists(),'Fresh scratch output required')
  require(re.fullmatch('[a-f0-9]{32}',args.session),'Exact expected session required');self.session=args.session
  for value in (args.catalog_sha256,args.deployment_sha256):require(re.fullmatch('[a-f0-9]{64}',value),'Exact expected SHA256 required')
  self.timeout=finite_positive(args.timeout);self.deployment=safe(args.deployment)
  require(digest(self.deployment)==args.deployment_sha256,'Deployment receipt pin differs')
  receipt=read(self.deployment);require(receipt['root']==str(self.root) and isinstance(receipt['new'],dict),'Deployment owned root/new file pins required')
  self.filepins={}
  for rel,sha in receipt['new'].items():
   require(isinstance(rel,str) and not Path(rel).is_absolute() and '..' not in Path(rel).parts and re.fullmatch('[a-f0-9]{64}',sha),'Unsafe deployment entry')
   self.filepins[str(safe(self.root/rel))]=sha
  required={'model-test-profiles.json':args.catalog_sha256,'BepInEx/plugins/FtkRuntimeModelTest.dll':HELPER}
  for rel,sha in required.items():require(receipt['new'].get(rel)==sha,'Expected catalog/helper differs from supplied deployment')
  for rel in ('BepInEx/plugins/FTKModFramework.dll','BepInEx/plugins/FtkRuntimeModelTestContent.dll'):require(rel in receipt['new'],'Missing binary deployment pin')
  self.native={}
  for role,tail,want in [('assets','resources.assets',ASSETS),('gameAssembly','Managed/Assembly-CSharp.dll',ASSEMBLY)]:
   p=self.root/'FTK.app/Contents/Resources/Data'/tail
   if not p.exists():p=self.root/'FTK_Data'/tail
   self.native[role]=pin(p);require(self.native[role]['sha256']==want,'Native source differs')
   self.filepins[str(p)]=want
  self.filepins[str(self.deployment)]=args.deployment_sha256
  # Freeze every Python source imported by this family of offline validators.
  self.sourcepins={str(p):digest(p) for p in sorted(BASE.glob('*.py'))}
  self.sourcepins[str(Path(__file__).resolve())]=digest(Path(__file__).resolve())
  self.seq=0;self.pending=None;self.ready_pin=None;self.native_ready_pin=None
  self.check();self.pending_guard()
 def check(self):
  require(read(safe(self.root/'model-test-session.json'))['session']==self.session,'Helper session changed')
  for path,sha in {**self.filepins,**self.sourcepins}.items():require(digest(safe(path))==sha,'Pinned input changed: '+path)
 def pending_guard(self):
  p=safe(self.root/'model-test-command.json')
  if not p.exists():return None
  previous=read(p);require(re.fullmatch('[a-f0-9]{32}',previous.get('id','')),'Malformed previous command ID')
  if previous.get('session')==self.session:
   result=safe(self.root/'model-test-output'/(previous['id']+'.json'))
   require(result.exists(),'Another helper operation is pending; never overwrite')
   done=read(result);require(done.get('id')==previous['id'] and done.get('session')==self.session,'Previous result identity mismatch')
  return digest(p)
 def log(self,kind,data):
  self.seq+=1
  with (self.output/'journal.jsonl').open('a') as f:f.write(json.dumps({'seq':self.seq,'kind':kind,'data':data,'time':time.time()},allow_nan=False)+'\n');f.flush()
 def command(self,op,dest,payload=None):
  require(op in ('fixture-state','kraken-controller-fixture'),'Only bounded observation/fixture operations allowed')
  require(self.pending is None,'An earlier submitted operation is unresolved')
  self.check();previous=self.pending_guard();key=uuid.uuid4().hex
  request=dict(payload or {},id=key,session=self.session,op=op)
  save(dest.with_name(dest.stem+'-request.json'),request)
  self.log('submission-intent',request) # persistent claim before potentially uncertain file delivery
  self.pending=key
  temporary=safe(self.root/('model-test-'+key+'.tmp'));save(temporary,request)
  self.check();require(self.pending_guard()==previous,'Helper command changed before delivery')
  temporary.replace(self.root/'model-test-command.json')
  self.log('submitted',{'id':key,'op':op})
  result_path=safe(self.root/'model-test-output'/(key+'.json'));deadline=time.monotonic()+self.timeout
  while time.monotonic()<deadline:
   require(read(safe(self.root/'model-test-session.json'))['session']==self.session,'Helper session changed while observing result')
   if result_path.exists():
    self.check()
    copy_exact(result_path,dest);result=read(dest);self.log('result',{'id':key,'path':str(dest),'sha256':digest(dest),'ok':result.get('ok'),'error':result.get('error')})
    require(result.get('id')==key and result.get('session')==self.session,'Result identity mismatch')
    self.pending=None;require(result.get('ok') is True,'Helper rejected operation; sequence stopped')
    return result
   time.sleep(.2)
  raise TimeoutError('Uncertain operation; do not retry. Pending result: '+str(result_path))
 def ready(self,dest):
  state=self.command('fixture-state',dest);identity=state.get('identity') or {};strict=state.get('strictReady') or {}
  require(strict.get('ok') is True and identity.get('root')==str(self.root) and identity.get('session')==self.session,'Strict owned Ready required')
  current={'identity':identity,'strictReady':strict,'dungeon':state.get('dungeon')}
  if self.ready_pin is None:self.ready_pin=current
  require(current==self.ready_pin,'Ready/dungeon ownership changed')
  return state
 def execute(self):
  self.check();self.pending_guard()
  claim=safe(self.root/'model-test-output'/('kraken-two-bank-suite-'+self.session+'.json'))
  save(claim,{'session':self.session,'output':str(self.output),'mode':MODE,'scenarios':SCENARIOS,'rule':'Permanent once-only suite claim; no automatic resume or retry'})
  self.output.mkdir(parents=True,exist_ok=False)
  save(self.output/'inputs.json',{'session':self.session,'root':str(self.root),'filePins':self.filepins,'sourcePins':self.sourcepins,'measurement':'on_disk_binary_sha256; does not establish loaded memory identity','claim':str(claim)})
  copy_exact(self.deployment,self.output/'deployment.json');copy_exact(self.root/'model-test-session.json',self.output/'session.json')
  results=[]
  try:
   sys.path.insert(0,str(BASE));from verify_kraken_modern_mixer import run as verify
   for scenario in SCENARIOS:
    folder=self.output/scenario;folder.mkdir();self.ready(folder/'before-ready.json')
    for label in ('first','repeat'):
     self.ready(folder/(label+'-ready.json'))
     report=self.command('kraken-controller-fixture',folder/(label+'.json'),{'scenario':scenario,'modernInputMixer':MODE})
     require(report.get('scenario')==scenario and report.get('expectedFrames')==(361 if scenario=='intro' else 241),'Wrong fixture horizon/scenario')
     if self.native_ready_pin is None:self.native_ready_pin=report['pinnedReady']
     require(report['pinnedReady']==self.native_ready_pin,'Native dungeon identity changed across suite')
    self.ready(folder/'after-ready.json');self.check()
    evidence=dict(self.native)
    for label in ('first','repeat'):
     evidence[label]=pin(folder/(label+'.json'));evidence[label+'Request']=pin(folder/(label+'-request.json'))
    save(folder/'manifest.json',{'schema':'kraken-modern-input-mixer-v1','evidence':evidence})
    validation=verify(folder/'manifest.json');save(folder/'verification.json',validation);self.log('verification',{'scenario':scenario,'status':validation['status'],'maximumError':validation['maximumError']})
    require(validation['status']=='modern_input_mixer_match','Numerical mismatch preserved; no next scenario')
    results.append({'scenario':scenario,'manifest':pin(folder/'manifest.json'),'verification':pin(folder/'verification.json')})
   self.check();save(self.output/'suite-result.json',{'status':'six_owned_input_pairs_numerically_verified','session':self.session,'mode':MODE,'pairs':results,'scope':'Input source experiment only; no endpoint/skin/attack/native-avatar/production acceptance'})
  except BaseException as exc:
   self.log('stopped',{'error':str(exc),'pendingCommandId':self.pending,'completedPairs':results,'rule':'No action retries; operator may read pending output only'})
   save(self.output/'suite-stopped.json',{'status':'stopped_no_retry','error':str(exc),'pendingCommandId':self.pending,'completedPairs':results});raise

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for key in ('root','output','deployment'):p.add_argument('--'+key,type=Path,required=True)
 for key in ('session','catalog-sha256','deployment-sha256'):p.add_argument('--'+key,required=True)
 p.add_argument('--timeout',type=finite_positive,default=600);p.add_argument('--execute',action='store_true');a=p.parse_args();suite=Suite(a)
 if a.execute:suite.execute()
 else:print(json.dumps({'status':'offline_validation_only_no_writes_or_helper_calls','session':suite.session,'scenarios':SCENARIOS,'mode':MODE,'filePins':suite.filepins,'sourcePins':suite.sourcepins},indent=2))
if __name__=='__main__':main()
