"""Fixture authority checks; native Focus animation still requires live validation."""
from pathlib import Path
import re
import unittest
SOURCE=Path(__file__).with_name('NativeCombatFocus.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"','',SOURCE)
class NativeCombatFocusBoundary(unittest.TestCase):
    def test_native_input_only(self):
        self.assertEqual(CODE.count('button.OnRightClick();'),1)
        for name in ('FocusSlot','FocusSlotAnimateFinish','UpdateFocusPoints','Attack','OnLeftClick','SaveGame','SetValue'):
            self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertNotRegex(CODE,r'\.(?:SpentFocus|m_FocusPoints|m_Focusing|m_FocusInterrupt)\s*(?:=(?!=)|\+\+|--)')
    def test_one_shot_consumed_before_input(self):
        self.assertLess(CODE.index('nativeFocusReceipt=r;'),CODE.index('button.OnRightClick();'))
        self.assertIn('RequireSinglePlayer();',CODE)
        self.assertIn('Time.realtimeSinceStartup>r.Deadline',CODE)
        self.assertNotIn('OnRightClick',CODE[CODE.index('void NativeCombatFocusTick()'):])
    def test_actor_selection_and_exact_debit_pins(self):
        for text in ('m_CurrentSelected.gameObject!=r.Button.gameObject','m_EventListener!=r.Avatar',
                     'm_ActiveDiorama!=r.Diorama','r.AfterFocus==r.BeforeFocus-1',
                     'r.AfterSpent==r.BeforeSpent+1','m_CombatActionProfile.m_NoFocus',
                     '!cow.m_CharacterStats.CanFocus()','r.BeforeSpent>=r.Slots'):
            self.assertIn(text,CODE)
        self.assertIn('Guard is excluded',SOURCE)
        self.assertIn('nativeFocusReceipt.Stage=="unknown"',SOURCE)
if __name__=='__main__':unittest.main()
