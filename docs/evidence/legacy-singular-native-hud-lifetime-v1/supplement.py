from pathlib import Path
import json,gzip,hashlib,shutil
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir());O=R/'docs/evidence/legacy-singular-native-hud-lifetime-v1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
v=json.loads((O/'validation.json').read_text());known={x['source'] for x in v['losslessMappings']}; refs=set()
def scan(x):
 if isinstance(x,dict):
  for y in x.values():scan(y)
 elif isinstance(x,list):
  for y in x:scan(y)
 elif isinstance(x,str) and x.endswith(('.json','.jsonl')):
  p=Path(x)
  if p.is_absolute() and p.is_relative_to(R/'scratch/mirewarden-game/model-test-output') and p.is_file(): refs.add(p)
for m in v['losslessMappings']:
 data=gzip.decompress((O/m['archive']).read_bytes()).decode()
 if m['source'].endswith('.json'):scan(json.loads(data))
 elif m['source'].endswith('.jsonl'):
  for l in data.splitlines():scan(json.loads(l))
for p in sorted(refs):
 rel=str(p.relative_to(R))
 if rel in known:continue
 dest=O/'metadata'/Path(rel+'.gz');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 assert gzip.decompress(dest.read_bytes())==p.read_bytes()
 v['losslessMappings'].append({'source':rel,'sourceSha256':sha(p),'archive':str(dest.relative_to(O)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
(O/'history').mkdir();shutil.copy2(O/'validation.json',O/'history/validation-before-referenced-helper-results.json')
shutil.copy2(__file__,O/'supplement.py')
v['artifactSha256']={str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file() and p!=O/'validation.json'}
(O/'validation.json').write_text(json.dumps(v,indent=2)+'\n')
print(sha(O/'validation.json'),len(v['losslessMappings']),len(v['artifactSha256']))
