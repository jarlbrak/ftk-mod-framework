"""Validate native combat-motion provenance from one bound-renderer capture.

This module is deliberately evidence-only.  It never operates the bridge or a
game process.  The runtime helper records passive Harmony observations while
the existing one-case runner owns every action.
"""


def require(value, message):
    if not value:
        raise RuntimeError(message)


def same_fid(left, right):
    return (isinstance(left, dict) and isinstance(right, dict)
            and type(left.get('photonId')) is int and type(left.get('turnIndex')) is int
            and type(right.get('photonId')) is int and type(right.get('turnIndex')) is int
            and left.get('photonId') == right.get('photonId')
            and left.get('turnIndex') == right.get('turnIndex'))


def _integer(value, name):
    require(type(value) is int and value != 0, 'Exact nonzero ' + name + ' required')
    return value


def _observation(capture, renderer, target_fid):
    require(isinstance(capture, dict) and isinstance(renderer, dict), 'Capture and bound renderer required')
    require(isinstance(target_fid, dict), 'Exact target FID required')
    observation = capture.get('motionObservation')
    require(isinstance(observation, dict), 'Native combat motion observation missing')
    require(observation.get('schema') == 'ftkmf.native-combat-motion.v1', 'Unknown combat motion schema')
    require(observation.get('error') is None, 'Native combat motion observer reported an error')
    require(same_fid(observation.get('targetFid'), target_fid), 'Motion target FID differs from exact enemy')
    require(observation.get('targetOwnerInstanceId') == _integer(renderer.get('ownerInstanceId'), 'owner id'),
            'Motion target owner differs from bound renderer')
    require(observation.get('targetCelInstanceId') == _integer(renderer.get('celInstanceId'), 'CEL id'),
            'Motion target CEL differs from bound renderer')
    require(observation.get('targetRendererInstanceId') == _integer(renderer.get('instanceId'), 'renderer id'),
            'Motion target renderer differs from bound renderer')
    animator = renderer.get('animator')
    require(isinstance(animator, dict), 'Bound renderer native animator identity missing')
    animator_id = _integer(animator.get('instanceId'), 'animator id')
    require(observation.get('targetAnimatorInstanceId') == animator_id,
            'Motion target animator differs from bound renderer')
    baseline = observation.get('baseline')
    require(isinstance(baseline, dict) and baseline.get('baseLayerIdle') is True
            and baseline.get('baseLayerTransition') is False
            and baseline.get('animatorEnabled') is True and baseline.get('animatorActive') is True,
            'Capture did not arm on an enabled settled native IDLE state')
    require(baseline.get('animatorInstanceId') == animator_id,
            'Motion baseline animator differs from bound renderer')
    events = observation.get('events')
    require(isinstance(events, list) and observation.get('eventCount') == len(events)
            and len(events) <= 64, 'Bounded native motion event record missing or inconsistent')
    return observation, baseline, events


def _idle_sample(capture, renderer, observation):
    frames = capture.get('frames')
    require(isinstance(frames, list) and frames and isinstance(frames[0], dict),
            'No retained idle capture frame')
    frame = frames[0]
    require(type(frame.get('frame')) is int and frame['frame'] >= observation.get('startedFrame', frame['frame']),
            'Retained idle frame chronology missing')
    require(all(frame.get(key) == renderer.get(key) for key in ('instanceId', 'ownerInstanceId', 'celInstanceId')),
            'Retained idle frame differs from bound renderer')
    state = frame.get('motionAnimator')
    require(isinstance(state, dict) and state.get('animatorInstanceId') == observation.get('targetAnimatorInstanceId')
            and state.get('baseLayerIdle') is True and state.get('baseLayerTransition') is False
            and state.get('animatorEnabled') is True and state.get('animatorActive') is True,
            'First retained frame is not an enabled settled native IDLE state')
    return frame


def _exact_event(event, observation):
    require(event.get('targetOwnerInstanceId') == observation.get('targetOwnerInstanceId')
            and event.get('targetCelInstanceId') == observation.get('targetCelInstanceId')
            and event.get('targetRendererInstanceId') == observation.get('targetRendererInstanceId')
            and event.get('targetAnimatorInstanceId') == observation.get('targetAnimatorInstanceId')
            and same_fid(event.get('targetFid'), observation.get('targetFid')),
            'Native motion event differs from the bound renderer target')


def _attack_event(events, observation, role, target_fid, idle_frame, responses=None):
    for event in events:
        if not isinstance(event, dict):
            continue
        if event.get('nativeMethod') != 'CharacterDummy.PlayAttackSequence entry' or event.get('role') != role:
            continue
        _exact_event(event, observation)
        primary = event.get('primary')
        if not isinstance(primary, dict):
            continue
        if role == 'attacker':
            if not same_fid(event.get('actorFid'), target_fid):
                continue
        elif not same_fid(primary.get('victimFid'), target_fid):
            continue
        if responses is not None and primary.get('attackResponse') not in responses:
            continue
        require(event.get('finalized') is True and event.get('exception') is None,
                'Native PlayAttackSequence did not complete cleanly')
        require(type(event.get('sequence')) is int and type(event.get('frame')) is int,
                'Native PlayAttackSequence event lacks chronology')
        if event['frame'] <= idle_frame:
            continue
        return event
    response = 'any response' if responses is None else '/'.join(sorted(responses))
    raise RuntimeError('Missing exact native ' + role + ' PlayAttackSequence with ' + response
                       + ' after retained idle frame')


def _trigger_event(events, observation, after, expected):
    for event in events:
        if not isinstance(event, dict):
            continue
        if (event.get('nativeMethod') == 'CharacterEventListener.CombatTrigger entry'
                and type(event.get('sequence')) is int and event['sequence'] > after['sequence']
                and event.get('trigger') in expected):
            _exact_event(event, observation)
            require(event.get('finalized') is True and event.get('exception') is None,
                    'Native target CEL trigger did not complete cleanly')
            require(type(event.get('frame')) is int, 'Native trigger event lacks chronology')
            return event
    raise RuntimeError('Missing exact target CEL trigger after native action: ' + '/'.join(sorted(expected)))


def _sample_for_trigger(capture, trigger):
    frames = capture.get('frames')
    require(isinstance(frames, list) and frames, 'No retained capture frames')
    event_frame = trigger['frame']
    for index, frame in enumerate(frames):
        if not isinstance(frame, dict) or type(frame.get('frame')) is not int:
            continue
        if frame['frame'] >= event_frame and frame.get('lastCombatTrigger') == trigger['trigger']:
            return index
    raise RuntimeError('No retained exact-target frame follows native trigger ' + trigger['trigger'])


def _observed_victim_response(events, observation, target_fid, idle_frame):
    """Return one complete exact victim response, or None when none was observed.

    A no-damage attack can legitimately yield Block, Dodge, or no target event
    at all.  The bounded retry runner needs to preserve that observation without
    promoting it to a hit-motion gate.  Any event that *does* purport to belong
    to the exact target remains subject to the same identity, finalizer, and
    chronology checks as a successful hit.
    """
    for event in events:
        if not isinstance(event, dict):
            continue
        if event.get('nativeMethod') != 'CharacterDummy.PlayAttackSequence entry' or event.get('role') != 'victim':
            continue
        _exact_event(event, observation)
        primary = event.get('primary')
        if not isinstance(primary, dict) or not same_fid(primary.get('victimFid'), target_fid):
            continue
        require(event.get('finalized') is True and event.get('exception') is None,
                'Native PlayAttackSequence did not complete cleanly')
        require(type(event.get('sequence')) is int and type(event.get('frame')) is int,
                'Native PlayAttackSequence event lacks chronology')
        if event['frame'] > idle_frame:
            return event
    return None


def _sample_for_event(capture, event):
    frames = capture.get('frames')
    require(isinstance(frames, list) and frames, 'No retained capture frames')
    for index, frame in enumerate(frames):
        if isinstance(frame, dict) and type(frame.get('frame')) is int and frame['frame'] >= event['frame']:
            return index
    raise RuntimeError('No retained frame follows native action event')


def attack_response_observation(capture, renderer, target_fid):
    """Preserve a complete ordinary-attack response without calling it a hit.

    This validates the same arm, idle, identity, and any observed target-event
    invariants as the strict hit path.  It deliberately permits no exact victim
    event because a later bounded retry is authorized only by a complete
    same-target no-loss observation, never by an inferred block/dodge cause.
    """
    observation, baseline, events = _observation(capture, renderer, target_fid)
    idle_frame = _idle_sample(capture, renderer, observation)
    idle = {'label': 'idle', 'sampleIndex': 0, 'baseline': baseline,
            'frame': idle_frame['frame'], 'sample': idle_frame['motionAnimator']}
    event = _observed_victim_response(events, observation, target_fid, idle_frame['frame'])
    result = {
        'schema': 'ftkmf.exercise-attack-response-observation.v1', 'action': 'attack', 'idle': idle,
        'response': None,
        'limitations': 'This preserves an ordinary attack response without classifying its game cause or satisfying the nonlethal hit-motion gate.'
    }
    if event is not None:
        result['response'] = {'nativeAction': event, 'attackResponse': event['primary'].get('attackResponse'),
                              'sampleIndex': _sample_for_event(capture, event)}
    return result


def evidence_for_action(action, capture, renderer, target_fid):
    """Return review pointers for one existing pass, attack, or death capture.

    The first PNG is the idle view because Recorder waits for it before issuing
    the native bridge action.  Later pointers require an exact target event and
    a sampled target CEL trigger after that event.  Returned pointers still need
    human visual review of the original PNG sequence.
    """
    require(action in ('pass', 'attack', 'kill-fixture'), 'Unknown exercise action')
    observation, baseline, events = _observation(capture, renderer, target_fid)
    idle_frame = _idle_sample(capture, renderer, observation)
    idle = {'label': 'idle', 'sampleIndex': 0, 'baseline': baseline,
            'frame': idle_frame['frame'], 'sample': idle_frame['motionAnimator']}
    if action == 'pass':
        attack = _attack_event(events, observation, 'attacker', target_fid, idle_frame['frame'])
        trigger = _trigger_event(events, observation, attack, {
            'Attack', 'AttackCrit', 'AttackProf', 'AttackProf1', 'AttackProf2', 'Intro', 'ItemUse', 'Reload'
        })
        return {
            'schema': 'ftkmf.exercise-motion-evidence.v1', 'action': action, 'idle': idle,
            'attack': {'nativeAction': attack, 'nativeTrigger': trigger,
                       'sampleIndex': _sample_for_trigger(capture, trigger)},
            'limitations': 'Native action and trigger chronology select frames. Human review still judges pose, deformation, visibility and clip coverage.'
        }
    if action == 'attack':
        hit = _attack_event(events, observation, 'victim', target_fid, idle_frame['frame'], {'Damaged', 'DamagedHeavy'})
        expected = {hit['primary']['attackResponse']}
        trigger = _trigger_event(events, observation, hit, expected)
        return {
            'schema': 'ftkmf.exercise-motion-evidence.v1', 'action': action, 'idle': idle,
            'hit': {'nativeAction': hit, 'nativeTrigger': trigger,
                    'sampleIndex': _sample_for_trigger(capture, trigger)},
            'limitations': 'A nonlethal native damage response is proven. Human review still judges the retained hit pose and visual quality.'
        }
    death = _attack_event(events, observation, 'victim', target_fid, idle_frame['frame'], {'Death'})
    trigger = _trigger_event(events, observation, death, {'Death'})
    return {
        'schema': 'ftkmf.exercise-motion-evidence.v1', 'action': action, 'idle': idle,
        'death': {'nativeAction': death, 'nativeTrigger': trigger,
                  'sampleIndex': _sample_for_trigger(capture, trigger)},
        'limitations': 'This is an explicit KillSingle fixture on the native damage path, not ordinary lethal-damage proof. Human review still judges retained death frames and cleanup.'
    }


def review_points(evidence):
    """Return stable image labels and indices from evidence_for_action output."""
    points = [('idle', evidence['idle']['sampleIndex'])]
    for key in ('attack', 'hit', 'death', 'response'):
        value = evidence.get(key)
        if value is not None:
            points.append((key, value['sampleIndex']))
    return points
