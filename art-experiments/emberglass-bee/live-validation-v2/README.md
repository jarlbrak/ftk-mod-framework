# Emberglass Bee fresh live trial V2

This supplement records the fresh catalog-411 run in session `59cde8c216d54a79b5d22fab5ac82d0c` using the exact beeA renderer at public visual-scale factor `1.0` (captured native CEL scale `1.25`). The authored `emberglass.glb` bound to `Monster Bee` under one live enemy owner with the expected bone signature.

Selected idle and attack views show the amber abdomen, legs, antennae and paired wings staying coherent at native combat distance. The runtime inventory reports the authored basecolor and the first native Standard material: its emission keyword is present but its emission color is black with no emission map. The native second-slot glow is not claimed.

Pass and ordinary attack captures are complete 120-frame recordings. The ordinary attack changes the same target from HP 58 to 45 with `cheat=None` and no focus. The explicit `KillSingle` fixture reaches the native victory transition, but the renderer is destroyed during frame 104 of the requested 120-frame death capture; this is preserved as the expected death-prefix boundary and is not a full ragdoll or corpse acceptance. The fixture reaches strict native Ready at level 0 room 2. That encounter exposes a Ready vote directly, so no Collect action is claimed.

Root reviewed two idle frames, two attack frames and the death-prefix endpoints. Native targeting UI/effects, the small/dark combat silhouette and the renderer-destroyed boundary limit fine antenna/vein detail, complete death deformation, settled-ragdoll, culling-envelope, portrait/resource lifetime and finished-art acceptance.

`validation.json` preserves the case result, setup/action journals, helper responses, authoring assets and the immutable V1 live record as gzip-lossless metadata, all current source-image hashes, six selected originals and presentation videos for the two complete captures plus the 104-frame death prefix. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
