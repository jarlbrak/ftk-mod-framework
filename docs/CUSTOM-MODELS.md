# Custom Enemy Models

Replace an enemy's appearance with original geometry while retaining its existing
skeleton, or supply a bespoke prefab. For repeatable authoring and live checks,
use [the authoring workflow](MODEL-AUTHORING.md) and
[the skeleton validation register](MODEL-SKELETONS.md).

Model assets are loaded locally rather than streamed through the network. Ship
identical assets with the mod on every client; matching assets alone do not
prove co-op compatibility of the surrounding enemy or adventure logic.

There are three paths. Pick based on the rig you have and whether you can run a Unity editor.

| Path | API | Use when | Needs Unity editor? | Reuses vanilla animations? |
|---|---|---|---|---|
| Runtime glTF (recommended, editor-free) | `Content.SetEnemyBodyMeshFromGlb` | You reskin an existing creature's rig, with NO Unity 2017 editor | No | Yes |
| Mesh swap (AssetBundle) | `Content.SetEnemyBodyMesh` | You reskin a rig and have the editor | Yes | Yes |
| Full prefab | `Content.SetEnemyBodyFromBundle` | You have a fully bespoke rig | Yes | No (you ship your own) |

Ship the asset at:

```
<game>/BepInEx/plugins/FTKModFramework_content/models/<file>      # .glb (Path A0) or .unity3d (Paths A/B)
```

(`FTKModFramework_content/` is the same folder the framework already uses for adventure art. The `models/`
subfolder is created/expected by the loader.)

---

## Path A0: Runtime glTF mesh swap (recommended, editor-free)

The mesh swap **without a Unity editor**. The framework parses a self-format `.glb` at runtime (a pure
net35/Mono glTF reader, `Core/RuntimeGltfMeshLoader`), builds the `Mesh` in C#, and swaps it onto the
vanilla body `SkinnedMeshRenderer` exactly like Path A, reusing the vanilla skeleton + animations. No
AssetBundle, no Unity 2017 editor, no license activation.

### The non-standard glb contract

This loader consumes a purpose-built `.glb`, NOT arbitrary glTF: no general-purpose exporter emits
this shape, so whatever tooling you use must write to the contract below. The `.glb`:

- stores vertex POSITIONs **already in Unity mesh-local space** (no axis/handedness conversion on read);
- is keyed to the live skeleton by **bone NAME**: each per-vertex `JOINTS_0` slot indexes `skins[0].joints`,
  and `nodes[joint].name` is the vanilla bone name. At spawn the loader reads the live `smr.bones[i].name`
  and remaps every influence onto the live bone index, so it is robust to runtime bone-order differences
  (getting that wrong is what renders an exploded mesh);
- carries the vanilla bind poses as `inverseBindMatrices` (the loader uses these, falling back to the live
  `sharedMesh.bindposes`); must be `< 65,535` verts (Unity 2017.2 has no 32-bit mesh index support).

### Author the mesh (editor-free pipeline)

The executable pipeline is available in
[`tools/ai-model-pipeline`](../tools/ai-model-pipeline/README.md).
It uses Blender and Python without a Unity editor. Extract the chosen chassis
into ignored local scratch, author original geometry in its native bind pose,
then export and validate against that reference. Keep presentation poses
separate from the mesh used with native inverse bind matrices.

The [authoring workflow](MODEL-AUTHORING.md) covers concept development, rigid
versus blended weights, coordinate conversion, UVs, renderer selection, and
live testing. The [skeleton register](MODEL-SKELETONS.md) distinguishes rigs
that have been discovered from those with actual model/animation evidence. For
exact multipart assignments and native death hand-offs, use the
[renderer API](MODEL-RENDERER-API.md): an opt-in `FallOffLimb` policy applies
only to an explicit leased renderer and needs a same-binary omitted-policy
control alongside the normal-death fixture.

### Wire it up

```csharp
// editor-free; ship mudwretch_rigged.glb + mudwretch_basecolor.png at FTKModFramework_content/models/
Content.SetEnemyBodyMeshFromGlb(enemy, "mudwretch_rigged.glb", "mudwretch_basecolor.png");
```

The texture is loaded from the `.png` via `Texture2D.LoadImage` and pushed into the body material's
`_MainTex` (with texture-driven emission to brighten dim dioramas).
Load failures are logged and may retain a fallback body. Inspect the loader log
for the intended mesh and zero unintended dropped joint slots; fallback rendering
is not a successful custom-model test. Existing `proceduralBody` settings can
hide the skinned renderer even after a successful swap.

> Current authored example: Mirewarden replaces the Flooded Crypt boss body
> when `FTK_MIREWARDEN_BODY=1` and `FTK_BASELINE_STOCK_BODY` is unset. Its mesh,
> idle articulation, portrait, and one normal combat exchange were observed
> in-game. Captured standard attack, hit, ragdoll, and completed combat now have
> evidence; other state variants and artistic refinement remain pending. The default
> sample configuration in this checkout still uses the procedural body.

---

## Path A: Mesh swap (recommended)

Reskin an existing creature by replacing only its body mesh, while reusing its skeleton, bind poses, and
**all of its vanilla animations**. This is the cheapest path to a new-looking enemy.

### Author the mesh

1. Model your new creature.
2. Skin it to the **exact** vanilla skeleton of the enemy you are reskinning:
   matching bone names, hierarchy, and bind poses. Extract the actual renderer's
   bone array; a shortened list of major joints is not sufficient.
   If your bone names or bind poses differ, the vanilla animation clips will deform your mesh wrongly.
3. Export the mesh.

### Build the AssetBundle

1. In Unity **2017.2.2p2** (the game's engine version), import your mesh.
2. Assign it to an AssetBundle (Inspector, bottom: AssetBundle dropdown).
3. Build the bundle (`BuildPipeline.BuildAssetBundles`), producing a `.unity3d` file.
4. Copy it to `FTKModFramework_content/models/<yourbundle>.unity3d`.

### Wire it up

```csharp
// enemy is the FTK_enemyCombat you registered via Content.AddEnemy(...)
Content.SetEnemyBodyMesh(enemy, "yourbundle.unity3d", "YourMeshName");

// optional: also push a Texture2D from the same bundle into the body material's _MainTex
Content.SetEnemyBodyMesh(enemy, "yourbundle.unity3d", "YourMeshName", "YourTextureName");
```

On each combat spawn, the framework finds the enemy body's `SkinnedMeshRenderer` (the `enTroll01` child,
or the first `SkinnedMeshRenderer` under the body clone) and sets its `sharedMesh` to your mesh. The
skeleton, bind poses, and animations are untouched, so the creature animates exactly as the original did.

If the bundle or mesh fails to load, it is logged and the original mesh (or the procedural golem, if you
also registered one) is left intact. The body never disappears.

---

## Path B: Full prefab (bespoke rig)

Use this when you have a fully bespoke rig of your own and do not want to reuse the vanilla skeleton.

### Author the prefab

The prefab MUST carry:

- a `CharacterEventListener` on the root (the spawn path requires one; without it the call is rejected),
- a `SkinnedMeshRenderer` (the body),
- an `Animator` (so the game's animation events drive it),
- `WEAPON_HOLDER_L` and `WEAPON_HOLDER_R` bones (so the held weapon mounts).

### Build the AssetBundle

Same as Path A: Unity **2017.2.2p2**, assign the prefab to a bundle, build the `.unity3d`, ship it at
`FTKModFramework_content/models/<yourbundle>.unity3d`.

### Wire it up

```csharp
Content.SetEnemyBodyFromBundle(enemy, "yourbundle.unity3d", "YourPrefabName");
```

The framework loads the prefab, Instantiates a private persistent copy (parked off-screen), verifies it
carries a `CharacterEventListener`, and repoints `FTK_enemyCombat.m_EnemyAsset` at it. The game then clones
that template per combat. If the prefab is missing or has no `CharacterEventListener`, it is logged and the
enemy's original body is left intact.

---

## AssetBundle requirements (Paths A and B)

- **Unity version:** the AssetBundle MUST be built with **Unity 2017.2.2p2**, the game's exact engine
  version. A bundle built with any other version will not load.
- **Shader:** ship a compatible shader or use **Standard**. A shader the game does not include resolves to
  magenta.
- **Determinism:** the bundle MUST be **byte-identical on every co-op client**. Ship it inside the mod.
  No per-machine paths, no streaming, no per-client downloads. A model is co-op/save safe only because it
  is identical everywhere and never networks or persists.

## Autonomous alternative

If you have no artist asset, the framework can build a runtime low-poly mossy bog-golem from primitive
meshes parented to the vanilla skeleton (`ProceduralCreature` / `BossProceduralBody` in
`EnemyVisualPatch`). It animates with the skeleton and is deterministic (index-hash derived, no `Random`).
Use it as a placeholder or for a stylized creature, then switch to a real mesh via Path A when ready.
