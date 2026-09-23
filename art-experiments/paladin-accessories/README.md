# Original Paladin accessories

Twelve original display objects implement the approved accessory briefs in
[Paladin equipment](../../docs/paladin/EQUIPMENT.md) and
[art direction](../../docs/paladin/ART-DIRECTION.md). Astra High authored the
geometry and creative revisions. No native surface data was inspected or copied.

The trinkets progress from a stamped tin token and waisted brass seal to a silver
watchtower case. Endgame choices have separate constructions: an ivory lantern
with protected blue glass, a dark case around crimson wax and folded ribbons,
and a broad gold balance on black stone. Necklaces progress from cord and a plain
iron pendant to brass chain and a curved silver gorget. Mercy uses a hinged ivory
locket, Censure a faceted red medallion, and Verdict an articulated navy and gold
collar. Each object has closed original component geometry, including backs,
fastenings and frame depth. Intersections between assembled parts are intentional;
the objects do not claim a Boolean-unioned manifold.

## Reproduce and validate

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/paladin-accessories/build.py
python3 art-experiments/paladin-accessories/validate.py
python3 art-experiments/paladin-accessories/preview.py
```

Blender creates twelve strict static FTK GLBs, twelve transparent 256-square PNG
icons, a shared seventeen-swatch palette, editable `.source.json` arrays and named
`.pieces.json` maps. It also regenerates a local editable Blender studio, which
stays outside ordinary git history. Every icon renders the same exported source
mesh. `preview.py` creates the labeled actual-mesh contact sheet and a separate
64-pixel icon review strip with Pillow. These previews are offline studio art.

For reproducibility, preserve `manifest.json` outside this directory, run a second
complete Blender build, then invoke `validate.py --compare-manifest <saved-path>`.
`reproducibility.json` records the comparison. PNG Date and RenderTime ancillary
text chunks are stripped without altering encoded pixels or color information.
The validator independently decodes the GLBs, checks equality to source arrays,
index and normal validity, outward triangle-normal agreement, positive signed
volume and closed edges per named component, contiguous piece coverage,
orientation handedness, palette and transparent icon framing.

## Delivery contract

Runtime files are `paladin-trinket-{family}.glb` and
`paladin-necklace-{family}.glb`, with corresponding `-icon.png` files and shared
`paladin-accessory-palette.png`. The six families are novice, oathkeeper, highward,
mercy, censure and verdict. They use one static triangle primitive, no skin or
joints, flat normals and constant-per-piece palette UVs. V is centered at 0.5,
so the loader's required V inversion preserves the one-row palette.

The exact template renderer paths supplied by the separate native metadata audit
are `trinketDefense1 / trinketMetalPlate / trinketHorn2` and
`amuletVitality1 / amluetLocket1 / amuletLocket1`. Each has one active MeshRenderer
and one material slot. The child transforms are identity. Necklace root is
identity; trinket root quaternion is `(0, 1, 0, -1.6292068494294654e-7)` in Unity
x/y/z/w order. The generator applies its inverse to the original authored runtime
coordinates. The source `authorToRuntime` matrix records this transform, and icon
reconstruction inverts it. This uses only permitted transform metadata and keeps
orientation determinant positive. It does not establish native camera fit.

Original authored coordinates use Blender X-right, Z-up, front toward -Y. Export
first maps `(x,z,-y)` to Unity, then applies the root compensation. Size and
placement are original authored choices, not native bounds. The manifest pins
all delivery files and source dependencies. Existing art is reused only through
original primitive and icon-export code and the original split-oath motif.

Package synchronization is explicit and separate from the offline art build:

```sh
python3 art-experiments/paladin-accessories/sync_package.py
```

It validates first, copies only this campaign's 25 runtime files, and adds their
relative source paths and SHA-256 hashes to the existing Paladin asset provenance.
It does not build, deploy or launch the game.

## Acceptance boundary

The actual-mesh contact sheet and 64-pixel icon strip were visually inspected for
silhouette, family progression, color placement and recognition without glow.
The initial validator found small decorative bevel degeneracies, which were
removed before the final closed-component validation and full rebuild comparison.
No native geometry or material contents were used to resolve those defects.

Offline exports and art inspection do not establish native item-card, merchant,
loot, inventory or collection appearance, exact scale or facing, binding success,
material color fidelity, save/load, replacement, repeated UI creation or owned
resource cleanup. These slots are display-only and add no character attachment.
No deployment, game launch or in-game testing was performed. Live testing is
paused pending the user's separately authorized next step.
