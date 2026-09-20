"""Synthetic controller report tests; no native graph simulation or live claims."""
from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy")
import copy
import unittest
import numpy as np
from verify_kraken_controller_repeat import compare, ASSETS, ASSEMBLY, MODERN, OLD


def fixture(case, start):
    dt = float(np.float32(1/60)); identity = np.eye(4).tolist()
    run = {'id': case, 'session': 'session', 'scenario': 'appear', 'ok': True, 'error': None,
           'provenance': 'owned_native_controller_graph_observation_no_adapter', 'engineErrors': [], 'sameReadyAfter': True,
           'pinnedReady': {'session': 'session', 'dungeonInstanceId': 50, 'level': 0, 'room': 2},
           'cleanup': dict(graphsDisposedBeforeTargets=True, modernUnityNull=True, oldUnityNull=True, errors=[]),
           'identity': dict(resourcesSha256=ASSETS, gameAssembly={'assemblyFileSha256': ASSEMBLY}, controllerName='krakenHeadController', sourceControllerId=5973,
                            trigger='Appear', targetState='OverworldAppear', idleStateHash=10, targetStateHash=20),
           'stepSeconds': dt, 'triggerAfterStep': 15, 'expectedFrames': 241, 'frame': start+300, 'frames': []}
    surfaces = {}
    for side, paths in (('modern', MODERN|{'Root_M'}), ('old', OLD|{'Root_M'})):
        pose = {'locals': {p: dict(position=[0.,0.,0.],rotation=[0.,0.,0.,1.],scale=[1.,1.,1.],matrix=copy.deepcopy(identity),instanceId=start+i+1000) for i,p in enumerate(sorted(paths))},
                'prefabRootModels': {p: copy.deepcopy(identity) for p in paths}}
        run[side+'Rest'] = copy.deepcopy(pose); run[side+'Initialization'] = {'initializedAfter': True}
        surfaces[side] = dict(rootInstanceId=start+20,animatorInstanceId=start+21,avatarInstanceId=88,animatorInitialized=True,
                             runtimeControllerAssigned=False,fireEvents=False,applyRootMotion=False,graphMode='Manual',graphOutputCount=1,graphPlayableCount=1,
                             avatarName='enKrakenHeadAvatar',layerName='Base Layer',layerWeight=0.,
                             current=dict(fullPathHash=10,shortNameHash=12,normalizedTime=0.,length=4.,loop=True,speed=1.,speedMultiplier=1.),
                             next=None,inTransition=False,transition=None,currentClips=[dict(name='krakenIdle',instanceId=77,length=4.,weight=1.)],nextClips=[],pose=pose)
    for i in range(241):
        frame = dict(step=i,graphElapsedSeconds=i*dt,manualDeltaSeconds=0 if i==0 else dt,triggerIssued=i>15,unityFrame=start+i)
        frame.update(copy.deepcopy(surfaces))
        for side in ('modern','old'): frame[side]['current']['normalizedTime']=i*dt/4
        run['frames'].append(frame)
    return run, dict(id=case,session='session',op='kraken-controller-fixture',scenario='appear')


class RepeatTests(unittest.TestCase):
    def setUp(self):
        self.a,self.ar=fixture('a',1);self.b,self.br=fixture('b',500)

    def test_repeat_with_different_instance_ids(self):
        r=compare(self.a,self.ar,self.b,self.br)
        self.assertEqual(r['status'],'controller_repeat_match')
        self.assertEqual(r['repeatMaximumError'],0)
        self.assertEqual(r['coverage']['old']['transitionWindows'],[]) # No invented coverage pass.

    def test_source_hash_rejected(self):
        self.a['identity']['resourcesSha256']='0'*64
        with self.assertRaises(ValueError):compare(self.a,self.ar,self.b,self.br)

    def test_step_clock_rejected(self):
        self.a['frames'][16]['graphElapsedSeconds']+=.001
        with self.assertRaises(ValueError):compare(self.a,self.ar,self.b,self.br)

    def test_pose_mismatch_detected(self):
        self.b['frames'][20]['old']['pose']['locals']['Root_M/joint1/neck/jaw']['matrix'][0][3]=.01
        r=compare(self.a,self.ar,self.b,self.br)
        self.assertEqual(r['status'],'controller_repeat_mismatch')

    def test_nonfinite_rejected(self):
        self.a['frames'][0]['old']['pose']['locals']['Root_M']['position'][0]=float('nan')
        with self.assertRaises(ValueError):compare(self.a,self.ar,self.b,self.br)

    def test_stale_next_clips_preserved(self):
        for r in (self.a,self.b):r['frames'][41]['old']['nextClips']=copy.deepcopy(r['frames'][41]['old']['currentClips'])
        result=compare(self.a,self.ar,self.b,self.br)
        self.assertEqual(result['status'],'controller_repeat_match')
        self.assertEqual(result['coverage']['old']['inactiveTransitionWithNonemptyNextClipsSteps'],[41])

    def test_cleanup_error_rejected(self):
        self.a['engineErrors']=[{'type':'Exception','condition':'cleanup'}]
        with self.assertRaises(ValueError):compare(self.a,self.ar,self.b,self.br)

    def test_request_session_rejected(self):
        self.ar['session']='other'
        with self.assertRaises(ValueError):compare(self.a,self.ar,self.b,self.br)


if __name__=='__main__':unittest.main()
