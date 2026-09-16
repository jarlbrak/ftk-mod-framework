import json,gzip,hashlib,collections
from pathlib import Path
R=Path.cwd();B=R/'scratch/mirewarden-game/model-test-output';O=R/'docs/evidence/entry-preparation-delayed-quest-v1';read=lambda p:json.loads(Path(p).read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ev(p):return {'path':str(p.relative_to(R)),'sha256':sha(p)}
prior=B/'case-1093997986f847d7bcec9699dfa71dba';follow=B/'case-bb251298063147a4a9ce90dd3b62accf';result=read(follow/'continuation-result.json');claim=read(R/'scratch/amberwake-preentry-continuation-claim.json');assert sha(prior/'journal.jsonl')==claim['priorSha256'];assert result['session']==claim['session']=='5856168eea154be3adecc2a5d1cb2330';assert result['status']=='binding_metadata_observed' and result['matches'][0]['rendererInstanceId']==-245602
old=[json.loads(x) for x in (prior/'journal.jsonl').read_text().splitlines()];new=[json.loads(x) for x in (follow/'journal.jsonl').read_text().splitlines()]
def requests(events):
 return [{'sequence':e['sequence'],'kind':e['kind'],'data':e['data']} for e in events if e['kind']=='helper-request' or (e['kind']=='http-request' and e['data'].get('path')=='/action')]
oldreq=requests(old);newreq=requests(new);counts=collections.Counter(e['data'].get('op') for e in oldreq if e['kind']=='helper-request');assert counts['entry-position']==counts['entry-discover']==counts['stage-enemy']==counts['stage-next-enemy']==0
assert not any(e['data'].get('payload',{}).get('action')=='enter_dungeon' for e in oldreq)
assert not any(e['data'].get('payload',{}).get('action')=='start_run' for e in newreq)
assert result['finalState']['combat']['enemies'][0]['hp']==675
assert not O.exists();O.mkdir(parents=True);maps=[]
def save(p,rel):
 p=Path(p);assert p.is_file() and p.suffix.lower() not in ('.dll','.assets','.npz','.glb','.blend','.cs');b=p.read_bytes();q=O/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(gzip.compress(b,mtime=0) if rel.endswith('.gz') else b);assert (gzip.decompress(q.read_bytes()) if rel.endswith('.gz') else q.read_bytes())==b;maps.append({'source':str(p),'sourceSha256':hashlib.sha256(b).hexdigest(),'archive':ev(q),'compression':'gzip-lossless' if rel.endswith('.gz') else 'none'})
for folder in [prior,follow]:
 for p in folder.iterdir():
  if p.suffix in ('.json','.jsonl','.html'):save(p,folder.name+'/'+p.name+'.gz')
 for line in (folder/'journal.jsonl').read_text().splitlines():
  e=json.loads(line)
  if e.get('kind')=='helper-result':
   p=e.get('data',{}).get('path')
   if p and Path(p).is_file() and not any(m['source']==p for m in maps):save(p,'helper-results/'+Path(p).name+'.gz')
for n in ['amberwake-first-stage-result.txt','amberwake-entry-stopped-state.json','continue-amberwake-preentry-once.py','amberwake-preentry-continuation-claim.json','amberwake-preentry-continuation-result.txt']:save(R/'scratch'/n,n+'.gz')
for n in ['findings.md','source-pins.json']:save(R/'scratch/amberwake-entry-visit-analysis'/n,'source/'+n)
save(Path(__file__),'archive-script.py')
sourcepins=read(R/'scratch/amberwake-entry-visit-analysis/source-pins.json');pinchecks={n:sha(R/n)==h for n,h in sourcepins.items()};assert all(pinchecks.values())
limits=['Original initial runner stopped before any entry-position/discover/dungeon-entry/staging. Its failure remains unchanged.','Successful continuation is a same-session explicit continuation from first unsubmitted step, not a restart or automatic Runner repair.','Known Q1-to-Q2 native transition was later observed; exact intervening coroutine/completion timing was not captured. No fixed-sleep or exact-delay repair claim.','Only startup/story/pre-entry and binding metadata are covered; no original Amberwake visual, full gameplay or cleanup acceptance.','Existing Runner still stops on the initial incomplete readiness boundary; automatic bounded Q2 readiness handling remains separate work.','Root reports same processPID80583/launch7852; journal continuity verifies same session, not independent OS process lifetime instrumentation.']
(O/'README.md').write_text('# Delayed Visit-to-Dungeon quest readiness\n\nThe initial run was quiet at Visit Q1(-1) but had not reached expected crypt Q2(-10), so the strict entry gate stopped before position/discover/dungeon-entry/staging. A later read-only snapshot observed native Q2 and its actionable story. The original stopped result remains unchanged.\n\nAn explicitly claimed continuation in the same session completed the guarded Q2 pages and ordinary pre-entry preparation, then adjacent entry/staging reached Amberwake binding metadata on renderer-245602 at HP675. No new start_run was submitted. This is evidence for safe continuation, not an automatic Runner fix or an inferred exact coroutine delay.\n\n[Validation](validation.json) preserves both journals, helper results, original stop, source findings/pins and continuation script/claim. Decompiled source and native assets are excluded. Existing Runner still stops at the initial boundary. No original-model live appearance acceptance is implied.\n')
r={'status':'EXPLICIT_SAME_SESSION_CONTINUATION_AFTER_DELAYED_Q1_Q2_OBSERVATION','session':result['session'],'priorHelperRequestCounts':dict(counts),'priorMutationRequests':oldreq,'continuationMutationRequests':newreq,'priorEntryActionsSubmitted':0,'newStartRunSubmitted':False,'continuationResultStatus':result['status'],'matches':result['matches'],'finalEnemyHp':675,'claim':claim,'sourcePinsVerified':pinchecks,'limits':limits,'losslessMappings':maps,'evidence':[ev(p) for p in sorted(O.rglob('*')) if p.is_file()]};(O/'validation.json').write_text(json.dumps(r,indent=2)+'\n');assert sha(prior/'journal.jsonl')==claim['priorSha256'];print(json.dumps({'validation':ev(O/'validation.json'),'files':len(r['evidence']),'maps':len(maps)}))
