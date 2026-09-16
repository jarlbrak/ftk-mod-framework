#!/usr/bin/env python3
"""Offline Bronzehollow first trial archive; preserves frozen art files."""
from pathlib import Path
import json,gzip,hashlib,shutil,subprocess
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir());A=R/'art-experiments/bronzehollow-sentinel';O=A/'live-validation-v1';assert not O.exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text())
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
frozen={str(p.relative_to(A)):sha(p) for p in A.iterdir() if p.is_file()};S='0a532864d8004cdc85e1dba0b7e076d2';D=R/'scratch/mirewarden-game/model-test-output';sources=set();images={};caps=[];selected=[]
expected=['29b30908b7574c3583afb940a5dfea72','a37b33d48cb5447180969d3a865d7574','e10834685ab24adaa9bfdbd3af75f9d9','3a7311a8449e4ac9a7eb6d60322e8227']
for index,name in enumerate(['first-pass','first-paid-attack','second-paid-attack','explicit-death']):
 pointer=R/f'scratch/bronzehollow-{name}-result.txt';sources.add(pointer);case=Path(pointer.read_text().strip());case=case/'result.json' if case.is_dir() else case;r=read(case);assert r['ok'];sources.add(case)
 raw=Path(r['capture']['result']);d=read(raw);assert d['id']==expected[index] and d['session']==S and d['ok'] and d['error'] is None and len(d['frames'])==120;sources.add(raw)
 assert r['action']==('pass' if index==0 else 'kill-fixture' if index==3 else 'attack')
 if index in (1,2):assert r['actionResult']['result']['committed']=='Attack'
 if index==3:assert r['actionResult']['result']['committed']=='KillSingle'
 folder=raw.with_suffix('');pngs=sorted(folder.glob('*.png'));assert [p.name for p in pngs]==[f'{i:04d}.png' for i in range(120)]
 for p in pngs:images[str(p.relative_to(R))]=sha(p)
 caps.append({'label':name,'captureId':d['id'],'rawSha256':sha(raw),'frames':120})
 review=R/f'scratch/bronzehollow-root-{name if index<3 else "death"}-review.json';v=read(review);sources.add(review)
 if 'raw'in v:assert sha(R/v['raw'])==v['sha256']
 items=v.get('selectedFrames',[])
 if 'frame'in v:items=[{'path':v['frame'],'sha256':v['sha256'],'observation':v['observation']}]
 for item in items:
  p=Path(item['path']);p=p if p.is_absolute() else R/p;assert sha(p)==item['sha256']==images[str(p.relative_to(R))];selected.append((p,review,item))
for name in ['first-stage-result.txt','root-stage-review.json','411-first-startup.json','411-deployment-result.json','live-materials.json','live-inventory.json','native-loot-progression.json','after-first-paid-attack.json','after-second-paid-attack.json','native-paid-focus-architect-review.json']:
 sources.add(R/('scratch/bronzehollow-'+name))
for name in ['findings.md','native-material.json','receipt.json']:sources.add(R/'scratch/bronzehollow-material-analysis'/name)
for name in ['manifest.json','architect-trial-review.json','root-offline-review.json']:sources.add(A/name)
for name in ['after-first-paid-attack','after-second-paid-attack']:
 q=read(R/f'scratch/bronzehollow-{name}.json');state=q.get('state',q);assert state['combat']['enemies'][0]['hp']==58
loot=read(R/'scratch/bronzehollow-native-loot-progression.json');assert loot['status']=='STRICT_READY_0_2' and len(loot['actions'])==2
assert all(x['result']['ok'] and x['result']['method']=='VoteButton.OnLeftClick(Collect)' for x in loot['actions'])
seen=set()
def visit(v):
 if isinstance(v,dict):
  ident=v.get('id')
  if v.get('session')==S and isinstance(ident,str) and len(ident)==32 and all(c in '0123456789abcdef' for c in ident):
   q=D/(ident+'.json')
   if q.is_file():sources.add(q)
  for x in v.values():visit(x)
 elif isinstance(v,list):
  for x in v:visit(x)
 elif isinstance(v,str) and len(v)<512 and '\n'not in v:
  q=Path(v)
  if q.is_absolute() and q.is_relative_to(R) and q.suffix in ('.json','.jsonl','.txt','.html') and q.is_file():sources.add(q)
while sources-seen:
 assert len(sources)<1000;p=next(iter(sources-seen));seen.add(p)
 if p.parent.name.startswith('case-'):sources.update(q for q in p.parent.iterdir() if q.is_file() and q.suffix in ('.json','.jsonl','.txt','.html'))
 if p.suffix=='.json':visit(read(p))
 elif p.suffix=='.jsonl':
  for line in p.read_text().splitlines():
   if line:visit(json.loads(line))
 elif p.suffix=='.txt':
  q=Path(p.read_text().strip())
  if q.is_absolute() and q.is_file() and q.suffix=='.json':sources.add(q)
O.mkdir();m=[]
for p in sorted(sources):
 raw=p.read_bytes();dest=O/'metadata'/Path(str(p.relative_to(R))+'.gz');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(dest.read_bytes())==raw;m.append({'source':str(p.relative_to(R)),'sourceSha256':sha(p),'archive':str(dest.relative_to(O)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
write(O/'source-image-pins.json',images);selection=[]
for p,review,item in selected:
 dest=O/'selected'/p.parent.name/p.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);selection.append({'source':str(p.relative_to(R)),'archive':str(dest.relative_to(O)),'sha256':sha(dest),'review':str(review.relative_to(R)),'observation':item['observation']})
for cap in caps:
 dest=O/(cap['label']+'.mp4');subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-framerate','12','-i',str(D/cap['captureId']/'%04d.png'),'-frames:v','120','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(dest)],check=True)
 info=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=nb_read_frames,width,height','-of','json',str(dest)]))['streams'][0];assert int(info['nb_read_frames'])==120;cap.update(video=dest.name,videoSha256=sha(dest),verification=info,playbackFps=12,timing='Presentation derivative, not unperturbed timing')
shutil.copy2(__file__,O/'archive.py')
(O/'README.md').write_text('''# Bronzehollow first live trial

Catalog411/session0a532864d8004cdc85e1dba0b7e076d2: four complete120-frame recordings preserve native pass, two ordinary paid-focus attacks, and explicit KillSingle death. Both ordinary attacks show BLOCKED and enemyHP58 unchanged. Two native Collects lead to strict Ready0/2. This trial establishes no ordinary damaging hit or lethal-damage acceptance.

The original authored body is visible under retained native helmet, shield and weapon; the original design does not replace those parts. Root selected reviews record connected body poses with substantial equipment/hero/FX occlusion. The live body appears substantially darker/reddish than the studio version. Existing emission readback rules out retained native body emission at that sample; metallic/color/lighting explanations remain uncertain. Offline native metallic-map point samples are metadata, not GPU-filtered appearance proof. No appearance fix or full art acceptance is inferred.

Native ragdoll observations and selected corpse frames do not prove collision/sleeping, all animation clearances, accessory ownership or full resource lifetime. Explicit KillSingle is a fixture, not ordinary lethal damage. Paid-focus manual/CLI callback proof is separately archived at ../../../docs/evidence/native-paid-focus-v1 and does not make either blocked attack a successful hit.

All480 source PNG hashes, root-selected originals, four120-frame presentation MP4s and lossless metadata/journals/request-result mappings are retained. Startup, stage, inventory/material observations and metadata-only material analysis are included. Native payload, DLLs and decompiled source are excluded. Frozen original art files/manifests remain unchanged; this new subdirectory is the live-trial supplement. archive.py verifies inputs offline and refuses an existing destination.
''')
assert all(sha(A/p)==h for p,h in frozen.items())
write(O/'validation.json',{'status':'first_live_trial_motion_and_blocked_attacks_only','catalog':411,'session':S,'captures':caps,'ordinaryHit':False,'ordinaryLethal':False,'artAccepted':False,'fullLifetimeAccepted':False,'nativeCollects':2,'finalReady':{'level':0,'room':2},'retainedNativeParts':['helmet','shield','weapon'],'appearance':'Darker/reddish versus studio; cause unresolved','paidFocusEvidence':'../../../docs/evidence/native-paid-focus-v1','losslessMappings':m,'sourceImageCount':480,'sourceImagePins':'source-image-pins.json','selectedPNGs':selection,'frozenArtFilePins':frozen,'artifactSha256':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()}})
print(json.dumps({'archive':str(O),'validationSha256':sha(O/'validation.json'),'losslessMappings':len(m),'selected':len(selection),'sourceImages':480}))
