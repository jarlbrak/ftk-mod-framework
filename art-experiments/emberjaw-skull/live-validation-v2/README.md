# Emberjaw fresh live trial V2

This supplement records the fresh catalog-411 run in session `2be3d41ce2c24361900069460e222deb` using skullA at public visual-scale factor `1.0` (captured native CEL scale `0.75`). Both authored parts bound to the same enemy owner: `ChaosSkullBottom` uses the jaw GLB and `ChaosSkullTop` uses the cranium GLB, with separate observed bone signatures.

The upper cranium, horns, eye sockets, teeth and independent jaw remain a coherent readable skull in the selected idle and attack views. Both runtime materials use the authored basecolor, but the native `chaosBeastBody` emission map and white emission color brighten the live palette under purple effects; this material/readability limitation is preserved explicitly.

The pass and ordinary attack captures are complete 120-frame recordings. Ordinary damage changes the same target from HP 69 to 59 with `cheat=None` and no focus. The explicit `KillSingle` fixture completes 120 frames, removes the enemy, accepts two native Collect actions and reaches strict native Ready at level 0 room 2. The fixture death is not ordinary lethal damage.

Root reviewed two idle frames, two attack frames and the fixture endpoints. Native emission/effects, targeting UI and the victory item surface limit exact live color matching, complete jaw/death deformation, culling-envelope, portrait/resource lifetime and finished-art acceptance.

`validation.json` preserves the case result, setup/action journals, helper responses, authoring assets and immutable earlier live/native-scale records as gzip-lossless metadata, all current source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
