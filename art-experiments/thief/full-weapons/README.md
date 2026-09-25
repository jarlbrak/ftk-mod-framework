# Complete Thief weapon art

This directory is the current source for all 17 Thief weapons. It supersedes the
weapon outputs in the earlier Street, early-progression, ranged-apparel and artifact
experiments. Those older manifests describe earlier revisions. The authoritative
current export and package mapping is [manifest.json](manifest.json), and the package
receipt is `marketplace/packages/thief/assets/full-weapons.provenance.json`.

The compact dagger family follows the [visual brief](../art-direction-v2/INSPIRATION.md):
straight spines, clipped points, dark leather grips, mid-value steel faces and a thin
bright cutting edge. Plain repairs develop into cleaner guild construction and
specialized endgame forms. Skeleton Key retains asymmetric key/pick blades and open
key-bow pommels. Candle's End has blackened blades, small snuffer guards, contrasting
grips and non-emissive amber inlays. The Unlost Road uses a recurved laminated wood
silhouette, a moss grip and one small directional badge.

| Band | Dagger pair | Bow | Visible construction |
| --- | --- | --- | --- |
| Street | Street Twins | Rooftop Bow | Short plain blade, repaired grip, simple stave |
| Burglar | Windowfangs | Alley Recurve | Broader blade, additional grip wraps, recurve |
| Guild | Guild Twin Daggers | Guild Shortbow | Short brass stop, seal pommel, laminated limbs |
| Masterwork | Velvet Fangs | Gloamwood Bow | Longer balanced blade, steel diamond, deeper recurve |
| Locksmith | Locksmith's Picks | Latchspring | Narrow point, open key bow, brass latch plates |
| Nightblade | Nightglass Twins | Blackthorn | Dark slim blade, angled stop, dark limbs and compact spurs |
| Wayfarer | Trailbreakers | Farstep | Broad field blade, cord eye, pale reinforced limbs |
| Artifact | The Skeleton Key | | Stepped main blade and shorter pick blade |
| Artifact | Candle's End | | Asymmetric blackened pair and amber inlay |
| Artifact | | The Unlost Road | Laminated recurves, moss grip and brass trail arrow |

## Rebuild and verify

From the repository root:

```sh
blender --background --python art-experiments/thief/full-weapons/build.py
blender --background --python art-experiments/thief/full-weapons/render.py
python3 art-experiments/thief/full-weapons/validate.py
git diff --check
```

`build.py` writes editable geometry, component maps and strict rigid GLBs through the
existing FTK exporter, then copies only weapon assets into the package. It reads
stable weapon IDs and filenames from package definitions. Its seven ordinary bow
assignments require `assets/thief-bow-palette.png`; all other texture paths are retained.
It never runs a shared apparel generator. Rebuilding an older experiment can overwrite
these package assets; rebuild this directory last if those historical generators run.

`render.py` independently decodes the exported GLBs. Every `weapon-{contentId}.png`
is a 384 by 384 transparent three-quarter view with orthographic span 1.65 and identical
lighting. These 17 files are the common-scale approval inputs. Suffixes `-front`,
`-side`, `-back` and `-equipped` supply additional views; `-equipped` shows the actual
weapon-local export without a native avatar. Display views reconstruct both exact
native renderer transforms; the dagger display transform deliberately reverses the
preserved equipped `Z55 @ Y90` rotation. Icons use the same decoded geometry with
individual card framing at 256 by 256 RGBA. Icons are copied into the package.

`validation.json` checks GLB decoding against editable arrays, finite vertices, unit
normals, closed outward component topology, valid UVs, exact fragment reconstruction,
display/string transforms, every definition mapping, package hashes and all 17 icon
alpha bounds. The 117 exported GLBs include legacy complete bow files retained for
tooling and eight authored string sources; the latter are not copied into the package.

## Evidence boundary

This is original asset production and offline visual review. No mechanics, item IDs,
rarity, acquisition, stats, renderer paths or native mount matrices change. Public
`itemModels`, `offHandModels` and `displayModels` remain the integration surface.
Native binding, material behavior, male/female and alternate-avatar fit, attack motion,
bow draw/string response, break behavior, loot-display framing, actual inventory
readability, resource lifetime and user art approval remain separate gates.
