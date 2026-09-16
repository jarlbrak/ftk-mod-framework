#!/usr/bin/env python3
"""Offline, exclusive-output evidence archive. No game/HTTP/deployment operations."""
from pathlib import Path
import gzip,hashlib,json,shutil,subprocess
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir())
O=R/'docs/evidence/deathknighta-diagnostic-v1'; assert not O.exists(),'Preserve existing archive'
D=R/'scratch/mirewarden-game/model-test-output'; S='5856168eea154be3adecc2a5d1cb2330'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
case=D/'case-6fbcda28073f46f8a87e50d6410e2343/case-result.json';c=load(case)
assert c['session']==S and c['status']=='stopped' and c['error']=='Attack gate unmet: no_hp_loss_unclassified'
assert c['actions'][1]['before']['combat']['enemies'][0]['hp']==58==c['actions'][1]['after']['combat']['enemies'][0]['hp']
death=D/'case-08bc392d1f12490db8f93bd288404b3e/result.json';dr=load(death)
assert dr['ok'] and dr['action']=='kill-fixture' and dr['actionResult']['result']['committed']=='KillSingle'
ids=[('pass','cc28a1e0350e48a3bc927d1894bea810'),('blocked-normal-attack','79e0edfed0c049eebd4788919e9770d6'),('explicit-kill-fixture','3c32bd1578be482c87d3353aabdd5938')]
sources={case,death};pins={};captures=[];sourceIdentity=None
for label,id in ids:
 p=D/(id+'.json');d=load(p);assert d['ok'] is True and d['error'] is None and d['id']==id and d['session']==S and len(d['frames'])==120
 identity=(d['ownerInstanceId'],d['celInstanceId']);sourceIdentity=sourceIdentity or identity;assert identity==sourceIdentity
 pngs=sorted((D/id).glob('*.png'));assert [x.name for x in pngs]==[f'{i:04d}.png' for i in range(120)]
 for i,f in enumerate(d['frames']):
  assert (f['ownerInstanceId'],f['celInstanceId'])==identity and f['celRelativeRendererPath']=='deathKnight'
  if i:assert f['frame']==d['frames'][i-1]['frame']+1
 for f in pngs:pins[str(f.relative_to(R))]=sha(f)
 sources.add(p);captures.append({'action':label,'captureId':id,'sourceSha256':sha(p),'frames':120,'firstFrame':d['frames'][0]['frame'],'lastFrame':d['frames'][-1]['frame']})
for a in c['actions']:
 for image in a['capture']['images']:assert pins[str(Path(image['path']).relative_to(R))]==image['sha256']
 for k in ['journal','rawResult']:assert sha(Path(a[k]['path']))==a[k]['sha256'];sources.add(Path(a[k]['path']))
for p in list(sources):
 if p.parent.name.startswith('case-'):sources.update(x for x in p.parent.iterdir() if x.is_file() and x.suffix in ('.json','.jsonl','.html','.txt'))
reviews=[R/'scratch/deathknighta-root-partial-diagnostic-review.json',R/'scratch/deathknighta-root-death-review.json'];selected=[]
for p in reviews:
 sources.add(p)
 for f in load(p)['selectedFrames']:
  q=R/f['path'];assert sha(q)==f['sha256']==pins[f['path']];selected.append((q,p,f))
assert len(selected)==6 and len(pins)==360
for name in ['first-diagnostic-exercise-result.txt','explicit-death-result.txt','collect1.json','after-collect1.json','collect2.json','after-collect2.json']:
 sources.add(R/('scratch/deathknighta-'+name))
collects=[load(R/f'scratch/deathknighta-collect{i}.json') for i in (1,2)]
for v in collects:assert v['fixture']['strictLootCollect']['ok'] and v['result']['ok'] and v['result']['method']=='VoteButton.OnLeftClick(Collect)' and v['result']['session']==S
assert collects[0]['id']!=collects[1]['id'] and collects[0]['result']['frame']<collects[1]['result']['frame']
after1=load(R/'scratch/deathknighta-after-collect1.json');after2=load(R/'scratch/deathknighta-after-collect2.json')
assert [collects[0]['before']['party'][0]['gold'],after1['state']['party'][0]['gold'],after2['state']['party'][0]['gold']]==[18,38,38]
assert after2['fixture']['strictReady']['ok'] and after2['fixture']['strictReady']['level']==0 and after2['fixture']['strictReady']['room']==5 and after2['fixture']['dungeon']['queuedRoomType']=='Stair'
# Keep all embedded requests/results and referenced per-case helper metadata losslessly.
seen=set();excluded={}
def walk(v):
 if isinstance(v,dict):
  if isinstance(v.get('path'),str) and isinstance(v.get('sha256'),str):
   p=Path(v['path']);p=p if p.is_absolute() else R/p
   if p.exists() and p.is_file() and p.suffix in ('.dll','.glb','.png','.cs'):excluded[str(p)]=v['sha256']
  for x in v.values():yield from walk(x)
 elif isinstance(v,list):
  for x in v:yield from walk(x)
 elif isinstance(v,str):yield v
while sources-seen:
 assert len(sources)<1000
 p=next(iter(sources-seen));seen.add(p)
 if p.suffix not in ('.json','.jsonl'):continue
 values=[load(p)] if p.suffix=='.json' else [json.loads(x) for x in p.read_text().splitlines() if x]
 for v in values:
  for text in walk(v):
   if len(text)>512 or '\n' in text:continue
   q=Path(text)
   if not q.is_absolute():continue
   if not q.is_relative_to(R) or q.suffix not in ('.json','.jsonl','.html','.txt') or not q.is_file():continue
   if 'deathknighta' in q.name and 'stairs' in q.name:continue
   sources.add(q)
O.mkdir(parents=True);m=[]
for p in sorted(sources):
 raw=p.read_bytes();dest=O/'metadata'/Path(str(p.relative_to(R))+'.gz');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(dest.read_bytes())==raw
 m.append({'source':str(p.relative_to(R)),'sourceSha256':hashlib.sha256(raw).hexdigest(),'archive':str(dest.relative_to(O)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
write(O/'source-image-pins.json',pins);selectedRecords=[]
for p,review,item in selected:
 dest=O/'selected'/p.parent.name/p.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);assert sha(dest)==item['sha256']
 selectedRecords.append({'source':str(p.relative_to(R)),'archive':str(dest.relative_to(O)),'sha256':sha(dest),'review':str(review.relative_to(R)),'observation':item['observation']})
for capture in captures:
 dest=O/(capture['action']+'.mp4');subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-framerate','12','-i',str(D/capture['captureId']/'%04d.png'),'-frames:v','120','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(dest)],check=True)
 info=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,nb_read_frames','-of','json',str(dest)]))['streams'][0];assert int(info['nb_read_frames'])==120
 capture.update(video=dest.name,videoSha256=sha(dest),videoVerification=info,playbackFps=12,timing='Presentation derivative, not unperturbed wall/game timing')
shutil.copy2(__file__,O/'archive.py')
(O/'README.md').write_text('''# DeathknightA diagnostic capture

Exact deathknightA / deathKnight renderer121217 calibration probe: native pass, blocked normal attack, and explicit KillSingle death each retain120 frames. All three share the same native owner/CEL in session5856168eea154be3adecc2a5d1cb2330. Native rigid helmet, shield and weapon remain; this is calibration, not an original finished model or variant-wide acceptance.

The ordinary attack left actual enemyHP58 unchanged. Root's selected frame shows BLOCKED; the immutable wrapper correctly stopped with no_hp_loss_unclassified. This archive does not relabel that raw result or claim an ordinary damaging hit. A separately authorized KillSingle fixture produced the death recording; it is not ordinary lethal damage.

Two native Collect submissions advance gold18→38→38 and end at strict Ready level0/room5, with a queued Stair beyond the five definition rooms. No completed stair traversal or next-floor progression is claimed. The later first Ready merely changed the active encounter to Stair; that unfinished traversal is excluded from this archive's success boundary.

Root reviewed six selected originals: body/retained equipment, attack pose, daze/damage obstruction, BLOCKED, collapsed probe, and loot/level-up obstruction. Only those selected frames were reviewed; no all-frame or fine corpse surface acceptance. Native Animator disabling during ragdoll does not prove sleeping, floor collision or final resource disposal. RuntimeHP58 differs from source baseHP50; no unmeasured scaling explanation is asserted.

Metadata, journals, action results and fixture readbacks are lossless gzip with SHA mappings. All360 source PNG hashes, six selected originals and three120-frame MP4 derivatives are retained. Videos are presentation sequences at12fps, not unperturbed timing. Native payload, DLLs and decompiled C# are excluded. The separate native-source archive is ../deathknight-native-source-v1. archive.py is offline-only and refuses an existing destination.
''')
write(O/'validation.json',{'status':'diagnostic_pass_block_and_explicit_fixture_only','session':S,'ownerIdentity':sourceIdentity,'captures':captures,'ordinaryHit':False,'ordinaryLethal':False,'calibrationNotOriginal':True,'nativeCollects':2,'gold':[18,38,38],'finalReady':after2['fixture']['strictReady'],'queuedRoomType':'Stair','completedStairTraversal':False,'losslessMappings':m,'sourceImageCount':360,'sourceImagePins':'source-image-pins.json','selectedPNGs':selectedRecords,'excludedPayloadHashReferences':excluded,'artifactSha256':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()}})
print(json.dumps({'archive':str(O),'validationSha256':sha(O/'validation.json'),'losslessMappings':len(m),'sourceImages':360,'selected':len(selectedRecords)}))
