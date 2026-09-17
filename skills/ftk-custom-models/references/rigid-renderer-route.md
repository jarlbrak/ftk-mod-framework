## Rigid `MeshRenderer` child workflow

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

A selected target can be a rigid child rather than part of the skinned palette.
Before authoring, inventory the exact path and require exactly one
`MeshRenderer`, one `MeshFilter`, one native mesh, and one usable native material
slot, with no co-located `SkinnedMeshRenderer`. Multi-slot materials and native
`ScrollingUVs` are unsupported for a rigid assignment and must reject the
transaction rather than silently changing draw behavior.

Generate the read-only static inventory from the exact game asset pinned by the
enemy mapping, then pass it to the direct-profile preflight. The inventory must
name the same direct enemy/CEL root and child path; it records metadata only and
never decodes a native surface:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/inventory_static_renderers.py \
  --assets "$FTK_DATA/resources.assets" \
  --mapping scratch/enemy-rig-mapping-reproducible.json \
  --output scratch/static-renderer-inventory.json
python3 tools/ai-model-pipeline/validate_custom_model_profile_route.py \
  --static-inventory scratch/static-renderer-inventory.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
```

A static assignment needs `"rendererKind": "MeshRenderer"` and a linked
exact skinned assignment in the same direct profile. The skinned assignment
supplies the motion/controller contract; a static-only profile must reject.
[Abyssal Kraken V4](../../../art-experiments/abyssal-kraken/README.md) and its
[pinned preflight](../../../art-experiments/abyssal-kraken/route-preflight-v4-head-static.json)
are the reference combined head-plus-rigid-eye package.

Author that part entirely in the selected `MeshFilter` transform's local space.
Export it with the strict unskinned writer, without `--reference` or
`--rigid-bone`:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py \
  --static --source my-original-rigid-part.json --output my-rigid-part.glb
```

A static GLB has one triangle primitive, `POSITION` and indices, optional
normals/UVs, and no skin, `JOINTS_0`, `WEIGHTS_0`, bone palette, or inverse bind
matrices. Register it with `EnemyRendererMesh.ForStaticRenderer` or catalog
`"rendererKind": "MeshRenderer"`; never pass it through a skinned assignment.
The selected parent may animate the complete rigid part, but that does not give
the GLB a skin.

Do not inspect native static vertices, bounds, normals, UVs, texture pixels,
weights, or animation data to place or shape original art. Use only permitted
identity and transform metadata, material behavior, bone names/inverse binds
where applicable, your own authored landmarks, and observed live outcomes. If
placement is uncertain, first deploy an original low-poly marker probe that
maps visibility and occlusion. Preserve that diagnostic separately and do not
promote its result to art approval.

A rigid child cannot be the runner's selected skinned motion renderer. Capture
motion on a linked exact `SkinnedMeshRenderer`, then record the rigid child's
separate inventory: exact `MeshRenderer` kind, one live `MeshFilter`,
`ftkmf_static_glb_<file>`, private material/texture/emission state, owner, and
lifetime observations. Archive both claims together only after a fresh isolated
trial. Binding and sampled parent motion do not establish static-part art,
culling, all animation intervals, or final resource disposal.
