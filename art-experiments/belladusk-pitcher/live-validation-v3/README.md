# Belladusk Pitcher live validation V3

This immutable supplement closes the structured gate gaps for the exact
`plantE` / `enJungleNibbler_A` renderer 121530 route. It reuses the complete V2
combat session after proving that the selected historical and current catalog
profiles are identical. The profile, authored GLB, texture, source renderer,
and motion renderer have not changed.

The retained pass capture records settled `idle`, native `attack1`, and return
to idle. Its ordinary player attack records HP 58 to 50 with no focus spent,
followed by native `hit1`, recovery, a later `attack1`, and another idle. The
explicit `KillSingle` fixture records HP 50 to 0 and `death` across a complete
120-frame capture. The custom renderer reports `m_DoRagdoll=false` and no
rigidbodies across all 360 frames, so the death is animated rather than a body
ragdoll. Fixture death is not ordinary lethal-damage evidence. One guarded
native Collect reaches strict Ready at level 0 room 2.

Root-reviewed originals show the authored hood, mouth, teeth, throat, stalk,
and four-leaf base remaining coherent through idle, two attacks, ordinary hit,
and animated fixture death. Combat UI, the hero, effects, depth blur, and
victory progression obscure some surfaces. Full culling coverage, every
animation interval, collision, physics sleeping, settled corpse state, precise
corpse lifetime, portrait behavior, final resource disposal, and finished art
direction remain separate gates. The archive covers only `plantE`, not another
plant rig.

The older V2 review mislabeled two early idle frames as attack views. V3 derives
every motion label from the raw animator timeline and selects frames inside the
recorded `attack1`, `hit1`, and `death` intervals.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 360 source images, copies six reviewed originals, preserves metadata
with gzip-lossless mappings, and records three verified 120-frame presentation
videos. Native payloads and DLLs are excluded.
