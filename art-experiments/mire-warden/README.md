# Mirewarden: Blender art experiment

An original low-poly marsh guardian study, made directly with Blender Python.
This experiment tests visual quality before attempting FTK integration. It does
not replace or modify any installed game asset.

- `mirewarden.blend`: editable meshes, named materials, camera, and studio lights.
- `mirewarden-review.glb`: standard glTF review export, unrigged.
- `hero.png`, `front.png`, `back.png`: renders of the actual geometry.
- `review.png`: contact sheet including a small-scale readability check.
- `build.py`: reproducible source for the model and renders (Blender 5.2.1).
- `metrics.json`: model geometry counts, excluding the presentation stage.

Rebuild from the repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mire-warden/build.py
```

## Direction

An old marsh boundary stone that stood up: heavy hands, a small grumpy face,
large carved planes, sparse waymarker engraving, and an asymmetric moss mantle.
No downloaded game meshes or external generation services were used.

## Review and limitations

The model is a visual prototype. Proportions have not been fitted to a vanilla
skeleton. It is not rigged, animated, UV-baked, or verified in-game. The material
palette is represented by separate Blender materials. FTK's runtime path needs
a compatible texture/material strategy as well as its custom skeleton and GLB
contract. Do not install the review GLB as a game-ready asset.

Judge the silhouette, expression, moss shapes, and style at combat scale first.
If the direction is worth continuing, fit it to the chosen vanilla skeleton,
prepare a palette texture, export through the FTK-specific conversion pipeline,
and inspect deformation and lighting in actual combat.
