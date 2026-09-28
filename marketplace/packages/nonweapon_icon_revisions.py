"""Preserve studio-v2 source lineage while merging the bounded display-framing refresh."""
import copy
import hashlib
import json

PALADIN_TIERS=('novice','oathkeeper','highward','mercy','censure','verdict')
THIEF_TIERS=('street','burglar','guild','masterwork','locksmith','nightblade','wayfarer')
FRAMED={'paladin':{f'paladin_{part}_{tier}' for part in ('helmet','boots') for tier in PALADIN_TIERS},
        'thief':{f'thief_{part}_{tier}' for part in ('hood','boots') for tier in THIEF_TIERS}}
LEGACY={'paladin':{'paladin_helmet_oathkeeper','paladin_helmet_censure'}|{f'paladin_boots_{tier}' for tier in PALADIN_TIERS},
        'thief':{f'thief_{part}_{tier}' for part in ('coat','hood','charm') for tier in THIEF_TIERS}}
CURRENT={package:LEGACY[package]|FRAMED[package] for package in LEGACY}


def exact_items(ledger,allowed):
    rows=ledger['items']
    expected={(package,key) for package,keys in allowed.items() for key in keys}
    actual=[(row['package'],row['itemId']) for row in rows]
    assert len(actual)==len(expected) and set(actual)==expected
    return {row['itemId']:row for row in rows}


def merge_framed_icons(previous,refresh,history_path):
    """Inputs are already pinned by the adoption preflight. Never relabel retained pixels as rerenders."""
    old=exact_items(previous,LEGACY)
    new=exact_items(refresh,FRAMED)
    current=[]
    for key in sorted(set(old)|set(new)):
        if key in new:
            row=copy.deepcopy(new[key])
            row['sourceRevision']='studio-v3'
            if key in old:
                assert row['path']==old[key]['path'] and row['priorSha256']==old[key]['sha256']
                row['previous']=copy.deepcopy(old[key])
        else:
            row=copy.deepcopy(old[key])
            row['sourceRevision']='studio-v2'
        current.append(row)
    result=copy.deepcopy(refresh)
    result['schema']='ftkmf.nonweapon-inventory-icons.v2'
    result['scope']='40 nonweapon inventory icons: 26 display-framing portraits from studio-v3 and 14 unchanged studio-v2 coat/charm portraits. No weapon/class/proficiency/combat icon changes.'
    result['items']=current
    result['revisions']={'studio-v2':{k:copy.deepcopy(v) for k,v in previous.items() if k!='items'},
                         'studio-v3':{k:copy.deepcopy(v) for k,v in refresh.items() if k!='items'}}
    result['previousLedger']={'path':history_path,'sha256':hashlib.sha256((json.dumps(previous,indent=2)+'\n').encode()).hexdigest()}
    validate_revision_join(result,previous)
    return result


def validate_revision_join(ledger,previous):
    old=exact_items(previous,LEGACY)
    current=exact_items(ledger,CURRENT)
    assert ledger['revisions']['studio-v2']=={k:v for k,v in previous.items() if k!='items'}
    assert ledger['previousLedger']['sha256']==hashlib.sha256((json.dumps(previous,indent=2)+'\n').encode()).hexdigest()
    for key,row in current.items():
        if key in FRAMED[row['package']]:
            assert row['sourceRevision']=='studio-v3'
            if key in old:
                assert row['previous']==old[key] and row['priorSha256']==old[key]['sha256']
                assert row['path']==old[key]['path']
        else:
            assert row['sourceRevision']=='studio-v2'
            retained=copy.deepcopy(row);del retained['sourceRevision']
            assert retained==old[key]
    return list(current.values())
