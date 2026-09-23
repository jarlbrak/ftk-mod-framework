#!/usr/bin/env python3
"""Reproduce Paladin source-package structural checks without launching FTK."""
import hashlib
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
PACKAGE=HERE/'paladin'


def validate_accessories(by_id):
    """Check the complete accessory contract separately from file/hash validation."""
    families = ['novice', 'oathkeeper', 'highward', 'mercy', 'censure', 'verdict']
    expected = {
        'trinket': [
            ('Tin Oath Token', {'vitality': 0.01}),
            ("Keeper's Seal", {'vitality': 0.02}),
            ('Watchtower Reliquary', {'vitality': 0.02, 'resistance': 1}),
            ('Lantern of Mercy', {'armor': 1, 'resistance': 3}),
            ('Seal of Censure', {'speed': 0.02, 'armor': 1}),
            ('Scales of Verdict', {'armor': 2, 'resistance': 2}),
        ],
        'necklace': [
            ("Pilgrim's Pendant", {'resistance': 1}),
            ('Oath Chain', {'resistance': 1, 'vitality': 0.01}),
            ('Highward Gorget', {'resistance': 2}),
            ('Mercy Locket', {'resistance': 2, 'speed': 0.01}),
            ('Censure Medallion', {'speed': 0.02}),
            ("Judge's Collar", {'resistance': 3}),
        ],
    }
    accessory_ids = {'paladin_' + slot + '_' + family
                     for slot in expected for family in families}
    assert {key for key in by_id if key.startswith(('paladin_trinket_', 'paladin_necklace_'))} == accessory_ids
    for slot, values in expected.items():
        for index, (name, modifiers) in enumerate(values):
            family = families[index]
            key = 'paladin_' + slot + '_' + family
            row = by_id[key]
            # Requiring the whole modifier map rejects inherited HP/Focus claims and
            # unsupported passives. The loader supplies a fresh private modifier row.
            assert row['modifiers'] == modifiers, key
            assert set(row) == {'kind', 'id', 'template', 'displayName', 'fields',
                                'modifiers', 'icon', 'displayModels'}, key
            assert row['kind'] == 'item' and row['displayName'] == name, key
            assert row['template'] == {'trinket': 'trinketDefense1', 'necklace': 'amuletVitality1'}[slot], key
            band = min(index, 3)
            low, high = [(0, 0), (1, 2), (3, 3), (4, 6)][band]
            assert row['fields'] == {
                'minlevel': low, 'maxlevel': high,
                'goldvalue': [8, 35, 110, 250][band],
                'rarity': 'common' if index < 2 else 'rare',
                'dropable': True, 'townmarket': True, 'm_NightMarket': True,
                'm_DungeonMerchant': True, '_shopStock': 1,
                'm_CollectLoreItemUnlock': '', 'dlc': 'None',
            }, key
            stem = 'assets/paladin-' + slot + '-' + family
            assert row['icon'] == stem + '-icon.png', key
            # Accessories have a native loot display but no avatar renderer branch.
            assert row['displayModels'] == [{
                'path': {'trinket': 'trinketHorn2', 'necklace': 'amuletLocket1'}[slot],
                'model': stem + '.glb', 'texture': 'assets/paladin-accessory-palette.png',
            }], key
    assert by_id['paladin']['fields']['startweapon'] == 'paladin_hammer_1h_novice'
    assert by_id['paladin']['fields']['startitems'] == [
        'paladin_shield_novice', 'paladin_armor_novice',
        'paladin_boots_novice', 'paladin_helmet_novice',
    ]


def main():
    document=json.loads((PACKAGE/'content.json').read_text())
    entries=document['entries'];by_id={entry['id']:entry for entry in entries}
    assert len(entries)==len(by_id)==54
    assert {kind:sum(e['kind']==kind for e in entries) for kind in ['class','weapon','item','proficiency']}=={'class':1,'weapon':14,'item':37,'proficiency':2}
    validate_accessories(by_id)
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
    for family in ['hammer_1h','hammer_2h','shield','armor','boots','helmet','trinket','necklace']:
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
    print('PASS: 54 unique rows, 51 equipment items, six-family progression, 12 accessories, three Artifact items, original asset hashes, renderer paths and references. No live-game claims.')


if __name__=='__main__':main()
