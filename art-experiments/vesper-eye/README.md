# Vesper Eye

Original multipart Beholder model: a faceted obsidian orb, ivory eyelid armor, restrained copper seams and a separate amber eye with a vertical pupil. The canonical V4 archive verifies the exact multipart binding, corrected materials, sampled idle, two attacks, ordinary hit recoil, fixture cleanup and Ready progression. Its explicit remaining limits are preserved below.

| CEL-relative renderer | Native renderer ID | Runtime asset | Weighted joint |
| --- | --- | --- | --- |
| EyeBody | 121031 | vesper_body.glb | Root_M |
| EyeBody/EyeEye | 121210 | vesper_eye.glb | eyeball |

Both assignments use `vesper_basecolor.png` and set `disableNativeEmission: true`; `runtime-profile.json` supplies the exact `beholderA` catalog entry. The explicit opt-out is required because preserved native emission washes the intended black pupil yellow. Preserve the captured native visual scale (factor 1). No production loader or native bounds changes are needed by this artifact.

Forward was checked before authoring: the native separate eye extends toward Unity +Z, whereas the body extends behind it toward -Z. The authored pupil faces +Z (Blender -Y). Body and eye retain their own complete two-joint palettes and inverse binds, including the small native bind differences. Each original surface is rigidly weighted to the joint actually used by its corresponding native renderer. The eye is expected to move independently inside the body plates; checking intersections during native eye motion is a live acceptance task.

All surface geometry and the palette are original. Locally extracted reference geometry is used for binding and bounds comparisons only, remains in ignored scratch, and is absent from the Blender scenes and packaged source. Both surfaces fit within their corresponding native bind-surface boxes. This is not proof of an animated culling envelope.

`vesper_body.blend` and `vesper_eye.blend` are editable, tagged mesh/armature scenes. `vesper-studio.blend` combines both for presentation; export each individual part scene, not the combined studio. `hero.png` and `side.png` show the bind pose. The generator, explicit mesh source JSON and named piece ranges permit repeatable edits without proprietary geometry.

From the repository root, with the existing local dependencies and references:

```sh
scratch/model-venv/bin/python art-experiments/vesper-eye/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/vesper-eye/build_blender.py
scratch/model-venv/bin/python art-experiments/vesper-eye/finalize_manifest.py
```

The Blender script creates each scene using the shared template bridge, saves it, reopens the saved file, exports through the FTK writer, and independently validates the result. Roundtrip outputs remain in `scratch/vesper-roundtrip`; their GLB hashes and validation results are recorded in `manifest.json`. The finalizer verifies direct and reopened exports and native bind bounds, refreshes hashes, and resets manual acceptance to pending after regeneration. Studio approval applies only to the reviewed asset hashes.

The canonical checks cover the exact two-renderer assignment, live material state, sampled idle, `eye_attack1`, `eye_attack2`, ordinary `eye_damage`, the explicit fixture Death trigger and renderer cleanup, and native Ready progression. Other controller clips, a visible custom death deformation, ordinary lethal damage, portrait, extended culling, collision, resource lifetime, natural-arena behavior, unperturbed performance and finished-art acceptance remain open.

## First live run: material failure preserved

[Live v1 evidence](live-validation-v1.json) records exact bindings for the body
and separate eye, with unchanged original asset hashes. Each capture completes
120 unpaused frames. The ordinary hit deals8 damage (135 to127); the kill
fixture reaches0. Parent reviewed attack0/50, hit30, death40/60.

- [Attack](live-v1/attack.mp4)
- [Ordinary eye hit](live-v1/nonlethal-hit.mp4)
- [Kill-fixture death](live-v1/kill-fixture-death.mp4)

The intended black pupil is washed yellow by inherited matFloatingEye emission
(native material117). This is a material failure, not finished live art approval.
Native effects conceal death geometry at the reviewed frames; visible death
deformation is not established. A per-renderer emission opt-out correction
and live rerun are pending. The original v1 archive stays immutable. Videos
replay120 frames at12fps, not real-time performance evidence. Two Collect
actions were submitted; Ready confirmation was pending when archived.

Subsequent v1 progress confirmation: `scratch/vesper-ready.json` verifies
strict Ready after both Collect actions. The immutable first-run archive
retains its earlier pending wording; material failure remains unchanged.

## Corrected material live run (same geometry)

[V2 evidence](live-validation-v2.json) preserves unchanged V1 geometry/texture
hashes under framework900f/helper434, sessiona1eba409646e41f2b0784d751865866e.
Both replacement materials report emission keyword off, RGB black and no
emission map. The pupil is correctly black, the amber iris readable and idle
fit proper. This corrects the V1 material failure within reviewed views.

- [Eye attack](live-v2/attack.mp4), reviewed0/50/95.
- [Ordinary eye hit](live-v2/nonlethal-hit.mp4),135 to125, reviewed30.
- [Kill-fixture death](live-v2/kill-fixture-death.mp4),125 to0, reviewed40/60.

All three captures complete120 unpaused frames. Eye/root local rotation
components vary, which is motion evidence rather than an angular measurement.
Native attack effects obscure frames50/95 and native death effects hide geometry
at40/60; complete visible attack/death deformation and culling remain limited.
Two guarded Collect actions reach strict Ready0/2. Videos replay12fps, not
real-time performance. V1 material failure remains archived independently.

## Fresh catalog-411 multipart run

[V3 evidence](live-validation-v3/README.md) records a fresh session using
the exact two-renderer binding: `EyeBody`/`vesper_body.glb` and
`EyeBody/EyeEye`/`vesper_eye.glb` share owner `369188` while retaining
separate observed bone signatures. Both replacement materials report
emission disabled, black emission RGB and no emission map, and the selected
idle views show the amber iris with the intended black pupil.

The pass and ordinary attack captures each contain 120 unpaused frames. The
ordinary attack changes the same target from HP 135 to 125 with no cheat or
focus. The explicit `KillSingle` fixture completes, accepts one native
Collect and reaches strict Ready at level 0 room 2. Root review covers two
idle frames, two attack frames and the fixture endpoints; native UI/effects
and the victory surface still limit fine eye-tracking, full deformation,
culling-envelope and finished-art acceptance. The immutable V1/V2 records
remain separate in the V3 archive.

## Canonical V4 exact-source run

[V4 evidence](live-validation-v4/README.md) pins the current explicit-emission
profile revision, both exact source renderers, all three authored assets, the
fresh runner record, the full live stage journal, three complete 120-frame
captures, 18 reviewed original PNGs and three presentation videos. The stage
inventory records `EyeBody` and `EyeBody/EyeEye` on one owner with their distinct
two-bone signatures and private materials. Both materials use
`ftkmf_vesper_basecolor.png`, disable emission, report black emission RGB and
have no emission map.

The pass capture samples `eye_idle`, `eye_attack1`, `eye_attack2` and recovery.
The ordinary no-focus hit changes the same target from HP 135 to 125 and samples
the native `Damaged` trigger plus `eye_damage` recoil before recovery. One
guarded native Collect reaches strict Ready at level 0 room 2.

The separate `KillSingle` fixture changes HP 125 to 0 and issues native `Death`
at sample 23. Both custom renderers remain visible through sample 25, become
inactive at sample 26 and only then enter `eye_death` at sample 30.
`m_DoRagdoll` remains false with zero rigid bodies. This establishes the native
trigger, controller state, renderer removal and Victory handoff. It does not
establish ordinary lethal damage, visible custom death deformation, a corpse,
floor contact or corpse lifetime. Native effects, the foreground hero and UI
limit fine review, and the videos replay fixed-frame evidence rather than
real-time performance.
