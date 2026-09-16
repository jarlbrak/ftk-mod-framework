# Mireglass Croaker live validation V1

This archive records the exact direct `acidBlobB` / `enAcidMonster` route in
fresh session `f96e7f33362242a6897359725e3b7402`. The runtime inventory bound
the authored `mireglass.glb` to one owner with the expected 32-bone signature.

The reviewed 120-frame native captures record `AcidBlob_Idle`, both native
attack clips, `AcidBlob_HitSmall`, and `AcidBlob_DeathDirect` while the same
authored mesh identity remains present. The ordinary no-focus player hit lowers
the same target from HP 86 to 76. The separate explicit `KillSingle` fixture
then drives native death/loot handling, and guarded collection reaches strict
Ready.

The selected frames show a coherent articulated marsh-dragon in idle, attack,
hit and initial death transition. Native effects and the foreground hero obscure
some close surface detail; the quick disappearing death effect is not a settled
corpse or ragdoll review. Fixture death is not ordinary lethal-damage evidence.
Culling, portraits, long-session resource lifetime, other ability variants and
final art-direction approval remain separate gates.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all source PNGs, copies reviewed originals, preserves metadata losslessly,
and derives one 120-frame presentation video per action. Native payloads and
DLLs are excluded. `inputs/` preserves the exact catalog and registration
snapshots used by the live trial, so later isolated-catalog revisions cannot
change the recorded source configuration. Run `python3 verify.py` to
independently recheck the archive's hashes, compressed metadata, selected
frames, captured summaries, and video frame counts.
