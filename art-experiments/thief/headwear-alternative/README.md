# Compact cap and lowered cowl comparison prototype

This original standalone prototype established the compact headwear direction in a native comparison. It preserves the first version, including the crown clearance issue observed in the side view. It is not the final package asset.

The selected implementation and crown correction now live in `../ranged-apparel/redesign.py`, and final native evidence is in `../redesign-review/README.md`. Production `thief-hood-*.glb` and matching display/icon files use that implementation. This prototype's static model and source exports are retained for comparison only.

Reproduce with `python3 art-experiments/thief/headwear-alternative/build.py` and `blender --background --python art-experiments/thief/headwear-alternative/render.py`. The exported geometry uses the existing original apparel palette and the observed native crown mount offset. Its standalone validation establishes source/accessor agreement and surface validity, not native fit.
