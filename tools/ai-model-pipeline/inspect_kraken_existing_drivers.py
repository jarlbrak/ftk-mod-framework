#!/usr/bin/env python3
"""Read-only comparison of optional owned native input observations; no adapter acceptance."""
import argparse,json,hashlib
from pathlib import Path
import numpy as np
from verify_kraken_controller_repeat import validate_run, Checks
PATHS=['Root_M/base','Root_M/base/body','Root_M/base/body/neck','Root_M/base/body/neck/head','Root_M/base/body/neck/head/topHead','Root_M/base/body/neck/eye']

def require(value,reason):
    if not value:raise ValueError(reason)

def mat(value):
    a=np.asarray(value,dtype=float);require(a.shape==(4,4) and np.isfinite(a).all(),'finite4x4 required');return a

def observation(value):
    require(value['scope']=='owned_native_controller_input_observation_no_adapter','wrong observation scope')
    paths=value['existingPaths'];require(len(paths)<=256 and len(set(paths))==len(paths) and '' in paths and 'Root_M' in paths,'invalid existing paths')
    require(value['requiredPaths']==PATHS,'required paths changed')
    missing=[p for p in PATHS if p not in paths]
    require(value['missingPaths']==missing,'missing paths not factual')
    require(value['status']==('unavailable_missing_native_paths' if missing else 'available'),'availability mismatch')
    present=['Root_M']+[p for p in PATHS if p not in missing];pose=value['pose']
    require(set(pose['locals'])==set(pose['prefabRootModels'])==set(present),'pose does not match existing paths')
    for p in present:
        mat(pose['locals'][p]['matrix']);mat(pose['prefabRootModels'][p])
    return missing

def inspect(report):
    require(report['ok'] is True and report['sameReadyAfter'] is True and len(report['frames'])==241,'complete guarded report required')
    rest=report['identity']['oldExistingDriverRest'];missing=observation(rest)
    identities={p:v['instanceId'] for p,v in rest['pose']['locals'].items()}
    maxima={p:{'local':0.,'model':0.} for p in identities}
    for step,frame in enumerate(report['frames']):
        require(frame['step']==step,'frame gap')
        checks=Checks()
        for key in ('current','next','inTransition','transition','currentClips','nextClips'):
            checks.structured('matched native clocks/'+key,frame['old'][key],frame['modern'][key])
        require(not checks.failures,'native surface clocks/weights differ')
        old=frame['old']['existingDrivers'];require(observation(old)==missing and old['existingPaths']==rest['existingPaths'],'hierarchy changed')
        require({p:v['instanceId'] for p,v in old['pose']['locals'].items()}==identities,'native input identity changed')
        require(old['pose']['prefabRootModels']['Root_M']==frame['old']['pose']['prefabRootModels']['Root_M'],'old root observation disagrees')
        for p in identities:
            for kind,key in [('local','locals'),('model','prefabRootModels')]:
                a=old['pose'][key][p];b=frame['modern']['pose'][key][p]
                if kind=='local':a=a['matrix'];b=b['matrix']
                maxima[p][kind]=max(maxima[p][kind],float(abs(mat(a)-mat(b)).max()))
    return {'status':'unavailable_missing_native_paths' if missing else 'observed_existing_native_drivers','missingPaths':missing,'maximumDifferencesFromModernInput':maxima,'frames':241,'scenario':report['scenario'],'scope':'Native owned input observations only. Differences are measurements, not adapter acceptance; no synthesized nodes or root correction.'}

def inspect_validated(report,request):
    require(set(request)=={'id','session','op','scenario','observeExistingDrivers'} and request['observeExistingDrivers'] is True,'exact observation-only request required')
    base={k:v for k,v in request.items() if k!='observeExistingDrivers'}
    checks=Checks();validate_run(report,base,checks,'driver-observation')
    require(not checks.failures,'native graph TRS/model verification failed')
    return inspect(report)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--report',type=Path,required=True);p.add_argument('--request',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    require('scratch' in a.output.resolve().parts,'scratch output required');result=inspect_validated(json.loads(a.report.read_text()),json.loads(a.request.read_text()));result['requestSha256']=hashlib.sha256(a.request.read_bytes()).hexdigest();result['reportSha256']=hashlib.sha256(a.report.read_bytes()).hexdigest();a.output.write_text(json.dumps(result,indent=2)+'\n')
