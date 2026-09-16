# Sablevine Serpent live validation V2: scale 0.55

This immutable archive records a fresh resource-prefab `enbaseysnake` / `snakeJungleA` / `enSnake_Basey` trial in session `9c79f4374ece4e66b9a6ebbd3d411518`. V2 reuses the exact original Sablevine GLB and palette from V1; its only profile revision is public `visualScale: 0.55` under a fresh key.

The registration snapshot records a native prefab root scale of `[1, 1, 1]` and the public factor. Binding metadata and every one of the 360 captured renderer frames record the spawned CEL root at `[0.550000011920929, 0.550000011920929, 0.550000011920929]`. That is measured live scale evidence, not a fit inference from profile arithmetic.

Three complete 120-frame native captures preserve the exact 44-bone Sablevine mesh through `Snake_Idle`, native `Attack` / `Snake_BiteAttack`, an ordinary same-target no-focus hit with `Damaged` / `Snake_HitSmall` (HP 58 to 45), and an explicit `KillSingle` fixture with `Death` / `Snake_DeathBig`. The guarded sequence reaches strict Ready at level 0, room 2. Root-reviewed idle and attack frames show the complete practical serpent silhouette inside normal combat framing; selected hit and death frames retain its coherent bound form.

This accepts a usable limited-art combat-camera result for this exact source pair. It does not accept portraits, culling, collision, long-session resource lifetime, every native ability variant, ordinary lethal damage, ragdoll behavior, or final art direction. V1 remains a separate immutable camera-fit rejection.

`archive.py` is offline-only and refuses to overwrite a completed archive. It pins all 360 source capture PNGs, preserves selected originals and lossless metadata, then derives one 120-frame presentation video per action. Native game payloads and DLLs are excluded. Run `python3 verify.py` to independently recheck snapshots, hashes, exact scale, selected frames, capture summaries, causal motion, and video frame counts.
