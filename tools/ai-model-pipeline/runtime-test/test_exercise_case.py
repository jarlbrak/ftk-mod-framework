"""Offline wrapper state-machine tests. No FTK connections or helper commands."""
import copy,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock,patch
import exercise_case as x

FID={'photonId':-1,'turnIndex':0}
def state(hp=30):
    return {'inSession':True,'singlePlayer':True,'party':[{'fid':{'photonId':1,'turnIndex':0},'classId':4,'hp':999,'gold':1,'xp':0,'level':0}],
        'signals':{'modalOpen':False,'choiceOpen':False,'allDead':False,'victoryShowing':False},'dungeon':{'inDungeon':True,'dungeonId':'dungeon','level':0,'room':1},
        'combat':{'active':True,'heroTurnReady':True,'readyParts':{'initialized':True,'actingFid':{'photonId':1,'turnIndex':0}},'whoseTurn':{'isPlayer':True},
                  'enemies':[{'fid':FID,'type':'enemy','alive':hp>0,'hp':hp}]}}
def loot(ready=False):
    return {'strictReady':{'ok':ready,'level':0,'room':2},'strictLootCollect':{'ok':not ready},'sessions':{'mcVoteType':'Ready' if ready else 'Loot'},
        'voteSurfaces':[{'heroInstanceId':10,'alive':True,'containerActive':True,'voteType':'Loot','buttons':[{'option':'Collect','instanceId':20,'item':'None','usable':True,'belongsToHero':True}]}],
        'dungeon':{'level':0,'room':2 if ready else 1}}
def wrapper(folder):
    r=object.__new__(x.Exercise);r.a=SimpleNamespace(enemy='enemy',renderer_path='body',profile_sha256='pin',port=8788,wait_timeout=1,gather_timeout=.2)
    r.root=r.output=Path(folder).resolve();r.session='s';r.binary_pins={};r.asset_hashes={};r.party=x.party_identity(state());r.hero_ids=[10]
    r.start_level=0;r.start_room=1;r.dungeon_id='dungeon';r.combat_identity=x.guard(state(),'enemy');r.renderer={}
    r.journal=Path(folder).resolve()/'journal.jsonl';r.journal.write_text('')
    r.log=Mock();r.no_pending=Mock();r.check_inputs=Mock();r.wait_turn=Mock(return_value=state());return r

class ExerciseTests(unittest.TestCase):
    def test_damage_fixture_adopts_exact_staged_receipt_and_restores_once(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={}
            inspected={'ok':True,'receipt':None,'restorationPending':False,'hero':{
                'heroInstanceId':10,'weaponItemId':100006,'augmentedPhysicalDamage':0,'nativeWeaponMaxDamage':10,
                'playerLevel':0,'playerXp':0,'rawPhysicalDamageBeforeAugmentation':10.,'weaponDamageGain':1.,
                'levelXpThresholds':[25,70,130]}}
            applied={'ok':True,'status':'hero-damage-fixture-applied','receipt':'token','minimumNativeWeaponMaxDamage':30,
                'before':inspected['hero'],'after':{**inspected['hero'],'augmentedPhysicalDamage':20,'nativeWeaponMaxDamage':30}}
            restored={'ok':True,'status':'hero-damage-restored','receipt':'token','before':applied['after'],'after':inspected['hero'],
                'originalNativeWeaponMaxDamage':10,'expectedRestoredNativeWeaponMaxDamage':10,
                'levelProgression':{'originalLevel':0,'currentLevel':0,'originalXp':0,'currentXp':0,'weaponDamageGainPerLevel':1.,
                                    'originalRawPhysicalDamage':10.,'currentRawPhysicalDamage':10.}}
            output=r.root/'model-test-output';output.mkdir();stage=output/'stage-result.json'
            stage.write_text(json.dumps({'session':'s','heroDamageFixture':{
                'inspect':inspected,'apply':applied,'status':'applied_outside_combat'}}))
            r.a.staged_damage_fixture_result=stage;r.helper=Mock(return_value=restored)
            r.adopt_staged_damage_fixture();r.restore_damage_fixture()
            self.assertEqual(r.helper.call_args.args[1],{'action':'restore','heroInstanceId':10,'receipt':'token'})
            self.assertEqual(r.ledger['heroDamageFixture']['status'],'restored')
            self.assertIsNone(r.damage_fixture_receipt)

    def test_damage_fixture_accepts_exact_xp_backed_level_progression(self):
        inspected={'heroInstanceId':10,'weaponItemId':100006,'augmentedPhysicalDamage':0,'nativeWeaponMaxDamage':10,
            'playerLevel':0,'playerXp':0,'rawPhysicalDamageBeforeAugmentation':10.,'weaponDamageGain':1.,
            'levelXpThresholds':[25,70,130]}
        applied={'after':{**inspected,'augmentedPhysicalDamage':23,'nativeWeaponMaxDamage':33}}
        current={**inspected,'playerLevel':2,'playerXp':110,'rawPhysicalDamageBeforeAugmentation':12.}
        restored={'ok':True,'status':'hero-damage-restored','receipt':'token',
            'before':{**current,'augmentedPhysicalDamage':23,'nativeWeaponMaxDamage':35},
            'after':{**current,'augmentedPhysicalDamage':0,'nativeWeaponMaxDamage':12},
            'originalNativeWeaponMaxDamage':10,'expectedRestoredNativeWeaponMaxDamage':12,
            'levelProgression':{'originalLevel':0,'currentLevel':2,'originalXp':0,'currentXp':110,'weaponDamageGainPerLevel':1.,
                                'originalRawPhysicalDamage':10.,'currentRawPhysicalDamage':12.}}
        self.assertTrue(x.valid_damage_restore(inspected,applied,restored,10,'token'))
        for path,value in [('expectedRestoredNativeWeaponMaxDamage',10),('originalNativeWeaponMaxDamage',12)]:
            changed=copy.deepcopy(restored);changed[path]=value
            self.assertFalse(x.valid_damage_restore(inspected,applied,changed,10,'token'))
        changed=copy.deepcopy(restored);changed['levelProgression']['currentXp']=109
        self.assertFalse(x.valid_damage_restore(inspected,applied,changed,10,'token'))
        changed=copy.deepcopy(restored);changed['after']['nativeWeaponMaxDamage']=13
        self.assertFalse(x.valid_damage_restore(inspected,applied,changed,10,'token'))

    def test_damage_fixture_uncertain_restore_is_never_retried(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'heroDamageFixture':{'heroInstanceId':10,'inspect':{'hero':{}},'status':'applied'}}
            r.damage_fixture_receipt='token';r.damage_fixture_restore_attempted=False
            r.helper=Mock(side_effect=TimeoutError('uncertain restore'))
            with self.assertRaises(TimeoutError):r.restore_damage_fixture()
            with self.assertRaisesRegex(RuntimeError,'already issued'):r.restore_damage_fixture()
            self.assertEqual(r.helper.call_count,1)
            self.assertEqual(r.ledger['heroDamageFixture']['status'],'restore_uncertain_or_rejected')

    def test_exclusive_slot_claim_blocks_second_exercise_before_staging(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);(r.root/'model-test-output').mkdir();r.a.from_ready=False;r.state=Mock(return_value=state());r.fixture=Mock(return_value=loot());r.ledger={}
            with patch.object(x.Recorder,'select_renderer',return_value={'instanceId':3}):
                r.prepare()
                with self.assertRaises(FileExistsError):r.prepare()
            self.assertEqual(len(list((r.root/'model-test-output').glob('exercise-claim-*.json'))),1)
    def test_wrong_hero_owner_stops_before_collect(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));f=loot();f['voteSurfaces'][0]['heroInstanceId']=99;r.fixture=Mock(return_value=f);r.helper=Mock()
            with self.assertRaises(RuntimeError):r.collect_to_ready()
            r.helper.assert_not_called()
    def test_hp_outcomes_not_inferred(self):
        for hp,status in [(20,'nonlethal_hp_loss'),(30,'no_hp_loss_unclassified'),(0,'target_removed_or_zero_hp_after_action'),(35,'hp_increase_unclassified')]:
            self.assertEqual(x.hp_outcome(state(),state(hp),'enemy',FID)['status'],status)
        other=state();other['combat']['enemies'][0]['fid']={'photonId':-2,'turnIndex':0}
        with self.assertRaises(RuntimeError):x.hp_outcome(state(),other,'enemy',FID)
    def test_native_attack_outcome_preserves_observed_block_without_hp_inference(self):
        entry={'attackResponseObservation':{'response':{'attackResponse':'Block','nativeAction':{'primary':{
            'attackResponse':'Block','damage':0,'proficiencyId':-1,'proficiencySuccess':False}}}}}
        self.assertEqual(x.native_attack_outcome(entry),{
            'attackResponse':'Block','damage':0,'proficiencyId':-1,'proficiencySuccess':False})
        self.assertIsNone(x.native_attack_outcome({}))
        entry['attackResponseObservation']['response']['nativeAction']['primary']['attackResponse']='Dodge'
        with self.assertRaisesRegex(RuntimeError,'labels disagree'):x.native_attack_outcome(entry)
    def test_no_missing_or_nonfinite_hp_inference(self):
        for hp in [None,float('nan'),float('inf')]:
            s=state();s['combat']['enemies'][0]['hp']=hp
            with self.assertRaises(RuntimeError):x.hp_outcome(state(),s,'enemy',FID)
    def test_stair_or_wrong_ready_slot_rejected(self):
        f=loot(True);f['strictReady']['room']=1;f['dungeon'].update(room=1,slotAllowsEnemySubstitution=True,queuedRoomType='Enemy')
        x.ready_slot(f,0,1)
        for kind in ['Stair','ExitRoom','Merchant','Cleared']:
            bad=copy.deepcopy(f);bad['dungeon']['queuedRoomType']=kind
            with self.assertRaises(RuntimeError):x.ready_slot(bad,0,1)
        with self.assertRaises(RuntimeError):x.ready_slot(f,0,2)
    def test_loot_wrong_owner_or_ambiguous_button_rejected(self):
        f=loot();f['voteSurfaces'][0]['buttons'][0]['belongsToHero']=False
        with self.assertRaises(RuntimeError):x.collect_button(f)
        f=loot();f['voteSurfaces'].append(copy.deepcopy(f['voteSurfaces'][0]))
        with self.assertRaises(RuntimeError):x.collect_button(f)
    def test_deterministic_views_do_not_duplicate_short_prefix(self):
        self.assertEqual(x.selected_indices('pass',120),[12,24]);self.assertEqual(x.selected_indices('attack',120),[6,12])
        self.assertEqual(x.selected_indices('kill-fixture',1),[0]);self.assertEqual(x.selected_indices('kill-fixture',0),[])
    def test_unchanged_loot_never_clicks_twice(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));r.fixture=Mock(return_value=loot())
            r.helper=Mock(return_value={'ok':True,'status':'clicked','method':'VoteButton.OnLeftClick(Collect)','heroInstanceId':10,'buttonInstanceId':20,'item':'None'})
            with patch.object(x.time,'monotonic',side_effect=[0,0,.1,.2,.3,1.1]),patch.object(x.time,'sleep'):
                with self.assertRaises(TimeoutError):r.collect_to_ready()
            self.assertEqual(r.helper.call_count,1)
    def test_unrelated_ready_button_focus_does_not_authorize_second_collect(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));counter=[0]
            def observed():
                f=loot();counter[0]+=1
                f['voteSurfaces'][0]['buttons'].append({'option':'Ready','instanceId':30,'focusing':counter[0]%2==0,'enabled':counter[0]%2==0})
                f['sessions']['unrelatedFsmState']=str(counter[0]);return f
            r.fixture=Mock(side_effect=observed)
            r.helper=Mock(return_value={'ok':True,'status':'clicked','method':'VoteButton.OnLeftClick(Collect)','heroInstanceId':10,'buttonInstanceId':20,'item':'None'})
            with patch.object(x.time,'monotonic',side_effect=[0,0,.1,.2,.3,1.1]),patch.object(x.time,'sleep'):
                with self.assertRaises(TimeoutError):r.collect_to_ready()
            self.assertGreater(counter[0],1);self.assertEqual(r.helper.call_count,1)
    def test_reward_decrease_is_not_progress(self):
        first=state(0);last=copy.deepcopy(first);last['party'][0]['gold']=0
        self.assertFalse(x.loot_progress(x.loot_fingerprint(first,loot()),x.loot_fingerprint(last,loot())))
    def test_missing_reward_observation_does_not_unlock_collect(self):
        s=state(0);s['party'][0]['gold']=None
        with self.assertRaises(RuntimeError):x.loot_fingerprint(s,loot())
    def test_actual_gold_reward_advance_permits_fresh_collect(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};initial=state(0);reward=state(0);reward['party'][0]['gold']=4
            r.state=Mock(side_effect=[initial,initial,reward,reward,reward]);r.fixture=Mock(side_effect=[loot(),loot(),loot(True)])
            r.helper=Mock(return_value={'ok':True,'status':'clicked','method':'VoteButton.OnLeftClick(Collect)','heroInstanceId':10,'buttonInstanceId':20,'item':'None'})
            r.collect_to_ready();self.assertEqual(r.helper.call_count,2)
    def test_uncertain_collect_never_retried(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));r.fixture=Mock(return_value=loot());r.helper=Mock(side_effect=TimeoutError('uncertain'))
            with self.assertRaises(TimeoutError):r.collect_to_ready()
            self.assertEqual(r.helper.call_count,1)
    def test_unknown_modal_with_usable_loot_stops(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};s=state(0);s['signals']['modalOpen']=True
            r.state=Mock(return_value=s);r.fixture=Mock(return_value=loot());r.helper=Mock()
            with self.assertRaises(RuntimeError):r.collect_to_ready()
            r.helper.assert_not_called()
    def test_ready_finishes_without_another_click(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));r.fixture=Mock(return_value=loot(True));r.helper=Mock()
            r.collect_to_ready();r.helper.assert_not_called();self.assertIn('finalReady',r.ledger)
    def test_wrong_ready_slot_stops(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'lootObservations':[],'collects':[]};r.state=Mock(return_value=state(0));f=loot(True);f['strictReady']['level']=1;r.fixture=Mock(return_value=f)
            with self.assertRaises(RuntimeError):r.collect_to_ready()
    def test_changed_input_blocks_loot_before_transport(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.check_inputs.side_effect=RuntimeError('changed binary');r.state=Mock()
            with self.assertRaises(RuntimeError):r.collect_to_ready()
            r.state.assert_not_called()
    def run_sequence(self,folder,outcome='nonlethal_hp_loss',error_action=None):
        r=wrapper(folder);r.prepare=Mock();r.collect_to_ready=Mock();r.write_sheet=Mock();r.require_ordinary_hit_motion=Mock()
        def record(action):
            if action==error_action:raise TimeoutError('uncertain issued action/capture')
            if action=='attack':
                r.wait_turn.return_value=state({'nonlethal_hp_loss':20,'no_hp_loss_unclassified':30,'target_removed_or_zero_hp_after_action':0,'target_hp_outcome_unobserved':30}[outcome])
            entry={'action':action,'before':state(),'after':state(20),'hpOutcome':{'status':outcome}}
            r.ledger['actions'].append(entry);return entry
        r.record_one=Mock(side_effect=record);r.exercise();return r
    def test_sequence_actions_once_then_ready_visual_pending(self):
        with tempfile.TemporaryDirectory() as d:
            r=self.run_sequence(d);self.assertEqual([c.args[0] for c in r.record_one.call_args_list],['pass','attack','kill-fixture'])
            r.collect_to_ready.assert_called_once();self.assertEqual(r.ledger['status'],'needs_visual_review')
    def test_focused_supplement_is_labeled_without_replacing_default(self):
        with tempfile.TemporaryDirectory() as d:
            r=self.run_sequence(d);self.assertFalse(r.ledger['focusedAttack'])
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.a.focus=True;r.prepare=Mock();r.collect_to_ready=Mock();r.write_sheet=Mock()
            def record(action):
                if action=='attack':r.wait_turn.return_value=state(20)
                entry={'action':action,'before':state(),'after':state(20),'hpOutcome':{'status':'nonlethal_hp_loss'}}
                r.ledger['actions'].append(entry);return entry
            r.record_one=Mock(side_effect=record);r.exercise()
            self.assertTrue(r.ledger['focusedAttack'])
            self.assertIn('focus=true',r.ledger['limitations'])
    def test_record_one_forwards_focus_to_the_native_response_check(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.a.focus=True;r.profile={};r.ledger={'actions':[]};r.wait_turn=Mock(return_value=state());r.check_inputs=Mock()
            child_dir=Path(d).resolve()/'child';child_dir.mkdir();journal=child_dir/'journal.jsonl'
            with journal.open('w') as stream:
                for kind in ('action-before','after'):
                    stream.write(json.dumps({'kind':kind,'data':state()})+'\n')
            (child_dir/'result.json').write_text('{}')
            class Child:
                def __init__(self,options,coordinator):
                    self.a=options;self.session=coordinator.session;self.binary_pins=coordinator.binary_pins;self.profile=coordinator.profile;self.asset_hashes=coordinator.asset_hashes
                    self.journal=journal;self.output=child_dir;self.selected_renderer={}
                def record(self):
                    return {'ok':True,'errors':[],'actionResult':{'ok':True,'result':{'committed':'Attack(focus)','target':FID}}}
            with patch.object(x,'BoundRecorder',Child),patch.object(x,'capture_evidence',return_value={'boundary':{'partial':False}}):
                entry=r.record_one('attack')
            self.assertTrue(entry['focus']);self.assertTrue(entry['actionAccepted'])
    def test_bounded_attack_retry_requires_later_measured_damage(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.a.attack_attempts=3;r.a.motion_evidence=True;r.ledger={'actions':[]}
            first={'action':'attack','before':state(30),'hpOutcome':x.hp_outcome(state(30),state(30),r.a.enemy,FID)}
            second={'action':'attack','before':state(30),'hpOutcome':x.hp_outcome(state(30),state(20),r.a.enemy,FID)}
            r.record_one=Mock(side_effect=[first,second]);r.wait_turn=Mock(side_effect=[state(30),state(20)]);r.require_ordinary_hit_motion=Mock()
            accepted=r.record_attack_with_retries()
            self.assertIs(accepted,second);self.assertEqual(r.record_one.call_count,2)
            self.assertEqual([x['status'] for x in r.ledger['attackRetrySummary']],['no_hp_loss_unclassified','nonlethal_hp_loss'])
            r.require_ordinary_hit_motion.assert_called_once_with(second)

    def test_retry_summary_carries_the_exact_native_response(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.a.attack_attempts=2;r.a.motion_evidence=True;r.ledger={'actions':[]}
            blocked={'action':'attack','before':state(30),'hpOutcome':x.hp_outcome(state(30),state(30),r.a.enemy,FID),'attackResponseObservation':{'response':{
                'attackResponse':'Block','nativeAction':{'primary':{'attackResponse':'Block','damage':0,
                    'proficiencyId':-1,'proficiencySuccess':False}}}}}
            hit={'action':'attack','before':state(30),'hpOutcome':x.hp_outcome(state(30),state(22),r.a.enemy,FID),'attackResponseObservation':{'response':{
                'attackResponse':'Damaged','nativeAction':{'primary':{'attackResponse':'Damaged','damage':8,
                    'proficiencyId':-1,'proficiencySuccess':True}}}}}
            r.record_one=Mock(side_effect=[blocked,hit]);r.wait_turn=Mock(side_effect=[state(30),state(22)])
            r.require_ordinary_hit_motion=Mock();r.record_attack_with_retries()
            self.assertEqual(r.ledger['attackRetrySummary'][0]['nativeOutcome']['attackResponse'],'Block')
            self.assertEqual(r.ledger['attackRetrySummary'][1]['nativeOutcome']['damage'],8)

    def test_no_loss_attempt_never_requires_damage_motion_before_stopping(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'actions':[]};entry={'action':'attack','before':state(30),'hpOutcome':x.hp_outcome(state(30),state(30),r.a.enemy,FID)}
            r.record_one=Mock(return_value=entry);r.wait_turn=Mock(return_value=state(30));r.require_ordinary_hit_motion=Mock()
            with self.assertRaisesRegex(RuntimeError, 'Attack gate unmet'):
                r.record_attack_with_retries()
            r.require_ordinary_hit_motion.assert_not_called()

    def test_later_ready_hp_drift_is_not_attributed_to_the_recorded_attack(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.a.attack_attempts=2;r.ledger={'actions':[]};r.require_ordinary_hit_motion=Mock()
            immediate=x.hp_outcome(state(58),state(53),r.a.enemy,FID)
            immediate['nativeOutcome']={'attackResponse':'Damaged','damage':5,
                'proficiencyId':-1,'proficiencySuccess':False}
            entry={'action':'attack','before':state(58),'hpOutcome':immediate,
                'attackResponseObservation':{'response':{'attackResponse':'Damaged','nativeAction':{'primary':{
                    'attackResponse':'Damaged','damage':5,'proficiencyId':-1,'proficiencySuccess':False}}}}}
            r.record_one=Mock(return_value=entry);r.wait_turn=Mock(return_value=state(45))
            accepted=r.record_attack_with_retries()
            self.assertIs(accepted,entry)
            self.assertEqual(entry['hpOutcome']['afterHp'],53)
            self.assertEqual(entry['readyHpOutcome']['afterHp'],45)
            self.assertEqual(r.ledger['attackRetrySummary'][0]['afterHp'],53)
            self.assertEqual(r.ledger['attackRetrySummary'][0]['readyHpOutcome']['afterHp'],45)

    def test_hit_motion_reads_the_pinned_raw_capture_path(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.renderer={'instanceId':3};raw=Path(d).resolve()/'raw.json'
            entry={'capture':{'rawCapture':{'path':str(raw)},'images':[{'path':'idle.png'}]}}
            evidence={'idle':{'sampleIndex':0}}
            with patch.object(x,'read',return_value={'frames':[]}) as reader,patch.object(x.motion_evidence,'evidence_for_action',return_value=evidence):
                r.require_ordinary_hit_motion(entry)
            self.assertIsInstance(reader.call_args.args[0],Path)
            self.assertEqual(entry['motionEvidence']['idle']['image'],{'path':'idle.png'})
    def test_no_hp_loss_or_zero_hp_stops_before_death(self):
        for outcome in ['no_hp_loss_unclassified','target_removed_or_zero_hp_after_action','target_hp_outcome_unobserved']:
            with tempfile.TemporaryDirectory() as d:
                r=self.run_sequence(d,outcome);self.assertEqual([c.args[0] for c in r.record_one.call_args_list],['pass','attack']);r.collect_to_ready.assert_not_called()
                self.assertEqual(r.ledger['status'],'stopped')
    def test_uncertainty_stops_all_dependent_actions(self):
        with tempfile.TemporaryDirectory() as d:
            r=self.run_sequence(d,error_action='pass');self.assertEqual(r.record_one.call_count,1);r.collect_to_ready.assert_not_called()
    def capture_fixture(self,folder,error=None):
        renderer={'instanceId':3,'ownerInstanceId':4,'celInstanceId':5,'mesh':'mesh','boneSignature':'sig'}
        path=Path(folder).resolve()/'capture.json';path.with_suffix('').mkdir()
        frames=[]
        for i in range(3):
            frames.append(dict(renderer,frame=20+i,ownerKind='enemies',timeScale=1,captureFramerate=12))
            (path.with_suffix('')/f'{i:04d}.png').write_bytes(b'\x89PNG\r\n\x1a\nexample')
        doc={'ok':False,'error':error or 'System.InvalidOperationException: Renderer destroyed during capture.\n  at RuntimeModelTest+<Capture>d__100.MoveNext () [0x00000] in <filename unknown>:0 ',
             'frames':frames,'frame':24,'requestedSeconds':10,'requestedFps':12,'fixedStep':True,'scope':'enemies','ownerInstanceId':4,'celInstanceId':5,'id':'capture','session':'s'}
        path.write_text(json.dumps(doc));child=SimpleNamespace(capture_path=path,capture_id='capture',session='s',a=SimpleNamespace(action='kill-fixture'))
        return child,renderer,doc
    def test_exact_death_prefix_preserves_raw_error_and_selected_last(self):
        with tempfile.TemporaryDirectory() as d:
            child,r,doc=self.capture_fixture(d);result=x.capture_evidence(child,r,True)
            self.assertFalse(result['boundary']['rawCaptureOk']);self.assertEqual(result['boundary']['rawCaptureError'],doc['error']);self.assertEqual(result['selectedIndices'],[0,2])
    def test_controller_teardown_prefix_requires_death_lane_and_exact_provenance(self):
        with tempfile.TemporaryDirectory() as d:
            child,r,doc=self.capture_fixture(d)
            identity=dict(targetRendererInstanceId=3,targetOwnerInstanceId=4,targetCelInstanceId=5,targetAnimatorInstanceId=6,targetFid={'photonId':-1,'turnIndex':0})
            event=dict(identity,nativeMethod='CharacterEventListener.CombatTrigger entry',trigger='Death',finalized=True,exception=None,frame=21,realtime=1.)
            doc['error']='Controller resolution unavailable after observed native Death.'
            for retained in doc['frames']:retained['motionAnimator']={'animatorInstanceId':6}
            doc['motionObservation']=dict(identity,schema='ftkmf.native-combat-motion.v1',error=None,events=[event],eventCount=1,
                startedFrame=20,startedRealtime=.5,baseline=dict(animatorInstanceId=6,baseLayerIdle=True,baseLayerTransition=False,animatorEnabled=True,animatorActive=True),
                termination=dict(reason='controller-unresolved-after-observed-native-death',controllerError='Avatar controller ambiguous or absent.',terminalFrameCaptured=False,frame=23,realtime=2.))
            child.capture_path.write_text(json.dumps(doc))
            result=x.capture_evidence(child,r,True)
            self.assertFalse(result['boundary']['completeCapture']);self.assertFalse(result['boundary']['rawCaptureOk'])
            self.assertEqual(result['boundary']['termination'],'controller_unresolved_after_native_death')
            with self.assertRaises(ValueError):x.capture_evidence(child,r,False)
            doc['motionObservation']['events'][0]['trigger']='Damaged';child.capture_path.write_text(json.dumps(doc))
            with self.assertRaises(ValueError):x.capture_evidence(child,r,True)
    def test_other_partial_error_or_changed_owner_or_missing_png_rejected(self):
        for kind in ['error','owner','gap']:
            with tempfile.TemporaryDirectory() as d:
                child,r,doc=self.capture_fixture(d)
                if kind=='error':doc['error']='Renderer destroyed during capture.'
                if kind=='owner':doc['frames'][1]['ownerInstanceId']=99
                if kind=='gap':(child.capture_path.with_suffix('')/'0001.png').rename(Path(d)/'kept-frame.png')
                child.capture_path.write_text(json.dumps(doc))
                with self.assertRaises((RuntimeError,ValueError,FileNotFoundError)):x.capture_evidence(child,r,True)
    def test_normal_attack_cannot_use_death_prefix_exception(self):
        with tempfile.TemporaryDirectory() as d:
            child,r,doc=self.capture_fixture(d)
            with self.assertRaises(ValueError):x.capture_evidence(child,r,False)

    def test_scaled_capture_rejects_cel_root_scale_drift(self):
        with tempfile.TemporaryDirectory() as d:
            child,r,doc=self.capture_fixture(d)
            r['celRootLocalScale']=[.55,.55,.55]
            for frame in doc['frames']:frame['celRootLocalScale']=[.55,.55,.55]
            doc['frames'][1]['celRootLocalScale']=[.54,.55,.55]
            child.capture_path.write_text(json.dumps(doc))
            with self.assertRaises(RuntimeError):x.capture_evidence(child,r,True)
    def test_pending_capture_only_gather_stops_even_when_later_complete(self):
        with tempfile.TemporaryDirectory() as d:
            r=wrapper(d);r.ledger={'actions':[]};r.a.mode='exercise-case';child=Mock();child.output=Path(d).resolve()/'child';child.output.mkdir();child.journal=child.output/'journal.jsonl';child.journal.write_text('')
            (child.output/'result.json').write_text('{}');child.capture_path=child.output/'pending.json';child.record.return_value={'ok':False}
            with patch.object(x,'BoundRecorder',return_value=child),patch.object(x.time,'monotonic',side_effect=[0,0,1]),patch.object(x.time,'sleep'):
                with self.assertRaises(RuntimeError):r.record_one('pass')
            child.record.assert_called_once();child.check_session.assert_called_once();self.assertIn('pendingCapturePath',r.ledger['actions'][0])

if __name__=='__main__':unittest.main()
