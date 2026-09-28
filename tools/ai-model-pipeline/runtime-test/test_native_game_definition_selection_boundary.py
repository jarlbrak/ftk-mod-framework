"""Boundary checks for the inspected native campaign-row callback."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeGameDefinitionSelection.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
PLUGIN = (ROOT / 'Plugin.cs').read_text()
COMMAND = (ROOT / 'command.py').read_text()


class NativeGameDefinitionSelectionBoundaryTests(unittest.TestCase):
    def test_command_is_whitelisted_and_explicit(self):
        self.assertIn("'native-game-definition-select'", COMMAND)
        self.assertIn('op == "native-game-definition-select"', PLUGIN)
        self.assertIn('CatalogKeys(command, "id", "session", "op", "action", "inspectionToken", "buttonInstanceId")', SOURCE)
        self.assertIn('action != "inspect" && action != "submit"', SOURCE)

    def test_inspection_requires_active_native_new_game_and_exact_callback(self):
        for required in (
            'uiScreen.gCurrent != config', 'config.m_IsResume', 'menu.m_GameStarted',
            'FTKInput.Instance.m_CurrentInputFocus != config', 'config.m_GameDefButtons',
            'button.onClick.GetPersistentEventCount() != 1',
            'button.onClick.GetPersistentTarget(0) == candidate',
            'button.onClick.GetPersistentMethodName(0) == "OnClick"',
            '"inspectionToken"', '"buttonInstanceId"',
        ):
            self.assertIn(required, SOURCE)

    def test_submit_uses_only_pinned_native_selection_callback(self):
        self.assertEqual(len(re.findall(r'\btarget\.OnClick\s*\(', CODE)), 1)
        for forbidden in ('PlayerPrefs.Set', 'SetActive', 'SendEvent', 'CreateGame', 'CreateOfflineRoom', 'onClick.Invoke'):
            self.assertNotIn(forbidden, CODE)
        self.assertIn('context.button != target', SOURCE)
        self.assertIn('Str(command, "inspectionToken") != context.token', SOURCE)
        self.assertIn('context.saveFileName != NativeGameDefinitionSaveName(target)', SOURCE)
        self.assertIn('nativeGameDefinitionSelectionConsumed = true;', SOURCE)
        self.assertIn('selectedSaveName != context.saveFileName', SOURCE)


if __name__ == '__main__':
    unittest.main()
