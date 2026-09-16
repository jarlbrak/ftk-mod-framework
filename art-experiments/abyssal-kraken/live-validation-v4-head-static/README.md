# Abyssal Crown V4: skinned head plus rigid eye

This immutable supplement records one fresh isolated trial for the modern
`krakenHead` profile. It covers the original seven-bone V2 head at `kraken2`
and the original V4 rigid eye at
`Root_M/base/body/neck/eye/kraken2_eye`.

The trial identifies both assignments on the same `Enemy Dummy` owner. The
head is `ftkmf_glb_abyssal-crown-kraken-head-v2.glb` with its seven-bone
signature. The eye is a `MeshRenderer` with exactly one `MeshFilter` and mesh
`ftkmf_static_glb_abyssal-crown-kraken-eye-v4.glb`. Its private material uses
`ftkmf_abyssal-crown-kraken-eye-v4.png`, has native emission disabled, and is
seen under sampled Intro and Attack states.

![Idle sample](selected/pass-0012.png)

The pass and ordinary attack captures retain 120 frames each. The accepted
ordinary no-focus attack changes the same target from HP 324 to 316. The
explicit `KillSingle` fixture reaches HP 0 and retains 85 frames before native
renderer destruction ends the capture. The native fixture reaches strict Ready
at level 0, room 2. Those facts do not turn the fixture into ordinary lethal
damage or establish final resource disposal.

The key visual finding is narrow. V4 replaces the large pink-orange native
static surface left by the skinned-only replacement. At the sampled combat
camera, the original V4 geometry appears as a small dark teal lower-eye form.
That is technically cleaner but does not yet read as a fully integrated final
character feature. Its art status is therefore **unapproved**. The later
placement probe found that offsets deeper inside the skinned head become
occluded; the probe does not change this verdict.

`validation.json` contains the exact profile, owner/renderer records, action
boundaries, material state, visual verdict, and limitations. `metadata/` stores
59 losslessly compressed source records. `source-image-pins.json` hashes every
325 raw capture PNG. `selected/` copies six reviewed source frames and `video/`
contains presentation derivatives for the pass, ordinary attack, and fixture
prefix. `integrity.json` verifies every metadata gzip round-trip and output
hash.

Re-run `archive.py` only against the pinned source files. It refuses to
overwrite a completed archive unless `FTK_ARCHIVE_REBUILD=1` is explicitly set.
