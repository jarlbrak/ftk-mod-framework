#!/usr/bin/env python3
"""Exercise an unpublished archive through real helper state transitions in scratch."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('helper', 'archive', 'descriptor', 'game-assembly'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--platform', default='macos')
    args = parser.parse_args()
    helper, archive, descriptor = (p.resolve(strict=True) for p in (args.helper, args.archive, args.descriptor))
    metadata = json.loads(descriptor.read_text())
    assert sha(archive) == metadata['sha256']
    output = ROOT / 'scratch' / ('paladin-lifecycle-' + uuid.uuid4().hex)
    output.mkdir(parents=True)
    state = output / 'state'
    request = {'schemaVersion': 1, 'operationId': uuid.uuid4().hex, 'stateRoot': str(state),
               'frameworkVersion': metadata['frameworkVersion'], 'gameAssemblyPath': str(args.game_assembly.resolve(strict=True)),
               'platform': args.platform, 'manualRoots': [], 'manualGuids': [], 'bundledGuids': []}
    events = []

    def invoke(label, op, **changes):
        payload = dict(request, operationId=uuid.uuid4().hex, **changes)
        source, result = output / (label + '.request.json'), output / (label + '.result.json')
        write(source, payload)
        if op == 'fixture':
            command = [str(helper), 'marketplace-fixture', '--archive', str(archive), '--descriptor', str(descriptor)]
        else:
            command = [str(helper), 'marketplace', op]
        run = subprocess.run(command + ['--request', str(source), '--result', str(result)], capture_output=True, text=True)
        assert run.returncode == 0, run.stderr
        value = json.loads(result.read_text())
        assert value['ok'], value
        events.append({'step': label, 'resultSha256': sha(result)})
        return value

    prepared = invoke('01-prepare-install', 'fixture')
    assert prepared['pending'] and not prepared.get('active')
    # The fixture catalog is confined to its fresh scratch state, never production.
    write(state / 'catalog.json', {'schemaVersion': 1, 'packages': [metadata]})
    initial = invoke('02-activate', 'activate')
    assert initial['active'] and not initial.get('pending')
    first = initial['active']['generationId']
    with zipfile.ZipFile(archive) as zipped:
        expected_files = {entry.filename: zipped.read(entry) for entry in zipped.infolist() if not entry.is_dir()}
    assert len(expected_files) == metadata['fileCount']
    inventories = []

    def verify_content(label, value, enabled):
        active = value['active']
        assert len(active['packages']) == 1
        package = active['packages'][0]
        assert package['packageId'] == metadata['packageId'] and package['sha256'] == metadata['sha256']
        assert package['enabled'] is enabled
        content = Path(active['contentRoot'])
        actual = {path.relative_to(content).as_posix(): path.read_bytes()
                  for path in content.rglob('*') if path.is_file()}
        expected = {metadata['packageId'] + '/' + name: data for name, data in expected_files.items()}
        assert actual == expected, label + ': installed file inventory or bytes differ'
        inventories.append({'step': label, 'exactArchiveFiles': len(actual), 'enabled': enabled})

    verify_content('02-activate', initial, True)

    def prepare(label, selection):
        plan = invoke(label + '-plan', 'prepare', selection=selection, dryRun=True)
        value = invoke(label, 'prepare', selection=selection, expectedRevision=plan['planRevision'])
        assert value['pending']
        return value

    disabled = [{'packageId': metadata['packageId'], 'version': metadata['version'], 'enabled': False}]
    prepare('03-disable', disabled)
    off = invoke('04-activate-disabled', 'activate')
    verify_content('04-activate-disabled', off, False)
    enabled = [dict(disabled[0], enabled=True)]
    prepare('05-enable', enabled)
    on = invoke('06-activate-enabled', 'activate')
    verify_content('06-activate-enabled', on, True)
    restored_generation = on['active']['generationId']
    prepare('07-remove', [])
    removed = invoke('08-activate-removed', 'activate')
    assert not removed['active']['packages']
    assert not any(path.is_file() for path in Path(removed['active']['contentRoot']).rglob('*'))
    rollback = invoke('09-rollback', 'rollback')
    assert rollback['pending']['generationId'] == restored_generation
    restored = invoke('10-activate-rollback', 'activate')
    assert restored['active']['generationId'] == restored_generation
    verify_content('10-activate-rollback', restored, True)
    exported = invoke('11-export', 'export')
    export_path = Path(exported['exportPath'])
    assert json.loads(export_path.read_text())['active'] == restored['active']
    receipt = {'scope': 'Real helper CLI lifecycle in isolated scratch state; no game, UI, save or online proof.',
               'archiveSha256': sha(archive), 'helperSha256': sha(helper), 'initialGeneration': first,
               'fileCount': metadata['fileCount'], 'steps': events, 'status': 'passed',
               'byteVerification': inventories, 'removedContentEmpty': True,
               'export': {'format': 'Managed-set JSON manifest, not a package ZIP',
                          'activeSetMatches': True, 'sha256': sha(export_path)}}
    write(output / 'receipt.json', receipt)
    print('PASS package activation, byte inventory, disable, enable, remove, rollback and export:', output / 'receipt.json')


if __name__ == '__main__':
    main()
