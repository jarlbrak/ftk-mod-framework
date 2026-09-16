#!/usr/bin/env python3
"""Offline, exclusive archive; preflight all five cases before writing anything."""
import argparse, ast, gzip, hashlib, json, math, shutil, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'tools/ai-model-pipeline'
SCENARIOS=('appear','damaged','damaged-heavy','death','death-light')
HELPER='0c65f1275f3eae761ec87bae6240c91dcfa49a13e34c5fc08334b92a88bc6163'
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text())
def require(ok,message):
 if not ok:raise ValueError(message)
class Plan:
 def __init__(self):self.files={};self.external=[];self.manifests=set()
 def add(self,path,expected=None):
  p=Path(path).absolute();require(p.is_file() and not p.is_symlink(),f'Missing/symlink input: {p}')
  require(p.suffix.lower() not in ('.dll','.assets','.bundle'),f'Native/binary copy prohibited: {p}')
  sha=digest(p);require(expected is None or sha==expected,f'Changed pin: {p}')
  self.files[str(p)]={'sha256':sha,'bytes':p.stat().st_size};return str(p)
 def manifest(self,path,expected=None):
  key=self.add(path,expected)
  if key in self.manifests:return
  self.manifests.add(key);m=read(path)
  for role,pin in m.get('evidence',{}).items():
   if role in ('assets','gameAssembly'):
    require(digest(pin['path'])==pin['sha256'],'Native source pin changed')
    if pin not in self.external:self.external.append(pin)
   elif role in ('composedManifest','historicalEndpointManifest'):self.manifest(pin['path'],pin['sha256'])
   else:self.add(pin['path'],pin['sha256'])
  for pins in m.get('images',{}).values():
   for pin in pins:self.add(pin['path'],pin['sha256'])
 def python(self,name):
  p=BASE/name
  if str(p) in self.files:return
  self.add(p)
  for n in ast.walk(ast.parse(p.read_text())):
   names=([n.module] if isinstance(n,ast.ImportFrom) else [x.name for x in n.names] if isinstance(n,ast.Import) else [])
   for name in names:
    if name and (BASE/(name+'.py')).is_file():self.python(name+'.py')
def prepare(reverify=True):
 plan=Plan();runs=[];session=None
 receipt=ROOT/'scratch/candidate-composed-output-helper-0c65f127/receipt.json';r=read(receipt)
 require(r['sha256']==HELPER and 'approved' in r['status'],'Unreviewed helper receipt')
 require(digest(r['artifact'])==HELPER,'Frozen helper changed');plan.add(receipt)
 for file,sha in r['sourcePins'].items():plan.add(BASE/file,sha)
 for name in ('verify_kraken_composed_skin.py','verify_kraken_composed_fixture.py'):plan.python(name)
 if reverify:
  sys.path.insert(0,str(BASE));from verify_kraken_composed_skin import run as verify
 for scenario in SCENARIOS:
  folder=ROOT/'scratch'/f'kraken-composed-{scenario}-live-v1'
  v=read(folder/'skin-verification.json');review=read(folder/'root-visual-review.json')
  require(v['status']=='composed_gloamfin_numerical_match_pending_visual_review' and v['tolerance']==1e-5,'Numerical acceptance missing')
  for key in ('maximumBakeError','endpointMaximumError'):require(math.isfinite(v[key]) and 0<=v[key]<=1e-5,'Numerical bound failed')
  require(v['manifestSha256']==digest(folder/'skin-manifest.json'),'Verification manifest differs')
  before=read(folder/'before-ready.json');after=read(folder/'after-ready.json')
  require(before['ok'] and after['ok'] and before['strictReady']==after['strictReady'] and after['strictReady']['ok'],'Ready changed')
  require(before['session']==after['session'],'Ready session changed')
  session=session or before['session'];require(session==before['session'],'Pairs span sessions')
  manifest=read(folder/'endpoint-manifest.json');skin=read(folder/'skin-manifest.json')
  for label in ('first','repeat'):
   report=read(manifest['evidence'][label]['path']);request=read(manifest['evidence'][label+'Request']['path'])
   require(report['session']==session and report['scenario']==scenario and report['id']==request['id'],'Raw owner/session mismatch')
   require(len(report['frames'])==241,'Incomplete raw frames')
   steps=review.get('reviewed',{}).get(label,[])
   require(isinstance(steps,list) and all(type(x) is int for x in steps),'Malformed visual steps')
   available={int(Path(x['path']).stem) for x in skin['images'][label]}
   require(set(steps)<=available,'Visual review names unavailable frame')
  require(any(review.get('reviewed',{}).values()),'No selected visual review')
  require(read(folder/'deployment.json')['helperReview']['helperSha256']==HELPER,'Deployment helper differs')
  if reverify:
   actual=verify(folder/'skin-manifest.json');require(actual==v,'Recomputed verification differs from raw saved report')
  plan.manifest(folder/'skin-manifest.json')
  for p in sorted(folder.iterdir()):
   require(p.is_file() and p.suffix in ('.json','.txt'),'Unexpected case directory contents');plan.add(p)
  runs.append({'scenario':scenario,'numericalStatus':v['status'],'maximumBakeError':v['maximumBakeError'],'endpointMaximumError':v['endpointMaximumError'],'framesPerRun':241,'imagesPerRun':{k:len(x) for k,x in skin['images'].items()},'reviewedSteps':review['reviewed'],'visualReviewSource':str(folder/'root-visual-review.json'),'ready':after['strictReady']})
 plan.add(Path(__file__))
 return plan,{'schema':'kraken-composed-owned-output-archive-v1','session':session,'helperSha256':HELPER,'endpointPolicy':'fixed-main-appearance-local-v1','status':'five_owned_scenario_pairs_numerically_verified_selected_visual_reviews_preserved','runs':runs,'nativeSourcesNotDistributed':plan.external,'scope':'Owned five-scenario no-CEL composition only. No enemy coverage increment, production adapter, attacks/events, duplicate-role support or real-avatar lifetime acceptance.'}
def archive(out,update_index=False):
 plan,record=prepare();require(not out.exists(),'Archive already exists; refusing overwrite')
 index=ROOT/'docs/model-runtime-validation.json';index_bytes=None;data=None
 if update_index:
  index_bytes=index.read_bytes();data=json.loads(index_bytes);section=data.setdefault('kraken_composed_owned_output_trials',[])
  require(not any(x.get('endpointPolicy')==record['endpointPolicy'] for x in section),'Numerical index entry already exists')
 # Recheck all source bytes before the first write, then again per copy.
 for p,pin in plan.files.items():require(digest(p)==pin['sha256'],'Input changed after preflight')
 out.mkdir(parents=True);payload=out/'payload';payload.mkdir();mapping=[]
 for source,pin in sorted(plan.files.items()):
  p=Path(source);b=p.read_bytes();require(hashlib.sha256(b).hexdigest()==pin['sha256'],'Input changed during archive')
  compress=p.suffix=='.json' and len(b)>250000
  name=pin['sha256']+'-'+p.name+('.gz' if compress else '')
  dest=payload/name;encoded=gzip.compress(b,mtime=0) if compress else b
  if not dest.exists():dest.write_bytes(encoded)
  require((gzip.decompress(dest.read_bytes()) if compress else dest.read_bytes())==b,'Lossless archive check failed')
  mapping.append({'originalPath':source,'archivePath':'payload/'+name,'sha256':pin['sha256'],'archiveSha256':digest(dest),'compression':'gzip' if compress else None,'bytes':len(b)})
 record['files']=mapping;(out/'validation.json').write_text(json.dumps(record,indent=2)+'\n')
 (out/'README.md').write_text('# Composed owned Kraken output\n\nFive original Gloamfin first/repeat scenario pairs retain all241 frames, all original skin images, exact requests/arms/results, Ready/deployment observations, root-selected visual reviews and independently recomputed verification. Raw inputs are byte-identical or losslessly gzip compressed; validation.json maps original paths and hashes. Historical endpoint manifests and reports are preserved. Native assets and DLLs remain external hash pins. Source files and recursive Python verifier dependencies are frozen in the payload.\n\nTo replay, restore payloads to the original paths in an isolated checkout (decompress entries marked gzip), or produce explicitly derived manifests remapping paths and recomputing manifest pins. Supply your own matching native source files. Original manifests and evidence must remain unchanged.\n\nNumerical acceptance and selected-frame visual observations remain separate. The original review text records its historical remaining work; this archive does not rewrite it. No actual enemy coverage count, production adapter, attack/event support, duplicate-clip transitions or real-avatar lifetime follows from this owned experiment.\n')
 if update_index:
  require(index.read_bytes()==index_bytes,'Numerical index changed during archive; archive retained, index untouched')
  section.append({'endpointPolicy':record['endpointPolicy'],'status':record['status'],'evidence':{'path':str(out/'validation.json'),'sha256':digest(out/'validation.json')},'scope':record['scope']})
  index.write_text(json.dumps(data,indent=2)+'\n')
 print(json.dumps({'archive':str(out),'files':len(mapping),'sha256':digest(out/'validation.json')}))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--commit',action='store_true');p.add_argument('--update-index',action='store_true');p.add_argument('--output',type=Path,default=ROOT/'docs/evidence/kraken-composed-owned-output-v1');a=p.parse_args()
 require(not a.update_index or a.commit,'Index update requires explicit commit')
 if a.commit:archive(a.output.absolute(),a.update_index)
 else:
  plan,record=prepare();print(json.dumps({'status':'preflight_only_no_writes','fileCount':len(plan.files),'record':record},indent=2))
