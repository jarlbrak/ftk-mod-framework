# Mireglass Croaker

Mireglass Croaker is an original low-poly marsh-dragon package for the exact
`acidBlobB` route: native prefab `enAcidMonsterB`, renderer `enAcidMonster`
(`121345`), and `AcidBlobController` (`5931`). It uses the B-side 32-bone
palette and inverse bind matrices, so AcidBlob A evidence cannot substitute for
this route's future live result.

![Mireglass Croaker studio hero](mireglass-hero.png)

![Mireglass Croaker studio side](mireglass-side.png)

The sculpture is original peat, jade, lantern-mint, amber, and reed-bone
geometry: a broad pond-dragon body, layered back shell, paddle feet, compact
gilled face, and four cheek tusks. Its authoring phase reads only `bone_names`
and `bindposes` from the local reference. The separate export validator reads
the local reference to reject malformed skin/binary output, but never feeds
native positions, triangles, normals, UVs, weights, materials, textures, or
animation samples into the authored geometry.

`build-report.json` records the strict 32-bone export result. Every palette bone
has positive authored geometry, while `mireglass-palette.png`, the source JSON,
and the named-piece list are regenerated from the same deterministic script.
The read-only [route preflight](route-preflight.json) independently pins this
profile to AcidBlob B's exact renderer, combat fingerprint, and source ID; it
does not establish native runtime acceptance.

## Rebuild and inspect

Extract the exact local reference first; it stays under ignored `scratch/` and
must never be committed or distributed.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$FTK_DATA/resources.assets" --renderer-id 121345 \
  --output scratch/acidblob-b-reference-v1
scratch/model-venv/bin/python art-experiments/mireglass-croaker/build_geometry.py
/opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/mireglass-croaker/render_studio.py
scratch/model-venv/bin/python art-experiments/mireglass-croaker/verify_original_geometry.py
```

The studio images are authored bind-pose review aids. They do not establish
native FTK appearance, animation, culling, materials, portraits, or gameplay.

## Isolated live validation

The completed [V1 archive](live-validation-v1/validation.json) records a fresh
exact `acidBlobB`/`enAcidMonster` binding of `mireglass.glb` with its expected
32-bone signature. Its three complete 120-frame captures show `AcidBlob_Idle`,
both native attack clips, `AcidBlob_HitSmall`, and `AcidBlob_DeathDirect` while
the same authored mesh stays present. The ordinary no-focus hit reduces the
same target from HP 86 to 76; the separate `KillSingle` death fixture and
strict Ready result are explicitly kept distinct from ordinary lethal damage.

The reviewed selected frames show a coherent body in idle, attack, hit, and the
initial death transition. Native effects and the foreground hero limit close
surface review, and the rapid native disappearance is not a corpse or ragdoll
acceptance. Portraits, culling, long-session resource lifetime, other ability
variants, and final art-direction approval remain open.

Verify the immutable archive independently with:

```sh
python3 art-experiments/mireglass-croaker/live-validation-v1/verify.py
```

The archive preserves the exact catalog and registration snapshots used for the
trial, so later isolated test-profile updates cannot alter this evidence. No
AcidBlob A archive transfers to this B-side source pair.
