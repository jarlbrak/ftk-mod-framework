import argparse,copy,unittest
from unittest.mock import patch
import story_setup as s

def state(page=0,ready=True,done=False):
 focus=dict(instanceId=12,continuationId=13)
 panel=dict(focus=focus,componentId=8,panelId=9,hudActive=True,activeSelf=not done,activeInHierarchy=not done,fullyOpened=ready and not done,clickable=ready,closeOnOkay=True)
 confirm=dict(focus=focus,componentId=10,panelId=11,hudActive=True,activeSelf=False,activeInHierarchy=False,fullyOpened=False,clickable=False,samePresenterQuest=True,continuationPresent=True)
 return dict(root='/scratch/game',session='nonce',coreIdentity={'sha':'a'},inSession=True,livingOwnedParty=True,outsideCombat=True,outsideDungeon=True,
  coordinatorId=1,presenterId=2,heroInstanceId=3,messageId=-1 if done else 4,presenterMessageId=4,pageIndex=page,pageCount=2,questId=7,questPresent=True,questRegisteredReference=True,phase='DeliverStartQuestMsgPartClosed',contentSha256='a'*64,
  messagePresent=not done,messageClosed=False,messageType='None' if done else 'StoryQuestMessage',queueCount=0,currentContinuationPresent=not done,
  presenterFsms=[{'present':True,'enabled':not done} for _ in range(3)],portrait=panel,questConfirm=confirm,
  signals={'modalOpen':not done,'choiceOpen':False,'modalType':None if done else 'StoryQuestMessage','warnings':[]},
  complete=done,status='native-story-setup-complete' if done else 'native-story-page-actionable' if ready else 'native-story-pending',
  actionableSurface=None if done or not ready else 'portrait')
class Clock:
 def __init__(self):self.now=0
 def monotonic(self):return self.now
 def sleep(self,n):self.now+=n
class Tests(unittest.TestCase):
 def runner(self,states,uncertain=False):
  r=argparse.Namespace(a=argparse.Namespace(wait_timeout=4),log=lambda *a:None);r.calls=[];r.seen=0
  def helper(op,payload=None):
   r.calls.append((op,payload))
   if op=='story-state':
    x=copy.deepcopy(states[min(r.seen,len(states)-1)]);r.seen+=1;x['frame']=r.seen;r.latest=x;return x
   if uncertain:raise TimeoutError('uncertain native submission')
   return {'status':'native-story-page-submitted','surface':payload['surface'],'before':r.latest}
  r.helper=helper;return r
 def run_clear(self,r):
  clock=Clock()
  with patch.object(s,'time',clock):return s.clear(r)
 def test_full_sequence_pages_then_stable_complete(self):
  r=self.runner([state(),state(ready=False),state(1),state(1,ready=False),state(2,done=True)])
  self.assertTrue(self.run_clear(r)['complete']);self.assertEqual(len([c for c in r.calls if c[0]=='story-submit']),2)
 def test_no_repeat_even_if_same_page_reappears_actionable(self):
  r=self.runner([state(),state(ready=False),state()])
  with self.assertRaises(TimeoutError):self.run_clear(r)
  self.assertEqual(len([c for c in r.calls if c[0]=='story-submit']),1)
 def test_uncertain_action_once_no_observation_retry(self):
  r=self.runner([state()],True)
  with self.assertRaises(TimeoutError):self.run_clear(r)
  self.assertEqual([c[0] for c in r.calls],['story-state','story-submit'])
 def test_nonclickable_camera_pending_is_waited_not_operated(self):
  r=self.runner([state(ready=False),state(ready=False),state(2,done=True)])
  self.run_clear(r);self.assertNotIn('story-submit',[c[0] for c in r.calls])
 def test_unknownchoice_and_combat_stop_without_submission(self):
  for key,value in [('outsideCombat',False),('status','unsupported-choice'),('status','unsupported-modal')]:
   x=state();x[key]=value;r=self.runner([x])
   with self.assertRaises(RuntimeError):self.run_clear(r)
   self.assertNotIn('story-submit',[c[0] for c in r.calls])
 def test_changed_pin_stops(self):
  x=state(1);x['coreIdentity']={'sha':'changed'};r=self.runner([state(),x])
  with self.assertRaises(RuntimeError):self.run_clear(r)
  self.assertEqual(len([c for c in r.calls if c[0]=='story-submit']),1)
 def test_type_none_alone_not_completion(self):
  x=state();x['messageType']='None';x['complete']=True;x['signals'].update(modalOpen=False,choiceOpen=False)
  self.assertFalse(s.complete(x))
 def test_queue_continuation_fsm_and_panels_prevent_falsecompletion(self):
  for change in [lambda x:x.update(queueCount=1),lambda x:x.update(currentContinuationPresent=True),lambda x:x['presenterFsms'][0].update(enabled=True),lambda x:x['portrait'].update(activeSelf=True)]:
   x=state(done=True);change(x);self.assertFalse(s.complete(x))
 def test_negative_registered_native_quest_id(self):
  x=state();x['questId']=-1;self.assertEqual(s.submission(x)['questId'],-1)
  for key,value in [('questId',0),('questPresent',False),('questRegisteredReference',False)]:
   y=copy.deepcopy(x);y[key]=value
   with self.assertRaises(RuntimeError):s.submission(y)
 def test_final_start_page_confirm_advances_same_message(self):
  first=state(1);confirm=state(2);confirm['actionableSurface']='questConfirm';confirm['phase']='unavailable'
  r=self.runner([first,state(1,ready=False),confirm,state(2,done=True)])
  self.run_clear(r);self.assertEqual([c[1]['surface'] for c in r.calls if c[0]=='story-submit'],['portrait','questConfirm'])
 def test_multi_native_content_rebuild_and_subquest_callback(self):
  first=state(1);first['phase']='DeliverMultiQuestMsgClosed'
  transition=state(0,ready=False);transition.update(phase='unavailable',contentSha256='b'*64)
  sub=state(0);sub.update(phase='DeliverSubQuestMsgClosed',contentSha256='b'*64)
  r=self.runner([first,transition,sub,state(2,done=True)]);self.run_clear(r)
  self.assertEqual(len([c for c in r.calls if c[0]=='story-submit']),2)
 def test_reset_without_verified_subquest_phase_rejected(self):
  first=state(1);first['phase']='DeliverMultiQuestMsgClosed'
  for phase,content in [('DeliverMultiQuestMsgClosed','b'*64),('DeliverSubQuestMsgClosed','a'*64),('DeliverStartQuestMsgPartClosed','b'*64)]:
   next=state(0);next.update(phase=phase,contentSha256=content);r=self.runner([first,next])
   with self.assertRaises(RuntimeError):self.run_clear(r)
   self.assertEqual(len([c for c in r.calls if c[0]=='story-submit']),1)
 def test_poststage_pending_no_unknown_dismissal(self):
  self.assertEqual(s.staging_status(state()),'pending_story_message');self.assertEqual(s.staging_status(state(done=True)),'binding_metadata_observed')
  for signals in [{},{'modalOpen':True,'choiceOpen':True,'modalType':'StoryQuestMessage'},{'modalOpen':True,'choiceOpen':False,'modalType':'GlobalMessage'}]:
   with self.assertRaises(RuntimeError):s.staging_status({'signals':signals})
if __name__=='__main__':unittest.main()
