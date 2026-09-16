import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from arrival_case import Arrival, terminal_capture, arguments


class Fake(Arrival):
    def __init__(self, root, ready_error=False, write_result=True, changed_session=False):
        self.root=root;self.output=root/'case';self.output.mkdir();(root/'model-test-output').mkdir()
        self.session='a'*32;self.case='case';self.journal=self.output/'journal.jsonl'
        self.a=SimpleNamespace(enemy='custom',level=0,room=2,renderer_path='body',profile_sha256='b'*64,capture_timeout=1)
        self.profile={'key':'custom'};self.calls=[];self.dungeon_id='DungeonEnum';self.ready_error=ready_error;self.write_result=write_result;self.changed_session=changed_session;self.ready_sent=False
        self.arm={'id':'c'*32,'captureId':'d'*32,'status':'enemy-arrival-armed','session':self.session,
                  'profile':self.profile,'catalogSha256':'b'*64,'pinnedReady':{'dungeonId':17,'level':0,'room':2,'queuedEnemy':'custom'},
                  'assets':{},'core':{},'helper':{},'content':{},'gameAssembly':{}}
    def log(self,*args):pass
    def check_inputs(self):
        if self.ready_sent and self.changed_session:raise RuntimeError('session changed')
    def ready_guard(self):self.check_inputs()
    def helper(self,op,payload=None):
        self.calls.append(op)
        if op=='enemy-arrival-arm':return self.arm
        if op=='ready':
            self.ready_sent=True
            if self.write_result:
                doc={'id':self.arm['captureId'],'session':self.session,'ok':False,'error':'Renderer destroyed during capture','frames':[{}],
                     'arrival':dict(self.arm,armCommandId=self.arm['id'])}
                (self.root/'model-test-output'/(self.arm['captureId']+'.json')).write_text(json.dumps(doc))
            if self.ready_error:raise TimeoutError('delivery uncertain')
        return {'ok':True}


class Tests(unittest.TestCase):
    def test_uncertain_ready_gathers_exact_pending_capture_and_never_retries(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve(),ready_error=True);_,out=r.run_arrival()
            self.assertTrue(out['terminal']);self.assertFalse(out['rawCaptureOk']);self.assertEqual(out['rawError'],'Renderer destroyed during capture')
            self.assertEqual(r.calls,['stage-next-enemy','fortify-party','enemy-arrival-arm','ready'])
    def test_missing_capture_not_terminal_and_path_preserved(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve(),write_result=False)
            with patch('arrival_case.time.monotonic',side_effect=[0,2]):_,out=r.run_arrival()
            self.assertEqual(out['status'],'observation_timeout_capture_pending');self.assertFalse(out['terminal']);self.assertTrue(out['rawCapturePath'].endswith('d'*32+'.json'))
    def test_late_result_observed_after_poll_without_new_helper(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve(),write_result=False,ready_error=True)
            def late(_):
                doc={'id':r.arm['captureId'],'session':r.session,'ok':True,'frames':[], 'arrival':dict(r.arm,armCommandId=r.arm['id'])}
                (r.root/'model-test-output'/(r.arm['captureId']+'.json')).write_text(json.dumps(doc))
            with patch('arrival_case.time.sleep',side_effect=late):_,out=r.run_arrival()
            self.assertTrue(out['terminal']);self.assertEqual(r.calls.count('ready'),1)
    def test_changed_session_refuses_fresh_observation_even_existing_result(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve(),changed_session=True);_,out=r.run_arrival()
            self.assertFalse(out['terminal']);self.assertIn('session changed',out['errors']);self.assertEqual(r.calls.count('ready'),1)
    def test_wrong_late_identity_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve());r.helper('ready');p=r.root/'model-test-output'/(r.arm['captureId']+'.json');d=json.loads(p.read_text());d['arrival']['armCommandId']='e'*32;p.write_text(json.dumps(d))
            with self.assertRaisesRegex(RuntimeError,'different arm'):terminal_capture(p,r.arm,r.session)
    def test_claim_prevents_second_sequence(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve());(r.root/'model-test-output'/('arrival-session-'+r.session+'.json')).write_text('{}');_,out=r.run_arrival()
            self.assertFalse(out['terminal']);self.assertEqual(r.calls,[])
    def test_stage_uncertain_stops_before_arm(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve())
            def uncertain(op,payload=None):r.calls.append(op);raise TimeoutError('uncertain stage')
            r.helper=uncertain;_,out=r.run_arrival();self.assertEqual(r.calls,['stage-next-enemy']);self.assertFalse(out['terminal'])
    def test_ready_guard_rejects_stair_and_changed_party(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve())
            r.state=lambda:{'inSession':True,'singlePlayer':True,'party':[{'fid':{'photonId':-1,'turnIndex':0},'classId':1,'hp':58}],
                            'dungeon':{'inDungeon':True,'dungeonId':'DungeonEnum','level':0,'room':2}}
            fixture={'identity':{'root':str(r.root),'session':r.session},'strictReady':{'ok':True,'level':0,'room':2},
                     'dungeon':{'level':0,'room':2,'slotAllowsEnemySubstitution':False,'queuedRoomType':'Stair'}}
            r.helper=lambda *args:fixture
            with self.assertRaisesRegex(RuntimeError,'eligible Enemy'):Arrival.ready_guard(r)
            fixture['dungeon'].update(slotAllowsEnemySubstitution=True,queuedRoomType='Enemy')
            Arrival.ready_guard(r)
            r.party=[{'fid':{'photonId':-1,'turnIndex':1},'classId':1}]
            with self.assertRaisesRegex(RuntimeError,'Party identity changed'):Arrival.ready_guard(r)
    def test_arm_rejection_never_sends_ready(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve());original=r.helper
            def helper(op,payload=None):
                if op=='enemy-arrival-arm':r.calls.append(op);raise RuntimeError('arm rejected')
                return original(op,payload)
            r.helper=helper;_,out=r.run_arrival()
            self.assertNotIn('ready',r.calls);self.assertFalse(out['terminal'])
    def test_late_capture_changed_catalog_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Fake(Path(temp).resolve());r.helper('ready');p=r.root/'model-test-output'/(r.arm['captureId']+'.json')
            d=json.loads(p.read_text());d['arrival']['catalogSha256']='f'*64;p.write_text(json.dumps(d))
            with self.assertRaisesRegex(RuntimeError,'arm pin changed'):terminal_capture(p,r.arm,r.session)

    def test_nan_inf_timeout_refused(self):
        for value in ['nan','inf','0','1801']:
            with self.assertRaises(SystemExit):arguments(['--root','/tmp/game','--port','8788','--session','a'*32,'--enemy','x','--renderer-path','body','--profile-sha256','b'*64,'--level','0','--room','2','--capture-timeout',value])

if __name__=='__main__':unittest.main()
