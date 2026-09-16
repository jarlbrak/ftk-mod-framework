# Rivenquill Cockatrice boss-route live validation

This immutable archive records a fresh isolated `bossCockatrice` / `enbaseycockatriceboss` / `enBaseyCockatrice` trial in session `9927e419cda1403481d25b8db1a4be04`. It pins the original Rivenquill GLB and palette, exact 50-bone signature, live owner/renderer binding, and all 331 capture source PNG hashes.

The boss profile bound the authored GLB to the exact resource renderer. Two complete 120-frame captures retain visible idle/native attack and an ordinary no-focus nonlethal player hit (HP 540 to 539 with native `Damaged`). The explicit `KillSingle` fixture issued native `Death`; native cleanup destroyed the renderer after 91 retained frames, which is preserved as an expected partial death capture. The guarded sequence then reached strict Ready at level 0, room 2.

Selected original frames accept a coherent, readable combat-camera model for this exact source pair with limited material review. The scope excludes portraits, culling, collision, long-session resource lifetime, every ability variant, normal lethal damage, ragdoll behavior, and final art direction. It does not validate the independently serialized `cockatriceC` / `enbaseycockatricesmall` route.

`archive.py` refuses to overwrite a completed archive, excludes native game payloads, preserves lossless metadata and PNG hash pins, and derives presentation videos. Run `python3 verify.py` to recheck exact identity, source snapshots, every pinned image, selected copies, event evidence, expected death termination, and video frame counts.
