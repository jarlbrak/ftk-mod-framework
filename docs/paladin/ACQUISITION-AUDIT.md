# Paladin acquisition audit

The installed game confirms that Paladin equipment uses normal shared loot and
shop pools without requiring a Paladin in the party. Two launch defects were
found: the original acquisition bands used character levels where the game
expects item levels, and an early tailored weapon reward could find no eligible
Vitality weapon.

## Authority and scope

Inspected the installed `Assembly-CSharp.dll`, SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`,
with ILSpy. Native values were read from the matching `sharedassets1.assets`,
using the decompiled field order to decode item, rarity and progression rows.
This is source and serialized-data evidence, not a live acquisition test.
No game source or assets are included in this report.

## Native progression

`FTK_progressionTier.m_ItemLevel` is separate from expected character level.

| Progression | Expected party level | Item level |
| --- | --- | --- |
| Tier1 | 0 | 0 |
| Tier2 | 2 | 1 |
| Tier3 | 4 | 2 |
| Tier4 | 6 | 3 |
| Tier5 | 8 | 4 |
| Tier6 | 9 | 4 |

All 675 native item and weapon rows were inspected. Native dropable equipment
uses maximum item levels no higher than 6. The original Paladin endgame minimum
of 7 excluded Mercy, Censure and Verdict from ordinary campaign shops.

The corrected package bands are Novice 0, Oathkeeper 1-2, Highward 3, and
Mercy/Censure/Verdict 4-6. These retain all horizontal endgame choices.
Useful native comparisons are Great Hammer at item level 3 (32 damage,
188 gold), Ice Hammer at 4-6 (34 damage, 425 gold), and Royal Hammer at 5-6
(38 damage, 450 gold). Similar numbers do not by themselves prove combat balance.

## Ordinary loot and shops

- `GameCache.Cache.Items.Initialize` builds category pools from registered item
  and weapon database arrays. DLC ownership can exclude rows; Paladin explicitly
  uses `None`. No party class is inspected.
- `FTK_enemyCombat.ItemDrops.GetLootItems` determines drop count from the enemy's
  existing count, chance and difficulty multiplier. Adding equipment does not
  increase these values. Its base-drop path uses the native category shuffle.
- `FTKHub.GetWeightedDropItem` filters category candidates by `m_Dropable`, lore
  unlock, inclusive item-level bounds, adventure exclusions, end-dungeon filter
  and artifact handling. No party class or primary attribute is inspected.
- `GameLogic.BuildItemStringWeightArray` supplies each candidate's native rarity
  weight. `FTKUtil.RandomStringWeighted` sums those weights and draws once from
  the total. Weights are already normalized: common 1, uncommon 0.4, rare 0.2,
  artifact 0.1. A second custom normalization layer is unnecessary.
- `FTK_itemsDB.IsItemUnlocked` returns true when no corresponding item-category
  lore unlock exists. Paladin adds no such unlock requirement.
- `TownManager._fillMarketLists` uses lore eligibility, adventure exclusions and
  market flags. `GetFilteredMarketAllItems` applies inclusive item-level bounds.
  `CreateNewShopInventoryForPOI` uses campaign item level, then shuffles eligible
  category candidates and selects the configured stock quantity. Shop selection
  does not use rarity weights. No party class is inspected.

Paladin's 36 equipment rows declare dropable, all three market flags, stock 1,
no DLC requirement and an empty collection-lore identifier. The latter avoids
the separately documented invalid-lore collection failure.

At item level 4, the corrected bands add six rare weapons and three rare items
per other equipment category. Before lore/adventure/artifact exclusions, their
weighted shares among base-game dropable rows would be approximately:

| Category | Native candidate count | Native weight total | Paladin share |
| --- | --- | --- | --- |
| Weapon | 46 | 23.8 | 4.8% |
| Shield | 6 | 3.3 | 15.4% |
| Armor | 8 | 3.4 | 15.0% |
| Helmet | 13 | 4.3 | 12.2% |
| Boots | 6 | 3.0 | 16.7% |

These are conditional pool calculations, not measured campaign frequencies.
Category selection happens separately. Lore ownership, adventure restrictions,
DLC, and active-artifact state change the actual pool. Three horizontal variants
increase selection diversity while total drop count remains unchanged.

## Tailored reward safety

`GameLogic.GetTailoredWeaponItem(CharacterOverworld)` requests the current
progression item level plus one. It filters weapon rows by the character class's
primary attribute, non-common rarity, inclusive level bounds and lore unlock,
then indexes a random candidate without checking for an empty list.

All four native Vitality weapon rows are common. With common Oathkeeper hammers
at item levels 1-2, early Paladin tailored rewards have no native candidate.
The narrow remedy is a fallback for registered custom classes when the original
non-common pool is empty, using an eligible common weapon and the native random
selection. Preserve original behavior whenever its pool is populated. Ordinary
rarity should remain an acquisition decision, not a workaround for this crash.

## Acceptance still required

Verify package bands and every equipment family's acquisition flags. Check the
tailored reward fallback with and without an existing native candidate, empty
fallback behavior, and unchanged vanilla-class behavior. Inspect live category
membership and level exclusions in a party containing no Paladin, then observe
native shop purchase or random loot collection without appending rewards or
forcing stock. Verify representative later-stage acquisition and save/reload
on the final 1.0.0 artifact. Controlled fixture selection may prove eligibility
or collection, but must not be described as observed random-drop distribution.
