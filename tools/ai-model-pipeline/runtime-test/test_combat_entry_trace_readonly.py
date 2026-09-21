"""Diagnostic authority-boundary checks, not native combat or crash reproduction."""
from pathlib import Path
import re
import unittest

SOURCE = Path(__file__).with_name('CombatEntryTrace.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class CombatEntryTraceBoundary(unittest.TestCase):
    def test_no_native_mutation_or_animator_state_access(self):
        for name in ('Instantiate', 'Destroy', 'DestroyImmediate', 'SetActive', 'Play',
                     'Rebind', 'Update', 'EnsureRetained', 'SetResources', 'SetVisible',
                     'StartCoroutine', 'SendEvent', 'Invoke', 'SetValue'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\.(?:cullingMode|runtimeAnimatorController|isInitialized|avatar)\b')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')

    def test_finalizers_preserve_exception_and_prefixes_cannot_skip_original(self):
        self.assertNotRegex(CODE, r'\bbool\s+CombatEntry\w*Prefix\s*\(')
        self.assertEqual(CODE.count('return __exception;'), 2)
        self.assertNotRegex(CODE, r'\bref\s+(?:Exception|CharacterDummy|EncounterSession)\b')

    def test_scoped_skip_uses_exact_method_not_owner_wide_unpatch(self):
        self.assertIn(".Unpatch(original,target)", CODE)
        self.assertNotRegex(CODE, r"\bUnpatchAll\s*\(")
        self.assertIn("patch.PatchMethod==target", CODE)
        self.assertIn("matches!=1", CODE)

    def test_quiet_recorders_return_before_native_observation(self):
        self.assertEqual(CODE.count("observer==null || observer.combatEntryQuiet"), 2)

    def test_explicit_opt_in_before_patching_and_bounded_records(self):
        gate = SOURCE.index('FTK_MODEL_TEST_COMBAT_ENTRY_TRACE')
        self.assertLess(gate, SOURCE.index('harmony.Patch('))
        self.assertIn('combatEntryEvents>=256', CODE)
        self.assertIn('File.AppendAllText(', CODE)
        self.assertNotRegex(CODE, r'File\.(?:Delete|WriteAllText)\s*\(')


if __name__ == '__main__':
    unittest.main()
