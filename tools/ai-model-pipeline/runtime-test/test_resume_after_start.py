"""Offline-only checkpoint and no-retry continuation regressions."""
import copy
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
import resume_after_start as resume


def event(kind, data): return {'kind':kind,'data':data}
def journal():
    return [event('deployed-binaries',{}),event('provenance',{'mode':'new-run','class':'owned'}),
            event('fresh-process-claim',{}),event('http-request',{'id':'once','path':'/action','payload':{'action':'start_run','args':{'adventure':'HollowMire','party':1,'class':'owned'}}}),
            event('http-result',{'id':'once','result':{'ok':True,'result':{'phase':'starting'}}}),
            event('http-request',{'id':'read','path':'/state','payload':None}),event('stopped',{})]

def fake():
    state={'singlePlayer':True,'inSession':True,'party':[{'hp':38}], 'dungeon':{'inDungeon':False},'combat':{'active':False}}
    runner=Mock();runner.a=SimpleNamespace(enemy='enemy');runner.session='session';runner.journal='journal';runner.profile={}
    runner.wait.return_value=copy.deepcopy(state);runner.state.return_value=copy.deepcopy(state)
    runner.wait_for_encounter.return_value=None
    return runner

class ResumeTests(unittest.TestCase):
    def test_checkpoint_accepts_completed_start_then_failed_read(self): resume.checkpoint(journal())
    def test_uncertain_start_rejected(self):
        entries=journal();entries.pop(4)
        with self.assertRaises(ValueError):resume.checkpoint(entries)
    def test_failed_start_rejected(self):
        entries=journal();entries[4]['data']['result']['ok']=False
        with self.assertRaises(ValueError):resume.checkpoint(entries)
    def test_later_action_or_helper_rejected(self):
        for later in [event('helper-request',{'op':'quiet-tutorials'}),event('http-request',{'path':'/action','payload':{'action':'enter_dungeon'}})]:
            entries=journal();entries.insert(-1,later)
            with self.assertRaises(ValueError):resume.checkpoint(entries)
    def test_duplicate_start_rejected(self):
        entries=journal();entries.insert(-1,entries[3])
        with self.assertRaises(ValueError):resume.checkpoint(entries)
    def test_configuration_change_rejected(self):
        entries=journal();entries[3]['data']['payload']['args']['party']=2
        with self.assertRaises(ValueError):resume.checkpoint(entries)
    def test_existing_combat_rejects_before_mutation(self):
        runner=fake();runner.wait.return_value['combat']['active']=True
        with self.assertRaises(ValueError):resume.continue_unsent(runner)
        runner.helper.assert_not_called();runner.action.assert_not_called()
    def test_entry_uncertain_is_once_and_does_not_stage(self):
        runner=fake();runner.action.side_effect=TimeoutError('uncertain')
        with self.assertRaises(TimeoutError):resume.continue_unsent(runner)
        self.assertEqual(runner.action.call_count,1)
        self.assertEqual([c.args[0] for c in runner.helper.call_args_list],['quiet-tutorials','fortify-party'])

    def test_unfinished_native_preparation_prevents_resumed_entry(self):
        runner=fake();runner.prepare_entry.side_effect=TimeoutError('native discovery pending')
        with self.assertRaises(TimeoutError):resume.continue_unsent(runner)
        runner.action.assert_not_called()
        self.assertEqual([c.args[0] for c in runner.helper.call_args_list],['quiet-tutorials','fortify-party'])
    def test_success_never_restarts_and_stages_immediately(self):
        runner=fake();runner.staging_result.return_value={'status':'binding_metadata_observed'}
        with patch.object(resume,'validate_inventory',return_value=['match']):
            result=resume.continue_unsent(runner)
        self.assertEqual(result['status'],'binding_metadata_observed')
        runner.action.assert_called_once_with('enter_dungeon',{'dungeonId':'FloodedCrypt'})
        calls=runner.mock_calls;index=next(i for i,c in enumerate(calls) if c[0]=='action')
        self.assertEqual(calls[index+1][0],'helper');self.assertEqual(calls[index+1].args[0],'stage-enemy')
    def test_wait_resume_label_never_uses_startup_log(self):
        runner=fake()
        with patch.object(resume,'validate_inventory',return_value=[]):resume.continue_unsent(runner)
        self.assertNotEqual(runner.wait.call_args_list[0].args[1],'actual living party')

    def test_post_stage_story_preserves_pending_without_inventory(self):
        runner=fake();pending={'status':'pending_story_message','matches':[]}
        runner.wait_for_encounter.return_value=pending
        self.assertIs(resume.continue_unsent(runner),pending)
        runner.action.assert_called_once_with('enter_dungeon',{'dungeonId':'FloodedCrypt'})
        self.assertEqual([c.args[0] for c in runner.helper.call_args_list],
                         ['quiet-tutorials','fortify-party','stage-enemy'])
        runner.staging_result.assert_not_called()

if __name__=='__main__':unittest.main()
