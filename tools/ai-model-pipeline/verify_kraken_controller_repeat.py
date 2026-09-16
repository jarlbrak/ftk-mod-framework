#!/usr/bin/env python3
"""Independent fixed-step controller repeat comparison, with descriptive coverage."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
from audit_kraken_adapter import models
from bridge_kraken_native_samples import MODERN, OLD, read_pin
from verify_kraken_adapter_fixture import require, matrix

ASSETS = 'e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117'
ASSEMBLY = '94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8'
SCENARIOS = {'appear': ('Appear', 'OverworldAppear'), 'damaged': ('Damaged', 'DAMAGED'),
             'damaged-heavy': ('DamagedHeavy', 'DAMAGEDHEAVY'), 'death': ('Death', 'DEATH'), 'death-light': ('DeathLight', 'DEATHLIGHT')}
TOLERANCE = 1e-5


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def vector(value, count):
    result = np.asarray(value, float)
    require(result.shape == (count,) and np.isfinite(result).all(), 'Invalid finite vector')
    return result


def trs(local):
    position, rotation, scale = vector(local['position'], 3), vector(local['rotation'], 4), vector(local['scale'], 3)
    require(abs(np.linalg.norm(rotation) - 1) <= TOLERANCE and np.min(scale) > 0, 'Invalid native TRS quaternion/scale')
    result = np.eye(4)
    result[:3, :3] = Rotation.from_quat(rotation).as_matrix() @ np.diag(scale)
    result[:3, 3] = position
    return result


def without_ids(value):
    if isinstance(value, dict):
        return {key: without_ids(item) for key, item in value.items() if key != 'instanceId'}
    if isinstance(value, list):
        return [without_ids(item) for item in value]
    return value


class Checks:
    def __init__(self):
        self.count = 0
        self.maximum = 0.
        self.failures = []

    def numeric(self, name, a, b):
        a, b = np.asarray(a, float), np.asarray(b, float)
        require(a.shape == b.shape and np.isfinite(a).all() and np.isfinite(b).all(), 'Invalid comparison shape/nonfinite: ' + name)
        error = float(np.max(np.abs(a-b))) if a.size else 0.
        self.count += 1
        self.maximum = max(self.maximum, error)
        if error > TOLERANCE:
            self.failures.append({'name': name, 'maximumError': error})

    def structured(self, name, a, b):
        if isinstance(a, dict):
            require(isinstance(b, dict) and set(a) == set(b), 'Metadata keys differ: ' + name)
            for key in a:
                self.structured(name+'/'+key, a[key], b[key])
        elif isinstance(a, list):
            require(isinstance(b, list) and len(a) == len(b), 'Metadata array lengths differ: ' + name)
            for index, (x, y) in enumerate(zip(a, b)):
                self.structured(name+'/'+str(index), x, y)
        elif isinstance(a, bool) or a is None or isinstance(a, str) or isinstance(a, int):
            require(type(a) == type(b) and a == b, 'Exact metadata mismatch: ' + name)
        else:
            self.numeric(name, a, b)


def validate_run(run, request, checks, label, *, bank_intro=False):
    scenarios=dict(SCENARIOS)
    if bank_intro:scenarios["intro"]=("Intro","INTRO")
    count=361 if bank_intro and request.get("scenario")=="intro" else 241
    require(set(request) == {'id', 'session', 'op', 'scenario'} and request['op'] == 'kraken-controller-fixture', 'Exact request schema required')
    require(request['scenario'] in scenarios and run.get('scenario') == request['scenario'], 'Unknown/mismatched scenario')
    require(run.get('id') == request['id'] and run.get('session') == request['session'] and bool(run.get('session')), 'Request identity mismatch')
    require(run.get('ok') is True and run.get('error') is None and run.get('provenance') == 'owned_native_controller_graph_observation_no_adapter', 'Failed/wrong graph capture')
    require(run.get('engineErrors') == [] and run.get('sameReadyAfter') is True and run['pinnedReady']['session'] == run['session'], 'Engine error or Ready pin failure')
    cleanup = run['cleanup']
    require(all(cleanup.get(k) is True for k in ('graphsDisposedBeforeTargets', 'modernUnityNull', 'oldUnityNull')) and cleanup.get('errors') == [], 'Incomplete cleanup')
    identity = run['identity']
    require(identity.get('resourcesSha256') == ASSETS and identity['gameAssembly'].get('assemblyFileSha256') == ASSEMBLY, 'Unsupported source hashes')
    require(identity.get('controllerName') == 'krakenHeadController' and identity.get('sourceControllerId') == 5973, 'Wrong controller')
    require((identity.get('trigger'), identity.get('targetState')) == scenarios[run['scenario']], 'Wrong scenario trigger/state')
    dt = float(np.float32(1/60))
    require(run.get('stepSeconds') == dt and run.get('triggerAfterStep') == 15 and run.get('expectedFrames') == count and len(run['frames']) == count, 'Exact step/horizon contract mismatch')
    previous_frame = -1
    for surface, expected_paths in (('modern', MODERN | {'Root_M'}), ('old', OLD | {'Root_M'})):
        require(run[surface+'Initialization'].get('initializedAfter') is True, 'Graph not initialized')
        rest = run[surface+'Rest']
        require(set(rest['locals']) == set(rest['prefabRootModels']) == expected_paths, 'Rest path set mismatch')
    for index, frame in enumerate(run['frames']):
        require(frame['step'] == index and frame['graphElapsedSeconds'] == index*dt and frame['manualDeltaSeconds'] == (0 if index == 0 else dt)
                and frame['triggerIssued'] is (index > 15), 'Exact step/graph clock/trigger mismatch')
        require(isinstance(frame['unityFrame'], int) and frame['unityFrame'] > previous_frame, 'Unity frame order invalid')
        previous_frame = frame['unityFrame']
        for surface, expected_paths in (('modern', MODERN | {'Root_M'}), ('old', OLD | {'Root_M'})):
            view, initial = frame[surface], run['frames'][0][surface]
            require(all(view.get(k) is True for k in ('animatorInitialized',)) and all(view.get(k) is False for k in ('runtimeControllerAssigned', 'fireEvents', 'applyRootMotion')), 'Invalid owned graph settings')
            require(view.get('graphMode') == 'Manual' and view.get('graphOutputCount') == 1 and view.get('avatarName') == 'enKrakenHeadAvatar', 'Invalid graph/Avatar')
            for key in ('rootInstanceId', 'animatorInstanceId', 'avatarInstanceId'):
                require(view[key] == initial[key], 'Within-run surface identity changed')
            allowed = {identity['idleStateHash'], identity['targetStateHash']}
            require(view['current']['fullPathHash'] in allowed, 'Unexpected state')
            if view['inTransition']:
                require(view['next'] is not None and view['next']['fullPathHash'] in allowed and view['transition'] is not None, 'Missing/invalid transition state')
            else:
                require(view['next'] is None and view['transition'] is None, 'Inactive transition state metadata')
                # nextClips can remain populated one frame after transition completion; preserve it.
            pose = view['pose']
            require(set(pose['locals']) == set(pose['prefabRootModels']) == expected_paths, 'Pose path set mismatch')
            local_matrices = {}
            for path, local in pose['locals'].items():
                require(local['instanceId'] == run[surface+'Rest']['locals'][path]['instanceId'], 'Within-run bone identity changed')
                local_matrices[path] = trs(local)
                checks.numeric(label+':TRS:'+str(index)+':'+surface+':'+path, local_matrices[path], matrix(local['matrix']))
            for path, expected in models(local_matrices).items():
                checks.numeric(label+':model:'+str(index)+':'+surface+':'+path, expected, matrix(pose['prefabRootModels'][path]))
    require(run['frame'] > run['frames'][-1]['unityFrame'], 'Result did not follow captured frames')


def coverage(run):
    result = {}
    for surface in ('modern', 'old'):
        frames = run['frames']; windows = []; active = None
        for frame in frames:
            view = frame[surface]
            key = (view['current']['fullPathHash'], view['next']['fullPathHash'], view['transition']['fullPathHash']) if view['inTransition'] else None
            if key is not None and (active is None or active['_key'] != key):
                active = {'_key': key, 'startStep': frame['step'], 'lastTransitionStep': frame['step'],
                          'currentStateHash': key[0], 'nextStateHash': key[1], 'firstCurrentClock': view['current']['normalizedTime'],
                          'firstNextClock': view['next']['normalizedTime'], 'firstTransition': view['transition'], 'positiveCurrentAndNextClipWeightSteps': []}
                windows.append(active)
            if key is None:
                active = None
            else:
                active['lastTransitionStep'] = frame['step']
                if any(c['weight'] > 0 for c in view['currentClips']) and any(c['weight'] > 0 for c in view['nextClips']):
                    active['positiveCurrentAndNextClipWeightSteps'].append(frame['step'])
        for window in windows:
            window.pop('_key')
            following = window['lastTransitionStep'] + 1
            window['followingStep'] = following if following < len(frames) else None
            window['followingCurrentStateHash'] = frames[following][surface]['current']['fullPathHash'] if following < len(frames) else None
        variation = {}
        for path in frames[0][surface]['pose']['locals']:
            first = matrix(frames[0][surface]['pose']['locals'][path]['matrix'])
            rest = matrix(run[surface+'Rest']['locals'][path]['matrix'])
            values = [matrix(f[surface]['pose']['locals'][path]['matrix']) for f in frames]
            variation[path] = {'maximumFromFirst': max(float(np.max(np.abs(v-first))) for v in values),
                               'maximumAdjacentStepDelta': max(float(np.max(np.abs(a-b))) for a,b in zip(values,values[1:])),
                               'endToRestError': float(np.max(np.abs(values[-1]-rest)))}
        result[surface] = {'transitionWindows': windows, 'localMatrixVariation': variation,
                           'inactiveTransitionWithNonemptyNextClipsSteps': [f['step'] for f in frames if not f[surface]['inTransition'] and f[surface]['nextClips']],
                           'observedLayerWeights': sorted({f[surface]['layerWeight'] for f in frames}),
                           'endCurrentStateHash': frames[-1][surface]['current']['fullPathHash']}
    return result


def compare(a, ar, b, br):
    consistency = Checks()
    validate_run(a, ar, consistency, 'first'); validate_run(b, br, consistency, 'repeat')
    checks = Checks()
    require(a['session'] == b['session'] and a['id'] != b['id'] and a['scenario'] == b['scenario'], 'Expected distinct requests in same process/scenario')
    require(a['pinnedReady'] == b['pinnedReady'], 'Ready slot differs between repeats')
    require(a['identity'] == b['identity'], 'Source identity differs between same-process repeats')
    for surface in ('modern', 'old'):
        checks.structured('rest:'+surface, without_ids(a[surface+'Rest']), without_ids(b[surface+'Rest']))
    for index, (first, repeat) in enumerate(zip(a['frames'], b['frames'])):
        for surface in ('modern', 'old'):
            x, y = first[surface], repeat[surface]
            for key in ('current', 'next', 'inTransition', 'transition', 'currentClips', 'nextClips', 'layerName', 'layerWeight', 'graphPlayableCount', 'graphOutputCount'):
                checks.structured(str(index)+':'+surface+':'+key, without_ids(x[key]), without_ids(y[key]))
            checks.structured(str(index)+':'+surface+':pose', without_ids(x['pose']), without_ids(y['pose']))
    return {'status': 'controller_repeat_match' if not checks.failures and not consistency.failures else 'controller_repeat_mismatch',
            'scenario': a['scenario'], 'session': a['session'], 'framesPerRun': 241, 'tolerance': TOLERANCE,
            'checkedNumericValues': checks.count+consistency.count, 'maximumError': max(checks.maximum, consistency.maximum),
            'repeatMaximumError': checks.maximum, 'independentTrsModelMaximumError': consistency.maximum, 'failures': checks.failures+consistency.failures,
            'coverage': coverage(a), 'limitations': 'Repeatability and independent TRS/model consistency only. Coverage describes observed native graph output, not a fixed blend policy or animation acceptance. Instance IDs are validated within each run and excluded from pose comparison; absolute Unity frame/wall clocks are not compared across runs. Raw layer weight and stale nextClips are preserved. No adapter blending, live CEL/events, skin deformation or visual claim.'}


def run(path):
    manifest = json.loads(Path(path).read_text())
    require(manifest.get('schema') == 'kraken-controller-repeat-v1', 'Explicit controller repeat schema required')
    names = {'first', 'firstRequest', 'repeat', 'repeatRequest', 'assets', 'gameAssembly'}
    require(set(manifest['evidence']) == names, 'Exact six evidence pins required')
    paths = {key: read_pin(pin) for key,pin in manifest['evidence'].items()}
    require(digest(paths['assets']) == ASSETS and digest(paths['gameAssembly']) == ASSEMBLY, 'Actual source file hashes differ')
    docs = {key: json.loads(value.read_text()) for key,value in paths.items() if key not in ('assets', 'gameAssembly')}
    result = compare(docs['first'], docs['firstRequest'], docs['repeat'], docs['repeatRequest'])
    result.update(manifestSha256=digest(path), evidence=manifest['evidence'])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(); require('scratch' in args.output.resolve().parts, 'Pose reports must stay in scratch')
    result = run(args.manifest); args.output.write_text(json.dumps(result, indent=2, allow_nan=False)+'\n')
    print(result['status'], result['maximumError'])
    raise SystemExit(0 if result['status'] == 'controller_repeat_match' else 1)


if __name__ == '__main__':
    main()
