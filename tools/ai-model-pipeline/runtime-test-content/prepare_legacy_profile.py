#!/usr/bin/env python3
"""Append an opt-in singular+tint Reefstrider profile; never write the source catalog/assets."""
import argparse
import copy
import hashlib
import json
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog', type=Path, required=True)
    parser.add_argument('--models', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Output directory already exists; preserve previous candidate.')
    source = json.loads(args.catalog.read_text())
    rows = source['profiles']
    matches = [p for p in rows if p['key'] == 'ftkmf_modeltest_reefstrider']
    assert len(matches) == 1 and len(rows) < 512
    row = copy.deepcopy(matches[0])
    assert row['baseEnemy'] == 'fishA01' and len(row['renderers']) == 1
    body = row['renderers'][0]
    assert body['rendererPath'] == 'enFishA' and 'materialSlots' not in body
    assert body['glbFile'] == 'reefstrider.glb' and body['textureFile'] == 'reefstrider_basecolor.png'
    pins = {'reefstrider.glb': 'a865a3f602edc56c1f9545bc78d4f4b446d891228c70731ae4fc849257a6451d',
            'reefstrider_basecolor.png': '09c706dc70696ffd7ddba05305205c8db1660969e0da59d3e335a34ecbf75b50'}
    for name, expected in pins.items():
        assert sha(args.models / name) == expected, name
    row['key'] = 'ftkmf_modeltest_reefstrider_legacy'
    assert not any(p['key'] == row['key'] for p in rows)
    row['displayName'] = 'Reefstrider Legacy Lifetime Fixture'
    row['bindingKind'] = 'legacy-singular'
    row['tint'] = [0.85, 0.95, 1.0, 1.0]
    body.pop('disableNativeEmission', None)
    for field in ('resourcePrefab', 'portraitMarkerPath'):
        assert field not in row
    output = copy.deepcopy(source)
    output['profiles'].append(row)
    assert output['profiles'][:-1] == rows
    args.output.mkdir(parents=True)
    catalog = args.output / 'model-test-profiles.json'
    catalog.write_text(json.dumps(output, indent=2) + '\n')
    receipt = {'status': 'STAGED_NOT_DEPLOYED_LIVE_VALIDATION_PENDING',
               'sourceCatalog': str(args.catalog.resolve()), 'sourceCatalogSha256': sha(args.catalog),
               'catalogSha256': sha(catalog), 'priorRows': len(rows), 'candidateRows': len(output['profiles']),
               'priorRowsDeepEqual': True, 'addedProfile': row, 'existingAssetSha256': pins,
               'assetsCopiedOrModified': False,
               'registrationApis': ['Content.SetEnemyVisual', 'Content.SetEnemyBodyMeshFromGlb'],
               'rendererPathRole': 'expected_observation_only_not_api_target',
               'limitations': ['Native emission is retained; appearance differs from explicit emission-opt-out example.',
                              'Runtime selected renderer, ownership, native HUD clones and final disposal unverified.']}
    (args.output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(args.output / 'receipt.json')


if __name__ == '__main__':
    main()
