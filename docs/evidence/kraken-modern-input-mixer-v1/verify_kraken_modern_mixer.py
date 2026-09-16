#!/usr/bin/env python3
"""Independent controller-free mixer comparison; not a production adapter verdict."""
import argparse,json,math,re
from pathlib import Path
import numpy as np
from bridge_kraken_native_samples import read_pin,MODERN
from verify_kraken_controller_repeat import Checks,validate_run,digest,ASSETS,ASSEMBLY,without_ids
from verify_kraken_endpoint_fixture import source,pose
from verify_kraken_adapter_fixture import require

MODE='two-clip-raw-v1'
TOL=1e-5
LIMITS=('Owned controller-free modern input experiment only. Full immutable-rest reset is an experimental input convention. '
        'Appearance-contributing frames are excluded from native mixed equivalence; the separate proven endpoint policy is unchanged. '
        'No live adapter, synchronization, attack events, gameplay, original skin or visual acceptance. Full unreported source-tree '
        'invariance is runtime-reported hash evidence; exposed local/model matrices and repeats are independently checked.')


def scalar(value):
    require(type(value) in (int,float) and math.isfinite(value),'Finite numeric scalar required');return float(value)


def identity(value):
    require(type(value) is int and value!=0,'Exact nonzero Unity identity required');return value


def hash_(value):
    require(isinstance(value,str) and re.fullmatch('[a-f0-9]{64}',value),'Exact SHA256 required');return value


def error(a,b):return float(np.max(np.abs(np.asarray(a,float)-np.asarray(b,float))))


def validate_mixer(report,request,native,checks,label):
    require(set(request)=={'id','session','op','scenario','modernInputMixer'} and request['modernInputMixer']==MODE,'Exact separate mixer request required')
    require(all(isinstance(request[k],str) and re.fullmatch('[a-f0-9]{32}',request[k]) for k in ('id','session')),'Exact request ID/nonce required')
    require(report.get('endpointPolicy') is None and report.get('originalSkinProbe') is None,'Mixer cannot substitute endpoint/skin evidence')
    validate_run(report,{k:v for k,v in request.items() if k!='modernInputMixer'},checks,label+':native')
    summary=report.get('modernInputMixer') or {};meta=summary.get('identity') or {};old_rest,modern_rest,states=native
    require(summary.get('mode')==meta.get('mode')==MODE and report['cleanup'].get('modernInputMixerClean') is True,'Mixer mode/cleanup missing')
    require(all(summary.get(k) is True for k in ('graphDisposed','targetUnityNull','sourceUnchanged')),'Mixer/source cleanup failed')
    require(meta.get('initializedAfterRebindAndEvaluate') is True and meta.get('normalizeWeights') is False and meta.get('avatarIsHuman') is False,'Wrong initialization/mixer convention')
    require(type(meta.get('transformCount')) is int and 7<=meta['transformCount']<=256,'Bounded sampler hierarchy required')
    require(isinstance(meta.get('inputConvention'),str) and 'experimental' in meta['inputConvention'],'Reset convention must remain experimental')
    for k in ('immutableRestLocalTrsSha256','sourcePrefabLocalTrsSha256'):hash_(meta.get(k))
    root_id=identity(meta.get('rootInstanceId'));animator_id=identity(meta.get('animatorInstanceId'));require(root_id!=animator_id,'Sampler root/Animator aliases')
    avatar_id=identity(meta.get('avatarInstanceId'));paths=MODERN|{'Root_M'}
    local,_=pose(meta['rest'],modern_rest,checks,label+':sampler-rest')
    for path in paths:checks.numeric(label+':immutable-rest:'+path,local[path],modern_rest[path])
    bone_ids={path:identity(row.get('instanceId')) for path,row in meta['rest']['locals'].items()}
    require(len(set(bone_ids.values()))==len(bone_ids),'Sampler bone identity duplicates')
    owned={root_id,animator_id,*bone_ids.values()};require(len(owned)==len(bone_ids)+2,'Sampler component identities overlap')
    for side in ('modern','old'):
        local,_=pose(report[side+'Rest'],native[0 if side=='old' else 1],checks,label+':native-rest:'+side)
        for path in local:checks.numeric(label+':source-rest:'+side+path,local[path],native[0 if side=='old' else 1][path])
        view=report['frames'][0][side]
        require(owned.isdisjoint({view['rootInstanceId'],view['animatorInstanceId'],*[v['instanceId'] for v in report[side+'Rest']['locals'].values()]}),'Sampler aliases native inputs')
    require(avatar_id==report['frames'][0]['modern']['avatarInstanceId'],'Sampler Avatar differs from native modern Avatar')
    eligible=[];excluded=[];main_transitions=[];pure_by_clip={};first_max=repeat_max=0.;bad_main=bad_repeat=0;clip_ids={}
    for index,frame in enumerate(report['frames']):
        view=frame['modern'];sample=frame.get('modernInputMixer') or {}
        require(sample.get('mode')==MODE,'Missing mixer frame')
        roles=['current','next'] if view['inTransition'] else ['current'];inputs=sample.get('inputs')
        require(isinstance(inputs,list) and len(inputs)==len(roles),'Active state input count mismatch')
        weights=[];times=[];appearance=False;zero_appearance=False
        for role,item in zip(roles,inputs):
            st=view[role];clips=view[role+'Clips'];require(len(clips)==1,'Exact one active clip required');clip=clips[0]
            require(item.get('role')==role and item.get('state')==st,'Sample did not use exact captured state/clock')
            expected=states[st['fullPathHash']]
            require(item.get('clip')==clip['name']==expected['clip'] and item.get('clipInstanceId')==clip['instanceId'],'Source clip identity/map mismatch')
            require(clip_ids.setdefault(clip['name'],identity(clip['instanceId']))==clip['instanceId'],'Source clip identity changed within run')
            require(item.get('rawWeight')==clip['weight'] and item.get('clipLength')==clip['length'],'Raw weights/clip duration changed')
            phase=scalar(st['normalizedTime']);length=scalar(item['clipLength']);weight=scalar(item['rawWeight'])
            require(0<=weight<=1 and length>0 and st['speed']==st['speedMultiplier']==1 and item.get('loop') is st['loop'] and st['loop']==expected['loop'],'Unsupported native clock convention')
            checks.numeric(label+':source-duration',length,expected['length'])
            mapped=phase-math.floor(phase) if st['loop'] else min(1.,max(0.,phase));seconds=float(np.float32(mapped*length))
            require(item.get('mappedPhase')==mapped and item.get('sampleSeconds')==seconds and item.get('requestedUnwrappedSeconds')==phase*length,'Mapped/playable clock changed')
            weights.append(weight);times.append(seconds)
            if clip['name']=='kraken_appear':appearance|=weight>0;zero_appearance|=weight==0
        require(abs(sum(weights)-1)<=TOL,'Raw weights do not sum to one')
        require(sample.get('inputWeights')==weights+([0.] if len(weights)==1 else []) and sample.get('playableTimes')==times+([0.] if len(times)==1 else []),'Mixer weights/time differ from observed inputs')
        require(sample.get('inactiveNextClipMetadataIgnored') is (len(roles)==1),'Stale inactive next clips contributed')
        require(sample.get('eligibleMainComparison') is (not appearance) and sample.get('appearanceZeroWeightBoundary') is zero_appearance,'Appearance exclusion changed')
        require(sample.get('scope')==('appearance_contribution_excluded_no_native_mixed_equivalence' if appearance else 'main_whole_controller_comparison_including_history'),'Comparison scope mislabeled')
        for k,want in [('samplerRootInstanceId',root_id),('samplerAnimatorInstanceId',animator_id),('avatarInstanceId',avatar_id),('resetCount',2*(index+1))]:require(sample.get(k)==want,'Sampler identity/reset count changed: '+k)
        require(all(sample.get(k) is False for k in ('runtimeControllerAssigned','fireEvents','applyRootMotion','footIK','avatarIsHuman','playableIKControlAvailable')) and sample.get('playableIK') is None,'Unsupported sampler/controller/event/IK configuration')
        require(sample.get('graphMode')=='Manual' and sample.get('playableCount')==3,'Wrong sampler graph shape')
        for before,after in [('nativeModernBeforeSha256','nativeModernAfterSha256'),('nativeOldBeforeSha256','nativeOldAfterSha256')]:require(hash_(sample.get(before))==hash_(sample.get(after)),'Native input tree changed')
        require(hash_(sample.get('sourcePrefabAfterSha256'))==meta['sourcePrefabLocalTrsSha256'],'Source prefab changed')
        matrices=[]
        for which in ('firstPose','repeatedPose'):
            result=sample[which];require({p:v['instanceId'] for p,v in result['locals'].items()}==bone_ids,'Sampler bone identity changed')
            matrices.append(pose(result,modern_rest,checks,label+':'+str(index)+which))
        repeat_error=max(error(matrices[0][kind][path],matrices[1][kind][path]) for kind in (0,1) for path in paths)
        repeat_max=max(repeat_max,repeat_error);bad_repeat+=repeat_error>TOL
        checks.numeric(label+':reported-repeat-error',repeat_error,sample['repeatMaximumError']);require(sample.get('repeatPassed') is (repeat_error<=TOL),'Incorrect repeat verdict')
        for kind in (0,1):
            for path in paths:checks.numeric(label+':repeat:'+str(index)+':'+path+str(kind),matrices[0][kind][path],matrices[1][kind][path])
        if appearance:
            excluded.append(index);require(sample.get('mainMaximumError') is None and sample.get('mainComparisonPassed') is None,'Excluded appearance assigned equivalence verdict')
        else:
            eligible.append(index);target=pose(view['pose'],modern_rest,checks,label+':target:'+str(index))
            main_error=max(error(matrices[0][kind][path],target[kind][path]) for kind in (0,1) for path in paths)
            first_max=max(first_max,main_error);bad_main+=main_error>TOL
            checks.numeric(label+':reported-main-error',main_error,sample['mainMaximumError']);require(sample.get('mainComparisonPassed') is (main_error<=TOL),'Incorrect main comparison verdict')
            for kind in (0,1):
                for path in paths:checks.numeric(label+':native-match:'+str(index)+':'+path+str(kind),matrices[0][kind][path],target[kind][path])
            if view['inTransition'] and all(w>0 for w in weights):main_transitions.append(index)
        if not view['inTransition']:pure_by_clip.setdefault(inputs[0]['clip'],[]).append(index)
    require(summary.get('eligibleFrames')==len(eligible) and summary.get('excludedAppearanceFrames')==len(excluded) and summary.get('resets')==482,'Frame/reset accounting changed')
    require(summary.get('failedMainFrames')==bad_main and summary.get('failedRepeatFrames')==bad_repeat and summary.get('reportedNumericalComparisonPassed') is (bad_main==bad_repeat==0 and bool(eligible)),'Summary numerical verdict changed')
    checks.numeric(label+':summary-main-max',first_max,summary['maximumMainError']);checks.numeric(label+':summary-repeat-max',repeat_max,summary['maximumRepeatError'])
    require(pure_by_clip.get('krakenIdle'),'Pure idle coverage missing')
    if report['scenario']=='appear':
        require(excluded and pure_by_clip.get('kraken_appear') and min(pure_by_clip['krakenIdle'])<min(excluded) and max(pure_by_clip['krakenIdle'])>max(excluded),'Appearance must include excluded contribution and pre/post pure main coverage')
    else:
        target='krakenDamage' if report['scenario'].startswith('damaged') else 'krakenDisappear'
        require(not excluded and pure_by_clip.get(target) and main_transitions,'Pure target/main transition coverage missing')
    return {'eligibleSteps':eligible,'excludedAppearanceSteps':excluded,'mainTransitionSteps':main_transitions,'pureFramesByClip':pure_by_clip,'failedMainFrames':bad_main,'failedRepeatFrames':bad_repeat}


def compare(first,request,repeat,repeat_request,native):
    checks=Checks();a=validate_mixer(first,request,native,checks,'first');b=validate_mixer(repeat,repeat_request,native,checks,'repeat')
    require(first['session']==repeat['session'] and first['id']!=repeat['id'] and first['scenario']==repeat['scenario'] and first['pinnedReady']==repeat['pinnedReady'] and first['identity']==repeat['identity'],'Distinct same-session source-pinned repeat required')
    for x,y in zip(first['frames'],repeat['frames']):
        for field in ('current','next','inTransition','transition','currentClips','nextClips'):checks.structured('repeat-native-clock:'+field,x['modern'][field],y['modern'][field])
        for field in ('firstPose','repeatedPose'):checks.structured('repeat-mixer-pose',without_ids(x['modernInputMixer'][field]),without_ids(y['modernInputMixer'][field]))
    return {'status':'modern_input_mixer_match' if not checks.failures else 'modern_input_mixer_mismatch','tolerance':TOL,'maximumError':checks.maximum,'numericChecks':checks.count,'failures':checks.failures,'firstCoverage':a,'repeatCoverage':b,'limitations':LIMITS}


def run(path):
    manifest=json.loads(Path(path).read_text());require(set(manifest)=={'schema','evidence'} and manifest['schema']=='kraken-modern-input-mixer-v1','Exact mixer evidence schema required')
    evidence=manifest['evidence'];require(set(evidence)=={'first','firstRequest','repeat','repeatRequest','assets','gameAssembly'},'Six pinned evidence files required')
    paths={key:read_pin(value) for key,value in evidence.items()};require(digest(paths['assets'])==ASSETS and digest(paths['gameAssembly'])==ASSEMBLY,'Actual source hashes unsupported')
    docs={k:json.loads(p.read_text()) for k,p in paths.items() if k not in ('assets','gameAssembly')}
    result=compare(docs['first'],docs['firstRequest'],docs['repeat'],docs['repeatRequest'],source(paths['assets']));result.update(manifestSha256=digest(path),evidence=evidence);return result

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--manifest',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    require('scratch' in args.output.resolve().parts,'Scratch-only output required');result=run(args.manifest)
    with args.output.open('x') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    print(result['status'],result['maximumError']);raise SystemExit(0 if result['status']=='modern_input_mixer_match' else 1)
