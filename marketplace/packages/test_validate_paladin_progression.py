#!/usr/bin/env python3
"""Negative contracts for the unreleased Paladin set and shared token catalog."""
import copy
import json
from pathlib import Path

from validate_paladin import validate_v4_progression, validate_actions

ROOT = Path(__file__).resolve().parent
BASE = {row['id']: row for row in json.loads((ROOT/'paladin'/'content.json').read_text())['entries']}
TOKEN = json.loads((ROOT/'equipment-exchange'/'content.json').read_text())['entries'][0]


def rejected(change):
    rows, token = copy.deepcopy(BASE), copy.deepcopy(TOKEN)
    change(rows, token)
    try:
        validate_v4_progression(rows, token)
        validate_actions(rows)
    except (AssertionError, KeyError):
        return
    raise AssertionError('invalid progression was accepted')


def main():
    validate_v4_progression(copy.deepcopy(BASE), copy.deepcopy(TOKEN))
    validate_actions(copy.deepcopy(BASE))
    # Every family and both artifacts must replace the inherited native action list.
    for item_id, item in BASE.items():
        if item['kind'] != 'weapon':
            continue
        rejected(lambda rows, token, key=item_id: rows[key].pop('replaceProficiencies'))
        rejected(lambda rows, token, key=item_id: rows[key]['proficiencies'].append('bluntShockwaveSplash'))
        rejected(lambda rows, token, key=item_id: rows[key]['fields'].update(m_AttackDisplay='STR_profCrush'))
        rejected(lambda rows, token, key=item_id: rows[key]['fields'].update(m_NoRegularAttack=True))
    rejected(lambda rows, token: rows['paladin_smite'].update(template='magicdamage'))
    rejected(lambda rows, token: rows['paladin_smite']['fields'].update(m_DmgTypeOverride='physical'))
    rejected(lambda rows, token: rows['paladin_smite']['fields'].update(m_FullSlots=False))
    rejected(lambda rows, token: rows['paladin_smite']['fields'].update(m_ChanceToAffect=1))
    rejected(lambda rows, token: rows['paladin_smite']['fields'].update(m_IgnoresArmor=True))
    rejected(lambda rows, token: rows['paladin_smite']['fields'].pop('m_IgnoresArmor'))
    rejected(lambda rows, token: rows['paladin']['guardianEquipmentSets'][0]['core'].update(smitePercent=100))
    rejected(lambda rows, token: rows['paladin'].pop('weaponProficiencies'))
    rejected(lambda rows, token: rows['paladin']['weaponProficiencies'][0]['weapons'].append('bluntMace'))
    rejected(lambda rows, token: rows['paladin']['weaponProficiencies'][0]['weapons'].remove('paladin_hammer_1h_last_vigil'))
    rejected(lambda rows, token: rows['paladin']['weaponProficiencies'][1]['proficiencies'].remove('paladin_smite'))
    rejected(lambda rows, token: rows['paladin_trinket_mercy'].update(proficiencies=['paladin_smite']))
    rejected(lambda rows, token: rows['paladin']['guardianEquipmentSets'][0].update(coreProficiencies=['paladin_smite']))
    rejected(lambda rows, token: rows['paladin']['guardianEquipmentSets'][0].update(
        head='paladin_helmet_verdict'))
    rejected(lambda rows, token: rows['paladin']['guardianEquipmentSets'][0]['core'].update(
        physicalPercent=rows['paladin']['guardianEquipmentSets'][0]['minor']['physicalPercent']))
    rejected(lambda rows, token: rows['paladin']['guardianEquipmentSets'][0].update(
        twoHand='paladin_hammer_2h_kingsfall'))
    rejected(lambda rows, token: rows['paladin']['townExchange'].update(token='paladin_oathmark'))
    rejected(lambda rows, token: rows['paladin']['townExchange']['offers'][0].update(
        slot='Trinket'))
    rejected(lambda rows, token: rows['paladin']['townExchange']['offers'].append(
        copy.deepcopy(rows['paladin']['townExchange']['offers'][0])))
    rejected(lambda rows, token: token['enemyDropRule'].update(bossChancePercent=10))
    rejected(lambda rows, token: token['enemyDropRule'].update(guaranteedByOpportunity=0))
    print('PASS: Paladin progression rejects mixed roles, missing costs, artifact completion, wrong currency, offer mutation, invalid reward rule, inherited hammer actions and leaked class skills')


if __name__ == '__main__':
    main()
