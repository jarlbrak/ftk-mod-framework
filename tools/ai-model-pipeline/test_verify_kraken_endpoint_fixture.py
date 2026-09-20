"""Synthetic full-report math/guard regressions. No Unity or native equivalence claim."""
from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy")
import copy
import json
import tempfile
import unittest
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
from test_verify_kraken_controller_repeat import fixture as controller_fixture
from test_kraken_endpoint_policy import locals_for,blend,models_for
from verify_kraken_endpoint_fixture import compare,validate_endpoint,Checks,POLICY,run
from audit_kraken_adapter import MAPPING,JAW,models


def make_pose(locals_,template):
    out=copy.deepcopy(template)
    for path,m in locals_.items():
        out['locals'][path].update(position=m[:3,3].tolist(),rotation=Rotation.from_matrix(m[:3,:3]).as_quat().tolist(),scale=[1.,1.,1.],matrix=m.tolist())
    out['prefabRootModels']={p:m.tolist() for p,m in models(locals_).items()}
    return out


def fixture(case='a',start=1):
    report,request=controller_fixture(case*32,start);request['session']='f'*32;request['endpointPolicy']=POLICY
    report['session']='f'*32;report['pinnedReady']['session']='f'*32
    report.update(endpointPolicy=POLICY,provenance='owned_controller_endpoint_policy_diagnostic');report['cleanup']['endpointSurfacesClean']=True
    rest={side:{p:np.eye(4) for p in report[side+'Rest']['locals']} for side in ('old','modern')}
    for side in ('old','modern'):
        for n,path in enumerate(sorted(rest[side])):
            rest[side][path][:3,:3]=Rotation.from_euler('y',(.015 if side=='old' else -.02)*(n+1)).as_matrix()
            rest[side][path][2,3]=(.03 if side=='old' else -.04)*(n+1)
        report[side+'Rest']=make_pose(rest[side],report[side+'Rest'])
    for row in report['oldRest']['locals'].values():row['instanceId']+=10000
    report['endpointRests']={'output':copy.deepcopy(report['oldRest']),'pairs':{}}
    for row in report['endpointRests']['output']['locals'].values():row['instanceId']+=30000
    for clip in ('krakenIdle','kraken_appear'):
        report['endpointRests']['pairs'][clip]={side:copy.deepcopy(report[side+'Rest']) for side in ('old','modern')}
        for side in ('old','modern'):
            for row in report['endpointRests']['pairs'][clip][side]['locals'].values():row['instanceId']+=100000+(50000 if clip=='kraken_appear' else 0)
        for side in ('old','modern'):report['endpointRests']['pairs'][clip][side+'Initialization']={'rootActive':True,'initializedAfterRebindAndEvaluate':True,'nativeAvatarValid':True,'nativeAvatarName':'enKrakenHeadAvatar'}
    settings=dict(cullingMode='AlwaysAnimate',animatorEnabled=True,animatorInitialized=True,avatarValid=True,graphValid=True,runtimeControllerAssigned=False,fireEvents=False,applyRootMotion=False,footIK=False,graphUpdateMode='Manual',playableCount=1,outputCount=1,avatarName='enKrakenHeadAvatar')
    pure=0
    for frame in report['frames']:
        i=frame['step'];exiting=101<=i<=110;transition=16<=i<=24 or exiting;appear=25<=i<=110
        state=dict(fullPathHash=20 if appear else 10,shortNameHash=12,normalizedTime=i*.002,length=4.,loop=not appear,speed=1.,speedMultiplier=1.)
        nxt=dict(state,fullPathHash=10 if exiting else 20,loop=exiting,normalizedTime=.01+(i-101)*.002 if exiting else .27+(i-16)*.002) if transition else None
        weight=(i-100)/11 if exiting else (i-15)/10 if transition else 1
        curclip='kraken_appear' if appear else 'krakenIdle'
        cur={'name':curclip,'instanceId':78 if appear else 77,'length':4.,'weight':1-weight if transition else 1.}
        nextclip={'name':'krakenIdle' if exiting else 'kraken_appear','instanceId':77 if exiting else 78,'length':4.,'weight':weight}
        samples=[]
        for role,st,cl in [('current',state,cur)]+([('next',nxt,nextclip)] if transition else []):
            phase=st['normalizedTime'];seconds=float(np.float32(phase*4))
            sample=dict(role=role,clip=cl['name'],clipInstanceId=cl['instanceId'],clipLength=4.,loop=st['loop'],state=copy.deepcopy(st),rawWeight=cl['weight'],requestedUnwrappedSeconds=phase*4,mappedPhase=phase,sampleSeconds=seconds,modernPlayableTime=seconds,oldPlayableTime=seconds,scope='constructed_policy_endpoint_not_native_mixed_equivalence' if transition else 'observed_pure_graph_same_clock_check',pureMaximumError=None if transition else 0.)
            for side in ('old','modern'):
                local=copy.deepcopy(rest[side]);root=np.eye(4);root[0,3]=.13;local['Root_M']=root
                paths=list(MAPPING.values()) if side=='modern' else list(MAPPING)
                for n,path in enumerate(paths):
                    local[path][:3,:3]=Rotation.from_euler('z',(.07+n*.04)*np.sin(seconds)).as_matrix()
                    local[path][1,3]=(.1+n*.02)*seconds
                sample[side]=make_pose(local,report['endpointRests']['pairs'][cl['name']][side]);base=start+500000+(10000 if side=='old' else 0)+(100000 if cl['name']=='kraken_appear' else 0)
                sample[side+'RootInstanceId']=base;sample[side+'Sampler']=dict(settings,clipInstanceId=cl['instanceId'],animatorInstanceId=base+1,avatarInstanceId=88)
            samples.append(sample)
        for side in ('old','modern'):
            frame[side].update(current=copy.deepcopy(state),next=copy.deepcopy(nxt),inTransition=transition,transition={'fullPathHash':555} if transition else None,currentClips=[copy.deepcopy(cur)],nextClips=[copy.deepcopy(nextclip)] if transition else [],pose=copy.deepcopy(samples[0][side]))
            for path in frame[side]['pose']['locals']:frame[side]['pose']['locals'][path]['instanceId']=report[side+'Rest']['locals'][path]['instanceId']
            if side=='old':
                frame[side]['rootInstanceId']+=10000;frame[side]['animatorInstanceId']+=10000
        alpha=1-weight if exiting else weight if transition else 1 if appear else 0
        root=np.array(frame['old']['pose']['prefabRootModels']['Root_M'])
        main_sample=next((s for s in samples if s['clip']=='krakenIdle'),None)
        main=None if main_sample is None else [np.array(main_sample['modern']['prefabRootModels'][s])@np.linalg.inv(models(rest['modern'])[s])@models(rest['old'])[t] for t,s in MAPPING.items()]
        app_sample=next((s for s in samples if s['clip']=='kraken_appear'),None)
        app=None if app_sample is None else [np.array(app_sample['old']['prefabRootModels'][p]) for p in MAPPING]
        al=None if main is None else locals_for(main,root);bl=None if app is None else locals_for(app,root)
        # Independent synthetic producer uses its separate endpoint-policy algebra.
        if al is None:output_mats=app
        elif bl is None:output_mats=main
        else:output_mats=models_for(blend(al,bl,alpha),root)
        output_locals={p:np.array(v['matrix']) for p,v in frame['old']['pose']['locals'].items()}
        for n,path in enumerate(MAPPING):output_locals[path]=np.linalg.solve(root if n==0 else output_mats[n-1],output_mats[n])
        def matrices(values):
            return [] if values is None else [_trs(v).tolist() for v in values]
        frame['endpointPolicy']=dict(samples=samples,appearanceWeight=alpha,branch='authored_endpoint_local_blend' if transition or appear else 'whole_modern_controller',mainEndpointModels=None if main is None else [m.tolist() for m in main],appearanceEndpointModels=None if app is None else [m.tolist() for m in app],mainEndpointLocals=matrices(al),appearanceEndpointLocals=matrices(bl),actualNativeRootModel=root.tolist(),outputRootInstanceId=start+555,output=make_pose(output_locals,report['endpointRests']['output']),nativeInputsUnchanged=True,outputMirrorsNativeRoot=True,nativeJawLocalPreserved=True,repeatIdentical=True)
        if not transition:pure+=1
    report['pureEndpointChecks']=pure
    native=(rest['old'],rest['modern'],{10:dict(clip='krakenIdle',loop=True,length=4.),20:dict(clip='kraken_appear',loop=False,length=4.)})
    return report,request,native


def _trs(parts):
    p,q,s=parts;m=np.eye(4);m[:3,:3]=Rotation.from_quat(q).as_matrix()@np.diag(s);m[:3,3]=p;return m


class EndpointVerifierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.base,cls.request,cls.native=fixture()

    def check_bad(self,change):
        report=copy.deepcopy(self.base);change(report);checks=Checks()
        try:validate_endpoint(report,self.request,self.native,checks)
        except (ValueError,KeyError):return
        self.assertTrue(checks.failures,'Corruption passed independent checks')

    def test_full_nonconstant_compound_rotation_report(self):
        repeat,request,_=fixture('b',500)
        result=compare(self.base,self.request,repeat,request,self.native)
        self.assertEqual(result['status'],'endpoint_policy_mechanics_match');self.assertLess(result['maximumError'],1e-12)
        self.assertGreater(result['firstCoverage']['constructedMixedSamples'],0)

    def test_frame_gap_and_cleanup_refused(self):
        self.check_bad(lambda r:r['frames'].pop(20))
        self.check_bad(lambda r:r['cleanup'].update(endpointSurfacesClean=False))

    def test_raw_alpha_and_clock_cannot_use_transition_time(self):
        self.check_bad(lambda r:r['frames'][16]['endpointPolicy'].update(appearanceWeight=.5))
        self.check_bad(lambda r:r['frames'][16]['endpointPolicy']['samples'][1].update(sampleSeconds=.01))
        self.check_bad(lambda r:r['frames'][16]['modern']['currentClips'][0].update(weight=.7))

    def test_wrong_parent_or_target_formula_detected(self):
        self.check_bad(lambda r:r['frames'][16]['endpointPolicy']['mainEndpointLocals'][1][0].__setitem__(3,.31))
        self.check_bad(lambda r:r['frames'][16]['endpointPolicy']['mainEndpointModels'][1][1].__setitem__(3,9.))

    def test_output_trs_shear_and_jaw_not_just_labels(self):
        self.check_bad(lambda r:r['frames'][20]['endpointPolicy']['output']['locals'][JAW]['position'].__setitem__(0,.1))
        self.check_bad(lambda r:r['frames'][20]['endpointPolicy']['output']['locals']['Root_M']['matrix'][0].__setitem__(1,.1))
        self.check_bad(lambda r:r['frames'][20]['endpointPolicy']['output']['prefabRootModels'][JAW][0].__setitem__(3,.1))

    def test_baseline_mismatch_even_reported_zero(self):
        self.check_bad(lambda r:r['frames'][0]['endpointPolicy']['samples'][0]['old']['prefabRootModels'][JAW][0].__setitem__(3,.3))
        self.check_bad(lambda r:r['endpointRests']['pairs']['krakenIdle']['modern']['locals']['Root_M']['position'].__setitem__(0,.2))

    def test_mixed_cannot_claim_native_pure_scope(self):
        self.check_bad(lambda r:r['frames'][16]['endpointPolicy']['samples'][1].update(scope='observed_pure_graph_same_clock_check'))

    def test_actual_sampler_schema_and_identity_fail_closed(self):
        self.check_bad(lambda r:r['endpointRests']['pairs']['krakenIdle']['oldInitialization'].pop('initializedAfterRebindAndEvaluate'))
        self.check_bad(lambda r:r['frames'][0]['endpointPolicy']['samples'][0]['oldSampler'].update(clipInstanceId=999))
        self.check_bad(lambda r:r['frames'][1]['endpointPolicy']['samples'][0]['oldSampler'].update(animatorInstanceId=999))
        self.check_bad(lambda r:r['frames'][0]['endpointPolicy'].update(outputRootInstanceId=r['frames'][0]['modern']['rootInstanceId']))

    def test_true_repeat_pose_drift_and_transition_mismatch(self):
        repeat,request,_=fixture('b',500)
        repeat['frames'][16]['modern']['transition']['fullPathHash']=777
        with self.assertRaises(ValueError):compare(self.base,self.request,repeat,request,self.native)

    def test_manifest_missing_or_mismatched_pins(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'manifest.json';path.write_text(json.dumps({'schema':'kraken-endpoint-policy-v1','evidence':{}}))
            with self.assertRaises(ValueError):run(path)
            evidence=Path(directory)/'evidence.json';evidence.write_text('{}')
            path.write_text(json.dumps({'schema':'kraken-endpoint-policy-v1','evidence':{k:{'path':str(evidence),'sha256':'0'*64} for k in ('first','firstRequest','repeat','repeatRequest','assets','gameAssembly')}}))
            with self.assertRaises(ValueError):run(path)

if __name__=='__main__':unittest.main()
