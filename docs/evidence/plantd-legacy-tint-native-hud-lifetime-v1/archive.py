#!/usr/bin/env python3
"""Archive reviewed terminal PlantD evidence offline; never calls the game."""
from pathlib import Path
import json,gzip,hashlib,shutil,subprocess
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir());O=R/'docs/evidence/plantd-legacy-tint-native-hud-lifetime-v1';assert not O.exists(),'Preserve existing archive'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text())
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
afile=R/'scratch/plantd-legacy-tint-final-architect-review.json';vfile=R/'scratch/plantd-legacy-tint-root-visual-review.json';a=read(afile);v=read(vfile)
assert sha(afile)=='53664b44a3e17706fa767e4cfdda3284312e1d104bd6ea07fb9cec02a2550ee4';assert sha(vfile)=='fb665b3940eb24a07121103877c1d3094fc3a96a9a80aef7f4ea6062ac79e53a'
assert a['status']=='VERIFIED_NATIVE_CLONE_FIRST_SINGULAR_PLUS_TINT_FINAL_DISPOSAL'
S=a['session'];D=R/'scratch/mirewarden-game/model-test-output';case=Path((R/'scratch/plantd-legacy-tint-exercise-result.txt').read_text().strip());c=read(case)
assert c['session']==S and c['status']=='needs_visual_review' and len(c['collects'])==1
assert c['actions'][1]['hpOutcome']['beforeHp']==58 and c['actions'][1]['hpOutcome']['afterHp']==50
assert c['finalReady']['strictReady']['ok'] and c['finalReady']['strictReady']['level']==0 and c['finalReady']['strictReady']['room']==3
initial=read(R/'scratch/plantd-legacy-tint-initial-lifetime.json')['watch'];final=read(R/'scratch/plantd-legacy-tint-final-lifetime.json')
assert final['ok'] and final['errors']==[] and final['readoutPinsValid'] and final['historyComplete'] and final['session']==S
lease=final['lastState']['leases'][0];assert lease['leaseId']==5 and not lease['present'] and lease['references']==0
assert len(lease['observedResourceUnion'])==6 and all(x['unityNull'] for x in lease['observedResourceUnion'])
assert {x['instanceId'] for x in lease['observedResourceUnion']}=={x['instanceId'] for x in a['resourceUnionDisposed']}
assert len(final['lastState']['owners'])==2 and all(x['ownerUnityNull'] and x['celUnityNull'] and not x['inObservedDioramaCleanup'] for x in final['lastState']['owners'])
assert all(not x['unityNull'] for x in final['lastState']['nativeResources'])
hud=final['lastState']['hudTextures'][0];assert hud['textureUnityNull'] and not hud['dictionaryContainsFid'] and not hud['cameraUnityNull'] and not hud['renderTextureUnityNull'] and hud['cameraTargetCleared'] and hud['cameraTextureCleared']
sources={afile,vfile,case,R/'scratch/plantd-legacy-tint-exercise-result.txt'};images={};refs={};captures=[]
for review in [a,read(R/'scratch/plantd-legacy-tint-initial-architect-review.json')]:
 for name,h in review['pins'].items():
  p=Path(name);p=p if p.is_absolute() else R/p;assert sha(p)==h,p
  if p.suffix in ('.json','.jsonl','.txt','.html'):sources.add(p)
  else:refs[str(p)]=h
for action in c['actions']:
 raw=action['capture']['rawCapture'];p=Path(raw['path']);assert sha(p)==raw['sha256'];d=read(p);assert d['ok'] and d['error'] is None and d['session']==S and len(d['frames'])==120
 sources.add(p);captures.append({'action':action['action'],'captureId':d['id'],'rawSha256':sha(p),'frames':120})
 for image in action['capture']['images']:
  q=Path(image['path']);assert sha(q)==image['sha256'];images[str(q.relative_to(R))]=image['sha256']
 for key in ['journal','rawResult']:
  q=Path(action[key]['path']);assert sha(q)==action[key]['sha256'];sources.add(q)
assert len(images)==360
for item in v['frames']:
 p=Path(item['path']);assert sha(p)==item['sha256']==images[str(p.relative_to(R))]
assert [(x['action'],x['index']) for x in v['frames']]==[('pass',0),('pass',45),('attack',30),('kill-fixture',60)]
# Expand referenced metadata and actual raw helper IDs, never native/art binaries.
def visit(value):
 if isinstance(value,dict):
  id=value.get('id')
  if value.get('session')==S and isinstance(id,str) and len(id)==32 and all(x in 'abcdef0123456789' for x in id):
   q=D/(id+'.json')
   if q.is_file():sources.add(q)
  for x in value.values():visit(x)
 elif isinstance(value,list):
  for x in value:visit(x)
 elif isinstance(value,str) and len(value)<512 and '\n' not in value:
  q=Path(value)
  if q.is_absolute() and q.is_relative_to(R) and q.suffix in ('.json','.jsonl','.html','.txt') and q.is_file():sources.add(q)
seen=set()
while sources-seen:
 assert len(sources)<1000
 p=next(iter(sources-seen));seen.add(p)
 if p.parent.name.startswith('case-'):sources.update(x for x in p.parent.iterdir() if x.is_file() and x.suffix in ('.json','.jsonl','.html','.txt'))
 if p.suffix=='.json':visit(read(p))
 elif p.suffix=='.jsonl':
  for line in p.read_text().splitlines():
   if line:visit(json.loads(line))
O.mkdir(parents=True);m=[]
for p in sorted(sources):
 raw=p.read_bytes();dest=O/'metadata'/Path(str(p.relative_to(R))+'.gz');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(dest.read_bytes())==raw
 m.append({'source':str(p.relative_to(R)),'sourceSha256':sha(p),'archive':str(dest.relative_to(O)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
write(O/'source-image-pins.json',images);selected=[]
for item in v['frames']:
 p=Path(item['path']);dest=O/'selected'/p.parent.name/p.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);assert sha(dest)==item['sha256'];selected.append(dict(item,archive=str(dest.relative_to(O))))
for cap in captures:
 folder=D/cap['captureId'];assert [p.name for p in sorted(folder.glob('*.png'))]==[f'{i:04d}.png' for i in range(120)]
 dest=O/(cap['action']+'.mp4');subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-framerate','12','-i',str(folder/'%04d.png'),'-frames:v','120','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(dest)],check=True)
 info=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,nb_read_frames','-of','json',str(dest)]))['streams'][0];assert int(info['nb_read_frames'])==120
 cap.update(video=dest.name,videoSha256=sha(dest),videoVerification=info,playbackFps=12,timing='Presentation derivative, not unperturbed wall/game timing')
shutil.copy2(__file__,O/'archive.py')
(O/'README.md').write_text('''# PlantD singular body + tint native HUD lifetime

One exact PlantD/enJungleNibbler_C calibration fixture supports public singular GLB plus nonwhite tint ownership through native combat source → HUD clone → clone-first release → final source release. Independent review joins lease5 references1→2→1→0. Both owner/CEL pairs disappear from native cleanup membership and become Unity-null. All six pinned owned resources become Unity-null; five pinned native assets remain alive.

The measured body _Color approximately[0.6,0.8,1,1] matches the configured tint on source and native HUD clone. This proves property application, not rendered tint-color fidelity or every FX color. Six owned resources include a prior body tint material, two inactive FX material copies, the singular mesh, PNG texture and final body material. Native HUD output texture disappears from its FID dictionary; the shared native camera/render texture intentionally survives with cleared pointers.

Pass, normal58→50 hit and explicit KillSingle death each retain120frames. Exactly ONE native Collect ends at strict Ready level0/room3. KillSingle is not ordinary lethal damage. No observer Retain/Release/Destroy/prune operation manufactured disposal.

Root reviewed four selected originals: pass0000/0045, hit0030 and death0060. The calibration body is visible/assembled, with hero/FX occlusion in selected poses; it is not finished original art or full-frame/animation/material acceptance. Floor appearance does not prove physics collision/sleeping. Source-first and never-active destruction orders remain unproven; no global leak claim.

Raw metadata, commands/results, journals, lifetime observations and both review stages are lossless gzip. All360 PNG hashes, four selected originals and three120-frame presentation videos are retained. Native assets, DLLs and decompiled source are excluded; hashes remain provenance references. archive.py verifies inputs offline and refuses an existing destination.
''')
write(O/'validation.json',{'status':a['status'],'session':S,'initialFrame':a['initialFrame'],'finalFrame':a['finalFrame'],'leaseId':5,'referenceSequence':a['referenceSequence'],'resourceUnionDisposed':a['resourceUnionDisposed'],'nativeResourcesSurvive':a['nativeResourcesSurvive'],'hudTextureFinal':a['hudTextureFinal'],'ordinaryHit':{'before':58,'after':50},'ordinaryLethal':False,'explicitKillFixture':True,'nativeCollects':1,'finalReady':a['strictReady'],'limits':a['limits']+v['limitations'],'losslessMappings':m,'sourceImageCount':360,'sourceImagePins':'source-image-pins.json','selectedPNGs':selected,'captures':captures,'excludedPayloadHashReferences':refs,'artifactSha256':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()}})
print(json.dumps({'archive':str(O),'validationSha256':sha(O/'validation.json'),'losslessMappings':len(m),'sourceImages':360,'selected':4}))
