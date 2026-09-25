# Paired dagger redesign

The rejected equipment review hid most of each blade from the front. A native
side view exposed the cause: both rigid weapon mounts pointed their authored
positive Y direction forward from the fists. Increasing blade size alone made
the side silhouette resemble crossed shears.

The revised equipped exports apply `Z55 @ Y90` degrees around the original grip
center. Both hands now show the cutting faces from the front and the blades point
diagonally down and outward. This is an authored mesh transform; native weapon
holders, actions, and animation controllers remain game-owned. Item displays and
icons undo the equipped transform before arranging the pair. Break fragments are
converted from the revised equipped geometry into their exact child-local spaces.

Street Twins have broader clipped steel blades, slate teal grip repairs and brass
guards. The six progression pairs use their own blade shoulders, guard shapes,
pommels and palette accents. Ordinary cutting edges use muted silver. Skeleton
Key keeps a long stepped blade, a shorter pick blade and open key-bow pommels.
Candle's End uses dark leaf blades, pale cutting edges, distinct grip colors and
continuous amber inlays. The revised collars physically join blade, guard and grip.

The Street native male idle prototype was inspected in front, three-quarter,
side and back views. The final ordinary silver palette was then subdued slightly.
The parent fit review records native captures and later outfit/weapon combinations.
That review does not establish all body profiles, attack and hit motion, native
break behavior, bow draw behavior, or user art acceptance.

## Reproduction and evidence

Run the three generators with Blender, then their adjacent `validate.py` scripts:

- `build.py`: Street Twins, five rigid exports, palette and paired icon.
- `early-progression/build.py`: six pairs, 30 rigid exports, six palettes and icons.
- `artifacts/build.py`: two asymmetric pairs and the artifact bow, 18 rigid exports,
  shared palette and three icons.

Every output has editable geometry arrays and component maps. Independent
validators decode the actual GLB data and compare it with those arrays, verify
closed outward geometry, normals, UVs, fragment and display reconstruction,
icon alpha framing, and packaged asset hashes. The rendered previews use the
same validated geometry. All six progression previews and both revised artifact
pair previews were visually inspected. The Unlost Road bow keeps its original
geometry; only the shared brass palette changed in this pass.
