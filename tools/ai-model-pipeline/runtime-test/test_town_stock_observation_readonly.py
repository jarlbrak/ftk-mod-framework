"""Town observer authority boundaries, not native acquisition acceptance."""
from pathlib import Path
import re
import unittest

SOURCE = Path(__file__).with_name('TownStockObservation.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class TownStockObservationBoundary(unittest.TestCase):
    def test_no_native_mutation_or_reflection_invocation(self):
        for name in ('SetValue', 'Invoke', 'Instantiate', 'Destroy', 'SetActive',
                     'SendEvent', 'GenerateShopItemStock', 'Refresh', 'Purchase',
                     'Buy', 'SnapTo', 'AddGold', 'StartCoroutine', 'Generate'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')
        self.assertNotRegex(CODE, r'\bstock\s*\[[^]]+\]\s*=(?!=)')

    def test_only_existing_town_stock_and_strict_request(self):
        self.assertIn('CatalogKeys(command,"id","session","op");', SOURCE)
        self.assertIn('GetPOIList(MiniHexInfo.MiniHexType.Town)', CODE)
        self.assertIn('town.m_ShopItemStockCurrent.m_CountDictionary', CODE)
        self.assertIn('row.m_ID.StartsWith("paladin_",StringComparison.Ordinal)', SOURCE)
        self.assertIn('hero.m_CharacterStats.m_Gold', CODE)


if __name__ == '__main__':
    unittest.main()
