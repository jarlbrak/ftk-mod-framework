#!/usr/bin/env python3
"""Offline evidence classifiers; automated enemy exercise driving is retired."""
import argparse
import importlib.util
import json
import math
from pathlib import Path
from record_case import RECORD_GUIDANCE
from run_case import read, digest, living


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


class Exercise:
    def __init__(self, args):
        raise ValueError("Automated exercise is retired. " + RECORD_GUIDANCE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_known_args()
    parser.error("Automated exercise is retired. " + RECORD_GUIDANCE)


if __name__ == '__main__': main()
