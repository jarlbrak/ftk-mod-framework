# Explicit enemy renderer assignments

Use `Content.SetEnemyBodyMeshesFromGlb` when a creature has several skinned renderers, a rigid `MeshRenderer` child, or the legacy body heuristic selects the wrong part. Each immutable `EnemyRendererMesh` describes one complete renderer replacement:

```csharp
Content.SetEnemyBodyMeshesFromGlb(enemy,
    new EnemyRendererMesh("Body/BodyMesh", "my_enemy_body.glb", "my_enemy.png"),
    new EnemyRendererMesh("Body/Wings", "my_enemy_wings.glb", "my_enemy.png"));
```

The example paths are illustrative. Inspect the actual spawned `CharacterEventListener` hierarchy. Paths use exact case-sensitive child names relative to its transform, excluding the root name. `"."` addresses a renderer on that root. A duplicate path, ambiguous hierarchy, wrong component type, multiple matching components, or a missing target rejects the whole assignment set. File paths must stay beneath `FTKModFramework_content/models/`.

Registration snapshots the array and reports only that the request was accepted. At spawn, all meshes, skeleton bindings, requested textures and replacement materials are prepared before renderer mutations. Each GLB must use unique bone names and its target renderer's bind matrices. Duplicate live bone names, missing weighted bones, malformed skin data or a missing requested texture reject the entire set. Zero-weight unused joint names may be absent from the live renderer. Bind matrices are compared by matching bone name with a tolerance of `0.0001 * max(1, abs(nativeEntry))` per matrix entry.

Each GLB has one primitive, so the replacement renderer receives exactly one private material cloned from the target's first native material slot. Additional native slots are not retained on the replacement, avoiding repeated draws of the same primitive. The original material array is preserved for rollback. A target without a usable first material rejects the set.

File-backed model PNGs are uploaded without retaining a CPU-readable pixel copy.
Treat these framework-owned textures as immutable: do not call pixel-read/write
APIs on them, destroy them, or assume each renderer has a distinct texture.
Explicit assignments reuse a PNG with the same resolved path across slots and
renderers in one transaction. Materials remain private, and separate assignment
transactions have independent textures. Legacy optional model PNGs also discard
their CPU pixel copies, but do not participate in transaction-local reuse.

## Rigid MeshRenderer assignments

Use `ForStaticRenderer` only for a native rigid child that has exactly one `MeshRenderer`, one `MeshFilter`, one native mesh, and one usable native material slot at the selected path:

```csharp
Content.SetEnemyBodyMeshesFromGlb(enemy,
    new EnemyRendererMesh("kraken2", "abyssal_head.glb", "abyssal_head.png", disableNativeEmission: true),
    EnemyRendererMesh.ForStaticRenderer(
        "Root_M/base/body/neck/eye/kraken2_eye",
        "abyssal_eye.glb",
        "abyssal_eye.png",
        disableNativeEmission: true));
```

The static GLB uses the rigid contract: one mesh, one triangle primitive, `POSITION` and indices, optional normals and UVs, and no skin, `JOINTS_0`, or `WEIGHTS_0`. Its positions are authored in the selected `MeshFilter` transform's local space. A parent bone or animated transform can move the whole rigid object, but the static GLB itself has no skinning palette. The loader names the mesh `ftkmf_static_glb_<file>` so live inventory can distinguish it from a skinned `ftkmf_glb_<file>` replacement.

Static assignments use the same private material cloning, PNG loading, tint preparation, and `disableNativeEmission` option as skinned assignments. They deliberately do not support `WithNativeMaterialSlots` or native `ScrollingUVs`; a static target with either a multi-slot material array or `ScrollingUVs` rejects the complete transaction. This avoids silently changing native draw or UV behavior. On failure, the original `MeshFilter.sharedMesh` and `MeshRenderer.sharedMaterials` are restored with every other assignment in the transaction.

The isolated test catalog records this target as `"rendererKind": "MeshRenderer"`. Its inventory must report that exact kind, one live `MeshFilter`, `ftkmf_static_glb_<file>`, and the private material state. Use a linked skinned renderer for motion capture. The inventory and lifetime records establish binding and ownership for the rigid child; they do not establish artistic acceptance or culling across every animation.

Native emission is preserved by default, including when a replacement PNG changes
`_MainTex`. For an albedo-only replacement, opt in per renderer:

```csharp
new EnemyRendererMesh("body", "body.glb", "body.png", disableNativeEmission: true)
```

The option disables `_EMISSION`, sets `_EmissionColor` to black and clears
`_EmissionMap` where those properties exist, only on the private material clone
prepared within the existing transaction. Other material settings and native
shared assets are unchanged. The original three-argument constructor remains
available and defaults to preserving emission. This is an assignment-time option;
subsequent native effects may still change material properties during combat.
Re-registering applies to future avatar creation, not already retained swaps.


The swap preserves the native renderer's animated local bounds. Mesh bind-pose bounds are not substituted for that envelope. A custom model extending beyond it requires measured live-animation bounds and a separate validated integration change; retaining the native envelope alone does not prove culling correctness for arbitrary art.

On preflight failure the spawned clone retains its original appearance. On an
exceptional application failure, the transaction attempts to restore original
meshes, bones, material arrays and local bounds. If restoration cannot be
proven complete, newly allocated meshes, textures and cloned materials remain
leased until the last lease owner is destroyed instead of risking destruction
of a still referenced object. Successful rollback releases unreferenced allocations.
Original game assets are never owned or destroyed by this path.

Explicit assignments take precedence over the legacy GLB/AssetBundle body selection and suppress the procedural body after success. They leave unlisted renderers and existing animation controllers in place. Other registered visual settings, such as tint, scale or weapon hiding, still apply after a successful swap. A full subsequent `SetEnemyVisual` registration replaces the visual configuration, including explicit assignments and any fall-off policy. Register visual settings first, explicit meshes second, and `SetEnemyFallOffPolicy` last.

## Opt-in native fall-off policy

Some native enemies replace their skinned body with rigid fragment renderers during a death animation through `FallOffLimb`. An explicit custom body can keep its renderer through that hand-off:

```csharp
Content.SetEnemyBodyMeshesFromGlb(enemy,
    new EnemyRendererMesh("enJungleSnakeC", "bramblecoil.glb", "bramblecoil.png"));
Content.SetEnemyFallOffPolicy(enemy, EnemyFallOffPolicy.PreserveCustomBody);
```

`PreserveNative` is the default. `PreserveCustomBody` is deliberately narrow: at spawn the framework attaches a CEL-local marker only after the explicit mesh transaction succeeds, its live lease is valid, and that lease owns the exact `FallOffLimb.m_Renderer`. The Harmony prefix then skips only `FallOffLimb.FallOff` for that marked renderer. Failed swaps, legacy singular body assignments, material-only changes, an unowned renderer, and all profiles that omit the policy continue through native fall-off unchanged.

The policy does not invent a custom ragdoll or fragment set. Native `CharacterEventListener` cleanup after the fall-off callback, including lights, arrows, detachables, and ordinary native ragdoll handling, remains in place. Validate a normal native death and a no-policy control separately before treating a model as death-compatible. [Bramblecoil Viper's scoped archive](../art-experiments/bramblecoil-jungle-snake/live-validation-v1/README.md) is the reference exercise: it pins a semantic `DeathLight` trigger, paired same-binary normal-death fixtures, and a separate native Collect-to-Ready result.

## Public visual scale is a factor

With the corrected `EnemyVisualScale` implementation, public
`Content.SetEnemyVisual(enemy, Color.white, factor)` multiplies the spawned CEL's
captured native local scale on each axis. Factor1 preserves that scale, including
non-uniform axes; it does not replace it with `(1,1,1)`. The component captures
the native baseline once per CEL, so repeated application recomputes from that
baseline instead of compounding the factor. Native prefab assets remain unchanged.
Register the tint/scale first, then explicit GLB assignments so the mesh registry
merges them into the visual settings.

For the fitted old cockatrice example, native root scale `(0.9,0.9,0.9)` times
factor 0.55 predicts spawned local scale `(0.495,0.495,0.495)`. This is a source
contract calculation, **pending live measurement and visual fitting review**.
Record requested factor, native prefab root local scale, and observed spawned CEL
local scale separately. World size also depends on parent transforms, original
mesh dimensions and animation; a matching local scale does not establish camera
fit, ground contact or culling acceptance. Older builds that replaced native
scale with absolute values do not satisfy this contract.


`Content.SetEnemyBodyMeshFromGlb(enemy, file, texture)` keeps its existing heuristic and permissive fallback behavior. Use the explicit API for strict binding and multipart models. A skinned explicit file uses the FTK skin contract: one mesh primitive, one skin, mesh-local positions, top-origin texture V, and bone-name keyed joints. A `ForStaticRenderer` file uses the rigid contract above. This API does not retarget animations or convert source geometry.

Require the log line `[enemy-visual] explicit mesh swap applied N renderers` for the expected enemy and inspect the actual combat model. A registration success, a fallback body, or an offline export check is not a successful live mesh replacement.

## Explicit portrait marker

A custom enemy can select a native child marker for portrait capture without
changing its body, skeleton placement or combat camera:

```csharp
Content.SetEnemyPortraitMarker(enemy,
    "Root_M/BackA_M/BackB_M/Chest_M/Neck_M/Head_M/EncounterCam");
```

This example targets the old Yeti's head-attached marker. Its fixed native
`CameraRoot/PortraitCam` frames the chest. Registration requires the exact custom
DB row, a proper child path, and a unique marker leaf name. The same checks run
against each actual offscreen portrait clone. Missing or ambiguous markers retain
both native camera arguments; an invalid registration preserves the prior one.
The default remains unchanged, and only requests whose primary marker is
`PortraitCam` are eligible. Native positioning, rendering and cleanup are reused.
No source transform is renamed, moved, reparented or removed by this option.

The row snapshot overload carries explicit row identity through its nested avatar
snapshot. A live-avatar snapshot requires the exact EnemyDummy/avatar/registered
row pairing. The serialized `CEL.m_EnemyID` is not used to infer identity. Capture
scopes are restored even on exceptions; unrelated nested snapshots cannot inherit
another enemy's choice. Player and other camera requests retain native behavior.
Existing portrait textures are not refreshed by registration. Verify newly
captured turn-order and enemy-panel portraits separately; registration and source
marker validation do not prove final framing or custom mesh visibility.

## Explicit native material slots

For a renderer whose native behavior depends on multiple material slots, use a real two-to-four-primitive GLB and explicit slot descriptors. Existing `EnemyRendererMesh` constructors still select one primitive and one material. The named factory avoids ambiguous legacy null-texture constructor calls:

```csharp
EnemyRendererMesh.WithNativeMaterialSlots("enJellyCube", "original_cube.glb", new[]
{
    new EnemyRendererMaterial(0, 0, "original_interior.png", false),
    new EnemyRendererMaterial(1, 1, "original_shell.png", true)
})
```

Each descriptor specifies primitive index, native material slot, optional authored PNG, and emission opt-out. Both index sets must cover0..N−1 exactly once, and N must equal the native renderer's material count. The transaction clones the corresponding native materials, applies options per slot, and assigns exactly N submeshes/materials. There is no repeated-last-submesh drawing, implicit slot collapsing, source-prefab mutation, or partial renderer-set commit. The immutable descriptor array is copied at construction and when read publicly. All primitives share one complete exact skin and attribute accessor set; each owns a distinct nonempty uint16 triangle accessor. GLB material IDs equal primitive order; the public descriptors explicitly map that order to native slots.

CubeA's native ScrollingUVs materialIndex1 requires the two-slot form. On a successful opted-in renderer, an exact compatibility prefix retains the native component, existing private uvOffset, rate, property and index. It accumulates `uvAnimationRate * Time.deltaTime` on every native component invocation even while Renderer.enabled is false, then writes only when enabled. It uses tracked private sharedMaterials rather than invoking `.materials` and creating untracked copies. Unsupported/nonowned components execute their original method; failed transactions remove the opt-in. An unexpected changed material set is not adopted and is logged before returning to native behavior without first advancing phase.

Cloned avatars initially retain the shared resource lease; the first enabled scrolling write privately clones their verified assigned material set once. All allocations join the lease before assignment, with rollback on failure. The source and clone therefore do not cross-write UV phase. Disabled renderer phase accumulation does not force allocation. Newly allocated per-owner materials remain owned until the last lease owner releases; this is bounded shared-lease lifetime, not immediate per-owner disposal. Never-active owner pruning remains necessary. The canonical [Mossglass Cube A archive](../art-experiments/mossglass-reliquary/live-validation-v5/README.md) records the exact two-slot assignment, per-frame scroll readback, native motion, ordinary damage, death fixture, and Ready progression. Its stated limits still apply; one live route does not prove every multi-slot renderer or lifecycle order.
