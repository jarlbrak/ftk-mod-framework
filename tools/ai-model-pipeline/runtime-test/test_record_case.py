import copy
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch
from record_case import Recorder, guard, action_request, verify_action, kill_fixture_handoff


def state():
    return {'singlePlayer':True,'inSession':True,'party':[{'hp':100,'fid':{'photonId':1,'turnIndex':0}}],
        'signals':{'modalOpen':False,'choiceOpen':False,'allDead':False,'victoryShowing':False},
        'combat':{'active':True,'heroTurnReady':True,'enemies':[{'type':'probe','alive':True,'fid':{'photonId':-1,'turnIndex':0}}],
                  'readyParts':{'initialized':True,'actingFid':{'photonId':1,'turnIndex':0}},'whoseTurn':{'isPlayer':True}}}


class RecordingTests(unittest.TestCase):
    def test_terminal_death_summary_preserves_raw_partial_rejection(self):
        with tempfile.TemporaryDirectory() as folder:
            runner=object.__new__(Recorder);runner.a=SimpleNamespace(action='kill-fixture',capture_timeout=1)
            runner.output=Path(folder);runner.capture_path=Path(folder)/'capture.json';runner.capture_id='capture';runner.session='s';runner.capture_scope='enemies'
            runner.log=Mock();runner.check_inputs=Mock()
            renderer=dict(instanceId=3,ownerInstanceId=4,celInstanceId=5,mesh='mesh',boneSignature='sig')
            frames=[dict(renderer,frame=20+i,gameSeconds=i/12,timeScale=1,captureFramerate=12,ownerKind='enemies',motionAnimator={'animatorInstanceId':6}) for i in range(3)]
            runner.capture_path.with_suffix('').mkdir()
            for i in range(3):(runner.capture_path.with_suffix('')/f'{i:04d}.png').write_bytes(b'\x89PNG\r\n\x1a\nexample')
            identity=dict(targetRendererInstanceId=3,targetOwnerInstanceId=4,targetCelInstanceId=5,targetAnimatorInstanceId=6,targetFid={'photonId':-1,'turnIndex':0})
            event=dict(identity,nativeMethod='CharacterEventListener.CombatTrigger entry',trigger='Death',finalized=True,exception=None,frame=21,realtime=1.)
            motion=dict(identity,schema='ftkmf.native-combat-motion.v1',error=None,events=[event],eventCount=1,
                startedFrame=20,startedRealtime=.5,baseline=dict(animatorInstanceId=6,baseLayerIdle=True,baseLayerTransition=False,animatorEnabled=True,animatorActive=True),
                termination=dict(reason='controller-unresolved-after-observed-native-death',controllerError='Avatar controller ambiguous or absent.',terminalFrameCaptured=False,frame=23,realtime=2.))
            doc=dict(ok=False,error='Controller resolution unavailable after observed native Death.',id='capture',session='s',
                     scope='enemies',ownerInstanceId=4,celInstanceId=5,frame=24,requestedSeconds=10,requestedFps=12,fixedStep=True,frames=frames,motionObservation=motion)
            runner.capture_path.write_text(json.dumps(doc))
            with self.assertRaisesRegex(RuntimeError,'Partial/paused/changed capture'):runner.collect_capture(renderer)
            summary=json.loads((Path(folder)/'capture-summary.json').read_text())
            self.assertFalse(summary['captureReportedOk']);self.assertFalse(summary['captureBoundary']['completeCapture'])
            self.assertEqual(summary['captureBoundary']['termination'],'controller_unresolved_after_native_death')
            self.assertEqual(json.loads(runner.capture_path.read_text()),doc)

    def test_guard_fails_closed(self):
        for key,value in [('singlePlayer',False),('inSession',False),('party',[])]:
            s=state();s[key]=value
            with self.assertRaises(RuntimeError):guard(s,'probe')
        for key in ('modalOpen','choiceOpen','allDead','victoryShowing'):
            s=state();s['signals'][key]=True
            with self.assertRaises(RuntimeError):guard(s,'probe')
        s=state();s['combat']['heroTurnReady']=False
        with self.assertRaises(RuntimeError):guard(s,'probe')
        with self.assertRaises(RuntimeError):guard(state(),'other')

    def test_profile_hash_change_stops_before_transport(self):
        runner=object.__new__(Recorder);runner.root=Path('/unused');runner.a=SimpleNamespace(profile_sha256='expected')
        runner.check_session=Mock();runner.asset_hashes={}
        with patch('record_case.digest',return_value='changed'):
            with self.assertRaises(RuntimeError):runner.check_inputs()

    def test_stale_nonce_stops_before_hash_read(self):
        runner=object.__new__(Recorder);runner.check_session=Mock(side_effect=RuntimeError('stale nonce'))
        with patch('record_case.digest') as hashed:
            with self.assertRaises(RuntimeError):runner.check_inputs()
            hashed.assert_not_called()

    def test_invalid_timeouts_rejected_before_filesystem(self):
        for value in (float('nan'),float('inf'),float('-inf'),0,-1):
            for field in ('operation_timeout','capture_timeout'):
                args=SimpleNamespace(port=8788,operation_timeout=40,capture_timeout=180)
                setattr(args,field,value)
                with self.subTest(field=field,value=value):
                    with self.assertRaises(ValueError):Recorder(args)

    def test_action_target_must_match(self):
        fid={'photonId':-1,'turnIndex':0}
        verify_action('attack',{'ok':True,'result':{'committed':'Attack','target':fid}},fid)
        for target in (None,{'photonId':-2,'turnIndex':0}):
            with self.assertRaises(RuntimeError):
                verify_action('attack',{'ok':True,'result':{'committed':'Attack','target':target}},fid)
        self.assertEqual(action_request('pass',fid),('end_turn',{}))

    def test_explicit_action_mapping(self):
        fid={'photonId':-1,'turnIndex':0}
        self.assertEqual(action_request('attack',fid),('combat_turn',{'cheat':'None','focus':False,'targetFid':fid}))
        self.assertEqual(action_request('attack',fid,True),('combat_turn',{'cheat':'None','focus':True,'targetFid':fid}))
        self.assertEqual(action_request('kill-fixture')[1]['cheat'],'KillSingle')
        verify_action('pass',{'ok':True,'result':{'ended':'combat'}})
        verify_action('attack',{'ok':True,'result':{'committed':'Attack(focus)','target':fid}},fid,True)
        with self.assertRaises(RuntimeError):verify_action('attack',{'ok':True,'result':{'committed':'KillSingle'}})
        with self.assertRaises(RuntimeError):verify_action('attack',{'ok':True,'result':{'committed':'Attack','target':fid}},fid,True)

    def test_motion_evidence_capture_request_is_opt_in(self):
        renderer={'ownerInstanceId':1,'instanceId':2,'celInstanceId':3,'rendererPath':'body',
                  'celRelativeRendererPath':'body','mesh':'ftkmf_glb_probe.glb','boneSignature':'sig'}
        with tempfile.TemporaryDirectory() as folder:
            runner=object.__new__(Recorder);runner.root=Path(folder);runner.session='s';runner.a=SimpleNamespace(motion_evidence=True)
            runner.profile={'renderers':[]};runner.capture_scope='enemies';runner.check_inputs=Mock();runner.log=Mock()
            runner.begin_capture(renderer)
            command=json.loads((Path(folder)/'model-test-command.json').read_text())
            self.assertTrue(command['motionObservation'])
            self.assertEqual(command['op'],'capture')
        with tempfile.TemporaryDirectory() as folder:
            runner=object.__new__(Recorder);runner.root=Path(folder);runner.session='s';runner.a=SimpleNamespace(motion_evidence=False)
            runner.profile={'renderers':[]};runner.capture_scope='enemies';runner.check_inputs=Mock();runner.log=Mock()
            runner.begin_capture(renderer)
            command=json.loads((Path(folder)/'model-test-command.json').read_text())
            self.assertNotIn('motionObservation',command)

    def test_kill_fixture_handoff_identifies_native_loot_transition(self):
        s=state();combat=s['combat']
        combat.update({'heroTurnReady':False,'liveEnemies':0,'winningPlayerFid':{'photonId':1,'turnIndex':0},'stuck':False})
        combat['enemies'][0].update({'hp':0,'alive':False})
        handoff=kill_fixture_handoff(s,{'photonId':-1,'turnIndex':0})
        self.assertEqual(handoff['status'],'victory_pending_native_loot')
        self.assertTrue(handoff['combatActive'])
        self.assertFalse(handoff['targetAlive'])

    def test_kill_fixture_handoff_keeps_real_stuck_signature_distinct(self):
        s=state();combat=s['combat']
        combat.update({'heroTurnReady':False,'liveEnemies':1,'stuck':True})
        combat['enemies'][0].update({'hp':0,'alive':True})
        self.assertEqual(kill_fixture_handoff(s,{'photonId':-1,'turnIndex':0})['status'],'stuck_signature')

    def run_mock(self,folder,action_error=None,first_error=None,capture_error=None,changed=False,action='attack',final_state=None):
        runner=object.__new__(Recorder);runner.a=SimpleNamespace(enemy='probe',renderer_path='body',action=action)
        runner.output=Path(folder);runner.profile={'renderers':[{'rendererPath':'body','glbFile':'probe.glb'}]}
        renderer={'ownerInstanceId':1,'instanceId':2,'celInstanceId':3,'celRelativeRendererPath':'body','rendererPath':'body',
                  'mesh':'ftkmf_glb_probe.glb','boneSignature':'sig','active':True,'enabled':True}
        runner.check_inputs=Mock();runner.log=Mock();runner.helper=Mock(return_value={'renderers':[renderer]})
        fresh=state()
        if changed:fresh['combat']['enemies'][0]['fid']['photonId']=-2
        runner.state=Mock(side_effect=[state(),fresh,final_state if final_state is not None else state()])
        def begin(_):runner.capture_id='capture';runner.capture_path=Path(folder)/'pending.json'
        runner.begin_capture=Mock(side_effect=begin);runner.wait_first_frame=Mock(side_effect=first_error)
        committed='KillSingle' if action=='kill-fixture' else 'Attack'
        runner.action=Mock(side_effect=action_error,return_value={'ok':True,'result':{'committed':committed,'target':{'photonId':-1,'turnIndex':0}}})
        runner.collect_capture=Mock(side_effect=capture_error,return_value={'frames':120})
        return runner,runner.record()

    def test_uncertain_action_never_retried_preserves_capture(self):
        with tempfile.TemporaryDirectory() as folder:
            runner,result=self.run_mock(folder,action_error=TimeoutError('HTTP execution uncertain'))
            self.assertFalse(result['ok']);self.assertEqual(runner.action.call_count,1)
            self.assertEqual(runner.collect_capture.call_count,1);self.assertEqual(result['capture']['frames'],120)
            self.assertTrue((Path(folder)/'result.json').exists())

    def test_changed_identity_withholds_action(self):
        with tempfile.TemporaryDirectory() as folder:
            runner,result=self.run_mock(folder,changed=True)
            self.assertFalse(result['ok']);runner.action.assert_not_called()

    def test_missing_first_png_withholds_action(self):
        with tempfile.TemporaryDirectory() as folder:
            runner,result=self.run_mock(folder,first_error=TimeoutError('no frame'))
            self.assertFalse(result['ok']);runner.action.assert_not_called();runner.collect_capture.assert_called_once()

    def test_partial_capture_is_not_success(self):
        with tempfile.TemporaryDirectory() as folder:
            runner,result=self.run_mock(folder,capture_error=RuntimeError('partial capture'))
            self.assertFalse(result['ok']);self.assertEqual(runner.action.call_count,1)
            self.assertEqual(result['status'],'partial_or_uncertain')

    def test_happy_capture_is_recorded_not_visual_verdict(self):
        with tempfile.TemporaryDirectory() as folder:
            runner,result=self.run_mock(folder)
            self.assertTrue(result['ok']);self.assertIn('No visual acceptance',result['note'])

    def test_kill_fixture_records_loot_handoff_without_a_second_action(self):
        with tempfile.TemporaryDirectory() as folder:
            final=state();combat=final['combat']
            combat.update({'heroTurnReady':False,'liveEnemies':0,'winningPlayerFid':{'photonId':1,'turnIndex':0},'stuck':False})
            combat['enemies'][0].update({'hp':0,'alive':False})
            runner,result=self.run_mock(folder,action='kill-fixture',final_state=final)
            self.assertTrue(result['ok'])
            self.assertEqual(result['killFixtureHandoff']['status'],'victory_pending_native_loot')
            runner.action.assert_called_once_with('combat_turn',{'cheat':'KillSingle','focus':False,'targetFid':{'photonId':-1,'turnIndex':0}})
            self.assertIn('awaiting loot',result['note'])


if __name__=='__main__':unittest.main()
