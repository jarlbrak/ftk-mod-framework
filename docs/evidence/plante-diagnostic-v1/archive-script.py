import json,gzip,hashlib,subprocess
from pathlib import Path
R=Path.cwd();read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();ev=lambda p:dict(path=str(p),sha256=sha(p));out=R/'docs/evidence/plante-diagnostic-v1';case=R/'scratch/mirewarden-game/model-test-output/case-d5bdffaa692f4429989df94a42b72ba3';d=read(case/'case-result.json');review=read(R/'scratch/plante-root-review.json');source=R/'scratch/plant-e-native-topology-analysis/findings.json';index=R/'docs/model-runtime-validation.json';indexbytes=index.read_bytes();di=json.loads(indexbytes)
assert not out.exists() and not any(x.get('native_chassis')=='plantE' for x in di['calibration_probes'])
assert sha(source)=='e9535913ad4ea162ef596e603e5be0381e008801f2079ae20527a48bdf2fdf50'
assert d['enemy']=='ftkmf_modeltest_probe_plante' and d['rendererPath']=='enJungleNibbler_A' and d['session']==review['session'] and d['status']=='needs_visual_review'
assert d['finalReady']['strictReady']=={'ok':True,'level':0,'room':3,'buttonCount':1} and len(d['collects'])==1
assert d['actions'][1]['hpOutcome']['beforeHp']==58 and d['actions'][1]['hpOutcome']['afterHp']==50
assert read(R/'scratch/saffronspine-startup-evidence.json')['session']==d['session']
files={};caps=[]
def add(p,rel):
 p=Path(p);assert p.is_file() and not p.is_symlink() and p.suffix.lower()not in('.dll','.assets','.npz','.cs');files[str(p)]=(rel,sha(p))
for p in case.iterdir():add(p,'case/'+p.name)
for action,rr in zip(d['actions'],review['actions']):
 assert action['action']==rr['action'] and action['actionAccepted'] is True
 for kind in ('journal','rawResult','rawCapture'):
  pin=action[kind];assert sha(pin['path'])==pin['sha256']
 child=Path(action['journal']['path']).parent
 for p in child.iterdir():
  if p.is_file():add(p,child.name+'/'+p.name)
 raw=Path(action['rawCapture']['path']);assert ev(raw)=={'path':rr['capture']['path'],'sha256':rr['capture']['sha256']};v=read(raw);assert v['ok']is True and len(v['frames'])==120
 assert all(f['enabled'] and f['animator']['enabled'] for f in v['frames'])
 assert len(action['rawPngFiles'])==120
 for i,pin in enumerate(action['rawPngFiles']):assert sha(pin['path'])==pin['sha256'] and Path(pin['path']).stem==f'{i:04d}'
 label=action['action'];add(raw,label+'.json.gz')
 for i in review['reviewedFrames'][label]:add(raw.with_suffix('')/f'{i:04d}.png',f'{label}-{i:04d}.png')
 caps.append((label,raw,action['rawPngFiles']))
assert sum(len(v)for v in review['reviewedFrames'].values())==5
for name in ('plante-root-review.json','plante-first-exercise-result.txt','saffronspine-startup-evidence.json','saffronspine-deployment-result.json'):add(R/'scratch'/name,name)
add(source,'source-findings.json');add(source.parent/'probe-validation.json','probe-validation.json');add(Path(__file__),'archive-script.py')
out.mkdir();mappings=[]
for p,(rel,h) in files.items():
 b=Path(p).read_bytes();assert hashlib.sha256(b).hexdigest()==h;q=out/rel;q.parent.mkdir(parents=True,exist_ok=True);compressed=rel.endswith('.gz');q.write_bytes(gzip.compress(b,mtime=0)if compressed else b);assert(gzip.decompress(q.read_bytes())if compressed else q.read_bytes())==b;mappings.append(dict(originalPath=p,archivePath=rel,sha256=h,archiveSha256=sha(q),compression='gzip'if compressed else None))
reports=[]
for label,raw,pngpins in caps:
 pinfile=out/(label+'-source-image-pins.json');pinfile.write_text(json.dumps(pngpins,indent=2)+'\n');video=out/(label+'.mp4');subprocess.run(['/opt/homebrew/bin/ffmpeg','-nostdin','-n','-loglevel','error','-framerate','12','-i',str(raw.with_suffix('')/'%04d.png'),'-frames:v','120','-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','20','-pix_fmt','yuv420p',str(video)],check=True);vp=json.loads(subprocess.check_output(['/opt/homebrew/bin/ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=nb_read_frames,width,height,r_frame_rate','-of','json',str(video)]))['streams'][0];assert int(vp['nb_read_frames'])==120;reports.append(dict(action=label,raw=ev(out/(label+'.json.gz')),video=ev(video),videoMetadata=vp,sourceImagePins=ev(pinfile),frames=120,rendererDisabledFrames=0,animatorDisabledFrames=0))
limits=['Calibration geometry only, not original plant appearance or final-art acceptance.','Eight native-zero-weight palette bones remain diagnostic; no plantD/G or other exact-rig coverage inferred.','Only five explicitly root-reviewed PNGs; videos are lossy12fps review derivatives, not proof every frame was viewed.','Native attack8 damage58->50; death50->0 is explicit fixture trigger, not ordinary lethal damage.','No final resource teardown or numerical floor-contact acceptance. Native assets/meshes/DLLs/decompiled sources are external pins only.']
r=dict(status='DIAGNOSTIC_SELECTED_FRAMES_REVIEWED_NOT_ORIGINAL_ART',nativeEnemy='plantE',rendererId=121530,controllerId=5980,rigProfile='49294438bb2419f05a6af33199c6a99c988a9b58ad8ea393f42513dbd72f9d2c',combatProfile='30bf6f1223474a469aae2efa58b6eb7c1476b88984e95de5c8d24fc7b052f6e6',session=d['session'],binaryPins=d['binaryPins'],assetHashes=d['assetHashes'],profileSha256=d['profileSha256'],rawWrapperStatus=d['status'],captures=reports,finalReady=d['finalReady'],rootReview=ev(out/'plante-root-review.json'),limits=limits,losslessMappings=mappings,evidence=[ev(p)for p in sorted(out.rglob('*'))if p.is_file()]);v=out/'validation.json';v.write_text(json.dumps(r,indent=2)+'\n');assert index.read_bytes()==indexbytes;di['calibration_probes'].append(dict(name='PlantE37-bone diagnostic',native_chassis='plantE',renderer_paths=['enJungleNibbler_A'],rig_profile=r['rigProfile'],combat_profile=r['combatProfile'],status=r['status'],evidence={'path':'docs/evidence/plante-diagnostic-v1/validation.json','sha256':sha(v)},limits=limits));index.write_text(json.dumps(di,indent=2)+'\n');print(ev(v))
