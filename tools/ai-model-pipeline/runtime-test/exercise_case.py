#!/usr/bin/env python3
"""One isolated enemy exercise; once-only actions and evidence, never art acceptance."""
import argparse
import hashlib
import html
import importlib.util
import json
import math
import os
import re
from pathlib import Path
import time
import motion_evidence
from record_case import DEFAULT_CAPTURE_TIMEOUT, Recorder, guard, verify_action
from run_case import Runner, read, digest, living


def require(value,message):
    if not value:raise RuntimeError(message)


def journal_entries(path):return [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
def pin(path):
    path=Path(path)
    require(path.is_file() and not any(p.is_symlink() for p in (path,*path.parents)),'Existing nonsymlink evidence file required')
    return {'path':str(path),'sha256':digest(path),'bytes':path.stat().st_size}


def party_identity(state):
    require(state.get('inSession') is True and state.get('singlePlayer') is True and living(state),'Living single-player session required')
    return [{'fid':p['fid'],'classId':p.get('classId')} for p in state['party']]


def target(state,enemy,fid):
    entries=(state.get('combat') or {}).get('enemies') or []
    matches=[e for e in entries if e.get('type')==enemy and e.get('fid')==fid]
    require(len(matches)==1,'Exact current target HP identity missing/ambiguous')
    hp=matches[0].get('hp');require(type(hp) in (int,float) and math.isfinite(hp),'Finite observed target HP required')
    return matches[0]


def hp_outcome(before,after,enemy,fid):
    first=target(before,enemy,fid)['hp'];last=target(after,enemy,fid)['hp']
    require(first>0,'Target was not alive immediately before attack')
    status='nonlethal_hp_loss' if 0<last<first else ('target_removed_or_zero_hp_after_action' if last<=0 else ('no_hp_loss_unclassified' if last==first else 'hp_increase_unclassified'))
    return {'status':status,'beforeHp':first,'afterHp':last,'nativeOutcome':None,'boundary':'Observed same-target HP only; no inferred block, dodge, hit animation, or damage source.'}


def native_attack_outcome(entry):
    observation=entry.get('attackResponseObservation')
    if not isinstance(observation,dict) or not observation:return None
    response=observation.get('response')
    if not isinstance(response,dict):raise RuntimeError('Native attack response observation is malformed')
    native=response.get('nativeAction')
    primary=native.get('primary') if isinstance(native,dict) else None
    if not isinstance(primary,dict):raise RuntimeError('Native attack response lacks the primary damage record')
    attack_response=response.get('attackResponse')
    if not isinstance(attack_response,str) or primary.get('attackResponse')!=attack_response:
        raise RuntimeError('Native attack response labels disagree')
    damage=primary.get('damage')
    if not isinstance(damage,(int,float)) or isinstance(damage,bool) or not math.isfinite(damage):
        raise RuntimeError('Native attack response damage is not finite numeric evidence')
    return {'attackResponse':attack_response,'damage':damage,
            'proficiencyId':primary.get('proficiencyId'),'proficiencySuccess':primary.get('proficiencySuccess')}


def valid_damage_restore(inspect,applied,restored,hero_id,receipt):
    before=restored.get('before') or {};after=restored.get('after') or {};progress=restored.get('levelProgression') or {}
    required=('heroInstanceId','weaponItemId','augmentedPhysicalDamage','nativeWeaponMaxDamage','playerLevel','playerXp',
              'rawPhysicalDamageBeforeAugmentation','weaponDamageGain','levelXpThresholds')
    if not all(k in inspect and k in before and k in after for k in required):return False
    original_level=inspect['playerLevel'];current_level=after['playerLevel']
    original_xp=inspect['playerXp'];current_xp=after['playerXp'];thresholds=inspect['levelXpThresholds']
    numeric=(original_level,current_level,original_xp,current_xp,inspect['nativeWeaponMaxDamage'],after['nativeWeaponMaxDamage'],
             inspect['augmentedPhysicalDamage'],before['augmentedPhysicalDamage'],after['augmentedPhysicalDamage'])
    if not all(type(v) is int for v in numeric) or not isinstance(thresholds,list) or not all(type(v) is int for v in thresholds):return False
    if current_level<original_level or current_xp<original_xp:return False
    if current_level!=original_level:
        native_level=next((i for i,value in enumerate(thresholds) if current_xp<value),0)
        if current_level!=native_level:return False
    stable=('heroInstanceId','statsInstanceId','photonId','turnIndex','weaponItemId','weaponItem','statsWeaponId','damageType',
            'nativeWeaponInstanceId','weaponBaseDamage','weaponDamageGain','levelXpThresholds','statsInCombat','combatDamageModifier',
            'chaosDamageModifier','rawPhysicalModifier','rawAllDamageModifier','magicAugmentation','skill','rawSkill','focusPoints',
            'spentFocus','avatarId','animatorId','controllerId','nativeCritDamagePercent','nativeChaosDamageMultiplier','nativeRaceDamageBonuses')
    if any(k in inspect and (before.get(k)!=inspect[k] or after.get(k)!=inspect[k]) for k in stable):return False
    gain=inspect['weaponDamageGain'];original_raw=inspect['rawPhysicalDamageBeforeAugmentation']
    if not isinstance(gain,(int,float)) or isinstance(gain,bool) or not math.isfinite(gain):return False
    if not isinstance(original_raw,(int,float)) or isinstance(original_raw,bool) or not math.isfinite(original_raw):return False
    expected_raw=original_raw+gain*(current_level-original_level)
    if before['playerLevel']!=current_level or before['playerXp']!=current_xp or after['playerXp']!=current_xp:return False
    if before['rawPhysicalDamageBeforeAugmentation']!=expected_raw or after['rawPhysicalDamageBeforeAugmentation']!=expected_raw:return False
    applied_after=applied.get('after') or {}
    if type(applied_after.get('augmentedPhysicalDamage')) is not int:return False
    augmentation_delta=applied_after['augmentedPhysicalDamage']-inspect['augmentedPhysicalDamage']
    if before['augmentedPhysicalDamage']!=applied_after.get('augmentedPhysicalDamage') or after['augmentedPhysicalDamage']!=inspect['augmentedPhysicalDamage']:return False
    if before['nativeWeaponMaxDamage']-after['nativeWeaponMaxDamage']!=augmentation_delta:return False
    return (restored.get('ok') is True and restored.get('status')=='hero-damage-restored' and restored.get('receipt')==receipt
            and after['heroInstanceId']==hero_id and after['weaponItemId']==inspect['weaponItemId']
            and restored.get('originalNativeWeaponMaxDamage')==inspect['nativeWeaponMaxDamage']
            and restored.get('expectedRestoredNativeWeaponMaxDamage')==after['nativeWeaponMaxDamage']
            and progress=={'originalLevel':original_level,'currentLevel':current_level,'originalXp':original_xp,'currentXp':current_xp,
                           'weaponDamageGainPerLevel':gain,'originalRawPhysicalDamage':original_raw,'currentRawPhysicalDamage':expected_raw})


def ready_slot(fixture,level,room):
    d=fixture.get('dungeon') or {};ready=fixture.get('strictReady') or {}
    require(ready.get('ok') is True and ready.get('level')==level and ready.get('room')==room,'Exact strict native Ready indices required')
    require(d.get('level')==level and d.get('room')==room and d.get('slotAllowsEnemySubstitution') is True and d.get('queuedRoomType')=='Enemy','Only existing eligible Enemy slots; no Stair/terminal/nonEnemy replacement')


def collect_button(fixture):
    require((fixture.get('strictLootCollect') or {}).get('ok') is True,'Strict native Collect predicate unavailable')
    matches=[]
    for surface in fixture.get('voteSurfaces') or []:
        if surface.get('alive') is True and surface.get('containerActive') is True and surface.get('voteType')=='Loot':
            for button in surface.get('buttons') or []:
                if button.get('option')=='Collect' and button.get('usable') is True and button.get('belongsToHero') is True:
                    matches.append({'heroInstanceId':surface['heroInstanceId'],'buttonInstanceId':button['instanceId'],'item':button['item']})
    require(len(matches)==1,'Exactly one owned current usable Collect button required')
    return matches[0]


def loot_fingerprint(state,fixture):
    # Only the selected Collect's availability/identity or actual rewards can
    # establish progress. Other buttons, focus flags, FSMs and dungeon UI cannot.
    selected=collect_button(fixture) if (fixture.get('strictLootCollect') or {}).get('ok') is True else None
    rewards=[{'fid':p['fid'],'gold':p.get('gold'),'xp':p.get('xp'),'level':p.get('level')} for p in state['party']]
    require(all(type(p[k]) is int for p in rewards for k in ('gold','xp','level')),'Actual integer reward observations required')
    return {'collect':selected,'rewards':rewards}


def loot_progress(before,after):
    if before['collect']!=after['collect']:return True
    require([p['fid'] for p in before['rewards']]==[p['fid'] for p in after['rewards']],'Reward party identity changed')
    return any(new[k]>old[k] for old,new in zip(before['rewards'],after['rewards']) for k in ('gold','xp','level'))


def selected_indices(action,count):
    if action=='kill-fixture':return sorted(set([0,count-1])) if count else []
    return [i for i in ([12,24] if action=='pass' else [6,12]) if i<count]


def capture_evidence(child,renderer,allow_death_prefix=False):
    require(child.capture_path.exists(),'Issued capture still pending; no dependent operation allowed')
    doc=read(child.capture_path)
    spec=importlib.util.spec_from_file_location('exercise_capture_boundary',Path(__file__).resolve().parents[1]/'compare_kraken_native.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    boundary=module.capture_boundary({'allowRendererDestroyedPrefix':allow_death_prefix,
                                     'allowDeathControllerTeardownPrefix':allow_death_prefix},doc)
    require(doc.get('session')==child.session and doc.get('id')==child.capture_id and doc.get('scope')=='enemies' and doc.get('fixedStep') is True,'Capture identity/timing mismatch')
    require(doc.get('ownerInstanceId')==renderer['ownerInstanceId'] and doc.get('celInstanceId')==renderer['celInstanceId'],'Capture owner changed')
    require(doc['requestedSeconds']==10 and doc['requestedFps']==12,'Fixed 120-frame capture request changed')
    images=[]
    identity=('instanceId','ownerInstanceId','celInstanceId','mesh','boneSignature')
    if renderer.get('celRootLocalScale') is not None:
        identity=identity+('celRootLocalScale',)
    for index,frame in enumerate(doc['frames']):
        require(all(frame.get(k)==renderer.get(k) for k in identity) and frame.get('ownerKind')=='enemies','Retained prefix renderer identity changed')
        require(frame.get('timeScale',0)>0 and frame.get('captureFramerate')==12,'Paused or changed retained frame')
        image=child.capture_path.with_suffix('')/f'{index:04d}.png'
        with image.open('rb') as stream:require(stream.read(8)==b'\x89PNG\r\n\x1a\n','Retained prefix PNG missing/invalid')
        images.append(pin(image))
    return {'rawCapture':pin(child.capture_path),'boundary':boundary,'images':images,'selectedIndices':selected_indices(child.a.action,len(images)),
            'selectedFramesViewed':0,'visualReview':'pending; deterministic selected frames only, not full motion or art acceptance'}


class BoundRecorder(Recorder):
    def __init__(self,args,coordinator):
        self.coordinator=coordinator;super().__init__(args)
        require(self.session==coordinator.session and self.binary_pins==coordinator.binary_pins and self.profile==coordinator.profile and self.asset_hashes==coordinator.asset_hashes,'Child source pins changed')
    def check_inputs(self):
        super().check_inputs();self.coordinator.check_inputs()
    def combat_guard(self,state):
        self.coordinator.same_party(state);identity=super().combat_guard(state)
        require(identity==self.coordinator.combat_identity,'Target/acting hero identity changed before action')
        return identity
    def select_renderer(self):
        renderer=super().select_renderer()
        identity=('instanceId','ownerInstanceId','celInstanceId','mesh','boneSignature')
        if self.coordinator.renderer.get('celRootLocalScale') is not None:
            identity=identity+('celRootLocalScale',)
        require(all(renderer.get(k)==self.coordinator.renderer.get(k) for k in identity),'Exact renderer owner changed')
        self.selected_renderer=renderer;return renderer


class Exercise(Recorder):
    def attach_motion_images(self, entry, evidence):
        for label,index in motion_evidence.review_points(evidence):
            require(0<=index<len(entry['capture']['images']),'Native motion review frame is unavailable')
            evidence[label]['image']=entry['capture']['images'][index]

    def require_ordinary_hit_motion(self, entry):
        evidence=motion_evidence.evidence_for_action('attack',read(Path(entry['capture']['rawCapture']['path'])),self.renderer,
                                                      self.combat_identity['enemyFid'])
        self.attach_motion_images(entry,evidence)
        entry['motionEvidence']=evidence

    def same_party(self,state):
        require(party_identity(state)==self.party,'Living party identity changed')
        if hasattr(self,'dungeon_id'):require((state.get('dungeon') or {}).get('dungeonId')==self.dungeon_id,'Native dungeon identity changed')
    def fixture(self):
        doc=self.helper('fixture-state');identity=doc.get('identity') or {}
        require(identity.get('root')==str(self.root) and identity.get('session')==self.session,'Fixture root/session mismatch')
        heroes=[s['heroInstanceId'] for s in doc.get('voteSurfaces') or []]
        require(heroes and len(set(heroes))==len(heroes) and all(type(h) is int and h!=0 for h in heroes),'Exact native hero owner identities required')
        if hasattr(self,'hero_ids'):require(heroes==self.hero_ids,'Native hero owner changed')
        else:self.hero_ids=heroes
        return doc
    def adopt_staged_damage_fixture(self):
        path=getattr(self.a,'staged_damage_fixture_result',None)
        if path is None:return
        path=path.resolve()
        require(path.is_relative_to((self.root/'model-test-output').resolve()),
                'Staged damage fixture result must be inside this isolated output root')
        document=read(path);fixture=document.get('heroDamageFixture') or {}
        inspected=fixture.get('inspect') or {};applied=fixture.get('apply') or {}
        hero=inspected.get('hero') or {};before=applied.get('before') or {};after=applied.get('after') or {}
        receipt=applied.get('receipt');minimum=applied.get('minimumNativeWeaponMaxDamage')
        require(document.get('session')==self.session and fixture.get('status')=='applied_outside_combat'
                and len(self.hero_ids)==1 and hero.get('heroInstanceId')==self.hero_ids[0]
                and before.get('heroInstanceId')==self.hero_ids[0]
                and type(hero.get('weaponItemId')) is int and before.get('weaponItemId')==hero['weaponItemId']
                and type(hero.get('augmentedPhysicalDamage')) is int
                and before.get('augmentedPhysicalDamage')==hero['augmentedPhysicalDamage']
                and type(hero.get('nativeWeaponMaxDamage')) is int
                and before.get('nativeWeaponMaxDamage')==hero['nativeWeaponMaxDamage']
                and type(minimum) is int and 1<=minimum<=100
                and after.get('nativeWeaponMaxDamage',0)>=minimum
                and isinstance(receipt,str) and receipt,
                'Staged damage fixture receipt is incomplete or belongs to another run')
        self.ledger['heroDamageFixture']={'stageResult':pin(path),'inspect':inspected,'apply':applied,
            'heroInstanceId':self.hero_ids[0],'requestedMinimumNativeWeaponMaxDamage':minimum,
            'restoreIssued':False,'status':'adopted_from_outside_combat_stage',
            'boundary':'Native weapon, focus, RNG, action, response, and enemy authority remain unchanged. Restore only from the exact receipt at native between-room Ready.'}
        self.damage_fixture_receipt=receipt;self.damage_fixture_restore_attempted=False
    def restore_damage_fixture(self):
        receipt=getattr(self,'damage_fixture_receipt',None)
        if receipt is None:return
        require(getattr(self,'damage_fixture_restore_attempted',False) is False,
                'Damage fixture restoration was already issued; automatic retry is forbidden')
        ledger=self.ledger['heroDamageFixture'];ledger['restoreIssued']=True
        self.damage_fixture_restore_attempted=True
        try:
            restored=self.helper('hero-damage-fixture',{'action':'restore','heroInstanceId':ledger['heroInstanceId'],'receipt':receipt})
        except Exception:
            ledger['status']='restore_uncertain_or_rejected';raise
        inspect=ledger['inspect']['hero'];ledger['restore']=restored;ledger['status']='restore_receipt_validation_pending'
        valid=valid_damage_restore(inspect,ledger['apply'],restored,ledger['heroInstanceId'],receipt)
        if not valid:ledger['status']='restore_receipt_invalid'
        require(valid,'Damage fixture restoration receipt does not prove the exact baseline')
        ledger['status']='restored';self.damage_fixture_receipt=None
    def no_pending(self):
        path=self.root/'model-test-command.json'
        if path.exists():
            require(not path.is_symlink(),'Symlink helper command refused')
            command=read(path);require(isinstance(command.get('id'),str) and re.fullmatch('[A-Za-z0-9_-]{1,128}',command['id']) is not None,'Invalid pending helper identity')
            require(command.get('session')!=self.session or (self.root/'model-test-output'/(command['id']+'.json')).exists(),'Pending helper/capture; no new operation allowed')
    def wait_turn(self):
        deadline=time.monotonic()+self.a.wait_timeout
        while time.monotonic()<deadline:
            state=self.state();self.same_party(state)
            current=target(state,self.a.enemy,self.combat_identity['enemyFid']);require(current.get('alive') is True and current['hp']>0,'Target no longer alive before next action')
            try:observed=guard(state,self.a.enemy)
            except RuntimeError:
                signals=state.get('signals') or {}
                require(signals.get('modalOpen') is False and signals.get('choiceOpen') is False,'Unknown/modal state; no automatic dismissal')
            else:
                require(observed==self.combat_identity,'Native action identity changed');return state
            time.sleep(.25)
        raise TimeoutError('Native hero turn not observed; no action retried')
    def prepare(self):
        self.check_inputs();self.no_pending();initial=self.state();self.party=party_identity(initial)
        d=initial.get('dungeon') or {};require(d.get('inDungeon') is True and type(d.get('level')) is int and type(d.get('room')) is int,'Existing native dungeon required')
        self.dungeon_id=d.get('dungeonId');self.start_level=d['level'];self.start_room=d['room']
        fixture=self.fixture()
        if self.a.from_ready:
            require(d['level']==self.a.level and d['room']==self.a.room,'Supplied Ready indices changed')
            ready_slot(fixture,self.a.level,self.a.room)
        else:guard(initial,self.a.enemy)
        key=json.dumps({'session':self.session,'dungeon':d.get('dungeonId'),'level':d['level'],'room':d['room']},sort_keys=True).encode()
        claim=self.root/'model-test-output'/('exercise-claim-'+hashlib.sha256(key).hexdigest()+'.json')
        with claim.open('x') as stream:json.dump({'session':self.session,'enemy':self.a.enemy,'journal':str(self.journal),'slot':d},stream)
        self.log('exclusive-exercise-claim',pin(claim));self.ledger['claim']=pin(claim)
        if self.a.from_ready:
            self.a.mode='next-case';stage=Runner.run(self);self.log('stage-result',stage)
        current=self.state();self.same_party(current);self.combat_identity=guard(current,self.a.enemy)
        self.renderer=Recorder.select_renderer(self);self.ledger['initialRenderer']=self.renderer;self.ledger['initialState']=current
    def record_one(self,action):
        self.no_pending();self.wait_turn()
        options=argparse.Namespace(**vars(self.a));options.action=action;options.mode='record-action'
        child=BoundRecorder(options,self);result=child.record()
        entry={'action':action,'focus':bool(getattr(options,'focus',False)) and action=='attack','journal':pin(child.journal),'rawResult':pin(child.output/'result.json'),'actionAccepted':None,'actionResult':result.get('actionResult'),'capture':None}
        self.ledger['actions'].append(entry)
        # Existing Recorder already gathers issued captures in finally. On uncertainty we only
        # observe that same pending result; never issue a helper, action, or replacement capture.
        if getattr(child,'capture_path',None) and not child.capture_path.exists():
            deadline=time.monotonic()+self.a.gather_timeout
            while time.monotonic()<deadline and not child.capture_path.exists():
                child.check_session();time.sleep(.1)
            entry['pendingCapturePath']=str(child.capture_path)
            if child.capture_path.exists():entry['lateRawCapture']=pin(child.capture_path)
            raise RuntimeError('Recorder ended with pending capture; gathered only, sequence permanently stopped')
        if getattr(child,'capture_path',None) and child.capture_path.exists():
            entry['rawCapture']=pin(child.capture_path)
            entry['rawPngFiles']=[pin(child.capture_path.with_suffix('')/f'{i:04d}.png') for i in range(120) if (child.capture_path.with_suffix('')/f'{i:04d}.png').is_file()]
        entries=journal_entries(child.journal)
        before=[e['data'] for e in entries if e['kind']=='action-before'];after=[e['data'] for e in entries if e['kind']=='after']
        require(len(before)==len(after)==1,'Missing exact action-before/after observation')
        entry['before']=before[0];entry['after']=after[0]
        verify_action(action,result.get('actionResult') or {},self.combat_identity['enemyFid'],entry['focus']);entry['actionAccepted']=True
        self.same_party(before[0]);self.same_party(after[0])
        if action=='attack':
            try:entry['hpOutcome']=hp_outcome(before[0],after[0],self.a.enemy,self.combat_identity['enemyFid'])
            except RuntimeError as error:entry['hpOutcome']={'status':'target_hp_outcome_unobserved','reason':str(error),'nativeOutcome':None}
        entry['capture']=capture_evidence(child,child.selected_renderer,allow_death_prefix=action=='kill-fixture')
        if getattr(self.a,'motion_evidence',False):
            if action=='attack':
                observation=motion_evidence.attack_response_observation(read(child.capture_path),child.selected_renderer,
                                                                         self.combat_identity['enemyFid'])
                self.attach_motion_images(entry,observation)
                entry['attackResponseObservation']=observation
            else:
                evidence=motion_evidence.evidence_for_action(action,read(child.capture_path),child.selected_renderer,
                                                              self.combat_identity['enemyFid'])
                self.attach_motion_images(entry,evidence)
                entry['motionEvidence']=evidence
        partial=entry['capture']['boundary']['partial']
        if partial:
            require(action=='kill-fixture' and not any(e['kind'] in ('error','after-error') for e in entries)
                and result.get('errors')==['Partial/paused/changed capture; preserve evidence without acceptance'],'Other failure accompanies death prefix; no cleanup progression')
            entry['classification']='expected_death_capture_boundary'
        else:
            require(result.get('ok') is True,'Raw recorder failure; no dependent actions')
            entry['classification']='recorded_action_and_frames'
        self.check_inputs();return entry
    def record_attack_with_retries(self):
        """Record bounded native attacks until damage is observed or the outcome changes.

        A repeated attack is permitted only after a complete same-target
        no-damage observation. The gate still requires measured nonlethal HP
        loss; this never relabels a block, dodge, immunity or other native
        outcome.
        """
        limit=getattr(self.a,'attack_attempts',1)
        attempts=[];accepted=None
        for attempt in range(1,limit+1):
            attack=self.record_one('attack');attack['attempt']=attempt
            attack['nativeReadyAfter']=self.wait_turn()
            # record_one already measures the action window after its retained
            # capture. Waiting for the next player turn can advance several
            # more combat turns and status ticks, so preserve that later net
            # state separately instead of attributing it to this attack.
            outcome=attack['hpOutcome'];outcome['nativeOutcome']=native_attack_outcome(attack)
            attack['readyHpOutcome']=hp_outcome(attack['before'],attack['nativeReadyAfter'],self.a.enemy,self.combat_identity['enemyFid'])
            attempts.append({'attempt':attempt,'status':outcome['status'],
                'beforeHp':outcome.get('beforeHp'),'afterHp':outcome.get('afterHp'),'boundary':outcome.get('boundary'),
                'nativeOutcome':outcome.get('nativeOutcome'),'readyHpOutcome':attack['readyHpOutcome']})
            if outcome['status']=='nonlethal_hp_loss':
                if getattr(self.a,'motion_evidence',False):self.require_ordinary_hit_motion(attack)
                accepted=attack;break
            if outcome['status']!='no_hp_loss_unclassified':break
        if limit>1:self.ledger['attackRetrySummary']=attempts
        last=attempts[-1]['status'] if attempts else 'no_attempt'
        require(accepted is not None,'Attack gate unmet after bounded native retry: '+last)
        return accepted
    def collect_to_ready(self):
        self.no_pending();deadline=time.monotonic()+self.a.wait_timeout;last=None;awaiting_progress=False;count=0
        while time.monotonic()<deadline:
            self.check_inputs();state=self.state();self.same_party(state);fixture=self.fixture()
            observation={'state':state,'fixture':fixture};self.log('loot-observation',observation)
            self.ledger['lootObservations'].append(observation)
            signals=state.get('signals') or {}
            require(signals.get('choiceOpen') is False and signals.get('modalOpen') is False,'Unknown modal; no automatic dismissal')
            if (fixture.get('strictReady') or {}).get('ok') is True:
                ready=fixture['strictReady']
                require(ready.get('level')==self.start_level and ready.get('room')==self.start_room+1,'Unexpected Ready slot; no automatic floor/next-case progression')
                self.ledger['finalReady']=fixture;return
            enemies=(state.get('combat') or {}).get('enemies') or []
            require(not any(e.get('alive') is True for e in enemies),'Living enemy remains; no loot progression')
            fingerprint=loot_fingerprint(state,fixture)
            if awaiting_progress:
                if not loot_progress(last,fingerprint):time.sleep(.25);continue
                awaiting_progress=False
            if (fixture.get('strictLootCollect') or {}).get('ok') is not True:time.sleep(.25);continue
            require(count<8,'Eight native collect limit reached')
            button=collect_button(fixture);require(button['heroInstanceId'] in self.hero_ids,'Loot button belongs to another hero');self.no_pending();self.same_party(self.state())
            result=self.helper('collect-loot',{'heroInstanceId':button['heroInstanceId']});count+=1
            require(result.get('ok') is True and result.get('status')=='clicked' and result.get('method')=='VoteButton.OnLeftClick(Collect)'
                and all(result.get(k)==v for k,v in button.items()),'Collect response identity uncertain; never repeat')
            self.ledger['collects'].append({'before':button,'result':result});self.log('collect-submitted',self.ledger['collects'][-1])
            last=fingerprint;awaiting_progress=True
        raise TimeoutError('Native loot progress/strict Ready not observed; no collect retried')
    def exercise(self):
        attack_limit=getattr(self.a,'attack_attempts',1)
        focused_attack=bool(getattr(self.a,'focus',False))
        retry_note='' if attack_limit==1 else f' Bounded native attack attempts: up to {attack_limit}; each retry requires a complete same-target no-loss observation and acceptance still requires measured nonlethal HP loss.'
        focus_note=' Native attack uses focus=true as a separate strongest-legitimate-hit supplement; it does not replace a no-focus boundary.' if focused_attack else ''
        self.ledger={'schema':'ftkmf.exercise-case.v1','session':self.session,'root':str(self.root),'enemy':self.a.enemy,'rendererPath':self.a.renderer_path,
            'port':self.a.port,'portOwnership':'Explicit operator-selected bridge port; no bridge root/session identity endpoint.',
            'binaryPins':self.binary_pins,'profileSha256':self.a.profile_sha256,'assetHashes':self.asset_hashes,'actions':[],'collects':[],'lootObservations':[],
            'status':'stopped','focusedAttack':focused_attack,'motionEvidenceRequired':bool(getattr(self.a,'motion_evidence',False)),
            'visualReview':'pending','selectedFramesViewed':0,'limitations':'One case only. KillSingle is an explicit fixture death, not normal damage.'+retry_note+focus_note+' No art/animation acceptance or automatic next profile.'}
        failure=None
        try:
            self.prepare();self.adopt_staged_damage_fixture();passed=self.record_one('pass');passed['nativeReadyAfter']=self.wait_turn()
            passed['partyHpBefore']=[p['hp'] for p in passed['before']['party']];passed['partyHpAfter']=[p['hp'] for p in passed['nativeReadyAfter']['party']]
            self.record_attack_with_retries()
            self.record_one('kill-fixture');self.collect_to_ready();self.restore_damage_fixture();self.ledger['status']='needs_visual_review'
        except Exception as error:failure=error
        finally:
            if getattr(self,'damage_fixture_receipt',None) is not None and not getattr(self,'damage_fixture_restore_attempted',False):
                try:self.restore_damage_fixture()
                except Exception as restore_error:
                    self.ledger['damageFixtureRestoreError']=str(restore_error)
                    if failure is None:failure=restore_error
            if failure is not None:
                self.ledger['error']=str(failure);self.log('exercise-stopped',{'error':str(failure),'actionRetry':attack_limit>1})
        self.log('exercise-result',{'status':self.ledger['status'],'error':self.ledger.get('error')})
        self.ledger['journal']=pin(self.journal);self.write_sheet()
        path=self.output/'case-result.json'
        with path.open('x') as stream:json.dump(self.ledger,stream,indent=2,allow_nan=False)
        return path
    def write_sheet(self):
        selected=[]
        for entry in self.ledger['actions']:
            capture=entry.get('capture')
            if not capture:continue
            for index in capture['selectedIndices']:
                selected.append({'action':entry['action'],'attempt':entry.get('attempt'),'index':index,**capture['images'][index]})
        self.ledger['selectedFrames']=selected;self.ledger['selectedFramesAvailable']=len(selected)
        cards=[]
        for frame in selected:
            source=html.escape(os.path.relpath(frame['path'],self.output),quote=True)
            attempt='' if frame.get('attempt') is None else f' attempt {frame["attempt"]}'
            cards.append(f'<figure><img src="{source}"><figcaption>{html.escape(frame["action"])}{attempt} - frame {frame["index"]}</figcaption></figure>')
        page=self.output/'selected-frames.html'
        with page.open('x') as stream:stream.write('<!doctype html><meta charset="utf-8"><title>Selected case frames - review pending</title><style>body{background:#17191c;color:#eee;font:16px sans-serif}main{display:grid;grid-template-columns:1fr 1fr}img{max-width:100%}figure{margin:12px}</style><h1>Selected frames - visual review pending</h1><p>'+str(len(selected))+' of six requested views available; zero marked viewed. Original frames only. Selection does not prove motion or art quality.</p><main>'+''.join(cards)+'</main>')
        self.ledger['selectedFrameSheet']=pin(page)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',required=True,type=Path);p.add_argument('--port',required=True,type=int);p.add_argument('--enemy',required=True)
    p.add_argument('--renderer-path',required=True);p.add_argument('--profile-sha256',required=True)
    p.add_argument('--from-ready',action='store_true');p.add_argument('--level',type=int);p.add_argument('--room',type=int)
    p.add_argument('--operation-timeout',type=float,default=40);p.add_argument('--capture-timeout',type=float,default=DEFAULT_CAPTURE_TIMEOUT)
    p.add_argument('--wait-timeout',type=float,default=120);p.add_argument('--gather-timeout',type=float,default=60)
    p.add_argument('--attack-attempts',type=int,default=1,help='Bounded native hero attacks after pass; retries stop on a non-no-loss outcome and still require measured nonlethal HP loss.')
    p.add_argument('--staged-damage-fixture-result',type=Path,help='Exact run_case result carrying an outside-combat damage-fixture receipt for this session.')
    p.add_argument('--focus',action='store_true',help='Use native max focus for a separately labelled strongest-legitimate-hit supplement.')
    a=p.parse_args()
    for name,maximum in [('operation_timeout',120),('capture_timeout',600),('wait_timeout',600),('gather_timeout',120)]:
        value=getattr(a,name)
        if not math.isfinite(value) or not 0<value<=maximum:p.error('Finite bounded timeouts required')
    if a.from_ready != (a.level is not None and a.room is not None) or (not a.from_ready and (a.level is not None or a.room is not None)):p.error('--from-ready requires both --level and --room; staged mode accepts neither')
    if a.from_ready and (a.level<0 or a.room<0):p.error('Nonnegative explicit Ready indices required')
    if a.attack_attempts<1 or a.attack_attempts>8:p.error('--attack-attempts must be an integer from 1 through 8')
    a.class_key=None;a.mode='exercise-case';a.action='pass';a.motion_evidence=True
    exercise=Exercise(a);result=exercise.exercise();print(result)
    raise SystemExit(0 if exercise.ledger['status']=='needs_visual_review' else 1)
if __name__=='__main__':main()
