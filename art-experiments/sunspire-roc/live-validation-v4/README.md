# Sunspire Roc live validation V4

This immutable supplement closes the current archive-integrity and structured
gate gaps for the exact `rocA` / `enRoc01` renderer 121238 route. It reuses the
complete V2 combat session and V3 portrait session after proving that the
selected historical and current catalog profiles are identical. The profile,
authored GLB, texture, source renderer, and motion renderer have not changed.

The retained V2 pass capture records `cidle_roc`, native `attackProf_roc`, and
return to idle. Its ordinary no-focus player attack records HP 81 to 71 with
`damageLight_roc`, followed by recovery, native `attackCrit_roc`, and another
idle. The explicit `KillSingle` fixture records HP 71 to 0 and
`deathHeavy_roc` across a complete 120-frame capture. The custom renderer
reports `m_DoRagdoll=false` and no rigidbodies across all 360 combat frames, so
the death is animated rather than a body ragdoll. Fixture death is not ordinary
lethal-damage evidence. Two guarded native Collects reach strict Ready at
level 0 room 2.

The retained V3 session separately records one constructed native
`uiEnemyEncounterPortrait.Initialize` caller. Its exact 36-bone custom clone
produces a reviewed 328 by 280 row portrait and releases the recorded temporary
clone, owned UI texture, and newly-created preview lease assets. This fixture is
not an opened encounter menu or a live combat HUD.

Root-reviewed originals show the authored crown, eyes, beak, chest, wings,
legs, talons, and tail remaining coherent through idle, attacks, ordinary hit,
animated death, and the row portrait. Combat UI, the hero, effects, camera crop,
and the victory state obscure some surfaces. Full culling coverage, every
animation interval, settled corpse state, precise corpse lifetime, final
resource disposal, and finished art direction remain separate gates. The
archive covers only `rocA`, not `rocB`, `rocJungleA`, or another bird rig.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 360 combat images plus the portrait, copies the reviewed originals,
preserves metadata with gzip-lossless mappings, and records three verified
120-frame presentation videos. Native payloads and DLLs are excluded.
