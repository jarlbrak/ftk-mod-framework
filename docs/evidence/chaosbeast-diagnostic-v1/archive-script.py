import json,gzip,hashlib,subprocess,shutil
from pathlib import Path
R=Path.cwd();B=R/'scratch/mirewarden-game/model-test-output';O=R/'docs/evidence/chaosbeast-diagnostic-v1'
read=lambda p:json.loads(Path(p).read_text())
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p),'bytes':p.stat().st_size}
case=Path((R/'scratch/chaosbeast-first-exercise-result.txt').read_text().strip());d=read(case)
assert d['session']=='7c51a58e8aff493096bdba5f783af3ab' and len(d['actions'])==3
assert d['collects']==[] and d['finalReady']['strictReady']=={'ok':True,'level':0,'room':3,'buttonCount':1}
assert d['actions'][1]['hpOutcome']['beforeHp']==58 and d['actions'][1]['hpOutcome']['afterHp']==50
assert not O.exists();O.mkdir(parents=True)
mappings=[]
def save(p,rel,expected=None):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.npz','.assets','.glb','.cs')
 raw=p.read_bytes();h=hashlib.sha256(raw).hexdigest()
 if expected:assert h==expected
 dest=O/rel;dest.parent.mkdir(parents=True,exist_ok=True)
 dest.write_bytes(gzip.compress(raw,mtime=0) if rel.endswith('.gz') else raw)
 assert (gzip.decompress(dest.read_bytes()) if rel.endswith('.gz') else dest.read_bytes())==raw
 mappings.append({'source':str(p),'sourceSha256':h,'archive':ev(dest),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'})
 return ev(dest)
folders=[case.parent]+[Path(a['journal']['path']).parent for a in d['actions']]
for folder in folders:
 for p in folder.iterdir():
  if p.suffix in ('.json','.jsonl','.html'):save(p,folder.name+'/'+p.name+'.gz')
 for line in (folder/'journal.jsonl').read_text().splitlines():
  e=json.loads(line)
  if e.get('kind')=='helper-result':
   path=e.get('data',{}).get('path')
   if path and Path(path).is_file() and not any(m['source']==path for m in mappings): save(path,'helper-results/'+Path(path).name+'.gz')
for name in ['chaosbeast-first-exercise-result.txt','entry-preparation-second-startup.json','entry-preparation-deployment-result.json']:
 save(R/'scratch'/name,name+'.gz')
source=R/'scratch/chaos-beast-native-topology-analysis/findings.json'
save(source,'source-findings.json','18ea071dcc48ee1d044bbdc629949bd8d1bbfa401a55692fe4c8b3e3e10cf55e')
src=read(source);save(Path(__file__),'archive-script.py')
captures=[]
for a,label,selected,count in zip(d['actions'],['pass','normal-hit','explicit-death'],[[50],[30],[27,40]],[120,120,91]):
 c=a['capture'];p=Path(c['rawCapture']['path']);raw=read(p);frames=raw['frames'];assert len(frames)==count and raw['session']==d['session']
 save(p,label+'.json.gz',c['rawCapture']['sha256'])
 pins=c['images'];assert len(pins)==count
 for entry in pins:assert sha(entry['path'])==entry['sha256']
 (O/(label+'-source-image-pins.json')).write_text(json.dumps(pins,indent=2)+'\n')
 for n in selected:save(p.parent/p.stem/f'{n:04d}.png',f'{label}-{n:04d}.png',pins[n]['sha256'])
 video=O/(label+'.mp4')
 subprocess.run(['ffmpeg','-v','error','-framerate','12','-i',str(p.parent/p.stem/'%04d.png'),'-frames:v',str(count),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True)
 meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,r_frame_rate,nb_read_frames','-of','json',str(video)]))['streams'][0]
 assert int(meta['nb_read_frames'])==count and meta['width']==1280 and meta['height']==832 and meta['r_frame_rate']=='12/1'
 playing={}
 for i,f in enumerate(frames):
  for layer in f['animator']['layers']:
   for clip in layer.get('playing',[]):
    if clip['weight']>0:playing.setdefault(clip['name'],[]).append(i)
 captures.append({'action':a['action'],'label':label,'raw':ev(O/(label+'.json.gz')),'boundary':c['boundary'],'frameCount':count,'video':ev(video),'videoMetadata':meta,'positiveClipFrames':playing,'rendererDisabledFrames':[i for i,f in enumerate(frames) if not f['enabled']],'animatorDisabledFrames':[i for i,f in enumerate(frames) if not f['animator']['enabled']]})
assert captures[-1]['rendererDisabledFrames']==list(range(29,91)) and captures[-1]['animatorDisabledFrames']==[90]
review={'reviewer':'root','provenance':'Root reported actual selected PNG inspection in delegated task; not an all-frame visual review.','frames':{'pass-0050.png':'Raised arm probes heavily occluded by native effects.','normal-hit-0030.png':'Damage -8 and HP50; probes overlap hero and effects.','explicit-death-0027.png':'Explicit -1000 damage; probes partly visible.','explicit-death-0040.png':'Probes gone, lingering native effects.'},'scope':'Calibration probes only. No original model appearance acceptance.'}
(O/'root-selected-review.json').write_text(json.dumps(review,indent=2)+'\n')
limits=['Single-material calibration probe cannot establish the native two-slot body/face material contract or scrolling compatibility.','No original art acceptance; selected views are heavily occluded.','Death uses explicit KillSingle, not ordinary lethal damage.','Death is a 91-frame partial failed capture retaining the exact renderer-destroyed error, not a complete 120-frame capture.','Positive death clip is observed27..89, but renderer is enabled only27..28 during it; no visible full death claim.','Wrapper reached native Ready0/3 with zero Collect calls; final owned-resource teardown and native disposal causality are not established.']
(O/'README.md').write_text('# ChaosBeast diagnostic, selected poses and partial death\n\nPass and normal-hit recordings each retain 120 frames. The normal hit changed HP58 to50. Explicit KillSingle death retains 91 of120 requested frames and the exact renderer-destroyed error. The renderer disables at29; Animator disables at90. Positive death playback27..89 is not continuous visible body evidence.\n\nThe wrapper reached native Ready level0 room3 without a Collect action. Root inspected four selected PNGs with substantial hero/effect occlusion. This single-material calibration probe does not prove the native two-slot scrolling material contract or accept an original model.\n\nSee [validation.json](validation.json) for exact pins, boundaries, lossless mappings, actions and remaining limitations. Videos are lossy12fps derivatives with verified120/120/91 frames; raw captures and journals are lossless gzip. Native assets, DLLs and decompiled source are excluded.\n')
result={'status':'DIAGNOSTIC_SELECTED_POSES_WITH_PARTIAL_RENDERER_DESTROYED_DEATH','nativeEnemy':'chaosBeast','rendererId':121008,'controllerId':5960,'rigProfile':src['mapping']['renderers'][0]['rig_profile_fingerprint'],'combatProfile':src['legacyProbeProfile']['combatProfile'],'session':d['session'],'binaryPins':d['binaryPins'],'profileSha256':d['profileSha256'],'assetHashes':d['assetHashes'],'rawWrapperStatus':d['status'],'captures':captures,'normalHitHpOutcome':d['actions'][1]['hpOutcome'],'finalReady':d['finalReady'],'collectCount':0,'limits':limits,'losslessMappings':mappings,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]}
(O/'validation.json').write_text(json.dumps(result,indent=2)+'\n')
idx=R/'docs/model-runtime-validation.json';j=read(idx);assert not any(x.get('native_chassis')=='chaosBeast' for x in j['calibration_probes']);j['calibration_probes'].append({'name':'ChaosBeast31-bone diagnostic','native_chassis':'chaosBeast','renderer_paths':['enChaosBeast'],'rig_profile':result['rigProfile'],'combat_profile':result['combatProfile'],'status':result['status'],'evidence':ev(O/'validation.json'),'limits':limits});idx.write_text(json.dumps(j,indent=2)+'\n')
print(json.dumps(ev(O/'validation.json')))
