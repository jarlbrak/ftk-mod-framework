# AI Model Pipeline: text -> 3D -> Blender -> game-ready mesh

How to use AI to generate a custom enemy model for For The King, end to end. The
**entire pipeline is now autonomous on this Mac** (Apple M5, 32 GB, Blender 5.1,
no API key, **no Unity editor**). The former hard blocker (a Unity 2017.2.2p2
editor to build the AssetBundle) is removed by a **runtime net35 glTF loader**
(`Core/RuntimeGltfMeshLoader`) that parses a `.glb` and builds the skinned `Mesh`
in C# at spawn. See [`CUSTOM-MODELS.md`](CUSTOM-MODELS.md) Path A0.

## The proven pipeline (what actually ran)

```
nanobanana / TRELLIS.2 (text/img -> textured 3D mesh, local, free)
   -> Blender  (import, decimate to a Unity-1.0 vert budget, export GLB)
   -> 04b_extract_troll_skinned.py  (UnityPy: pull the vanilla enTroll01 skinned
                                      mesh + 37-bone skeleton + bind poses)
   -> 05b_rig_numpy.py  (numpy, ALL in Unity coords: align into troll mesh-local
                         space, nearest-surface weight transfer, weld+smooth,
                         optional --posearms/--rigid, write a name-keyed .glb)
   -> Content.SetEnemyBodyMeshFromGlb  (runtime glTF loader; NO Unity editor)
```

Result: `ai-model-gen/mudwretch_rigged.glb` + `mudwretch_basecolor.png`, a
faceted mossy bog-golem (the "Mudwretch Foreman") rigged to the cave-troll
skeleton, rendered in-game on the Flooded Crypt boss and verified by screenshot.

### Stage 1 - concept image (nanobanana)
A front-facing, A-pose, plain-background, low-poly faceted character on a flat grey
backdrop is the ideal image-to-3D input. The exact prompt used is in the git
history; regenerate variants by re-running the nanobanana image tool.

### Stage 2 - image -> 3D (TripoSR, free)
`tools/ai-model-pipeline/01_generate_mesh.py` calls the public `stabilityai/TripoSR`
Hugging Face Space via `gradio_client` (anonymous, no token): `/preprocess` (remove
background) then `/generate` (marching-cubes mesh) -> `.obj` + `.glb`. Output is
high-poly (~90k faces) and vertex-coloured.

```bash
uv venv .venv-3dgen --python 3.12
uv pip install --python .venv-3dgen/bin/python gradio_client trimesh pillow
.venv-3dgen/bin/python tools/ai-model-pipeline/01_generate_mesh.py
```

TripoSR is the fast/rough/free tier. For higher fidelity swap stage 2 for: a paid
**Tripo** or **Meshy** API (best clean low-poly + auto-rig), or **blender-mcp +
Rodin** (drive Blender + generate conversationally; free `vibecoding` Rodin trial
key). Same downstream stages.

### Stage 2b - TRELLIS.2 local (PROVEN, much higher quality, free)
Microsoft's TRELLIS.2 (4B params) via the Apple-Silicon MPS port
[shivampkumar/trellis-mac](https://github.com/shivampkumar/trellis-mac) runs
locally on this M5 (32 GB; ~18 GB peak) and produces a **textured** mesh (baked
1024 PBR base-color), a large quality jump over TripoSR. Verified working here.

```bash
git clone https://github.com/shivampkumar/trellis-mac.git tools/trellis-mac
SKIP_METAL=1 bash tools/trellis-mac/setup.sh          # uv venv (py3.11) + torch + microsoft/TRELLIS.2 + MPS patches
# one-time: accept the two GATED HF models, then `hf auth login`:
#   https://huggingface.co/facebook/dinov3-vitl16-pretrain-lvd1689m   (image encoder, essential)
#   https://huggingface.co/briaai/RMBG-2.0                            (background remover)
cd tools/trellis-mac
.venv/bin/python generate.py <concept.png> --output mudwretch_trellis --pipeline-type 512 --texture-size 1024
#   -> mudwretch_trellis.glb (~200k faces, textured) + .obj (1M faces) + _basecolor.png ; ~5 min compute
```

Notes: `SKIP_METAL=1` because no full Xcode (pure-Python texture baker; slightly
softer). `dinov3` is gated and essential (no bypass); RMBG is bypassable since our
concept image already has a clean background. The mesh exports with a non-standard
up-axis (stands when viewed down -Z) -> fix the orientation on import in Blender/Unity.
**Decimation caveat:** TRELLIS surfaces are dense/noisy, so an aggressive COLLAPSE
(200k -> ~5k) spikes; decimate gentler (~20-40k for a single boss, the baked texture
carries the detail) or planar-decimate + light smoothing.

### Stage 3 - Blender to game-ready (proven)
`tools/ai-model-pipeline/02_blender_lowpoly_export.py` (run headless):

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python tools/ai-model-pipeline/02_blender_lowpoly_export.py
```

Imports the GLB, joins, **Decimate (COLLAPSE)** to a ~4.5k-face budget,
**flat-shades** for the FTK facet look, and exports `mudwretch_lowpoly.fbx`
(Unity-ready: `mesh_smooth_type='FACE'`, `add_leaf_bones=False`,
`apply_scale_options='FBX_SCALE_ALL'`) + `.glb`. `03_render_preview.py` renders
Workbench turntable PNGs for review.

## Stage 4 - rig + load (editor-free, autonomous)

The Unity-editor AssetBundle step is no longer required. Two scripts plus a
runtime loader replace it:

### 4a - extract the vanilla rig (`04b_extract_troll_skinned.py`, UnityPy)
Reads `resources.assets` and pulls the cave-troll body mesh `enTroll01`
(path_id 3190): decodes the skinned vertex data (positions, normals, uv0,
bone indices, bone weights) via `MeshHelper.MeshHandler`, plus the 37 bind-pose
matrices and the `SkinnedMeshRenderer.m_Bones` NAME order (the authoritative bone
order; do not assume the cached order). Dumps `troll_skinned.npz` + `troll_skel.json`.
A decode quirk: ~18% of troll verts have a corrupted 4th bone-weight (a large
negative int) while the first 3 weights + all bone indices are valid; clamp
negatives to 0 and renormalize.

### 4b - rig the AI mesh (`05b_rig_numpy.py`, numpy, ALL in Unity coords)
No Blender coordinate round-trips: everything is plain numpy in the troll's
Unity mesh-local space, so the AI vertices and the reused troll bind poses share
one frame by construction. Steps: fix the TRELLIS up-axis (a +/-90 rotation about
X; the feet end is detected by leg-bimodality of the bottom slice), bbox-align the
AI mesh into the troll envelope, transfer skin weights from the vanilla troll by
nearest surface (scipy cKDTree, k-NN blended), weld coincident verts + smooth
normals, and write a name-keyed `.glb` (per-vertex JOINTS index `skins[0].joints`,
whose node names are the 37 troll bones, + the troll bind poses as
`inverseBindMatrices`).

Two flags matter when the AI proportions do not match the troll skeleton (a squat
golem vs a lanky troll): the troll animation can fling mis-placed verts 2-3 units
out (a spiky look), so `--rigid <bone>` weights everything to one bone (a stable
prop that cannot deform-spike, ideal for a stone golem), and `--posearms <deg>`
rotates the T-pose arms down to the sides for a natural stance. Verify the rig
offline (rest + a synthetic pose) before shipping; a software rasterizer of the
`.glb` faces confirms the silhouette without a Unity editor.

### 4c - load it at runtime (`Core/RuntimeGltfMeshLoader`, net35)
A pure-managed glTF reader (hand-rolled JSON + binary buffer; no `System.Text.Json`)
builds the `Mesh` in C# at spawn: vertices/uv (V-flipped)/triangles, `boneWeights`
remapped from the glb's bone NAMES onto the live `smr.bones` index order, and
`bindposes` from the glb's `inverseBindMatrices` (falling back to the live
`sharedMesh.bindposes`). It is fed into the EXISTING mesh-swap (`smr.sharedMesh = mesh`),
so the vanilla skeleton + Animator drive it. `< 65,535` verts (no 32-bit index
support in 2017.2). Never throws: any miss logs and keeps the original body.

```csharp
Content.SetEnemyBodyMeshFromGlb(boss, "mudwretch_rigged.glb", "mudwretch_basecolor.png");
```

The body material is the vanilla **Standard** shader, so there is no magenta-shader
risk (the texture is pushed into `_MainTex`, plus a subtle `_EmissionColor` so the
mesh reads in the Flooded Crypt's dim cool light). Ship the `.glb` + `.png` at
`<plugin>/FTKModFramework_content/models/`.

### Optional fallback - AssetBundle (needs the Unity 2017 editor)
The original AssetBundle path still works if you prefer it (or need a bespoke rig):
build with **exactly Unity 2017.2.2p2** (Intel-only; Rosetta 2 on M-series, crash-prone;
a Windows x64 box is more reliable), embed a self-contained shader, and call
`Content.SetEnemyBodyMesh` / `SetEnemyBodyFromBundle`. See `CUSTOM-MODELS.md` Paths A/B.

## Determinism
The `.glb` (or bundle) is shipped inside the mod, byte-identical on every co-op
client, and the mesh/material never network or persist (only the enum-int enemy
identity is shared state). The runtime loader adds NO ids (`IdAllocator` is not
touched; a mesh is purely visual). So a custom model is co-op/save safe, same basis
as the procedural-mesh and recolor paths already shipped. See
[`CUSTOM-MODELS.md`](CUSTOM-MODELS.md).

## What is autonomous vs needs you
- **Fully autonomous on this Mac (no Unity editor):** concept/mesh generation,
  Blender decimation, vanilla-rig extraction (UnityPy), numpy weight-transfer rig,
  runtime glTF load, and in-game screenshot + log verification (BepInEx + the agent
  harness). The whole pipeline ran end to end headless here.
- **Optional:** a higher-fidelity generator key (TRELLIS.2 is free/local), or the
  Unity 2017.2.2p2 editor if you choose the legacy AssetBundle path instead of the
  runtime glTF loader.
