#!/usr/bin/env python3
"""Reproduce Paladin source-package structural checks without launching FTK."""
import hashlib
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
PACKAGE=HERE/'paladin'


def main():
    document=json.loads((PACKAGE/'content.json').read_text())
    entries=document['entries'];by_id={entry['id']:entry for entry in entries}
    assert len(entries)==len(by_id)==42
    assert {kind:sum(e['kind']==kind for e in entries) for kind in ['class','weapon','item','proficiency']}=={'class':1,'weapon':14,'item':25,'proficiency':2}
    # Inherit the template's complete native appearance list and unlock checks.
    assert by_id['paladin']['template']=='blacksmith'
    assert 'm_Skinsets' not in by_id['paladin']['fields']
    assert 'playerModels' not in by_id['paladin']
    refs=[]
    def walk(value):
        if isinstance(value,dict):
            for key,child in value.items():
                if key in ['model','texture','icon'] and isinstance(child,str):
                    path=Path(child)
                    assert not path.is_absolute() and '..' not in path.parts and path.parts[0]=='assets'
                    assert (PACKAGE/path).is_file(),child
                    refs.append(child)
                else:walk(child)
        elif isinstance(value,list):
            for child in value:walk(child)
    walk(document)
    for family in ['hammer_1h','hammer_2h','shield','armor','boots','helmet']:
        for index,tier in enumerate(['novice','oathkeeper','highward','mercy','censure','verdict']):
            entry=by_id['paladin_'+family+'_'+tier]
            # Native acquisition uses item tiers, not character levels. Campaign
            # stages request 0, 1, 2, 3, 4, 4; rewards may request one tier higher.
            assert (entry['fields']['minlevel'],entry['fields']['maxlevel'])==[(0,0),(1,2),(3,3),(4,6)][min(index,3)]
            assert entry['fields']['dropable'] and entry['fields']['townmarket']
            # Every progression option must remain configured for acquisition,
            # including players without DLC. Live stock selection is a separate gate.
            assert entry['fields']['dlc']=='None',entry['id']
            assert entry['fields']['_shopStock']==1,entry['id']
            assert entry['fields']['m_DungeonMerchant'] is True,entry['id']
            assert entry['fields']['m_NightMarket'] is True,entry['id']
            assert entry['fields']['goldvalue']==[12,70,200,360][min(index,3)],entry['id']
            assert entry['fields']['rarity']==('common' if index<2 else 'rare'),entry['id']
            # Native LootAccept treats this as a lore identifier, not a boolean.
            assert entry['fields']['m_CollectLoreItemUnlock']=='',entry['id']
            if family.startswith('hammer'):assert entry['fields']['skill']=='vitality'
            if family=='hammer_1h':assert [m['path'] for m in entry['itemModels']]==['.','Break','Break/Break']
            if family=='hammer_2h':assert [m['path'] for m in entry['itemModels']]==['.','break','break/break2','break/break1']
            if family=='armor':
                assert len(entry['apparelModels']['renderers'])==2
                assert 'itemModels' not in entry
                assert [m['path'] for m in entry['displayModels']]==['armorSplintVestDisplay']
            if family=='boots':
                assert len(entry['apparelModels']['renderers'])==1
                assert entry['template']=='bootsHeavy3'
                assert 'itemModels' not in entry
                assert [m['path'] for m in entry['displayModels']]==['bootsIronGreavesDisplay']
            display_prefix={'hammer_1h':'smithhammer','hammer_2h':'hammer','shield':'shieldBlacksmith01','helmet':'helmKettle'}.get(family)
            if display_prefix:
                expected_display=[dict(m,path=display_prefix if m['path']=='.' else display_prefix+'/'+m['path']) for m in entry['itemModels']]
                if family=='hammer_2h':expected_display[0]['model']='assets/paladin-hammer-2h-'+tier+'-display.glb'
                if family=='shield':expected_display[0]['model']='assets/paladin-shield-'+tier+'-display.glb'
                assert entry['displayModels']==expected_display
            if family=='helmet':
                assert entry['template']=='helmetHeavy1'
                assert [m['path'] for m in entry['itemModels']]==['.']
    for family in ['hammer_1h','hammer_2h','shield','armor','boots','helmet']:
        for item_level in [0,1,2,3,4,5,6]:
            eligible=[row for key,row in by_id.items() if key.startswith('paladin_'+family+'_')
                      and row['fields']['minlevel'] <= item_level <= row['fields']['maxlevel']]
            assert eligible,(family,item_level)
        for tier in ['mercy','censure','verdict']:
            row=by_id['paladin_'+family+'_'+tier]
            assert row['fields']['minlevel'] <= 4 <= row['fields']['maxlevel'],row['id']
    legendary = {
        'paladin_hammer_1h_last_vigil': ('hammer_1h', 'last-vigil', {'guardFocusRestore': 1}),
        'paladin_hammer_2h_kingsfall': ('hammer_2h', 'kingsfall', {'guardReckoning': True}),
        'paladin_shield_last_bastion': ('shield', 'last-bastion', {'guardCleanse': True}),
    }
    for key, (family, slug, bonuses) in legendary.items():
        row = by_id[key]
        fields = row['fields']
        assert fields['rarity'] == 'artifact' and (fields['minlevel'], fields['maxlevel']) == (4, 6), key
        assert fields['dropable'] and fields['m_NightMarket'] and fields['m_DungeonMerchant'], key
        assert fields['townmarket'] is False and fields['_shopStock'] == 1, key
        assert fields['dlc'] == 'None' and fields['m_CollectLoreItemUnlock'] == '', key
        assert row['guardianBonuses'] == bonuses, key
        template = by_id['paladin_' + family + '_verdict']
        assert row['template'] == template['template'], key
        assert [m['path'] for m in row['itemModels']] == [m['path'] for m in template['itemModels']], key
        assert [m['path'] for m in row['displayModels']] == [m['path'] for m in template['displayModels']], key
        stem = 'assets/paladin-' + family.replace('_', '-') + '-' + slug
        assert row['itemModels'][0]['model'] == stem + '.glb', key
        assert row['displayModels'][0]['model'] == stem + '-display.glb', key
        assert row['icon'] == stem + '-icon.png', key
        assert all(m['texture'] == 'assets/paladin-legendary-palette.png'
                   for group in ('itemModels', 'displayModels') for m in row[group]), key
        if family.startswith('hammer'):
            assert fields['skill'] == 'vitality', key
        else:
            assert row['modifiers'] == {'armor': 0, 'resistance': 0, 'speed': -0.04}, key
    for item in by_id['paladin']['fields']['startitems']:
        assert item in by_id and item not in legendary
    assert by_id['paladin']['fields']['startweapon'] not in legendary
    for entry in entries:
        for ref in entry.get('proficiencies',[]):assert by_id[ref]['kind']=='proficiency'
    assert by_id['paladin_censure_fracture']['fields']['m_SlotOverride']==4
    assert by_id['paladin_censure_fracture_1h']['fields']['m_SlotOverride']==3
    assert by_id['paladin_censure_fracture_1h']['fields']['m_CustomValue']==-4
    receipt=json.loads((HERE/'paladin-assets.provenance.json').read_text())
    for name,record in receipt['files'].items():assert hashlib.sha256((PACKAGE/name).read_bytes()).hexdigest()==record['sha256'],name
    assert set(refs)<=set(receipt['files'])
    assert all(path.suffix in ['.png','.glb'] for path in (PACKAGE/'assets').iterdir())
    print('PASS: 42 unique rows, six-family progression, three Artifact items, original asset hashes, renderer paths and references. No live-game claims.')


if __name__=='__main__':main()
