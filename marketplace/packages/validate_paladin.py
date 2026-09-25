#!/usr/bin/env python3
"""Reproduce Paladin source-package structural checks without launching FTK."""
import hashlib
import json
import struct
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
            expected_keys = {'kind', 'id', 'template', 'displayName', 'fields',
                             'modifiers', 'icon', 'displayModels'}
            if slot == 'trinket':
                expected_keys.add('proficiencies')
                assert row['proficiencies'] == ['paladin_smite'], key
            assert set(row) == expected_keys, key
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
            assert len(row['displayModels']) == 1, key
            route = row['displayModels'][0]
            assert route['path'] == {'trinket': 'trinketHorn2', 'necklace': 'amuletLocket1'}[slot], key
            assert route['model'] == stem + '.glb', key
    assert by_id['paladin']['fields']['startweapon'] == 'paladin_hammer_1h_novice'
    assert by_id['paladin']['fields']['startitems'] == [
        'paladin_shield_novice',
    ]


def validate_balance(by_id):
    """Protect the role tradeoffs, not a duplicate table of tuning constants."""
    stats = by_id['paladin']['fields']
    # Vitality buys both hammer accuracy and health. Keep it within the freshly
    # inspected native Blacksmith ceiling; slower initiative pays for support.
    assert stats['vitality'] <= 0.80
    assert stats['speed'] <= 0.60
    assert stats['focus'] == 3
    for family in ['novice', 'oathkeeper', 'highward', 'mercy', 'censure', 'verdict']:
        one = by_id['paladin_hammer_1h_' + family]['fields']
        two = by_id['paladin_hammer_2h_' + family]['fields']
        assert one['damage'] < two['damage'], family
        assert one['slots'] < two['slots'], family
    for hands in ['1h', '2h']:
        damage = lambda family: by_id['paladin_hammer_' + hands + '_' + family]['fields']['damage']
        assert damage('novice') < damage('oathkeeper') < damage('highward')
        assert damage('highward') <= damage('mercy') == damage('censure') < damage('verdict')
    # These four-check weapons keep native control actions. Support branches
    # must not exceed the comparable five-check native hammer's base damage.
    assert by_id['paladin_hammer_2h_highward']['fields']['damage'] <= 32
    for family in ['mercy', 'censure']:
        assert by_id['paladin_hammer_2h_' + family]['fields']['damage'] <= 34
    assert by_id['paladin_hammer_2h_verdict']['fields']['damage'] < 38
    mercy_vitality = stats['vitality'] + sum(
        by_id['paladin_' + slot + '_mercy'].get('modifiers', {}).get('vitality', 0)
        for slot in ['armor', 'helmet', 'boots', 'trinket', 'necklace'])
    assert mercy_vitality + 0.05 < 0.95  # Leave room below the Apprentice cap.
    # Kingsfall's secured normal Guard/strike pair must not outdamage two
    # Verdict strikes; mitigation and healing are its reason to spend that turn.
    kingsfall = by_id['paladin_hammer_2h_kingsfall']['fields']
    verdict = by_id['paladin_hammer_2h_verdict']['fields']
    assert kingsfall['slots'] > verdict['slots']
    for level in [0, 6, 8, 10]:
        assert 1.5 * (kingsfall['damage'] + level * kingsfall['damagegain']) < 2 * (verdict['damage'] + level * verdict['damagegain'])


def validate_actions(by_id):
    assert by_id['paladin']['guardian'] is True
    assert not by_id['paladin'].get('proficiencies')
    for key, row in by_id.items():
        if row['kind'] == 'weapon':
            assert 'icon' not in row, key  # Inherit the native weapon-family Attack glyph.
            action = 'paladin_censure_fracture_1h' if '_1h_' in key else 'paladin_censure_fracture'
            assert row['proficiencies'] == [action], key
        elif row['kind'] == 'item' and not key.startswith('paladin_trinket_'):
            assert not row.get('proficiencies'), key
    for suffix, slots, reduction in [('_1h', 3, -4), ('', 4, -6)]:
        armor = by_id['paladin_censure_fracture' + suffix]
        resist_id = 'paladin_censure_resistance' + suffix
        resist = by_id[resist_id]
        assert armor['template'] == 'musicArmorDown' and resist['template'] == 'magicResistDown'
        expected = {'m_DmgMultiplier': 0.75, 'm_FullSlots': False, 'm_SlotOverride': slots,
                    'm_CustomValue': reduction, 'm_ChanceToAffect': 1, 'm_RepeatCount': 1}
        assert armor['fields'] == expected
        assert armor['randomDebuffOutcomes'] == [armor['id'], resist_id]
        assert 'randomDebuffOutcomes' not in resist
        assert resist['fields'] == dict(expected, m_DmgTypeOverride='none', m_WpnTypeOverride='none',
            m_Target='None', m_TargetFriendly=False, m_Harmless=False, m_IgnoresArmor=False,
            m_PerSlotSkillRoll=0.0, m_Quickness=0.30000001192092896, m_DamagePerAttack=0,
            m_Suicide=False, m_GunShot=False, m_BoatDamage=0, m_ChaosOption=False)
    smite = by_id['paladin_smite']
    assert smite['template'] == 'magicdamage'
    assert smite['fields'] == {'m_DmgMultiplier': 0.25, 'm_DmgTypeOverride': 'magic', 'm_Target': 'None',
        'm_TargetFriendly': False, 'm_Harmless': False, 'm_SlotOverride': -1, 'm_FullSlots': False}
    assert smite['resistanceDamageBonus'] == {
        'sources': ['paladin_censure_resistance_1h', 'paladin_censure_resistance'], 'multiplier': 6.0}


def validate_asset(path):
    """Check basic binary safety here; native helper remains the full loader validator."""
    raw = path.read_bytes()
    if path.suffix == '.png':
        assert raw[:8] == b'\x89PNG\r\n\x1a\n' and raw[12:16] == b'IHDR', path
        width, height = struct.unpack_from('>II', raw, 16)
        assert 0 < width <= 1024 and 0 < height <= 1024, path
        assert raw[24] == 8 and raw[25] in (2, 6), path
        return
    assert path.suffix == '.glb' and len(raw) >= 28, path
    assert struct.unpack_from('<III', raw) == (0x46546c67, 2, len(raw)), path
    length, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4e4f534a and length % 4 == 0, path
    document = json.loads(raw[20:20+length])
    binary_length, kind = struct.unpack_from('<II', raw, 20+length)
    assert kind == 0x004e4942 and binary_length % 4 == 0 and 28+length+binary_length == len(raw), path
    assert len(document['buffers']) == 1 and 'uri' not in document['buffers'][0], path
    assert not document.get('animations') and not document.get('images'), path
    assert len(document['meshes']) == 1 and len(document['meshes'][0]['primitives']) == 1, path
    primitive = document['meshes'][0]['primitives'][0]
    expected = {'POSITION', 'NORMAL', 'TEXCOORD_0'}
    if document.get('skins'):expected |= {'JOINTS_0', 'WEIGHTS_0'}
    assert set(primitive['attributes']) == expected and primitive.get('mode', 4) == 4, path
    position = document['accessors'][primitive['attributes']['POSITION']]
    assert position['componentType'] == 5126 and position['type'] == 'VEC3' and 0 < position['count'] < 65535, path
    indices = document['accessors'][primitive['indices']]
    assert indices['componentType'] == 5123 and indices['type'] == 'SCALAR' and indices['count'] > 0 and indices['count'] % 3 == 0, path
    for view in document['bufferViews']:
        assert view.get('buffer', 0) == 0 and 'byteStride' not in view, path
        assert 0 <= view.get('byteOffset', 0) < binary_length, path
        assert 0 < view['byteLength'] <= binary_length - view.get('byteOffset', 0), path
    for accessor in document['accessors']:
        assert not accessor.get('sparse') and not accessor.get('normalized', False), path


def main():
    document=json.loads((PACKAGE/'content.json').read_text())
    entries=document['entries'];by_id={entry['id']:entry for entry in entries}
    assert len(entries)==len(by_id)==57
    assert {kind:sum(e['kind']==kind for e in entries) for kind in ['class','weapon','item','proficiency']}=={'class':1,'weapon':14,'item':37,'proficiency':5}
    validate_accessories(by_id)
    validate_balance(by_id)
    # Inherit the template's complete native appearance list and unlock checks.
    assert by_id['paladin']['template']=='blacksmith'
    assert by_id['paladin']['overworldAilmentImmunity'] == {'displayName': 'Cleansing March'}
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
                expected_paths=[display_prefix if m['path']=='.' else display_prefix+'/'+m['path'] for m in entry['itemModels']]
                assert [m['path'] for m in entry['displayModels']] == expected_paths
                if family=='hammer_2h':
                    assert entry['displayModels'][0]['model']=='assets/paladin-hammer-2h-'+tier+'-display.glb'
                    assert all(m['model'].endswith('-display.glb') for m in entry['displayModels'])
                if family=='shield':assert entry['displayModels'][0]['model']=='assets/paladin-shield-'+tier+'-display.glb'
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
        if not family.startswith('hammer'):assert row['icon'] == stem + '-icon.png', key
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
    validate_actions(by_id)
    receipt=json.loads((HERE/'paladin-assets.provenance.json').read_text())
    for name,record in receipt['files'].items():assert hashlib.sha256((PACKAGE/name).read_bytes()).hexdigest()==record['sha256'],name
    assert set(refs)==set(receipt['files'])
    assert {str(path.relative_to(PACKAGE)) for path in (PACKAGE/'assets').iterdir()} == set(refs)
    routes={e['id']:{k:e[k] for k in ['itemModels','displayModels','apparelModels'] if k in e}
            for e in entries if any(k in e for k in ['itemModels','displayModels','apparelModels'])}
    assert routes == receipt['rendererRoutes']
    manifest=json.loads((PACKAGE/'manifest.json').read_text())
    assert manifest['version']=='1.4.0' and manifest['frameworkVersion']=='1.2.1'
    # The unchanged art retains its original 1.3.0 provenance and evidence.
    assert manifest['modGuid']=='com.ftkmf.paladin' and receipt['packageVersion']=='1.3.0'
    for name in set(refs):validate_asset(PACKAGE/name)
    assert all(path.suffix in ['.png','.glb'] for path in (PACKAGE/'assets').iterdir())
    print('PASS: balance tradeoffs, Guard/March class ownership, 57 unique rows, 14 Censure weapons, six Smite trinkets, 51 equipment items, acquisition, Artifact contracts, pinned assets and renderer routes. No live-game claims.')


if __name__=='__main__':main()
