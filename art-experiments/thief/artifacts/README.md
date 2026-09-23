# Original Thief artifact art

All three paper artifacts have original geometry, a rendered 256px inventory
icon, a 768px studio preview, editable source arrays, component maps and
package copies. This is offline art production. Native fitting, animation,
material appearance and lifecycle acceptance remain separate work.

## Designs

| Item | Geometry and visual identity | Asset prefix |
| --- | --- | --- |
| The Skeleton Key | Broad silver knife with a stepped shoulder, shorter hooked pick blade, open brass key-bow pommels, dark wrapped grips, turquoise insets | `thief-twins-skeleton-key` |
| Candle's End | Two blackened leaf blades with pale cutting edges, contrasting ivory and charcoal grips, readable amber inlays, candle-cup and hooked snuffer guards, snuffer pommels | `thief-twins-candles-end` |
| The Unlost Road | Compact recurve, weathered dark wood, pale laminated strips, green grip wraps and two anchored short tabs, brass trail arrow | `thief-bow-unlost-road` |

The blades of each pair are separate original designs. The redesigned pairs use
the reviewed Street equipped rotation, `Z55 @ Y90`, around the authored grip
center. Display and icon transforms undo it before arranging their silhouettes. Both main blades and
both off-hand blades are smaller than the bow and use grip-centered authored
landmarks. No particle effect is required to recognize the artifacts. The bow
string is a separate original mesh. Icons show the same original geometry
as the GLB sources, with no rarity border or tiny lettering.

## Files and bindings

Every `.glb` has matching `.source.json` and `.pieces.json` files. Each item
also has `-icon.png`, `-icon.source.json` and `-preview.png` outputs.
`thief-artifact-palette.png` is shared by all three items.

For each paired prefix:

| Suffix | Intended renderer root/path |
| --- | --- |
| `.glb` | Main Weapon, `.` |
| `-offhand.glb` | Explicit off-hand object, `.` |
| `-fragment-1.glb` | Main Weapon, `Break` |
| `-fragment-2.glb` | Main Weapon, `Break/Break` |
| `-display.glb` | Whole `dualKnife` item display, `dualKnife` |
| `-display-offhand.glb` | Whole `dualKnife` item display, `offHandWeapon` |

For `thief-bow-unlost-road`:

| Suffix | Intended renderer root/path |
| --- | --- |
| `.glb` | Main Weapon, `.`; body only |
| `-string.glb` | Main Weapon, `shortbowString` |
| `-fragment-1.glb` | Main Weapon, `Break` |
| `-fragment-2.glb` | Main Weapon, `Break/Break` |
| `-display.glb` | Whole `bowShort` item display, `shortbow` |
| `-display-string.glb` | Whole `bowShort` item display, `shortbow/shortbowString` |

Fragment vertices are converted into the exact child-local transforms and
reconstruct each main mesh once. Candle's End's amber inlay belongs to its
blade fragment. Off-hand templates have no break renderers in the inspected
hierarchy, so no off-hand fragment is invented. Bow fragments partition the
body; the string remains its own renderer and requires separate native
behavior verification.

The paired route comes from `../native-route.json`; the bow route comes from
`../ranged-apparel/bow-route.json`. Both contain permitted identity and transform
metadata only. Bow endpoints are authored landmarks, not measurements of native
surfaces. Static conversion to renderer-local space does not establish that the
native bow draw logic, anchors or projectile emission agree with these shapes.

## Reproduce and verify

From the repository root, using Blender and Python with NumPy/Pillow:

```sh
blender --background --python art-experiments/thief/artifacts/build.py
python3 art-experiments/thief/artifacts/validate.py
git diff --check
```

The generator uses the repository's strict static GLB writer. No game files
are needed to rebuild from the checked metadata. All geometry, colors, normals,
UVs and icon pixels are original authored output. The only shared production
utilities are the existing original Thief geometry helpers and mesh icon
renderer, both pinned in `manifest.json`.

`validation.json` independently decodes the GLB buffers, checks source equality,
finite unit normals, winding, closed outward components, palette UVs, 16-bit
indices, exact fragment reconstruction, display transforms, package hashes and
transparent icon framing. All three 768px studio previews have been inspected
for silhouette, palette, readable details and clipping. These are studio art
checks and do not assert game acceptance.

Package copies live in `marketplace/packages/thief/assets/`; their exact source
paths and SHA-256 values are in `thief-artifacts.provenance.json` there. No
runtime code, content definitions, deployment or game launch is part of this
art slice.
