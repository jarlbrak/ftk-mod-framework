# Reefstrider - original fishA01 candidate

A turquoise fish biped with a cream belly, rounded fish muzzle, dark lateral eyes, deep-blue gill panels, restrained coral crown and broad articulated flipper hands/webbed feet. The design uses original closed low-poly surfaces rather than armor or human clothing. Native unarmed attacks, effects and ragdoll remain unchanged.

Exact fishA01 renderer121695 enFishA uses34 native palette entries and unarmed controller5958. The32 native-positive joints are weighted; MiddleFinger4_L/R remain unused but retained. Native scale1 is preserved. No fishA02/A03 controller inheritance is claimed. The source's matLoot emission should be explicitly disabled for the intended palette when a future profile is reviewed.

The generator reads only native bone names and bind matrices for landmarks; independent regeneration reproduces original source/palette/pieces exactly. Native geometry remains ignored. Direct/reopened exports, closed-piece positive volume and native bind bounds are checked. The initial intersecting belly badge is preserved in offline-history/initial-bind; the current cream belly is coloring on the continuous torso surface.

Completed native pass and dodged-attempt captures provide240 pose samples. The dodged attack is not hit evidence. Published sheets normalize Root_M travel for anatomy; no original live acceptance is inferred. Hit and11-body ragdoll fitting remain pending completed recordings. Head/crown, muzzle, shoulder and hip connections require particular attention under physics.

Run build_geometry.py, verify_original_geometry.py and audit_surfaces.py with scratch/model-venv/bin/python. Run build_blender.py with Blender --background --python for editable/reopened source and hero/side renders. audit_native_poses.py accepts explicit capture and six frame indices. No frozen manifest, runtime profile, catalog staging or deployment yet.
