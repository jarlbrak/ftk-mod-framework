import json,gzip,hashlib,subprocess
from pathlib import Path
R=Path.cwd();B=R/'scratch/mirewarden-game/model-test-output';O=R/'docs/evidence/monkeyc-arrival-native-v1';read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p)}
rawpath=B/'3dcae051935d428bb2a62178954f8a67.json';raw=read(rawpath);case=B/'case-ecfafdc06568405c8160489fe1f0ed86';trial=read(case/'arrival-trial-result.json');review=read(R/'scratch/monkeyc-arrival-root-live-review.json');f=raw['frames']
assert raw['ok'] and len(f)==120 and sha(rawpath)==trial['rawCaptureSha256']
assert raw['arrival']['initialSnapshot']['currentAttackInfo']['proficiency']=='enDiseaseHit'
assert [i for i,x in enumerate(f) if not x['active']]==list(range(108,120))
assert not (O/'validation.json').exists();O.mkdir(parents=True,exist_ok=True);maps=[]
def save(p,rel,expected=None):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.assets','.npz','.glb','.blend','.cs');b=p.read_bytes();h=hashlib.sha256(b).hexdigest()
 if expected:assert h==expected
 q=O/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(gzip.compress(b,mtime=0) if rel.endswith('.gz') else b);assert (gzip.decompress(q.read_bytes()) if rel.endswith('.gz') else q.read_bytes())==b
 maps.append({'source':str(p),'sourceSha256':h,'archive':ev(q),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'})
save(rawpath,'arrival.json.gz',trial['rawCaptureSha256'])
for p in case.iterdir():
 if p.suffix in ('.json','.jsonl','.html'):save(p,'case/'+p.name+'.gz')
for line in (case/'journal.jsonl').read_text().splitlines():
 e=json.loads(line)
 if e.get('kind')=='helper-result':
  p=e.get('data',{}).get('path')
  if p and Path(p).is_file() and not any(m['source']==p for m in maps):save(p,'helper-results/'+Path(p).name+'.gz')
for name in ['run-monkey-arrival-once.py','monkeyc-arrival-root-live-review.json','mournglass-arrival-first-startup.json','mournglass-arrival-deployment-result.json']+['monkeyc-arrival-'+s+'.json' for s in ['collect1-before','collect1','after-collect1','collect2-before','collect2','final-ready']]:save(R/'scratch'/name,name+'.gz')
save(R/'scratch/monkey-c-native-topology-analysis/findings.json','source-findings.json','1dc4f52eaaac66c3f8f3aa7ec8eee31abdbb16190e125b519b955566495901dc')
save(R/'scratch/monkey-c-removal-analysis/findings.json','suicide-source-findings.json','1ebaec862efd5b087deab437c259d8e3ecd6e684c4c731084ba6673c6007689c')
save(R/'scratch/runtime-profile-406-mournglass/model-test-profiles.json','model-test-profiles.json.gz',raw['arrival']['catalogSha256']);save(Path(__file__),'archive-script.py')
pins=[]
for i in range(120):p=B/rawpath.stem/f'{i:04d}.png';assert p.is_file();pins.append(ev(p))
(O/'source-image-pins.json').write_text(json.dumps(pins,indent=2)+'\n')
for v in review['frames']:save(R/v['path'],f'arrival-{v["index"]:04d}.png',v['sha256'])
video=O/'arrival.mp4';subprocess.run(['ffmpeg','-y','-v','error','-framerate','12','-i',str(B/rawpath.stem/'%04d.png'),'-frames:v','120','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(video)],check=True)
meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=width,height,r_frame_rate,nb_read_frames','-of','json',str(video)]))['streams'][0];assert int(meta['nb_read_frames'])==120 and meta['width']==raw['width']==960 and meta['height']==raw['height']==624 and meta['r_frame_rate']=='12/1'
final=read(R/'scratch/monkeyc-arrival-final-ready.json')
limits=['Exact monkeyC121301 diagnostic probe only, not original Tamarind acceptance.','Only four actual PNGs reviewed; hallway blur, distance and native effects limit anatomy and face inspection.','Body inactive108..119; hidden DeathIndirect117..119 is analytical motion only, not visible death.','Initial enDiseaseHit AttackInfo is stale prior attack state; it is not a new monkeyC event. Native enSuicideCurse544 and synchronous secondary damage are separately captured.','120 samples span64.23468017578125wall seconds and6.283203125game seconds. PNG capture perturbs timing; no unperturbed timing or120fixed-step claim.','MP4 is a12fps lossy review derivative, not a wall-clock or game-clock timing reconstruction.','Two native guarded Collect calls reach Ready0/3 Trap1; trap was not replaced. No full campaign or final resource-disposal claim.']
(O/'README.md').write_text('# MonkeyC native arrival observation\n\nThe prearmed observer retained120complete samples from native arrival through the suicide attack. Actual enSuicideCurse544 and synchronous secondary damage58to0 establish this trial\'s removal route. Initial enDiseaseHit AttackInfo is stale prior state, not a new event. The body becomes inactive at108; hidden DeathIndirect117to119 is not visible death evidence.\n\nSamples span64.23468wall seconds and6.283203game seconds; capture changes timing. The12fps MP4 is a review derivative only. Root viewed four selected PNGs, with hallway blur, distance and native effects limiting anatomy review. This is a diagnostic probe, not Tamarind acceptance. Two guarded native Collect actions reached Ready0/3 with the next Trap1 slot unchanged.\n\n[Validation](validation.json) preserves raw capture, trial journal, native events, timing, deployment/source metadata and selected images. The [earlier setup-removal failure](../monkeyc-setup-removal-v1/validation.json) remains separate and unchanged.\n')
result={'status':'NATIVE_ARRIVAL_SUICIDE_EVENT_CAPTURED_DIAGNOSTIC_ONLY','nativeEnemy':'monkeyC','rendererId':121301,'controllerId':5979,'rigProfile':'20e30635cd3929e5660ddfa9a8cc426041fdfd16e3b74546a2f27aa660b1b43e','combatProfile':raw['arrival']['profile']['combatProfile'],'session':raw['session'],'raw':ev(O/'arrival.json.gz'),'rawCaptureOk':True,'frames':120,'arrival':raw['arrival'],'video':ev(video),'videoMetadata':meta,'timing':{'wallSeconds':f[-1]['realtimeSeconds']-f[0]['realtimeSeconds'],'gameSeconds':f[-1]['gameSeconds']-f[0]['gameSeconds'],'rawTimingMode':raw['timingMode'],'rawFixedStep':raw['fixedStep']},'rootReview':review,'finalReady':final,'collectCount':2,'earlierFailure':ev(R/'docs/evidence/monkeyc-setup-removal-v1/validation.json'),'limits':limits,'losslessMappings':maps,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]}
v=O/'validation.json';v.write_text(json.dumps(result,indent=2)+'\n');idx=R/'docs/model-runtime-validation.json';j=read(idx);assert not any(x.get('native_chassis')=='monkeyC' for x in j['calibration_probes']);j['calibration_probes'].append({'name':'MonkeyC42-bone native arrival diagnostic','native_chassis':'monkeyC','renderer_paths':['enMonkeyBasey'],'rig_profile':result['rigProfile'],'combat_profile':result['combatProfile'],'status':result['status'],'evidence':ev(v),'limits':limits});idx.write_text(json.dumps(j,indent=2)+'\n');print(json.dumps({'validation':ev(v),'files':len(result['evidence']),'maps':len(maps)}))
