"""Offline native-entry orchestration: observe pages while discovery is pending."""
import argparse
import copy
import unittest
from unittest.mock import patch
import entry_setup as entry
from test_story_setup import state as story_state, Clock

KEY='a'*32

def observation(frame=1,phase=None,done=True,ready=False):
    story=story_state(done=done);story['frame']=frame
    result=dict(zip(entry.PINS,range(1,6)))
    result.update(story=story,positionEligible=True,discoveryEligible=done,
                  atTargetHex=True,expectedQuestRegistered=ready,
                  expectedQuestDefinition=ready,expectedQuestDestination=ready,ticket=None)
    if phase:
        discovered=phase.startswith('discover')
        result['ticket']={'ticketId':KEY,'valid':True,'phase':phase,'discoverSubmitted':discovered,
            'callbackCount':1 if ready else 0,'sameStoredCheckContinuation':discovered,
            'continuation':{'id':12,'callerId':1,'waitCount':0 if ready else 1,'local':True,'waitClients':'Self'},
            'entryReady':ready}
    return result

class Runner:
    def __init__(self,states,uncertain=None):
        self.states=iter(states);self.latest=None;self.calls=[];self.uncertain=uncertain
        self.a=argparse.Namespace(wait_timeout=2)
    def log(self,*args):pass
    def helper(self,op,args=None):
        self.calls.append((op,args))
        if op==self.uncertain:raise TimeoutError('uncertain')
        if op=='entry-preparation-state':
            self.latest=copy.deepcopy(next(self.states));return self.latest
        if op=='entry-position':return {'status':'native-position-submitted','ticketId':KEY,'before':self.latest}
        if op=='entry-discover':return {'status':'native-discovery-submitted','before':self.latest}
        if op=='story-submit':return {'status':'native-story-page-submitted','surface':args['surface'],'before':self.latest['story']}
        raise AssertionError(op)

class EntryTests(unittest.TestCase):
    def run_prepare(self,runner):
        with patch.object(entry,'time',Clock()):return entry.prepare(runner)

    def test_story_page_serviced_while_discovery_pending_then_fresh_verify(self):
        states=[observation(),observation(2,'position-submitted'),
                observation(3,'discover-submitted',done=False),
                observation(4,'discover-submitted',ready=True),observation(5,'discover-submitted',ready=True)]
        runner=Runner(states);prepared=self.run_prepare(runner);entry.verify(runner,prepared)
        ops=[c[0] for c in runner.calls]
        self.assertEqual(ops.count('entry-position'),1);self.assertEqual(ops.count('entry-discover'),1)
        self.assertEqual(ops.count('story-submit'),1)
        self.assertLess(ops.index('entry-discover'),ops.index('story-submit'))
        self.assertNotIn('enter_dungeon',ops)

    def test_position_story_completes_before_discovery(self):
        runner=Runner([observation(),observation(2,'position-submitted',done=False),
                       observation(3,'position-submitted'),observation(4,'discover-submitted',ready=True)])
        self.run_prepare(runner);ops=[c[0] for c in runner.calls]
        self.assertLess(ops.index('story-submit'),ops.index('entry-discover'))

    def test_uncertain_mutations_never_retried(self):
        for op in ['entry-position','entry-discover','story-submit']:
            runner=Runner([observation(),observation(2,'position-submitted'),
                           observation(3,'discover-submitted',done=False)],uncertain=op)
            with self.assertRaises(TimeoutError):self.run_prepare(runner)
            self.assertEqual([c[0] for c in runner.calls].count(op),1)

    def test_initial_ineligible_or_existing_ticket_no_mutation(self):
        for change in [{'positionEligible':False},{'ticket':{}},{'heroInstanceId':0}]:
            initial=observation();initial.update(change);runner=Runner([initial])
            with self.assertRaises(RuntimeError):self.run_prepare(runner)
            self.assertEqual([c[0] for c in runner.calls],['entry-preparation-state'])

    def test_changed_pins_or_uncertain_ticket_stop_before_discovery(self):
        for change in [{'heroInstanceId':99},{'ticket':{'ticketId':KEY,'valid':False,'phase':'position-uncertain'}}]:
            current=observation(2,'position-submitted');current.update(change);runner=Runner([observation(),current])
            with self.assertRaises(RuntimeError):self.run_prepare(runner)
            self.assertNotIn('entry-discover',[c[0] for c in runner.calls])

    def test_ready_requires_actual_quest_and_callback_completion(self):
        for key in ['atTargetHex','expectedQuestRegistered','expectedQuestDefinition','expectedQuestDestination']:
            current=observation(4,'discover-submitted',ready=True);current[key]=False
            self.assertFalse(entry.ready(current,KEY))
        for key,value in [('callbackCount',0),('callbackCount',2),('sameStoredCheckContinuation',False)]:
            current=observation(4,'discover-submitted',ready=True);current['ticket'][key]=value
            self.assertFalse(entry.ready(current,KEY))

    def test_changed_core_stops_story_and_discovery(self):
        current=observation(2,'position-submitted');current['story']['coreIdentity']={'sha':'different'}
        runner=Runner([observation(),current])
        with self.assertRaises(RuntimeError):self.run_prepare(runner)
        self.assertNotIn('entry-discover',[c[0] for c in runner.calls])

    def test_verify_requires_new_frame_and_remaining_readiness(self):
        for state in [observation(3,'discover-submitted',ready=True),observation(4,'discover-submitted')]:
            runner=Runner([observation(),observation(2,'position-submitted'),
                           observation(3,'discover-submitted',ready=True),state])
            prepared=self.run_prepare(runner)
            with self.assertRaises(RuntimeError):entry.verify(runner,prepared)

if __name__=='__main__':unittest.main()
