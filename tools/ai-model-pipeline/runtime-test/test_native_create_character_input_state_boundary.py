"""Source-boundary guards for the read-only native Party Select input probe."""
from pathlib import Path
import unittest


ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeCreateCharacterInputState.cs').read_text()
PLUGIN = (ROOT / 'Plugin.cs').read_text()
COMMAND = (ROOT / 'command.py').read_text()


class NativeCreateCharacterInputStateBoundaryTests(unittest.TestCase):
    def test_no_payload_operation_is_whitelisted_and_bypasses_only_the_game_guard(self):
        self.assertIn('CatalogKeys(command, "id", "session", "op")', SOURCE)
        self.assertIn("'native-create-character-input-state'", COMMAND)
        update = PLUGIN[PLUGIN.index('void Update()'):]
        self.assertIn('op == "native-create-character-input-state"', update)
        self.assertLess(
            update.index('op == "native-create-character-input-state"'),
            update.index('RequireSinglePlayer();'),
        )

    def test_reports_native_focus_graph_and_each_party_select_candidate(self):
        for required in (
            'FTKInput.Instance', 'input.m_CurrentInputFocus', 'FTKInput.GetSelectable()',
            '"currentFocus"', '"currentSelected"',
            '"characterCreateRoot"', 'NativeCreateInputFocusState(rootFocus)',
            'menu.m_CreateUIs', 'uiQuickPlayerCreate candidate',
            'candidate.m_ClassID', 'candidate.m_PlayerClass.text', 'candidate.m_TurnIndex',
            'candidate.m_Mode.ToString()', 'candidate.m_ClaimDevice',
            'NativeCreateTryReadMember(device, "m_Type", out type)',
            'type.GetProperty(name, Members | BindingFlags.DeclaredOnly)',
            'candidate.m_Interactable',
            'candidate.m_InputFocus', 'focus.m_HasInputFocus',
            'focus.m_CurrentSelected', 'focus.m_CurrentSelectables',
            '"claimed"', '"currentSelectableNames"',
            '"observed_actual_native_party_select"',
            '"unavailable_native_party_select"',
        ):
            self.assertIn(required, SOURCE)

    def test_probe_does_not_drive_native_focus_or_character_creation(self):
        for forbidden in (
            'SetFocus(', 'SetClass(', 'OnClassClick(', 'OnStartGame(',
            'ShowCreateCharacter(', 'CreateAllCreatePlayerUIs(', 'CreateUI(',
            'Select(', 'Invoke(', 'StartCoroutine(', 'AddComponent(',
        ):
            self.assertNotIn(forbidden, SOURCE)

    def test_claim_probe_accepts_the_live_property_without_a_static_enum_member_reference(self):
        self.assertIn('property.GetValue(owner, null)', SOURCE)
        self.assertNotIn('device.m_Type', SOURCE)
        self.assertNotIn('AssignDevice.Type', SOURCE)


if __name__ == '__main__':
    unittest.main()
