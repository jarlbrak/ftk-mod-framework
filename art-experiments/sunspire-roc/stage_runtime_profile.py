#!/usr/bin/env python3
"""Append the frozen Sunspire Roc profile to a fresh isolated test-catalog stage."""
import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

import jsonschema


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repo', type=Path)
parser.add_argument('--game-root', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
REPO = args.repo.resolve() if args.repo else next(
    path for path in Path(__file__).resolve().parents
    if (path / 'FTKModFramework').is_dir() and (path / 'tools').is_dir()
)
ART = REPO / 'art-experiments/sunspire-roc'
GAME = args.game_root.resolve()
OUT = args.output.resolve()
assert not OUT.exists(), 'Existing output refused: preserve previous stage evidence and choose a new path.'
assert GAME.is_dir() and not GAME.is_symlink()
manifest = read(ART / 'manifest.json')
assert manifest['status'] in {
    'FROZEN_OFFLINE_ORIGINAL_CANDIDATE_LIVE_VALIDATION_PENDING',
    'FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE',
}
for name, expected in manifest['files'].items():
    assert sha(ART / name) == expected, name
catalog = GAME / 'model-test-profiles.json'
models = GAME / 'BepInEx/plugins/FTKModFramework_content/models'
assert catalog.is_file() and models.is_dir()
old = read(catalog)
new = copy.deepcopy(old)
profile = manifest['profile']
assert profile == read(ART / 'runtime-profile.json')['profiles'][0]
assert not any(row['key'] == profile['key'] for row in old['profiles'])
probe = next(row for row in old['profiles'] if row['key'] == 'ftkmf_modeltest_probe_roca')
assert probe['baseEnemy'] == profile['baseEnemy'] == 'rocA'
assert probe['combatProfile'] == profile['combatProfile']
assert probe['renderers'][0]['rendererPath'] == profile['renderers'][0]['rendererPath'] == 'enRoc01'
new['profiles'].append(profile)
jsonschema.validate(new, read(REPO / 'tools/ai-model-pipeline/runtime-test-content/profiles.schema.json'))
prior_assets = {path.name: sha(path) for path in sorted(models.iterdir()) if path.is_file()}
assert not set(manifest['originalAssets']) & set(prior_assets)
OUT.mkdir(parents=True)
shutil.copytree(models, OUT / 'models')
for name, expected in manifest['originalAssets'].items():
    assert sha(ART / name) == expected
    shutil.copy2(ART / name, OUT / 'models' / name)
assert {path.name: sha(path) for path in sorted((OUT / 'models').iterdir()) if path.is_file()} == dict(prior_assets, **manifest['originalAssets'])
(OUT / 'model-test-profiles.json').write_text(json.dumps(new, indent=2) + '\n')
shutil.copy2(__file__, OUT / 'stage-script.py')
for name in ('manifest.json', 'runtime-profile.json', 'studio-review.json', 'original-geometry-proof.json', 'target-binding-proof.json'):
    shutil.copy2(ART / name, OUT / name)
receipt = {
    'status': 'STAGED_NOT_DEPLOYED_LIVE_ORIGINAL_PENDING',
    'sourceCatalogSha256': sha(catalog),
    'catalogSha256': sha(OUT / 'model-test-profiles.json'),
    'priorRows': len(old['profiles']),
    'candidateRows': len(new['profiles']),
    'priorRowsAndTopMetadataDeepEqual': new['profiles'][:-1] == old['profiles'] and {k: v for k, v in new.items() if k != 'profiles'} == {k: v for k, v in old.items() if k != 'profiles'},
    'priorAssetSha256': prior_assets,
    'assetSha256': {path.name: sha(path) for path in sorted((OUT / 'models').iterdir()) if path.is_file()},
    'allPriorAssetsUnchanged': all(sha(OUT / 'models' / name) == digest for name, digest in prior_assets.items()),
    'authoringManifestSha256': sha(ART / 'manifest.json'),
    'authoringFiles': {name: sha(ART / name) for name in ('runtime-profile.json', 'studio-review.json', 'original-geometry-proof.json', 'target-binding-proof.json')},
    'stageScriptSha256': sha(OUT / 'stage-script.py'),
    'newProfile': profile,
    'newAssets': manifest['originalAssets'],
    'limits': manifest['limits'],
}
(OUT / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps({'receipt': str(OUT / 'receipt.json'), 'catalogSha256': receipt['catalogSha256'], 'newProfile': profile['key']}, indent=2))
