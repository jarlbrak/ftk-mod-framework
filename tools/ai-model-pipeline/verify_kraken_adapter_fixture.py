#!/usr/bin/env python3
"""Check an explicit owned Unity adapter result against independent asset-space algebra."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from audit_kraken_adapter import extract, adapt, models, MAPPING, JAW


def require(condition, message):
    if not condition:
        raise ValueError(message)


def matrix(value):
    result = np.asarray(value, dtype=float)
    require(result.shape == (4, 4) and np.isfinite(result).all(), 'Expected finite 4x4 matrix')
    return result


def compare(assets, sample, request, reference):
    require(request.get('op') == 'kraken-adapter-fixture' and request.get('method') == 'clip-playable', 'Wrong request operation/method')
    require(request.get('clip') in ('krakenAttack', 'krakenIdle', 'krakenDamage', 'krakenDisappear'), 'Unsupported clip')
    require(sample.get('id') == request.get('id') and sample.get('session') == request.get('session') and bool(sample.get('session')), 'Request identity mismatch')
    require(sample.get('ok') is True and sample.get('error') is None, 'Fixture failed')
    require(sample.get('provenance') == 'isolated_old_kraken_adapter_single_clip_diagnostic', 'Wrong fixture provenance')
    require(sample.get('sameReadyAfter') is True and sample.get('pinnedReady', {}).get('session') == sample['session'], 'Ready pin failed')
    cleanup = sample.get('cleanup', {})
    require(all(cleanup.get(k) is True for k in ('graphsDisposedBeforeTargets', 'modernUnityNull', 'oldUnityNull')) and not cleanup.get('errors'), 'Cleanup failed')
    require(reference.get('ok') is True and reference.get('provenance') == 'isolated_controller_free_manual_clip_playable_native_avatar', 'Expected successful original native sample')
    require(reference.get('session') == sample['session'], 'Reference sample must belong to same live process')
    for key in ('controllerName', 'controllerInstanceId', 'clipName', 'clipInstanceId', 'clipLength', 'modernPrefabInstanceId', 'oldPrefabInstanceId'):
        require(sample['identity'].get(key) == reference['identity'].get(key), 'Reference source identity mismatch: ' + key)
    require(sample['identity']['controllerName'] == 'krakenHeadController', 'Wrong native controller')
    checks = sample.get('adapterChecks', {})
    require(checks.get('nonfiniteReadbackRejected') is True and checks.get('injectedCommitRollback') is True and checks.get('singularShearNonfiniteRejected') is True and checks.get('exactSavedLocalsRestored') is True, 'Mechanics checks missing')
    (old, palette, _), (modern, _, _) = extract(assets)
    require(palette == list(MAPPING) + [JAW], 'Unexpected old palette')
    old_models, modern_models = models(old), models(modern)
    rest = sample['adapterRest']
    require(rest.get('capturedBeforeAnimatorInitialization') is True, 'Rest capture missing')
    errors = []
    def check(label, actual, expected):
        error = float(np.max(np.abs(matrix(actual) - matrix(expected))))
        errors.append({'name': label, 'maximumError': error, 'pass': error <= 1e-5})
    for target, source in MAPPING.items():
        check('rest modern ' + source, rest['modernRestModels'][source], modern_models[source])
        check('rest old ' + target, rest['oldRestModels'][target], old_models[target])
    check('rest jaw', rest['jawRestLocal'], old[JAW])
    frames, native = sample['frames'], reference['frames']
    require(1 <= len(frames) <= 32 and len(frames) == len(request['times']) == len(native), 'Sample count mismatch')
    for i, (frame, baseline, time) in enumerate(zip(frames, native, request['times'])):
        require(frame['clip'] == baseline['clip'] == request['clip'], 'Frame clip mismatch')
        require(float(np.float32(time)) == frame['time'] == baseline['time'], 'Exact time mismatch')
        require(frame.get('fullRestResetBeforeSample') is True, 'Missing independent reset')
        for field in ('modernPlayable', 'oldPlayable'):
            for observed in (frame[field], baseline[field]):
                require(observed.get('animatorInitialized') is True and observed.get('runtimeControllerAssigned') is False and observed.get('fireEvents') is False, 'Invalid sampling surface')
        check(str(i) + ' modern root native reference', frame['modernRootAnimated'], baseline['modernRootAnimated'])
        require(set(frame['driverLocals']) == set(baseline['driverLocals']) == set(modern) - {'Root_M'}, 'Driver set mismatch')
        for path in frame['driverLocals']:
            check(str(i) + ' native driver ' + path, frame['driverLocals'][path], baseline['driverLocals'][path])
        drivers = dict(modern)
        drivers.update({path: matrix(value) for path, value in frame['driverLocals'].items()})
        expected_local, desired, _ = adapt(old, modern, matrix(frame['modernRootAnimated']), drivers, matrix(frame['oldRootAnimated']))
        observed = frame['oldAdapter']
        require(observed.get('modelReadbackMethod') == 'actual-local-TRS-chain-excluding-owned-top', 'Explicit local-chain readback required')
        require(all(observed.get(k) is True for k in ('ok', 'sharedRootUnchanged', 'targetIdentitiesUnchanged', 'jawRestLocal', 'sourceSnapshotsBeforeWrites')), 'Adapter invariant missing')
        require(0 <= observed['repeatMaximumLocalError'] <= 1e-5 and 0 <= observed['maximumModelReadbackError'] <= 1e-5, 'Adapter readback/repeat failed')
        require(set(observed['targetLocals']) == set(observed['targetModels']) == set(palette), 'Output palette paths mismatch')
        for path in palette:
            check(str(i) + ' output local ' + path, observed['targetLocals'][path], expected_local[path])
            check(str(i) + ' output model ' + path, observed['targetModels'][path], desired[path])
    return {'status': 'owned_adapter_mechanics_match' if all(row['pass'] for row in errors) else 'owned_adapter_mechanics_mismatch',
            'frames': len(frames), 'checks': errors, 'maximumError': max(row['maximumError'] for row in errors),
            'limitations': 'Supplied owned Unity fixture readback compared to independent asset algebra and same-process original sample. Native-reference capture validation must be pinned separately. No skinned renderer deformation, native events, appearance or crossfade integration claim.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('assets', 'sample', 'request', 'reference', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    require('scratch' in args.output.resolve().parts, 'Derived pose reports must remain in scratch')
    result = compare(args.assets, *[json.loads(getattr(args, name).read_text()) for name in ('sample', 'request', 'reference')])
    result['evidence'] = {name: {'path': str(getattr(args, name).resolve()), 'sha256': hashlib.sha256(getattr(args, name).read_bytes()).hexdigest()} for name in ('assets', 'sample', 'request', 'reference')}
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
    print(result['status'], result['maximumError'])
    raise SystemExit(0 if result['status'] == 'owned_adapter_mechanics_match' else 1)


if __name__ == '__main__':
    main()
