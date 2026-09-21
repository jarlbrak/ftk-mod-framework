"""Boundary checks for the inspected native Options Menu Save/Exit route."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeSaveExit.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
PLUGIN = (ROOT / 'Plugin.cs').read_text()
COMMAND = (ROOT / 'command.py').read_text()


class NativeSaveExitBoundaryTests(unittest.TestCase):
    def test_command_is_whitelisted_and_requires_inspection(self):
        self.assertIn("'native-save-exit'", COMMAND)
        self.assertIn("'native-save-exit-state'", COMMAND)
        self.assertIn('op == "native-save-exit"', PLUGIN)
        self.assertIn('op == "native-save-exit-state"', PLUGIN)
        self.assertIn('CatalogKeys(command, "id", "session", "op", "action", "inspectionToken", "buttonInstanceId")', SOURCE)
        self.assertIn('if (action != "inspect" && action != "submit")', SOURCE)

    def test_requires_visible_options_menu_and_exact_callback(self):
        for required in (
            'menu = uiOptionsMenu.Instance', 'menu.m_Showing',
            '!GameLogic.Instance.IsSinglePlayer()', 'EncounterSession.Instance.m_IsInCombat',
            'menu.GetType().GetField("m_SaveOptions", Members)',
            'panel.GetType().GetField("m_SaveExit", Members)',
            'NativeSaveExitControlActive(control)',
            'control.onClick.GetPersistentEventCount() != 1',
            'control.onClick.GetPersistentTarget(0)',
        ):
            self.assertIn(required, SOURCE)

    def test_can_invoke_only_the_verified_button_once(self):
        self.assertEqual(len(re.findall(r'control\.onClick\.Invoke\s*\(', CODE)), 1)
        for forbidden in ('SaveAndQuit', 'SendEvent', 'File.', 'Directory.'):
            self.assertNotRegex(CODE, r'\b' + forbidden + r'\s*\(')
        self.assertIn('nativeSaveExitConsumed = true;', SOURCE)
        self.assertIn('Str(command, "inspectionToken") != nativeSaveExitToken', SOURCE)
        self.assertIn('Int(command, "buttonInstanceId", 0) != control.GetInstanceID()', SOURCE)

    def test_metadata_route_is_read_only(self):
        start = SOURCE.index('JObject NativeSaveExitState')
        end = SOURCE.index('JObject InspectNativeSaveExit', start)
        metadata = SOURCE[start:end]
        self.assertIn('"readOnly", true', metadata)
        self.assertIn('"native_save_exit_metadata"', metadata)
        self.assertNotIn('.Invoke(', metadata)


if __name__ == '__main__':
    unittest.main()
