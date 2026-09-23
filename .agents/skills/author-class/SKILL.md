---
name: author-class
description: Design and implement an FTK mod class with a coherent role, stats, starting loadout, actions, and progression. Use for class and ability authoring; route equipment to author-item or author-gear-set.
---

# Author an FTK class

Read the root and nearest `AGENTS.md`, [content authoring](../../../docs/WRITING-CONTENT.md), and the shipped [Paladin package](../../../marketplace/packages/paladin/content.json). Paladin shows a complete class; its mechanics and balance are not defaults for another class.

1. Define the class promise, primary decision in combat, party contribution, weaknesses, starting loadout, and progression choices. Compare the closest native class and state what the new class changes. For exact stats, skill flags, action behavior, or template semantics, use `decompile-lookup` against the installed assembly.
2. Register a stable class ID under the mod GUID through a JSON `class` entry or `Content.AddClass`. Clone the nearest native template and change only required fields. Keep starting items and weapons as stable local references. Use public `Content.*` APIs for proficiencies, passives, and supported class capabilities; do not mutate vanilla rows or introduce hard-coded enum integers.
3. Map each unique action to its trigger, target, costs, timing, success rule, effect, and reset boundary. Check whether inherited weapon actions or native class abilities remain. Where the public API lacks a required mechanic, identify the missing primitive rather than hiding engine logic in content. Keep shared outcomes deterministic and registration idempotent.
4. Use `author-item` or `author-gear-set` for loadout and item progression, `author-player-skin` for declared avatar layouts, and `ftk-custom-models` for original geometry. Do not claim race or sex coverage from one skinset preview. If preparing marketplace content, check the current [submission contract](../../../docs/MARKETPLACE.md) before relying on a behavior DLL.
5. Verify class selection, starting items, ordinary acquisition, every advertised action and passive, combat and encounter resets, save/resume, and the supported avatar layouts. Run `verify-change` and available `ingame-smoke` checks. Separate offline, simulated, and live results, including co-op limitations.

Deliver the role and progression contract, content and asset inventory, template rationale, observed behavior, and remaining gates.
