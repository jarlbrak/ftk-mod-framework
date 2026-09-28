"""Authority checks for isolated package fit setup; not live-game evidence."""
from pathlib import Path
import re
import unittest
ROOT = Path(__file__).parent
SOURCE = (ROOT / 'PackageGearFixture.cs').read_text()
APPEARANCE = (ROOT / 'BlacksmithAppearanceFixture.cs').read_text()

class PackageFitBoundary(unittest.TestCase):
    def test_scope_and_owned_idle_party(self):
        for guard in ('RequirePackageFitIsolation();', 'RequireSinglePlayer(); RequireOutsideCombat();',
                      'FTK_MODEL_TEST_PACKAGE_ONLY', 'uiStartGame.GetSavePath()', 'PreviewCoreIdentity();',
                      '!hero.IsOwner', 'hero.m_WaitForRespawn', 'hero.m_CharacterStats.m_IsInCombat',
                      'member.m_IsMoving', '"m_MoveCoroutineRunning"', '"m_Queue"',
                      'blacksmithAppearance != null || previewRace != null'):
            self.assertIn(guard, SOURCE)

    def test_preflight_precedes_mutation_and_keeps_native_ownership(self):
        method = SOURCE[SOURCE.index('    JObject PackageGearFixture('):]
        mutation = method.index('foreach (BlacksmithGearEntry entry in entries)')
        for guard in ('!unique.Add', 'ResolvePackageFitGear', 'PreflightOwnedBlacksmithItem',
                      'Only one requested item per equipment slot', 'Shield requires',
                      'Displaced equipment must fit', 'BlacksmithInventoryTotals(hero)'):
            self.assertLess(method.index(guard), mutation)
        for guard in ('RequireBlacksmithInventoryPreserved', 'Native swap changed an unrelated inventory slot',
                      'ReferenceEquals(statsOwner, hero.m_CharacterStats)', 'PackageFitStableStats(statsBefore)',
                      'BlacksmithGrantPreservedEquals(expected, after)'):
            self.assertIn(guard, method)
        self.assertIn('if (equip && entry == shield) continue;', method)
        code = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
        self.assertFalse(re.findall(r'\b(m_\w+)\s*=(?!=)', code))
        for forbidden in ('SetValue(', 'Allocate(', 'Save(', 'RPC(', 'SetClass(', 'StartCoroutine('):
            self.assertNotIn(forbidden, code)

    def test_appearance_retains_restoration_and_old_class_guard(self):
        self.assertIn('packageFit ? ExactPackageFitHero(id) : ExactBlacksmithHero(id)', APPEARANCE)
        self.assertIn('blacksmithAppearance.packageFit != packageFit', APPEARANCE)
        self.assertIn('BlacksmithAppearanceHero(ownerId, false, pin.packageFit)', APPEARANCE)
        self.assertRegex(APPEARANCE, r'finally\s*\{[^}]*pin.hero.m_SkinType = pin.original;')
        for guard in ('pin.hero.m_Avatar != pin.avatar', 'pin.retired == null', 'Time.frameCount > pin.frame',
                      'row.m_Skinsets[desired] == FTK_skinset.ID.None', 'Original native skin is not restorable',
                      'pin.avatar.GetInstanceID() != avatarId'):
            self.assertIn(guard, APPEARANCE)

    def test_native_selection_keeps_unlocks_and_zero_class(self):
        source = (ROOT / 'NativePartyClass.cs').read_text()
        for guard in ('PreviewRaceIndex(command, "targetClassId")', 'ExactClassRow(key, target)',
                      '!db.IsUnlock', '!visible[target]', 'HasDLC', 'db.IsReveal',
                      'partyClassClaim.Submit', 'owner.OnClassClick()'):
            self.assertIn(guard, source)
        self.assertNotIn('SetClass(', source)
        self.assertIn('ExactClassRow(key, FTK_playerGameStartDB.GetDB().GetIntFromID(key))',
                      (ROOT / 'PlayerPreviewObservation.cs').read_text())

    def test_load_observation_is_passive_and_never_refreshes_pins(self):
        session = (ROOT / 'PackageFitSession.cs').read_text()
        for guard in ('++packageFitLoads != 1', 'Candidate is not an immediate child',
                      'Actual discovered candidate source/version/content differs',
                      'packageFitFiles[path] != hash', 'packageFitRegistryRows[guid], entry',
                      'Fit observer installed after candidate registration', 'RegisteredCount', 'TotalCount'):
            self.assertIn(guard, session)
        for callback in ('PackageFitLoadPrefix', 'PackageFitDiscoveryPostfix', 'PackageFitLoadPostfix'):
            body = re.search(r'    static void '+callback+r'\(.*?\n    }', session, re.S).group()
            self.assertIn('catch (Exception error)', body)
            self.assertIn('packageFitSourceError = error.ToString()', body)
            self.assertNotIn('return false', body)
        plugin = (ROOT / 'Plugin.cs').read_text()
        self.assertLess(plugin.index('InstallPackageFitObserver(harmony)'), plugin.index('enabled = true'))
        self.assertIn('!File.Exists(Path.Combine(root, "model-test-package-gear.json"))) return;', session)

    def test_uncertainty_covers_native_calls_and_final_receipts(self):
        for text in (SOURCE, APPEARANCE):
            self.assertIn('PackageFitBeginMutation(', text)
            self.assertIn('packageFitMutationVerified = true', text)
            self.assertNotIn('packageFitUncertain = false', text)
        session = (ROOT / 'PackageFitSession.cs').read_text()
        self.assertIn('packageFitMutationVerified = false; packageFitUncertain = true', session)
        self.assertIn('if (!packageFitUncertain) throw;', session)
        self.assertIn('"partialAfter", partial', session)
        self.assertIn('"before", packageFitMutationBefore', session)
        self.assertIn('"differences", partial == null ? null : PackageFitDifferences', session)
        self.assertIn('PackageFitCommandBoundary(op)', (ROOT / 'Plugin.cs').read_text())

    def test_candidate_package_allocation_is_read_only_and_exact(self):
        for guard in ('"com.ftkmf.paladin"', '"com.ftkmf.thief"', 'CatalogHash(configPath) != configHash',
                      'packageGearConfigHash != configHash', 'CatalogHash(path) != expectedHash',
                      'allocations.Contains(allocationKey)', '(int)allocations[allocationKey] != id',
                      '(string)owners[id] != allocationKey', '!ReferenceEquals(row, exact)'):
            self.assertIn(guard, SOURCE)

if __name__ == '__main__':
    unittest.main()
