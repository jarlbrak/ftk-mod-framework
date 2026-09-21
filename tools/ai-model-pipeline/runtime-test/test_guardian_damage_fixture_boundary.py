"""Authority boundary only; native combat requires live validation."""
from pathlib import Path
import re
import unittest
SOURCE=Path(__file__).with_name('GuardianDamageFixture.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"','',SOURCE)
class GuardianFixtureBoundary(unittest.TestCase):
    def test_no_direct_damage_state_or_save_mutations(self):
        for name in ('TryGuard','ResolveDamage','TryResolveDirectDamage','TryResolveAttackDamage','ResetEncounter',
                     'GainSpecificHealth','SetSpecificHealthRPC','DieRPC','RespondToHit','PlayAttackSequence',
                     'SaveGame','SetValue'):
            self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertNotRegex(CODE,r'\b(?:File|PlayerPrefs)\s*\.')
        self.assertNotRegex(CODE,r'\.m_HealthCurrent\s*=(?!=)')
    def test_effect_duration_is_not_treated_as_attack_count(self):
        self.assertNotIn(".m_RepeatCount", CODE)
        self.assertIn("_atk.m_AoeTargets.Count", CODE)
        self.assertIn("_ddi1!=null || _ddi2!=null", CODE)

    def test_single_player_and_scoped_hooks(self):
        self.assertIn('RequireSinglePlayer();',CODE)
        self.assertIn('h.Unpatch(method,HarmonyPatchType.All,GuardianFixtureOwner)',CODE)
        self.assertNotIn('UnpatchAll',CODE)
    def test_consumed_before_native_payload_assignment(self):
        self.assertLess(CODE.index('r.Policy.Commit();'),CODE.index('_ddi0=known;'))
        self.assertNotRegex(CODE,r'\bbool\s+GuardianFixture(?:Engage|Commit)\s*\(')
        self.assertIn('r.Events.Count>=24',CODE)
        self.assertIn('Time.realtimeSinceStartup>r.Deadline',CODE)
    def test_exact_guardian_set_and_charge_pins(self):
        self.assertIn('receipt.Policy.MatchesPins(protectors,charges)', CODE)
        self.assertIn('GuardianFixtureRescues(r)', CODE)
        self.assertIn('GuardianFixtureIdentity(guardian)!=r.Policy.GuardianIds[i]', CODE)
        self.assertIn('beforeCommit && guardian.GetCurrentHealth()!=r.GuardianHealth[i]', CODE)
        self.assertIn('r.Policy.ExpectAlive', CODE)
if __name__=='__main__':unittest.main()
