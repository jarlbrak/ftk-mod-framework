import json,gzip,hashlib,subprocess
from pathlib import Path
R=Path.cwd();A=R/'art-experiments/amberwake-dragon';B=R/'scratch/mirewarden-game/model-test-output';O=A/'live-v1';read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p)}
manifest=A/'manifest.json';mh=sha(manifest);mf=read(manifest);assert len(mf['files'])==64
for n,h in mf['files'].items():assert sha(A/n)==h
review=read(R/'scratch/amberwake-root-partial-live-review.json');deathreview=read(R/'scratch/amberwake-root-explicit-death-review.json');assert review['session']==deathreview['session'];parent=Path((R/'scratch/amberwake-first-exercise-result.txt').read_text().strip());d=read(parent)
assert not O.exists();O.mkdir();maps=[]
def save(p,rel,expected=None):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.assets','.npz','.glb','.blend','.cs');b=p.read_bytes();h=hashlib.sha256(b).hexdigest()
 if expected:assert h==expected
 q=O/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(gzip.compress(b,mtime=0) if rel.endswith('.gz') else b);assert (gzip.decompress(q.read_bytes()) if rel.endswith('.gz') else q.read_bytes())==b;maps.append({'source':str(p),'sourceSha256':h,'archive':ev(q),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'})
folders=[parent.parent]+[Path(x['journal']['path']).parent for x in d['actions']]
for n in ['second-attack','third-attack','fourth-attack','explicit-death']:folders.append(Path((R/f'scratch/amberwake-{n}-result.txt').read_text().strip()))
for folder in folders:
 for p in folder.iterdir():
  if p.suffix in ('.json','.jsonl','.html'):save(p,folder.name+'/'+p.name+'.gz')
 for line in (folder/'journal.jsonl').read_text().splitlines():
  e=json.loads(line)
  if e.get('kind')=='helper-result':
   p=e.get('data',{}).get('path')
   if p and Path(p).is_file() and not any(m['source']==p for m in maps):save(p,'helper-results/'+Path(p).name+'.gz')
for n in ['amberwake-first-exercise-result.txt','amberwake-second-attack-result.txt','amberwake-third-attack-result.txt','amberwake-fourth-attack-result.txt','amberwake-explicit-death-result.txt','amberwake-root-partial-live-review.json','amberwake-root-explicit-death-review.json','amberwake-after-explicit-death.json','amberwake-live-materials.json','amberwake-live-scale-observation.json','amberwake-before-native-focus-v1.json','amberwake-native-focus-attempt1-state.json','amberwake-before-focus-observation.json','amberwake-trap-first-startup.json','amberwake-trap-deployment-result.json','amberwake-after-fourth-attack.json']:save(R/'scratch'/n,n+'.gz')
for n in ['manifest.json','root-offline-draft-review.json','architect-trial-review.json','source-findings.json']:save(A/n,'authoring/'+n+'.gz')
save(R/'scratch/runtime-profile-409-amberwake/model-test-profiles.json','model-test-profiles.json.gz',review['catalogSha256']);save(R/'scratch/runtime-profile-409-amberwake/receipt.json','stage-receipt.json.gz');save(R/'docs/evidence/entry-preparation-delayed-quest-v1/validation.json','entry-continuation-validation.json.gz');save(Path(__file__),'archive-script.py')
raws=list(review['rawPins'].items())+[(Path(deathreview['raw']).stem,deathreview['sha256'])];caps=[];selected=review['selectedFrames']+deathreview['selectedFrames']
for (key,h),label in zip(raws,['pass','blocked-attack1','blocked-attack2','blocked-attack3','blocked-attack4','explicit-death']):
 rawpath=B/(key+'.json');raw=read(rawpath);f=raw['frames'];n=91 if label=='explicit-death' else 120;assert len(f)==n and raw['session']==review['session'] and raw['ok']==(n==120);save(rawpath,label+'.json.gz',h)
 pins=[ev(B/key/f'{i:04d}.png') for i in range(n)];assert len(list((B/key).glob('*.png')))==n;(O/(label+'-source-image-pins.json')).write_text(json.dumps(pins,indent=2)+'\n')
 for v in selected:
  if Path(v['path']).parent.name==key:save(R/v['path'],label+'-'+Path(v['path']).name,v['sha256'])
 video=O/(label+'.mp4');subprocess.run(['ffmpeg','-v','error','-framerate','12','-i',str(B/key/'%04d.png'),'-frames:v',str(n),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True)
 meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,r_frame_rate,nb_read_frames','-of','json',str(video)]))['streams'][0];assert int(meta['nb_read_frames'])==n and meta['width']==raw['width'] and meta['height']==raw['height'] and meta['r_frame_rate']=='12/1'
 clips={}
 for i,x in enumerate(f):
  for layer in x['animator']['layers']:
   for c in layer.get('playing',[]):
    if c['weight']>0:clips.setdefault(c['name'],[]).append(i)
 caps.append({'label':label,'raw':ev(O/(label+'.json.gz')),'rawOk':raw['ok'],'rawError':raw['error'],'frames':n,'requestedFrames':120,'complete':n==120,'video':ev(video),'videoMetadata':meta,'positiveClipFrames':clips,'inactiveFrames':[i for i,x in enumerate(f) if not x['active']],'animatorDisabledFrames':[i for i,x in enumerate(f) if not x['animator']['enabled']]})
final=read(R/'scratch/amberwake-after-explicit-death.json');assert final['fixture']['strictReady']['ok'] and final['fixture']['strictReady']['room']==2
limits=['Selected original idle/attack and explicit-death views only; tail, all-angle culling, collision and complete visual acceptance remain unverified.','Four terminal ordinary attacks blocked with HP675 unchanged; damaging hit is NOT_OBSERVED, ordinary lethal NOT_TESTED.','Explicit KillSingle is a separate fixture, not ordinary lethal;91of120frames retained with exact renderer-destroyed raw error.','No Collect action submitted by root during this death; fresh strictReady0/2 observed. Exact native loot callbacks are not inferred.','Scale0.25 is observed renderer-world-scale consistency, not separate local CEL measurement. Both portraits and selected wing tips readable; native FX/hero/blur still occlude portions.','Material emission opt-out observed on current owner, not final leased-resource disposal.','CUA focus attempts returned noWindowsAvailable twice per root review; no paid focus observed. These reported UI failures are not combat mutations.','Original partial review says death pending historically; later explicit-death review extends only fixture evidence, preserving the historical text.','Native dungeon fixture with scale0.25 is not a native-scale natural-arena or full campaign acceptance.']
(O/'README.md').write_text('# Amberwake original live V1, incomplete validation\n\nSelected original idle/attack framing and both portraits were reviewed at trial scale0.25 with emission disabled. Four ordinary attack recordings retain120frames each but all blocked at HP675. A damaging hit remains unobserved and ordinary lethal untested.\n\nThe explicit KillSingle fixture retains91of120frames and its exact renderer-destroyed error. Selected corpse views show connected body/head/wings with hero occlusion and blur. Fresh native Ready0/2 followed; no Collect was submitted by root during this death and exact native loot callbacks are not inferred. CUA focus attempts failed with noWindowsAvailable, so no paid focus is claimed.\n\nLossless raw/journals and original reviews remain unchanged, including the earlier death-pending review. Six12fps videos retain120/120/120/120/120/91frames. Frozen64authoring files remain unchanged. See [validation](../live-validation-v1.json) for per-check status and limits.\n')
r={'status':'SELECTED_ORIGINAL_VIEWS_WITH_FOUR_BLOCKS_AND_PARTIAL_EXPLICIT_DEATH','nativeEnemy':'dragonFrost','rendererId':121561,'sharedRigRepresentativeRendererId':121525,'session':review['session'],'frozenAuthoringManifestSha256':mh,'profileSha256':review['catalogSha256'],'binaryPins':d['binaryPins'],'assetHashes':d['assetHashes'],'rawExerciseStatus':d['status'],'captures':caps,'ordinaryDamagingHit':'NOT_OBSERVED_FOUR_BLOCKS','ordinaryLethal':'NOT_TESTED','rootPartialReview':review,'rootExplicitDeathReview':deathreview,'finalReady':final,'rootCollectCountDuringDeath':0,'limits':limits,'losslessMappings':maps,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]};v=A/'live-validation-v1.json';assert not v.exists();v.write_text(json.dumps(r,indent=2)+'\n')
for n,h in mf['files'].items():assert sha(A/n)==h
assert sha(manifest)==mh
print(json.dumps({'validation':ev(v),'files':len(r['evidence']),'maps':len(maps),'selectedImages':len(selected)}))
