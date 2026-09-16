# Belladusk Pitcher - original bind-pose candidate

A curved dark-teal stalk rises from four broad root leaves into a plum pitcher hood, pale scalloped lip and warm amber throat. The open mouth and hood carry its identity, with no mammalian eyes. Broad leaves and restrained teeth should read at combat distance. This is an original visual replacement, with native mechanics and effects retained.

Exact source: plantE, CEL138985 enPlantE, renderer121530 enJungleNibbler_A, controller5980, native scale1.2. All37 bone names and inverse binds are retained. Eight native-unweighted palette entries remain unused: Bone018(mirrored), Bone019(mirrored), Vine2, Leaf1, Leaf2, Leaf3, Leaf4 and Leaf5. Do not turn these into invented animated branches merely because they exist in the palette.

The initial original surface uses a continuous segmented stalk, articulated root leaves, head hood, head-bound upper rim and jaw-bound lower rim. Separate lip corners overlap in bind pose; captured mouth-opening poses must establish whether this remains sufficient. No native vertices, triangles or texture are copied. The source-generation access proof permits only bone names and bind matrices. Direct/reopened export and positive-volume/native-bind-bounds checks are mechanical gates, not artistic or animation acceptance.

The single native matMasterJungle169 has emission; a future public runtime profile should explicitly disable native emission for the intended plum/teal palette. No profile or deployment is frozen yet. Motion fit, actual material readback, portraits, native death effects and lifecycle remain pending diagnostic evidence.

Reproduce with build_geometry.py, verify_original_geometry.py and audit_surfaces.py using scratch/model-venv/bin/python. Run build_blender.py using Blender --background --python to save the editable scene, independently reopen/export and render hero/side. This is a bind-pose art candidate rather than a finished live-tested model.
