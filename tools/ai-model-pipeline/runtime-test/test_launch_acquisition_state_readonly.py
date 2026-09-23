"""Read authority boundaries for launch observation, not acquisition acceptance."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'LaunchAcquisitionState.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class LaunchAcquisitionBoundary(unittest.TestCase):
    def test_no_mutation_rng_generation_or_lazy_pool_initialization(self):
        for name in ('SetValue', 'Invoke', 'Instantiate', 'Destroy', 'SetActive',
                     'SendEvent', 'Initialize', 'Refresh', 'GenerateShopItemStock',
                     'GetWeightedDropItem', 'GetTailoredWeaponItem', 'Shuffle',
                     'Range', 'GetNewDungeonSanctum', 'CreateDungeonSanctumList',
                     'ClaimSanctum', 'ClaimSanctumRPC', 'AddCharacterMod',
                     'AddItem', 'SetHealth', 'StartCoroutine'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')
        self.assertNotRegex(CODE, r'\b(?:cache|category|rows|dungeonSource|remainingWorld|remainingDungeon)\s*\[[^]]+\]\s*=(?!=)')
        self.assertNotRegex(CODE, r'\b(?:cache|category|rows|dungeonSource|remainingWorld|remainingDungeon)\.(?:Add|Remove|Clear|Sort)\(')

    def test_request_and_existing_session_guards(self):
        self.assertIn('CatalogKeys(command, "id", "session", "op");', SOURCE)
        self.assertIn('CatalogNoLinks(root);', SOURCE)
        self.assertIn('RequireSinglePlayer();', SOURCE)
        plugin = (ROOT / 'Plugin.cs').read_text()
        dispatch = 'else if(op == "launch-acquisition-state") Finish(id,LaunchAcquisitionState(command));'
        self.assertIn(dispatch, plugin)
        self.assertLess(plugin.index('RequireSinglePlayer();'), plugin.index(dispatch))
        self.assertLess(plugin.index('Str(command,"session") != sessionId'), plugin.index(dispatch))
        self.assertIn("OPS += ('launch-acquisition-state',)", (ROOT / 'command.py').read_text())

    def test_reports_native_state_without_acceptance_verdict(self):
        for anchor in ('hero.m_CharacterStats.m_CharacterClass',
                       'FTK_itemsDB.GetDB().m_Array', 'FTK_weaponStats2DB.GetDB().m_Array',
                       'category.Contains(row)', 'row.m_MinLevel', 'row.m_MaxLevel',
                       'row.m_ItemRarity', 'row.m_Dropable', 'row.m_TownMarket',
                       'row.m_NightMarket', 'row.m_DungeonMerchant',
                       'FTK_characterModifier.ID.Sanctum08)',
                       'FTK_characterModifier.ID.Sanctum08E)',
                       'row.m_ModVitality', 'row.m_ExtraHealth', 'row.m_HealthRegen'):
            self.assertIn(anchor, CODE)
        for field in ('_itemsByCategory', '_sanctumsToGenerate',
                      'm_DungeonSanctumGrandLookUp', 'm_AvailableDungeonSanctums'):
            self.assertIn('"' + field + '"', SOURCE)
        for unavailable in ('remainingWorldPoolAvailable', 'dungeonSourcePoolAvailable',
                            'remainingDungeonPoolAvailable', 'categoryCacheAvailable'):
            self.assertIn('"' + unavailable + '"', SOURCE)
        self.assertNotIn('"passed"', SOURCE)
        self.assertNotIn('"accepted"', SOURCE)
        self.assertIn('rows.Length > 4096', CODE)
        self.assertIn('source.Count > 64', CODE)


if __name__ == '__main__':
    unittest.main()
