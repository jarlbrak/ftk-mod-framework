import copy,hashlib,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
from paid_focus_case import PaidFocusCase
sha=lambda v:hashlib.sha256(v).hexdigest()
class Native:
 def __init__(self,r,mode):self.r=r;self.mode=mode;self.now=0.;self.calls=[];self.seen=set();self.operation=None;self.reads=0
 def sleep(self,t):
  self.now+=5 if self.mode=='expired' else max(.1,t)
  p=self.r.root/'model-test-command.json'
  if not p.exists():return
  c=json.loads(p.read_text())
  if c['id'] in self.seen:return
  self.seen.add(c['id']);op=c['op'];self.calls.append(op)
  if self.mode=='uncertain' and op=='paid-focus-submit':return
  d={'ok':True,'id':c['id'],'session':self.r.session}
  if op=='paid-focus-state':
   identity=dict(root=str(self.r.root),session=self.r.session,level=0,room=1,turn=2,**{k:i+1 for i,k in enumerate(('heroId','dungeonId','esId','mcId','dummyId','celId','targetId','targetCelId','stanceId','buttonId','slotsId','hudId','weaponInstanceId'))})
   d.update(identity=identity,pins={'helper':{'assemblyFileSha256':sha(b'helper')},'core':{'assemblyFileSha256':sha(b'core')}},availableFocus=4,spentFocus=0,frame=100,animationCount=0,focusing=False,interrupt=False,scope=True,noModal=True,normalAttack=True,inputReady=True,canPay=True,stanceReady=True,alreadySubmitted=False,ticketId='b'*32)
   if self.operation is None:self.initial=copy.deepcopy(d)
   else:
    self.reads+=1;d.update(operation=copy.deepcopy(self.operation),availableFocus=3,spentFocus=1,frame=104+self.reads,alreadySubmitted=True)
    if self.mode=='timeout':d['operation']['status']='submitted'
    if self.mode=='animation' and self.reads==1:d['animationCount']=1
    if self.mode=='wrong-turn':d['identity']['turn']=3
    if self.mode=='wrong-submission':d['operation']['submissionId']='f'*32
    if self.mode=='refund':d.update(availableFocus=4,spentFocus=0)
    if self.mode=='delta':d['operation']['callbackAfter']['availableFocus']=2
    if self.mode=='baseline':d['operation']['callbackBefore']['availableFocus']=3
    if self.mode=='duplicate':d['operation']['callbackCount']=2
    if self.mode=='not-ready':d['stanceReady']=False
   if self.mode=='session':(self.r.root/'model-test-session.json').write_text(json.dumps({'session':'f'*32}))
  elif op=='paid-focus-submit':
   baseline=copy.deepcopy(self.initial);baseline['frame']=101;before=copy.deepcopy(baseline);before.update(focusing=True,animationCount=1,frame=102)
   after=copy.deepcopy(before);after.update(focusing=False,availableFocus=3,spentFocus=1,frame=103)
   self.operation=dict(submissionId=c['id'],status='payment-complete',claimRetained=True,error=None,callbackCount=1,callbackEvidenceTruncated=False,beforeSubmission=baseline,callbackBefore=before,callbackAfter=after,
    callbacks=[dict(index=1,previousStatus='submitted',classification='payment-complete',observationError=None,nativeException=None,before=copy.deepcopy(before),after=copy.deepcopy(after))])
   d['operation']=copy.deepcopy(self.operation)
   if self.mode=='rejected':d.update(ok=False);d['operation']['status']='uncertain-native-submission'
  else:raise AssertionError('Unexpected operation '+op)
  (self.r.root/'model-test-output'/(c['id']+'.json')).write_text(json.dumps(d))
class Tests(unittest.TestCase):
 def prepare(self,t,mode='paid'):
  root=Path(t).resolve()/'scratch/game';(root/'BepInEx/plugins').mkdir(parents=True);(root/'model-test-output').mkdir()
  for n,b in [('FTKModFramework.dll',b'core'),('FtkRuntimeModelTest.dll',b'helper'),('FtkRuntimeModelTestContent.dll',b'content')]:(root/'BepInEx/plugins'/n).write_bytes(b)
  (root/'model-test-session.json').write_text(json.dumps({'session':'a'*32}));(root/'model-test-profiles.json').write_bytes(b'{}')
  r=PaidFocusCase(SimpleNamespace(root=root,session='a'*32,level=0,room=1,catalog_sha256=sha(b'{}'),helper_sha256=sha(b'helper'),outcome_timeout=2))
  return r,Native(r,mode)
 def run_case(self,r,n):
  with patch('paid_focus_case.time.monotonic',side_effect=lambda:n.now),patch('paid_focus_case.time.sleep',side_effect=n.sleep):return r.run()[1]
 def test_payment_exact_native_callback_and_no_attack(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t);o=self.run_case(r,n);self.assertTrue(o['terminal']);self.assertEqual(n.calls,['paid-focus-state','paid-focus-submit','paid-focus-state']);self.assertFalse(o['attackSubmitted'])
 def test_waits_for_animation_count_zero(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'animation');o=self.run_case(r,n);self.assertTrue(o['terminal']);self.assertEqual(n.reads,2)
 def test_expired_ticket_no_native_submit(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'expired');o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls,['paid-focus-state'])
 def test_uncertain_submission_retains_exact_pending_path_no_followup(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'uncertain');o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls,['paid-focus-state','paid-focus-submit']);self.assertEqual(o['pending']['op'],'paid-focus-submit');self.assertTrue(o['pending']['resultPath'].endswith(o['pending']['id']+'.json'))
 def test_rejected_submission_no_readout_commands(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'rejected');o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls,['paid-focus-state','paid-focus-submit'])
 def test_timeout_is_nonterminal_pending(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'timeout');o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(o['status'],'observation_timeout_payment_pending');self.assertEqual(n.calls.count('paid-focus-submit'),1)
 def test_wrong_turn_submission_payment_or_readiness_stops(self):
  for mode in ['wrong-turn','wrong-submission','refund','delta','baseline','duplicate','not-ready']:
   with self.subTest(mode=mode),tempfile.TemporaryDirectory() as t:
    r,n=self.prepare(t,mode);o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls.count('paid-focus-submit'),1)
 def test_existing_native_turn_claim_blocks_second_submit(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t);self.assertTrue(self.run_case(r,n)['terminal']);second=PaidFocusCase(r.a);native=Native(second,'paid');o=self.run_case(second,native);self.assertFalse(o['terminal']);self.assertEqual(native.calls,['paid-focus-state'])
 def test_session_loss_stops_before_payment(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t,'session');o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls,['paid-focus-state'])
 def test_foreign_pending_no_command_replacement(self):
  with tempfile.TemporaryDirectory() as t:
   r,n=self.prepare(t);(r.root/'model-test-command.json').write_text(json.dumps({'id':'other','session':r.session,'op':'capture'}));o=self.run_case(r,n);self.assertFalse(o['terminal']);self.assertEqual(n.calls,[])
if __name__=='__main__':unittest.main()
