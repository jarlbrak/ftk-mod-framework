# Fitted original shield displays

The native Mercy shield card cropped its upper corners. Six dedicated display meshes
reuse the complete original shield geometry with positive uniform scale and translation
in the verified native `shieldBlacksmith01` display frame. Each fits inside the original
Novice shield X/Y bounds with a five-percent margin. Its native card previously fitted.
No native surface geometry is used. Equipped meshes and icons remain unchanged.

```sh
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/shield-display/build.py
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/shield-display/validate.py
```

The manifest records source hashes, transforms and bounds. Binary validation establishes
source equality, normals, UVs, indices and winding. Native card fit remains a separate gate.
