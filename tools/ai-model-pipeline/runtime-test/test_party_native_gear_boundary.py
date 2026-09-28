"""Source safety boundaries; native slot behavior still requires a live trial."""
from pathlib import Path
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / "PartyNativeGearFixture.cs").read_text()
ACQUISITION = (ROOT / "LaunchAcquisitionState.cs").read_text()


class PartyNativeGearBoundaryTests(unittest.TestCase):
    def test_no_mutation_before_full_preflight_or_inspect_return(self):
        mutation = SOURCE.index("backpack.Add(")
        for guard in ("RequireSinglePlayer();", "RequireOutsideCombat();", "CatalogNoLinks(root);",
                      'hero.GetDBEntry().m_ID != classKey', 'Enum.IsDefined(typeof(FTK_itembase.ID), key)',
                      'row.m_ID != key', 'types.Add(row.m_ObjectType)', 'if (action == "inspect") return'):
            self.assertLess(SOURCE.index(guard), mutation)

    def test_only_native_inventory_mutation(self):
        self.assertIn("hero.ForceEquip(entry.itemId, false)", SOURCE)
        self.assertIn("OwnedAcrossEquipment(hero, entry.itemId) != 1", SOURCE)
        for forbidden in ("PlayerPrefs", ".m_HealthCurrent =", ".m_PlayerLevel =", "SaveGame(", "SetClass(", "Random."):
            self.assertNotIn(forbidden, SOURCE)

    def test_acquisition_prefix_is_bounded_and_read_only(self):
        self.assertIn('prefix != "paladin_" && prefix != "blacksmith_"', ACQUISITION)
        for forbidden in ("GenerateShopItems", "CreateNewShopInventory", "GetRandom", "ForceEquip", "backpack.Add"):
            self.assertNotIn(forbidden, ACQUISITION)


if __name__ == "__main__":
    unittest.main()
