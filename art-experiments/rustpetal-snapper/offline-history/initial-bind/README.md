# Rustpetal Snapper - original plantD bind candidate

A broad copper seedpod mouth with two split upper leaf blades, a pale biting rim, deep-blue throat and paired olive side leaves. The flattened head and tall split foliage distinguish this candidate from Belladusk's round purple pitcher. Native behavior remains unchanged; the design adds no mechanics or copied native surface.

Exact plantD selection: renderer121537 enJungleNibbler_C, CEL138791 enPlantD,43-bone palette. Preserve native1.7 root scale through visual factor1. The30 native-positive bones are used;13 native-zero-weight bones are retained unused. In particular Leaf5 is positive here, unlike plantE. Additional crown bones and mirrored chains are not invented animation targets.

Belladusk's original surface functions are useful authoring code, but its37-bone asset is not a compatible replacement. This generator reads the separately verified D bind reference and exports all43 exact IBMs. The distinct wider head uses continuous Head/Jaw outer tissue and an intentionally open inner mouth funnel. Positive-volume checks apply only to closed pieces, not the open sleeve/interior. Native bind-bounds, source-only regeneration and direct/reopened binary checks pass for this initial candidate.

build_geometry.py creates original surfaces; verify_original_geometry.py restricts generator reads to bone names/bindposes. audit_surfaces.py checks closed-piece orientation and native bind bounds. build_blender.py creates the editable scene and independently reopens/exports before studio rendering. Run Python scripts using scratch/model-venv/bin/python and the Blender script using Blender --background --python.

This is an unfrozen bind-pose candidate. Full D motion fitting, native scale/camera fit, actual material/emission readback, portraits and lifecycle await the completed diagnostic. No D/G sharing claim, runtime profile, staging or deployment. Frozen Belladusk and Saffronspine assets are untouched.
