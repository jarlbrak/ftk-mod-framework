---
name: author-player-skin
description: Add or revise an FTK custom class skinset appearance on native player avatars, including body, apparel, hair, and backpack renderer assignments. Use ftk-custom-models for mesh creation and binding details.
---

# Author an FTK player skin

Read the root and nearest `AGENTS.md`, [custom models](../../../docs/CUSTOM-MODELS.md), [player renderer contract](../../../docs/MODEL-PLAYER-API.md), and [Paladin content](../../../marketplace/packages/paladin/content.json). Preserve the game's own face, body, and customization unless the requested skin explicitly changes them.

1. Define the exact registered custom class and the skinsets or races this appearance claims to support. Inventory the native assembled renderer paths, native mesh names, materials, and conditional equipment variants for each. Paths observed on one sex or skinset do not establish another.
2. Create original mesh and texture assets through `ftk-custom-models`. Bind body, apparel, and backpack assignments to the exact native skeleton and renderer layout. Use required and conditional assignments according to the player renderer contract; a missing conditional renderer may skip, while a present incompatible renderer rejects the transaction. Do not mutate vanilla skinset rows, prefab assets, or shared materials.
3. Connect package-relative `playerModels` and apparel declarations, or public `Content.SetClassBodyMeshesFromGlb` / `Content.SetClassBackpackMeshesFromGlb` calls. Keep class and skinset identities stable. If only some races or equipment layouts are covered, declare that scope explicitly.
4. Compare the preview and actual game avatar for every claimed skinset with equipment on and off. Check native faces and customization, backpack attachment, combat clone, idle/attack/hit/death animation, clipping, fallback, and resource cleanup. An external render or offline profile check cannot establish live support.
5. Run focused model preflights, `verify-change`, and available `ingame-smoke` checks. Preserve source assets and exact asset/profile hashes with evidence; report each skinset as validated, failed, or not tested.

Deliver the supported-layout matrix, package assets and bindings, visual comparison, and precise runtime evidence or remaining gates.
