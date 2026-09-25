"""Retired orchestration must fail before filesystem or game mutation."""
import unittest
from types import SimpleNamespace
from unittest.mock import patch
from exercise_case import Exercise
from record_case import Recorder
from record_player_case import PlayerRecorder
from run_case import Runner


class RetirementTests(unittest.TestCase):
    def test_recorders_reject_missing_and_legacy_actions_before_parent(self):
        with patch.object(Runner, '__init__') as parent:
            for cls in (Recorder, PlayerRecorder):
                for action in (None, 'pass', 'attack', 'kill-fixture'):
                    with self.subTest(cls=cls.__name__, action=action):
                        with self.assertRaisesRegex(ValueError, 'retired'):
                            cls(SimpleNamespace(action=action))
            parent.assert_not_called()

    def test_exercise_cannot_reach_staging_even_with_observe_override(self):
        with patch.object(Recorder, '__init__') as parent:
            for args in (SimpleNamespace(), SimpleNamespace(action='observe')):
                with self.assertRaisesRegex(ValueError, 'retired'): Exercise(args)
            parent.assert_not_called()


if __name__ == '__main__': unittest.main()
