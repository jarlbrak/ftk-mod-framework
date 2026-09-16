#!/usr/bin/env python3
"""Summarize measured FTK capture timing and state coverage, never visual quality."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def summarize_ragdoll(frames, game_times, linear_threshold=.01, angular_threshold=.01):
    """Report recorded flags and dynamic-body measurements; absence never means false."""
    def flags(getter):
        samples=[];transitions=[];previous=None
        for index,frame in enumerate(frames):
            value=getter(frame)
            if value is None:
                previous=None
                continue
            if type(value) is not bool: raise ValueError('Recorded physics/Animator flag is not Boolean')
            sample={'frameIndex':index,'gameSeconds':game_times[index],'value':value}
            if previous is not None and previous['value'] != value:
                transitions.append({'fromFrame':previous['frameIndex'],'toFrame':index,
                                    'from':previous['value'],'to':value,'gameSeconds':game_times[index]})
            samples.append(sample);previous=sample
        return {'status':'recorded' if samples else 'not_recorded','recordedFrameCount':len(samples),
                'missingFrameCount':len(frames)-len(samples),'trueFrameCount':sum(s['value'] for s in samples),
                'falseFrameCount':sum(not s['value'] for s in samples),
                'firstSample':samples[0] if samples else None,'lastSample':samples[-1] if samples else None,
                'transitions':transitions}
    def vector(value,label):
        if value is None:return None
        if not isinstance(value,list) or len(value)!=3:raise ValueError('Invalid '+label+' vector')
        result=[float(v) for v in value]
        if not all(math.isfinite(v) for v in result):raise ValueError('Non-finite '+label+' vector')
        return result
    def magnitude(value):return math.sqrt(sum(v*v for v in value)) if value is not None else None
    animator=flags(lambda f:(f.get('animator') or {}).get('enabled'))
    native=flags(lambda f:(f.get('ragdoll') or {}).get('m_DoRagdoll'))
    bodies={};recorded_frames=0;truncated=[];counts=[]
    for index,frame in enumerate(frames):
        ragdoll=frame.get('ragdoll')
        if not isinstance(ragdoll,dict) or 'rigidbodies' not in ragdoll:continue
        recorded_frames+=1
        if ragdoll.get('truncated') is True:truncated.append(index)
        counts.append({'frameIndex':index,'reportedTotal':ragdoll.get('rigidbodyCount'),
                       'recordedCount':len(ragdoll['rigidbodies'])})
        seen=set()
        for body in ragdoll['rigidbodies']:
            identity=body['instanceId']
            if identity in seen:raise ValueError('Duplicate Rigidbody instanceId in one frame')
            seen.add(identity)
            for field in ['active','isKinematic']:
                if field in body and type(body[field]) is not bool:raise ValueError('Invalid Rigidbody '+field)
            sample={'frameIndex':index,'gameSeconds':game_times[index],
                    'dynamic':body.get('active') is True and body.get('isKinematic') is False,
                    'position':vector(body.get('position'),'body position'),
                    'linearSpeed':magnitude(vector(body.get('velocity'),'velocity')),
                    'angularSpeed':magnitude(vector(body.get('angularVelocity'),'angular velocity'))}
            entry=bodies.setdefault(identity,{'instanceId':identity,'paths':set(),'samples':[]})
            entry['paths'].add(body.get('path'));entry['samples'].append(sample)
    summaries=[]
    for body in bodies.values():
        samples=body.pop('samples');dynamic=[s for s in samples if s['dynamic']]
        distance=0.;pairs=0;moving_pairs=0;linear=[];angular=[];tail=[];prior=None
        for sample in samples:
            if sample['dynamic']:
                if sample['linearSpeed'] is not None:linear.append(sample['linearSpeed'])
                if sample['angularSpeed'] is not None:angular.append(sample['angularSpeed'])
                if prior and prior['dynamic'] and sample['frameIndex']==prior['frameIndex']+1 and sample['gameSeconds']>prior['gameSeconds'] and sample['position'] is not None and prior['position'] is not None:
                    step=math.sqrt(sum((a-b)**2 for a,b in zip(sample['position'],prior['position'])))
                    distance+=step;pairs+=1;moving_pairs+=step>1e-6
            low=(sample['dynamic'] and sample['linearSpeed'] is not None and sample['angularSpeed'] is not None
                 and sample['linearSpeed']<=linear_threshold and sample['angularSpeed']<=angular_threshold)
            if not low:tail=[]
            elif tail and sample['frameIndex']==tail[-1]['frameIndex']+1 and sample['gameSeconds']>tail[-1]['gameSeconds']:tail.append(sample)
            else:tail=[sample]
            prior=sample
        # A body absent from the final capture frame cannot support a capture-ending quiet interval.
        if not tail or tail[-1]['frameIndex']!=len(frames)-1:tail=[]
        summaries.append({'instanceId':body['instanceId'],'paths':sorted(p for p in body['paths'] if p is not None),
            'recordedSampleCount':len(samples),'activeNonKinematicSampleCount':len(dynamic),
            'consecutiveDynamicPositionPairs':pairs,'movingPositionPairCount':moving_pairs,
            'sampledWorldPositionPathLength':distance if pairs else None,
            'maxRecordedLinearSpeed':max(linear) if linear else None,
            'maxRecordedAngularSpeed':max(angular) if angular else None,
            'lastRecordedLinearSpeed':samples[-1]['linearSpeed'] if samples else None,
            'lastRecordedAngularSpeed':samples[-1]['angularSpeed'] if samples else None,
            'trailingLowVelocitySamples':len(tail),'trailingLowVelocityFirstFrame':tail[0]['frameIndex'] if tail else None,
            'trailingLowVelocityGameSeconds':tail[-1]['gameSeconds']-tail[0]['gameSeconds'] if tail else None})
    return {'animatorEnabled':animator,'nativeRagdollFlag':native,
            'rigidbodies':{'status':'recorded' if recorded_frames else 'not_recorded',
                'recordedFrameCount':recorded_frames,'missingFrameCount':len(frames)-recorded_frames,
                'truncatedFrames':truncated,'bodyCounts':counts,'bodies':sorted(summaries,key=lambda b:b['instanceId'])},
            'lowVelocityThresholds':{'linearGameUnitsPerSecond':linear_threshold,'angularRadiansPerSecond':angular_threshold},
            'interpretation':'Evidence only. A low-velocity tail describes recorded samples, not a successful death or visual settling verdict.',
            'limitations':['m_DoRagdoll records native configuration, not proof that physics took over.',
                           'Renderer.enabled is not Animator.enabled. Missing flags are not inferred from clip progress.',
                           'Body motion uses active nonkinematic samples, positive game-time steps and consecutive frame observations only.',
                           'World-space body movement can include parent motion; sampled path length omits between-frame movement.',
                           'A quiet tail requires both recorded linear and angular velocities; truncated/missing bodies prevent whole-ragdoll conclusions.']}


def summarize(path):
    doc = json.loads(path.read_text())
    frames = doc.get('frames', [])
    if not isinstance(frames, list) or not frames:
        raise ValueError('Capture has no recorded frames')
    states = {}
    paused = 0
    signatures = set()
    game_times = []
    screenshots = []
    for index, frame in enumerate(frames):
        game_time = float(frame['gameSeconds'])
        if not math.isfinite(game_time):
            raise ValueError('Non-finite game time')
        game_times.append(game_time)
        paused += float(frame['timeScale']) <= 0
        signatures.add((frame['mesh'], frame['boneSignature']))
        screenshot = path.with_suffix('') / f'{index:04d}.png'
        screenshots.append({'file': screenshot.name, 'present': screenshot.is_file(),
                            'sha256': hashlib.sha256(screenshot.read_bytes()).hexdigest() if screenshot.is_file() else None})
        for layer in (frame.get('animator') or {}).get('layers', []):
            key = (layer['layer'], layer['stateHash'])
            state = states.setdefault(key, {'layer': key[0], 'stateHash': key[1], 'samples': [], 'clips': set()})
            progress = float(layer['normalizedTime'])
            if not math.isfinite(progress):
                raise ValueError('Non-finite animator progress')
            state['samples'].append({'frameIndex': index, 'gameSeconds': game_time,
                                     'normalizedTime': progress, 'transition': layer['transition']})
            state['clips'].update(c['name'] for c in layer.get('playing', []))
    if any(b < a for a, b in zip(game_times, game_times[1:])):
        raise ValueError('Game time moved backwards')
    summaries = []
    for state in states.values():
        samples = state.pop('samples')
        progress = [s['normalizedTime'] for s in samples]
        # Only a continuous occurrence can support a near-complete single playback.
        runs = []
        for sample in samples:
            if not runs or sample['frameIndex'] != runs[-1][-1]['frameIndex'] + 1 or sample['normalizedTime'] < runs[-1][-1]['normalizedTime']:
                runs.append([])
            runs[-1].append(sample)
        spans = [{'firstFrame': r[0]['frameIndex'], 'lastFrame': r[-1]['frameIndex'],
                  'firstNormalizedTime': r[0]['normalizedTime'], 'lastNormalizedTime': r[-1]['normalizedTime'],
                  'gameSeconds': r[-1]['gameSeconds'] - r[0]['gameSeconds']} for r in runs]
        state.update(clips=sorted(state['clips']), sampleCount=len(samples), occurrences=spans,
                     nearCompleteFirstCycleObserved=any(s['firstNormalizedTime'] <= .1 and s['lastNormalizedTime'] >= .9 and s['gameSeconds'] > 0 for s in spans))
        summaries.append(state)
    return {'version': 2, 'captureId': doc.get('id'), 'captureSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'provenance': doc.get('provenance'), 'timingMode': doc.get('timingMode'),
            'testFixtureMaxHpTarget': doc.get('testFixtureMaxHpTarget'), 'tutorialsQuiet': doc.get('tutorialsQuiet'),
            'captureReportedOk': doc.get('ok'), 'captureError': doc.get('error'),
            'frameCount': len(frames), 'pausedFrameCount': paused,
            'actualGameSeconds': game_times[-1] - game_times[0],
            'timingStatus': 'no_simulation_progress' if game_times[-1] == game_times[0] else 'measured_progress',
            'meshIdentityStable': len(signatures) == 1, 'meshSignatures': [list(x) for x in sorted(signatures)],
            'allScreenshotsPresent': all(s['present'] for s in screenshots),
            'states': summaries, 'screenshots': screenshots, 'visualReview': 'pending',
            'ragdollEvidence': summarize_ragdoll(frames, game_times),
            'limitations': ['State progress is measured evidence, not a visual quality verdict.',
                            'Near-complete means one continuous sampled occurrence includes <=0.1 through >=0.9 normalized time; endpoints and intervening poses still need visual review.',
                            'A capture request duration does not establish elapsed simulation time.',
                            'Gameplay claims require the separately recorded action and before/after combat state.']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--result', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Output exists; preserve prior evidence or choose a new output')
    report = summarize(args.result)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: report[k] for k in ['frameCount', 'pausedFrameCount', 'actualGameSeconds', 'timingStatus', 'visualReview']}))


if __name__ == '__main__':
    main()
