#!/usr/bin/env python3
"""Archive the Mirewarden native-scale live trial without game payloads."""
from pathlib import Path
import gzip,hashlib,json,os,shutil,subprocess
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir())
ASSET=ROOT/'art-experiments/mirewarden-ftk'; OUT=ASSET/'live-validation-v2'; BASE=ROOT/'scratch/mirewarden-game/model-test-output'
CASE=ROOT/'scratch/mirewarden-v2-standalone-case.json'; REVIEW=ROOT/'scratch/mirewarden-v2-root-visual-review.json'; PROFILE=ROOT/'scratch/mirewarden-game/model-test-profiles.json'; SESSION='5754790cd92e4557b28b6f728024815c'; FORBIDDEN={'.dll','.assets','.resources','.bundle','.exe','.app','.unity3d'}
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest(); read=lambda p:json.loads(Path(p).read_text())
def write(p,v): Path(p).parent.mkdir(parents=True,exist_ok=True); Path(p).write_text(json.dumps(v,indent=2)+'\n')
assert not (OUT/'validation.json').exists() or os.environ.get('FTK_ARCHIVE_REBUILD')=='1','refusing to overwrite a completed archive'
OUT.mkdir(parents=True,exist_ok=True); case=read(CASE); review=read(REVIEW); manifest=read(ASSET/'asset-manifest.json')
assert case['status']=='needs_visual_review' and case['session']==SESSION and case['enemy']=='ftkmf_modeltest_mirewarden'; assert [a['frames'] for a in case['actions']]==[120,120,120]; assert all(a['complete'] for a in case['actions']); assert case['nativeCollects']==2; assert review['session']==SESSION and len(review['frames'])==5
assets={k:(v['sha256'] if isinstance(v,dict) else v) for k,v in manifest.items() if isinstance(v,dict) and 'sha256' in v}; sources={CASE,REVIEW,PROFILE,ASSET/'asset-manifest.json',ASSET/'live-validation.json',ASSET/'live-validation-native-scale.json'}
sources.update(ASSET/name for name in assets if (ASSET/name).is_file()); sources.add(BASE/f'new-run-session-{SESSION}.json'); sources.add(ROOT/case['setup']['bindingResult']); sources.add(ROOT/'scratch/mirewarden-scale-ready.json')
for p in case['progressFiles']: sources.add(ROOT/p)
for h in case['helpers']: sources.add(ROOT/h['path'])
for a in case['actions']:
 sources.update({ROOT/a['journal'],ROOT/a['rawResult'],ROOT/a['summaryPath']}); sources.update((ROOT/a['rawResult']).with_suffix('').glob('*.png'))
for f in case['playerApparel'].values():
 if isinstance(f,str) and f.startswith('scratch/') and f.endswith('.json'): sources.add(ROOT/f)
for f in review['frames']: sources.add(ROOT/f['path'])
for p in ASSET.rglob('*'):
 if p.is_file() and not any(x.startswith('live-validation-') for x in p.parts) and '__pycache__' not in p.parts and p.suffix!='.pyc': sources.add(p)
journals={p for p in sources if p.suffix=='.jsonl'}; seen=set()
while journals:
 j=journals.pop()
 if j in seen or not j.is_file(): continue
 seen.add(j)
 for line in j.read_text().splitlines():
  value=json.loads(line).get('data',{}).get('path')
  if not value: continue
  p=Path(value)
  if not p.is_file(): continue
  try:p.relative_to(ROOT)
  except ValueError:continue
  if p.suffix.lower() in FORBIDDEN:continue
  sources.add(p)
  if p.suffix=='.jsonl':journals.add(p)
for p in sources: assert p.is_file() and not p.is_symlink() and p.suffix.lower() not in FORBIDDEN,p
pins={}; mappings=[]
for source in sorted(sources):
 raw=source.read_bytes(); rel=source.relative_to(ROOT)
 if rel.suffix.lower()=='.png':pins[str(rel)]=hashlib.sha256(raw).hexdigest();continue
 dest=OUT/'metadata'/(str(rel)+'.gz');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(dest.read_bytes())==raw
 mappings.append({'source':str(rel),'sourceSha256':hashlib.sha256(raw).hexdigest(),'archive':str(dest.relative_to(OUT)),'archiveSha256':sha(dest),'encoding':'gzip-lossless'})
write(OUT/'source-image-pins.json',pins);write(OUT/'asset-pins.json',assets)
selected=[]
for f in review['frames']:
 src=ROOT/f['path'];dest=OUT/'selected'/f"{f['action']}-{f['index']:04d}.png";dest.parent.mkdir(parents=True,exist_ok=True);assert sha(src)==f['sha256'];shutil.copy2(src,dest);selected.append({'source':f['path'],'archive':str(dest.relative_to(OUT)),'sha256':sha(dest),'action':f['action'],'index':f['index'],'observation':f['observation']})
videos=[];captures=[]
for a in case['actions']:
 rawp=ROOT/a['rawResult'];raw=read(rawp);d=rawp.with_suffix('');assert len(raw['frames'])==a['frames'] and len(list(d.glob('*.png')))==a['frames'];video=OUT/f"{a['label']}.mp4";subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-framerate','12','-i',str(d/'%04d.png'),'-frames:v',str(a['frames']),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True);info=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=nb_read_frames,width,height','-of','json',str(video)]))['streams'][0];assert int(info['nb_read_frames'])==a['frames'];videos.append({'label':a['label'],'captureId':rawp.stem,'path':video.name,'sha256':sha(video),'frames':a['frames'],'width':int(info['width']),'height':int(info['height']),'playbackFps':12,'timing':'Presentation derivative, not unperturbed timing'});captures.append({'label':a['label'],'captureId':rawp.stem,'case':a['case'],'frames':a['frames'],'rawSha256':sha(rawp),'summarySha256':sha(ROOT/a['summaryPath']),'width':raw['width'],'height':raw['height'],'requestedFps':raw['requestedFps'],'complete':a['complete'],'termination':a['termination'],'captureReportedOk':a['captureReportedOk'],'captureError':a['captureError'],'actualGameSeconds':a['actualGameSeconds']})
validation={'status':'fresh_native_scale_single_renderer_complete_hit_kill_ragdoll_and_ready_observed','session':SESSION,'enemy':case['enemy'],'displayName':case['displayName'],'nativeChassis':case['nativeChassis'],'frameworkVersionPrefix':case['frameworkVersionPrefix'],'profileInputSha256':case['profileInputSha256'],'assetHashes':assets,'binding':case['binding'],'captures':captures,'ordinaryAttack':case['ordinaryAttack'],'ordinaryLethal':False,'explicitKillFixture':case['explicitKillFixture'],'nativeCollects':case['nativeCollects'],'finalReady':case['finalReady'],'progressFiles':case['progressFiles'],'playerApparel':case['playerApparel'],'deathRagdoll':{'animatorFirstDisabledFrame':28,'nativeRagdollFlag':True,'recordedNativeRigidbodies':14,'limits':'Rock-segment gaps and separated chunks remain visible.'},'historicalEvidence':case['historicalEvidence'],'scale':{'requestedFactor':1.0,'nativePrefabRootLocalScale':[0.949999988079071]*3,'observedRendererWorldAxisLengths':[0.9500001072883606,0.949999988079071,0.9500001072883606]},'progression':'The native-scale process bound enTroll01 at root scale 0.95, recorded complete pass/nonlethal-hit/KillSingle captures with ordinary HP 50 to 42, accepted two native Collects into strict Ready at level 0 room 2, and retained the native ragdoll boundary.','selectedPNGs':selected,'videos':videos,'sourceImageCount':len(pins),'sourceImagePins':'source-image-pins.json','assetPins':'asset-pins.json','losslessMappings':mappings,'standaloneCase':str(CASE.relative_to(ROOT)),'rootVisualReview':str(REVIEW.relative_to(ROOT)),'limits':case['limits']}
write(OUT/'validation.json',validation)
(OUT/'README.md').write_text(f'''# Mirewarden native-scale live trial V2\n\nThis supplement records the native-scale catalog process (`{SESSION}`) for the exact `trollCaveA` / `enTroll01` body. Public visual factor `1.0` preserves the native root scale `0.95`; the authored renderer stays attached to one enemy owner.\n\nPass, ordinary nonlethal hit and explicit `KillSingle` captures each contain 120 unpaused frames. Ordinary damage resolves HP `50→42` with `cheat=None`; the fixture kill commits `42→0` and preserves the native ragdoll boundary. One native Collect sequence reaches strict Ready at level `0` / room `2` after two guarded Collect actions.\n\nThe selected views preserve readable attack/hit motion and the native ragdoll. Rock-segment gaps and separated limb chunks remain visible, so this is scoped scale/binding/progression evidence rather than finished-art acceptance. Player apparel motion is retained as a separate helper observation with native equipment occlusion.\n\n`validation.json` preserves the case, continuation helpers, journals, complete raw captures, historical evidence, authoring files and source hashes as gzip-lossless metadata. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.\n''')
print(json.dumps({'validation':str(OUT/'validation.json'),'sourceImages':len(pins),'losslessMappings':len(mappings),'selected':len(selected),'videos':len(videos),'validationSha256':sha(OUT/'validation.json')},indent=2))
