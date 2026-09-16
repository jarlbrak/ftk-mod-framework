#!/usr/bin/env python3
"""Offline archival of terminal legacy singular lifetime evidence; no game operations."""
from pathlib import Path
import gzip,hashlib,json,shutil,subprocess
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir())
O=R/'docs/evidence/legacy-singular-native-hud-lifetime-v1'
assert not O.exists(), 'Preserve existing archive'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v): p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
a= json.loads((R/'scratch/reefstrider-legacy-final-architect-review.json').read_text())
sources=set(); imagepins={}; excluded={}
for k,h in a['pins'].items():
 p=Path(k);p=p if p.is_absolute() else R/p
 assert sha(p)==h,p
 if p.suffix=='.png': imagepins[str(p.relative_to(R))]=h
 elif p.suffix in ('.json','.jsonl','.txt','.html'): sources.add(p)
 else: excluded[str(p)]=h
for p in R.joinpath('scratch').glob('reefstrider-legacy-*'):
 if p.is_file() and p.suffix in ('.json','.txt'): sources.add(p)
for p in list(sources):
 if p.parent.name.startswith('case-'):
  sources.update(x for x in p.parent.rglob('*') if x.is_file() and x.suffix in ('.json','.jsonl','.txt','.html'))
# Archive active profile as read-only provenance only while its pinned409 is unchanged.
catalog=R/'scratch/mirewarden-game/model-test-profiles.json'
assert sha(catalog)=='fa09602d975226776751ef6a57a437dba473113c89529205ab5c6e0b5f898ab3'
sources.add(catalog)
assert len(imagepins)==360
O.mkdir(parents=True);m=[]
for p in sorted(sources):
 rel=p.relative_to(R);dest=O/'metadata'/Path(str(rel)+'.gz');dest.parent.mkdir(parents=True,exist_ok=True)
 raw=p.read_bytes();dest.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(dest.read_bytes())==raw
 m.append({'source':str(rel),'sourceSha256':sha(p),'archive':str(dest.relative_to(O)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
write(O/'source-image-pins.json',imagepins)
selected=[]
for p in sorted(R.joinpath('scratch').glob('reefstrider-legacy-root-*-review.json')):
 d=json.loads(p.read_text()); frames=d.get('selectedFrames',[])
 if 'frame' in d:frames=[{'path':d['frame'],'sha256':d['frameSha256']}]
 for item in frames:
  q=R/item['path'];assert sha(q)==item['sha256'];dest=O/'selected'/q.parent.name/q.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(q,dest)
  selected.append({'source':item['path'],'archive':str(dest.relative_to(O)),'sha256':sha(dest),'review':str(p.relative_to(R))})
assert len(selected)==6
videos=[]
for c in a['captures']:
 src=Path(c['raw']).with_suffix('');dest=O/(c['action']+'.mp4')
 assert len(list(src.glob('*.png')))==120
 subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-framerate','12','-i',str(src/'%04d.png'),'-frames:v','120','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(dest)],check=True)
 info=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,nb_read_frames','-of','json',str(dest)]))['streams'][0]
 assert int(info['nb_read_frames'])==120
 videos.append({'action':c['action'],'captureId':c['id'],'archive':dest.name,'sha256':sha(dest),'verification':info,'playbackFps':12,'timing':'Presentation sequence, not unperturbed wall/game timing'})
shutil.copy2(__file__,O/'archive.py')
(O/'README.md').write_text('''# Legacy singular native HUD lifetime\n\nOne exact Reefstrider/fishA01 singular-body lineage supports source → native HUD clone → clone-first release → final source release. Architect joined lease5 and initial IDs through frame142891 to149161 in session5856168eea154be3adecc2a5d1cb2330. All three exact owned Mesh/Texture2D/Material wrappers become Unity-null; five pinned native assets remain alive. The HUD output Texture2D is destroyed and its dictionary entry removed; the native cached camera/render texture intentionally survives with cleared pointers.\n\nTint was NOT EXERCISED: matLoot triggered the existing weapon-material exclusion, so measured _Color stayed white. This does not validate combined singular+tint material ownership.\n\nThree complete120-frame recordings preserve native pass, ordinary58→50 hit, and explicit KillSingle death. Two native Collect actions lead to strict Ready0/3. Explicit death is not ordinary lethal damage. No direct Destroy/Release/prune commands were used in the reviewed cleanup sequence.\n\nRoot reviewed six selected PNGs: body and HUD portraits visible, raised-arm pose, native -8 hit with hero occlusion, falling corpse, and later loot-panel occlusion. Neither every frame nor every angle was visually accepted. No source-first/never-active clone proof, global leak claim, floor collision or physics sleeping acceptance.\n\nAll raw capture metadata and journals are lossless gzip. All360 source PNG hashes are retained; six selected originals and three120-frame MP4 derivatives are archived. Native payload, DLLs and decompiled C# are excluded; source hashes remain provenance references. Reproduction is offline and refuses an existing output directory.\n''')
v={'status':a['status'],'scope':'Exact Reefstrider legacy-singular body, one native HUD clone lineage; combined tint gate remains open','session':a['session'],'initialFrame':a['initialFrame'],'finalFrame':a['finalFrame'],'leaseId':5,'ordinaryHit':a['ordinaryHit'],'death':a['death'],'nativeCollects':2,'finalReady':a['finalReady'],'tintStatus':a['tintStatus'],'limitations':a['limits'],'losslessMappings':m,'sourceImageCount':360,'sourceImagePins':'source-image-pins.json','selectedPNGs':selected,'videos':videos,'excludedPayloadHashReferences':excluded,'architectReview':'metadata/scratch/reefstrider-legacy-final-architect-review.json.gz','artifactSha256':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()}}
write(O/'validation.json',v);print(json.dumps({'archive':str(O),'validationSha256':sha(O/'validation.json'),'losslessMappings':len(m),'artifacts':len(v['artifactSha256'])}))
