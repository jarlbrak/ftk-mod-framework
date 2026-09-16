# Honeyback Bear

An original quadruped bear with a heavy dark-fur trunk, warm golden shoulder saddle, broad cream muzzle, rounded ears and small pale claws. The model has its own authored surface topology, local spine/limb blends and separate articulated lower jaw. Studio and authored-model live acceptance remain pending.

The catalog reconciles reference renderer **121467** to `bearB`, CEL-relative path **enBear01**, combat profile `a03f8882d4c799314d6fc4a476213e985e3b639399658e8c502737ff47cb609d`. `runtime-profile.json` uses exactly that assignment with `minimumBaseHealth: 64`. Runtime files are `honeyback.glb` and `honeyback_basecolor.png`. The source material is named `matPolarBear`; its name does not change the catalog's exact native enemy identity.

No indexed bear live baseline was located when authoring. The skeleton register lists the offline bearController representative, and the current catalog identifies the exact assignment. Native diagnostic validation remains a separate pending step; offline compatibility does not establish live support.

Native forward is +UnityZ, verified before authoring from head/jaw/toe chain positions and the rear tail at -Z. The complete 38-joint palette and exact inverse binds are preserved. Original torso rings follow consecutive spine joints, neck/head follow their actual joints, limbs blend at local knees, paws follow ankle/ball/toe chains, ears follow their native ear bones, and lower muzzle follows Jaw_M. The jaw does not add a full-weight link to the distant JawEnd joint. No native mesh, topology or texture is copied or packaged.

All original vertices fit within the native bind-surface box. An initial distal leg-ring radius exceeded the native minimum Y and was reduced before finalization. Production animation bounds remain unchanged; bind containment is not proof of animated culling. The golden shoulder marking colors the original trunk faces directly, avoiding a floating overlay. Organic overlaps still need native motion review for gaps and intersections.

`native-material-metadata.json` records the source asset hash and scalar/color properties. The single source material, 188 `matPolarBear`, has white tint, no emission keyword and zero emission RGB. The profile leaves `disableNativeEmission` false. Actual shader/tint appearance remains a live check. Refresh the audit with `inspect_materials.py --assets /absolute/path/to/resources.assets`.

Rebuild from repository root with existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/honeyback-bear/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/honeyback-bear/build_blender.py
scratch/model-venv/bin/python art-experiments/honeyback-bear/finalize_manifest.py
```

The Blender script creates and saves the editable rigged scene, reopens the saved file, exports through the FTK bridge and independently validates it. Reopened outputs remain in ignored `scratch/honeyback-roundtrip`; their hashes are recorded in the manifest. Export `honeyback.blend`; `honeyback-studio.blend` adds presentation-only floor, lights and cameras. Regeneration resets manual acceptance after refreshing checks and hashes.

Pending: studio approval, native Bear B diagnostic baseline, exact authored live assignment, native-scale readability, idle/attacks/proficiency/hit/death, jaw/limb deformation, material appearance, culling and cleanup. No game, framework, helper or catalog changes are performed by these scripts.

## First live run: body motion, portrait-readability failure

[V1 evidence](live-validation-v1.json) preserves original hashes and exact
deployment. Body/muzzle/limbs fit and move coherently in reviewed pass0/50,
hit30 and death40/60; hero obscures lower body during hit. The portrait remains
unclear. Later read-only camera/ray inspection supersedes the initial review's
crop hypothesis: native Head_M camera90260 views from below, upper muzzle/nose
blocks one eye center, and the other eye is small. No EncounterCam swap is
justified. Separate eye/muzzle art correction is pending, not yet fixed.

- [Attack](live-v1/attack.mp4)
- [Ordinary normal12 hit](live-v1/nonlethal-hit.mp4),72 to60.
- [Kill-fixture death](live-v1/kill-fixture-death.mp4),60 to0.

All three captures complete120 unpaused frames. Native splayed death has no
ragdoll flag or rigidbodies and no obvious new stretch in selected views. One
Collect reaches Ready0/3. Videos replay12fps, not real-time performance. Body
results do not establish portrait approval, unoccluded all-motion or culling.
The planned correction is a separate artifact; V1 failure remains preserved.
