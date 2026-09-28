#!/usr/bin/env python3
"""Adopt the exact26 framing portraits and retain14 prior studio-v2 icons."""
import argparse
import copy
import json
from pathlib import Path
from sync_nonweapon_icons import HERE, ROOT, LEDGER, digest, check_png, retained
from nonweapon_icon_revisions import FRAMED, merge_framed_icons, validate_revision_join

MANIFEST_SHA='c2e19dae1ce0acfa232d4a8763e23afcbe9424466ba385926d8da0701e891331'
HISTORY=HERE/'nonweapon-icons.studio-v2.provenance.json'
HISTORY_SHA='63a58e8650252670d4c3b845b1331119f52734952dffab09dd8a245687adede8'


def previous_ledger():
    path=HISTORY if HISTORY.exists() else LEDGER
    assert digest(path)==HISTORY_SHA, 'Original studio-v2 ledger changed'
    return json.loads(path.read_text())


def validate_current(ledger,package,entries):
    assert ledger['deliveryManifest']['sha256']==MANIFEST_SHA
    assert ledger['previousLedger']['path']==str(HISTORY.relative_to(ROOT))
    assert digest(HISTORY)==HISTORY_SHA
    validate_revision_join(ledger,previous_ledger())
    by_id={e['id']:e for e in entries}
    rows=[r for r in ledger['items'] if r['package']==package]
    for row in rows:
        entry=by_id[row['itemId']]
        assert entry['kind']=='item' and 'precisionWeapon' not in entry
        icon=HERE/package/entry['icon']
        assert row['path']==str(icon.relative_to(ROOT)) and digest(icon)==row['sha256']
        check_png(icon)
        expected={str((HERE/package/m[k]).relative_to(ROOT)) for m in entry['displayModels'] for k in ('model','texture')}
        assert {p['path'] for p in row['inputs']}==expected
        for source in row['inputs']: assert digest(ROOT/source['path'])==source['sha256']
    return rows


def generate(manifest_path):
    assert digest(manifest_path)==MANIFEST_SHA
    manifest=json.loads(Path(manifest_path).read_text())
    assert len(manifest['items'])==26
    previous=previous_ledger()
    assert manifest['supersedesForTheseItemsOnly']['sha256']==previous['deliveryManifest']['sha256']
    entries={p:{e['id']:e for e in json.loads((HERE/p/'content.json').read_text())['entries']} for p in FRAMED}
    rows,copies,seen=[],[],set()
    for row in manifest['items']:
        package,key=row['package'],row['itemId']
        assert package in FRAMED and key in FRAMED[package] and key not in seen
        seen.add(key)
        entry=entries[package][key]
        assert entry['kind']=='item' and 'precisionWeapon' not in entry
        destination=HERE/package/entry['icon']
        assert str(destination.relative_to(ROOT))==row['canonicalIconPath']
        assert digest(destination) in (row['canonicalBeforeSha256'],row['outputSha256'])
        source=ROOT/row['outputPath'];check_png(source)
        assert row['exactCopy'] and digest(source)==row['outputSha256']==row['sourceSha256']==digest(ROOT/row['sourceRenderPath'])
        inputs=[]
        for ref in row['sourceModelAndTexturePins']:
            canonical=HERE/package/'assets'/Path(ref['path']).name
            assert digest(ROOT/ref['path'])==ref['sha256']==digest(canonical)
            inputs.append({'path':str(canonical.relative_to(ROOT)),'sha256':ref['sha256']})
        expected={str((HERE/package/m[k]).relative_to(ROOT)) for m in entry['displayModels'] for k in ('model','texture')}
        assert {ref['path'] for ref in inputs}==expected
        rows.append({'itemId':key,'package':package,'path':str(destination.relative_to(ROOT)),
            'sha256':row['outputSha256'],'bytes':source.stat().st_size,'priorSha256':row['canonicalBeforeSha256'],
            'renderSource':{'sourceId':str(Path(row['sourceRenderPath']).relative_to('scratch')),'sha256':row['sourceSha256']},
            'inputs':inputs,'camera':row['sourceRenderCamera'],'native64AlphaThreshold':row['native64AlphaThreshold'],'native64Bounds':row['native64Bounds']})
        copies.append((source,destination))
    assert seen==set.union(*FRAMED.values())
    refresh={'schema':'ftkmf.nonweapon-inventory-icons.v2',
        'deliveryManifest':{'sourceId':'gear-consolidation/thief-coat-fit/nonweapon-icons-v2/manifest.json','sha256':MANIFEST_SHA},
        'adoptionGenerator':{'path':str(Path(__file__).resolve().relative_to(ROOT)),'sha256':digest(__file__)},
        'revisionGenerator':{'path':'marketplace/packages/nonweapon_icon_revisions.py','sha256':digest(HERE/'nonweapon_icon_revisions.py')},
        'studioManifest':retained(manifest['sourceStudioManifest']),'studioFrozenReceipt':retained(manifest['sourceStudioFrozenReceipt']),
        'renderGenerator':retained(manifest['generator']),'recipe':manifest['recipe'],'outputFormat':manifest['outputFormat'],
        'limits':'Studio art with original inputs; native icon-state/layout evidence remains separate. Retained coat/charm pixels keep their prior studio-v2 provenance.',
        'items':rows}
    ledger=merge_framed_icons(previous,refresh,str(HISTORY.relative_to(ROOT)))
    return ledger,copies


def paladin_provenance(ledger):
    receipt=json.loads((HERE/'paladin-assets.provenance.json').read_text())
    for row in ledger['items']:
        if row['package']!='paladin':continue
        key=str(Path(row['path']).relative_to('marketplace/packages/paladin'))
        old=receipt['files'][key]
        if isinstance(old.get('derivation'),dict) and old['derivation'].get('iconRevision')=='studio-v3':
            assert old['sha256']==row['sha256'] and old['previous']['sha256']==row['priorSha256']
            continue
        assert old['sha256']==row['priorSha256']
        receipt['files'][key]={'source':row['renderSource']['sourceId'],'sha256':row['sha256'],'bytes':row['bytes'],
            'baseline':copy.deepcopy(old.get('baseline',old)),'previous':copy.deepcopy(old),
            'derivation':{'ledger':str(LEDGER.relative_to(ROOT)),'itemId':row['itemId'],'inputs':row['inputs'],'iconRevision':'studio-v3'}}
    import hashlib
    receipt['nonweaponIconRevision']={'ledger':str(LEDGER.relative_to(ROOT)),'sha256':hashlib.sha256((json.dumps(ledger,indent=2)+'\n').encode()).hexdigest(),
        'scope':'Twelve exact studio-v3 helmet/boot icons. Previous studio-v2 and original icon records retained; native icon-state evidence separate.'}
    return receipt


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,required=True)
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    ledger,copies=generate(args.manifest)
    receipt=paladin_provenance(ledger)
    text=json.dumps(ledger,indent=2)+'\n'
    if args.check:
        assert LEDGER.read_text()==text
        assert all(a.read_bytes()==b.read_bytes() for a,b in copies)
        assert json.loads((HERE/'paladin-assets.provenance.json').read_text())==receipt
    else:
        if not HISTORY.exists(): HISTORY.write_bytes(LEDGER.read_bytes())
        assert digest(HISTORY)==HISTORY_SHA
        for a,b in copies:b.write_bytes(a.read_bytes())
        LEDGER.write_text(text)
        (HERE/'paladin-assets.provenance.json').write_text(json.dumps(receipt,indent=2)+'\n')
    for package in FRAMED:validate_current(ledger,package,json.loads((HERE/package/'content.json').read_text())['entries'])
    print('PASS: current40 nonweapon icons;26 exactstudio-v3 copies and14 unchangedstudio-v2 icons, previous ledger preserved.')


if __name__=='__main__':main()
