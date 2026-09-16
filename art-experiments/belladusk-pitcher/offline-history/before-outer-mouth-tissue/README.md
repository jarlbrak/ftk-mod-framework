# Belladusk Pitcher - original bind-pose candidate

A curved dark-teal stalk rises from four broad root leaves into a plum pitcher hood, pale scalloped lip and warm amber throat. The open mouth and hood carry its identity, with no mammalian eyes. Broad leaves and restrained teeth should read at combat distance. This is an original visual replacement, with native mechanics and effects retained.

Exact source: plantE, CEL138985 enPlantE, renderer121530 enJungleNibbler_A, controller5980, native scale1.2. All37 bone names and inverse binds are retained. Eight native-unweighted palette entries remain unused: Bone018(mirrored), Bone019(mirrored), Vine2, Leaf1, Leaf2, Leaf3, Leaf4 and Leaf5. Do not turn these into invented animated branches merely because they exist in the palette.

The initial original surface uses a continuous segmented stalk, articulated root leaves, head hood, head-bound upper rim and jaw-bound lower rim. Separate lip corners overlap in bind pose; captured mouth-opening poses must establish whether this remains sufficient. No native vertices, triangles or texture are copied. The source-generation access proof permits only bone names and bind matrices. Direct/reopened export and positive-volume/native-bind-bounds checks are mechanical gates, not artistic or animation acceptance.

The single native matMasterJungle169 has emission; a future public runtime profile should explicitly disable native emission for the intended plum/teal palette. No profile or deployment is frozen yet. Motion fit, actual material readback, portraits, native death effects and lifecycle remain pending diagnostic evidence.

Reproduce with build_geometry.py, verify_original_geometry.py and audit_surfaces.py using scratch/model-venv/bin/python. Run build_blender.py using Blender --background --python to save the editable scene, independently reopen/export and render hero/side. This is a bind-pose art candidate rather than a finished live-tested model.

## First native motion fitting

The complete preliminary state is preserved in offline-history/before-mouth-fit. Recorded attack50 exposed a detached lower rim, and the initial amber throat read as a suspended bead. The original mouth now uses a recessed open funnel, with a small amber inset at its back. The lower lip blends from Head at both corners to Jaw at the center; the funnel boundary uses the same blend. The hood is set behind the recess. These are authored surface changes, with the exact native binding metadata unchanged.

All360 recorded plantE pass/hit/death poses were applied. Pass/hit sheets remove Root_M travel for anatomy; the death sheet retains renderer-local travel. The largest lower-rim edge stretches .046547 to.092882 (1.99545×) at pass50/hit85. The largest death edge is Leaf6 at40, .060640 to.128617 (2.12097×). The collapsed plant remains analytically connected in the selected sheet, but floor contact and camera/culling are not established. The mouth funnel is intentionally an open interior surface and is excluded from closed-volume orientation checks; each closed piece is tested separately. No new frozen manifest or staging yet.
