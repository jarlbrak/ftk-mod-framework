from pathlib import Path
import unittest

SOURCE = (Path(__file__).parent / 'NativePartyStart.cs').read_text()

class PartyStartBoundary(unittest.TestCase):
    def test_single_exact_callback(self):
        # Fresh and resumed parties have separate entry points but share the
        # process-lifetime claim. Each must submit exactly one guarded callback.
        fresh, resumed = SOURCE.split('    JObject NativeResumePartyStart(JObject command)')
        fresh = fresh.split('    JObject NativePartyStart(JObject command)')[1]
        for route in (fresh, resumed):
            self.assertEqual(route.count('menu.EnterFahrul();'), 1)
            self.assertEqual(route.count('partyStartClaim.Submit('), 1)
            self.assertIn('if (partyStartClaim.Consumed)', route)
            self.assertIn('partyStartMenu != menu', route)
            self.assertIn('partyStartDefinition, GameLogic.Instance.GetGameDef()', route)
            self.assertIn('delegate { menu.EnterFahrul(); }', route)
        self.assertIn('InspectNativePartyStart(out menu);', fresh)
        self.assertIn('InspectNativePartyStart(out menu, false, true);', resumed)
        self.assertIn('menu.m_IsResuming != requireResume', SOURCE)
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
