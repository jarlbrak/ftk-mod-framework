# Thief art direction and asset brief

Status: paper art brief for [the Thief design](DESIGN.md). No concept image, mesh, fitting, or animation acceptance is claimed.

## Visual identity

The Thief should read as agile and resourceful in FTK's small, faceted characters: narrow shoulders, a fitted waist, soft footwear, a visible face, and useful tools. Short coats and cropped hoods preserve leg motion and character expression. Pouches are small and anchored to the garment. The palette uses worn leather, dark blue, muted green, ivory, and aged brass, with broader light/dark shapes visible at combat-camera distance.

Keep the fantasy mischievous rather than grim or murderous. A scratched guild token and a carefully mended cuff say more than blood, skulls, or an oversized weapon. D&D supplies broad archetypes, not copied costume art, logos, item illustrations, or named characters.

## Geometry progression

| Band | Garments and silhouette | Paired weapon and bow treatment | Palette |
| --- | --- | --- | --- |
| Street | Patched short vest, small cap, wrapped shoes; one stitched tool pocket | A matched pair of short practical knives and a plain wooden bow | Warm brown, dusty blue, dull iron |
| Burglar | Fitted leather jack, short open hood, ankle boots with reinforced toes | Wrapped grips, modest metal guards, cleaner bow limbs | Dark brown, blue-gray, restrained brass |
| Guild | Layered fitted torso, split short hem, shaped hood, cleaner boots | Deliberate guild craftsmanship, repeated small rook motif, matching paired guards | Deep blue, aged brass, pale stitching |
| Masterwork | Sharper hood opening, articulated leather panels, defined sole and heel | More distinct profiles, balanced paired blade shoulders, laminated bow silhouette | Charcoal, muted blue, polished edge accents |
| Locksmith | Additional fitted tool pockets, high collar, compact rounded hood | Key/lock motifs concentrated around paired pommels and grips | Petrol blue, warm brass, cream stitchwork |
| Nightblade | Shortest coat hem, sparse trim, angled cowl, tightly wrapped boots | Clean dark blades, narrow bright cutting edge, contrasting paired guards | Charcoal, wine lining, restrained ivory |
| Wayfarer | Open shoulder line, small split hem, laced reinforcement, visible boot articulation | Trail-marker geometry and lighter bow limbs and two related field blades | Moss green, dark wood, pale reinforcement |

Progression must be visible through geometry before texture color. Endgame branches may share original modular construction but need different hood openings, coat panels, fastenings, and boot details. Palette swaps alone do not distinguish the branches. Pieces from different branches should still look wearable together.

## Artifact silhouettes

[Artifact mechanics and lore](ARTIFACTS.md) are authoritative for their identities.

| Artifact | Distinct silhouette | Small visual accent | Avoid |
| --- | --- | --- | --- |
| The Skeleton Key | Long-key/short-pick pair, related brass key-bow pommels, one stepped guard detail | Turquoise inset and optional brief glint | A giant literal key replacing the cutting blade |
| Candle's End | Candle/snuffer pair, pale and charcoal grips, related blackened leaf blades | Thin amber channel, momentary hit flash | Constant fire, smoke, or large skull ornament |
| The Unlost Road | Compact recurve, short limb tips, pale reinforcement strips | Green grip binding and brass trail marker | Dangling string obstacles or oversized antlers |

Weapon scale follows the actual hand grip and native motion. Each dagger must remain recognizably smaller than a sword. Both hands must show the original pair through the actual native paired attack sequence. Avoid a single knife plus a disguised shield or a new independent off-hand weapon slot. Bow grip, limbs, string, projectile emission, and draw animation must agree. This is a separate model route from Paladin hammers; hammer validation does not establish bow support.

## Icons and displays

Every equipment item gets an original PNG icon readable at native UI size. Use the item's silhouette and a simple backdrop; do not put tiny text, a full landscape, or a character portrait in an equipment icon. Each progression band's icons preserve an obvious family resemblance. Artifact rarity is supplied by normal UI and should not depend on a colored border baked into every image.

Sneak Attack uses a small blade with an opening wedge; Prepared uses a cocked blade or arrow; Slip Away uses a boot and curved escape line. Artifacts use key, snuffer, and trail-arrow motifs. The corresponding UI text supplies precise meaning so color is never the sole signal.

Equipped art, shop/inventory 3D displays, loot models, and icons are separate acceptance surfaces. Charms use original keyring, coin, token, wick, or compass object art for their supported display surfaces; no new visible character attachment slot is promised. Class portrait generation continues through the native avatar path.

## Asset inventory and fitting

The 45 items require 45 icons and 45 authored item identities. A dagger pair is one item with two equipped blades and one combined icon/display identity. Seven coats and seven boots need the supported native apparel bindings for female and male bodies, with testing of alternate native race appearances. Seven headpieces and seventeen total weapon items need exact equipped rigid-renderer maps wherever their templates use that route. Charms and all loot/display representations need their own supported renderer bindings. These are asset coverage requirements, not an assertion that every item consists of one mesh.

Reuse original parts, palettes, materials, and textures where appropriate, with a provenance record for every output. Retain editable source geometry and deterministic export scripts. Include any native weapon break fragments or other renderers only if the selected template actually needs them; do not accidentally leave a vanilla fragment visible when the rest of the weapon is replaced.

Inspect front, three-quarter, and back views in class selection, overworld, and combat. Check both blades' paired strikes, bow draws/releases, incoming hits, walking, defeat/revive, equipment swaps, and loot presentation. Verify skin tones, native faces/hair, race features, hands, and feet remain intact. A hood cannot hide fit problems by obscuring the face, and a long cloak cannot substitute for properly fitted armor.

## Art acceptance

- The four campaign silhouettes read as successive craftsmanship levels; the three endgame branches are distinguishable without inspecting stats.
- All seventeen weapon items are identifiable in hand and in the item card; artifact identities remain recognizable when their glow is absent.
- Full and mixed outfits fit supported appearances through native motion. Record each tested appearance and animation explicitly.
- No placeholder geometry, vanilla model fallback, copied commercial asset, or palette-only vanilla reskin is accepted as original finished art.
- Every shipped asset has source, export instructions, provenance, and package-relative paths. Game assets remain local references and are never committed or redistributed.

The existing [player model authoring guide](../MODEL-PLAYER-API.md) and [model skeleton guide](../MODEL-SKELETONS.md) govern implementation. New bindings require fresh evidence rather than inheriting unrelated Paladin fit claims.
