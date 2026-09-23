# Street outfit art prototype

Original Street apparel and paired knives for the [v2 direction](../DIRECTION.md).
The first pass in this directory is retained as a rejected art iteration. Native
review found crown/scalp intersections, a stone-like cap profile, a projecting
neck fold, a flat teal chest panel, and knives with poorly readable dark faces.
Its passing binary validation does not reverse those art findings.

The revised candidate lives in [revision-2](revision-2/README.md). Production
package content and assets are not changed by either builder.

`build.py` is the editable procedural source. Each GLB has matching original
`.source.json` and `.pieces.json` files. `manifest.json` records export hashes,
the package counterpart, palette and exact established Street binding identity.
`validation.json` is an independent decode of these exports. `render.py` produces
neutral views of the actual exports; the native body, face, hands, hair and
backpack are omitted. `preview.json` pins cameras and exported asset hashes.

To rebuild this historical first pass from the repository root:

```sh
python3 art-experiments/thief/art-direction-v2/prototype/build.py --bindings BINDINGS_DIR
python3 art-experiments/thief/art-direction-v2/prototype/validate.py
blender --background --factory-startup --python-exit-code 1 --python art-experiments/thief/art-direction-v2/prototype/render.py
```

`BINDINGS_DIR` supplies `male-armor.json`, `female-armor.json` and `boots.json`.
The builder requires their hashes to equal the existing Street manifest; it
reads bone names and inverse bind matrices only.

Offline validation passes for ten GLBs, including both coat sexes, shared boots,
headwear, paired-weapon main mesh, break fragments and rigid displays. Validation
checks source equality, finite unit normals, triangle orientation, positive
closed-piece volumes, UV bounds, complete positive skin weights, exact existing
inverse binds and the rest-pose identity. It establishes no motion or visual
acceptance. Male native stills were reviewed and this iteration was rejected.
Female and alternate profiles, complete native animation intervals, equipment
rebuild, display framing and resource lifetime remain unverified.
