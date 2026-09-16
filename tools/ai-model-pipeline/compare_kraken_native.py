#!/usr/bin/env python3
"""Fail-closed offline comparison of native Kraken Animator poses and explicit playable samples."""
import argparse
import hashlib
import json
import math
import re
import struct
import uuid
from pathlib import Path

import numpy as np

PATHS = ['Root_M', 'Root_M/base', 'Root_M/base/body', 'Root_M/base/body/neck',
         'Root_M/base/body/neck/head', 'Root_M/base/body/neck/head/topHead', 'Root_M/base/body/neck/eye']
MAIN = {'krakenIdle', 'krakenAttack', 'krakenDamage', 'krakenDisappear'}


def require(ok, message):
    if not ok:
        raise ValueError(message)


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def finite(value):
    require(isinstance(value, (float, int)) and not isinstance(value, bool) and math.isfinite(value), 'Nonfinite/nonnumeric value')
    return float(value)


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def pointer(value, path):
    require(isinstance(path, str) and path.startswith('/'), 'Explicit JSON pointer required')
    for key in path[1:].split('/'):
        key = key.replace('~1', '/').replace('~0', '~')
        value = value[int(key)] if isinstance(value, list) else value[key]
    return value


def one(items, message):
    require(len(items) == 1, message)
    return items[0]


def load_manifest(path):
    path = Path(path).resolve()
    manifest = json.loads(path.read_text())
    require(manifest.get('version') == 1, 'Manifest version 1 required')
    documents, paths = {}, {}
    names = ['assets', 'mapping', 'profiles', 'registration', 'journal', 'inventory', 'capture', 'deployment']
    for name in names:
        entry = manifest['evidence'][name]
        target = (path.parent / entry['path']).resolve(strict=True)
        require(re.fullmatch('[a-f0-9]{64}', entry['sha256']) is not None, 'Invalid evidence hash')
        require(digest(target) == entry['sha256'], 'Evidence hash mismatch: ' + name)
        paths[name] = target
        if name != 'assets':
            documents[name] = [json.loads(line) for line in target.read_text().splitlines() if line.strip()] if name == 'journal' else json.loads(target.read_text())
    for name in ['framework', 'helper']:
        entry = manifest['binaries'][name]
        binary = (path.parent / entry['path']).resolve(strict=True)
        require(re.fullmatch('[a-f0-9]{64}', entry['sha256']) is not None and digest(binary) == entry['sha256'], 'Binary hash mismatch: ' + name)
        require(pointer(documents['deployment'], entry['deploymentPointer']) == entry['sha256'], 'Deployment does not pin ' + name)
        require(pointer(documents['journal'], entry['journalPointer']) == entry['sha256'], 'Journal does not pin ' + name)
    return manifest, documents, paths


def source_model(assets, mapping):
    import UnityPy
    require(mapping['source_sha256'] == digest(assets), 'Mapping source asset mismatch')
    enemy = one([x for x in mapping['enemies'] if x['enemy_id'] == 'krakenHead'], 'Exact native Kraken mapping required')
    renderer = one([x for x in enemy['renderers'] if x['renderer_path_id'] == 121035], 'Modern renderer 121035 required')
    controller = one(enemy['weapon_animation_controller_candidates'], 'Single native controller mapping required')
    require(controller['controller_path_id'] == 5973 and controller['controller_name'] == 'krakenHeadController', 'Native controller source mismatch')
    env = UnityPy.load(str(assets))
    source = next(x for x in env.files.values() if Path(x.name).name == assets.name)
    def tree(pid):
        return source.objects[pid].read_typetree(check_read=False)
    def bone_path(pid):
        tr = tree(pid)
        parent = tr['m_Father']['m_PathID']
        if not parent:
            return ''
        return '/'.join(x for x in [bone_path(parent), tree(tr['m_GameObject']['m_PathID'])['m_Name']] if x)
    raw_renderer = tree(121035)
    require([bone_path(x['m_PathID']) for x in raw_renderer['m_Bones']] == PATHS, 'Source palette/path changed')
    mesh = source.objects[raw_renderer['m_Mesh']['m_PathID']].read()
    binds = [[[getattr(m, f'e{r}{c}') for c in range(4)] for r in range(4)] for m in mesh.m_BindPose]
    raw = tree(5973)
    require(raw['m_Name'] == 'krakenHeadController' and len(raw['m_Controller']['m_LayerArray']) == 1, 'Unsupported native controller/layers')
    names = dict(raw['m_TOS'])
    states = {}
    for item in raw['m_Controller']['m_StateMachineArray'][0]['data']['m_StateConstantArray']:
        state = item['data']; reasons = []
        for key in ['m_SpeedParamID', 'm_MirrorParamID', 'm_CycleOffsetParamID', 'm_TimeParamID', 'm_CycleOffset']:
            if state.get(key) != 0:
                reasons.append(key)
        if state.get('m_Speed') != 1 or state.get('m_Mirror') is not False:
            reasons.append('state speed/mirror')
        trees = state['m_BlendTreeConstantArray']
        if len(trees) != 1 or len(trees[0]['data']['m_NodeArray']) != 1:
            continue
        node = trees[0]['data']['m_NodeArray'][0]['data']
        if node['m_ChildIndices'] or node['m_CycleOffset'] != 0 or node['m_Mirror'] or node['m_Duration'] != 1:
            reasons.append('blend node')
        clip_ptr = raw['m_AnimationClips'][node['m_ClipID']]
        require(clip_ptr['m_FileID'] == 0, 'External clip source unsupported')
        clip = tree(clip_ptr['m_PathID']); muscle = clip['m_MuscleClip']
        if muscle['m_StartTime'] != 0 or muscle['m_CycleOffset'] != 0 or muscle['m_Mirror'] or clip['m_Legacy']:
            reasons.append('clip start/offset/mirror/legacy')
        if state['m_Loop'] != muscle['m_LoopTime']:
            reasons.append('state/clip loop mismatch')
        states[str(state['m_FullPathID'])] = dict(name=names[state['m_FullPathID']], clip=clip['m_Name'],
            seconds=muscle['m_StopTime'], loop=state['m_Loop'], unsupported=reasons, clipSourceId=clip_ptr['m_PathID'])
    return dict(renderer=renderer, prefab=enemy['prefab_name'], controller='krakenHeadController', controllerSourceId=5973,
                paths=PATHS, bindposes=binds, states=states)


def capture_boundary(manifest, capture):
    allow_prefix = manifest.get('allowRendererDestroyedPrefix', False)
    require(type(allow_prefix) is bool, 'allowRendererDestroyedPrefix must be a literal boolean')
    allow_teardown = manifest.get('allowDeathControllerTeardownPrefix', False)
    require(type(allow_teardown) is bool, 'allowDeathControllerTeardownPrefix must be a literal boolean')
    termination = None
    frames = capture['frames']
    seconds, fps = finite(capture['requestedSeconds']), finite(capture['requestedFps'])
    require(0 < seconds <= 10 and 1 <= fps <= 20, 'Invalid requested capture duration/rate')
    requested = math.ceil(seconds * fps)
    require(1 <= requested <= 120, 'Invalid requested capture frame count')
    require(isinstance(frames, list) and frames, 'Nonempty retained frame sequence required')
    partial = capture.get('ok') is False
    if capture.get('ok') is True:
        require(capture.get('error') is None and len(frames) == requested, 'Successful capture must contain all requested frames and no error')
    else:
        require(partial and (allow_prefix or allow_teardown), 'Failed capture requires explicit terminal-prefix opt-in')
        error = capture.get('error')
        lines = error.splitlines() if isinstance(error, str) else []
        renderer_destroyed = (len(lines) == 2 and lines[0] == 'System.InvalidOperationException: Renderer destroyed during capture.'
                and re.fullmatch(r'\s+at RuntimeModelTest\+<Capture>d__\d+\.MoveNext \(\) \[0x[0-9a-fA-F]+\] in .+:\d+\s*', lines[1]) is not None)
        if renderer_destroyed:
            require(allow_prefix, 'RendererDestroyed prefix requires its own opt-in')
            termination = 'renderer_destroyed'
        else:
            require(allow_teardown, 'Controller teardown prefix requires its own opt-in')
            death_controller_boundary(capture)
            termination = 'controller_unresolved_after_native_death'
        require(len(frames) < requested, 'Terminal prefix must be shorter than requested capture')
    frame_ids = [frame['frame'] for frame in frames]
    require(all(type(value) is int for value in frame_ids) and all(a < b for a,b in zip(frame_ids, frame_ids[1:])), 'Retained frame IDs must be strictly increasing integers')
    if partial:
        require(type(capture['frame']) is int and capture['frame'] > frame_ids[-1], 'Partial result boundary must follow its last retained frame')
    return dict(partial=partial, completeCapture=not partial, allowRendererDestroyedPrefix=allow_prefix,
                rawCaptureOk=capture['ok'], rawCaptureError=capture.get('error'), requestedFrameCount=requested,
                retainedFrameCount=len(frames), firstRetainedFrame=frame_ids[0], lastRetainedFrame=frame_ids[-1],
                resultFrame=capture.get('frame'), termination=termination,
                allowDeathControllerTeardownPrefix=allow_teardown,
                limitation='Failure category only; neither native cleanup causality nor full death-animation coverage is inferred.' if partial else None)


def death_controller_boundary(capture):
    require(capture.get('error') == 'Controller resolution unavailable after observed native Death.', 'Not the exact controller teardown boundary')
    motion = capture.get('motionObservation') or {}
    terminal = motion.get('termination') or {}
    require(motion.get('schema') == 'ftkmf.native-combat-motion.v1' and 'error' in motion and motion['error'] is None,
            'Successful native motion provenance required')
    require(terminal.get('reason') == 'controller-unresolved-after-observed-native-death'
            and terminal.get('controllerError') == 'Avatar controller ambiguous or absent.'
            and terminal.get('terminalFrameCaptured') is False, 'Exact uncaptured terminal boundary required')
    frame = terminal.get('frame')
    require(type(frame) is int and capture['frames'][-1]['frame'] < frame <= capture['frame'], 'Invalid controller termination frame')
    require(type(terminal.get('realtime')) in (int,float), 'Numeric non-boolean terminal clock required')
    clock = finite(terminal['realtime'])
    require(clock > 0, 'Positive terminal clock required')
    started_frame = motion.get('startedFrame')
    require(type(started_frame) is int and 0 <= started_frame <= capture['frames'][0]['frame'], 'Exact motion arm frame required')
    require(type(motion.get('startedRealtime')) in (int,float), 'Numeric non-boolean arm clock required')
    started_clock = finite(motion['startedRealtime'])
    require(0 <= started_clock <= clock, 'Invalid motion arm clock')
    keys = {'targetRendererInstanceId':'instanceId', 'targetOwnerInstanceId':'ownerInstanceId', 'targetCelInstanceId':'celInstanceId'}
    for target, field in keys.items():
        require(type(motion.get(target)) is int and motion[target] != 0 and all(item.get(field) == motion[target] for item in capture['frames']), 'Terminal renderer identity changed')
    fid = motion.get('targetFid')
    require(type(motion.get('targetAnimatorInstanceId')) is int and motion['targetAnimatorInstanceId'] != 0
            and isinstance(fid,dict) and all(type(fid.get(key)) is int for key in ('photonId','turnIndex')), 'Exact native animator and FID provenance required')
    animator_id = motion['targetAnimatorInstanceId']
    baseline = motion.get('baseline') or {}
    require(type(baseline.get('animatorInstanceId')) is int and baseline['animatorInstanceId'] == animator_id
            and baseline.get('baseLayerIdle') is True and baseline.get('baseLayerTransition') is False
            and baseline.get('animatorEnabled') is True and baseline.get('animatorActive') is True,
            'Exact enabled settled IDLE arm baseline required')
    for retained in capture['frames']:
        animator = retained.get('motionAnimator') or {}
        require(type(animator.get('animatorInstanceId')) is int and animator['animatorInstanceId'] == animator_id,
                'Retained native motion Animator identity changed or missing')
    require(capture.get('ownerInstanceId') == motion['targetOwnerInstanceId'] and capture.get('celInstanceId') == motion['targetCelInstanceId'], 'Terminal owner identity mismatch')
    events = motion.get('events')
    require(isinstance(events,list) and 0 < len(events) <= 64 and type(motion.get('eventCount')) is int
            and motion['eventCount'] == len(events), 'Bounded native events required')
    def observed(event):
        return (event.get('nativeMethod') == 'CharacterEventListener.CombatTrigger entry'
                and event.get('trigger') == 'Death' and event.get('finalized') is True
                and 'exception' in event and event['exception'] is None
                and type(event.get('frame')) is int and started_frame <= event['frame'] <= frame
                and type(event.get('realtime')) in (int,float) and math.isfinite(event['realtime'])
                and started_clock <= event['realtime'] <= clock
                and all(event.get(key) == motion.get(key) for key in (*keys, 'targetAnimatorInstanceId', 'targetFid')))
    require(any(observed(event) for event in events), 'Prior successful exact-target native Death required')


def verify_documents(manifest, docs, model):
    session = manifest['session']
    require(re.fullmatch('[a-f0-9]{32}', session) is not None, 'Exact helper session required')
    profile = one([p for p in docs['profiles']['profiles'] if p['key'] == manifest['profileKey']], 'Unique profile required')
    require(profile['baseEnemy'] == 'krakenHead' and not profile.get('resourcePrefab'), 'Native modern Kraken profile required, no resource override')
    require(profile['combatProfile'] == model['renderer']['combat_profile_fingerprint'], 'Combat profile fingerprint mismatch')
    require(len(profile['renderers']) == 1 and profile['renderers'][0]['rendererPath'] == 'kraken2', 'Exact modern renderer assignment required')
    report = docs['registration']
    require(report['status'] == 'registered', 'Successful registration required')
    reg = one([p for p in report['registered'] if p['key'] == profile['key']], 'Exact registered profile missing')
    require(reg['baseEnemy'] == 'krakenHead' and reg['combatProfile'] == profile['combatProfile'] and not reg.get('resourcePrefab'), 'Registered source/profile mismatch')
    provenance = one([x['data'] for x in docs['journal'] if x['kind'] == 'provenance'], 'One journal provenance entry required')
    require(provenance['session'] == session and provenance['profile'] == profile and provenance['profileInputSha256'] == manifest['evidence']['profiles']['sha256'], 'Journal profile/session mismatch')
    registered = one([x['data']['report'] for x in docs['journal'] if x['kind'] == 'enemy-registration'], 'Journal registration required')
    require(registered == report, 'Journal registration differs from report')
    inv, cap = docs['inventory'], docs['capture']
    require(inv.get('ok') is True and inv['session'] == cap['session'] == session, 'Successful inventory and same-session capture required')
    capture_boundary(manifest, cap)
    require(cap['scope'] == 'enemies' and cap['provenance'] == 'observed-runtime', 'Actual observed native capture required, not forced-state playback')
    target = one([r for r in inv['renderers'] if r['instanceId'] == manifest['rendererInstanceId']], 'Exact inventory renderer required')
    require(re.fullmatch('[a-f0-9]{64}', target['boneSignature']) is not None, 'Exact runtime bone signature required')
    require(target['ownerKind'] == 'enemies' and target['celRelativeRendererPath'] == 'kraken2' and target['celRootName'] == model['prefab'] + '(Clone)', 'Wrong native CEL/path')
    require(target['mesh'] == 'ftkmf_glb_' + profile['renderers'][0]['glbFile'], 'Profile mesh identity differs from inventory')
    require(target['animator']['controller'] == model['controller'], 'Inventory native controller mismatch')
    require([b['name'] for b in target['bones']] == [p.split('/')[-1] for p in PATHS], 'Inventory palette differs from exact source')
    inventory_binds = np.array([b['bindposeRowMajor'] for b in target['bones']], dtype=float)
    require(inventory_binds.shape == (len(PATHS), 16), 'Expected flat row-major inventory bind matrices')
    require(np.max(np.abs(inventory_binds.reshape((-1, 4, 4)) - np.array(model['bindposes']))) < 1e-6, 'Inventory bind matrices differ from source profile')
    require(cap['ownerInstanceId'] == target['ownerInstanceId'] and cap['celInstanceId'] == target['celInstanceId'], 'Capture owner/CEL mismatch')
    for frame in cap['frames']:
        for key in ['instanceId', 'ownerInstanceId', 'celInstanceId', 'boneSignature', 'mesh', 'celRelativeRendererPath']:
            require(frame[key] == target[key], 'Capture identity changed: ' + key)
        require(frame['animator']['instanceId'] == target['animator']['instanceId'], 'Capture Animator changed')
        require([b['name'] for b in frame['bones']] == [b['name'] for b in target['bones']], 'Capture palette changed')
        for bone in frame['bones']:
            trs(bone)
    return target


def clip_clock(normalized, state):
    require(not state['unsupported'], 'Unsupported state clock: ' + ','.join(state['unsupported']))
    n = finite(normalized)
    require(n >= 0, 'Negative state time unsupported')
    phase = n - math.floor(n) if state['loop'] else min(1.0, n)
    return f32(phase * state['seconds']), dict(normalizedTime=n, mode='loop' if state['loop'] else 'clamp',
        loopIndex=math.floor(n) if state['loop'] else None, clamped=not state['loop'] and n > 1.0, phase=phase)


def pure_frame(frame, model):
    require(frame.get('active') is True and frame.get('enabled') is True, 'Inactive/disabled renderer')
    require(frame.get('ragdoll', {}).get('m_DoRagdoll') is False, 'Ragdoll or missing ragdoll evidence')
    animator = frame['animator']
    require(animator['enabled'] is True and abs(finite(animator['speed']) - 1) < 1e-7, 'Animator disabled or speed changed')
    require(len(animator['layers']) == 1 and animator['layers'][0]['layer'] == 0, 'Unobserved layer weights unsupported')
    layer = animator['layers'][0]
    require(layer['transition'] is False and len(layer['playing']) == 1, 'Transition or blended clips')
    clip = layer['playing'][0]
    require(abs(finite(clip['weight']) - 1) < 1e-5, 'Clip weight not one')
    require(type(layer['stateHash']) is int, 'Integer native state hash required')
    key = str(layer['stateHash'] & 0xffffffff)
    require(key in model['states'], 'Unknown native state')
    state = model['states'][key]
    require(state['clip'] in MAIN and clip['name'] == state['clip'], 'State/clip mapping mismatch or appearance excluded')
    require(abs(finite(clip['clipSeconds']) - state['seconds']) < 1e-6, 'Clip length mismatch')
    require(finite(frame['deltaTime']) > 0 and finite(frame['timeScale']) > 0, 'Paused or invalid frame')
    time, clock = clip_clock(layer['normalizedTime'], state)
    return dict(stateHash=key, state=state['name'], clip=state['clip'], time=time, clock=clock)


def select_frames(frames, model):
    candidates, rejected = {}, []
    parsed = []
    for index, frame in enumerate(frames):
        try:
            parsed.append(pure_frame(frame, model))
        except (ValueError, KeyError, TypeError) as error:
            parsed.append(None); rejected.append(dict(frameIndex=index, reason=str(error)))
    for index in range(1, len(frames) - 1):
        row = parsed[index]
        if row is None or any(x is None or x['stateHash'] != row['stateHash'] for x in [parsed[index-1], parsed[index+1]]):
            continue
        if not finite(frames[index-1]['gameSeconds']) < finite(frames[index]['gameSeconds']) < finite(frames[index+1]['gameSeconds']):
            continue
        entries = candidates.setdefault(row['clip'], {})
        entries.setdefault(row['time'], dict(frameIndex=index, frame=frames[index]['frame'], **row))
    selected = {}
    for clip, entries in candidates.items():
        rows = list(entries.values())
        if len(rows) < 2:
            continue
        if len(rows) > 32:
            rows = [rows[round(i*(len(rows)-1)/31)] for i in range(32)]
        selected[clip] = rows
    require(selected, 'No clip has two distinct stable interior times; no requests emitted')
    return selected, rejected


def trs(bone):
    position = np.array([finite(x) for x in bone['localPosition']]); scale = np.array([finite(x) for x in bone['localScale']])
    x, y, z, w = [finite(x) for x in bone['localRotation']]
    require(abs(x*x+y*y+z*z+w*w - 1) < 1e-4, 'Quaternion not normalized')
    m = np.eye(4)
    m[:3,:3] = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                         [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                         [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]) @ np.diag(scale)
    m[:3,3] = position
    return m


def compose(locals):
    models = {}
    for path in PATHS:
        parent = path.rpartition('/')[0]
        models[path] = (models[parent] if parent else np.eye(4)) @ locals[path]
    return models


def matrix(value):
    out = np.array(value, dtype=float)
    require(out.shape == (4,4) and np.isfinite(out).all(), 'Invalid matrix')
    return out


def compare_rows(capture, selected, sample, session, model, tolerance):
    require(sample.get('ok') is True and sample['session'] == session, 'Successful same-session playable sample required')
    require(sample['provenance'] == 'isolated_controller_free_manual_clip_playable_native_avatar' and sample['samplingMethod'] == 'clip-playable', 'Wrong sample provenance')
    require(sample['coordinateConvention'] == 'independent-modern-root-and-local-drivers', 'Sample coordinate convention mismatch')
    identity = sample['identity']; clip = identity['clipName']
    require(clip in selected and identity['controllerName'] == model['controller'] and identity['modernSource'] == 'native-enemy:krakenHead', 'Sample clip/controller/source mismatch')
    require(identity['modernInitialization']['initializedAfterRebindAndEvaluate'] is True and identity['modernInitialization']['nativeAvatarValid'] is True, 'Playable not initialized')
    require(sample['cleanup']['modernUnityNull'] is True and sample['cleanup']['oldUnityNull'] is True
            and sample['cleanup']['graphsDisposedBeforeTargets'] is True and not sample['cleanup']['errors'], 'Sample cleanup failed')
    rows = selected[clip]
    require(len(sample['frames']) == len(rows), 'Sample count must exactly match requested times')
    expected_length = next(s['seconds'] for s in model['states'].values() if s['clip'] == clip)
    require(abs(identity['clipLength'] - expected_length) < 1e-6, 'Sample clip length mismatch')
    result = []
    for row, frame in zip(rows, sample['frames']):
        require(frame['clip'] == clip and f32(finite(frame['time'])) == row['time'], 'Sample time/order differs from exact request')
        require(frame['fullRestResetBeforeSample'] is True and frame['samplingMethod'] == 'clip-playable'
                and frame['modernFixtureActive'] is True, 'Sample fixture state mismatch')
        playable = frame['modernPlayable']
        require(playable['animatorEnabled'] is True and playable['animatorInitialized'] is True and playable['avatarValid'] is True
                and playable['runtimeControllerAssigned'] is False and playable['fireEvents'] is False and playable['applyRootMotion'] is False
                and playable['graphValid'] is True and playable['graphUpdateMode'] == 'Manual' and playable['playableCount'] == 1
                and playable['outputCount'] == 1 and playable['footIK'] is False
                and playable['clipInstanceId'] == identity['clipInstanceId'], 'Unsupported playable state')
        native_locals = {path:trs(bone) for path,bone in zip(PATHS,capture['frames'][row['frameIndex']]['bones'])}
        require(set(frame['driverLocals']) == set(PATHS[1:]), 'Incomplete/extra playable drivers')
        sampled_locals = {'Root_M':matrix(frame['modernRootAnimated']), **{p:matrix(v) for p,v in frame['driverLocals'].items()}}
        native_models, sample_models = compose(native_locals), compose(sampled_locals)
        local_deltas = {p:float(np.max(np.abs(native_locals[p]-sampled_locals[p]))) for p in PATHS}
        common_deltas = {p:float(np.max(np.abs(native_models[p]-sample_models[p]))) for p in PATHS}
        maximum = max(*local_deltas.values(), *common_deltas.values())
        result.append(dict(frameIndex=row['frameIndex'], time=row['time'], clock=row['clock'], localMatrixDeltas=local_deltas,
                           celRelativeModelDeltas=common_deltas, maximumDelta=maximum, pass_=maximum<=tolerance))
    return dict(clip=clip, pass_=all(x['pass_'] for x in result), frames=result)


def run(manifest_path, sample_paths):
    manifest, docs, paths = load_manifest(manifest_path)
    model = source_model(paths['assets'], docs['mapping'])
    target = verify_documents(manifest, docs, model)
    selected, rejected = select_frames(docs['capture']['frames'], model)
    requests = [dict(op='kraken-sample-fixture', method='clip-playable', clip=clip, times=[r['time'] for r in rows],
                     session=manifest['session'], id=uuid.uuid5(uuid.NAMESPACE_URL, manifest['evidence']['capture']['sha256']+clip+json.dumps(rows,sort_keys=True)).hex)
                for clip,rows in selected.items()]
    results = []; supplied = set()
    for path in sample_paths:
        sample = json.loads(Path(path).read_text()); clip = sample['identity']['clipName']
        require(clip not in supplied, 'Duplicate sample clip input'); supplied.add(clip)
        item = compare_rows(docs['capture'], selected, sample, manifest['session'], model, 1e-4)
        item.update(samplePath=str(Path(path).resolve()), sampleSha256=digest(path));results.append(item)
    complete = supplied == set(selected)
    boundary = capture_boundary(manifest, docs['capture'])
    status = 'reference_match' if complete and all(x['pass_'] for x in results) else 'reference_mismatch' if results and any(not x['pass_'] for x in results) else 'samples_required'
    if boundary['partial']:
        status = 'prefix_' + status
    return dict(status=status, captureBoundary=boundary,
                manifestSha256=digest(manifest_path), session=manifest['session'], evidence=manifest['evidence'], binaries=manifest['binaries'],
                sourceRendererId=121035, sourceControllerId=5973, rigProfile=model['renderer']['rig_profile_fingerprint'],
                combatProfile=model['renderer']['combat_profile_fingerprint'], boneSignature=target['boneSignature'],
                requests=requests, selected=selected, rejectedFrames=rejected, comparisonTolerance=1e-4, comparisons=results,
                limits=['Clip times derive from verified single-node native states: speed1, no time/speed/offset/mirror parameters, zero offsets. clipSeconds is duration, never current time.',
                        'Only stable single-layer, single-clip weight-one interior frames are selected. Transitions, mixed clips, pauses and ragdolls are excluded.',
                        'Local TRS is reconstructed in common CEL-root coordinates, excluding world placement and CEL instance scale. No live root-motion or world-pose equivalence claimed.',
                        'Deployment and journal binary pins attest provenance; this offline tool does not query loaded process memory.',
                        'Matching sampled native poses does not establish controller transitions, adapter runtime behavior or visual acceptance.'])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,required=True);parser.add_argument('--samples',type=Path,action='append',default=[])
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    require(not args.output.exists(), 'Refusing to overwrite output')
    result=run(args.manifest,args.samples)
    args.output.write_text(json.dumps(result,indent=2,allow_nan=False)+'\n')
    print(result['status'])

if __name__=='__main__':
    main()
