#!/usr/bin/env python3
"""Offline paid-focus evidence archive. No game/deployment calls."""
from pathlib import Path
import json,gzip,hashlib,shutil,sys
R=next(p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir());O=R/'docs/evidence/native-paid-focus-v1';assert not O.exists(),'Preserve existing archive'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
audit=R/'scratch/bronzehollow-native-paid-focus-architect-review.json';a=load(audit);sources={audit};refs={}
for path,h in a['pins'].items():
 p=Path(path);p=p if p.is_absolute() else R/p;assert sha(p)==h,p
 if p.suffix in ('.json','.jsonl','.txt'):sources.add(p)
 else:refs[str(p)]=h
cli=R/'tools/ai-model-pipeline/runtime-test/paid_focus_case.py';assert sha(cli)=='94623cc68b58e257c9ef9b6be8a83713f2cba8520d3ddfab6be07dc4209400dc';sys.path.insert(0,str(cli.parent));from paid_focus_case import PaidFocusCase
manual=load(R/'scratch/bronzehollow-native-paid-focus-first-submission.json');completed=load(R/'scratch/bronzehollow-native-paid-focus-first-completion.json');checker=PaidFocusCase.__new__(PaidFocusCase)
assert checker.payment(completed['focus']['operation'],manual['before'],manual['id'],completed['focus'])
manual_initial=R/'scratch/mirewarden-game/model-test-output'/(manual['before']['id']+'.json')
if manual_initial.is_file():sources.add(manual_initial)
case=R/'scratch/mirewarden-game/model-test-output/paid-focus-case-ff19b69da69b4c629cae430d4bdf631e';result=load(case/'paid-focus-case-result.json');assert result['terminal'] and result['status']=='native_payment_and_hero_ready_observed' and result['attackSubmitted'] is False
assert checker.payment(result['lastState']['operation'],result['before'],result['submission']['id'],result['lastState'])
sources.update(p for p in case.iterdir() if p.is_file() and p.suffix in ('.json','.jsonl'))
for name in ['bronzehollow-411-first-startup.json','bronzehollow-411-deployment-result.json','bronzehollow-root-first-paid-attack-review.json','bronzehollow-after-first-paid-attack.json']:
 sources.add(R/'scratch'/name)
dep=load(R/'scratch/bronzehollow-411-deployment-result.json');sources.add(Path(dep['deployment']));catalog=R/'scratch/runtime-profile-411-bronzehollow/model-test-profiles.json';assert sha(catalog)==dep['new']['model-test-profiles.json'];sources.add(catalog);sources.add(catalog.parent/'receipt.json')
attack=R/'scratch/mirewarden-game/model-test-output/case-0368a18a625d4335a860bca71037e4a1';sources.update(p for p in attack.iterdir() if p.is_file() and p.suffix in ('.json','.jsonl'))
ar=load(attack/'result.json');raw=Path(ar['capture']['result']);sources.add(raw);assert ar['actionResult']['result']['committed']=='Attack'
review=load(R/'scratch/bronzehollow-root-first-paid-attack-review.json');image=R/review['frame'];assert sha(image)==review['sha256'];assert review['ordinaryHit']=='NOT_OBSERVED'
helper=R/'scratch/candidate-paid-focus-helper-d1c9dc83/receipt.json';hr=load(helper);owned=[]
for pin in hr['sourcePins']:
 p=Path(pin['archivePath']);assert sha(p)==pin['sha256']
 if '/scratch/native-paid-focus-candidate/' in pin['path'] and Path(pin['path']).suffix in ('.cs','.csproj','.md','.json'):owned.append((p,Path(pin['path']).relative_to(R/'scratch/native-paid-focus-candidate')))
 else:refs[pin['path']]=pin['sha256']
refs[hr['binary']['path']]=hr['binary']['sha256']
for p in [helper.with_name('review-approval.json'),R/'scratch/paid-focus-runner-source-94623cc6/receipt.json']:sources.add(p)
O.mkdir(parents=True);m=[]
for p in sorted(sources):
 rawbytes=p.read_bytes();d=O/'metadata'/Path(str(p.relative_to(R))+'.gz');d.parent.mkdir(parents=True,exist_ok=True);d.write_bytes(gzip.compress(rawbytes,mtime=0));assert gzip.decompress(d.read_bytes())==rawbytes
 m.append({'source':str(p.relative_to(R)),'sourceSha256':sha(p),'archive':str(d.relative_to(O)),'archiveSha256':sha(d),'encoding':'gzip-lossless'})
for p,rel in owned:
 d=O/'owned-helper-source'/rel;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d);assert sha(p)==sha(d)
code=[]
for parent,label in [(R/'scratch/paid-focus-runner-candidate','reviewed-candidate'),(cli.parent,'promoted')]:
 for name in ['paid_focus_case.py','test_paid_focus_case.py','PAID-FOCUS-CASE.md']:
  p=parent/name;d=O/'cli'/label/name;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d);code.append({'source':str(p.relative_to(R)),'archive':str(d.relative_to(O)),'sha256':sha(d)})
d=O/'selected-blocked-attack.png';shutil.copy2(image,d);assert sha(d)==review['sha256'];shutil.copy2(__file__,O/'archive.py')
(O/'README.md').write_text('''# Native paid focus

Two independently reviewed native paid-focus completions in session0a532864d8004cdc85e1dba0b7e076d2: manual helper turn6 debited available focus4→3 and spent0→1; standalone CLI turn9 debited3→2 and spent0→1. Each has exactly one matched native payment callback and later fresh hero readiness with focusingfalse/animationCount0.

The live CLI sequence was exactly paid-focus-state → paid-focus-submit → paid-focus-state. Its immutable turn claim, three request/result pairs, journal and full callback baseline/debit evidence are preserved. The promoted CLI is byte-identical to the reviewed/live-tested source SHA94623cc68b58e257c9ef9b6be8a83713f2cba8520d3ddfab6be07dc4209400dc. Tests are unchanged; promoted docs update the invocation path and distinguish the two live routes. Frozen candidates remain unchanged.

These are one manual and one CLI case, not broad weapon/class coverage or live interruption/duplicate/uncertain-path acceptance. The separate normal attack after the first payment was BLOCKED at58HP; its raw recording/journal and one root-selected image are retained as a counterexample, not damage acceptance. Paid focus does not guarantee damage or critical success. This archive does not add enemy/model/art coverage.

Lossless gzip mappings preserve raw evidence, startup/deployment/catalog identity and independent review. Owned helper source is retained separately; native source/DLL payloads are excluded, with hash references only. archive.py performs offline verification and refuses an existing destination. No game commands or deployment occur during archive reproduction.
''')
write(O/'validation.json',{'status':a['status'],'session':a['session'],'observations':a['observations'],'cliCommands':a['cliCommands'],'limits':a['limits'],'damageAcceptance':False,'enemyCoverageAdded':False,'losslessMappings':m,'cliSource':code,'ownedHelperSourceFiles':len(owned),'excludedPayloadHashReferences':refs,'selectedBlockedAttack':{'source':str(image.relative_to(R)),'archive':d.name,'sha256':sha(d),'review':'metadata/scratch/bronzehollow-root-first-paid-attack-review.json.gz'},'artifactSha256':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()}})
print(json.dumps({'archive':str(O),'validationSha256':sha(O/'validation.json'),'losslessMappings':len(m),'ownedHelperSourceFiles':len(owned)}))
