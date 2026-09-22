"""Boundary checks for the inspected native title New Game route."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeTitleNewGame.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
PLUGIN = (ROOT / 'Plugin.cs').read_text()
COMMAND = (ROOT / 'command.py').read_text()


class NativeTitleNewGameBoundaryTests(unittest.TestCase):
    def test_command_is_whitelisted_and_requires_an_explicit_action(self):
        self.assertIn("'native-title-new-game'", COMMAND)
        self.assertIn('op == "native-title-new-game"', PLUGIN)
        self.assertIn('CatalogKeys(command,"id","session","op","action","inspectionToken","buttonInstanceId")', SOURCE)
        self.assertIn('if(action!="inspect" && action!="submit")', SOURCE)

    def test_inspection_requires_the_native_title_screen_and_real_callback(self):
        for required in (
            'uiScreen.gCurrent as MainScreen', 'uiStartGame.Instance',
            'FTKInput.Instance.m_CurrentInputFocus!=screen',
            'button.onClick.GetPersistentEventCount()!=1',
            'button.onClick.GetPersistentTarget(0)!=screen',
            'button.onClick.GetPersistentMethodName(0)!="OnNewGame"',
            '"path",TitlePath(button.transform,screen.transform)',
            '"inspectionToken"', '"selectedButtonInstanceId"',
        ):
            self.assertIn(required, SOURCE)

    def test_only_verified_native_callback_can_change_the_title_state(self):
        self.assertEqual(len(re.findall(r'\.OnNewGame\s*\(', CODE)), 1)
        for forbidden in ('SendEvent', 'onClick.Invoke', 'SetActive', 'SetClass', 'CreateUI'):
            self.assertNotRegex(CODE, r'\b' + forbidden + r'\s*\(')
        self.assertIn('nativeTitleNewGameConsumed=true;screen.OnNewGame();', SOURCE)
        self.assertIn('Str(command,"inspectionToken")!=nativeTitleNewGameToken', SOURCE)
        self.assertIn('Int(command,"buttonInstanceId",0)!=nativeTitleNewGameButton.GetInstanceID()', SOURCE)
        self.assertIn('!candidates.Contains(nativeTitleNewGameButton)', SOURCE)


if __name__ == '__main__':
    unittest.main()
