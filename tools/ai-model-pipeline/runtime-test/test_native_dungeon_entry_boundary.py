"""No simulation: checks mutation boundaries for the isolated native callback."""
from pathlib import Path
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / "NativeDungeonEntry.cs").read_text()


class NativeDungeonEntryBoundaryTests(unittest.TestCase):
    def test_all_readiness_checks_precede_one_shot_callback(self):
        callback = SOURCE.index("entry.OnClick();")
        for guard in ("RequireSinglePlayer();", "RequireOutsideCombat();", "CatalogNoLinks(root);",
                      "GameFlow.Instance.m_DungeonEntered != null", "FTKHub.Instance.AnyPlayersInDungeon()",
                      "order.Count != 0", "target.m_DungeonEncounters.Count != 0", "party.Count != 3",
                      "nativeParty.Count != 3", "hero.IsInDungeon()", "hero.m_HexLand != actor.m_HexLand",
                      "hero.m_HexLand.m_HexInfo.m_Realm != target.m_HexLand.m_HexInfo.m_Realm",
                      "!nativeParty.Contains(hero.m_FTKPlayerID)", "!target.ShowLocationMenu()",
                      'Str(command, "inspectionToken") != nativeDungeonEntryToken', "nativeDungeonEntryConsumed = true;"):
            self.assertLess(SOURCE.index(guard), callback)
        self.assertEqual(SOURCE.count("entry.OnClick();"), 1)

    def test_menu_must_be_native_visible_matching_and_interactable(self):
        for guard in ('target.ShowLocationMenu(actor, true, null, false)', 'display.IsShowing()',
                      'display.m_FSM.ActiveStateName != "Showing"', 'Field(display, "m_CurrentCow")',
                      'Field(display, "m_MiniHexInfo")', 'candidate.m_Menu.m_Location != target',
                      'candidate.m_Menu.m_Cow != actor', '!entry.m_Button.IsInteractable()',
                      'entry != nativeDungeonEntryButton'):
            self.assertIn(guard, SOURCE)
        self.assertNotIn('target.OnLoadParty(actor)', SOURCE)
        self.assertNotIn('display.Show2(', SOURCE)

    def test_no_alternate_entry_or_forced_state(self):
        for forbidden in ("SnapTo(", "SnapToRPC(", "SetEnteredDungeon(", "SendEvent(",
                          "GenerateDungeonEncounters(", "SaveGame(", "PlayerPrefs", "m_DungeonEntered =",
                          "m_IsInCombat =", "m_PlayerLevel =", "m_HealthCurrent =", "ACK"):
            self.assertNotIn(forbidden, SOURCE)

    def test_never_used_encounter_sessions_can_be_absent(self):
        self.assertIn('(order != null && order.Count != 0)', SOURCE)
        self.assertNotIn('session == null || master == null || order == null', SOURCE)
        self.assertIn('hero.m_CharacterStats.m_IsInCombat', SOURCE)
        self.assertIn('RequireOutsideCombat();', SOURCE)

    def test_map_neighbors_are_observed_without_generation_or_movement(self):
        source = (ROOT / "DungeonMapObservation.cs").read_text()
        for expected in ('origin.m_Neighbors', 'hex.m_HexInfo.m_Realm', 'hex.m_POI==null && hex.m_AirPOI==null', 'hex.CanTravelLandOnly()'):
            self.assertIn(expected, source)
        for forbidden in ('SnapTo(', 'GetRandomNeighbor', 'RevealNeighbor', 'OnLoadParty(', 'Generate'):
            self.assertNotIn(forbidden, source)

    def test_operation_whitelisted(self):
        self.assertIn('op == "native-dungeon-entry"', (ROOT / "Plugin.cs").read_text())
        self.assertIn("'native-dungeon-entry'", (ROOT / "command.py").read_text())


if __name__ == "__main__":
    unittest.main()
