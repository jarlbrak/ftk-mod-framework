# Briarback Guardian

An original woodland stone guardian for the resource-only old Yeti rig: heavy stone limbs, bark seams, low moss mounds and amber eyes. The editable mesh and studio scenes contain original geometry and palette only. Authored-model live acceptance remains pending.

The exact target is renderer **120991**, CEL-relative path **Yeti**, resource prefab **enyeti**, with native combat row **yetiBoss**. This is distinct from modern `enYeti` renderer 121381. `runtime-profile.json` preserves the existing resource override contract and combat profile `3f19ee4c9ca672b41fd1a7ba596834d812454428f8bc942b64c051fa37745414`, with `minimumBaseHealth: 64`. Runtime files are `briarback.glb` and `briarback_basecolor.png`.

Native forward was verified before authoring: toe joints extend toward +UnityZ; the majority-weight Head_M surface spans Z -0.223 to +0.610. The authored face follows +Z (Blender -Y). The full 37-joint native palette and exact inverse binds are retained. The unused native Hair_M is preserved in the palette without adding invented weights. Original trunk rings follow consecutive spine joints, limb rings blend locally at elbows and knees, hands use native finger chains, and feet follow ankle/toe chains. Moss mounds follow head or shoulders; they do not bridge independently moving parts.

All authored vertices fit within the native mesh's bind-surface box. Native animation bounds remain unchanged; containment does not establish animated culling coverage. The new mesh has its own original topology and weights. Native surface vertices were inspected only for local orientation/bounds metadata and remain in ignored scratch.

`native-material-metadata.json` records the source asset hash and scalar/color properties. The source has one material, 261 `matYeti`, with white tint, `_EMISSION` keyword and zero emission RGB. The profile leaves `disableNativeEmission` false because the source does not show nonzero emission. Native material appearance and any runtime changes require live review; studio eye color is palette color, not a glow effect.

Rebuild from the repository root with existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/briarback-guardian/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/briarback-guardian/build_blender.py
scratch/model-venv/bin/python art-experiments/briarback-guardian/finalize_manifest.py
```

The Blender script creates the editable rigged scene, saves it, reopens the saved file, exports through the shared FTK bridge and independently validates the result. Reopened outputs remain in ignored `scratch/briarback-roundtrip`; their hashes are recorded in `manifest.json`. Export `briarback.blend`; `briarback-studio.blend` adds presentation-only floor, lights and cameras. The finalizer refreshes hashes and resets manual acceptance after regeneration. `inspect_materials.py --assets /absolute/path/to/resources.assets` reproduces the material metadata audit.

The earlier resource-Yeti calibration established a separate native baseline, including hit and ragdoll observations; its partial capture and cleanup do not validate this authored model. Pending checks are studio approval, exact resource override assignment, native-scale readability, idle/attacks/proficiency/hit/ragdoll, hand deformation, material appearance, culling and native cleanup. No game, helper or catalog deployment is performed by these scripts.

## First live run: body motion observed, portrait failure

[V1 evidence](live-validation-v1.json) preserves original resource Yeti120991
assignment under framework900f/helper7c, session16074358d6ce4f469d99e13bb3b58211.
Parent reviewed pass0/50, hit30 and death40/60: body remains readable and
articulated with coherent collapse. The portrait incorrectly shows two ivory
chest pads. Source landmarks distinguish the old fixed PortraitCam from the
head-attached EncounterCam; an explicit selector and corrected rerun are pending.

- [Attack](live-v1/attack.mp4),120 frames.
- [Ordinary hit](live-v1/nonlethal-hit.mp4),153 to148, normal 5 damage,120 frames.
- [Kill-fixture death](live-v1/kill-fixture-death.mp4),148 to0.

Death remains a failed95/120-frame capture ending RendererDestroyed, with
11 final native rigidbodies; no added frames or full-death claim. Automatic
strict Ready0/3 requires no Collect. Retained frames are unpaused and videos
replay12fps, not real-time performance. Portrait correction, broad culling
and complete art acceptance remain pending despite the scoped body results.

## Head portrait marker correction verified

[V2 archive](live-validation-v2.json) pins the corrected deployment and preserved
selection log. Turn-strip and enemy-health-panel portraits both frame the face
at reviewed 0/50 using exact Head_M/EncounterCam selection. Geometry is unchanged
from V1. Normal 5 damage 153 to148 and body collapse remain coherent in reviewed
views. Attack/hit complete 120 frames; death148 to0 still fails after95/120
frames with RendererDestroyed. Automatic Ready0/3 follows without Collect.
This fixes the selected UI portrait views, without claiming full death/culling
acceptance. The V1 chest-framing failure remains archived.

[Attack](live-v2/attack.mp4), [hit](live-v2/nonlethal-hit.mp4),
[partial death](live-v2/kill-fixture-death.mp4).
