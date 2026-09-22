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
    assert len(entries)==len(by_id)==39
    assert {kind:sum(e['kind']==kind for e in entries) for kind in ['class','weapon','item','proficiency']}=={'class':1,'weapon':12,'item':24,'proficiency':2}
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
            assert (entry['fields']['minlevel'],entry['fields']['maxlevel'])==[(0,1),(2,3),(4,6),(7,13)][min(index,3)]
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
    for item in by_id['paladin']['fields']['startitems']:assert item in by_id
    for entry in entries:
        for ref in entry.get('proficiencies',[]):assert by_id[ref]['kind']=='proficiency'
    assert by_id['paladin_censure_fracture']['fields']['m_SlotOverride']==4
    assert by_id['paladin_censure_fracture_1h']['fields']['m_SlotOverride']==3
    assert by_id['paladin_censure_fracture_1h']['fields']['m_CustomValue']==-4
    receipt=json.loads((HERE/'paladin-assets.provenance.json').read_text())
    for name,record in receipt['files'].items():assert hashlib.sha256((PACKAGE/name).read_bytes()).hexdigest()==record['sha256'],name
    assert set(refs)<=set(receipt['files'])
    assert all(path.suffix in ['.png','.glb'] for path in (PACKAGE/'assets').iterdir())
    print('PASS: 39 unique rows, complete six-family progression, original asset hashes, renderer paths and references. No live-game claims.')


if __name__=='__main__':main()
