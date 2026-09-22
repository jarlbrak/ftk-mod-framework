#!/usr/bin/env python3
"""Create a never-publish starting-inventory fixture in a new scratch directory."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'marketplace/packages/paladin'
HUNTER = 'paladin_gear_fixture_hunter'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fixture_content(source):
    result = copy.deepcopy(source)
    rows = result['entries']
    gear = sorted(row['id'] for row in rows if row['kind'] in ('weapon', 'item'))
    assert len(gear) == len(set(gear)) == 51
    assert len(rows) == 54 and not any(row['id'] == HUNTER for row in rows)
    paladin = next(row for row in rows if row['id'] == 'paladin')
    # The production Paladin deliberately uses native FTK bodies, faces and hair.
    # This visual/equipment fixture must never revive the superseded body override route.
    assert 'playerModels' not in paladin
    previous = copy.deepcopy(paladin['fields']['startitems'])
    paladin['fields']['startitems'] = gear[:]
    rows.append({'kind': 'class', 'id': HUNTER, 'template': 'hunter',
                 'displayName': 'Gear Fixture Hunter', 'guardian': False,
                 'fields': {'dlc': 'None', 'startitems': gear[:]}})
    assert all(any(row['id'] == key and row['kind'] in ('weapon', 'item') for row in rows) for key in gear)
    return result, gear, previous


def build(output):
    output = output.absolute()
    scratch = (ROOT / 'scratch').resolve(strict=True)
    if output.parent.resolve(strict=True) != scratch or output.exists() or output.is_symlink():
        raise ValueError('Output must be a new directory directly under repository scratch; existing output is never overwritten.')
    paths = [SOURCE / 'manifest.json', SOURCE / 'content.json'] + sorted((SOURCE / 'assets').iterdir())
    if any(path.is_symlink() or not path.is_file() for path in paths):
        raise ValueError('Runtime sources must be ordinary files.')
    source_hashes = {path.relative_to(SOURCE).as_posix(): sha(path) for path in paths}
    content, gear, old = fixture_content(json.loads((SOURCE / 'content.json').read_text()))
    package = output / 'package'
    package.mkdir(parents=True)
    for path in paths:
        relative = path.relative_to(SOURCE)
        target = package / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
    (package / 'content.json').write_text(json.dumps(content, indent=2) + '\n')
    copied_hashes = {name: sha(package / name) for name in source_hashes}
    assert [name for name in source_hashes if copied_hashes[name] != source_hashes[name]] == ['content.json']
    assert all(sha(SOURCE / name) == digest for name, digest in source_hashes.items())
    receipt = {'schema': 'ftkmf.paladin-gear-fixture.v1', 'neverPublish': True,
               'scope': 'Starting-inventory grants for visual/equipment fixtures only; not normal acquisition, balance, save compatibility or live proof.',
               'generatorSha256': sha(Path(__file__)), 'sourceFiles': source_hashes,
               'fixtureFiles': copied_hashes, 'assetFilesUnchanged': len(paths) - 2,
               'contentDelta': [{'path': 'entries[id=paladin].fields.startitems', 'before': old, 'after': gear},
                                {'addedClass': HUNTER, 'template': 'hunter', 'guardian': False, 'startitems': gear}],
               'rowCount': len(content['entries']), 'packageModGuidPreserved': 'com.ftkmf.paladin'}
    (output / 'receipt.json').write_text(json.dumps(receipt, indent=2, sort_keys=True) + '\n')
    (output / 'NEVER-PUBLISH.txt').write_text('TEST FIXTURE ONLY. All 51 gear items are starting inventory grants. Use only as the sole Paladin package in an isolated new-save trial. Never publish or install alongside the production package.\n')
    print(json.dumps({'package': str(package.relative_to(ROOT)), 'receipt': str((output / 'receipt.json').relative_to(ROOT)),
                      'rows': len(content['entries']), 'unchangedAssets': len(paths)-2,
                      'contentSha256': copied_hashes['content.json']}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    build(parser.parse_args().output)
