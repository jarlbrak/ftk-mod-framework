from pathlib import Path
import unittest
SOURCE = (Path(__file__).parent / 'NativeInventoryFixture.cs').read_text()


class NativeInventoryBoundary(unittest.TestCase):
    def test_native_ui_routes_only(self):
        self.assertIn('hero.m_UIPlayMainHud.OnInventoryToggle();', SOURCE)
        self.assertIn('inventory.OnClose();', SOURCE)
        for forbidden in ('.SetActive(', '.Invoke(', '.SetValue(', '.RPC(', 'ForceEquip(', 'Save(', 'StartCoroutine('):
            self.assertNotIn(forbidden, SOURCE)

    def test_visibility_and_exact_owner_guards(self):
        for guard in ('RequireOutsideCombat();', 'member.GetInstanceID() == id', 'member.m_IsMoving',
                      '"m_MoveCoroutineRunning"', 'ui.IsModal', '.IsInteractable()',
                      'inventory.m_InventoryOwner != hero', 'focus.transform.IsChildOf(inventory.transform)',
                      'BlacksmithAppearanceScalars(hero.m_CharacterStats)', 'hero.m_Avatar != avatar'):
            self.assertIn(guard, SOURCE)


if __name__ == '__main__':
    unittest.main()
