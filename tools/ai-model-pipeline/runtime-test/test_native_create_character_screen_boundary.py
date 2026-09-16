"""Source authority guards for the one-callback native character-create route."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeCreateCharacterScreen.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
PLUGIN = (ROOT / 'Plugin.cs').read_text()
COMMAND = (ROOT / 'command.py').read_text()


class NativeCreateCharacterScreenBoundaryTests(unittest.TestCase):
    def test_command_has_no_user_controlled_fields_and_is_whitelisted(self):
        self.assertIn('CatalogKeys(command, "id", "session", "op")', SOURCE)
        self.assertIn('op == "native-create-character-preflight"', PLUGIN)
        self.assertIn('op == "native-create-character-screen"', PLUGIN)
        self.assertIn("'native-create-character-preflight'", COMMAND)
        self.assertIn("'native-create-character-screen'", COMMAND)

    def test_read_only_preflight_bypasses_the_game_guard_only_to_report_the_gate(self):
        start = SOURCE.index('JObject NativeCreateCharacterPreflight(JObject command)')
        end = SOURCE.index('static JObject NativeCreateCandidate', start)
        preflight = SOURCE[start:end]
        self.assertIn('CatalogKeys(command, "id", "session", "op")', preflight)
        self.assertIn('CatalogNoLinks(root)', preflight)
        self.assertIn('InspectNativeCreateCharacterScreen()', preflight)
        self.assertNotIn('RequireSinglePlayer()', preflight)
        for forbidden in ('OnStartGame', 'ShowCreateCharacter', 'CreateAllCreatePlayerUIs', 'CreateUI', 'SetClass'):
            self.assertNotIn(forbidden, preflight)

        update = PLUGIN[PLUGIN.index('void Update()'):]
        self.assertLess(
            update.index('op == "native-create-character-preflight"'),
            update.index('RequireSinglePlayer();'),
        )

    def test_only_the_native_create_game_callback_is_invoked(self):
        self.assertEqual(len(re.findall(r'\.OnStartGame\s*\(', CODE)), 1)
        for forbidden in (
            'ShowCreateCharacter', 'CreateAllCreatePlayerUIs', 'CreateUI',
            'SetClass', 'SyncSettings', 'RandomClass', 'AddComponent',
        ):
            self.assertNotRegex(CODE, r'\b' + forbidden + r'\s*\(')

    def test_preflight_pins_the_live_screen_and_success_requires_native_ownership(self):
        for required in (
            'currentScreen == config', 'config.m_CreateGame.interactable',
            'config.m_IsResume', 'menu.m_UseOnlineSinglePlayer',
            'logic.IsSinglePlayer()', 'menu.m_CreateUIs.Count',
            'bool eligible = menuPresent && configPresent && logicPresent',
            'if (!inspection.eligible)',
            'menu.GetInstanceID()', 'config.GetInstanceID()',
            'config.m_CreateGame.GetInstanceID()',
        ):
            self.assertIn(required, SOURCE)
        for required in (
            'NativeCreateMapReady()', 'NativeCreateRootActive(context.menu)',
            'NativeCreateCandidates(context.menu, out candidateReady)',
            'avatar.m_uiQuickPlayerCreate == candidate',
            'avatar.transform.parent == candidate.m_CharacterPos',
            'waitedFrames < 3600',
        ):
            self.assertIn(required, SOURCE)

    def test_read_only_report_exposes_every_native_create_gate(self):
        for required in (
            '"eligible", eligible',
            '"menu", NativeCreateObjectState(menu)',
            '"config", NativeCreateObjectState(config)',
            '"logic", NativeCreateObjectState(logic)',
            '"currentScreen", NativeCreateObjectState(currentScreen)',
            '"createGameButton", NativeCreateObjectState',
            '"isResume"', '"useOnlineSinglePlayer"', '"isSinglePlayer"',
            '"gameDefinitionPresent"', '"m_GameStarted"', '"createUiCount"',
            '"currentScreenMatchesConfig"', '"configActive"',
            '"createGameButtonActive"', '"createGameButtonInteractable"',
            '"notResume"', '"notOnlineSinglePlayer"', '"notGameStarted"',
            '"createUiListPresent"', '"noCreateUis"',
        ):
            self.assertIn(required, SOURCE)


if __name__ == '__main__':
    unittest.main()
