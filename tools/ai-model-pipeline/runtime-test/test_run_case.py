"""Offline orchestration regressions. Never connects to FTK."""
import argparse
import io
import json
import tempfile
from pathlib import Path
import unittest
from unittest.mock import patch
import run_case


class RunnerTests(unittest.TestCase):
    def binary_fixture(self,directory):
        root=Path(directory).resolve()/'scratch'/'game';root.mkdir(parents=True)
        for relative in run_case.DEPLOYED_BINARIES.values():
            path=root/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b'fixture DLL '+relative.encode())
        (root/'model-test-session.json').write_text(json.dumps(
            {'session':'a'*32, 'contentRegistrationRun':'b'*32}))
        return root

    def test_initial_journal_measures_binaries_before_profile_work(self):
        with tempfile.TemporaryDirectory() as directory:
            root=self.binary_fixture(directory)
            args=argparse.Namespace(root=root,class_key=None,port=8788,mode='next-case',enemy='test')
            def profile(r):
                entry=json.loads(r.journal.read_text().splitlines()[0])
                self.assertEqual(entry['kind'],'deployed-binaries')
                self.assertEqual(set(entry['data']['binaries']), {'framework', 'helper', 'content'})
                self.assertEqual(entry['data']['binaries']['helper']['sha256'],run_case.digest(root/run_case.DEPLOYED_BINARIES['helper']))
                self.assertEqual(entry['data']['binaries']['content']['sha256'],run_case.digest(root/run_case.DEPLOYED_BINARIES['content']))
                return {}
            (root/'model-test-profiles.json').write_text('{}')
            with patch.object(run_case.Runner,'profile_preflight',profile):runner=run_case.Runner(args)
            runner.check_inputs()

    def test_changed_binary_blocks_http_once_without_transport(self):
        with tempfile.TemporaryDirectory() as directory:
            root=self.binary_fixture(directory);runner=run_case.Runner.__new__(run_case.Runner)
            runner.root=root;runner.session='a'*32;runner.binary_pins=run_case.measured_binaries(root)
            (root/run_case.DEPLOYED_BINARIES['framework']).write_bytes(b'changed')
            with patch.object(run_case.urllib.request,'urlopen') as transport:
                with self.assertRaises(RuntimeError):runner.action('end_turn')
                transport.assert_not_called()

    def test_binary_symlink_and_missing_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            root=self.binary_fixture(directory);path=root/run_case.DEPLOYED_BINARIES['helper']
            moved=path.with_suffix('.saved');path.rename(moved)
            with self.assertRaises(RuntimeError):run_case.measured_binaries(root)
            path.symlink_to(moved)
            with self.assertRaises(RuntimeError):run_case.measured_binaries(root)

    def setup_runner(self, mode='new-run', reject=None):
        r=run_case.Runner.__new__(run_case.Runner)
        r.a=argparse.Namespace(mode=mode,enemy='test_key',class_key=None,level=0,room=2)
        r.session='test';r.journal=Path('offline');r.profile={'renderers':[{'rendererPath':'Body','glbFile':'original.glb'}]}
        r.expected_class_id=None
        calls=[]
        active={'signals':{'modalOpen':False,'choiceOpen':False},'singlePlayer':True,'party':[{'hp':99}],'combat':{'active':True,'heroTurnReady':True,'enemies':[{'type':'test_key'}]}}
        start={'phase':'menu'} if mode=='new-run' else {'singlePlayer':True,'party':[{'hp':99}],'dungeon':{'level':0,'room':2}}
        states=iter([start,active])
        r.state=lambda:next(states)
        def action(name,args=None):
            calls.append(name)
            if name==reject:raise RuntimeError('rejected')
            return {'ok':True}
        def helper(name,args=None):
            calls.append(name)
            if name==reject:raise RuntimeError('rejected')
            return {'renderers':[{'ownerInstanceId':1,'instanceId':2,'celRelativeRendererPath':'Body',
                'mesh':'ftkmf_glb_original.glb','active':True,'enabled':True}]}
        r.action=action;r.helper=helper
        r.claim_first_run=lambda:calls.append('claim_first_run')
        def wait(pred,label):
            calls.append('wait:'+label)
            return active
        r.wait=wait
        r.log=lambda *args:None
        r.clear_intro=lambda:calls.append('clear_intro')
        r.verify_story_clear=lambda:calls.append('verify_story_clear')
        r.prepare_entry=lambda:calls.append('prepare_entry')
        return r,calls

    def test_new_sequence_no_gap_after_entry(self):
        r,calls=self.setup_runner();result=r.run()
        index=calls.index('enter_dungeon')
        self.assertEqual(calls[index+1],'stage-enemy')
        self.assertNotIn('dungeon_encounter',calls)
        self.assertEqual(result['status'],'binding_metadata_observed')

    def test_native_cap_attack_skill_is_an_explicit_fortify_fixture(self):
        r,calls=self.setup_runner();r.a.cap_equipped_attack_skill=True;submitted=[]
        original=r.helper
        def helper(name,args=None):
            submitted.append((name,args));return original(name,args)
        r.helper=helper
        result=r.run()
        fortify=[args for name,args in submitted if name=='fortify-party']
        self.assertEqual(fortify,[{'targetMaxHp':999,'capEquippedAttackSkill':True}])
        self.assertIsNotNone(result['partyFixture'])

    def test_damage_fixture_uses_fortified_hero_and_exact_inspected_before_values(self):
        r,calls=self.setup_runner();r.a.minimum_native_weapon_max_damage=30;submitted=[]
        original=r.helper
        hero={'heroInstanceId':41,'weaponItemId':100006,'augmentedPhysicalDamage':0,'nativeWeaponMaxDamage':10}
        def helper(name,args=None):
            submitted.append((name,args));calls.append(name)
            if name=='fortify-party':return {'after':[{'heroInstanceId':41}]}
            if name=='hero-damage-fixture' and args['action']=='inspect':
                return {'receipt':None,'restorationPending':False,'hero':hero}
            if name=='hero-damage-fixture' and args['action']=='apply':
                return {'status':'hero-damage-fixture-applied','receipt':'token','minimumNativeWeaponMaxDamage':30,
                    'before':hero,'after':{**hero,'augmentedPhysicalDamage':20,'nativeWeaponMaxDamage':30}}
            return original(name,args)
        r.helper=helper
        result=r.run()
        apply=next(args for name,args in submitted if name=='hero-damage-fixture' and args['action']=='apply')
        self.assertEqual(apply,{'action':'apply','heroInstanceId':41,'expectedWeaponItemId':100006,
            'expectedAugmentedPhysicalDamage':0,'expectedNativeWeaponMaxDamage':10,
            'minimumNativeWeaponMaxDamage':30})
        self.assertEqual(result['heroDamageFixture']['status'],'applied_outside_combat')
        self.assertLess(calls.index('hero-damage-fixture'),calls.index('enter_dungeon'))

    def test_damage_fixture_retains_rejected_apply_response_for_inspection(self):
        r,calls=self.setup_runner();r.a.minimum_native_weapon_max_damage=30
        hero={'heroInstanceId':41,'weaponItemId':100006,'augmentedPhysicalDamage':0,'nativeWeaponMaxDamage':10}
        original=r.helper
        def helper(name,args=None):
            calls.append(name)
            if name=='fortify-party':return {'after':[{'heroInstanceId':41}]}
            if name=='hero-damage-fixture' and args['action']=='inspect':
                return {'receipt':None,'restorationPending':False,'hero':hero}
            if name=='hero-damage-fixture' and args['action']=='apply':
                return {'status':'hero-damage-fixture-applied','receipt':'token','minimumNativeWeaponMaxDamage':30,
                    'before':hero,'after':{**hero,'augmentedPhysicalDamage':20,'nativeWeaponMaxDamage':29}}
            return original(name,args)
        r.helper=helper
        with self.assertRaisesRegex(RuntimeError,'apply receipt is incomplete'):
            r.run()
        self.assertEqual(r.hero_damage_fixture['status'],'apply_response_rejected_receipt_may_require_inspection')
        self.assertEqual(r.hero_damage_fixture['apply']['receipt'],'token')
        self.assertNotIn('enter_dungeon',calls)

    def test_entry_rejection_stops_before_stage(self):
        r,calls=self.setup_runner(reject='enter_dungeon')
        with self.assertRaises(RuntimeError):r.run()
        self.assertNotIn('stage-enemy',calls)


    def test_ready_stage_rejection_stops_before_heal_and_click(self):
        r,calls=self.setup_runner('next-case','stage-next-enemy')
        with self.assertRaises(RuntimeError):r.run()
        self.assertEqual(calls,['stage-next-enemy'])

    def test_ready_sequence(self):
        r,calls=self.setup_runner('next-case');r.run()
        self.assertEqual(calls[:3],['stage-next-enemy','fortify-party','ready'])

    def test_empty_party_is_not_ready(self):
        self.assertFalse(run_case.living({'inSession':True,'party':[]}))

    def test_exact_two_enemy_combat_accepts_target_and_companion_in_either_order(self):
        state={'party':[{'hp':30}], 'combat':{'active':True,'heroTurnReady':True,
               'enemies':[{'type':'ogreA'},{'type':'gloamfin'}]}}
        self.assertTrue(run_case.exact_combat(state,'gloamfin','ogreA'))
        self.assertFalse(run_case.exact_combat(state,'gloamfin'))

    def test_victory_setup_can_skip_fortify_and_stage_native_companion(self):
        r,calls=self.setup_runner();r.a.companion_enemy='ogreA';r.a.skip_fortify=True
        active={'signals':{'modalOpen':False,'choiceOpen':False},'singlePlayer':True,'party':[{'hp':30}],
                'combat':{'active':True,'heroTurnReady':True,
                          'enemies':[{'type':'test_key'},{'type':'ogreA'}]}}
        states=iter([{'phase':'menu'},active]);r.state=lambda:next(states)
        r.wait=lambda predicate,label:active
        staged=[]
        def helper(name,args=None):
            calls.append(name);staged.append((name,args))
            return {'renderers':[{'ownerInstanceId':1,'instanceId':2,'celRelativeRendererPath':'Body',
                'mesh':'ftkmf_glb_original.glb','active':True,'enabled':True}]}
        r.helper=helper
        result=r.run()
        self.assertEqual(result['companionEnemy'],'ogreA')
        self.assertNotIn('fortify-party',calls)
        stage=next(args for name,args in staged if name=='stage-enemy')
        self.assertEqual(stage['companionEnemy'],'ogreA')

    def test_observed_late_story_stops_before_inventory(self):
        # Exact first post-stage bridge observation from the stopped Reefstrider
        # session, where the story prevented any combatant from being created.
        observed=run_case.read(Path(__file__).parent/'fixtures/reefstrider-post-stage-story.json')
        r,calls=self.setup_runner()
        r.a.enemy='ftkmf_modeltest_reefstrider'
        r.wait=lambda predicate,label:observed
        self.assertTrue(r.encounter_observed(observed))
        result=r.run()
        self.assertEqual(result['status'],'pending_story_message')
        self.assertEqual(result['matches'],[])
        self.assertFalse(result['finalState']['combat']['active'])
        self.assertNotIn('inventory',calls)
        self.assertNotIn('story-submit',calls)
        self.assertEqual(calls.count('stage-enemy'),1)

    def test_unknown_post_stage_modal_is_rejected(self):
        r,calls=self.setup_runner()
        for signals in ({'modalOpen':True,'choiceOpen':False,'modalType':'Unknown'},
                        {'modalOpen':True,'choiceOpen':True,'modalType':'StoryQuestMessage'}):
            with self.assertRaises(RuntimeError):r.encounter_observed({'signals':signals})

    def test_fallback_mesh_rejected(self):
        with self.assertRaises(ValueError):
            run_case.validate_inventory({'renderers':[{'ownerInstanceId':1,'celRelativeRendererPath':'Body','mesh':'vanilla'}]},
                {'renderers':[{'rendererPath':'Body','glbFile':'original.glb'}]})

    def test_target_inventory_ignores_unrelated_companion_owner(self):
        inventory={'renderers':[
            {'ownerInstanceId':1,'instanceId':2,'celRelativeRendererPath':'Body',
             'mesh':'ftkmf_glb_original.glb','active':True,'enabled':True},
            {'ownerInstanceId':3,'instanceId':4,'celRelativeRendererPath':'Body',
             'mesh':'native_ogre','active':True,'enabled':True}]}
        matches=run_case.validate_inventory(inventory,
            {'renderers':[{'rendererPath':'Body','glbFile':'original.glb'}]})
        self.assertEqual([item['ownerInstanceId'] for item in matches],[1])

    def test_static_renderer_requires_kind_meshfilter_and_private_material(self):
        profile={'renderers':[
            {'rendererPath':'kraken2','glbFile':'head.glb'},
            {'rendererPath':'Root_M/base/body/neck/eye/kraken2_eye','glbFile':'eye.glb',
             'textureFile':'eye.png','disableNativeEmission':True,'rendererKind':'MeshRenderer'}]}
        inventory={'renderers':[
            {'ownerInstanceId':9,'instanceId':10,'celRelativeRendererPath':'kraken2',
             'rendererKind':'SkinnedMeshRenderer','mesh':'ftkmf_glb_head.glb','active':True,'enabled':True},
            {'ownerInstanceId':9,'instanceId':11,'celRelativeRendererPath':'Root_M/base/body/neck/eye/kraken2_eye',
             'rendererKind':'MeshRenderer','meshFilterCount':1,'meshFilterInstanceId':12,
             'mesh':'ftkmf_static_glb_eye.glb','active':True,'enabled':True,
             'materials':[{'instanceId':13,'emissionKeyword':False,'_EmissionMapSupported':True,
                           '_EmissionMap':None,'emissionColor':[0,0,0,1],
                           '_MainTex':{'instanceId':14,'name':'ftkmf_eye.png'}}]}]}
        matches=run_case.validate_inventory(inventory,profile)
        self.assertEqual([(item['rendererPath'],item['rendererKind']) for item in matches],
                         [('kraken2','SkinnedMeshRenderer'),('Root_M/base/body/neck/eye/kraken2_eye','MeshRenderer')])
        self.assertEqual(matches[1]['materialInstanceId'],13)
        for mutate in [
                lambda value:value['renderers'][1].update(rendererKind='SkinnedMeshRenderer'),
                lambda value:value['renderers'][1].update(meshFilterCount=0),
                lambda value:value['renderers'][1].update(mesh='ftkmf_glb_eye.glb'),
                lambda value:value['renderers'][1].update(materials=[]),
                lambda value:value['renderers'][1]['materials'][0].update(emissionKeyword=True)]:
            broken=__import__('copy').deepcopy(inventory);mutate(broken)
            with self.assertRaises(ValueError):run_case.validate_inventory(broken,profile)

    def test_skinned_single_material_options_are_observed_for_every_assignment(self):
        profile={'renderers':[
            {'rendererPath':'EyeBody','glbFile':'body.glb','textureFile':'eye.png',
             'disableNativeEmission':True},
            {'rendererPath':'EyeBody/EyeEye','glbFile':'eye.glb','textureFile':'eye.png',
             'disableNativeEmission':True}]}
        material=lambda instance:{'instanceId':instance,'emissionKeyword':False,
            '_EmissionMapSupported':True,'_EmissionMap':None,'emissionColor':[0,0,0,1],
            '_MainTex':{'instanceId':instance+100,'name':'ftkmf_eye.png'}}
        inventory={'renderers':[
            {'ownerInstanceId':9,'instanceId':10,'celRelativeRendererPath':'EyeBody',
             'rendererKind':'SkinnedMeshRenderer','mesh':'ftkmf_glb_body.glb',
             'active':True,'enabled':True,'materials':[material(12)]},
            {'ownerInstanceId':9,'instanceId':11,'celRelativeRendererPath':'EyeBody/EyeEye',
             'rendererKind':'SkinnedMeshRenderer','mesh':'ftkmf_glb_eye.glb',
             'active':True,'enabled':True,'materials':[material(13)]}]}
        matches=run_case.validate_inventory(inventory,profile)
        self.assertEqual([item['materialInstanceId'] for item in matches],[12,13])
        for mutate in [
                lambda value:value['renderers'][1].update(materials=[]),
                lambda value:value['renderers'][1]['materials'][0].update(emissionKeyword=True),
                lambda value:value['renderers'][1]['materials'][0]['_MainTex'].update(name='native_eye')]:
            broken=__import__('copy').deepcopy(inventory);mutate(broken)
            with self.assertRaises(ValueError):run_case.validate_inventory(broken,profile)

    def test_registered_public_scale_requires_exact_spawned_cel_root_scale(self):
        profile={'key':'ftkmf_modeltest_scale_probe','visualScale':.55,
                 'renderers':[{'rendererPath':'Body','glbFile':'original.glb'}]}
        registration={'key':'ftkmf_modeltest_scale_probe','visualScale':.55,
                      'nativePrefabRootLocalScale':[.9,.9,.9]}
        contract=run_case.visual_scale_contract(profile,registration)
        inventory={'renderers':[{'ownerInstanceId':1,'instanceId':2,'celRelativeRendererPath':'Body',
                                 'mesh':'ftkmf_glb_original.glb','active':True,'enabled':True,
                                 'celRootLocalScale':[.495,.495,.495]}]}
        matches=run_case.validate_inventory(inventory,profile,visual_scale=contract)
        self.assertTrue(all(abs(value-.495)<1e-9 for value in matches[0]['visualScale']['actualSpawnedCelRootLocalScale']))
        inventory['renderers'][0]['celRootLocalScale']=[.55,.55,.55]
        with self.assertRaises(ValueError):run_case.validate_inventory(inventory,profile,visual_scale=contract)

    def test_only_exact_no_message_rejection_tolerated(self):
        r=run_case.Runner.__new__(run_case.Runner);r.log=lambda *args:None
        with patch.object(r,'http',side_effect=RuntimeError('Bridge rejected request: no message open')):
            self.assertIsNone(r.action('dismiss_message',tolerate_no_message=True))
        with patch.object(r,'http',side_effect=TimeoutError('uncertain')):
            with self.assertRaises(TimeoutError):r.action('dismiss_message',tolerate_no_message=True)

    def test_unknown_modal_never_dismissed(self):
        r=run_case.Runner.__new__(run_case.Runner)
        r.a=argparse.Namespace(wait_timeout=2)
        r.helper=lambda op,payload=None:{'signals':{'modalOpen':None}}
        r.log=lambda *args:None
        with patch.object(r,'action') as action:
            with self.assertRaises(RuntimeError):r.clear_intro()
            action.assert_not_called()

    def test_old_registration_refused(self):
        with self.assertRaises(ValueError):
            run_case.validate_registration_freshness({'updatedUtc':'2026-01-01T00:00:00Z','run':'a'*32},
                1800000000,1700000000,1900000000)

    def test_changed_profile_refused(self):
        with self.assertRaises(ValueError):
            run_case.validate_registration_freshness({'updatedUtc':'2026-01-01T00:00:00Z','run':'a'*32},
                1700000000,1800000000,1900000000)

    def test_current_registration_accepted(self):
        run_case.validate_registration_freshness({'updatedUtc':'2026-01-01T00:00:00Z','run':'a'*32},
            1700000000,1700000000,1900000000)

    def test_active_content_run_allows_registration_before_helper_session_write(self):
        run_case.validate_registration_freshness({'updatedUtc':'2026-01-01T00:00:00Z','run':'a'*32},
            1800000000,1700000000,1900000000,'a'*32)

    def test_registration_from_other_content_instance_refused(self):
        with self.assertRaises(ValueError):
            run_case.validate_registration_freshness({'updatedUtc':'2026-01-01T00:00:00Z','run':'a'*32},
                1800000000,1700000000,1900000000,'b'*32)

    def test_wrong_party_class_refused(self):
        r=run_case.Runner.__new__(run_case.Runner);r.expected_class_id=17
        with self.assertRaises(RuntimeError):r.verify_party_class({'party':[{'classId':16}]})

    def test_prior_session_journal_detected(self):
        text=json.dumps({'kind':'provenance','data':{'session':'abc','mode':'new-run'}})+'\n'
        with patch.object(Path,'open',return_value=io.StringIO(text)):
            self.assertTrue(run_case.journal_has_new_run(Path('offline'),'abc'))
        with patch.object(Path,'open',return_value=io.StringIO(text)):
            self.assertFalse(run_case.journal_has_new_run(Path('offline'),'different-process'))

    def test_second_new_run_stops_before_start_action(self):
        r,calls=self.setup_runner()
        with patch.object(r,'claim_first_run',side_effect=RuntimeError('fresh process required')):
            with self.assertRaises(RuntimeError):r.run()
        self.assertNotIn('start_run',calls)

    def test_existing_exclusive_marker_refused(self):
        r=run_case.Runner.__new__(run_case.Runner)
        r.root=Path('offline');r.journal=Path('own');r.session='abc'
        with patch.object(Path,'glob',return_value=[]),patch.object(Path,'open',side_effect=FileExistsError('prior attempt')):
            with self.assertRaises(FileExistsError):r.claim_first_run()

if __name__=='__main__':unittest.main()
