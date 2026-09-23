---
name: preview-ftk-assets
description: Render original FTK models, equipment, skins, or item icons outside the game for visual review. Use for studio views, turntables, comparison boards, and icon legibility checks; keep runtime validation separate.
---

# Preview FTK assets outside the game

Create a reproducible visual review of the **actual authored geometry and textures**. A concept illustration can guide the art, but cannot stand in for a render of exported assets. Keep editable source, export, render command, asset hashes, camera setup, and a clear caption with the result.

Read the root `AGENTS.md`, `tools/ai-model-pipeline/AGENTS.md`, [custom model guide](../../../docs/CUSTOM-MODELS.md), and the relevant authoring route in [the model workflow](../../../skills/ftk-custom-models/SKILL.md). Use [the Paladin package](../../../marketplace/packages/paladin/content.json) as the shipped content example. Check the current pipeline and source scripts before reusing a command; a historical art-experiment script may describe superseded assets.

## Make the review useful

1. Identify each previewed asset by its source and exported runtime path. For a player skin or armor, record the sex/skinset and conditional body, hair, helmet, apparel, weapon, and shield branches being shown. Native FTK bodies, faces, hair, and race choices remain game-owned. Do not present a fabricated mannequin or a single human body as proof that other races fit.
2. Render the exported geometry in bind or neutral pose first. Include front, three-quarter, side, and back views at a shared scale, then a combat-camera-sized view. Use consistent lighting and framing across progression tiers. Include closeups where seams, boots, hands, face clearance, or material slots matter. For items, show equipped orientation and the distinct loot-display or card orientation if both exist.
3. Compare the complete silhouette, tier readability, joins, clipping, normals, transparency, color, icon readability at native size, and whether the preview shows every material and mesh slot. Review female and male assignments and every intended race/body profile. When a real native avatar is unavailable to the offline renderer, label the missing fit check and obtain a native game preview later.
4. Keep concept art, Blender construction renders, exported-mesh previews, and native game captures explicitly labeled. A simulated pose may help find collisions, but it does not establish native animation behavior. Record which views were visually inspected and what remains open.

The Paladin source provides concrete routes: `art-experiments/paladin-equipment/render_icons.py -- --characters-only` renders armor icons; `art-experiments/paladin-characters/loot-display/render.py` builds a rigid loot-display studio image. Their prerequisites and exact commands are documented in `art-experiments/paladin-characters/README.md` and its `loot-display/README.md`. Use them only for their stated assets. For a new asset, adapt a local Blender scene and the repository's FTK exporter/validator rather than treating an arbitrary glTF preview as runtime-ready.

Offline renders can support **art direction and visual QA**. They do not prove the constrained GLB loads, the exact native renderer or skeleton binds, materials survive the game's loader, equipment branches select correctly, any race fits, combat motion works, or resources clean up. Use the [model authoring workflow](../../../docs/MODEL-AUTHORING.md) for binary and binding checks, and the [in-game smoke workflow](../ingame-smoke/SKILL.md) for live evidence when authorized. Report those gates separately.
