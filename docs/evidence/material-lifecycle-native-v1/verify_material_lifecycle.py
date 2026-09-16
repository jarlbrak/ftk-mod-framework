#!/usr/bin/env python3
"""Independent raw lifecycle audit. No Unity calls; runtime assertion labels are not an oracle."""
import argparse,hashlib,json,math,re,struct
from pathlib import Path

CORE='19ece392cf3d58fe43b6178733a9a5ecd85c51943f9cd1769060b88fb572b735'
HELPER='b9fc9374607ba550937aa872d6538e960c49cc929a409dc7b1b1e72e8e5f0272'
PHASES=['source-enabled','clone-disabled','clone-enabled','grandclone-disabled','grandclone-enabled','never-enabled-renderer','prefix-rejection-only','destroy-never-enabled','first-destruction','survivor-after-second-destruction','last-owner-destruction']
TOL=1e-5
LIMITS='Owned original no-CEL hierarchy only; explicit owner setup is not public GLB binding. Raw Unity-null/lease absence are source-reviewed runtime measurements joined to independently reconstructed resource identities, not a second engine observation. Prefix rejection has only formatted runtime assertions and is not independently numerically verified. No never-active clone, failed Unity commit rollback, native avatar lifetime, gameplay, animation or art acceptance.'
def need(ok,message):
 if not ok:raise ValueError(message)
def ident(v):need(type(v) is int and v!=0,'Exact nonzero object ID');return v
def finite(v):need(type(v) in (int,float) and math.isfinite(v),'Finite numeric field');return float(v)
def vec(v,n):need(isinstance(v,list) and len(v)==n,'Vector dimensions');return [finite(x) for x in v]
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def equal(actual,expected,label):need(actual==expected and ((type(actual) is bool)==(type(expected) is bool)),label)

def verify(report,request,before,after,deployment):
 equal(set(request),{'id','session','op'},'Exact request schema');equal(request['op'],'material-lifecycle-fixture','Operation')
 for k in ('id','session'):
  need(isinstance(request[k],str) and re.fullmatch('[a-f0-9]{32}',request[k]),'Exact request ID/nonce');equal(report[k],request[k],'Request/report '+k)
 need(report['ok'] is True and report['error'] is None and report['engineErrors']==report['cleanupErrors']==[],'Mechanics errors')
 need(report['sameReadyAfter'] is True and report['allOwnedResourcesDisposed'] is True,'Completion measurements missing')
 equal(report['provenance'],'owned_original_no_CEL_native_scheduled_material_lineage_fixture','Scope')
 equal(report['framework']['assemblyFileSha256'],CORE,'Core pin');equal(deployment['new']['BepInEx/plugins/FTKModFramework.dll'],CORE,'Deployment Core')
 equal(deployment['new']['BepInEx/plugins/FtkRuntimeModelTest.dll'],HELPER,'Deployment helper');equal(deployment['helperReview']['sha256'],HELPER,'Reviewed helper')
 need(deployment['helperReview']['status']=='architect_approved_owned_trial_live_gate_pending','Source review')
 root=deployment['root'];equal(report['framework']['location'],root+'/BepInEx/plugins/FTKModFramework.dll','Owned binary path')
 party=report['pinnedParty'];need(0<len(party)<=3 and len(set(party))==len(party),'Party bounds');[ident(x) for x in party]
 for value in (before,after):
  equal(value['session'],request['session'],'Ready nonce');equal(value['identity']['session'],request['session'],'Identity nonce');equal(value['identity']['root'],root,'Owned root')
  need(value['ok'] is True and value['strictReady']['ok'] is True,'Strict Ready')
  for key in ('level','room'):equal(value['strictReady'][key],report['pinnedReady'][key],'Ready indices');equal(value['dungeon'][key],report['pinnedReady'][key],'Dungeon indices')
  s=value['sessions'];need(s['esInCombat'] is False and s['mcEncounterType']=='Ready' and s['esVoteType']==s['mcVoteType']=='Ready' and s['fightOrderCount']==0 and s['voteFsm']['present'] is True and s['voteFsm']['enabled'] is True,'Native Ready fields')
  equal([s['heroInstanceId'] for s in value['voteSurfaces']],party,'Owned Ready party')
  for surface in value['voteSurfaces']:
   need(surface['alive'] is True and surface['containerActive'] is True and surface['voteType']=='Ready','Living Ready owner')
   need(any(b['option']=='Ready' and all(b[k] is True for k in ('active','enabled','interactable','usable','belongsToHero')) for b in surface['buttons']),'Native Ready button')
 equal(before['identity'],after['identity'],'Session/save identity');equal(before['dungeon'],after['dungeon'],'Native dungeon unchanged');equal(before['playerScopes'],after['playerScopes'],'Native avatars unchanged')
 equal(report['pinnedReady']['session'],request['session'],'Pinned nonce');need(before['frame']<report['cases'][0]['frames'][0]['frame']<report['frame']<after['frame'],'Observation ordering')
 need(len(report['cases'])==2,'Two lineages');cases=[];all_objects=set();leases=set();max_error=0.;phase_checks=0;previous_last=before['frame']
 for order,case in enumerate(report['cases']):
  equal(case['order'],['descendants-first','source-first-grandclone-survives'][order],'Order');lease=ident(case['leaseId']);need(lease not in leases,'Distinct leases');leases.add(lease)
  frames=case['frames'];need(len(frames)==22,'Exact22frames');need(previous_last<frames[0]['frame']<=frames[-1]['frame']<report['frame']<after['frame'],'Lineage chronological bounds');previous_last=frames[-1]['frame'];identities={};materials={};previous={};seen_resources=set();mesh=None;textures=None;component_ids=set()
  for index,frame in enumerate(frames):
   need(type(frame['frame']) is int and frame['frame']>=0,'Exact frame index');step=index//2;equal(frame['phase'],PHASES[step],'Fixed phase order');equal(frame['leaseId'],lease,'Lease identity')
   finite(frame['time']);finite(frame['realtime']);dt=finite(frame['deltaTime']);need(dt>0,'Advancing native time')
   if index:equal(frame['frame'],frames[index-1]['frame']+1,'Consecutive frames');need(frame['time']>frames[index-1]['time'] and frame['realtime']>frames[index-1]['realtime'],'Monotonic clocks')
   alive=set(range(1 if step==0 else 2 if step<=2 else 3 if step<=4 else 4))
   if step>=7:alive.discard(3)
   if step>=8:alive.discard(2 if order==0 else 0)
   if step>=9:alive.discard(1)
   if step>=10:alive.clear()
   rows=frame['owners'];equal([r['index'] for r in rows],sorted(alive),'Exact surviving owner set');equal(frame['references'],len(alive),'Measured lease references')
   expected_count=5 if step<=1 else 7 if step<=3 else 9 if step<10 else 0;equal(frame['resourceCount'],expected_count,'Measured resource count')
   enabled_materials=set();by_index={r['index']:r for r in rows}
   for row in rows:
    owner=row['index'];need(type(owner) is int,'Exact lineage index');equal(row['frame'],frame['frame'],'Owner sampling frame');equal(row['parentIndex'],[-1,0,1,2][owner],'Clone parent')
    ids=tuple(ident(row[k]) for k in ('root','owner','renderer','scroller','boneInstanceId'));need(len(set(ids))==5,'Owner component alias')
    if owner not in identities:need(component_ids.isdisjoint(ids),'Clone component alias');identities[owner]=ids;component_ids.update(ids)
    equal(ids,identities[owner],'Stable exact owner identities')
    for key,want in [('localPosition',[0,0,0]),('boneLocalPosition',[0,0,0]),('localRotation',[0,0,0,1]),('boneLocalRotation',[0,0,0,1]),('localScale',[1,1,1]),('boneLocalScale',[1,1,1])]:equal(vec(row[key],len(want)),want,'Immutable owned TRS')
    enabled=owner==0 or owner==1 and step>=2 or owner==2 and step>=4
    need(row['rendererEnabled'] is enabled and row['scrollerEnabled'] is True,'Native enabled schedule');equal(row['privateMapCount'],1 if enabled else 0,'Private owner map')
    equal(row['property'],'_MainTex','Texture property');equal(row['materialIndex'],1,'Native slot')
    mids=tuple(ident(x) for x in row['materialIds']);need(len(mids)==2 and mids[0]!=mids[1],'Two material slots');equal(row['serializedMaterialIds'],list(mids),'Serialized provenance')
    tx=tuple(ident(x) for x in row['textureIds']);need(len(tx)==2 and tx[0]!=tx[1],'Two textures');mid=ident(row['sharedMeshId'])
    if mesh is None:mesh=mid;textures=tx
    equal(mid,mesh,'Shared mesh');equal(tx,textures,'Shared textures')
    if enabled:
     if owner not in materials:need(set(mids).isdisjoint(seen_resources),'New private materials required');materials[owner]=mids
     equal(mids,materials[owner],'No repeated allocation');need(enabled_materials.isdisjoint(mids),'Enabled owner independence');enabled_materials.update(mids)
    else:equal(mids,tuple(by_index[row['parentIndex']]['materialIds']),'Disabled inherited parent materials')
    seen_resources.update([mid,*tx,*mids]);need(component_ids.isdisjoint(seen_resources),'Component/resource identity alias')
    phase=vec(row['phase'],2);rate=vec(row['rate'],2);equal(rate,[0.,f32(f32(.2)+f32(f32(.11)*owner))],'Fixed native rates')
    old=previous.get(owner);prior=[0.,0.] if old is None else old['phase']
    expected=[f32(f32(prior[k])+f32(f32(rate[k])*f32(dt))) for k in range(2)]
    err=max(abs(phase[k]-expected[k]) for k in range(2));max_error=max(max_error,err);phase_checks+=1;need(err<=TOL,'Raw native phase accumulation mismatch')
    if index%2:need(0<=finite(row['phaseMaximumError'])<=TOL,'Reported phase failure')
    offsets=[vec(x,2) for x in row['offsets']];need(len(offsets)==2,'Offset slots');equal(offsets[0],[0.,0.],'Untargeted slot0 changed')
    if enabled:need(max(abs(offsets[1][k]-phase[k]) for k in range(2))<=TOL,'Enabled target offset')
    else:equal(offsets,by_index[row['parentIndex']]['offsets'],'Inherited disabled offsets')
    previous[owner]=row
   equal(len(seen_resources),5 if step<=1 else 7 if step<=3 else 9,'Reconstructed resource union')
  need(len(identities)==4 and len(materials)==3,'Full source/clone/grandclone/never-enabled coverage')
  disposal=case['resourceDisposal'];equal(len(disposal),9,'Nine final resource measurements');need(len({r['instanceId'] for r in disposal})==9,'Unique disposal IDs')
  equal({r['instanceId'] for r in disposal},seen_resources,'Final disposal joins exact historical resources')
  for r in disposal:
   ident(r['instanceId']);need(r['unityNull'] is True,'Resource not disposed');equal(r['type'],'UnityEngine.Mesh' if r['instanceId']==mesh else 'UnityEngine.Texture2D' if r['instanceId'] in textures else 'UnityEngine.Material','Resource type')
  need(case['leaseAbsent'] is True and case['disposed'] is True and case['cleanupErrors']==[],'Final lease disposal measurement');equal(case['pinnedResourceCount'],9,'Resource pin count')
  need(all_objects.isdisjoint(component_ids|seen_resources),'Lineages share objects');all_objects.update(component_ids|seen_resources)
  cases.append({'order':case['order'],'frames':22,'owners':4,'distinctPrivateMaterialSets':3,'resources':9,'leaseId':lease,'finalMeasuredLeaseAbsent':True,'finalMeasuredResourcesUnityNull':True})
 return {'status':'owned_material_lifecycle_audit_pass','requestId':request['id'],'session':request['session'],'cases':cases,'rawPhaseChecks':phase_checks,'maximumPhaseError':max_error,'tolerance':TOL,'runtimeAssertionBooleansUsedAsOracle':False,'limits':LIMITS}

def run(manifest_path):
 path=Path(manifest_path);m=json.loads(path.read_text());equal(set(m),{'schema','evidence'},'Manifest fields');equal(m['schema'],'ftkmf.material-lifecycle-evidence.v1','Manifest schema')
 need(set(m['evidence'])=={'request','result','before-ready','after-ready','deployment','helper-review','fixture-source','timing-source'},'Exact evidence pins')
 files={}
 for key,pin in m['evidence'].items():
  equal(set(pin),{'path','sha256'},'Pin schema');p=(path.parent/pin['path']).resolve();need(sha(p)==pin['sha256'],'Changed evidence '+key);files[key]=p
 docs={key:json.loads(files[key].read_text()) for key in ('request','result','before-ready','after-ready','deployment','helper-review')}
 review=docs['helper-review'];equal(review,docs['deployment']['helperReview'],'Exact source review join')
 for relative,digest in review['sourcePins'].items():
  rel=Path(relative);need(not rel.is_absolute() and '..' not in rel.parts,'Safe reviewed-source path');equal(sha(path.parent/'reviewed-source'/rel),digest,'Archived reviewed source pin')
 for key,source in [('fixture-source','runtime-test/MaterialLifecycleFixture.cs'),('timing-source','runtime-test/MaterialLifecycleTiming.cs')]:equal(sha(files[key]),review['sourcePins'][source],'Reviewed source pin')
 result=verify(docs['result'],docs['request'],docs['before-ready'],docs['after-ready'],docs['deployment']);result['manifestSha256']=sha(path);result['evidence']=m['evidence'];return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();r=run(a.manifest)
 with a.output.open('x') as f:json.dump(r,f,indent=2,allow_nan=False)
 print(r['status'],r['rawPhaseChecks'],r['maximumPhaseError'])
