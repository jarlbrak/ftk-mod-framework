import json,gzip,hashlib,subprocess
from pathlib import Path
R=Path.cwd();B=R/'scratch/mirewarden-game/model-test-output';O=R/'docs/evidence/dragonfrost-diagnostic-v1';read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p)}
review=read(R/'scratch/dragon-root-live-review.json');parent=B/'case-ec57daa2d0584ef2ac227b72b1fff9b1';d=read(parent/'case-result.json');assert d['status']=='stopped' and d['session']==review['session'];assert not O.exists();O.mkdir(parents=True);maps=[]
def save(p,rel,expected=None):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.assets','.npz','.glb','.blend','.cs');b=p.read_bytes();h=hashlib.sha256(b).hexdigest()
 if expected:assert h==expected
 q=O/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(gzip.compress(b,mtime=0) if rel.endswith('.gz') else b);assert (gzip.decompress(q.read_bytes()) if rel.endswith('.gz') else q.read_bytes())==b
 maps.append({'source':str(p),'sourceSha256':h,'archive':ev(q),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'})
folders=[parent,B/'case-890c5c8f8d114d5799b47baedbcb8787',B/'case-82edc0d4f684439cb2f015fde6c14648',Path((R/'scratch/dragon-normal-hit-result.txt').read_text().strip()),Path((R/'scratch/dragon-explicit-death-result.txt').read_text().strip())]
for folder in folders:
 for p in folder.iterdir():
  if p.suffix in ('.json','.jsonl','.html'):save(p,folder.name+'/'+p.name+'.gz')
 for line in (folder/'journal.jsonl').read_text().splitlines():
  e=json.loads(line)
  if e.get('kind')=='helper-result':
   p=e.get('data',{}).get('path')
   if p and Path(p).is_file() and not any(m['source']==p for m in maps):save(p,'helper-results/'+Path(p).name+'.gz')
for n in ['dragon-frost-first-stage-result.txt','dragon-normal-hit-result.txt','dragon-explicit-death-result.txt','dragon-after-explicit-death.json','dragon-after-late-pass.json','dragon-root-live-review.json','dragon-pass-late-result-reconciliation.json','tamarind-lifetime-first-startup.json','tamarind-lifetime-deployment-result.json']:save(R/'scratch'/n,n+'.gz')
for p in (R/'docs/evidence/dragonfrost-native-source-v1').iterdir():
 if p.is_file():save(p,'source/'+p.name)
save(Path(__file__),'archive-script.py');caps=[]
for label,key,n in [('pass','7c9d194c139f4e33bca315cf33cc9258',120),('normal-hit','ff6c9c97e2ed40d4921f0b1117761de6',120),('explicit-death','2561a6acafeb4bcfb8ec29fbae6393b9',91)]:
 p=B/(key+'.json');raw=read(p);f=raw['frames'];assert len(f)==n and raw['session']==d['session'];assert raw['ok']==(label!='explicit-death');save(p,label+'.json.gz')
 pins=[ev(B/key/f'{i:04d}.png') for i in range(n)];assert len(list((B/key).glob('*.png')))==n;(O/(label+'-source-image-pins.json')).write_text(json.dumps(pins,indent=2)+'\n')
 for v in review['reviewedFrames']:
  if v['captureId']==key:save(R/v['path'],f'{label}-{v["index"]:04d}.png',v['sha256'])
 video=O/(label+'.mp4');subprocess.run(['ffmpeg','-v','error','-framerate','12','-i',str(B/key/'%04d.png'),'-frames:v',str(n),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True)
 meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,r_frame_rate,nb_read_frames','-of','json',str(video)]))['streams'][0];assert int(meta['nb_read_frames'])==n and meta['width']==1280 and meta['height']==832 and meta['r_frame_rate']=='12/1'
 clips={}
 for i,x in enumerate(f):
  for layer in x['animator']['layers']:
   for c in layer.get('playing',[]):
    if c['weight']>0:clips.setdefault(c['name'],[]).append(i)
 caps.append({'label':label,'raw':ev(O/(label+'.json.gz')),'ok':raw['ok'],'error':raw['error'],'frames':n,'complete':n==120 and raw['ok'],'requestedFrames':120,'video':ev(video),'videoMetadata':meta,'positiveClipFrames':clips,'inactiveFrames':[i for i,x in enumerate(f) if not x['active']],'animatorDisabledFrames':[i for i,x in enumerate(f) if not x['animator']['enabled']],'timing':{'wallSeconds':f[-1]['realtimeSeconds']-f[0]['realtimeSeconds'],'gameSeconds':f[-1]['gameSeconds']-f[0]['gameSeconds']}})
assert caps[-1]['inactiveFrames']==[90] and caps[-1]['animatorDisabledFrames']==[90]
final=read(R/'scratch/dragon-after-explicit-death.json');assert final['fixture']['strictReady']['ok'] and final['fixture']['strictReady']['room']==2
limits=review['limits']+['Calibration evidence only, not a model or rig appearance PASS.','Normal attack675to674 differs from explicit KillSingle674to0; ordinary lethal remains untested.','Death91of120partial renderer-destroyed capture remains failed and unmodified.','Eight selected images only; native body/wing/portrait cropping prevents full anatomy and framing acceptance.','Capture timing is perturbed;12fps videos are lossy review derivatives.','Native Ready0/2 without Collect; full campaign and final owned-resource teardown unproved.']
(O/'README.md').write_text('# DragonFrost diagnostic with crop and timeout limitations\n\nThis exact native renderer121561 uses the verified shared rig representative121525. It is a dungeon fixture for a native boss whose m_SpawnDungeon is false. Native scale1, emission and frost effects are retained; body, wings and portraits are severely cropped. Eight selected PNGs provide scoped diagnostic evidence, not visual acceptance or original Amberwake acceptance.\n\nThe pass wrapper timed out, while its original raw recording completed120frames later. The parent stayed stopped; the additive reconciliation and once-only pass action are retained. A separate normal attack recorded120frames and damage675to674. Explicit KillSingle674to0 recorded91of120frames before renderer destruction; native deathDirect27to89 was active, followed by inactive/Animator-off sample90.\n\nNative Ready0/2 followed without Collect. Raw records and journals are lossless gzip; MP4s have verified120/120/91frames at1280x832 and12fps. No ordinary lethal, full campaign or resource teardown claim. See [validation](validation.json).\n')
r={'status':'DIAGNOSTIC_EVIDENCE_WITH_SEVERE_NATIVE_CROPPING_AND_PARTIAL_DEATH','nativeEnemy':'dragonFrost','rendererId':121561,'sharedRigRepresentativeRendererId':121525,'session':d['session'],'binaryPins':d['binaryPins'],'profileSha256':d['profileSha256'],'assetHashes':d['assetHashes'],'rawParentStatus':d['status'],'captures':caps,'rootReview':review,'latePassReconciliation':read(R/'scratch/dragon-pass-late-result-reconciliation.json'),'finalReady':final,'collectCount':0,'limits':limits,'losslessMappings':maps,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]}
p=O/'validation.json';p.write_text(json.dumps(r,indent=2)+'\n');idx=R/'docs/model-runtime-validation.json';j=read(idx);assert not any(x.get('native_chassis')=='dragonFrost' for x in j['calibration_probes']);j['calibration_probes'].append({'name':'DragonFrost70-bone diagnostic','native_chassis':'dragonFrost','renderer_paths':['enDragon'],'status':r['status'],'evidence':ev(p),'limits':limits});idx.write_text(json.dumps(j,indent=2)+'\n');print(json.dumps({'validation':ev(p),'files':len(r['evidence']),'maps':len(maps)}))
