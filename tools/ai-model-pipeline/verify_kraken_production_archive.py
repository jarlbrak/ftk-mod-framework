#!/usr/bin/env python3
"""Verify one immutable Gloamfin Kraken production-observation archive."""
import argparse
import gzip
import hashlib
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

from verify_kraken_production_adapter import verify_campaign

SCHEMA = 'ftkmf.kraken-production-adapter-evidence.v1'
INTEGRITY_SCHEMA = 'ftkmf.kraken-production-adapter-archive-integrity.v1'
RUNS = ('native-combat-death', 'enemy-victory-terminal')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()


def digest(path):
    return digest_bytes(path.read_bytes())


def child(root, value):
    require(isinstance(value, str), 'Relative archive path required')
    relative = Path(value)
    require(not relative.is_absolute() and '..' not in relative.parts, 'Contained archive path required')
    path = root / relative
    require(path.is_file() and not path.is_symlink(), 'Missing or symlink archive file: ' + value)
    return path


def descriptor(root, value, fields=('path', 'sha256')):
    require(isinstance(value, dict) and set(value) == set(fields), 'Exact archive descriptor required')
    path = child(root, value['path'])
    require(digest(path) == value['sha256'], 'Archive digest mismatch: ' + value['path'])
    return path


def compressed_report(root, value):
    fields = {'campaignRun', 'path', 'sha256', 'sourceSha256', 'sourceBytes', 'encoding'}
    require(isinstance(value, dict) and set(value) == fields and value.get('campaignRun') in RUNS and
            value.get('encoding') == 'gzip-mtime-zero', 'Exact compressed report descriptor required')
    path = child(root, value['path'])
    encoded = path.read_bytes()
    require(digest_bytes(encoded) == value['sha256'], 'Compressed report digest mismatch')
    decoded = gzip.decompress(encoded)
    require(len(decoded) == value['sourceBytes'] and digest_bytes(decoded) == value['sourceSha256'],
            'Decompressed report identity mismatch')
    report = json.loads(decoded)
    require(report.get('route', {}).get('campaignRun') == value['campaignRun'], 'Report role mismatch')
    return report


def verify_integrity(root):
    path = child(root, 'integrity.json')
    value = json.loads(path.read_text())
    require(value.get('schema') == INTEGRITY_SCHEMA and isinstance(value.get('files'), list),
            'Archive integrity schema required')
    expected = {}
    for row in value['files']:
        require(isinstance(row, dict) and set(row) == {'path', 'sha256'} and row['path'] not in expected,
                'Exact unique integrity file descriptor required')
        expected[row['path']] = row['sha256']
    actual = {str(path.relative_to(root)) for path in root.rglob('*')
              if path.is_file() and path.name != 'integrity.json'}
    require(set(expected) == actual, 'Integrity manifest must cover every archive file exactly once')
    for name, sha in expected.items():
        require(re.fullmatch('[0-9a-f]{64}', sha or '') and digest(child(root, name)) == sha,
                'Integrity file digest mismatch: ' + name)
    return value


def verify_archive(root):
    root = root.resolve(strict=True)
    require(root.is_dir() and not root.is_symlink(), 'Archive directory required')
    integrity = verify_integrity(root)
    validation_path = child(root, 'validation.json')
    validation = json.loads(validation_path.read_text())
    require(validation.get('schema') == SCHEMA and
            validation.get('status') == 'production_observation_campaign_satisfied',
            'Kraken production evidence schema and status required')
    route = validation.get('route')
    require(route == {'topologyGroup': '6a28ac3cf4523c24',
                      'sourceKind': 'resource_prefab_override',
                      'nativeEnemy': 'krakenHead', 'resourcePrefab': 'enkrakenhead',
                      'registeredEnemy': 'ftkmf_modeltest_gloamfin_kraken_legacy',
                      'rendererPath': 'krakenHead', 'sourceRendererId': 121260},
            'Exact Kraken route changed')
    campaign_path = descriptor(root, validation.get('campaign'))
    receipt_path = descriptor(root, validation.get('deploymentReceipt'))
    verification_path = descriptor(root, validation.get('verification'))
    campaign = json.loads(campaign_path.read_text())
    receipt = json.loads(receipt_path.read_text())
    require(receipt.get('status') == 'VERIFIED_COMPLETE', 'Verified deployment receipt required')
    reports = [compressed_report(root, row) for row in validation.get('reports') or []]
    require([row.get('route', {}).get('campaignRun') for row in reports] == list(RUNS),
            'Both ordered campaign roles required')
    sessions = [row.get('session') for row in reports]
    require(len(set(sessions)) == 2 and all(re.fullmatch('[0-9a-f]{32}', value or '') for value in sessions),
            'Two distinct helper sessions required')
    report_descriptors = campaign.get('reports') or []
    require([row.get('campaignRun') for row in report_descriptors] == list(RUNS),
            'Archived campaign report order changed')
    for campaign_row, archive_row in zip(report_descriptors, validation['reports']):
        require(campaign_row.get('sha256') == archive_row['sourceSha256'],
                'Campaign and compressed source report digest differ')
    require(campaign.get('deploymentReceipt', {}).get('sha256') == digest(receipt_path),
            'Campaign and archived deployment receipt differ')
    result = verify_campaign(campaign, reports)
    archived_result = json.loads(verification_path.read_text())
    require(archived_result == result, 'Archived verification output is not reproducible')
    visual = validation.get('supplementalVisual')
    require(isinstance(visual, dict) and set(visual) == {'observation', 'image'},
            'Supplemental visual descriptors required')
    observation_path = descriptor(root, visual['observation'])
    image_path = descriptor(root, visual['image'])
    observation = json.loads(observation_path.read_text())
    require(observation.get('status') == 'supplemental_still_observed_raw_capture_review_pending' and
            observation.get('image', {}).get('sha256') == digest(image_path),
            'Supplemental still identity or limitation changed')
    require(integrity.get('validationSha256') == digest(validation_path),
            'Integrity manifest does not pin validation.json')
    return {'status': 'production_observation_campaign_satisfied_archive_verified',
            'campaignStatus': result['status'], 'sessions': sessions,
            'reportCount': len(reports), 'fileCount': len(integrity['files']),
            'nativeKrakenProficiencies': result['nativeKrakenProficiencies'],
            'nativeKrakenFailedProficiencyAttempts': result['nativeKrakenFailedProficiencyAttempts'],
            'unusedIncomingAttackCalculations': result['unusedIncomingAttackCalculations'],
            'visualStatus': observation['status'], 'limitations': validation.get('limits')}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = verify_archive(args.archive)
    if args.output:
        require(not args.output.exists(), 'Refusing to overwrite verifier output')
        args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
    print(result['status'])


if __name__ == '__main__':
    main()
