# Belladusk Pitcher - original bind-pose candidate

A curved dark-teal stalk rises from four broad root leaves into a plum pitcher hood, pale scalloped lip and warm amber throat. The open mouth and hood carry its identity, with no mammalian eyes. Broad leaves and restrained teeth should read at combat distance. This is an original visual replacement, with native mechanics and effects retained.

Exact source: plantE, CEL138985 enPlantE, renderer121530 enJungleNibbler_A, controller5980, native scale1.2. All37 bone names and inverse binds are retained. Eight native-unweighted palette entries remain unused: Bone018(mirrored), Bone019(mirrored), Vine2, Leaf1, Leaf2, Leaf3, Leaf4 and Leaf5. Do not turn these into invented animated branches merely because they exist in the palette.

The initial original surface uses a continuous segmented stalk, articulated root leaves, head hood, head-bound upper rim and jaw-bound lower rim. Separate lip corners overlap in bind pose; captured mouth-opening poses must establish whether this remains sufficient. No native vertices, triangles or texture are copied. The source-generation access proof permits only bone names and bind matrices. Direct/reopened export and positive-volume/native-bind-bounds checks are mechanical gates, not artistic or animation acceptance.

The single native matMasterJungle169 has emission, so the public runtime profile explicitly disables native emission for the intended plum/teal palette. The canonical live run reads back `matMasterJungle (Instance)` on Standard with the authored base-color texture, emission disabled, black emission color and no emission map. Portrait behavior and final resource disposal remain separate gates.

Reproduce with build_geometry.py, verify_original_geometry.py and audit_surfaces.py using scratch/model-venv/bin/python. Run build_blender.py using Blender --background --python to save the editable scene, independently reopen/export and render hero/side. The canonical V3 archive below adds exact-source live binding, motion, gameplay and selected-pixel evidence to these offline authoring checks.

## First native motion fitting

The complete preliminary state is preserved in offline-history/before-mouth-fit. Recorded attack50 exposed a detached lower rim, and the initial amber throat read as a suspended bead. The original mouth now uses a recessed open funnel, with a small amber inset at its back. The lower lip blends from Head at both corners to Jaw at the center; the funnel boundary uses the same blend. The hood is set behind the recess. These are authored surface changes, with the exact native binding metadata unchanged.

All360 recorded plantE pass/hit/death poses were applied. Pass/hit sheets remove Root_M travel for anatomy; the death sheet retains renderer-local travel. The largest lower-rim edge stretches .046547 to.092882 (1.99545×) at pass50/hit85. The largest death edge is Leaf6 at40, .060640 to.128617 (2.12097×). The collapsed plant remains analytically connected in the selected sheet, but floor contact and camera/culling are not established. The mouth funnel is intentionally an open interior surface and is excluded from closed-volume orientation checks; each closed piece is tested separately. This historical first-fit review precedes the final outer-tissue correction below.

## Outer mouth tissue correction

The previous funnel-only version is preserved intact in offline-history/before-outer-mouth-tissue. Its hit27/29 opening read as a detached rim. A new purple outer sleeve now spans embedded head tissue, an intermediate Head/Jaw blend, and the same front-lip weights, with an annular tissue edge. Native jaw opening is retained. The96 outer side triangles independently face the intended radial exterior in bind. The sleeve and inner funnel remain deliberately open surfaces; closed-volume checks apply only to the other closed pieces.

All360 poses were recomputed against the revised GLB. native-hit-mouth-pose-study.png isolates0/27/28/29/30/31 using per-frame head framing to inspect the continuous purple connection; it does not depict travel or camera fit. The full-body pass/hit and renderer-local death sheets remain separate. Direct and reopened exports, original provenance and native bind bounds pass. Root reviewed the corrected closeups for offline packaging, then separately reviewed exact original game frames in V3.

## Final offline package

manifest.json freezes the reviewed source, editable/reopened scenes, palette, all current pose audits, both original mouth-failure histories and the scoped live evidence. runtime-profile.json targets exact plantE with native1.2 scale preserved by visualScale1 and explicit native emission opt-out. The initial bind-candidate manifest describes its preserved historical version, not the final model.

## Canonical exact-source live validation

The canonical [V3 archive](live-validation-v3/README.md) preserves the complete V2 session `8e7350b348ba499993a11279be206fb2` after proving that its selected historical profile is identical to the current catalog profile. The Belladusk mesh remained bound to the exact `plantE` / `enJungleNibbler_A` renderer 121530 on one native enemy owner for all 360 retained frames. The profile, authored GLB, texture, source renderer and motion renderer are unchanged.

The three complete captures record settled `idle`, two native `attack1` intervals, ordinary HP 58 to 50 with `hit1`, recovery, and explicit-fixture `death`. One guarded native Collect reaches strict Ready at level 0 room 2. The exact renderer reports `m_DoRagdoll=false` and no rigidbodies throughout, so death is animated rather than a body ragdoll. Fixture death is not ordinary lethal damage.

V3 corrects a V2 review error that labeled two early idle frames as attack views. Its six reviewed originals come from the exact raw `idle`, `attack1`, `hit1` and `death` intervals. The archive independently passes integrity verification with 28 lossless metadata mappings, all 360 source-image and capture-image pins, two asset pins and three checked 120-frame videos. Full culling coverage, collision, physics sleeping, corpse lifetime, portraits, final resource disposal and finished-art acceptance remain separate checks. V1 and V2 remain preserved as historical evidence.
