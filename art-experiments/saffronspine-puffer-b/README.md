# Saffronspine Puffer B - exact native binding variant

This reuses the original Saffronspine design on pufferB renderer121388, CEL139647 enPufferFishB, controller5999. Full30 inverse binds come from the separately extracted B reference. Native materials match A, but bind matrices and attachment targets are not byte-equal. B EncounterCam also differs; default Root_M/PortraitCam remains selected. No native geometry or attachment mesh is copied.

The B generator is a separately pinned copy with explicit B reference and unique output filenames. Artist coordinates and palette remain the same; landmark-driven vertex changes are under0.0000005 mesh units. Full B palette, independent direct and saved/reopened export, native bounds and closed-piece orientation, binding-only regeneration and all360-pose eye-tip checks pass. The eye-tip check excludes burial in the original body, not every camera occlusion or socket intersection.

Native B pass/hit/death sheets retain renderer-local travel. Renderer-disabled death frames are labeled, because this analytical geometry is not visible in the actual native death. The B source has distinct weapon/foot/head targets and EncounterCam; shared names/controller do not justify substituting A IBMs or inheriting A live claims. Future live validation must inspect original B material, portrait, inflation and native effects independently.

Run build_geometry.py, verify_original_geometry.py, audit_surfaces.py and audit_eye_seating.py using scratch/model-venv/bin/python. Run build_blender.py inside Blender to rebuild the editable scene and reopen/export checks. audit_native_poses.py accepts explicit B capture paths and six frame indices; pass --keep-root-motion for the published renderer-local sheets. Run finalize_manifest.py only before freezing a new candidate. Original A files are not modified.

Native B diagnostic evidence is in ../../docs/evidence/pufferb-diagnostic-v1/validation.json.

## Fresh live trial V2 (pufferB)

The [fresh live V2 archive](live-validation-v2/README.md) records one catalog-411 process with the exact `pufferB` chassis, `enBlowFishA` renderer 121388, owner 369188, native CEL scale 1.0, and an independent 30-bone bind signature. The authored `saffronspine_b.glb` stayed intact through the native pass and attack captures; the ordinary attack resolved HP 58→50 without cheat or focus. A separate `KillSingle` fixture captured the complete native `BlowFish_DeathDirect` interval, followed by two guarded native Collect votes and strict Ready at level 0 / room 2. The first Collect changed gold 11→41.

The B material was read back as `matLoot (Instance)` using the Standard shader with `ftkmf_saffronspine_b_basecolor.png`, native emission disabled, black emission color and no emission map. The archive keeps the independent B bind matrices, material readback, journals, complete 120-frame captures, selected originals and presentation videos. It does not transfer pufferA evidence. Native targeting/effects, hero/UI occlusion, victory depth blur and the small combat view limit fine surface, culling-envelope, portrait/resource lifetime, indirect-death and finished-art claims; ordinary lethal damage remains untested.
