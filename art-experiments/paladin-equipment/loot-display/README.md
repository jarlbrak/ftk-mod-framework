# Fitted original two-handed hammer displays

The native two-handed loot card cropped the original hammer head. Equipped weapon
geometry remains unchanged. These six dedicated static display meshes reuse the
complete original two-handed hammer geometry with a uniform scale and translation
only, retaining its native display angle.

The fit is anchored to the original novice one-handed hammer, whose native card
was observed to fit. The generator applies verified native child transforms to
both original source meshes, centers the two-handed result on that one-handed
reference, and fits its X/Y bounds inside the reference with a five-percent margin.
It then applies the inverse native two-handed child transform to export correct
MeshFilter-local coordinates. No native surface geometry or bounds are used.
The display assignment targets `hammer`; worn models and one-handed displays are
unchanged. Existing original inactive fragment assignments remain separate; this
work does not establish visible break behavior.

```sh
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/loot-display/build.py
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/loot-display/validate.py
```

`manifest.json` retains original input and native attachment metadata hashes,
per-set scales/transforms and resulting bounds. A separate export reproduced all
12 source/GLB file hashes. The validator independently decodes the binary arrays
and verifies equality, normals, UVs, indices and winding. Containment is offline
framing evidence; actual native card fit and refresh remain pending.
