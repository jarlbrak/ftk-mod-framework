from pathlib import Path
import unittest

SOURCE = (Path(__file__).parent / 'NativePartyClass.cs').read_text()
GATE = (Path(__file__).parent / 'NativePartyStart.cs').read_text()

class PartyClassBoundary(unittest.TestCase):
    def test_only_one_native_direction_callback_no_avatar_or_field_mutation(self):
        self.assertEqual(SOURCE.count('owner.OnClassClick();'), 1)
        self.assertEqual(SOURCE.count('owner.OnClassClickLeft();'), 1)
        for forbidden in ('SetClass(', 'SyncSettings(', 'SyncSettingsRPC(', 'CreateAvatar(',
                          'owner.m_ClassID = ', 'owner.m_IsReady = ', 'owner.m_Avatar = ', '.Invoke(owner'):
            self.assertNotIn(forbidden, SOURCE)

    def test_custom_registration_and_exact_route_pins(self):
        for required in ('"TryGetSyntheticId"', '(int)registration[1] != target',
                         'db.GetEntryByInt(target).m_ID != key', 'db.GetCount() > 512',
                         'db.GetIntFromID(row.m_ID) != i', 'db.IsReveal((FTK_playerGameStart.ID)i, true)',
                         'pins["direction"]', 'pins["targetClassId"]', 'pins["route"]',
                         'object.ReferenceEquals(rows[i], partyClassRows[i])', 'GetPersistentTarget(listener) == owner'):
            self.assertIn(required, SOURCE)

    def test_visible_locked_intermediates_keep_native_navigation(self):
        self.assertIn('InspectNativePartyStart(out menu, true)', SOURCE)
        self.assertIn('(!forClassSelection && !menu.GetAllPlayersReady())', GATE)
        self.assertIn('(!forClassSelection && !candidate.m_IsReady)', GATE)
        self.assertIn('candidate.m_IsReady != FTK_playerGameStartDB.GetDB().IsUnlock(', GATE)

    def test_uncertainty_and_fresh_preview_guards(self):
        for required in ('RequireSelectionOpen(partyStartClaim.Consumed, partyClassUncertain,', 'partyClassClaim.Submit(',
                         'owner.m_ClassID != next', 'owner.m_Avatar.GetInstanceID() == beforeAvatar',
                         'owner.m_SkinType != rows[next].m_DefaultSkinType',
                         'NativeCreateCandidateReady(NativeCreateCandidate(owner))',
                         'UnityEngine.Time.frameCount, partyClassSubmittedFrame'):
            self.assertIn(required, SOURCE)
        self.assertLess(SOURCE.index('partyClassUncertain = true'), SOURCE.index('owner.OnClassClick();'))
        self.assertLess(SOURCE.index('owner.m_ClassID != next'), SOURCE.index('partyClassUncertain = false'))

    def test_arrow_selection_uses_full_eligibility_and_reports_all_candidates(self):
        for required in ('NativePartyClassPolicy.UniqueEligibleArrow(identities, eligible)',
                         'candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy',
                         'candidate.IsInteractable()', 'candidate.transform.IsChildOf(owner.transform)',
                         'graphic.canvasRenderer.GetAlpha()', 'graphic.canvasRenderer.cull',
                         'NativePartyClassPolicy.GraphicVisible(graphicActive, canvasActive, culled, rendererAlpha)', 'NativePartyClassPolicy.EligibleArrow(explicitDirectional, sole, state,',
                         '"targetGraphicInstanceId"', '"canvasInstanceId"', '"graphicColorAlpha"', '"rendererAlpha"',
                         'alphaFactorsPositive &= NativePartyClassPolicy.GraphicVisible(true, true, false, group.alpha)',
                         'pins["arrowCandidates"] = arrowCandidates', '{"reasons", reasons}',
                         'partyClassToken = null; partyClassPins = null; partyClassClaim = null;',
                         '{"arrowCandidates", arrowCandidates}', '{"callbacks", callbacks}'):
            self.assertIn(required, SOURCE)

    def test_explicit_native_directional_control_required_in_addition_to_callback(self):
        for required in ('ExplicitDirectionalControl(controlName, direction)',
                         'eligible[index] = NativePartyClassPolicy.EligibleArrow(explicitDirectional, sole, state,',
                         'reasons.Add("not_explicit_directional_control")',
                         'pins["arrowName"] = arrow.gameObject.name',
                         '{"explicitDirectionalControl", explicitDirectional}'):
            self.assertIn(required, SOURCE)

if __name__ == '__main__':
    unittest.main()
