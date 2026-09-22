#!/usr/bin/env python3
"""Summarize existing fixture snapshots; never connect to or modify the game."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = ROOT / 'marketplace/packages/paladin'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_snapshot(path):
    raw = path.read_text()
    result = json.loads(raw[raw.index('{'):])
    if not result.get('ok'):
        raise ValueError('Failed observation: ' + path.name)
    return result


def package_identity(guid, name, sha):
    return digest((guid + '\n' + name + '\n' + sha).encode())


def assets(package=PACKAGE):
    guid = json.loads((package / 'manifest.json').read_text())['modGuid']
    refs = {}
    def walk(value, row):
        if isinstance(value, dict):
            if 'model' in value:
                refs.setdefault(value['model'], []).append({'row': row, 'assignmentPath': value.get('path')})
            for child in value.values(): walk(child, row)
        elif isinstance(value, list):
            for child in value: walk(child, row)
    for row in json.loads((package / 'content.json').read_text())['entries']:
        walk(row, row['id'])
    result = {}
    for file in sorted((package / 'assets').glob('*.glb')):
        name = file.relative_to(package).as_posix()
        sha = digest(file.read_bytes())
        result[package_identity(guid, name, sha)] = {'asset': name, 'sha256': sha, 'assignments': refs.get(name, [])}
    return result


def summarize(renderer_path, table):
    base = renderer_path.name.removesuffix('-renderers.txt')
    parent = renderer_path.parent
    guardian_path = parent / (base + '-guardian-state.txt')
    equipment_path = parent / (base + '-equipment-inventory.txt')
    render = read_snapshot(renderer_path)
    guardian, equipment = read_snapshot(guardian_path), read_snapshot(equipment_path)
    if len({v.get('session') for v in [render, guardian, equipment]}) != 1:
        raise ValueError('Mismatched observation sessions: ' + base)
    heroes = {h['dummyInstanceId']: h for h in guardian['heroes']}
    gear = {h['heroInstanceId']: h for h in equipment['heroes']}
    result = []
    for dummy, hero in sorted(heroes.items()):
        observations = []
        for renderer in render['renderers']:
            if renderer['ownerInstanceId'] != dummy: continue
            mesh = renderer.get('mesh') or ''
            identity = mesh.split('package-model:', 1)[1] if 'package-model:' in mesh else None
            resolved = table.get(identity)
            observations.append({'rendererPath': renderer['celRelativeRendererPath'], 'meshName': mesh,
                                 'asset': resolved, 'active': renderer['active'], 'enabled': renderer['enabled'],
                                 'isVisibleAtCapture': renderer['isVisible'], 'rendererKind': renderer['rendererKind']})
            if identity and resolved is None:
                raise ValueError('Unknown pinned-package model identity: ' + identity)
        equipped = [slot for slot in gear[hero['heroInstanceId']]['slots'] if slot['slot'] in ['Body','Foot','Head','LeftHand','RightHand']]
        result.append({'identity': hero['identity'], 'classKey': hero['classKey'], 'dummyInstanceId': dummy,
                       'heroInstanceId': hero['heroInstanceId'], 'equippedSlots': equipped, 'renderers': observations})
    return {'case': base, 'session': render['session'], 'scope': render['scope'],
            'frames': {'renderers': render['frame'], 'guardian': guardian['frame'], 'equipment': equipment['frame']},
            'inputs': {p.name: digest(p.read_bytes()) for p in [renderer_path, guardian_path, equipment_path]}, 'heroes': result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshots', type=Path, default=ROOT / 'scratch')
    parser.add_argument('--prefix', default='paladin-gear')
    parser.add_argument('--package', type=Path, default=PACKAGE)
    parser.add_argument('--expected-session')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--fixture-receipt', type=Path, default=ROOT / 'scratch/paladin-gear-game/gear-fixture-source-receipt.json')
    args = parser.parse_args()
    output = args.output.absolute()
    if not output.resolve().is_relative_to(ROOT / 'scratch') or output.exists():
        raise ValueError('Output must be a new file under scratch.')
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]*', args.prefix):
        raise ValueError('Invalid capture prefix')
    package = args.package.resolve()
    table = assets(package)
    fixture_check = json.loads(args.fixture_receipt.read_text())
    if digest((package / 'content.json').read_bytes()) not in [fixture_check['fixtureFiles']['content.json'], fixture_check['sourceFiles']['content.json']]:
        raise ValueError('Lookup content does not match pinned fixture or its original production source')
    for record in table.values():
        original = fixture_check['sourceFiles'].get(record['asset'])
        if original is not None and original != record['sha256']:
            raise ValueError('Current asset changed since fixture source: ' + record['asset'])
    cases = []
    for path in sorted(args.snapshots.glob(args.prefix + '-*-*h-renderers.txt')):
        case = summarize(path, table)
        if args.expected_session and case['session'] != args.expected_session:
            raise ValueError('Unexpected snapshot session: ' + case['session'])
        cases.append(case)
    if not cases:
        raise ValueError('No complete capture cases found for prefix ' + args.prefix)
    fixture = json.loads(args.fixture_receipt.read_text())
    report = {'fixtureReceiptSha256': digest(args.fixture_receipt.read_bytes()),
              'observedFixtureContentSha256': fixture['fixtureFiles']['content.json'],
              'fixtureProductionSourceContentSha256': fixture['sourceFiles']['content.json'],
              'schema': 'ftkmf.paladin-gear-observations.v1', 'cases': cases,
              'lookupContentSha256': digest((package / 'content.json').read_bytes()),
              'scope': 'Read-only summaries of fixture-assisted combat equipment. Package mesh identities match the pinned lookup package bytes; this does not prove visual fit, animation, break behavior, native action costs, acquisition or disposal.',
              'limitations': ['All 36 items were granted by the starting-inventory fixture.',
                              'Equipment actions during combat bypass ordinary equipment action costs.',
                              'Snapshots are sequential, not atomic. Frames and source hashes are retained.',
                              'Disabled fragments prove assignment only, not visible break behavior.',
                              'Sex coverage follows actual body asset names; preview labels alone do not establish body selection.']}
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'cases': len(cases), 'output': str(output.relative_to(ROOT))}))


if __name__ == '__main__': main()
