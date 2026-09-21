"""Authority boundary checks; native input diagnosis still needs a live snapshot."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'WorldInputObservation.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class WorldInputObservationBoundary(unittest.TestCase):
    def test_no_input_polling_events_or_mutation(self):
        for name in ('GetButton', 'GetButtonDown', 'GetButtonUp', 'SetValue',
                     'Invoke', 'SetFocus', 'SendEvent', 'SetActive', 'Destroy',
                     'Instantiate', 'StartCoroutine', 'OnRightClick', 'Save',
                     'Wait', 'Acknowledge', 'AcknowledgeReceived'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')

    def test_creation_snapshot_never_serializes_names_or_full_save_values(self):
        self.assertIn('WorldInputInteger(saved,1)', CODE)
        self.assertIn('WorldInputInteger(saved,4)', CODE)
        self.assertIn('Math.Min(saved.Length,64)', CODE)
        self.assertIn('saved[i].GetType().FullName', CODE)
        self.assertNotIn('m_PlayerNameStr', CODE)
        self.assertNotIn('saved[3]', CODE)
        self.assertNotIn('FromObject', CODE)
        self.assertIn('screen.m_MainColorArray', CODE)
        self.assertIn('m_CreateUITargets', CODE)
        self.assertIn('m_PlayerTargets', CODE)

    def test_encounter_waiter_is_read_only_and_bounded(self):
        self.assertIn('GetField("m_WaitForClientAck",Members).GetValue(master)', SOURCE)
        self.assertIn('Math.Min(ack.m_WaitList.Count,64)', CODE)
        self.assertIn('Math.Min(ids.Length,64)', CODE)
        self.assertIn('master.m_EncounterStarted', CODE)
        self.assertIn('WorldInputPoi(cow.GetMiniHexInfo())', CODE)
        self.assertIn('encounter.m_EncounterType.ToString()', CODE)

    def test_reward_teardown_uses_existing_fsm_variables(self):
        self.assertIn('WorldInputEncounterFsm(master.m_LootCollectionFSM)', CODE)
        self.assertIn('WorldInputEncounterFsm(encounter.m_ShowXPGoldFSM)', CODE)
        self.assertIn('WorldInputEncounterFsm(encounter.m_FSM)', CODE)
        self.assertIn('component.FsmVariables.IntVariables', CODE)
        self.assertIn('component.FsmVariables.BoolVariables', CODE)
        self.assertNotIn('GetFsmInt(', CODE)
        self.assertNotIn('GetFsmBool(', CODE)
        self.assertIn('master.IsInvoking("ShowNextXPGold")', SOURCE)
        self.assertIn('master.IsInvoking("ReturnToOverworld")', SOURCE)

    def test_encounter_pois_guard_mutating_missing_category_lookup(self):
        self.assertIn('world.m_MiniHexList.ContainsKey(type)', CODE)
        self.assertIn('present?world.GetPOIList(type):null', CODE)
        self.assertIn('Math.Min(pois.Count,1024)', CODE)
        self.assertIn('pois.Count>1024', CODE)
        self.assertIn('WorldInputHex(poi.m_HexLand)', CODE)
        for member in ('m_EnemyType', 'm_ID', 'm_Deactivated', 'm_Locked', 'm_Hidden'):
            self.assertIn('.' + member, CODE)
        self.assertNotIn('GetEncounterData(', CODE)
        self.assertNotIn('GetNonQuestEncounter(', CODE)

    def test_success_envelope(self):
        self.assertIn('JObject result=new JObject{{"ok",true}', SOURCE)

    def test_exact_fixed_scope_and_hex_property(self):
        self.assertIn('CatalogKeys(command,"id","session","op");', SOURCE)
        self.assertIn('WorldInputHex(cow.m_HexLand)', CODE)
        self.assertNotIn('GetField("m_HexLand"', SOURCE)
        self.assertIn('GetField("m_StartHex",Members).GetValue(movement)', SOURCE)
        self.assertIn('input.GetCurrentController()', CODE)
        self.assertIn('input.GetInputPlayer()', CODE)
        self.assertIn('cow.IsOwner', CODE)
        self.assertIn('master.m_DoKeepDiorama', CODE)
        self.assertIn('catch(Exception e)', CODE)

    def test_title_instances_exclude_prefabs_and_include_inactive_children(self):
        self.assertIn('screen.gameObject.scene.IsValid()', CODE)
        self.assertIn('FTKInput.Instance.m_CurrentInputFocus==screen', CODE)
        self.assertIn('parent.GetComponentsInChildren<Transform>(true)', CODE)
        self.assertIn('child.name!="ModsButton"', SOURCE)
        self.assertIn('child.GetComponentsInChildren<Text>(true)', CODE)
        self.assertIn('child.GetComponentsInChildren<Button>(true)', CODE)
        self.assertIn('button.interactable', CODE)
        self.assertIn('rect.rect.width', CODE)

    def test_exposed_only_through_observation_route(self):
        self.assertIn('world-input-state', (ROOT / 'command.py').read_text())
        self.assertIn('WorldInputObservation(command)', (ROOT / 'Plugin.cs').read_text())
        self.assertNotIn('Harmony', CODE)


if __name__ == '__main__':
    unittest.main()
