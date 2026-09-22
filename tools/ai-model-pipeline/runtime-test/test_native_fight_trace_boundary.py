"""Trace authority boundaries; observing a prefix is not native completion evidence."""
from pathlib import Path
import re
import unittest
ROOT=Path(__file__).parent
SOURCE=(ROOT/'NativeFightTrace.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
class NativeFightTraceBoundary(unittest.TestCase):
    def test_never_drives_native_input_or_continuation(self):
        for name in ('Invoke','SetValue','SetFocus','SendEvent','OnClick','OnFight',
                     'LocalInitCombatSession','InitiateEncounterSessionRPC','Acknowledge',
                     'UseFightButton','Continue','Event'):
            self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertNotRegex(CODE,r'\bm_\w+\s*=(?!=)')
        self.assertNotIn('__args',CODE)
        self.assertNotIn('Finalizer',SOURCE)
    def test_actual_popup_and_menu_fight_continuation_are_observed(self):
        self.assertIn('typeof(uiEnemyPoiMenu).GetMethod("UseFightButton",Members,null,Type.EmptyTypes,null)',SOURCE)
        self.assertIn('typeof(ContinueFSM).GetMethod("Continue",Members,null,new[]{typeof(object)},null)',SOURCE)
        self.assertIn('(string)_v!="menuFight"',SOURCE)
        self.assertIn('continuation.m_FSM.ActiveStateName',CODE)
        self.assertIn('continuation.m_FSM.Name',CODE)
        self.assertIn('continuation.m_WaitCount',CODE)
        self.assertIn('JToken.DeepEquals(state,nativeFightContinuationLast)',CODE)
        self.assertIn('Unpatch(method,NativeFightContinuePrefixMethod())',CODE)
        self.assertIn('GetField("m_CompleteDelegate",Members).GetValue(continuation)',SOURCE)

    def test_fsm_event_trace_is_exact_and_reference_filtered(self):
        self.assertIn('typeof(Fsm).GetMethod("Event",Members,null,new[]{typeof(string)},null)',SOURCE)
        self.assertIn('object.ReferenceEquals(__instance,observer.nativeFightContinuation.m_FSM)',CODE)
        self.assertIn('fsmEventName!="menuFight"',SOURCE)
        self.assertIn('fsm.ActiveState.Transitions',CODE)
        self.assertIn('fsm.GlobalTransitions',CODE)
        self.assertIn('fsm.Owner.enabled',CODE)
        self.assertIn('fsm.GameObject.activeInHierarchy',CODE)
        self.assertIn('fsm.EventTarget.target.ToString()',CODE)
        self.assertIn('Unpatch(method,NativeFightEventPrefixMethod())',CODE)

    def test_fixed_scope_expiry_and_record_caps(self):
        self.assertIn('Time.realtimeSinceStartup+60f',CODE)
        self.assertIn('nativeFightRecords.Count>=64',CODE)
        self.assertIn('entries.Count<32',CODE)
        self.assertIn('GetPersistentEventCount(),16',CODE)
        self.assertIn('CatalogKeys(command,"id","session","op","action")',SOURCE)
        self.assertIn('Unpatch(method,NativeFightPrefixMethod())',CODE)
        self.assertIn('catch{NativeFightDisarm();throw;}',CODE)
    def test_expiry_and_destruction_remove_only_own_hooks(self):
        plugin=(ROOT/'Plugin.cs').read_text()
        self.assertIn('NativeFightTraceTick();',plugin)
        self.assertRegex(plugin,r'void OnDestroy\(\)\{[^}]*NativeFightDisarm\(\);')
        self.assertNotIn('UnpatchAll',CODE)
        self.assertIn("OPS += ('native-fight-trace',)",(ROOT/'command.py').read_text())
if __name__=='__main__':unittest.main()
