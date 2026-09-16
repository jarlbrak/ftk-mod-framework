#!/usr/bin/env python3
"""Explicit fixed-sampler/endpoint composition audit; no live adapter acceptance."""
import argparse,copy,json
from pathlib import Path
from bridge_kraken_native_samples import read_pin
from verify_kraken_controller_repeat import Checks,without_ids,ASSETS,ASSEMBLY
from verify_kraken_endpoint_fixture import POLICY,validate_endpoint,compare as compare_endpoint,source,digest
from verify_kraken_modern_mixer import FIXED_MODE,validate_mixer
from verify_kraken_adapter_fixture import require

COMPOSED='fixed-main-appearance-local-v1'
PROVENANCE='owned_fixed_sampler_endpoint_composition_diagnostic'
INTERNAL='internal_fixed_four_sampler_same_native_frame_not_separately_submitted_command'
LIMITS='Composed owned no-CEL output only. Fixed sampler main-only retargeting plus independently full-strength appearance endpoints blended once. No live hook, attacks, portrait synchronization, avatar cloning, gameplay events, production ownership or art acceptance. Internal validation envelopes are derived views of the exact pinned composed request, not separately submitted commands.'

def validate_composed(report,request,native,checks,label):
 require(set(request)=={'id','session','op','scenario','endpointPolicy'} and request['endpointPolicy']==COMPOSED,'Exact composed request required')
 require(report['endpointPolicy']==COMPOSED and report['provenance']==PROVENANCE and report.get('compositionInputProvenance')==INTERNAL,'Explicit internal fixed input provenance required')
 # Retain real inputs unchanged. Derived envelopes only reuse the independent common validators.
 mixer_request={k:v for k,v in request.items() if k!='endpointPolicy'};mixer_request['modernInputMixer']=FIXED_MODE
 mixer_view=dict(report,endpointPolicy=None,originalSkinProbe=None,provenance='owned_native_controller_graph_observation_no_adapter')
 mix=validate_mixer(mixer_view,mixer_request,native,checks,label+':internal-fixed')
 endpoint_request=dict(request,endpointPolicy=POLICY);endpoint_view=dict(report,endpointPolicy=POLICY,provenance='owned_controller_endpoint_policy_diagnostic')
 main_inputs=[];identity=report['modernInputMixer']['identity'];fixed_ids={identity['rootInstanceId'],identity['animatorInstanceId'],*[r['instanceId'] for r in identity['rest']['locals'].values()]}
 for frame in report['frames']:
  fixed=frame['modernInputMixer'];policy=frame['endpointPolicy'];samples=policy['samples'];inputs=fixed['inputs'];require(len(samples)==len(inputs),'Composed role count differs from frozen input')
  for sample,item in zip(samples,inputs):
   for key in ('role','clip','clipInstanceId','state','rawWeight','sampleSeconds','mappedPhase'):require(sample[key]==item[key],'Composed input clock/identity changed: '+key)
  appearance=next((s for s in samples if s['clip']=='kraken_appear'),None)
  expected='frozen_fixed_four_first_pose' if appearance is None else 'appearance_only_no_main' if len(samples)==1 else 'independent_full_strength_main_endpoint'
  require(policy.get('mainInputProvenance')==expected,'Main input provenance/appearance-role boundary changed')
  other_ids={policy['outputRootInstanceId'],*[r['instanceId'] for r in policy['output']['locals'].values()]}
  for sample in samples:
   for side in ('modern','old'):other_ids.update({sample[side+'RootInstanceId'],sample[side+'Sampler']['animatorInstanceId'],*[r['instanceId'] for r in sample[side]['locals'].values()]})
  require(fixed_ids.isdisjoint(other_ids),'Fixed input aliases endpoint/output ownership')
  main_inputs.append(fixed['firstPose'])
 endpoint=validate_endpoint(endpoint_view,endpoint_request,native,checks,main_inputs=main_inputs)
 return {'fixedInput':mix,'endpoint':endpoint}

def native_contract(report):
 i=report['identity'];return {k:i[k] for k in ('resourcesSha256','controllerName','sourceControllerId','trigger','targetState','idleStateHash','targetStateHash')}|{'gameAssemblySha256':i['gameAssembly']['assemblyFileSha256']}

def compare_historical(current,historical,checks,label):
 require(current['scenario']==historical['scenario'] and native_contract(current)==native_contract(historical),'Historical source/controller scenario differs')
 for side in ('modern','old'):
  require(without_ids(current[side+'Rest'])==without_ids(historical[side+'Rest']),'Historical native rest differs')
 require(without_ids(current['endpointRests']['output'])==without_ids(historical['endpointRests']['output']),'Historical old output rest differs')
 require(len(current['frames'])==len(historical['frames'])==241,'Historical frame coverage differs')
 for index,(a,b) in enumerate(zip(current['frames'],historical['frames'])):
  for key in ('step','graphElapsedSeconds','manualDeltaSeconds','triggerIssued'):require(a[key]==b[key],'Historical frame/clock shifted')
  for side in ('modern','old'):
   for key in ('current','next','inTransition','transition','currentClips','nextClips'):require(without_ids(a[side][key])==without_ids(b[side][key]),'Historical native state/clock/weight differs')
   checks.structured(label+':historical-native:'+str(index)+side,without_ids(a[side]['pose']),without_ids(b[side]['pose']))
  require(a['endpointPolicy']['appearanceWeight']==b['endpointPolicy']['appearanceWeight'],'Historical alpha differs')
  checks.structured(label+':historical-output:'+str(index),without_ids(a['endpointPolicy']['output']),without_ids(b['endpointPolicy']['output']))

def compare(first,request,repeat,repeat_request,native,historical,historical_request,historical_repeat,historical_repeat_request):
 historical_result=compare_endpoint(historical,historical_request,historical_repeat,historical_repeat_request,native)
 require(historical_result['status']=='endpoint_policy_mechanics_match','Historical proven endpoint pair no longer validates')
 checks=Checks();a=validate_composed(first,request,native,checks,'first');b=validate_composed(repeat,repeat_request,native,checks,'repeat')
 require(first['session']==repeat['session'] and first['id']!=repeat['id'] and first['scenario']==repeat['scenario'] and first['pinnedReady']==repeat['pinnedReady'] and first['identity']==repeat['identity'],'Composed repeat identity changed')
 for index,(x,y) in enumerate(zip(first['frames'],repeat['frames'])):
  for side in ('modern','old'):
   for key in ('current','next','inTransition','transition','currentClips','nextClips','pose'):checks.structured('repeat-native:'+str(index)+side+key,without_ids(x[side][key]),without_ids(y[side][key]))
  for key in ('firstPose','repeatedPose'):checks.structured('repeat-fixed:'+str(index)+key,without_ids(x['modernInputMixer'][key]),without_ids(y['modernInputMixer'][key]))
  for key in ('appearanceWeight','branch','mainInputProvenance','mainEndpointModels','appearanceEndpointModels','mainEndpointLocals','appearanceEndpointLocals','output'):checks.structured('repeat-composed:'+str(index)+key,without_ids(x['endpointPolicy'][key]),without_ids(y['endpointPolicy'][key]))
 compare_historical(first,historical,checks,'first');compare_historical(repeat,historical_repeat,checks,'repeat')
 return {'status':'composed_endpoint_match' if not checks.failures else 'composed_endpoint_mismatch','maximumError':checks.maximum,'tolerance':1e-5,'numericChecks':checks.count,'failures':checks.failures,'firstCoverage':a,'repeatCoverage':b,'historicalEndpointMaximumError':historical_result['maximumError'],'validationViews':'Derived internal fixed and endpoint envelopes; original composed request/evidence remain pinned.','limits':LIMITS}

def run(path):
 manifest=json.loads(Path(path).read_text());require(set(manifest)=={'schema','evidence'} and manifest['schema']=='kraken-composed-endpoint-v1','Exact composed evidence manifest')
 require(set(manifest['evidence'])=={'first','firstRequest','repeat','repeatRequest','assets','gameAssembly','historicalEndpointManifest'},'Exact composed evidence pins')
 paths={k:read_pin(v) for k,v in manifest['evidence'].items()};require(digest(paths['assets'])==ASSETS and digest(paths['gameAssembly'])==ASSEMBLY,'Pinned native sources differ')
 history=json.loads(paths['historicalEndpointManifest'].read_text());require(set(history)=={'schema','evidence'} and history['schema']=='kraken-endpoint-policy-v1' and set(history['evidence'])=={'first','firstRequest','repeat','repeatRequest','assets','gameAssembly'},'Exact historical endpoint manifest')
 hp={k:read_pin(v) for k,v in history['evidence'].items()};require(digest(hp['assets'])==ASSETS and digest(hp['gameAssembly'])==ASSEMBLY,'Historical source pins differ')
 load=lambda p:json.loads(p.read_text())
 r=compare(load(paths['first']),load(paths['firstRequest']),load(paths['repeat']),load(paths['repeatRequest']),source(paths['assets']),load(hp['first']),load(hp['firstRequest']),load(hp['repeat']),load(hp['repeatRequest']))
 r.update(manifestSha256=digest(path),evidence=manifest['evidence']);return r
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();require('scratch' in a.output.resolve().parts,'Scratch output required');r=run(a.manifest)
 with a.output.open('x') as f:json.dump(r,f,indent=2,allow_nan=False)
 print(r['status'],r['maximumError']);raise SystemExit(r['status']!='composed_endpoint_match')
