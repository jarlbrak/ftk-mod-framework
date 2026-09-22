# Original Paladin armor and boot loot displays

Large inventory/shop cards render a native 3D loot clone rather than the small
item icon. These twelve static GLBs reuse only the already-original Paladin armor
and boot surfaces for all six sets. Armor displays retain the cuirass, gorget,
shoulder armor and original emblems; limb pieces are excluded. Boot displays
retain the paired original plated boots. No native surface geometry is inspected
or copied.

The generator records exact source/piece hashes and selected pieces. It preserves
the authored triangle indices, including reversed winding on mirrored rear
panels, instead of inferring triangle order from sequential vertex attributes.
Each mesh is
centered using its own authored bounds and uniformly scaled to unit height,
positive Y up and positive Z front. Normals and UVs remain those of the original
surfaces. The existing `paladin-character-palette.png` supplies the texture.

Native `armorHeavy1` has a rigid renderer at `armorSplintVestDisplay` beneath its
root, with one material slot. `bootsplayersmith` has no native loot prefab. The
boots now use the verified `bootsHeavy3` template, whose rigid renderer is the
single-material child `bootsIronGreavesDisplay`. Explicit original wearable
bindings and custom modifiers remain unchanged. This selects a native private
clone structure without adding a prefab API or modifying vanilla rows. Armor uses
`displayModels` renderer path `armorSplintVestDisplay`; boots use
`bootsIronGreavesDisplay`. Worn apparel assignments remain separate and unchanged.

The native offscreen camera forces local Y rotation of 180 degrees and uses fixed
scale, without bounds normalization. Unit-height art is a candidate display frame,
not established native fit. Verify framing, front visibility, clipping and
ordinary inventory/shop refresh in the isolated game before acceptance.

```sh
scratch/paladin-venv/bin/python art-experiments/paladin-characters/loot-display/build.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/loot-display/validate.py
blender -b -t 2 --python art-experiments/paladin-characters/loot-display/render.py
```

A separate re-export reproduced all 36 source, piece and GLB hashes. Validation
independently decodes each static GLB and checks display-source equality, exact
projection from the selected wearable source pieces, finite positions, valid
indices/UVs, normalized normals and source triangle winding. It checks positive
volume only for pieces whose welded triangle edges form a closed surface.
Open cloth necklines and fitted toe/instep shells retain their original surfaces
and are reported separately. The ignored studio lineup was visually reviewed. These checks do
not establish live shop rendering, wearer appearance or normal acquisition.

Equipped rigid and loot clone roots are different. All 36 equipment rows use
explicit `displayModels`; armor/boots have no equipped rigid assignment. Hammer,
shield and helmet display assignments reuse the original meshes at the same
native MeshFilter local coordinates while retaining the loot wrapper transforms.
Paths are `smithhammer`, `hammer`, `shieldBlacksmith01`, and `helmKettle`. The
verified hammer fragment children also receive their original meshes under these
prefixes. Their presence does not establish visible break behavior. This route
separation does not imply that all large cards have passed live framing review.
