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

class ExerciseTests(unittest.TestCase):

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
    def test_reward_decrease_is_not_progress(self):
        first=state(0);last=copy.deepcopy(first);last['party'][0]['gold']=0
        self.assertFalse(x.loot_progress(x.loot_fingerprint(first,loot()),x.loot_fingerprint(last,loot())))
    def test_missing_reward_observation_does_not_unlock_collect(self):
        s=state(0);s['party'][0]['gold']=None
        with self.assertRaises(RuntimeError):x.loot_fingerprint(s,loot())




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

if __name__=='__main__':unittest.main()
