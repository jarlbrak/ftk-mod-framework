# Saffronspine Puffer - original design and fresh live V2 trial

A stout saffron and ochre puffer with a dark umber dorsal saddle, cream belly, compact coral lips, small dark eyes and broad muted-blue fan fins. Short ivory dermal nubs follow the body surface. Broad color masses and a round silhouette carry recognition at combat distance; fine ornament is secondary. This is a visual replacement, with no new abilities, balance changes or replacement effects.

The exact target is pufferA, renderer 121509 at enBlowFishA, CEL139156 enPufferFishA and controller5999. Preserve native scale 1, all30 positive-weight palette entries and exact inverse binds. Native head/lips/eyes face+Z, tail points-Z. Side/Mid/Cheek names suggest surface zones but are not proof of an inflation motion envelope. Their nearest palette parents are all absent because the source Root_M is outside the skin palette; do not invent bone chains. The complete 55-transform source hierarchy stays available in ignored source findings.

Author a continuous softly weighted body with deliberate fin, lip and cheek transitions. Avoid long rigid segments across independently animated surface controls. Fit actual captured poses before choosing final nub positions and body volume. Keep the body inside native bind bounds; animation and camera fit require separate live validation.

Source matLoot1056 has white color and emission1.4RGB. The intended painted palette calls for explicit per-renderer disableNativeEmission:true, subject to live material review. Native Root_M/PortraitCam and CameraRoot/EncounterCam both exist; retain the normal portrait initially and compare actual pixels before any alternative marker selection. DeathDirect invokes conditional DeathFade while indirect death has no such event; fading is native behavior, not an authored corpse failure by itself.

`create_bind_setup.py` produces an empty exact rest-armature scene using the repository template, removing its original calibration mesh. It contains no copied native surface or finished creature. Full palette and inverse bind metadata are retained. Source findings/ref NPZ stay ignored. No runtime profile, finished-art acceptance or catalog staging yet.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/saffronspine-puffer/create_bind_setup.py
```

## Original body and reviewed offline fit

The finished offline candidate uses a continuous original ellipsoid surface with four-influence Side/Mid/Cheek weighting, closed blue fins and tail, articulated coral lips, short ivory nubs and ochre eye sockets. All30 palette bones receive positive weights. The exact native surface is never read by the generator: the binding-only regeneration proof reproduces source, palette and pieces byte-for-byte. Direct and saved/reopened export pass, all closed original pieces have positive signed volume, and the authored bind bounds fit the native bounds.

An early eye placement was swallowed by inflation at pass101. The corrected eye/glint positions move0.151 mesh units forward, with original socket volumes extending back into the body. The prior generator and reproduced failed sheet are explicitly labeled reconstructed evidence in offline-history. Both eye tips remain outside the closed original body in all360 recorded pass/hit/death poses; this parity check does not prove full camera visibility or intersection-free sockets. Root reviewed hero and corrected extreme, hit and death sheets and approved a live trial.

Worst measured body edge stretch is .186008 to.540122 (2.90376×), pass101 or hit82. Native deformation is substantial; selected views remain coherent but do not establish all-controller quality. All published sheets retain renderer-local travel. Native death first disables the renderer 27; the sheet labels later geometry as a disabled-renderer diagnostic, not an in-game visible corpse. Direct DeathFade is source-consistent but the actual CEL fade field was not captured. Indirect death, actual material properties, portrait fit and final resource teardown remain live gates.

Reproduce original geometry with build_geometry.py, verify_original_geometry.py and audit_surfaces.py using scratch/model-venv/bin/python. Run build_blender.py inside Blender for editable/reopened scenes and studio renders. audit_native_poses.py accepts --capture, --label, --steps (six frames), --keep-root-motion; all final sheets use the last flag. audit_eye_seating.py checks all recorded poses. Source references remain in ignored scratch; packaged geometry and palette are original.

Native diagnostic evidence: ../../docs/evidence/puffera-diagnostic-v1/validation.json. This candidate is separately staged as profile401 with native emission explicitly disabled.

## Fresh live trial V2 (pufferA)

The [fresh live V2 archive](live-validation-v2/README.md) records one isolated
catalog-411 process for the exact `pufferA` chassis, renderer `enBlowFishA`
(121509), controller 5999, native CEL scale 1.0 and the authored
`saffronspine.glb`. The 30-bone mesh bound under owner 369188 with a stable
observed signature, and the native Standard material read back the authored
basecolor with native emission disabled.

The process preserves native pass/attack behavior. A normal attack resolves
HP 58→48 without a cheat or focus and is kept separate from death acceptance.
The explicit `KillSingle` fixture supplies a complete 120-frame
`BlowFish_DeathDirect` capture; two guarded native Collect clicks (gold 11→25
on the first) and strict Ready at level 0 / room 2 then complete the native
loot boundary. Seven selected frames and three 120-frame videos are archived.

The trial establishes scoped binding, intact combat motion, material readback,
explicit-fixture death and native loot/Ready progression. It does not close
ordinary lethal damage, indirect death, portrait/resource lifetime, full
culling or finished-art acceptance. The independent archive validator and
overwrite refusal are part of the repeatable record.

## Canonical live V3

The [V3 archive](live-validation-v3/README.md) is the canonical exact-source
record for `pufferA / enBlowFishA / 121509`. One fresh catalog-411 process binds
the same authored 30-bone mesh at native CEL scale 1.0. All three captures
complete 120 frames: pass records settled idle and the native `Attack`
inflation cycle, an ordinary no-focus attack records HP 58 to 48 and native
`Damaged`, and the separate `KillSingle` fixture records native `Death` plus the
direct-death hide-and-effect handoff. One guarded native Collect reaches strict
Ready at level 0 room 2.

Eighteen exact original PNGs accept the sampled round silhouette, seated eyes
and lips, attached fins and dorsal nubs, large inflation, hit recovery, and
clean native death handoff. Impact flashes and the foreground hero obscure the
causal hit and death frames, and the renderer is hidden between reviewed death
frames 23 and 27. V3 therefore does not claim a visible articulated corpse,
ordinary lethal damage, indirect death, sibling puffer compatibility, complete
culling or lifetime coverage, or final art-direction approval.

Reproduce and verify the immutable archive with:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py art-experiments/saffronspine-puffer/live-validation-v3-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/saffronspine-puffer/live-validation-v3 --check-video-metadata
```

The first command intentionally refuses to overwrite the existing archive.
