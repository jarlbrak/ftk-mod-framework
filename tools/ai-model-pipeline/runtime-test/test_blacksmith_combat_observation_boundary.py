from pathlib import Path
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'BlacksmithCombatObservation.cs').read_text()


class BlacksmithCombatObservationBoundaryTests(unittest.TestCase):
    def test_no_mechanics_queries_or_writes(self):
        # State getters allocate actors, and the patched native Armor getter observes equipment.
        for forbidden in ('.Invoke(', '.SetValue(', 'stats.TotalArmor', 'dummy.ArmorMod',
                          'ObserveEquipment(', 'TemperAvailable(', 'Available(', 'BeginTurn(',
                          'TryCommit(', 'StartCoroutine(', '.RPC(', 'SetHealth(', 'UpdateFocusPoints('):
            self.assertNotIn(forbidden, SOURCE)
        self.assertIn('RuntimeFieldRead.Read(target, name)', SOURCE)
        self.assertIn('"nativeTotalArmor"] = new JValue((object)null)', SOURCE)

    def test_exact_scope_and_receipts(self):
        for required in ('RequireSinglePlayer();', 'command.Properties()',
                         'FTKHub.Instance.m_CharacterOverworlds', 'ReferenceEquals(dummy.m_CharacterOverworld, hero)',
                         '"commits"', '"starts"', '"ends"', '"TemperAppliedDuring"',
                         '"TemperUsed"', '"SetSource"', '"runtimeSessionMatches"',
                         'ModuleVersionId', 'sha.ComputeHash(stream)', '"session", sessionId'):
            self.assertIn(required, SOURCE)

    def test_operation_is_wired(self):
        self.assertIn('Finish(id,BlacksmithCombatObservation(command))', (ROOT / 'Plugin.cs').read_text())
        self.assertIn("'blacksmith-combat-state'", (ROOT / 'command.py').read_text())


if __name__ == '__main__':
    unittest.main()
