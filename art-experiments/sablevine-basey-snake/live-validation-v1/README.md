# Sablevine Serpent live validation V1

This archive records the exact resource-prefab enbaseysnake / snakeJungleA / enSnake_Basey
route in fresh session 6aedf9b7ab6e4c4292425002505d64e4. Runtime inventory
bound the authored sablevine.glb to one owner with the expected 44-bone
signature.

Three reviewed, complete 120-frame native captures retain that mesh identity.
They record Snake_Idle, native Attack and Snake_BiteAttack, an ordinary
same-target player hit with Damaged and Snake_HitSmall, then a separate explicit
KillSingle fixture with Death and Snake_DeathBig. The ordinary no-focus hit
lowers the target from HP 58 to 50. The fixture then drives native death and
the guarded sequence reaches strict Ready.

The root-reviewed frames show that the colored serpent remains technically
connected through idle, attack, damaged, and animator-driven death poses.
However, its tall upper body is cropped by the native combat camera in normal
idle and attack framing. V1 is therefore a camera-fit rejection, not usable
art acceptance. The fixture death is not ordinary lethal-damage evidence, and
the no-rigidbody record is not ragdoll evidence. Culling, portraits,
long-session resource lifetime, other native ability variants, and final
art-direction approval remain separate gates.

archive.py is offline-only and refuses to overwrite a completed archive. It
pins all source PNGs, copies reviewed originals, preserves metadata losslessly,
and derives one 120-frame presentation video per action. Native payloads and
DLLs are excluded. inputs/ preserves the exact catalog and registration
snapshots used by the live trial, so later isolated-catalog revisions cannot
change the recorded source configuration. Run python3 verify.py to independently
recheck the archive hashes, compressed metadata, selected frames, captured
summaries, causal motion evidence, and video frame counts.
