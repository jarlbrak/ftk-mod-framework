#!/usr/bin/env python3
"""Adopt only the 29 approved nonweapon inventory PNGs from the frozen studio delivery."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
LEDGER = HERE/'nonweapon-icons.provenance.json'
MANIFEST_SHA = 'cee639931c3c6c5f026b48841b2d19d5c8196f396934196dd36994b96a4b0aa6'
TIERS = ('street','burglar','guild','masterwork','locksmith','nightblade','wayfarer')
ALLOWED = {'thief': {'thief_'+part+'_'+tier for part in ('coat','hood','charm') for tier in TIERS},
           'paladin': {'paladin_helmet_oathkeeper','paladin_helmet_censure'} |
                      {'paladin_boots_'+tier for tier in ('novice','oathkeeper','highward','mercy','censure','verdict')}}


def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def check_png(path):
    raw = Path(path).read_bytes()
    assert raw[:8] == b'\x89PNG\r\n\x1a\n' and raw[12:16] == b'IHDR'
    assert struct.unpack('>IIBB',raw[16:26]) == (512,512,8,6), path


def retained(ref):
    path = ROOT/ref['path']
    assert digest(path) == ref['sha256'], path
    return {'sourceId': Path(ref['path']).relative_to('scratch').as_posix(), 'sha256': ref['sha256']}


def validate_package_icons(package, entries):
    """Validate installed source bytes against the public ledger without private inputs."""
    ledger = json.loads(LEDGER.read_text())
    if ledger.get('censureVisualRevision'):
        from censure_visual_revision import validate_icons
        return validate_icons(ledger,package,entries)
    if ledger.get('helmetRevision'):
        from sync_display_framing import validate_helmet_icons
        return validate_helmet_icons(ledger,package,entries)
    if ledger.get('schema') == 'ftkmf.nonweapon-inventory-icons.v2':
        from sync_nonweapon_icons_v3 import validate_current
        return validate_current(ledger,package,entries)
    assert ledger['deliveryManifest']['sha256'] == MANIFEST_SHA
    records = [r for r in ledger['items'] if r['package'] == package]
    assert {r['itemId'] for r in records} == ALLOWED[package]
    by_id = {e['id']:e for e in entries}
    for row in records:
        entry = by_id[row['itemId']]
        assert entry['kind'] == 'item' and 'precisionWeapon' not in entry
        icon = HERE/package/entry['icon']
        assert row['path'] == str(icon.relative_to(ROOT)) and digest(icon) == row['sha256']
        check_png(icon)
        expected = {str((HERE/package/m[key]).relative_to(ROOT)) for m in entry['displayModels'] for key in ('model','texture')}
        assert {p['path'] for p in row['inputs']} == expected, row['itemId']
        for source in row['inputs']: assert digest(ROOT/source['path']) == source['sha256'], source['path']
    return records


def generate(manifest_path):
    assert digest(manifest_path) == MANIFEST_SHA, 'Frozen delivery manifest differs'
    manifest = json.loads(manifest_path.read_text())
    assert len(manifest['items']) == 29
    entries = {p:{e['id']:e for e in json.loads((HERE/p/'content.json').read_text())['entries']} for p in ALLOWED}
    records, copies, seen = [], [], set()
    for row in manifest['items']:
        package, key = row['package'], row['itemId']
        assert package in ALLOWED and key in ALLOWED[package] and key not in seen
        seen.add(key)
        entry = entries[package][key]
        assert entry['kind'] == 'item' and 'precisionWeapon' not in entry
        destination = HERE/package/entry['icon']
        assert str(destination.relative_to(ROOT)) == row['canonicalIconPath']
        assert digest(destination) in (row['canonicalBeforeSha256'],row['outputSha256']), destination
        source = ROOT/row['outputPath']; check_png(source)
        assert row['exactCopy'] and digest(source) == row['outputSha256'] == row['sourceSha256'] == digest(ROOT/row['sourceRenderPath'])
        inputs = []
        for pin in row['sourceModelAndTexturePins']:
            canonical = HERE/package/'assets'/Path(pin['path']).name
            assert digest(ROOT/pin['path']) == pin['sha256'] == digest(canonical)
            inputs.append({'path':str(canonical.relative_to(ROOT)), 'sha256':pin['sha256']})
        expected = {str((HERE/package/m[k]).relative_to(ROOT)) for m in entry['displayModels'] for k in ('model','texture')}
        assert {pin['path'] for pin in inputs} == expected
        records.append({'itemId':key, 'package':package, 'path':str(destination.relative_to(ROOT)),
                        'sha256':row['outputSha256'], 'bytes':source.stat().st_size,
                        'priorSha256':row['canonicalBeforeSha256'],
                        'renderSource':{'sourceId':Path(row['sourceRenderPath']).relative_to('scratch').as_posix(), 'sha256':row['sourceSha256']},
                        'inputs':inputs, 'camera':row['sourceRenderCamera'],
                        'native64AlphaThreshold':row['native64AlphaThreshold'], 'native64Bounds':row['native64Bounds']})
        copies.append((source,destination))
    assert seen == set.union(*ALLOWED.values())
    ledger = {'schema':'ftkmf.nonweapon-inventory-icons.v1',
              'scope':'Exact copies of frozen original-model studio artwork for 29 nonweapon inventory icons. Not native item-display or fit evidence. No weapon/class/proficiency/combat icon changes.',
              'deliveryManifest':{'sourceId':'gear-consolidation/thief-coat-fit/nonweapon-icons-v1/manifest.json','sha256':MANIFEST_SHA},
              'adoptionGenerator':{'path':str(Path(__file__).resolve().relative_to(ROOT)), 'sha256':digest(__file__)},
              'studioManifest':retained(manifest['sourceStudioManifest']),
              'studioFrozenReceipt':retained(manifest['sourceStudioFrozenReceipt']),
              'renderGenerator':retained(manifest['generator']), 'recipe':manifest['recipe'],
              'outputFormat':manifest['outputFormat'],
              'limits':'Street and Burglar neckwear remain small at shared scale; color/silhouette identifiable at 64px but fine detail weak. Native UI icon-state/layout checks pending.',
              'items':records}
    return ledger, copies


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,required=True)
    parser.add_argument('--check',action='store_true',help='Verify exact reproduction without writing PNGs or ledgers')
    args = parser.parse_args()
    ledger,copies = generate(args.manifest)
    text = json.dumps(ledger,indent=2)+'\n'
    if args.check:
        assert LEDGER.read_text() == text
        assert all(source.read_bytes() == destination.read_bytes() for source,destination in copies)
    else:
        path = HERE/'paladin-assets.provenance.json'
        receipt = json.loads(path.read_text())
        for row in ledger['items']:
            if row['package'] != 'paladin': continue
            key = str(Path(row['path']).relative_to('marketplace/packages/paladin'))
            previous = receipt['files'][key]
            baseline = previous.get('baseline',previous)
            assert baseline['sha256'] == row['priorSha256']
            receipt['files'][key] = {'source':row['renderSource']['sourceId'], 'sha256':row['sha256'], 'bytes':row['bytes'],
                                    'baseline':baseline, 'derivation':{'ledger':str(LEDGER.relative_to(ROOT)), 'itemId':row['itemId'], 'inputs':row['inputs']}}
        receipt['nonweaponIconRevision'] = {'ledger':str(LEDGER.relative_to(ROOT)), 'sha256':hashlib.sha256(text.encode()).hexdigest(),
                                           'scope':'Eight original studio icons; helmet and paired boot display models unchanged. Native UI-state evidence pending.'}
        # All identity, source, route, baseline and PNG checks complete before the first write.
        for source,destination in copies: destination.write_bytes(source.read_bytes())
        LEDGER.write_text(text)
        path.write_text(json.dumps(receipt,indent=2)+'\n')
    for package in ALLOWED:
        validate_package_icons(package,json.loads((HERE/package/'content.json').read_text())['entries'])
    print('PASS: 29 exact nonweapon icons; existing paths, display inputs and prohibited icon categories preserved.')


if __name__ == '__main__': main()
