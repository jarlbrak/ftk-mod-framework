# Cinderbloom fresh live trial V3

This supplement records the fresh catalog-411 run in session `832c6a68d7084c4e967a53274884d574` using plantA at public visual-scale factor `1.0` (captured native CEL scale `1.6`). Both authored parts bound to the same enemy owner: `enPlant01` uses the corrected facing body GLB and `enPlant01Leaves` uses the separate leaf GLB, with separate observed bone signatures.

The corrected V2 mouth, teeth, ember throat and jaw cup face the combat camera. Selected idle and attack views retain the stem, corolla, mouth, root leaves and separate leaf renderer without obvious multipart separation. Both runtime materials use the authored basecolor; the inventory reports black emission values with no emission maps while the inherited emission keyword remains present.

The pass and ordinary attack captures are complete 120-frame recordings. The ordinary attack changes the same target from HP 58 to 50 with `cheat=None` and no focus. The explicit `KillSingle` fixture completes 120 frames, removes the enemy, accepts one native Collect and reaches strict native Ready at level 0 room 2. The fixture death is not ordinary lethal damage.

Root reviewed two idle frames, two attack frames and the fixture endpoints. Native targeting UI/effects and the victory item surface limit fine teeth, jaw, leaf-intersection, complete deformation, culling-envelope, portrait/resource lifetime and finished-art acceptance.

`validation.json` preserves the case result, setup/action journals, helper responses, authoring assets and immutable V1/V2/native-scale records as gzip-lossless metadata, all current source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
