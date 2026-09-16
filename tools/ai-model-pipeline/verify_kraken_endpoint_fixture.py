#!/usr/bin/env python3
"""Independent authored Kraken endpoint-policy audit; never native blend equivalence."""
import argparse
import json
import math
import re
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation, Slerp
from audit_kraken_adapter import extract, models, MAPPING, JAW
from bridge_kraken_native_samples import MODERN, OLD, read_pin
from verify_kraken_controller_repeat import ASSETS, ASSEMBLY, Checks, digest, trs, validate_run, without_ids
from verify_kraken_adapter_fixture import matrix, require

POLICY='main-appearance-local-v1'
TOL=1e-5
LIMITS=('Authored four-local endpoint policy and observed pure-frame baseline checks only. '
        'Mixed-clock endpoints are constructed samples, not native mixed-pose equivalence. '
        'No mesh deformation, visual continuity, jaw MODEL endpoint equivalence, live adapter or full controller acceptance. '
        'Input immutability and within-step repeated commit are runtime-reported assertions; independent cross-run poses are compared separately.')


def number(value):
    require(type(value) in (float,int) and math.isfinite(value),'Finite numeric scalar required')
    return float(value)


def close(checks,label,a,b):
    checks.numeric(label,a,b)


def decompose(value):
    m=matrix(value);require(np.max(np.abs(m[3]-[0,0,0,1]))<=TOL,'Non-affine TRS')
    scale=np.linalg.norm(m[:3,:3],axis=0)
    require(min(scale)>1e-8 and np.linalg.det(m[:3,:3])>0,'Nonpositive/singular/reflected TRS')
    rotation=m[:3,:3]/scale
    require(np.max(np.abs(rotation.T@rotation-np.eye(3)))<=TOL,'Sheared local TRS')
    return m[:3,3],Rotation.from_matrix(rotation).as_quat(),scale


def compose(p,q,s):
    out=np.eye(4);out[:3,:3]=Rotation.from_quat(q).as_matrix()@np.diag(s);out[:3,3]=p
    return out


def local_endpoints(endpoint,root):
    return [np.linalg.solve(root if i==0 else endpoint[i-1],m) for i,m in enumerate(endpoint)]


def blend_locals(a,b,alpha):
    require(0<=number(alpha)<=1,'Invalid raw alpha')
    if a is None:return [matrix(x) for x in b]
    if b is None:return [matrix(x) for x in a]
    result=[]
    for x,y in zip(a,b):
        ap,aq,az=decompose(x);bp,bq,bz=decompose(y)
        if np.dot(aq,bq)<0:bq=-bq
        q=Slerp([0,1],Rotation.from_quat([aq,bq]))([alpha]).as_quat()[0]
        result.append(compose(ap*(1-alpha)+bp*alpha,q,az*(1-alpha)+bz*alpha))
    return result


def pose(value,paths,checks,label):
    require(set(value['locals'])==set(value['prefabRootModels'])==set(paths),'Exact pose paths required')
    local={}
    for path,row in value['locals'].items():
        local[path]=trs(row);decompose(local[path])
        close(checks,label+':trs:'+path,local[path],matrix(row['matrix']))
    calculated=models(local)
    for path,m in calculated.items():close(checks,label+':model:'+path,m,matrix(value['prefabRootModels'][path]))
    return local,calculated


def sample_settings(view):
    require(all(view.get(k) is True for k in ('animatorEnabled','animatorInitialized','avatarValid','graphValid')),'Inactive endpoint sampler')
    require(all(view.get(k) is False for k in ('runtimeControllerAssigned','fireEvents','applyRootMotion','footIK')),'Unexpected endpoint callbacks/controller/IK')
    require(view.get('cullingMode')=='AlwaysAnimate' and view.get('graphUpdateMode')=='Manual' and view.get('playableCount')==view.get('outputCount')==1 and view.get('avatarName')=='enKrakenHeadAvatar','Wrong endpoint graph/Avatar')


def source(assets):
    import UnityPy
    (old,_,_),(modern,_,_)=extract(assets)
    env=UnityPy.load(str(assets));file=next(f for f in env.files.values() if Path(f.name).name==assets.name)
    tree=lambda pid:file.objects[pid].read_typetree(check_read=False)
    controller=tree(5973);states={}
    for item in controller['m_Controller']['m_StateMachineArray'][0]['data']['m_StateConstantArray']:
        state=item['data'];nodes=state['m_BlendTreeConstantArray']
        if len(nodes)!=1 or len(nodes[0]['data']['m_NodeArray'])!=1:continue
        node=nodes[0]['data']['m_NodeArray'][0]['data'];ptr=controller['m_AnimationClips'][node['m_ClipID']]
        require(ptr['m_FileID']==0,'External controller clip unsupported')
        clip=tree(ptr['m_PathID']);muscle=clip['m_MuscleClip']
        if clip['m_Name'] not in ('krakenIdle','krakenDamage','krakenDisappear','kraken_appear'):continue
        require(state['m_Speed']==1 and state['m_CycleOffset']==0 and not state['m_Mirror'] and
                all(state.get(k,0)==0 for k in ('m_SpeedParamID','m_MirrorParamID','m_CycleOffsetParamID','m_TimeParamID')) and
                node['m_CycleOffset']==0 and not node['m_Mirror'] and not node['m_ChildIndices'] and node['m_Duration']==1 and
                muscle['m_StartTime']==0 and muscle['m_CycleOffset']==0 and not muscle['m_Mirror'] and not clip['m_Legacy'] and
                state['m_Loop']==muscle['m_LoopTime'],'Unsupported native endpoint clock convention')
        hash_=int(state['m_FullPathID']);hash_=hash_ if hash_<2**31 else hash_-2**32
        states[hash_]={'clip':clip['m_Name'],'loop':state['m_Loop'],'length':muscle['m_StopTime']}
    return old,modern,states


def validate_endpoint(run,request,native,checks,*,main_inputs=None):
    # Internal composed verifier supplies already independently validated fixed snapshots.
    # The original CLI/request remains strict and never supplies this optional input.
    if main_inputs is not None:require(isinstance(main_inputs,list) and len(main_inputs)==241,"Exact241 composed main snapshots required")
    require(set(request)=={'id','session','op','scenario','endpointPolicy'} and request['endpointPolicy']==POLICY,'Exact endpoint request schema required')
    require(all(isinstance(request[k],str) and re.fullmatch('[a-f0-9]{32}',request[k]) for k in ('id','session')),'Exact request nonce/id required')
    require(run.get('endpointPolicy')==POLICY and run.get('provenance')=='owned_controller_endpoint_policy_diagnostic','Wrong endpoint report provenance')
    require(run['cleanup'].get('endpointSurfacesClean') is True,'Endpoint surfaces not cleaned')
    # Validate the common controller envelope without weakening the original validator.
    envelope=dict(run,provenance='owned_native_controller_graph_observation_no_adapter')
    native_request={k:v for k,v in request.items() if k!='endpointPolicy'}
    validate_run(envelope,native_request,checks,'controller')
    old_rest,modern_rest,states=native;target_rest=models(old_rest);source_rest=models(modern_rest)
    target_paths=list(MAPPING);pure_count=0;mixed_count=0;pure_by_clip={};alphas=[]
    for side,expected in (('old',old_rest),('modern',modern_rest)):
        local,_=pose(run[side+'Rest'],expected,checks,'source-rest:'+side)
        for path in expected:close(checks,'immutable-rest:'+side+':'+path,local[path],expected[path])
    rest=run['endpointRests'];output_local,_=pose(rest['output'],old_rest,checks,'output-rest')
    for path in old_rest:close(checks,'output-native-rest:'+path,output_local[path],old_rest[path])
    wanted={'krakenIdle','kraken_appear' if run['scenario']=='appear' else 'krakenDamage' if run['scenario'].startswith('damaged') else 'krakenDisappear'}
    require(set(rest['pairs'])==wanted,'Endpoint pair clip set mismatch')
    for clip,pair in rest['pairs'].items():
        for side,expected in (('old',old_rest),('modern',modern_rest)):
            initial=pair[side+'Initialization']
            require(all(initial.get(k) is True for k in ('rootActive','initializedAfterRebindAndEvaluate','nativeAvatarValid')) and initial.get('nativeAvatarName')=='enKrakenHeadAvatar','Endpoint initialization absent')
            local,_=pose(pair[side],expected,checks,'sampler-rest:'+clip+side)
            for path in expected:close(checks,'independent-rest:'+clip+side+path,local[path],expected[path])
    first_output_id=None;sampler_ids={};owned_ids=set()
    for side in ('modern','old'):
        view=run['frames'][0][side]
        ids={view['rootInstanceId'],view['animatorInstanceId'],*[v['instanceId'] for v in run[side+'Rest']['locals'].values()]}
        require(not (ids & owned_ids),'Native graph owned identities overlap');owned_ids.update(ids)
    output_bones={v['instanceId'] for v in rest['output']['locals'].values()}
    require(len(output_bones)==len(old_rest) and not(output_bones & owned_ids),'Output bone identities overlap');owned_ids.update(output_bones)
    for frame in run['frames']:
        index=frame['step'];label=str(index);policy=frame['endpointPolicy'];transition=frame['modern']['inTransition']
        require(frame['old']['inTransition']==transition,'Graph transition disagreement')
        roles=['current','next'] if transition else ['current']
        samples=policy['samples'];require([s['role'] for s in samples]==roles,'Exact active endpoint roles required; stale next must be ignored')
        by_clip={};weights=[]
        for sample,role in zip(samples,roles):
            state=frame['modern'][role];old_state=frame['old'][role]
            require(state==old_state==sample['state'],'Endpoint sampled clock/state differs from active graph state')
            contract=states[state['fullPathHash']]
            require(number(state['speed'])==number(state['speedMultiplier'])==1 and type(state['loop']) is bool and state['loop']==contract['loop'],'Clock convention mismatch')
            require(number(state['length'])>0,'Invalid state duration')
            require(sample['clip']==contract['clip'] and sample['loop']==contract['loop'],'Wrong state endpoint clip/loop')
            require(sample['clip'] in wanted,'Unexpected scenario clip')
            clips=frame['modern'][role+'Clips'];old_clips=frame['old'][role+'Clips']
            require(len(clips)==len(old_clips)==1 and clips==old_clips,'Exact matching native clip entries required')
            clip=clips[0];weight=number(clip['weight']);require(0<=weight<=1 and sample['rawWeight']==weight,'Raw weight mismatch')
            require(clip['name']==sample['clip'] and clip['instanceId']==sample['clipInstanceId'],'Native clip identity mismatch')
            length=number(sample['clipLength']);require(length>0,'Invalid clip length')
            close(checks,label+':source-length',length,contract['length']);close(checks,label+':clip-length',length,clip['length'])
            phase=number(state['normalizedTime']);mapped=phase-math.floor(phase) if state['loop'] else min(1,max(0,phase))
            seconds=float(np.float32(mapped*length))
            close(checks,label+':unwrapped-clock',sample['requestedUnwrappedSeconds'],phase*length)
            close(checks,label+':mapped-clock',sample['mappedPhase'],mapped)
            close(checks,label+':sample-clock',sample['sampleSeconds'],seconds)
            for side,paths in (('modern',modern_rest),('old',old_rest)):
                sample_settings(sample[side+'Sampler'])
                view=sample[side+'Sampler']
                require(view['clipInstanceId']==sample['clipInstanceId'],'Sampler playable clip identity mismatch')
                ids=(sample[side+'RootInstanceId'],view['animatorInstanceId'],view['avatarInstanceId'],tuple((p,sample[side]['locals'][p]['instanceId']) for p in sorted(paths)))
                key=(sample['clip'],side)
                if key in sampler_ids:require(sampler_ids[key]==ids,'Sampler owned identity changed')
                else:
                    owned={ids[0],ids[1],*[v for _,v in ids[3]]}
                    require(len(owned)==2+len(paths) and all(type(v) is int and v!=0 for v in owned) and not(owned & owned_ids),'Sampler owned identities overlap')
                    require(ids[2]==frame[side]['avatarInstanceId'],'Sampler native Avatar identity differs')
                    owned_ids.update(owned);sampler_ids[key]=ids
                for p in paths:require(sample[side]['locals'][p]['instanceId']==rest['pairs'][sample['clip']][side]['locals'][p]['instanceId'],'Sampler rest bone identity differs')
                close(checks,label+':playable-time:'+side,sample[side+'PlayableTime'],seconds)
                pose(sample[side],paths,checks,label+':sample:'+role+side)
            if not transition:
                require(abs(weight-1)<=TOL and sample['scope']=='observed_pure_graph_same_clock_check','Invalid pure scope/weight')
                for side in ('old','modern'):
                    for section in ('locals','prefabRootModels'):
                        for path in sample[side][section]:
                            a=sample[side][section][path];b=frame[side]['pose'][section][path]
                            close(checks,label+':pure:'+side+section+path,matrix(a['matrix'] if section=='locals' else a),matrix(b['matrix'] if section=='locals' else b))
                require(0<=number(sample['pureMaximumError'])<=TOL,'Pure runtime check failed')
                pure_count+=1;pure_by_clip.setdefault(sample['clip'],[]).append(index)
            else:
                require(sample['scope']=='constructed_policy_endpoint_not_native_mixed_equivalence' and sample['pureMaximumError'] is None,'Mixed endpoint must remain constructed policy')
                mixed_count+=1
            require(sample['clip'] not in by_clip,'Duplicate endpoint clip')
            by_clip[sample['clip']]=sample;weights.append(weight)
        require(abs(sum(weights)-1)<=TOL,'Raw endpoint weights must sum to one before alpha selection')
        appearance=by_clip.get('kraken_appear');alpha=appearance['rawWeight'] if appearance else 0
        require(policy['appearanceWeight']==alpha,'Alpha must equal raw appearance weight')
        alphas.append(alpha)
        native_root=matrix(frame['old']['pose']['prefabRootModels']['Root_M'])
        close(checks,label+':native-root',policy['actualNativeRootModel'],native_root)
        if appearance:
            require(policy['branch']=='authored_endpoint_local_blend','Wrong appearance branch')
            main=next((s for s in samples if s is not appearance),None)
            main_pose=None if main is None else main['modern']
            appearance_models=[matrix(appearance['old']['prefabRootModels'][p]) for p in target_paths]
        else:
            require(policy['branch']==('fixed_modern_sampler' if main_inputs is not None else 'whole_modern_controller'),'Wrong no-appearance branch')
            main_pose=frame['modern']['pose'] if main_inputs is None else main_inputs[index];appearance_models=None
        main_models=None if main_pose is None else [matrix(main_pose['prefabRootModels'][s])@np.linalg.inv(source_rest[s])@target_rest[t] for t,s in MAPPING.items()]
        endpoints=[]
        for name,expected in (('main',main_models),('appearance',appearance_models)):
            recorded=policy[name+'EndpointModels'];locals_=None if expected is None else local_endpoints(expected,native_root)
            if expected is None:require(recorded is None and policy[name+'EndpointLocals']==[],'Absent endpoint metadata mismatch')
            else:
                require(len(recorded)==len(policy[name+'EndpointLocals'])==4,'Exactly four endpoint matrices required')
                for i in range(4):
                    decompose(locals_[i]);close(checks,label+':'+name+'model'+str(i),recorded[i],expected[i]);close(checks,label+':'+name+'local'+str(i),policy[name+'EndpointLocals'][i],locals_[i])
            endpoints.append(locals_)
        expected_local=blend_locals(*endpoints,alpha)
        actual_local,actual_model=pose(policy['output'],old_rest,checks,label+':output')
        if first_output_id is None:
            first_output_id=policy['outputRootInstanceId'];require(first_output_id not in owned_ids,'Output root aliases another owned object');owned_ids.add(first_output_id)
        for p in old_rest:require(policy['output']['locals'][p]['instanceId']==rest['output']['locals'][p]['instanceId'],'Output bone identity changed')
        require(policy['outputRootInstanceId']==first_output_id and type(first_output_id) is int and first_output_id!=0,'Output identity changed')
        for p in ('Root_M',JAW):close(checks,label+':preserve-native-local:'+p,actual_local[p],trs(frame['old']['pose']['locals'][p]))
        expected_all={p:trs(v) for p,v in frame['old']['pose']['locals'].items()}
        expected_all.update(dict(zip(target_paths,expected_local)))
        expected_models=models(expected_all)
        for p in old_rest:close(checks,label+':expected-output:'+p,actual_model[p],expected_models[p])
        for i,p in enumerate(target_paths):
            close(checks,label+':four-local:'+p,actual_local[p],expected_local[i])
            if alpha==0:close(checks,label+':alpha0:'+p,actual_model[p],main_models[i])
            if alpha==1:close(checks,label+':alpha1:'+p,actual_model[p],appearance_models[i])
        require(all(policy.get(k) is True for k in ('nativeInputsUnchanged','outputMirrorsNativeRoot','nativeJawLocalPreserved','repeatIdentical')),'Runtime repeated-commit/preservation assertion failed')
    require(run['pureEndpointChecks']==pure_count and pure_count>0,'Pure check count mismatch')
    require(set(pure_by_clip)==wanted and mixed_count>0,'Missing actual pure target/idle or transition coverage')
    if run['scenario']=='appear':
        pure_appearance=pure_by_clip['kraken_appear'];pure_idle=pure_by_clip['krakenIdle']
        require(any(i<min(pure_appearance) for i in pure_idle) and any(i>max(pure_appearance) for i in pure_idle),'Appearance must include pre/post pure idle')
        for entering in (True,False):
            require(any(f['modern']['inTransition'] and ((f['modern']['currentClips'][0]['name']=='krakenIdle') if entering else (f['modern']['currentClips'][0]['name']=='kraken_appear')) for f in run['frames']),'Missing appearance entry/exit transition')
    return {'pureFramesByClip':pure_by_clip,'pureChecks':pure_count,'constructedMixedSamples':mixed_count,'observedAlphaRange':[min(alphas),max(alphas)]}


def compare(first,request,repeat,repeat_request,native):
    checks=Checks();a=validate_endpoint(first,request,native,checks);b=validate_endpoint(repeat,repeat_request,native,checks)
    require(first['session']==repeat['session'] and first['id']!=repeat['id'] and first['scenario']==repeat['scenario'],'Distinct same-session scenario requests required')
    require(first['pinnedReady']==repeat['pinnedReady'] and first['identity']==repeat['identity'],'Repeat source/Ready changed')
    for i,(x,y) in enumerate(zip(first['frames'],repeat['frames'])):
        for side in ('modern','old'):
            for key in ('current','next','inTransition','transition','currentClips','nextClips','pose'):
                checks.structured('repeat:'+str(i)+side+key,without_ids(x[side][key]),without_ids(y[side][key]))
        require(len(x['endpointPolicy']['samples'])==len(y['endpointPolicy']['samples']),'Repeat endpoint count differs')
        for j,(u,v) in enumerate(zip(x['endpointPolicy']['samples'],y['endpointPolicy']['samples'])):
            for key in ('role','clip','clipInstanceId','clipLength','loop','state','rawWeight','sampleSeconds','mappedPhase','modern','old','scope'):
                checks.structured('repeat-endpoint:'+str(i)+':'+str(j)+key,without_ids(u[key]),without_ids(v[key]))
        for key in ('appearanceWeight','branch','mainEndpointModels','appearanceEndpointModels','mainEndpointLocals','appearanceEndpointLocals','output'):
            checks.structured('repeat-policy:'+str(i)+key,without_ids(x['endpointPolicy'][key]),without_ids(y['endpointPolicy'][key]))
    return {'status':'endpoint_policy_mechanics_match' if not checks.failures else 'endpoint_policy_mechanics_mismatch',
            'scenario':first['scenario'],'session':first['session'],'framesPerRun':241,'tolerance':TOL,'maximumError':checks.maximum,
            'numericChecks':checks.count,'failures':checks.failures,'firstCoverage':a,'repeatCoverage':b,'limitations':LIMITS}


def run(manifest_path):
    manifest=json.loads(Path(manifest_path).read_text())
    require(set(manifest)=={'schema','evidence'} and manifest['schema']=='kraken-endpoint-policy-v1','Exact endpoint manifest schema required')
    require(set(manifest['evidence'])=={'first','firstRequest','repeat','repeatRequest','assets','gameAssembly'},'Exact six pinned evidence files required')
    paths={k:read_pin(v) for k,v in manifest['evidence'].items()}
    require(digest(paths['assets'])==ASSETS and digest(paths['gameAssembly'])==ASSEMBLY,'Unsupported actual source hashes')
    docs={k:json.loads(v.read_text()) for k,v in paths.items() if k not in ('assets','gameAssembly')}
    result=compare(docs['first'],docs['firstRequest'],docs['repeat'],docs['repeatRequest'],source(paths['assets']))
    result.update(manifestSha256=digest(manifest_path),evidence=manifest['evidence'])
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--manifest',required=True,type=Path);parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args();require('scratch' in args.output.resolve().parts,'Pose evidence must remain in scratch')
    result=run(args.manifest)
    with args.output.open('x') as out:json.dump(result,out,indent=2,allow_nan=False)
    print(result['status'],result['maximumError']);raise SystemExit(0 if result['status']=='endpoint_policy_mechanics_match' else 1)

if __name__=='__main__':main()
