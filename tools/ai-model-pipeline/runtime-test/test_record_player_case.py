import copy
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock
from record_player_case import PlayerRecorder,player_guard,select_player_renderer,player_assignments,player_outfit_coverage,validate_player_registration_assignments
from test_record_case import state

PROFILE={'renderers':[{'rendererPath':'body','glbFile':'player.glb'}]}
EQUIPMENT={'heroes':[{'heroInstanceId':101,'alive':True,'dummyCelInstanceId':303}]}
RENDERER={'ownerKind':'player-combat','ownerInstanceId':202,'instanceId':404,'celInstanceId':303,
          'celRelativeRendererPath':'body','rendererPath':'actual-owner-relative/body','mesh':'ftkmf_glb_player.glb',
          'boneSignature':'sig','active':True,'enabled':True}


class PlayerRecordingTests(unittest.TestCase):
    def test_correct_owner_join_is_native_ids(self):
        r=select_player_renderer({'renderers':[RENDERER]},EQUIPMENT,PROFILE,101,'body')
        self.assertEqual(r['ownerInstanceId'],202);self.assertEqual(r['celInstanceId'],303)

    def test_wrong_owner_or_scope_rejected(self):
        for hero,scope,cel in [(999,'player-combat',303),(101,'enemies',303),(101,'player-preview',303),(101,'player-combat',999)]:
            r=dict(RENDERER,ownerKind=scope,celInstanceId=cel)
            with self.assertRaises((RuntimeError,ValueError)):
                select_player_renderer({'renderers':[r]},EQUIPMENT,PROFILE,hero,'body')

    def test_wrong_mesh_and_ambiguous_owners_rejected(self):
        for renderers in [[dict(RENDERER,mesh='nativeMesh')],[RENDERER,dict(RENDERER,ownerInstanceId=909)]]:
            with self.assertRaises(ValueError):select_player_renderer({'renderers':renderers},EQUIPMENT,PROFILE,101,'body')

    def test_actual_class_and_single_hero_guard(self):
        s=state();s['party'][0]['classId']=16
        player_guard(s,'probe',16)
        with self.assertRaises(RuntimeError):player_guard(s,'probe',17)
        s['party'].append(copy.deepcopy(s['party'][0]))
        with self.assertRaises(RuntimeError):player_guard(s,'probe',16)

    def test_apparel_absent_is_incomplete_but_body_selectable(self):
        profile=copy.deepcopy(PROFILE);profile['apparel']=[{'rendererPath':'armor','expectedNativeMeshName':'nativeArmor','glbFile':'armor.glb'}]
        coverage=player_outfit_coverage([RENDERER],profile)
        self.assertFalse(coverage['allConfiguredAssignmentsPresent']);self.assertEqual(coverage['absentApparelPaths'],['armor'])
        select_player_renderer({'renderers':[RENDERER]},EQUIPMENT,profile,101,'body')
        with self.assertRaises(RuntimeError):select_player_renderer({'renderers':[RENDERER]},EQUIPMENT,profile,101,'armor')

    def test_present_apparel_strict_identity_and_selection(self):
        profile=copy.deepcopy(PROFILE);profile['apparel']=[{'rendererPath':'armor','expectedNativeMeshName':'nativeArmor','glbFile':'armor.glb'}]
        armor=dict(RENDERER,instanceId=405,celRelativeRendererPath='armor',mesh='ftkmf_glb_armor.glb')
        self.assertEqual(select_player_renderer({'renderers':[RENDERER,armor]},EQUIPMENT,profile,101,'armor')['instanceId'],405)
        for bad in [dict(armor,mesh='nativeArmor'),dict(armor,mesh='ftkmf_glb_wrong.glb')]:
            with self.assertRaises(ValueError):select_player_renderer({'renderers':[RENDERER,bad]},EQUIPMENT,profile,101,'body')
        with self.assertRaises(ValueError):player_outfit_coverage([RENDERER,armor,armor],profile)

    def test_unmapped_actual_native_apparel_reported_separately(self):
        native=dict(RENDERER,celRelativeRendererPath='nativeClothes',mesh='nativeArmor',instanceId=406)
        coverage=player_outfit_coverage([RENDERER,native],PROFILE)
        self.assertTrue(coverage['allConfiguredAssignmentsPresent'])
        self.assertEqual(coverage['activeUnmappedRenderers'][0]['mesh'],'nativeArmor')
        with self.assertRaises(RuntimeError):select_player_renderer({'renderers':[RENDERER,native]},EQUIPMENT,PROFILE,101,'nativeClothes')

    def test_apparel_schema_duplicates_and_exact_name(self):
        valid={'rendererPath':'armor','expectedNativeMeshName':'Native Exact (F)','glbFile':'armor.glb','textureFile':'armor.png'}
        for field,value in [('rendererPath','body'),('rendererPath','../armor'),('glbFile','../armor.glb'),('glbFile',3),('textureFile',3),('expectedNativeMeshName',' '),('expectedNativeMeshName','bad\nname'),('expectedNativeMeshName','x'*161),('expectedNativeMeshName',3)]:
            profile=copy.deepcopy(PROFILE);profile['apparel']=[dict(valid,**{field:value})]
            with self.assertRaises(ValueError):player_assignments(profile)
        for invalid in [None,{},[valid]*17]:
            with self.assertRaises(ValueError):player_assignments(dict(PROFILE,apparel=invalid))
        self.assertEqual(player_assignments(dict(PROFILE,apparel=[valid]))[1][0]['expectedNativeMeshName'],'Native Exact (F)')

    def test_apparel_registered_assignment_snapshot_required(self):
        profile=dict(PROFILE,apparel=[{'rendererPath':'armor','expectedNativeMeshName':'native','glbFile':'armor.glb'}])
        validate_player_registration_assignments(profile,copy.deepcopy(profile))
        for entry in [PROFILE,dict(profile,apparel=[]),dict(profile,renderers=[])]:
            with self.assertRaises(ValueError):validate_player_registration_assignments(profile,entry)
        with self.assertRaises(ValueError):validate_player_registration_assignments(PROFILE,profile)
        validate_player_registration_assignments(PROFILE,{}) # Legacy profiles without apparel remain compatible.

    def test_native_action_once_after_player_owner_selection(self):
        with tempfile.TemporaryDirectory() as directory:
            runner=object.__new__(PlayerRecorder)
            runner.a=SimpleNamespace(enemy='probe',renderer_path='body',hero_instance_id=101,action='attack',class_key='custom')
            runner.profile=PROFILE;runner.player_profile=PROFILE;runner.expected_class_id=16;runner.output=Path(directory)
            runner.check_inputs=Mock();runner.log=Mock();s=state();s['party'][0]['classId']=16;runner.state=Mock(return_value=s)
            runner.helper=Mock(side_effect=[EQUIPMENT,{'renderers':[RENDERER]}])
            def begin(_):runner.capture_id='capture';runner.capture_path=Path(directory)/'pending.json'
            runner.begin_capture=Mock(side_effect=begin);runner.wait_first_frame=Mock()
            runner.action=Mock(side_effect=TimeoutError('uncertain native action'))
            runner.collect_capture=Mock(return_value={'frames':120})
            result=runner.record()
            self.assertFalse(result['ok']);self.assertEqual(runner.action.call_count,1)
            self.assertEqual(runner.action.call_args.args,('combat_turn',{'cheat':'None','focus':False,'targetFid':{'photonId':-1,'turnIndex':0}}))
            runner.collect_capture.assert_called_once();self.assertEqual(result['captureScope'],'player-combat')
            self.assertEqual(runner.helper.call_args_list[1].args,('inventory',{'scope':'player-combat'}))


if __name__=='__main__':unittest.main()
