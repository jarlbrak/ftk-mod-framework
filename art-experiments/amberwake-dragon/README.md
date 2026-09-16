# Amberwake Dragon

Original slate-and-amber dragon in progress for the exact dragonFrost rig. Four articulated legs, swept ivory horns, warm eyes and amber wing membranes follow original authored surfaces. Only native bone names and inverse-bind landmarks supply geometry placement; no native surface is copied.

The profile filename probe_121525.glb represents a shared rig. Actual dragonFrost renderer121561 has the same mesh, ordered70bones and exact inverse binds according to the pinned [native source analysis](../../docs/evidence/dragonfrost-native-source-v1/README.md). The native boss is excluded from dungeon spawning; the isolated dungeon diagnostic is a deliberate fixture and does not prove natural placement. Native weapons, effects, controller, physics and AI remain unchanged.

Status: frozen original with scoped V3 live evidence, not finished-art or full-game acceptance. The current original mesh has 9,084 vertices and 3,028 triangles. Binding validation, binding-only regeneration, positive closed-piece volumes, native bind-bound containment and an independent Blender round-trip have passed their respective offline checks.

[runtime-profile.json](runtime-profile.json) declares the exact `dragonFrost`
`enDragon` route, retains the reviewed `0.25` visual factor, and passes static
direct-route preflight. It supplies a repeatable staging input without claiming
that the current document shares a historical live archive's revision.

The canonical [V3 live archive](live-validation-v3/validation.json) binds the authored mesh to the exact `dragonFrost` `enDragon` renderer 121561 under one native owner at intentional public visual scale 0.25. The captured Standard material uses the authored basecolor with emission disabled. Two complete 120-frame sequences preserve settled idle, native `AttackProf`, recovery, an ordinary `Damaged` response and a later native enemy action. Twenty-three reviewed originals show a readable body, head, four legs, tail and paired wings through those sampled motions without an observed detached surface or mesh explosion.

The ordinary V3 attack used zero focus and produced native `Damaged` for 11 HP, from 675 to 664. It ran under an explicit disposable hero fixture that capped the equipped native attack skill at 0.95 and raised native weapon maximum damage from 10 to 30. The runner pinned the exact hero, stats, weapon, character event listener, animator and controller, then restored physical augmentation to 0 and native maximum damage to 10 at between-room `Ready`. This establishes the model's ordinary native hit response and repeatable route behavior. It does not establish representative combat balance or guarantee that another attempt will hit.

The separate explicit `KillSingle` fixture reduced HP 664 to 0 for a recorded 988 damage and retained exact death frames 0 through 90 before renderer destruction. Reviewed frames show native animated falling and a coherent fallen pose. The source has `m_DoRagdoll=false` and zero rigidbodies, so this is animated death evidence rather than ragdoll evidence. At frame 90 the exact selected renderer is inactive and not visible; a faint background shape does not establish an active corpse or later lifetime. The archive therefore does not claim ordinary lethal damage, a complete 120-frame death capture, cleanup causality, corpse lifetime or final resource disposal.

No loot `Collect` occurred; strict `Ready` succeeded at level 0 room 2. The dungeon fixture is not a natural boss-spawn or full-campaign claim. Native scale 1 crops the calibration dragon severely, which is why this recorded original-model trial uses the deliberate 0.25 camera-fit factor. Native UI, effects, blur, hero overlap and combat distance obscure fine anatomy. Tail and lower-jaw intersections, distant camera scale, culling, portraits, alternate materials and finished-art acceptance remain open. The archive preserves the exact scale, source hashes, raw metadata, 331 capture PNGs, 23 reviewed originals and three presentation videos.

The [V2 archive](live-validation-v2/validation.json) remains immutable focused-attack history, and [V1](live-validation-v1.json) remains the earlier ordinary no-focus boundary. V3 supersedes them as the canonical current route without rewriting either record.

`offline-history/` preserves the initial bound correction, the pre-connector wing study and the overly bright studio setup. Runtime geometry stays in bind pose; studio lighting changes do not alter that exported pose.
