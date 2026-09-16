# Bronzewake Champion fresh live trial V2

This supplement records the fresh catalog-411 run in session `9df826f02881492aa5be8b9098937942` using the exact bossGladiator chassis at public visual-scale factor `1.0` (captured native CEL scale `1.1`). Four authored renderers bound to one enemy owner: `enBossGladiator`, `hairBottomBossGladiator`, `armorBossGladiator` and `bootsBossGladiator`. The native helmet and weapon/shield accessories remain game-owned.

Selected idle and attack views show the authored body, short hair, armor/trousers and boots moving as one readable champion around the retained accessories. All four runtime materials use the authored basecolor and report no active emission; the boots assignment also explicitly opts out of inherited native emission. Native lighting darkens the palette but leaves the silhouette readable.

Pass and ordinary attack captures are complete 120-frame recordings. Ordinary damage changes the same target from HP 58 to 48 with `cheat=None` and no focus. The explicit `KillSingle` fixture reaches the native victory transition, but the renderer is destroyed during frame 91 of the requested 120-frame death capture. That expected prefix is archived as a boundary, not as full death, settled ragdoll or destruction-causality acceptance. The encounter reaches strict Ready at level 0 room 2 and exposes a Ready vote directly, so no Collect action is claimed.

Root reviewed two idle frames, two attack frames and the death-prefix endpoints. Native helmet/weapon accessories, targeting UI/effects and the renderer-destroyed boundary limit fine plate and hair intersection, complete death deformation, culling, portraits, resource lifetime and finished-art acceptance.

`validation.json` preserves the case result, setup/action journals, helper responses, authoring assets and the immutable V1 live record as gzip-lossless metadata, all current source-image hashes, six selected originals and presentation videos for the two complete captures plus the 91-frame death prefix. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
