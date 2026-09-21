# Original Paladin equipment

Eighteen newly authored low-poly equipment meshes: six one-handed hammers, six
two-handed hammers and six shields. Each family contains starter Novice, early
Oathkeeper, midgame Highward, and three endgame alternatives: Mercy, Censure and
Verdict. These are art names, not finalized gameplay definitions or balance.

The order's original split-diamond and suspended-bead emblem unifies the sets.
The [novice redesign](../../docs/paladin/NOVICE-REDESIGN.md) uses compact steel
hammers and a thin crimson-and-gold shield. Later early-game equipment retains
its blue accents, while endgame equipment uses dark red and gold.
The hammer reference pass recreates Verigan's Fist and Ironfoe features on the
novice paths, Might of Menethil hooks on Mercy and Sulfuras spikes on Verdict.
All vertices and materials are newly authored; reference links and inspection
notes are in the redesign document. Every shield has modeled back fittings and a grip.

## Reproduce

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/paladin-equipment/build_blender.py
python3 art-experiments/paladin-equipment/validate.py
```

The editable generator authors all mesh components through Blender. Its export
collects triangulated geometry in Unity coordinates and calls the repository's
`write_static_glb`. The source JSON and piece maps remain editable durable
records. The generated `.blend`, palette PNG and studio lineup stay local under
the repository's existing ignore rules. Regenerate the palette before packaging.

`manifest.json` pins the generator, all source/GLB/piece records and the original
palette. The generator reads no game assets, extracted surface data, reference
images, textures or third-party art. Minimal binding metadata is unnecessary for
these unskinned assets. The independent validator rereads the GLB binary and
checks source equality, finite values, indices, UVs, winding, normal length and
positive signed volume for every piece.

## Integration boundary

These models use provisional authored local coordinates. The native hammer,
shield and hand attachment transforms, scale and exact renderer contracts have
not been established. A runtime placement revision may be required. A studio
render does not prove an in-game appearance, fit, animation, equipment rebuild,
resource lifetime or gameplay behavior. No asset has an in-game acceptance claim.

The studio scene arranges all models for review; those presentation translations
are applied only after runtime export and do not change exported mesh coordinates.

## Original break fragments and icons

```sh
python3 art-experiments/paladin-equipment/build_fragments.py
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/paladin-equipment/render_icons.py
python3 art-experiments/paladin-equipment/validate_delivery.py
```

The icon renderer also reads the original Paladin character sources and local
binding metadata for the male/female class portraits. It writes 18 equipment
icons, one Guard shield icon, two portraits and 18 armor item icons at 256 square
with transparency.
`icons-manifest.json` pins their source and output hashes. The validator builds
a dark-background contact sheet for visual review.

`fragments-manifest.json` supplies two original break meshes per one-handed
hammer and three per two-handed hammer. Whole authored pieces partition every
source triangle exactly once, with no duplicated or omitted triangles. Source
component intersections are retained; this does not claim manifold union geometry.
The intended post-detach paths are `Break` and `Break/Break` for SmithHammer;
`break`, `break/break2` and `break/break1` for WarHammer. The original complete
hammer targets `.` in either case. All fragments preserve the original source
coordinates. Native child placement and transforms require live confirmation.

`delivery-manifest.json` combines base asset, icon and fragment hashes, preserving
the original geometry manifest as its own provenance boundary. Run the derived
commands again after any source change. Neither supplied replacement fragments
nor offline icon validation proves that native break state or UI selects them.
