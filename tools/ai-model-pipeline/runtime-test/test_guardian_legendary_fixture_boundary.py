"""Fixture authority boundaries; gameplay results require native live trials."""
from pathlib import Path
import re
import unittest

SOURCE = Path(__file__).with_name("GuardianLegendaryFixture.cs").read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class GuardianLegendaryFixtureBoundary(unittest.TestCase):
    def test_only_native_input_setup_not_legendary_or_guard_execution(self):
        for name in ("ApplyGuard", "ApplyLegendaryGuard", "BeginGuard", "ResolveMitigation",
                     "BeginAttack", "ObserveEquipment", "ResetActor", "AddItem", "SaveGame",
                     "RespondToHit", "PlayAttackSequence", "SetValue"):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\b(?:File|PlayerPrefs)\s*\.')
        self.assertNotRegex(CODE, r'\.m_(?:FocusPoints|PoisonLvl)\s*=(?!=)')
        self.assertIn('stats.UpdateFocusPoints(focus - stats.m_FocusPoints, true)', CODE)
        self.assertIn('stats.SetPoison(poison, false, false)', CODE)

    def test_setup_claim_precedes_both_native_mutations(self):
        claim = CODE.index('guardianLegendaryClaims.Add(claim)')
        self.assertLess(claim, CODE.index('stats.UpdateFocusPoints('))
        self.assertLess(claim, CODE.index('stats.SetPoison('))
        self.assertIn('focus > stats.m_FocusPoints', CODE)
        self.assertIn('deficit > 3', CODE)
        self.assertIn('poison > 1', CODE)

    def test_exact_owned_encounter_and_actor_stance(self):
        self.assertIn('RequireSinglePlayer();', CODE)
        self.assertIn('CatalogNoLinks(root);', CODE)
        self.assertIn('encounter.GetInstanceID() != LeaseObservationPin.ExactId', CODE)
        self.assertIn('buttons.CombatCow != actor.m_CharacterOverworld', CODE)
        self.assertIn('!found.m_CharacterOverworld.IsOwner', CODE)
        self.assertIn('!actor.m_IsAlive || !target.m_IsAlive', CODE)
        self.assertIn('actor == target', CODE)
        self.assertIn('guardianDamageReceipt.Policy.Active', CODE)

    def test_inspection_returns_before_mutation_and_only_queries_charge(self):
        inspect_start = SOURCE.index('if (action == "inspect")')
        setup_start = SOURCE.index('uiBattleStanceButtons buttons')
        inspection = SOURCE[inspect_start:setup_start]
        self.assertIn('return new JObject', inspection)
        for mutation in ('UpdateFocusPoints', 'SetPoison', 'guardianLegendaryClaims.Add'):
            self.assertNotIn(mutation, inspection)
        self.assertIn('"IsCharged", GuardianFixtureIdentity(hero)', SOURCE)


if __name__ == '__main__':
    unittest.main()
