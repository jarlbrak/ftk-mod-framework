"""Appearance fixture authority boundaries, not native fit acceptance."""
from pathlib import Path
import re
import unittest
ROOT = Path(__file__).parent
SOURCE = (ROOT / 'BlacksmithAppearanceFixture.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class BlacksmithAppearanceBoundary(unittest.TestCase):
    def test_only_skin_field_is_written_and_restored_synchronously(self):
        self.assertEqual(re.findall(r'\b(m_\w+)\s*=(?!=)', CODE), ['m_SkinType', 'm_SkinType'])
        self.assertRegex(CODE, r'try\s*\{\s*pin.hero.m_SkinType = desired;\s*pin.hero.AssignAvatar\(\);\s*\}\s*finally\s*\{\s*pin.hero.m_SkinType = pin.original;')
        for forbidden in ('StartCoroutine', 'yield return', 'Save(', 'RPC(', 'ForceEquip(', 'SetValue(', '.vertices', '.triangles'):
            self.assertNotIn(forbidden, CODE)

    def test_scope_and_preservation_guards(self):
        for guard in ('ExactBlacksmithHero(id)', 'RequireOutsideCombat();', 'member.m_IsMoving',
                      '"m_MoveCoroutineRunning"', '"m_Queue"', 'uiPlayerInventory.Instance.m_IsShowing',
                      'ReferenceEquals(pin.hero.m_CharacterStats, pin.stats)',
                      'ReferenceEquals(pin.hero.m_PlayerInventory, pin.inventory)',
                      'BlacksmithPreservedEquals(statsBefore, BlacksmithAppearanceScalars(pin.stats))',
                      'Time.frameCount > pin.frame', 'pin.retired == null', 'pin.hero.m_Avatar != pin.avatar',
                      'BlacksmithAppearanceRebuild(blacksmithAppearance.original)'):
            self.assertIn(guard, SOURCE)

    def test_studio_exact_identity_and_provenance(self):
        self.assertIn('pin.avatar.GetInstanceID() != avatarId', SOURCE)
        self.assertIn('if (!pin.valid || !BlacksmithAppearanceSettled()', SOURCE)
        self.assertIn('"desiredVisualSkin"', SOURCE)
        self.assertIn('"restoredSerializedSkin"', SOURCE)
        studio = (ROOT / 'PlayerStudio.cs').read_text()
        self.assertIn('appearance=BlacksmithAppearanceStudio(ownerId,celId);', studio)
        self.assertIn('result["appearanceFixture"]=appearance;', studio)


if __name__ == '__main__':
    unittest.main()
