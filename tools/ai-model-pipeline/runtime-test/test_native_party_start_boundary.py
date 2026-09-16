from pathlib import Path
import unittest

SOURCE = (Path(__file__).parent / 'NativePartyStart.cs').read_text()

class PartyStartBoundary(unittest.TestCase):
    def test_single_exact_callback(self):
        self.assertEqual(SOURCE.count('menu.EnterFahrul();'), 1)
        for forbidden in ('SendEvent(', 'RPC(', 'RPCAllSelf(', '.onClick.Invoke(', 'SetReady(', 'SetClass(', 'SetActive('):
            self.assertNotIn(forbidden, SOURCE)

    def test_actual_native_gate(self):
        for gate in ('NativeCreatePhotonOfflineMode()', '!menu.IsMasterClient', 'menu.m_IsResuming',
                     'menu.m_GameStarted', 'NativeCreateMapReady()', 'NativeCreateRootActive(menu)',
                     'uiSystemDialog.Instance.gameObject.activeInHierarchy', 'button.IsInteractable()',
                     'GetPersistentTarget(0) != menu', 'GetPersistentMethodName(0) != "EnterFahrul"',
                     '"m_CallState"', 'menu.m_CreateUIs.Count < 1', '!menu.GetAllPlayersReady()',
                     '!NativeCreateCandidateReady(row)', '!create.m_Players.Contains(candidate)',
                     '!owners.Add(', '!avatars.Add(', '!turns.Add(', 'if (!ownedFocus)'):
            self.assertIn(gate, SOURCE)

    def test_entered_unready_foreign_and_invalid_slots_rejected(self):
        for gate in ('menu.m_FahrulEntered', 'NativeCreateField(menu, "m_MapReady"), true',
                     'NativePartyStartPolicy.RequireNativePreview(localPhotonId, candidate.m_PhotonID,',
                     'candidate.m_TurnIndex, menu.m_ActualMaxCharCount)',
                     'callState != 1 && callState != 2', '(int)localIdValue <= 0'):
            self.assertIn(gate, SOURCE)

    def test_pins_and_observation_only(self):
        for field in ('"session"', '"root"', '"gameDefinitionIdentityHash"', '"gameDefinitionName"',
                      '"photonId"', '"previews"', '"completionClaimed", false', '"callbackAttempted"'):
            self.assertIn(field, SOURCE)
        self.assertIn('static readonly NativePartyStartPolicy', SOURCE)
        self.assertIn('partyStartMenu != menu', SOURCE)

if __name__ == '__main__':
    unittest.main()
