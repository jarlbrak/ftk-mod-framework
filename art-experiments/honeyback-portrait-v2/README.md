# Honeyback Bear — portrait variant v2

A separate original-art variant addressing the tested Honeyback portrait's small/obscured eyes. The tested v1 generator, source, GLB, scenes and live evidence remain unchanged in `../honeyback-bear/`. This folder uses the new runtime filename `honeyback_portrait_v2.glb` and distinct profile key `ftkmf_modeltest_honeyback_portrait_v2`.

The exact native target remains `bearB`, `enBear01`, renderer 121467, the same combat profile, full 38-joint palette and inverse binds. No camera marker, runtime API or native animation bounds are changed. The original v1 palette is copied byte-identically as `honeyback_basecolor.png`.

Eight face pieces change: eyes and shadows move outward 0.088 mesh units per side; eyes grow 45% in width and 40% in height; shadows grow 22%/20%; brows move outward 0.065; upper muzzle narrows 12% and black nose 19%. Changes affect 1788 of 8352 original vertices. The body, limbs, ears, lower jaw and remaining positions are unchanged. Triangles, UVs, weights and joint indices/names are identical. Flat normals are recomputed for transformed faces. `geometry-change-audit.json` records exact piece ranges and source v1 hash.

`audit_portrait.py` resolves the actual head-attached native PortraitCam 90260 into bind space, accounting for serialized-pose versus bind-pose differences. It casts eye-center and eight half-radius XY sample rays against original Head_M-rigid surfaces, excluding the eye itself. V1 has 9/9 unobstructed samples for one eye and 0/9 for the other, which is blocked by muzzle/nose. V2 has 9/9 and 7/9; both centers are unobstructed. The remaining two samples are partial inner-eye occlusion. `portrait-visibility-audit.json` includes input hashes, camera matrix, sample results and explicit limitations.

This is an offline visibility guide, not native portrait acceptance. It excludes non-head-rigid geometry, pose-dependent jaw/body occlusion, actual offscreen FOV/crop and shader/light behavior. Native portrait animation and combat appearance remain live checks. Studio views show a wider-eyed bear while preserving the tested body silhouette.

Rebuild from repository root with existing local dependencies, the preserved original v1 source and native reference:

```sh
scratch/model-venv/bin/python art-experiments/honeyback-portrait-v2/build_geometry.py
scratch/model-venv/bin/python art-experiments/honeyback-portrait-v2/audit_portrait.py --assets '/absolute/path/to/resources.assets'
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/honeyback-portrait-v2/build_blender.py
scratch/model-venv/bin/python art-experiments/honeyback-portrait-v2/finalize_manifest.py
```

The editable source scene is `honeyback_portrait_v2.blend`; the studio scene is presentation-only. The Blender script saves, reopens and exports the individual scene, then independently validates it. Roundtrip outputs stay in ignored `scratch/honeyback-portrait-v2-roundtrip`. All vertices fit native bind bounds; this does not prove animated culling. Regeneration refreshes hashes and resets manual review. No proprietary surface, game deployment or catalog changes are included.

## Live revision: portrait still fails readability

[Live evidence](live-validation.json) pins revision assets and exact deployment
in session1ed421a57c3e483bafb6803d051d0ab1. Larger/moved eyes are visible in
reviewed combat0/50, but the native portrait remains nose-dominated. The offline
visibility improvement therefore does not establish live portrait acceptance.

- [Attack](live/attack.mp4), reviewed 0/50.
- [Normal 10 hit](live/nonlethal-hit.mp4),72 to62, reviewed 30 with hero occlusion.
- [Kill-fixture death](live/kill-fixture-death.mp4),62 to0, reviewed 40/60 coherent native animated collapse.

All three captures complete 120 unpaused frames; two Collect actions reach
Ready0/2. Source review shows combat HUD snapshots the dressed custom CEL
fresh; no shared cache cause is demonstrated. A separate row-based encounter
preview may miss the custom visual plan, but is not established as this combat
portrait's cause. Passive actual portrait camera/pose/mesh/texture telemetry
is pending. No fix or complete portrait approval is claimed; V1 stays archived.
Videos replay12fps, not real-time performance.

Passive native portrait tracing later confirmed the actual HUD clone used the same custom 8352-vertex mesh and texture (one complete record, zero telemetry errors). The actual camera/pose was captured, but frame0 still showed a nose-dominated portrait. This is diagnostic evidence, not a fix; an empty portraitNamedClips list does not prove pose sampling absent. Exact hashes and Ready/Trap1 aftermath are in the runtime index under native_portrait_trace_trials.

## Separate native camera acceptance

[EncounterCam trial](live-validation-encounter-camera.json) preserves the same v2GLB/PNG and uses the unique native `CameraRoot/EncounterCam` in a separate profile. Actual204x172 raw pixels and both HUD portraits are recognizable in root-reviewed combat frames0/40. The face is smaller; this is scoped camera acceptance, not a promise of large eye detail. The earlier default PortraitCam nose-dominance failure remains in its original archive.

The120-frame death capture in this camera case served cleanup only; previous body hit/death evidence remains separate. Two guarded Collect actions returned strict Ready0/3. Native encounter-row preview is not covered: its separate helper attempt rejected an incorrect204x172 dimension assumption before native Initialize, so no Core live PASS follows from that trial.

For the tested camera configuration, use [runtime-profile-encounter-camera.json](runtime-profile-encounter-camera.json). It retains the same v2GLB/PNG and the exact native CameraRoot/EncounterCam marker. The original runtime-profile.json remains preserved as the default PortraitCam configuration that failed portrait readability. The separate native encounter-row UI uses328×280; its earlier helper mistakenly required combat-portrait dimensions204×172 and rejected before invocation.

## V3 canonical exact bearB route

The [V3 canonical archive](live-validation-v3-canonical/README.md) repeats the
unchanged portrait-v2 GLB and texture through the current execution-queue
workflow for exact `bearB / enBear01 / 121467`. One owner binds the custom mesh
at native CEL scale 1.0 with `Root_M` and the expected 38-joint signature.

Three complete 120-frame captures retain `bear_idle`, native
`bear_attackBite`, an ordinary 72 to 64 `Damaged` response and recovery,
`bear_attackSwipe`, `bear_attackPound`, and explicit-fixture
`bear_deathHeavy`. Seventeen reviewed original PNGs show the dark-brown and
golden bear remaining coherent through each sampled motion and reaching a
readable prone pose. The exact renderer stays active, enabled, reported visible
and identity-stable in all 360 telemetry frames. It reports
`m_DoRagdoll=false` with zero rigidbodies, so the death is animated.

The ordinary hit used the unmodified native weapon without a skill or damage
fixture. The separate `KillSingle` reduced HP 64 to 0 for recorded native 1000
damage and is not ordinary lethal evidence. One guarded Collect reached strict
Ready at level 0 room 2. The loot panel later blocks most of the resting bear,
so exact collision, general corpse lifetime, cleanup and final disposal remain
unproven. Earlier PortraitCam failure and scoped EncounterCam portrait
acceptance remain separate records; V3 grants canonical full-body route credit
only.
