#!/usr/bin/env python3
"""Explicit hashed cross-process pose bridge. Does not relax the adapter verifier."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from audit_kraken_adapter import models, MAPPING, JAW
from verify_kraken_adapter_fixture import require, matrix
from compare_kraken_native import run as verify_historical

MODERN = {'Root_M/base', *MAPPING.values(), 'Root_M/base/body/neck/eye'}
OLD = {*MAPPING, JAW}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read_pin(pin):
    path = Path(pin['path']).resolve()
    require(isinstance(pin.get('sha256'), str) and len(pin['sha256']) == 64 and digest(path) == pin['sha256'], 'Evidence hash mismatch: ' + str(path))
    return path


def compare_poses(fresh, historical, request, source_hash, historical_source_hash):
    require(source_hash == historical_source_hash, 'Source asset hash mismatch')
    require(fresh.get('session') and historical.get('session') and fresh['session'] != historical['session'], 'Bridge requires explicitly different process sessions')
    require(request.get('op') == 'kraken-sample-fixture' and request.get('method') == 'clip-playable', 'Wrong fresh request operation/method')
    require(request.get('session') == fresh['session'] and request.get('id') == fresh.get('id') and bool(fresh.get('id')), 'Fresh request identity mismatch')
    clip = request.get('clip')
    require(clip in ('krakenIdle', 'krakenAttack', 'krakenDamage', 'krakenDisappear'), 'Unsupported bridge clip')
    for sample in (fresh, historical):
        require(sample.get('ok') is True and sample.get('error') is None, 'Failed native sample')
        require(sample.get('provenance') == 'isolated_controller_free_manual_clip_playable_native_avatar' and sample.get('samplingMethod') == 'clip-playable', 'Unexpected native sample provenance')
        require(sample.get('coordinateConvention') == 'independent-modern-root-and-local-drivers' and sample.get('appearanceSamplesOnly') is False, 'Wrong sample coordinates/scope')
        require(sample['identity'].get('clipName') == clip and sample['identity'].get('controllerName') == 'krakenHeadController', 'Clip/controller mismatch')
        require(sample['identity'].get('modernSource') == 'native-enemy:krakenHead' and sample['identity'].get('oldSource') == 'Resources:enkrakenhead', 'Wrong native source')
        cleanup = sample.get('cleanup', {})
        require(all(cleanup.get(k) is True for k in ('graphsDisposedBeforeTargets', 'modernUnityNull', 'oldUnityNull')) and not cleanup.get('errors'), 'Sample cleanup failed')
    for key in ('clipLength', 'modernTransformCount', 'oldTransformCount'):
        require(fresh['identity'].get(key) == historical['identity'].get(key), 'Source structural identity mismatch: ' + key)
    length = fresh['identity']['clipLength']
    require(np.isfinite(length) and 0 < length <= 60, 'Invalid clip duration')
    fresh_frames, old_frames, times = fresh['frames'], historical['frames'], request['times']
    require(1 <= len(times) <= 32 and len(times) == len(fresh_frames) == len(old_frames), 'Exact sample count mismatch')
    checks = []
    def check(name, a, b):
        error = float(np.max(np.abs(matrix(a) - matrix(b))))
        checks.append({'name': name, 'maximumError': error, 'pass': error <= 1e-5})
    for index, (a, b, time) in enumerate(zip(fresh_frames, old_frames, times)):
        require(np.isfinite(time) and 0 <= time <= length and float(np.float32(time)) == a['time'] == b['time'], 'Exact clip time mismatch')
        for frame in (a, b):
            require(frame.get('clip') == clip and frame.get('fullRestResetBeforeSample') is True and frame.get('samplingMethod') == 'clip-playable', 'Frame clip/reset mismatch')
            require(set(frame['driverLocals']) == MODERN and set(frame['oldTargetLocals']) == OLD, 'Exact transform path set mismatch')
            for key in ('modernPlayable', 'oldPlayable'):
                view = frame[key]
                require(all(view.get(k) is True for k in ('animatorEnabled', 'animatorInitialized', 'avatarValid', 'graphValid')), 'Inactive/uninitialized playable')
                require(all(view.get(k) is False for k in ('runtimeControllerAssigned', 'fireEvents', 'applyRootMotion', 'footIK')), 'Unexpected gameplay/IK effects')
                require(view.get('graphUpdateMode') == 'Manual' and view.get('playableCount') == view.get('outputCount') == 1, 'Unexpected graph shape')
                require(view.get('avatarName') == 'enKrakenHeadAvatar', 'Wrong Avatar')
        for root, locals_key in (('modernRootAnimated', 'driverLocals'), ('oldRootAnimated', 'oldTargetLocals')):
            check(str(index) + ':' + root, a[root], b[root])
            a_local = {'Root_M': matrix(a[root])}; b_local = {'Root_M': matrix(b[root])}
            for path in a[locals_key]:
                check(str(index) + ':local:' + path, a[locals_key][path], b[locals_key][path])
                a_local[path] = matrix(a[locals_key][path]); b_local[path] = matrix(b[locals_key][path])
            a_models, b_models = models(a_local), models(b_local)
            for path in a_models:
                check(str(index) + ':prefab-root-model:' + path, a_models[path], b_models[path])
    return {'status': 'cross_process_pose_bridge_match' if all(x['pass'] for x in checks) else 'cross_process_pose_bridge_mismatch',
            'clip': clip, 'historicalSession': historical['session'], 'freshSession': fresh['session'], 'frames': len(times),
            'sourceAssetSha256': source_hash, 'tolerance': 1e-5, 'maximumError': max(x['maximumError'] for x in checks), 'checks': checks}


def run(manifest_path):
    manifest = json.loads(Path(manifest_path).read_text())
    require(manifest.get('schema') == 'kraken-native-cross-process-bridge-v1', 'Explicit bridge schema required')
    names = ('assets', 'historicalManifest', 'historicalComparison', 'historicalSample', 'freshSample', 'freshRequest')
    require(set(manifest.get('evidence', {})) == set(names), 'Exact named evidence pins required')
    paths = {name: read_pin(manifest['evidence'][name]) for name in names}
    docs = {name: json.loads(path.read_text()) for name, path in paths.items() if name != 'assets'}
    previous = docs['historicalComparison']
    require(previous.get('status') in ('reference_match', 'prefix_reference_match'), 'Historical native reference did not pass')
    require(previous.get('manifestSha256') == digest(paths['historicalManifest']), 'Historical manifest pin mismatch')
    require(previous['evidence']['assets']['sha256'] == digest(paths['assets']), 'Historical/current source asset mismatch')
    historical = docs['historicalSample']; clip = historical['identity']['clipName']
    matching = [x for x in previous['comparisons'] if x['clip'] == clip]
    require(len(matching) == 1 and matching[0].get('pass_') is True and matching[0]['sampleSha256'] == digest(paths['historicalSample']), 'Historical sample was not verified by supplied comparison')
    # Revalidate original capture/profile/controller/binary chain; successful report text alone is insufficient.
    replay = verify_historical(paths['historicalManifest'], [paths['historicalSample']])
    require(replay['session'] == previous['session'] == historical['session'] and replay['sourceControllerId'] == previous['sourceControllerId'] == 5973
            and replay['sourceRendererId'] == previous['sourceRendererId'] == 121035, 'Historical source provenance mismatch')
    require(len(replay['comparisons']) == 1 and replay['comparisons'][0]['pass_'] is True, 'Historical native comparison no longer passes')
    replay_item, previous_item = dict(replay['comparisons'][0]), dict(matching[0])
    replay_item.pop('samplePath'); previous_item.pop('samplePath')
    require(replay_item == previous_item, 'Historical comparison results changed')
    result = compare_poses(docs['freshSample'], historical, docs['freshRequest'], digest(paths['assets']), previous['evidence']['assets']['sha256'])
    result.update(manifestSha256=digest(manifest_path), evidence=manifest['evidence'], historicalReferenceStatus=previous['status'], historicalCaptureBoundary=replay['captureBoundary'],
                  limitations='Offline pose equivalence across explicitly different sessions, with hashed files and replayed historical native reference validation. Fresh loaded-process/deployment provenance is recorded separately by the parent. Historical timestamps alone are not a reference chain. Does not establish transitions, events, skin deformation or complete death coverage; adapter verification still requires its own same-process original sample.')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(); require('scratch' in args.output.resolve().parts, 'Pose reports must stay in scratch')
    result = run(args.manifest)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
    print(result['status'], result['maximumError'])
    raise SystemExit(0 if result['status'] == 'cross_process_pose_bridge_match' else 1)


if __name__ == '__main__':
    main()
