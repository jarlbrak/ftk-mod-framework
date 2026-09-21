"""Guard observer authority-boundary checks; not live gameplay acceptance."""
from pathlib import Path
import re
import unittest

SOURCE = Path(__file__).with_name('GuardianObservation.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class GuardianObservationBoundary(unittest.TestCase):
    def test_no_game_mutation(self):
        for name in ('SetValue', 'Instantiate', 'Destroy', 'SetActive', 'SendEvent',
                     'ApplyGuard', 'TryGuard', 'BeginTurn', 'ExpireGuard', 'ResetGuardian',
                     'TryResolveAttackDamage', 'ResolveFocusedHitHealing', 'StartCoroutine',
                     'UpdateProficiency', 'ClearEndOnTurnProficiency', 'AddToDummy', 'End',
                     'SetAttackDecision', 'GetRNGAttackDecision', 'GetScheduleAttackDecision',
                     'GetNextAttackScheduleItem', 'ToggleProfCoolDown'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')

    def test_armor_snapshot_reads_both_native_rosters(self):
        self.assertIn('encounter.m_PlayerDummies.Values', SOURCE)
        self.assertIn('encounter.m_EnemyDummies.Values', SOURCE)
        for member in ('m_Count', 'm_Time', 'm_FullTime', 'm_ProficiencyID',
                       'm_CustomValue', 'm_IsEndOnTurn', 'ArmorMod', 'm_TauntArmor'):
            self.assertIn('.' + member, SOURCE)
        self.assertIn('TryGetValue(ProficiencyBase.Category.Armor,out record)', SOURCE)
        self.assertIn('{"armorEffectPresent",present}', SOURCE)
        self.assertIn('{"fid",GuardianObservedFid(dummy.FID)}', SOURCE)
        self.assertIn('{"armorDummies",armorDummies}', SOURCE)
        for member in ('m_AttackScheduleList', 'm_Weapon', 'GetProficiencyIDs',
                       'm_AttackScheduleIndex', 'm_Shuffle', 'm_ProfCoolDown'):
            self.assertIn(member, SOURCE)
        self.assertIn('Observed selection inputs only; no attack selected or RNG consumed.', SOURCE)

    def test_stored_damage_and_ward_state_are_read_only_snapshots(self):
        for member in ('m_AttackInfo', 'm_DamageInfo', 'm_PostAttackHealthMod',
                       'm_ProfSuccess', 'm_ProfAffect', 'm_ProfImmune', 'm_AttackerHealthMod'):
            self.assertIn('.' + member, SOURCE)
        self.assertIn('fid.m_TurnIndex+":"+fid.m_PhotonID', SOURCE)
        self.assertNotIn('FID.ToString()', SOURCE)
        self.assertIn('Last stored native fields; may belong to an earlier attack.', SOURCE)
        self.assertIn('entry.Value==null || entry.Value.m_Proficiency==null', CODE)
        self.assertIn('stats.MaxHealth', CODE)
        self.assertIn('enemy.m_EnemyCombat.GetHealthTotal()', CODE)

    def test_reflection_invokes_only_reviewed_queries(self):
        names = set(re.findall(r'GuardianQuery\([^;\n]*?,"(\w+)"', SOURCE))
        self.assertEqual(names, {'Identity', 'IsGuardian', 'CanAct', 'IsActive',
                                'DesignatedAlly', 'RescueAvailable', 'StatusDescription',
                                'ActiveGuardians'})
        self.assertEqual(CODE.count('.Invoke('), 1)
        self.assertIn('CatalogKeys(command,"id","session","op");', SOURCE)
        self.assertIn('"m_IsInCombat"', SOURCE)


if __name__ == '__main__':
    unittest.main()
