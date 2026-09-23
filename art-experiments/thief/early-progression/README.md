# Original paired dagger progression art

Six original pairs extend the separately authored Street Twins. Windowfangs have
narrow clipped tips and a compact iron guard. Guild Twin Daggers carry matched
brass guards and small rook pommels. Velvet Fangs have pronounced blade shoulders
and balanced steel fittings. Locksmith's Picks are long, narrow tools with open
key-bow pommels. Nightglass Twins use broad dark leaf blades, bright bevels and
ivory guards. Trailbreakers have practical field blades, pale trail chevrons and
small cord eyes. Their silhouettes change before palette color does. The revised blades use broader
cutting planes and restrained silver edges for combat-camera readability. Equipped
exports share the Street prototype mount rotation, `Z55 @ Y90`, while icons and
loot displays keep an outward fan with clear space between the tips.

Each item has five static GLBs: an equipped blade used in both hands, blade and
grip fragments, and two transformed display blades. Both members of each pair
are one inventory identity. Editable source arrays and component maps accompany
every export; six transparent icons render the same source geometry. Six large
previews support visual review. No native surfaces or third-party artwork are
used.

The approved design uses native one-hand dagger and buckler items as the emergency
fallback. It defines no custom fallback progression, so none is added here.

## Reproduce

Run from the repository root with Blender, NumPy and Pillow available:

```sh
blender --background --python art-experiments/thief/early-progression/build.py
python3 art-experiments/thief/early-progression/validate.py
```

The generator reuses the original Street Twins geometry utilities, strict FTK
static writer and original icon renderer. Their hashes are recorded alongside
the existing permitted `dualKnife` renderer-transform metadata. No game files
are read by the build. Package assets are copied only into the source package's
`assets` directory. Nothing is deployed or launched.

`manifest.json` maps stable item IDs to each GLB, palette, icon and renderer
transform. `validation.json` checks independent GLB decoding, finite vertices,
index limits, normals, UVs, closed outward components, exact fragment
reconstruction, display transforms, icon framing and package hashes.

These are offline art outputs. Game fitting, both hands' native motion, item-card
framing and break lifetime require later acceptance. This art task does not
claim those results.
