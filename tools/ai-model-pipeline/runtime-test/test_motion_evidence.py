#!/usr/bin/env python3
import copy
import unittest

import motion_evidence


TARGET = {'photonId': 0, 'turnIndex': 7}
HERO = {'photonId': -1, 'turnIndex': 0}
RENDERER = {'instanceId': 31, 'ownerInstanceId': 21, 'celInstanceId': 22,
            'animator': {'instanceId': 23}}
TARGET_EVENT = {'targetOwnerInstanceId': 21, 'targetCelInstanceId': 22,
                'targetRendererInstanceId': 31, 'targetAnimatorInstanceId': 23,
                'targetFid': TARGET}
IDLE_STATE = {'animatorInstanceId': 23, 'baseLayerIdle': True,
              'baseLayerTransition': False, 'animatorEnabled': True,
              'animatorActive': True}


def attack(role, response, sequence=0, frame=101):
    return {
        **TARGET_EVENT,
        'nativeMethod': 'CharacterDummy.PlayAttackSequence entry', 'sequence': sequence, 'frame': frame,
        'role': role, 'actorFid': TARGET if role == 'attacker' else HERO,
        'primary': {'victimFid': HERO if role == 'attacker' else TARGET, 'attackResponse': response},
        'finalized': True, 'exception': None,
    }


def trigger(name, sequence=1, frame=102):
    return {
        **TARGET_EVENT,
        'nativeMethod': 'CharacterEventListener.CombatTrigger entry', 'sequence': sequence,
        'frame': frame, 'trigger': name, 'finalized': True, 'exception': None,
    }


def capture(events):
    return {
        'frames': [
            {'frame': 100, 'instanceId': 31, 'ownerInstanceId': 21, 'celInstanceId': 22,
             'lastCombatTrigger': 'None', 'motionAnimator': dict(IDLE_STATE)},
            {'frame': 102, 'lastCombatTrigger': events[-1].get('trigger', 'None')},
        ],
        'motionObservation': {
            'schema': 'ftkmf.native-combat-motion.v1', 'error': None,
            'startedFrame': 99, 'targetOwnerInstanceId': 21, 'targetCelInstanceId': 22,
            'targetRendererInstanceId': 31, 'targetAnimatorInstanceId': 23, 'targetFid': TARGET,
            'baseline': {
                'animatorInstanceId': 23,
                'baseLayerIdle': True, 'baseLayerTransition': False,
                'animatorEnabled': True, 'animatorActive': True,
            },
            'events': events, 'eventCount': len(events),
        },
    }


class MotionEvidenceTests(unittest.TestCase):
    def test_pass_requires_target_as_native_attacker_and_selects_post_trigger_frame(self):
        result = motion_evidence.evidence_for_action('pass', capture([
            attack('attacker', 'Damaged'), trigger('Attack'),
        ]), RENDERER, TARGET)
        self.assertEqual(result['attack']['sampleIndex'], 1)
        self.assertEqual(motion_evidence.review_points(result), [('idle', 0), ('attack', 1)])

    def test_attack_accepts_native_heavy_damage_and_exact_target_trigger(self):
        result = motion_evidence.evidence_for_action('attack', capture([
            attack('victim', 'DamagedHeavy'), trigger('DamagedHeavy'),
        ]), RENDERER, TARGET)
        self.assertEqual(result['hit']['nativeTrigger']['trigger'], 'DamagedHeavy')
        self.assertEqual(result['hit']['sampleIndex'], 1)

    def test_block_response_is_preserved_without_becoming_hit_motion(self):
        raw = capture([attack('victim', 'Block'), trigger('Defend')])
        result = motion_evidence.attack_response_observation(raw, RENDERER, TARGET)
        self.assertEqual(result['response']['attackResponse'], 'Block')
        self.assertEqual(result['response']['sampleIndex'], 1)
        self.assertEqual(motion_evidence.review_points(result), [('idle', 0), ('response', 1)])
        with self.assertRaisesRegex(RuntimeError, 'Damaged/DamagedHeavy'):
            motion_evidence.evidence_for_action('attack', raw, RENDERER, TARGET)

    def test_no_exact_victim_response_remains_an_unclassified_observation(self):
        result = motion_evidence.attack_response_observation(capture([
            attack('attacker', 'Damaged'), trigger('Attack'),
        ]), RENDERER, TARGET)
        self.assertIsNone(result['response'])

    def test_kill_fixture_requires_native_death_response_and_death_trigger(self):
        result = motion_evidence.evidence_for_action('kill-fixture', capture([
            attack('victim', 'Death'), trigger('Death'),
        ]), RENDERER, TARGET)
        self.assertEqual(result['death']['sampleIndex'], 1)

    def test_rejects_sampled_last_trigger_without_matching_native_event(self):
        bad = capture([attack('victim', 'Damaged')])
        bad['frames'][1]['lastCombatTrigger'] = 'Damaged'
        with self.assertRaisesRegex(RuntimeError, 'Missing exact target CEL trigger'):
            motion_evidence.evidence_for_action('attack', bad, RENDERER, TARGET)

    def test_rejects_native_trigger_that_did_not_finish_cleanly(self):
        bad = capture([attack('victim', 'Damaged'), trigger('Damaged')])
        bad['motionObservation']['events'][1]['exception'] = 'native failure'
        with self.assertRaisesRegex(RuntimeError, 'trigger did not complete cleanly'):
            motion_evidence.evidence_for_action('attack', bad, RENDERER, TARGET)

    def test_rejects_nonidle_arm_and_wrong_renderer_identity(self):
        bad = capture([attack('victim', 'Damaged'), trigger('Damaged')])
        bad['motionObservation']['baseline']['baseLayerIdle'] = False
        with self.assertRaisesRegex(RuntimeError, 'settled native IDLE'):
            motion_evidence.evidence_for_action('attack', bad, RENDERER, TARGET)
        wrong = copy.deepcopy(capture([attack('victim', 'Damaged'), trigger('Damaged')]))
        wrong['motionObservation']['targetRendererInstanceId'] = 99
        with self.assertRaisesRegex(RuntimeError, 'renderer differs'):
            motion_evidence.evidence_for_action('attack', wrong, RENDERER, TARGET)

    def test_rejects_action_that_precedes_the_retained_idle_frame(self):
        bad = capture([attack('victim', 'Damaged', frame=100), trigger('Damaged', frame=101)])
        with self.assertRaisesRegex(RuntimeError, 'after retained idle frame'):
            motion_evidence.evidence_for_action('attack', bad, RENDERER, TARGET)

    def test_rejects_nonidle_first_retained_frame_and_wrong_native_animator(self):
        bad = capture([attack('victim', 'Damaged'), trigger('Damaged')])
        bad['frames'][0]['motionAnimator']['baseLayerIdle'] = False
        with self.assertRaisesRegex(RuntimeError, 'First retained frame'):
            motion_evidence.evidence_for_action('attack', bad, RENDERER, TARGET)
        wrong = copy.deepcopy(capture([attack('victim', 'Damaged'), trigger('Damaged')]))
        wrong['motionObservation']['targetAnimatorInstanceId'] = 99
        with self.assertRaisesRegex(RuntimeError, 'animator differs'):
            motion_evidence.evidence_for_action('attack', wrong, RENDERER, TARGET)


if __name__ == '__main__':
    unittest.main()
