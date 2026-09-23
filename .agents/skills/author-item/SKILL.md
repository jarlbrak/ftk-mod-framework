---
name: author-item
description: Create or revise one FTK mod item or weapon, including its stats, actions, acquisition, and optional original presentation assets. Use for individual equipment, consumables, and artifacts; use author-gear-set for a connected progression.
---

# Author an FTK item

Read the root and nearest `AGENTS.md`, [content authoring](../../../docs/WRITING-CONTENT.md), and the [Paladin content package](../../../marketplace/packages/paladin/content.json). Paladin is the shipped example, not a required balance or art template.

1. Define the item's slot, intended user, level band, rarity, ordinary acquisition routes, and gameplay tradeoff. Identify the closest native item or weapon template. For uncertain template actions, slots, field meanings, or loot behavior, verify the installed game assembly with `decompile-lookup` before choosing values.
2. Add a stable local ID under the mod's permanent GUID and clone through a JSON `item` or `weapon` entry, or `Content.AddItem` / `Content.AddWeapon`. Change only the needed fields and typed modifiers. Do not alter vanilla rows, share a mutable modifier row, or assign custom enum integers. A weapon inherits its template actions unless explicitly replaced; account for those actions in balance.
3. If the item needs unique behavior, first check the supported proficiency and passive APIs. Keep framework mechanics in `Core/` and mod content on public `Content.*` surfaces. Preserve deterministic multiplayer and save identities.
4. For original visuals, declare the icon and the applicable equipped, display, or apparel models as package-relative assets. Follow `ftk-custom-models` for geometry and exact renderer binding. Inspect every claimed body/skinset and the inventory view; a GLB validation pass is not proof of fit or animation.
5. Verify registration, ordinary acquisition, equip/unequip, action or modifier behavior, save/resume, and package inventory. Use `verify-change` for game-free checks and `ingame-smoke` for an available local game. Record offline and live evidence separately, including unavailable routes.

Deliver the content definition, original asset inventory when applicable, template rationale, intended acquisition, observed results, and remaining limits.
