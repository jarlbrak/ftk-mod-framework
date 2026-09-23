# Street prototype revision 2

Status: **unapproved experimental prototype**. Native front, three-quarter,
side and back review found improvements to the chest panel and knife shape,
but the cap remains lumpy, the front torso reads as a plain rectangle, and
weapon lighting/readability is uneven. Do not promote these assets to the
production package. Further art revisions await a concrete visual anchor.

This candidate responds to the first prototype's native review:

- A broad brow fold follows the outside of the cap. The crown returns to the
  established Street clearance envelope with a small leftward cloth slouch.
  The whole crown is not lowered because the rejected pass already exposed
  the native scalp. The face and ears remain open.
- The headwear has no projecting neck geometry. Its collar is part of the coat
  and follows `Neck_M`.
- A continuous slate jerkin carries a narrow diagonal closure seam. The waist,
  split hem, single shoulder drape and one pouch remain readable garment parts.
- Matching bracers and boots use one broad retaining strap. The bracers are
  wider to cover retained native detail; boot cuffs have a shallow single rim.
- Knife edges converge to a distinct point in the last third. Both faces use
  middle grey steel and the silver cutting bevel is narrow.

The ten GLBs are named exactly for their package counterparts. See
`manifest.json` for paths and hashes. The paired weapon still uses the same
`thief-street-twins.glb` in both native hands, with the established `Z55 @ Y90`
mount orientation. Headwear keeps the `helmKettle` crown mount and the authored
face-center offset of `Y=-0.55`. Both coat bind palettes and the shared boots
retain the exact established Street inverse bind matrices.

Every assignment that uses one of these GLBs must use
`thief-street-v2-palette.png`. In an isolated test package, replace the selected
GLB files and update only their texture assignments. Other tiers, the original
coat/boot display meshes and existing icons still belong to the production
palette. The builders never copy to production or change package definitions.

Rebuild from the repository root:

```sh
python3 art-experiments/thief/art-direction-v2/prototype/revision-2/build.py --bindings BINDINGS_DIR
python3 art-experiments/thief/art-direction-v2/prototype/revision-2/validate.py
blender --background --factory-startup --python-exit-code 1 --python art-experiments/thief/art-direction-v2/prototype/revision-2/render.py
git diff --check
```

The binding directory must contain the exact `male-armor.json`,
`female-armor.json` and `boots.json` hashes already recorded by the prior Street
campaign. Only joint names and inverse binds are used from those files.

`build.py` and the generated `.source.json` files are editable original geometry;
`.pieces.json` maps each closed construction piece. `validation.json` records
independent binary decoding, source equality, nondegenerate outward surfaces,
normalized complete joint weights, exact previous Street binds and rest-pose
identity. All ten GLBs pass those checks.

The six offline views use actual exported geometry in the neutral bind pose.
Their camera setup and hashes are in `preview.json`. They deliberately omit the
native body, face, hands, hair and backpack and cannot establish native fit.
Male native stationary views were reviewed and did not meet the art bar.
Complete idle/attack/hit/locomotion intervals, female and alternate profiles,
display framing and lifetime remain separate gates.

## Narrow seam correction

The side studio view additionally exposed a closure seam floating ahead of the
chest. `correct_seam.py` places that seam against this project's original
authored jerkin surface and writes only the two corrected coats to
`seam-correction/`. The ten native-reviewed GLBs in this directory are retained
unchanged, including their recorded hashes. The correction has independent
binary/surface/bind validation but no native or art approval. It uses this
directory's palette and makes no broader art change.

```sh
python3 art-experiments/thief/art-direction-v2/prototype/revision-2/correct_seam.py
```

Its manifest pins the native-reviewed predecessor hashes. The paired dagger,
cap, boots, production package, and package definitions are untouched.
