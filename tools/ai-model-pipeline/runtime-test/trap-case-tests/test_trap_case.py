import hashlib,json,sys,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from trap_case import TrapCase


class NativeFiles:
    def __init__(self,root,helper_sha,mode='ready'):
        self.root=root;self.sha=helper_sha;self.mode=mode;self.now=0.;self.calls=[];self.seen=set()
    def sleep(self,seconds):
        self.now+=5 if self.mode=='expired' else max(seconds,.1)
        path=self.root/'model-test-command.json'
        if not path.exists():return
        command=json.loads(path.read_text());op=command['op']
        if command['id'] in self.seen:return
        self.seen.add(command['id']);self.calls.append(op)
        if self.mode=='uncertain' and op=='trap-submit':return
        result={'id':command['id'],'session':command['session'],'ok':True}
        if op=='trap-state':
            identity={'root':str(self.root),'session':command['session'],'level':0,'room':3,'heroId':1,'dungeonId':2,'trapId':3,
                      'pins':{'helper':{'assemblyFileSha256':self.sha},'core':{'assemblyFileSha256':hashlib.sha256(b'core').hexdigest()}}}
            result.update(identity=identity,ticketId='b'*32,alreadySubmitted=False,scope=True,noModal=True,nativeTrap=True,heroQueue=True,nativeVote=True,trapActive=True,buttons={'Proceed':{'usable':True}})
        elif op=='trap-submit':result.update(status='submitted')
        elif op=='fixture-state':
            room=8 if self.mode=='room-change' else 4
            result.update(voteSurfaces=[{'heroInstanceId':1,'alive':True}],identity={'root':str(self.root),'session':command['session']},dungeon={'level':0,'room':room},strictReady={'ok':self.mode!='timeout','level':0,'room':room})
        (self.root/'model-test-output'/(command['id']+'.json')).write_text(json.dumps(result))


class Tests(unittest.TestCase):
    def prepare(self,temp,mode):
        root=Path(temp).resolve()/'scratch'/'game';(root/'BepInEx/plugins').mkdir(parents=True);(root/'model-test-output').mkdir()
        (root/'BepInEx/plugins/FTKModFramework.dll').write_bytes(b'core');(root/'BepInEx/plugins/FtkRuntimeModelTest.dll').write_bytes(b'helper')
        (root/'BepInEx/plugins/FtkRuntimeModelTestContent.dll').write_bytes(b'content')
        (root/'model-test-session.json').write_text(json.dumps({'session':'a'*32}));(root/'model-test-profiles.json').write_text('{}')
        sha=hashlib.sha256(b'helper').hexdigest()
        args=SimpleNamespace(root=root,session='a'*32,option='Proceed',level=0,room=3,catalog_sha256=hashlib.sha256(b'{}').hexdigest(),helper_sha256=sha,outcome_timeout=2)
        return TrapCase(args),NativeFiles(root,sha,mode)
    def execute(self,r,native):
        with patch('trap_case.time.monotonic',side_effect=lambda:native.now),patch('trap_case.time.sleep',side_effect=native.sleep):return r.run()[1]
    def test_exact_once_native_choice_then_observed_ready(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'ready');out=self.execute(r,n)
            self.assertEqual(n.calls,['trap-state','trap-submit','fixture-state']);self.assertTrue(out['terminal'])
            requests=list(r.output.glob('*-request.json'));self.assertEqual(len(requests),3)
            submit=next(json.loads(p.read_text()) for p in requests if json.loads(p.read_text())['op']=='trap-submit')
            self.assertEqual(submit['ticketId'],'b'*32);self.assertEqual(submit['identity']['room'],3)
    def test_expired_ticket_never_submits(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'expired');out=self.execute(r,n);self.assertEqual(n.calls,['trap-state']);self.assertFalse(out['terminal'])
    def test_uncertain_submit_no_retry_or_later_helper(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'uncertain');out=self.execute(r,n)
            self.assertEqual(n.calls,['trap-state','trap-submit']);self.assertEqual(out['pending']['op'],'trap-submit');self.assertFalse(out['terminal'])
            self.assertTrue(out['pending']['resultPath'].endswith(out['pending']['id']+'.json'))
    def test_wrong_room_stops_without_more_action(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'room-change');out=self.execute(r,n);self.assertFalse(out['terminal']);self.assertEqual(n.calls.count('trap-submit'),1)
    def test_outcome_timeout_is_pending_not_failure(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'timeout');out=self.execute(r,n);self.assertEqual(out['status'],'observation_timeout_outcome_pending');self.assertFalse(out['terminal']);self.assertEqual(n.calls.count('trap-submit'),1)
    def test_claim_blocks_changed_option(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'ready');self.execute(r,n)
            a=r.a;a.option='Disarm';second=TrapCase(a);n.calls=[];out=self.execute(second,n)
            self.assertEqual(n.calls,[]);self.assertFalse(out['terminal'])
    def test_helper_rejection_preserved_no_outcome_poll(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'ready');original=n.sleep
            def reject(seconds):
                original(seconds)
                command=json.loads((r.root/'model-test-command.json').read_text())
                if command['op']=='trap-submit':
                    path=r.root/'model-test-output'/(command['id']+'.json');doc=json.loads(path.read_text());doc.update(ok=False,status='uncertain-native-submission',error='native failure');path.write_text(json.dumps(doc))
            n.sleep=reject;out=self.execute(r,n);self.assertFalse(out['terminal']);self.assertEqual(n.calls,['trap-state','trap-submit']);self.assertEqual(out['submission']['error'],'native failure')
    def test_session_change_before_response_no_further_command(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'ready');original=n.sleep
            def changed(seconds):
                original(seconds)
                (r.root/'model-test-session.json').write_text(json.dumps({'session':'f'*32}))
            n.sleep=changed;out=self.execute(r,n);self.assertFalse(out['terminal']);self.assertEqual(n.calls,['trap-state']);self.assertEqual(out['pending']['op'],'trap-state')

    def test_foreign_pending_blocks_even_state(self):
        with tempfile.TemporaryDirectory() as temp:
            r,n=self.prepare(temp,'ready');(r.root/'model-test-command.json').write_text(json.dumps({'id':'foreign','session':r.session,'op':'capture'}))
            out=self.execute(r,n);self.assertEqual(n.calls,[]);self.assertFalse(out['terminal'])

if __name__=='__main__':unittest.main()
