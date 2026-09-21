"""One append boundary; native collection requires a separate live receipt."""
from pathlib import Path
import re
import unittest
ROOT=Path(__file__).parent
SOURCE=(ROOT/'CustomLootFixture.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
class CustomLootFixtureBoundary(unittest.TestCase):
    def test_only_one_list_append_no_reward_or_database_write(self):
        self.assertEqual(SOURCE.count('_arrayList.Add("paladin_helmet_novice")'),1)
        self.assertNotRegex(CODE,r'\b(m_\w+|_perPlayerGold|_perPlayerXP)\s*=(?!=)')
        for name in ('Clear','Remove','SetValue','AddItem','CollectLoot','SendEvent','SaveSettings'):
            self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertIn('string.IsNullOrEmpty(row.m_CollectLoreItemUnlock)',CODE)
    def test_exact_list_and_encounter_pins(self):
        self.assertIn('object.ReferenceEquals(_arrayList,observer.customLootList)',CODE)
        self.assertIn('object.ReferenceEquals(currentList.arrayList,_arrayList)',CODE)
        self.assertIn('client.m_ActiveDiorama!=observer.customLootDiorama',CODE)
        self.assertIn('object.ReferenceEquals(row,observer.customLootItem)',CODE)
        self.assertIn('MiniHexDungeon.EncounterType.Enemy',CODE)
    def test_claim_and_unpatch_before_append_with_expiry(self):
        callback=CODE[CODE.index('static void CustomLootPostfix'):CODE.index('JObject CustomLootFixture')]
        self.assertLess(callback.index('observer.CustomLootRemoveHook()'),callback.index('_arrayList.Add('))
        self.assertIn('if(customLootClaimed)',CODE)
        self.assertIn('Time.realtimeSinceStartup+600f',CODE)
        self.assertIn('Unpatch(customLootMethod,CustomLootPostfixMethod())',CODE)
        self.assertIn('CustomLootTick();',(ROOT/'Plugin.cs').read_text())
        self.assertRegex((ROOT/'Plugin.cs').read_text(),r'void OnDestroy\(\)\{[^}]*CustomLootRemoveHook\(\);')
if __name__=='__main__':unittest.main()
