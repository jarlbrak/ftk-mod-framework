# Original Paladin artifact equipment

Three original rigid equipment models implement the approved visual concepts:

- **The Last Vigil**, a one-handed ivory reliquary hammer with silver striking caps, gold protective framing, navy grip, shield pommel and sapphire ward.
- **Kingsfall**, a two-handed charcoal hammer with broad lateral striking faces, a broken gold crown and royal seal, restrained amber seam and long burgundy grip.
- **The Last Bastion**, a tall battlement kite shield with silver rim, ivory planes, navy field, gold sanctuary arch and sapphire ward. Modeled rear anchors, bracing and hand grip complete the shield's back.

The designs were art-directed with Astra High. Imagegen previews supplied visual direction only. This generator creates the actual original geometry; no game mesh, texture, surface bounds, UVs, normal, weights or animation data is read or copied.

## Reproduce

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/paladin-legendaries/build.py
python3 art-experiments/paladin-legendaries/validate.py
```

For a byte reproducibility check, preserve `manifest.json` outside the campaign,
run the build again, then pass its previous path to the validator with
`--compare-manifest <previous-manifest.json>`. The resulting
`reproducibility.json` pins the generator and comparison count. Icon export
removes Blender's ancillary Date and RenderTime text metadata without changing
encoded pixels or color information.

The generator reuses the established Paladin original-geometry primitives, orientation-preserving Unity coordinate export, static FTK GLB writer and icon renderer. Each primary asset has editable `.source.json` geometry and a named `.pieces.json` map. The complete editable Blender studio is regenerated locally as `paladin-legendaries.blend`; it is not required to reproduce the files.

`manifest.json` pins the generator and its dependencies, source/GLB/piece files, original palette, icons and exact native renderer path mappings. It also records display transforms. Only this campaign's GLBs, palette and icons are copied into the Paladin package; its additive entries in `paladin-assets.provenance.json` identify the original campaign files. Existing package asset entries remain unchanged.

## Exact route and coordinate scope

Equipped original-art stations remain Y-up with the one-handed head at 0.88 and the two-handed head at 1.5, as in the existing Paladin equipment sources. Shield geometry is centered around the existing authored grip origin. Native game surface data was never used for these proportions.

The whole SmithHammer and WarHammer renderers target `.`. Last Vigil has two original break meshes targeting `Break` and `Break/Break`. Kingsfall has three targeting `break`, `break/break2` and `break/break1`. Whole authored pieces partition the primary source triangles exactly once and retain their original coordinates. This preserves the existing package's mapping convention; new live break detach remains a separate gate.

All three have separate `-display.glb` variants. The only native inputs are existing transform-only attachment metadata in `display-route-bindings.json`: `smithhammer`, `hammer`, and `shieldBlacksmith01`. Fit targets are frozen, previously authored original-art card envelopes. No native surface bounds are used. Display scale and translation are independent of equipped geometry.

The separate `paladin-legendary-palette.png` has fourteen original horizontal swatches. Flat normals and constant-per-piece UVs preserve large readable color fields; V is centered at 0.5 so the required loader V flip leaves the palette unchanged. The core contract has one static triangle primitive and no skin or joints.

## Verification and acceptance

The independent validator decodes all eleven GLBs, compares exported arrays to editable sources, checks finite arrays and indices, unit normals, UV ranges, triangle-normal agreement, positive signed volume and closed manifold edges per original component, exact-once fragment triangle coverage, package/provenance hashes, and transparent 256-square icon framing.

`actual-mesh-board.png` is a render of the exported source geometry, not the imagegen concept. It and all three icons were visually inspected for recognizable silhouettes and palette placement. Intersecting connected components are intentional; the assets do not claim a manifold Boolean union.

Offline export and art review do not establish native equipped fit, shield occlusion during Guard, attack poses, break behavior, loot-card rendering, per-race grip coverage, or lifecycle behavior. Those gates require fresh pinned live evidence for these exact model hashes. Runtime/content integration and live testing are handled separately.
