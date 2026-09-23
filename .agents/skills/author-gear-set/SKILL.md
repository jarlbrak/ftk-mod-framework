---
name: author-gear-set
description: Design and implement a cohesive FTK equipment family or level progression across multiple slots, including item balance, acquisition, and original visual identity. Use author-item for a single piece.
---

# Author an FTK gear set

Read the root and nearest `AGENTS.md`, [content authoring](../../../docs/WRITING-CONTENT.md), and the shipped [Paladin equipment ledger](../../../docs/paladin/EQUIPMENT.md). Treat Paladin as an example of a complete progression, not a set of stats or silhouettes to copy.

1. Write a compact matrix of every planned slot and tier: item ID, native template, stat/action budget, rarity, level band, gold value, acquisition, and visual motif. Compare each tier with existing game equipment. State the progression curve and the tradeoff for horizontal endgame options before editing definitions. Do not create a class-only drop rule unless the design explicitly calls for one.
2. Implement each piece through `author-item`. Keep IDs and item levels stable across updates. Check all inherited template actions and typed modifiers. Verify shield/weapon exclusivity, relevant equipment slots, start items, ordinary drop and shop routes, and whether gear remains obtainable without its themed class in the party.
3. Build a distinct, readable silhouette at the game's camera distance for each tier while keeping materials, palette, and motifs coherent within the family. Use `ftk-custom-models` for original meshes and exact renderer bindings; use `preview-ftk-assets` for comparison outside the game. A render is art review, not runtime acceptance.
4. Review the full set side by side in the inventory and on supported avatars, including male/female and each claimed race or skinset. Check equipped, unequipped, combat clone, motion, clipping, and fallback separately. Inspect lower and upper tiers at the same scale and lighting so visible progression can be judged.
5. Run content/package validators, `verify-change`, and available `ingame-smoke` checks. Record actual loot/shop observations and unmet probability or platform gates without upgrading offline checks into gameplay claims.

Deliver the matrix, item and asset inventory, progression comparison, validation evidence, and explicit uncovered slots or skinsets.
