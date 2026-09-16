import json,gzip,hashlib,subprocess
from pathlib import Path
R=Path.cwd();A=R/'art-experiments/mournglass-wraith';O=A/'live-v1';read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p)}
manifest=A/'manifest.json';mh=sha(manifest);assert mh=='09ef2b0cd960c4185b188e29d117963b69dbc147263fa15a6e616369cc676da0';frozen=read(manifest)
for n,h in frozen['files'].items():assert sha(A/n)==h
case=Path((R/'scratch/mournglass-first-exercise-result.txt').read_text().strip());d=read(case);review=read(R/'scratch/mournglass-root-live-review.json')
assert d['session']==review['session']=='9476272a23d04891bee71aa188f24f02';assert len(d['actions'])==3 and d['collects']==[] and d['finalReady']['strictReady']['room']==2
assert d['actions'][1]['hpOutcome']['beforeHp']==58 and d['actions'][1]['hpOutcome']['afterHp']==50
assert not O.exists();O.mkdir();mappings=[]
def save(p,rel,expected=None):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.npz','.assets','.glb','.blend','.cs')
 b=p.read_bytes();h=hashlib.sha256(b).hexdigest()
 if expected:assert h==expected
 q=O/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(gzip.compress(b,mtime=0) if rel.endswith('.gz') else b)
 assert (gzip.decompress(q.read_bytes()) if rel.endswith('.gz') else q.read_bytes())==b
 mappings.append({'source':str(p),'sourceSha256':h,'archive':ev(q),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'});return ev(q)
folders=[case.parent,Path(read(R/'scratch/mournglass-first-stage-result.txt')['journal']).parent]+[Path(a['journal']['path']).parent for a in d['actions']]
for folder in folders:
 for p in folder.iterdir():
  if p.suffix in ('.json','.jsonl','.html'):save(p,folder.name+'/'+p.name+'.gz')
 for line in (folder/'journal.jsonl').read_text().splitlines():
  e=json.loads(line)
  if e.get('kind')=='helper-result':
   p=e.get('data',{}).get('path')
   if p and Path(p).is_file() and not any(m['source']==p for m in mappings):save(p,'helper-results/'+Path(p).name+'.gz')
for name in ['mournglass-first-exercise-result.txt','mournglass-first-stage-result.txt','mournglass-arrival-first-startup.json','mournglass-arrival-deployment-result.json','mournglass-first-live-inventory.json','mournglass-first-material-state.json','mournglass-root-live-review.json','mournglass-pass-scroll-verification.json','mournglass-hit-scroll-verification.json','mournglass-death-prefix-scroll-verification.json']:
 save(R/'scratch'/name,name+'.gz')
for name in ['manifest.json','source-findings.json','root-native-pose-review.json','native-pass-pose-audit.json','native-hit-pose-audit.json','native-death-prefix-pose-audit.json','native-shoulder-worst-pose-audit.json','material-partition-audit.json','original-geometry-proof.json','surface-bounds-audit.json']:
 save(A/name,'authoring/'+name+'.gz')
save(R/'scratch/runtime-profile-406-mournglass/receipt.json','staged-profile-receipt.json.gz');save(R/'scratch/runtime-profile-406-mournglass/model-test-profiles.json','model-test-profiles.json.gz',d['profileSha256']);save(Path(__file__),'archive-script.py')
caps=[]
for a,label,count in zip(d['actions'],['pass','normal-hit','explicit-death'],[120,120,100]):
 c=a['capture'];p=Path(c['rawCapture']['path']);raw=read(p);assert raw['session']==d['session'] and len(raw['frames'])==count
 save(p,label+'.json.gz',c['rawCapture']['sha256']);pins=c['images'];assert len(pins)==count
 for pin in pins:assert sha(pin['path'])==pin['sha256']
 (O/(label+'-source-image-pins.json')).write_text(json.dumps(pins,indent=2)+'\n')
 for v in review['views']:
  if v['captureId']==p.stem:save(R/v['path'],f'{label}-{v["index"]:04d}.png',v['sha256'])
 video=O/(label+'.mp4');subprocess.run(['ffmpeg','-v','error','-framerate','12','-i',str(p.parent/p.stem/'%04d.png'),'-frames:v',str(count),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True)
 meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,r_frame_rate,nb_read_frames','-of','json',str(video)]))['streams'][0];assert int(meta['nb_read_frames'])==count and meta['width']==1280 and meta['height']==832 and meta['r_frame_rate']=='12/1'
 clips={}
 for i,f in enumerate(raw['frames']):
  for layer in f['animator']['layers']:
   for clip in layer.get('playing',[]):
    if clip['weight']>0:clips.setdefault(clip['name'],[]).append(i)
 caps.append({'label':label,'action':a['action'],'raw':ev(O/(label+'.json.gz')),'boundary':c['boundary'],'frames':count,'video':ev(video),'videoMetadata':meta,'positiveClipFrames':clips,'rendererDisabledFrames':[i for i,f in enumerate(raw['frames']) if not f['enabled']],'animatorDisabledFrames':[i for i,f in enumerate(raw['frames']) if not f['animator']['enabled']]})
assert caps[-1]['boundary']['partial'] and not caps[-1]['boundary']['rawCaptureOk']
limits=['Selected original views only; native FX, hero and status icons obscure portions of the face, torso and hem.','Cloth appears dark and reflective under native lighting; both portraits show a readable ivory mask. No unconditional color-fidelity claim.','Two-slot pass/hit scroller metadata passed independently with zero float32 recurrence error; current owner only, no clone independence or final resource disposal.','Explicit KillSingle death is not ordinary lethal damage.','Death retains100of120requested frames with exact renderer-destroyed failure; partial raw was not relabeled successful. Complete-capture material-scroll verifier rejected it as expected.','Native Ready0/2 reached without Collect; full campaign, ordinary lethal and final owned-resource teardown remain unverified.']
(O/'README.md').write_text('# Mournglass live V1\n\nSelected original idle, attack, hit and early explicit death views were inspected by root. Native effects obscure parts of the body; dark reflective cloth and readable ivory portrait masks are documented. Pass and hit retain120frames each, normal damage58to50. Explicit KillSingle death retains100of120frames and the exact renderer-destroyed failure. Native Ready0/2 followed without Collect.\n\nTwo-slot pass/hit scroller checks report stable materials and zero float32 recurrence error. The complete-capture verifier rejected partial death; that rejection is preserved. This establishes neither clone independence nor final resource disposal.\n\nLossless gzip preserves raw captures, journals, reports and authoring metadata. The120/120/100frame MP4s are lossy review derivatives. Frozen authoring files remain unchanged. See [validation](../live-validation-v1.json) for pins and limitations.\n')
result={'status':review['status'],'nativeEnemy':'chaosBeast','rendererId':121008,'controllerId':5960,'rigProfile':'f8480a2d2d6a276730a02dca4d86b93484137a0a7ae861123ad26a9e947809c2','combatProfile':'1d2808cbd2bc1cb6f31a3d51b0787cb854efdf316b9ba44fb3b7825899d35d78','session':d['session'],'binaryPins':d['binaryPins'],'assetHashes':d['assetHashes'],'profileSha256':d['profileSha256'],'frozenAuthoringManifestSha256':mh,'rawWrapperStatus':d['status'],'captures':caps,'normalHitHpOutcome':d['actions'][1]['hpOutcome'],'finalReady':d['finalReady'],'collectCount':0,'rootReview':review,'materialScroll':{'pass':read(R/'scratch/mournglass-pass-scroll-verification.json'),'hit':read(R/'scratch/mournglass-hit-scroll-verification.json'),'death':read(R/'scratch/mournglass-death-prefix-scroll-verification.json')},'limits':limits,'losslessMappings':mappings,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]}
validation=A/'live-validation-v1.json';assert not validation.exists();validation.write_text(json.dumps(result,indent=2)+'\n')
idx=R/'docs/model-runtime-validation.json';j=read(idx);assert not any(x.get('model')=='Mournglass Wraith' for x in j['enemy_models']);j['enemy_models'].append({'model':'Mournglass Wraith','native_chassis':'chaosBeast','status':result['status'],'evidence':ev(validation),'limits':limits});idx.write_text(json.dumps(j,indent=2)+'\n')
for n,h in frozen['files'].items():assert sha(A/n)==h
assert sha(manifest)==mh
print(json.dumps({'validation':ev(validation),'files':len(result['evidence']),'mappings':len(mappings)}))
