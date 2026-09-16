#!/usr/bin/env python3
"""Stage Mournglass over the pinned 405-row catalog. No deployment or DLL changes."""
import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    art = Path(__file__).resolve().parent
    manifest = art / 'manifest.json'
    assert manifest.is_file(), 'Final reviewed manifest.json required before staging.'
    frozen = json.loads(manifest.read_text())
    files = frozen['files']
    for name, value in files.items():
        expected = value if isinstance(value, str) else value['sha256']
        assert sha(art / name) == expected, name
    assert not args.output.exists(), 'Preserve existing stage; use a fresh output.'
    catalog = args.game_root / 'model-test-profiles.json'
    prior_hash = 'b52196b3c1928f35c76188c17e8b135c5cdbbedfa1fa3ecbc6a484b29acc34d6'
    assert sha(catalog) == prior_hash, 'Rebase required: deployed input catalog changed.'
    doc = json.loads(catalog.read_text())
    assert len(doc['profiles']) == 405
    original = copy.deepcopy(doc['profiles'])
    assets = {'mournglass.glb': 'c1c04fbac1b7ca604f9091148d14b75f48b95b913327e2aeb7404b0986caa029',
              'mournglass.slot0.png': '6d5d0f1e4b179dc017b5800164035108613cc79a3e83e533920694a93414476e',
              'mournglass.slot1.png': '76fde08cf639f6cd1fd078d9943f2470b2c9ea2c0555b4fbe8c8b5f279126186'}
    for name, expected in assets.items():
        assert sha(art / name) == expected and files[name] == expected
    profile = {'key': 'ftkmf_modeltest_mournglass', 'baseEnemy': 'chaosBeast',
               'displayName': 'Mournglass Wraith',
               'combatProfile': '1d2808cbd2bc1cb6f31a3d51b0787cb854efdf316b9ba44fb3b7825899d35d78',
               'renderers': [{'rendererPath': 'enChaosBeast', 'glbFile': 'mournglass.glb',
                              'materialSlots': [{'primitiveIndex': 0, 'nativeMaterialSlot': 0,
                                                 'textureFile': 'mournglass.slot0.png', 'disableNativeEmission': True},
                                                {'primitiveIndex': 1, 'nativeMaterialSlot': 1,
                                                 'textureFile': 'mournglass.slot1.png', 'disableNativeEmission': True}]}],
               'minimumBaseHealth': 64, 'visualScale': 1.0}
    assert not any(p['key'] == profile['key'] for p in original)
    doc['profiles'].append(profile)
    assert doc['profiles'][:-1] == original
    model_dir = args.game_root / 'BepInEx/plugins/FTKModFramework_content/models'
    prior_assets = {p.name: sha(p) for p in model_dir.iterdir() if p.is_file()}
    assert all(not (model_dir / name).exists() for name in assets)
    binaries = {'BepInEx/plugins/FTKModFramework.dll': '19ece392cf3d58fe43b6178733a9a5ecd85c51943f9cd1769060b88fb572b735',
                'BepInEx/plugins/FtkRuntimeModelTestContent.dll': 'aba52487088a1cd05e960eb0a29b56273a231bfc5442afb0506586817a9b3265'}
    for rel, expected in binaries.items():
        assert sha(args.game_root / rel) == expected, rel
    args.output.mkdir(parents=True)
    shutil.copytree(model_dir, args.output / 'models')
    for name in assets:
        shutil.copy2(art / name, args.output / 'models' / name)
    for name, expected in prior_assets.items():
        assert sha(args.output / 'models' / name) == expected
    target = args.output / 'model-test-profiles.json'
    target.write_text(json.dumps(doc, indent=2) + '\n')
    receipt = {'status': 'OFFLINE_STAGED_NOT_DEPLOYED', 'profileCount': 406,
               'catalogSha256': sha(target), 'priorCatalogSha256': prior_hash,
               'prior405ProfilesDeepEqual': True, 'allPriorAssetsByteEqual': True,
               'priorAssetHashes': prior_assets, 'newAssetHashes': assets, 'newProfile': profile,
               'manifestSha256': sha(manifest), 'manifestPath': str(manifest),
               'stageScriptSha256': sha(Path(__file__)), 'requiredBinarySha256': binaries,
               'registrationRoute': 'Content.SetEnemyBodyMeshesFromGlb; bindingKind absent',
               'nativeRootScale': 1.5, 'publicVisualScaleFactor': 1.0,
               'branchWarning': 'This Mournglass406 and legacy-singular406 are separate branches of405. Combining requires rebasing and preserving both rows; never silently replace one with the other.',
               'limits': ['Native scroller/FX preserved by API intent; live two-slot scrolling, colors and ownership remain unverified.',
                          'Offline native-pose review does not establish original live appearance, portraits, combat or disposal.']}
    (args.output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(args.output / 'receipt.json')


if __name__ == '__main__':
    main()
