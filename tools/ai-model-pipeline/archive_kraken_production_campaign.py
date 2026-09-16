#!/usr/bin/env python3
"""Create one immutable, compressed Kraken production-observation archive."""
import argparse
import gzip
import json
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

from verify_kraken_production_adapter import load_campaign, verify_campaign
from verify_kraken_production_archive import INTEGRITY_SCHEMA, SCHEMA, digest, digest_bytes, verify_archive


def require(condition, message):
    if not condition:
        raise ValueError(message)


def descriptor(path, root):
    return {'path': str(path.relative_to(root)), 'sha256': digest(path)}


def copy(source, destination):
    require(not destination.exists(), 'Refusing to overwrite archive file: ' + str(destination))
    shutil.copyfile(source, destination)


def write(path, value):
    require(not path.exists(), 'Refusing to overwrite archive file: ' + str(path))
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n')


def build(args):
    campaign_path = args.campaign.resolve(strict=True)
    verification_path = args.verification.resolve(strict=True)
    visual_path = args.visual_observation.resolve(strict=True)
    image_path = args.visual_image.resolve(strict=True)
    campaign, reports = load_campaign(campaign_path)
    result = verify_campaign(campaign, reports)
    require(json.loads(verification_path.read_text()) == result,
            'Input verification is not reproducible from the selected campaign')
    visual = json.loads(visual_path.read_text())
    require(visual.get('status') == 'supplemental_still_observed_raw_capture_review_pending' and
            visual.get('image', {}).get('sha256') == digest(image_path),
            'Supplemental visual observation and image differ')
    output = args.output.absolute()
    require(not output.exists() and not output.is_symlink(), 'Archive output already exists')
    require(not any(path.is_symlink() for path in output.parents), 'Symlink output ancestry refused')
    output.mkdir(parents=True)
    copy(campaign_path, output / 'campaign.json')
    receipt_source = campaign_path.parent / campaign['deploymentReceipt']['path']
    copy(receipt_source, output / 'deployment.json')
    copy(verification_path, output / 'verification.json')
    copy(visual_path, output / 'supplemental-visual-observation.json')
    copy(image_path, output / 'gloamfin-live-screenshot.png')
    report_rows = []
    for row, report in zip(campaign['reports'], reports):
        source = campaign_path.parent / row['path']
        encoded = gzip.compress(source.read_bytes(), compresslevel=9, mtime=0)
        destination = output / (row['campaignRun'] + '.json.gz')
        destination.write_bytes(encoded)
        report_rows.append({'campaignRun': row['campaignRun'], 'path': destination.name,
            'sha256': digest_bytes(encoded), 'sourceSha256': row['sha256'],
            'sourceBytes': source.stat().st_size, 'encoding': 'gzip-mtime-zero'})
    validation = {'schema': SCHEMA, 'status': result['status'],
        'route': {'topologyGroup': '6a28ac3cf4523c24', 'sourceKind': 'resource_prefab_override',
                  'nativeEnemy': 'krakenHead', 'resourcePrefab': 'enkrakenhead',
                  'registeredEnemy': 'ftkmf_modeltest_gloamfin_kraken_legacy',
                  'rendererPath': 'krakenHead', 'sourceRendererId': 121260},
        'campaign': descriptor(output / 'campaign.json', output),
        'deploymentReceipt': descriptor(output / 'deployment.json', output),
        'verification': descriptor(output / 'verification.json', output),
        'reports': report_rows,
        'sessions': [row['session'] for row in reports],
        'observed': {'naturalStates': result['observedNaturalStates'],
                     'naturalEdges': result['observedNaturalEdges'],
                     'successfulProficiencies': result['nativeKrakenProficiencies'],
                     'failedProficiencyAttempts': result['nativeKrakenFailedProficiencyAttempts'],
                     'unusedIncomingAttackCalculations': result['unusedIncomingAttackCalculations'],
                     'naturalDeath': True, 'naturalEnemyVictory': True,
                     'naturalResourceTeardownInBothRuns': True},
        'supplementalVisual': {
            'observation': descriptor(output / 'supplemental-visual-observation.json', output),
            'image': descriptor(output / 'gloamfin-live-screenshot.png', output)},
        'limits': [
            'The production observer proves exact-route native behavior and teardown, not final art quality.',
            'The supplemental image is a Discord screenshot composite and cannot prove temporal motion, culling, or portrait behavior.',
            'Canonical candidate coverage still requires a reviewed route archive with explicit appearance, camera, portrait, and progression conclusions.'
        ]}
    write(output / 'validation.json', validation)
    readme = (
        '# Gloamfin Kraken production observer campaign\n\n'
        'This immutable archive preserves two fresh helper sessions for the exact five-bone '
        '`enkrakenhead` resource route. The death run used only ordinary no-focus hero attacks. '
        'The victory run used an ordinary party loss while the Kraken remained alive. Both runs '
        'ended through native controller terminals and natural resource teardown.\n\n'
        'The JSON reports are gzip-compressed with `mtime=0`. `validation.json` records both the '
        'compressed and decompressed hashes. Verify the full archive with:\n\n'
        '```sh\npython3 tools/ai-model-pipeline/verify_kraken_production_archive.py '
        'docs/evidence/kraken-production-adapter-v1\n```\n\n'
        'The included still remains supplemental review-pending evidence. This archive does not '
        'claim final art, continuous visual motion, camera, culling, portrait, or progression acceptance.\n')
    (output / 'README.md').write_text(readme)
    files = [{'path': str(path.relative_to(output)), 'sha256': digest(path)}
             for path in sorted(output.rglob('*')) if path.is_file()]
    integrity = {'schema': INTEGRITY_SCHEMA,
                 'validationSha256': digest(output / 'validation.json'), 'files': files}
    write(output / 'integrity.json', integrity)
    verified = verify_archive(output)
    return output, verified


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--campaign', type=Path, required=True)
    parser.add_argument('--verification', type=Path, required=True)
    parser.add_argument('--visual-observation', type=Path, required=True)
    parser.add_argument('--visual-image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        output, result = build(args)
    except Exception as error:
        print(json.dumps({'status': 'stopped', 'error': str(error)}, indent=2))
        raise SystemExit(1)
    print(json.dumps({'status': result['status'], 'archive': str(output),
                      'validationSha256': digest(output / 'validation.json')}, indent=2))


if __name__ == '__main__':
    main()
