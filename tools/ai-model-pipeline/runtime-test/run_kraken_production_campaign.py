#!/usr/bin/env python3
"""Assemble existing two-owner Kraken reports offline; automatic combat driving is retired."""
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

PIPELINE = Path(__file__).resolve().parents[1]
if str(PIPELINE) not in sys.path:
    sys.path.insert(0, str(PIPELINE))

from run_case import digest, read
from verify_kraken_production_adapter import (
    BINARY_KEYS, CAMPAIGN_SCHEMA, GLB_SHA256, MANIFEST_SHA256, PNG_SHA256,
    REGISTERED_ENEMY, load_campaign, verify_campaign,
)


ROLES = {
    'native-combat-death': {'companion': None, 'expectedTerminal': 'DEATH'},
    'enemy-victory-terminal': {'companion': 'ogreA', 'expectedTerminal': 'VICTORY'},
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def safe_output(path, parent=None):
    path = path.absolute()
    if any(part.is_symlink() for part in (path, *path.parents)):
        raise RuntimeError('Symlink output ancestry refused: ' + str(path))
    if parent is not None:
        parent = parent.resolve(strict=True)
        require(path.parent.resolve(strict=True) == parent,
                'Output must be a direct child of the selected campaign directory')
    require(not path.exists(), 'Refusing to overwrite output: ' + str(path))
    return path


def exact_json(path, value):
    with path.open('x') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write('\n')


def copy_exact(source, destination):
    source = source.resolve(strict=True)
    safe_output(destination)
    before = source.stat()
    data = source.read_bytes()
    after = source.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns),
            'Source changed during copy: ' + str(source))
    destination.write_bytes(data)
    require(digest(destination) == hashlib.sha256(data).hexdigest(),
            'Copied bytes changed: ' + str(destination))


def target(state, enemy_fid):
    return next((row for row in (state.get('combat') or {}).get('enemies') or []
                 if row.get('fid') == enemy_fid), None)


def ordinary_attack_result(result, enemy_fid):
    body = result.get('result', result)
    require(result.get('ok') is True and body.get('committed') == 'Attack' and
            body.get('target') == enemy_fid and body.get('whoseTurnWas') == 'player',
            'Bridge did not confirm one ordinary no-focus attack on the pinned Kraken')


CAMPAIGN_GUIDANCE = ("Automated Kraken combat driving is retired with the direct-action bridge. "
    "Prepare and operate offline gameplay through harness ftk_ui/ftk_input; "
    "retain passive helper observation and use assemble for existing independently verified reports.")


class KrakenRun:
    def __init__(self, args):
        raise ValueError(CAMPAIGN_GUIDANCE)

    def run_campaign(self):
        raise ValueError(CAMPAIGN_GUIDANCE)


def contained_copy(source, campaign_dir, label):
    source = source.resolve(strict=True)
    sha = digest(source)
    if source.parent == campaign_dir:
        return source, sha
    destination = campaign_dir / (label + '-' + sha[:16] + source.suffix)
    copy_exact(source, destination)
    return destination, sha


def assemble(args):
    campaign = safe_output(args.campaign)
    campaign.parent.mkdir(parents=True, exist_ok=True)
    campaign_dir = campaign.parent.resolve(strict=True)
    verification = safe_output(args.verification, campaign_dir)
    death_source = args.death_report.resolve(strict=True)
    victory_source = args.victory_report.resolve(strict=True)
    reports = [read(death_source), read(victory_source)]
    require([row.get('route', {}).get('campaignRun') for row in reports] ==
            ['native-combat-death', 'enemy-victory-terminal'],
            'Report arguments do not match their exact campaign roles')
    sessions = [row.get('session') for row in reports]
    require(all(isinstance(value, str) and re.fullmatch('[a-f0-9]{32}', value) for value in sessions) and
            len(set(sessions)) == 2, 'Two distinct fresh helper sessions are required')
    pin_names = {'framework': 'framework', 'runtimeHelper': 'runtimeHelper', 'runtimeContent': 'runtimeContent'}
    expected = {}
    for campaign_key, report_key in pin_names.items():
        values = [(row.get('pinsAtArm') or {}).get(report_key, {}).get('assemblyFileSha256') for row in reports]
        require(len(set(values)) == 1 and isinstance(values[0], str) and re.fullmatch('[a-f0-9]{64}', values[0]),
                'Report binary pins differ: ' + campaign_key)
        expected[campaign_key] = values[0]
    require(set(expected) == BINARY_KEYS, 'Exact campaign binary pins are required')
    receipt_path, receipt_sha = contained_copy(args.deployment_receipt, campaign_dir, 'deployment')
    death_path, death_sha = contained_copy(death_source, campaign_dir, 'native-combat-death')
    victory_path, victory_sha = contained_copy(victory_source, campaign_dir, 'enemy-victory-terminal')
    document = {'schema': CAMPAIGN_SCHEMA, 'expectedBinaries': expected,
        'deploymentReceipt': {'path': receipt_path.name, 'sha256': receipt_sha},
        'reports': [
            {'campaignRun': 'native-combat-death', 'path': death_path.name, 'sha256': death_sha},
            {'campaignRun': 'enemy-victory-terminal', 'path': victory_path.name, 'sha256': victory_sha},
        ]}
    exact_json(campaign, document)
    loaded, loaded_reports = load_campaign(campaign)
    result = verify_campaign(loaded, loaded_reports)
    exact_json(verification, result)
    return {'status': result['status'], 'campaign': str(campaign),
            'campaignSha256': digest(campaign), 'verification': str(verification),
            'verificationSha256': digest(verification), 'sessions': sessions}


def parser():
    value = argparse.ArgumentParser(description=__doc__)
    commands = value.add_subparsers(dest='command', required=True)
    commands.add_parser('run', help='Retired; use the native-input harness')
    package = commands.add_parser('assemble', help='Build and verify one two-session offline campaign')
    package.add_argument('--death-report', type=Path, required=True)
    package.add_argument('--victory-report', type=Path, required=True)
    package.add_argument('--deployment-receipt', type=Path, required=True)
    package.add_argument('--campaign', type=Path, required=True)
    package.add_argument('--verification', type=Path, required=True)
    return value


def main():
    value = parser()
    args, unknown = value.parse_known_args()
    if args.command == 'run':
        value.error(CAMPAIGN_GUIDANCE)
    else:
        if unknown: value.error("unrecognized arguments: " + " ".join(unknown))
        try:
            result = assemble(args)
        except Exception as error:
            print(json.dumps({'status': 'stopped', 'error': str(error)}, indent=2))
            raise SystemExit(1)
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
