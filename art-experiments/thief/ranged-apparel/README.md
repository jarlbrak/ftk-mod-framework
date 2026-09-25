# Thief ranged equipment and apparel source campaign

This directory contains the original source campaign for seven bows and 28 apparel/accessory items. The current [open-face apparel redesign](REDESIGN.md) covers coats, headpieces, boots and charms across Street, Burglar, Guild, Masterwork, Locksmith, Nightblade and Wayfarer. Both female and male coats are exported separately. Bows are a separate ownership and integration route.

The apparel source is `redesign.py`, invoked by `build.py`. `*.source.json` retains editable geometry, `*.pieces.json` maps constructed pieces, and `manifest.json` records the item inventory and hashes. The 28 apparel items produce 35 equipped meshes, 28 display meshes, 28 icons and one dedicated palette. Production copies use the same filenames under `marketplace/packages/thief/assets/`.

## Reproduce apparel only

Use Python with NumPy and Pillow plus Blender. Supply a local metadata-only directory with the exact `female-armor.json`, `male-armor.json` and `boots.json` files whose SHA-256 hashes appear in the manifest. These local inputs are not packaged.

```sh
python3 art-experiments/thief/ranged-apparel/build.py --bindings "$THIEF_BINDINGS" --apparel-only
blender --background --factory-startup --python-exit-code 1 --python art-experiments/thief/ranged-apparel/render.py -- --apparel-only
blender --background --factory-startup --python-exit-code 1 --python art-experiments/thief/ranged-apparel/render_sets.py
python3 art-experiments/thief/ranged-apparel/validate.py --apparel-only --bindings "$THIEF_BINDINGS"
python3 art-experiments/thief/ranged-apparel/finalize_apparel_review.py
python3 art-experiments/thief/ranged-apparel/document_apparel.py
git diff --check
```

`--apparel-only` preserves all bow exports, sources, icons and their original palette. Apparel uses `thief-apparel-palette.png`; every corresponding content assignment must use that texture. `validate.py` validates binary accessors independently, updates only the selected assets in the shared provenance record, and writes `apparel-validation.json`. Its optional `--baseline-dir` compares exact prior skinned joint names and inverse matrices. That baseline is local historical data, not redistributed native art.

## Review and boundaries

[The inventory](APPAREL-INVENTORY.md) lists all item IDs, native templates, level bands, mechanics and visual motifs from current definitions. `apparel-contact-sheet.png` shows all 28 actual asset icons. The 56 exported-mesh views under `apparel-review/` compare every tier and both coat branches at the same scale. Their manifest pins camera, light, assembly convention, palette and mesh hashes. These views omit native body, face, hair, hands and backpack and cannot prove native fit.

The `Surface` helper is generic original geometry code reused from Hearthveil. All Thief garment dimensions, surfaces, colors, UVs, normals and weights are original. Native data use is limited to pinned renderer identities, joint names and inverse bind matrices. Prior native fitting and cap/hood review records cover superseded assets. Validators and renders do not imply art approval or game acceptance.

The historical bow route is recorded in `bow-route.json`; `bow_parts.py` maps separate original body, string, break and display pieces into exact renderer-local spaces. Bow draw, string and fragment checks remain independent from apparel evidence. Do not run the unfiltered build or validator during separate bow authoring without coordinating ownership.
