import json,gzip,hashlib,subprocess,math
from pathlib import Path
R=Path.cwd();read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();ev=lambda p:dict(path=str(p),sha256=sha(p));out=R/'docs/evidence/fisha01-diagnostic-v1';base=R/'scratch/mirewarden-game/model-test-output';review=read(R/'scratch/fisha01-root-live-review.json');case=base/'case-a13f33d01ce54d89b3d1fe2178e3d8b2';d=read(case/'case-result.json');idx=R/'docs/model-runtime-validation.json';old=idx.read_bytes();di=json.loads(old);assert not out.exists() and not any(x.get('native_chassis')=='fishA01'for x in di['calibration_probes']);assert d['session']==review['session'] and d['status']=='stopped' and len(d['actions'])==2 and d['actions'][1]['hpOutcome']['beforeHp']==d['actions'][1]['hpOutcome']['afterHp']==58
assert read(R/'scratch/belladusk-startup-evidence.json')['session']==d['session'];assert read(R/'scratch/fisha01-final-ready.json')['fixture']['strictReady']==review['finalReady'];files={}
def add(p,rel):
 p=Path(p);assert p.is_file()and not p.is_symlink()and p.suffix.lower()not in('.dll','.assets','.npz','.cs');files[str(p)]=(rel,sha(p))
dirs=[case,base/'case-74871990d882431e8f8e769fe80dcf63',base/'case-e0ced1114d0f40dda64b0a68123db726']+[Path(a['journal']['path']).parent for a in d['actions']]
for folder in dirs:
 for p in folder.iterdir():
  if p.is_file():add(p,folder.name+'/'+p.name)
 journal=folder/'journal.jsonl'
 for line in journal.read_text().splitlines():
  e=json.loads(line);data=e.get('data',{});raw=data.get('path')if e.get('kind')=='helper-result'else None
  if raw and Path(raw).is_file():add(raw,'helper-results/'+Path(raw).name)
for n in ['fisha01-root-live-review.json','fisha01-first-root-review.json','fisha01-ragdoll-analysis.json','fisha01-first-exercise-result.txt','fisha01-second-hit-result.txt','fisha01-first-death-result.txt','fisha01-after-first-state.json','fisha01-after-second-hit-state.json','fisha01-before-loot.json','fisha01-collect1.json','fisha01-after-collect1.json','fisha01-collect2.json','fisha01-final-ready.json','belladusk-startup-evidence.json','belladusk-deployment-result.json']:add(R/'scratch'/n,n)
source=R/'scratch/fish-a01-native-topology-analysis/findings.json';assert sha(source)=='66af7b180ef012bdeb472edd4969cc887da21fe99ab3e1f9443afe9fd6d107d1';add(source,'source-findings.json');add(source.parent/'probe-validation.json','probe-validation.json');add(Path(__file__),'archive-script.py');caps=[]
for label,key in zip(['pass','dodged-hit','normal-hit','explicit-death'],review['reviewedFrames']):
 raw=base/(key+'.json');r=read(raw);assert r['ok']is True and r['session']==d['session']and len(r['frames'])==120;add(raw,label+'.json.gz');pins=[]
 for i in range(120):
  p=base/key/f'{i:04d}.png';assert p.is_file()and not p.is_symlink();pins.append(ev(p))
 for i in review['reviewedFrames'][key]:add(base/key/f'{i:04d}.png',f'{label}-{i:04d}.png')
 caps.append((label,raw,r,pins))
assert sum(len(v)for v in review['reviewedFrames'].values())==5
r=caps[-1][2];f=r['frames'];assert next(i for i,x in enumerate(f)if not x['animator']['enabled'])==26;assert next(i for i,x in enumerate(f)if all(not b['isKinematic']for b in x['ragdoll']['rigidbodies']))==26;assert all(x['ragdoll']['rigidbodyCount']==11 and not x['ragdoll']['truncated']for x in f)
assert all(x['enabled']for _,_,v,_ in caps for x in v['frames'])
norm=lambda a,b:math.sqrt(sum((x-y)**2 for x,y in zip(a,b)))
settling=max(norm(f[i]['ragdoll']['rigidbodies'][j]['position'],f[i-1]['ragdoll']['rigidbodies'][j]['position'])for i in range(61,120)for j in range(11));assert settling==0
out.mkdir();mapping=[]
for source,(rel,h)in files.items():
 b=Path(source).read_bytes();assert hashlib.sha256(b).hexdigest()==h;p=out/rel;p.parent.mkdir(parents=True,exist_ok=True);z=rel.endswith('.gz');p.write_bytes(gzip.compress(b,mtime=0)if z else b);assert(gzip.decompress(p.read_bytes())if z else p.read_bytes())==b;mapping.append(dict(originalPath=source,archivePath=rel,sha256=h,archiveSha256=sha(p),compression='gzip'if z else None))
records=[]
for label,raw,r,pins in caps:
 p=out/(label+'-source-image-pins.json');p.write_text(json.dumps(pins,indent=2)+'\n');v=out/(label+'.mp4');subprocess.run(['/opt/homebrew/bin/ffmpeg','-nostdin','-n','-loglevel','error','-framerate','12','-i',str(raw.with_suffix('')/'%04d.png'),'-frames:v','120','-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','20','-pix_fmt','yuv420p',str(v)],check=True);meta=json.loads(subprocess.check_output(['/opt/homebrew/bin/ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=nb_read_frames,width,height,r_frame_rate','-of','json',str(v)]))['streams'][0];assert int(meta['nb_read_frames'])==120;records.append(dict(action=label,raw=ev(out/(label+'.json.gz')),video=ev(v),videoMetadata=meta,sourceImagePins=ev(p),frames=120,rendererDisabledFrames=sum(not x['enabled']for x in r['frames']),animatorDisabledFrames=sum(not x['animator']['enabled']for x in r['frames'])))
limits=['ExactfishA01/unarmed5958 only; no A02/A03/spear/staff or other34bone variant inheritance.','Diagnostic probe only; two native-zero-weight finger endpoints are not authored-anatomy acceptance.','First dodged/noHP-loss wrapper remains stopped; separate ordinary hit58->50 and explicitfixturedeath50->0 are distinct actions, no silent retry.','Native11body ragdoll dynamic+Animatoroff26 observed; zero adjacent displacement latecapture is settling only, no IsSleeping/floorcontact/finalownerteardown proof.','Five root-reviewed original PNGs only; four lossy12fps videos are review derivatives, not all480framesviewed.','No originalart acceptance or nativegeometry/DLL/decompiledsource redistribution.']
r=dict(status='DIAGNOSTIC_SELECTED_POSES_RAGDOLL_AND_DODGE_FAILURE_PRESERVED',nativeEnemy='fishA01',rendererId=121695,controllerId=5958,rigProfile='2cb4d06da0ad32dba308fc1f7fe7900993c51486a9cb37576d854b1615c5a080',combatProfile='746deb43cc057df260abe66bad848ded92c511f22d0b0ef05162c1f7287674fc',session=d['session'],binaryPins=d['binaryPins'],assetHashes=d['assetHashes'],profileSha256=d['profileSha256'],rawStoppedWrapperStatus=d['status'],captures=records,finalReady=read(out/'fisha01-final-ready.json'),rootReview=ev(out/'fisha01-root-live-review.json'),limits=limits,losslessMappings=mapping,evidence=[ev(p)for p in sorted(out.rglob('*'))if p.is_file()]);p=out/'validation.json';p.write_text(json.dumps(r,indent=2)+'\n');assert idx.read_bytes()==old;di['calibration_probes'].append(dict(name='FishA01 34-bone diagnostic',native_chassis='fishA01',renderer_paths=['enFishA'],rig_profile=r['rigProfile'],combat_profile=r['combatProfile'],status=r['status'],evidence=dict(path='docs/evidence/fisha01-diagnostic-v1/validation.json',sha256=sha(p)),limits=limits));idx.write_text(json.dumps(di,indent=2)+'\n');print(ev(p))
