"""Recorded native Q1/Q2 snapshots + hostile variants; no transport/game actions."""
import copy,json,unittest
from pathlib import Path
from unittest.mock import patch
import entry_setup as entry
from test_entry_setup import Runner
from test_story_setup import Clock


def fixture(name):return json.loads((Path(__file__).parent/'fixtures'/name).read_text())

class Tests(unittest.TestCase):
    def run_wait(self,runner):
        with patch.object(entry,'time',Clock()):return entry.wait_position_eligible(runner)
    def test_actual_q1_quiet_gap_then_q2_page_then_eligible(self):
        first=fixture('q1-quiet.json');gap=copy.deepcopy(first);gap['story']['frame']+=1
        runner=Runner([first,gap,fixture('q2-story.json'),fixture('q2-eligible.json')])
        state,pins,pages=self.run_wait(runner)
        self.assertTrue(state['positionEligible'])
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state','entry-preparation-state','entry-preparation-state','story-submit','entry-preparation-state'])
        self.assertEqual(next(args['questId'] for op,args in runner.calls if op=='story-submit'),-10)
        self.assertNotIn('entry-position',[op for op,args in runner.calls])
    def test_unknown_quest_no_story_or_entry_submission(self):
        state=fixture('q2-story.json');state['currentQuestDefinition']='unreviewed'
        runner=Runner([state])
        with self.assertRaisesRegex(RuntimeError,'Unexpected'):self.run_wait(runner)
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state'])
    def test_combat_dungeon_or_unknown_modal_no_submission(self):
        for key,value in [('outsideCombat',False),('outsideDungeon',False),('status','unsupported-choice')]:
            state=fixture('q2-story.json');state['story'][key]=value;runner=Runner([state])
            with self.assertRaises(RuntimeError):self.run_wait(runner)
            self.assertEqual([op for op,args in runner.calls],['entry-preparation-state'])
    def test_wrong_presenter_quest_no_submission(self):
        state=fixture('q2-story.json');state['story']['questId']=-1;runner=Runner([state])
        with self.assertRaisesRegex(RuntimeError,'Presenter'):self.run_wait(runner)
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state'])
    def test_uncertain_story_does_not_retry_or_position(self):
        runner=Runner([fixture('q2-story.json')],uncertain='story-submit')
        with self.assertRaises(TimeoutError):self.run_wait(runner)
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state','story-submit'])
    def test_quiet_q1_timeout_keeps_entry_unsubmitted(self):
        first=fixture('q1-quiet.json')
        def states():
            for index in range(20):
                state=copy.deepcopy(first);state['story']['frame']+=index;yield state
        runner=Runner(states())
        with self.assertRaisesRegex(TimeoutError,'no entry mutation'):self.run_wait(runner)
        self.assertTrue(all(op=='entry-preparation-state' for op,args in runner.calls))
    def test_q2_regression_and_owner_change_stop(self):
        first=fixture('q2-story.json');regress=fixture('q1-quiet.json');regress['story']['frame']=first['story']['frame']+1
        for change in [regress,dict(fixture('q2-eligible.json'),heroInstanceId=123)]:
            runner=Runner([first,change])
            with self.assertRaises(RuntimeError):self.run_wait(runner)
            self.assertNotIn('entry-position',[op for op,args in runner.calls])
    def test_actionable_unknown_destination_stops_before_page(self):
        state=fixture('q2-story.json');state['expectedQuestDestination']=False;runner=Runner([state])
        with self.assertRaisesRegex(RuntimeError,'destination required'):self.run_wait(runner)
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state'])
    def test_quiet_pending_destination_waits_without_mutation(self):
        state=fixture('q2-eligible.json');state['expectedQuestDestination']=False;state['positionEligible']=False
        ready=fixture('q2-eligible.json');ready['story']['frame']+=1;runner=Runner([state,ready])
        result,_,_=self.run_wait(runner);self.assertTrue(result['positionEligible'])
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state','entry-preparation-state'])
    def test_changed_q2_instance_id_stops_before_position(self):
        changed=fixture('q2-eligible.json');changed['currentQuestId']=-11
        runner=Runner([fixture('q2-story.json'),changed])
        with self.assertRaisesRegex(RuntimeError,'quest instance changed'):self.run_wait(runner)
        self.assertNotIn('entry-position',[op for op,args in runner.calls])

    def test_visit_page_not_replayed(self):
        state=fixture('q1-quiet.json');state['story']['actionableSurface']='portrait';runner=Runner([state])
        with self.assertRaisesRegex(RuntimeError,'Visit story reopened'):self.run_wait(runner)
        self.assertEqual([op for op,args in runner.calls],['entry-preparation-state'])

if __name__=='__main__':unittest.main()
