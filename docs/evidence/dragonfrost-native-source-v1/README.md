# Frost Dragon native topology audit

Read-only serialized metadata and fresh installed-assembly decompilation. This supports an **existing native dungeon combat diagnostic**, not natural dungeon spawning or runtime/visual acceptance. No game calls, native asset payload copies, or framework edits.

## Exact identity and representative reconciliation

`dragonFrost` resolves CEL138558, root GameObject29673, renderer121561 at CEL-relative `enDragon`, mesh2658 `enDragon`, Animator116974/controller5951 `dragonController`, weapon136713. Native CEL root and renderer scale are `(1,1,1)`.

`probe_121525.glb` names a different serialized renderer representative:121525 belongs to CEL138277/Animator116803 under `baseFrostbitePeak/targets/enemy_target/enDragon`; its controller is also5951. Both renderers reference **the same mesh2658**, with exactly equal ordered70 bone names and all70 bind matrices. The renderer local TRS is identity in both. Their outer scene/diorama placement differs and must not be imported as model placement. A representative filename does not identify the actual native owner. `details.json` and `findings.json` preserve this comparison. Existing `catalog-378-preflight.json` also records this precise dragonFrost/probe_121525 combination decoding against70 actual native bones; that historical decode is not current live acceptance.

## Combat viability and hazards

The native row is boss=true, level10, baseHP750, defenses12/12, quickness1.25, maxdamage45, accuracy0.99; `m_SpawnDungeon=false`. Explicit staging therefore exercises the native enemy in a diagnostic fixture, not native spawning eligibility. Root scale is1, not an assumed boss multiplier.

Weapon136713 carries AttackSchedule135096: shuffle=false, eleven entries `0,1,2,0,1,0,2,1,0,1,2` (Prof0/1/2). `EnemyDummy.SetAttackDecision` chooses this schedule before RNG. First attack is `enDragonFrostA`, repeat3, damagePerAttack16. Native `m_ChanceToProf=0` does not disable this schedule. All three proficiencies A/B/C have `m_Suicide=false`; none is the monkeyC suicide mechanism. They use distinct target values0/1/2 and effects bleeding/frozen/entangle. No specific first-turn removal was found in this bounded source audit.

`m_NoFlee=false`; initialization resets `m_AttemptToFlee=false`, but external native triggers can change it. This is not proof of a forced flee or a guarantee against later flee. Preserve actual action/HP evidence and stop on target removal; zero HP alone cannot distinguish damage from native removal. High defense can prevent normal attack HP loss: keep the existing unmet-hit stop rather than retrying until a favorable result.

## Materials, FX and death

One body material:103 `matDragon`, keywords `_EMISSION _METALLICGLOSSMAP`, white emission color. `_MainTex`1729 `dragonIce`, `_EmissionMap`1200 `dragonIce_e`, metallic map1428 `dragonIce_s`; offsets0/scales1. Replacing albedo alone retains the native emission map. No body ScrollingUVs component exists in the inspected hierarchy. Body has two native particle systems and one light. Weapon has FTKTrail and MagicParticle137746 `fxDragonBreathe` with six frost-beam particle systems. Body replacement does not replace this separate native FX.

Controller5951 has16 states and three CombatAction/AttackStart pairs. Use a real combat CEL; `fireEvents=false` alone would not make attack SMBs safe on a constructed no-CEL controller.

Direct death5394 lasts1.833333s and has voice/bodyfall/camera-shake events. Indirect death5417 lasts1.625000s and includes `DeathFallOff` at0.684020s. The native callback **returns immediately for DeathLight or DeathRevive**, so the event alone does not prove model removal. No Rigidbody, CharacterJoint, or FallOffLimb exists in the native body hierarchy. Native OnEnable only sets ragdoll=true if joints exist and does not reset a stale flag; actual runtime `m_DoRagdoll`, Animator and visibility still require observation. Both complete clip event lists and native local rest metadata are retained.

## Reproduction and boundary

Run `scratch/model-venv/bin/python` on `audit_source.py`, `details.py`, `weapon_audit.py`, then `finalize.py` from repository root. `source-pins.json` pins resources.assets, both serialized DB copies, installed Assembly-CSharp and reused/fresh source evidence. DB values use the existing verified aligned field-prefix recognizer with decompiled field order; this is not a full MonoBehaviour deserializer. Optional UnityPy TypeTreeGeneratorAPI was unavailable and no dependency was installed.

Artifacts contain metadata only: no mesh vertices/indices, texture pixels, or sampled native animation curves. Live custom binding, animation/wing-tail deformation, native emissions/FX, portrait framing, ordinary damage and native completion remain separate gates.
