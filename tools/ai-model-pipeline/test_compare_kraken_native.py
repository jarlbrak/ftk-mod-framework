import copy
import json
import tempfile
import unittest
from pathlib import Path
import numpy as np
import compare_kraken_native as c


def model(loop=False):
    state=dict(name='Base Layer.ATTACK',clip='krakenAttack',seconds=4.0,loop=loop,unsupported=[])
    return dict(states={'1':state},controller='krakenHeadController')


def frame(index):
    return dict(frame=100+index,gameSeconds=index*.1,deltaTime=.1,timeScale=1.,active=True,enabled=True,
        ragdoll={'m_DoRagdoll':False},animator=dict(enabled=True,speed=1.,layers=[dict(layer=0,stateHash=1,
        normalizedTime=index*.02,transition=False,playing=[dict(name='krakenAttack',weight=1.,clipSeconds=4.)])]))


def bone(position=(0,0,0)):
    return dict(localPosition=list(position),localRotation=[0,0,0,1],localScale=[1,1,1])


class ClockAndSelection(unittest.TestCase):
    def test_loop_and_clamp_are_explicit(self):
        t,clock=c.clip_clock(2.25,model(True)['states']['1']);self.assertEqual(t,1);self.assertEqual(clock['loopIndex'],2)
        t,clock=c.clip_clock(2.25,model()['states']['1']);self.assertEqual(t,4);self.assertTrue(clock['clamped'])
        self.assertEqual(c.clip_clock(1.,model(True)['states']['1'])[0],0)

    def test_unsupported_clock_fails(self):
        m=model();m['states']['1']['unsupported']=['m_TimeParamID']
        with self.assertRaises(ValueError):c.clip_clock(.2,m['states']['1'])
        for value in [float('nan'),float('inf'),-.1,True]:
            with self.assertRaises(ValueError):c.clip_clock(value,model()['states']['1'])

    def test_stable_interior_and_32_bound(self):
        selected,_=c.select_frames([frame(i) for i in range(100)],model(True))
        rows=selected['krakenAttack'];self.assertEqual(len(rows),32)
        self.assertGreater(rows[0]['frameIndex'],0);self.assertLess(rows[-1]['frameIndex'],99)
        self.assertEqual(len({r['time'] for r in rows}),32)

    def test_excludes_transition_neighbours(self):
        frames=[frame(i) for i in range(9)];frames[4]['animator']['layers'][0]['transition']=True
        selected,_=c.select_frames(frames,model())
        self.assertEqual([r['frameIndex'] for r in selected['krakenAttack']],[1,2,6,7])

    def test_mixed_weights_layers_pause_and_ragdoll(self):
        edits=[lambda f:f['animator']['layers'][0]['playing'][0].update(weight=.99),
               lambda f:f['animator']['layers'].append(copy.deepcopy(f['animator']['layers'][0])),
               lambda f:f.update(timeScale=0),lambda f:f['ragdoll'].update(m_DoRagdoll=True),
               lambda f:f['animator'].update(speed=2),lambda f:f['animator']['layers'][0].update(stateHash=2)]
        for edit in edits:
            f=frame(1);edit(f)
            with self.assertRaises(ValueError):c.pure_frame(f,model())

    def test_clipSeconds_is_checked_as_duration(self):
        f=frame(1);f['animator']['layers'][0]['playing'][0]['clipSeconds']=.2
        with self.assertRaises(ValueError):c.pure_frame(f,model())

    def test_no_vacuous_single_time(self):
        frames=[frame(i) for i in range(8)]
        for f in frames:f['animator']['layers'][0]['normalizedTime']=2
        with self.assertRaises(ValueError):c.select_frames(frames,model())


class PoseComparison(unittest.TestCase):
    def setup_case(self):
        m=model();frames=[frame(i) for i in range(5)]
        for f in frames:f['bones']=[bone((0,.2,0)) for p in c.PATHS]
        selected,_=c.select_frames(frames,m)
        poses=[]
        for row in selected['krakenAttack']:
            poses.append(dict(clip='krakenAttack',time=row['time'],fullRestResetBeforeSample=True,samplingMethod='clip-playable',modernFixtureActive=True,
                modernRootAnimated=c.trs(bone((0,.2,0))).tolist(),driverLocals={p:c.trs(bone((0,.2,0))).tolist() for p in c.PATHS[1:]},
                modernPlayable=dict(animatorEnabled=True,animatorInitialized=True,avatarValid=True,runtimeControllerAssigned=False,fireEvents=False,
                    applyRootMotion=False,graphValid=True,graphUpdateMode='Manual',playableCount=1,outputCount=1,footIK=False,clipInstanceId=7)))
        sample=dict(ok=True,session='s',samplingMethod='clip-playable',provenance='isolated_controller_free_manual_clip_playable_native_avatar',
            coordinateConvention='independent-modern-root-and-local-drivers',identity=dict(clipName='krakenAttack',clipLength=4.,clipInstanceId=7,
                controllerName='krakenHeadController',modernSource='native-enemy:krakenHead',modernInitialization=dict(initializedAfterRebindAndEvaluate=True,nativeAvatarValid=True)),
            cleanup=dict(modernUnityNull=True,oldUnityNull=True,graphsDisposedBeforeTargets=True,errors=[]),frames=poses)
        return {'frames':frames},selected,sample,m

    def test_local_and_common_root_pose_match(self):
        cap,selection,sample,m=self.setup_case()
        result=c.compare_rows(cap,selection,sample,'s',m,1e-4)
        self.assertTrue(result['pass_']);self.assertEqual(result['frames'][0]['maximumDelta'],0)

    def test_changed_joint_and_root_fail(self):
        for path in [c.PATHS[0],c.PATHS[-1]]:
            cap,selection,sample,m=self.setup_case()
            matrix=sample['frames'][0]['modernRootAnimated'] if path==c.PATHS[0] else sample['frames'][0]['driverLocals'][path]
            matrix[0][3]+=.01
            self.assertFalse(c.compare_rows(cap,selection,sample,'s',m,1e-4)['pass_'])

    def test_exact_time_nonce_cleanup_and_playable_required(self):
        cap,selection,sample,m=self.setup_case()
        edits=[lambda s:s.update(session='other'),lambda s:s['frames'][0].update(time=0),
               lambda s:s['cleanup'].update(modernUnityNull=False),
               lambda s:s['frames'][0]['modernPlayable'].update(runtimeControllerAssigned=True)]
        for edit in edits:
            s=copy.deepcopy(sample);edit(s)
            with self.assertRaises(ValueError):c.compare_rows(cap,selection,s,'s',m,1e-4)

    def test_reject_nonfinite_and_invalid_quaternion(self):
        with self.assertRaises(ValueError):c.matrix([[float('nan')]*4]*4)
        bad=bone();bad['localRotation']=[0,0,0,0]
        with self.assertRaises(ValueError):c.trs(bad)

    def test_compose_uses_hierarchy_not_world_location(self):
        locals={p:np.eye(4) for p in c.PATHS};locals['Root_M'][0,3]=10;locals['Root_M/base'][1,3]=2
        self.assertEqual(c.compose(locals)['Root_M/base/body'][0,3],10)
        self.assertEqual(c.compose(locals)['Root_M/base/body'][1,3],2)


class ProvenancePins(unittest.TestCase):
    def test_missing_and_tampered_evidence_fail_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);manifest=root/'manifest.json';asset=root/'assets';asset.write_bytes(b'a')
            manifest.write_text(json.dumps(dict(version=1,evidence={'assets':{'path':'assets','sha256':'0'*64}})))
            with self.assertRaises(ValueError):c.load_manifest(manifest)
            manifest.write_text(json.dumps(dict(version=1,evidence={})))
            with self.assertRaises(KeyError):c.load_manifest(manifest)

    def test_source_profile_inventory_and_session_join(self):
        session='a'*32;signature='b'*64;profile_hash='c'*64
        profile=dict(key='probe',baseEnemy='krakenHead',combatProfile='combat',renderers=[dict(rendererPath='kraken2',glbFile='probe.glb')])
        report=dict(status='registered',registered=[dict(key='probe',baseEnemy='krakenHead',combatProfile='combat')])
        bones=[dict(name=path.split('/')[-1],bindposeRowMajor=np.eye(4).reshape(-1).tolist(),**bone()) for path in c.PATHS]
        target=dict(instanceId=10,ownerKind='enemies',ownerInstanceId=11,celInstanceId=12,celRelativeRendererPath='kraken2',
                    celRootName='enKraken(Clone)',mesh='ftkmf_glb_probe.glb',boneSignature=signature,
                    animator=dict(instanceId=13,controller='krakenHeadController'),bones=bones)
        m=dict(renderer=dict(combat_profile_fingerprint='combat'),prefab='enKraken',controller='krakenHeadController',bindposes=[np.eye(4).tolist()]*7)
        manifest=dict(session=session,profileKey='probe',rendererInstanceId=10,evidence={'profiles':{'sha256':profile_hash}})
        docs=dict(profiles={'profiles':[profile]},registration=report,inventory=dict(ok=True,session=session,renderers=[target]),
                  capture=dict(ok=True,error=None,requestedSeconds=1,requestedFps=1,session=session,scope='enemies',provenance='observed-runtime',ownerInstanceId=11,celInstanceId=12,frames=[dict(frame=100,**copy.deepcopy(target))]),
                  journal=[dict(kind='provenance',data=dict(session=session,profile=profile,profileInputSha256=profile_hash)),dict(kind='enemy-registration',data={'report':report})])
        self.assertEqual(c.verify_documents(manifest,docs,m)['instanceId'],10)
        edits=[lambda d:d['capture'].update(session='d'*32),lambda d:d['capture']['frames'][0]['animator'].update(instanceId=99),
               lambda d:d['profiles']['profiles'][0].update(resourcePrefab='enkrakenhead'),
               lambda d:d['inventory']['renderers'][0]['animator'].update(controller='wrongController'),
               lambda d:d['capture'].update(provenance='native-state-playback'),
               lambda d:d['inventory']['renderers'][0]['bones'][0]['bindposeRowMajor'].__setitem__(0,2)]
        for edit in edits:
            changed=copy.deepcopy(docs);edit(changed)
            with self.assertRaises(ValueError):c.verify_documents(manifest,changed,m)
        prefix=copy.deepcopy(docs)
        prefix['capture'].update(ok=False,requestedFps=2,error=RendererDestroyedPrefix.error,frame=101)
        opted=dict(manifest,allowRendererDestroyedPrefix=True)
        self.assertEqual(c.verify_documents(opted,prefix,m)['instanceId'],10)
        prefix['capture']['frames'][0]['ownerInstanceId']=99
        with self.assertRaises(ValueError):c.verify_documents(opted,prefix,m)

    def test_explicit_json_pointer(self):
        self.assertEqual(c.pointer([{'data':{'hash':'abc'}}],'/0/data/hash'),'abc')
        with self.assertRaises(ValueError):c.pointer({},'hash')


class ControllerTeardownPrefix(unittest.TestCase):
    def capture(self):
        identity=dict(targetRendererInstanceId=3,targetOwnerInstanceId=4,targetCelInstanceId=5,targetAnimatorInstanceId=6,targetFid={'photonId':-1,'turnIndex':0})
        event=dict(identity,nativeMethod='CharacterEventListener.CombatTrigger entry',trigger='Death',finalized=True,exception=None,frame=101,realtime=1.1)
        terminal=dict(reason='controller-unresolved-after-observed-native-death',controllerError='Avatar controller ambiguous or absent.',terminalFrameCaptured=False,frame=108,realtime=2.)
        baseline=dict(animatorInstanceId=6,baseLayerIdle=True,baseLayerTransition=False,animatorEnabled=True,animatorActive=True)
        motion=dict(identity,schema='ftkmf.native-combat-motion.v1',error=None,events=[event],eventCount=1,termination=terminal,
                    startedFrame=100,startedRealtime=1.,baseline=baseline)
        return dict(ok=False,error='Controller resolution unavailable after observed native Death.',requestedSeconds=1.,requestedFps=10.,frame=109,
                    ownerInstanceId=4,celInstanceId=5,motionObservation=motion,
                    frames=[dict(frame=100+i,instanceId=3,ownerInstanceId=4,celInstanceId=5,motionAnimator={'animatorInstanceId':6}) for i in range(8)])

    def test_terminal_prefix_never_claims_complete_capture(self):
        result=c.capture_boundary({'allowDeathControllerTeardownPrefix':True},self.capture())
        self.assertTrue(result['partial']);self.assertFalse(result['completeCapture']);self.assertFalse(result['rawCaptureOk'])
        self.assertEqual(result['termination'],'controller_unresolved_after_native_death')
        self.assertEqual(result['retainedFrameCount'],8)

    def test_opt_in_and_provenance_fail_closed(self):
        for manifest in ({},{'allowRendererDestroyedPrefix':True},{'allowDeathControllerTeardownPrefix':'true'}):
            with self.assertRaises(ValueError):c.capture_boundary(manifest,self.capture())
        for change in ('ok','trigger','finalized','exception','renderer','event_identity','future','terminal','telemetry'):
            cap=self.capture();motion=cap['motionObservation'];event=motion['events'][0]
            if change=='ok':cap['ok']=True
            elif change=='trigger':event['trigger']='2HandWield_Death'
            elif change=='finalized':event['finalized']=False
            elif change=='exception':event.pop('exception')
            elif change=='renderer':cap['frames'][2]['instanceId']=99
            elif change=='event_identity':event['targetCelInstanceId']=99
            elif change=='future':event['frame']=110
            elif change=='terminal':motion['termination']['frame']=107
            elif change=='telemetry':motion['error']='failed'
            with self.subTest(change=change),self.assertRaises(ValueError):c.capture_boundary({'allowDeathControllerTeardownPrefix':True},cap)

    def test_pre_arm_death_and_unjoined_animator_rejected(self):
        for change in ('old_frame','old_clock','missing_start','forged_pair','retained_id','missing_retained','bad_baseline','missing_baseline','bool_count','bool_start','nan_start','bool_event','inf_event','bool_terminal'):
            cap=self.capture();motion=cap['motionObservation'];event=motion['events'][0]
            if change=='old_frame':event['frame']=99
            elif change=='old_clock':event['realtime']=.9
            elif change=='missing_start':motion.pop('startedFrame')
            elif change=='forged_pair':motion['targetAnimatorInstanceId']=event['targetAnimatorInstanceId']=99
            elif change=='retained_id':cap['frames'][3]['motionAnimator']['animatorInstanceId']=99
            elif change=='missing_retained':cap['frames'][3].pop('motionAnimator')
            elif change=='bad_baseline':motion['baseline']['baseLayerIdle']=False
            elif change=='missing_baseline':motion.pop('baseline')
            elif change=='bool_count':motion['eventCount']=True
            elif change=='bool_start':motion['startedRealtime']=True
            elif change=='nan_start':motion['startedRealtime']=float('nan')
            elif change=='bool_event':event['realtime']=True
            elif change=='inf_event':event['realtime']=float('inf')
            elif change=='bool_terminal':motion['termination']['realtime']=True
            with self.subTest(change=change),self.assertRaises(ValueError):c.capture_boundary({'allowDeathControllerTeardownPrefix':True},cap)

    def test_death_exactly_at_arm_boundary_is_allowed(self):
        cap=self.capture();event=cap['motionObservation']['events'][0]
        event.update(frame=100,realtime=1.)
        self.assertFalse(c.capture_boundary({'allowDeathControllerTeardownPrefix':True},cap)['completeCapture'])


class RendererDestroyedPrefix(unittest.TestCase):
    error='System.InvalidOperationException: Renderer destroyed during capture.\n  at RuntimeModelTest+<Capture>d__100.MoveNext () [0x00000] in <filename unknown>:0 '

    def capture(self):
        return dict(ok=False,error=self.error,requestedSeconds=1.,requestedFps=10.,frame=108,frames=[frame(i) for i in range(8)])

    def test_opt_in_required_and_exact_boolean(self):
        for manifest in [{},{'allowRendererDestroyedPrefix':False},{'allowRendererDestroyedPrefix':'true'},{'allowRendererDestroyedPrefix':1}]:
            with self.assertRaises(ValueError):c.capture_boundary(manifest,self.capture())

    def test_exact_allowed_prefix_preserves_boundary(self):
        result=c.capture_boundary({'allowRendererDestroyedPrefix':True},self.capture())
        self.assertTrue(result['partial']);self.assertFalse(result['completeCapture'])
        self.assertEqual(result['rawCaptureError'],self.error);self.assertEqual(result['retainedFrameCount'],8)
        self.assertEqual(result['requestedFrameCount'],10);self.assertEqual(result['lastRetainedFrame'],107)

    def test_other_or_nested_errors_never_allowed(self):
        errors=['System.InvalidOperationException: Avatar owner changed during capture.',
                self.error.replace('Renderer destroyed','Renderer maybe destroyed'),
                self.error+'\nInner exception: Renderer destroyed during capture.',
                self.error.replace('<Capture>', '<OtherOperation>'),
                'Renderer destroyed during capture.']
        for error in errors:
            cap=self.capture();cap['error']=error
            with self.assertRaises(ValueError):c.capture_boundary({'allowRendererDestroyedPrefix':True},cap)

    def test_empty_full_duplicate_or_unordered_prefix_rejected(self):
        for indices in [[],list(range(10)),[0,0,1],[2,1]]:
            cap=self.capture();cap['frames']=[frame(i) for i in indices]
            with self.assertRaises(ValueError):c.capture_boundary({'allowRendererDestroyedPrefix':True},cap)
        cap=self.capture();cap['frame']=107
        with self.assertRaises(ValueError):c.capture_boundary({'allowRendererDestroyedPrefix':True},cap)

    def test_failed_capture_does_not_mark_successful_capture_partial(self):
        cap=self.capture();cap.update(ok=True,error=None,requestedFps=8.)
        result=c.capture_boundary({'allowRendererDestroyedPrefix':True},cap)
        self.assertTrue(result['completeCapture']);self.assertFalse(result['partial'])

    def test_last_prefix_frame_stays_excluded(self):
        cap=self.capture();c.capture_boundary({'allowRendererDestroyedPrefix':True},cap)
        selected,_=c.select_frames(cap['frames'],model())
        self.assertNotIn(7,[x['frameIndex'] for x in selected['krakenAttack']])


if __name__=='__main__':unittest.main()
