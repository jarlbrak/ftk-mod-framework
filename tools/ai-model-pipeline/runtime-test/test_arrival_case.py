import argparse
import unittest
from unittest import mock

import arrival_case


class PassiveArrivalReadyTests(unittest.TestCase):
    def test_read_only_ready_preflight_accepts_exact_slot(self):
        runner = arrival_case.Arrival.__new__(arrival_case.Arrival)
        runner.a = argparse.Namespace(level=0, room=2, ready_timeout=5)
        runner.check_inputs = mock.Mock()
        runner.state = mock.Mock(return_value={
            "singlePlayer": True,
            "party": [{"hp": 10}],
            "dungeon": {"inDungeon": True, "level": 0, "room": 2},
        })
        fixture = {
            "strictReady": {"ok": True, "level": 0, "room": 2, "buttonCount": 1},
            "dungeon": {
                "level": 0,
                "room": 2,
                "slotAllowsEnemySubstitution": True,
                "queuedRoomType": "Enemy",
            },
        }
        runner.helper = mock.Mock(return_value=fixture)
        runner.log = mock.Mock()

        self.assertEqual(runner.wait_for_ready_guard(), fixture)
        runner.helper.assert_called_once_with("fixture-state")
        runner.log.assert_called_once()

    def test_party_death_stops_before_fixture_read(self):
        runner = arrival_case.Arrival.__new__(arrival_case.Arrival)
        runner.a = argparse.Namespace(level=0, room=2, ready_timeout=5)
        runner.check_inputs = mock.Mock()
        runner.state = mock.Mock(return_value={
            "singlePlayer": True,
            "party": [{"hp": 0}],
            "dungeon": {"inDungeon": True, "level": 0, "room": 2},
        })
        runner.helper = mock.Mock()
        runner.log = mock.Mock()

        with self.assertRaisesRegex(RuntimeError, "Party death"):
            runner.wait_for_ready_guard()
        runner.helper.assert_not_called()

    def test_post_arrival_ready_observation_accepts_non_enemy_next_slot(self):
        runner = arrival_case.Arrival.__new__(arrival_case.Arrival)
        runner.a = argparse.Namespace(post_ready_timeout=5)
        runner.check_inputs = mock.Mock()
        runner.state = mock.Mock(return_value={
            "singlePlayer": True,
            "party": [{"hp": 10}],
            "dungeon": {"inDungeon": True, "level": 0, "room": 3},
        })
        fixture = {
            "strictReady": {"ok": True, "level": 0, "room": 3, "buttonCount": 1},
            "dungeon": {"level": 0, "room": 3, "queuedRoomType": "Trap"},
            "identity": {"session": "a" * 32},
        }
        runner.helper = mock.Mock(return_value=fixture)
        runner.log = mock.Mock()

        observed = runner.wait_for_native_ready(0, 3)
        self.assertEqual(observed["strictReady"], fixture["strictReady"])
        self.assertEqual(observed["dungeon"]["queuedRoomType"], "Trap")
        runner.helper.assert_called_once_with("fixture-state")


if __name__ == "__main__":
    unittest.main()
