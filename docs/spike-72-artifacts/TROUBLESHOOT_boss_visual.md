# Deep-dive: the runtime-glTF custom boss mesh renders as SHATTERED triangle soup in-game

## Mission
The Flooded Crypt boss "Mudwretch Foreman" (FTK Mod Framework; For The King; Unity 2017.2.2p2 / Mono / net35 / BepInEx 5 + HarmonyX) is given a custom AI mesh via an editor-free runtime glTF loader that swaps the troll body `SkinnedMeshRenderer.sharedMesh` at spawn. **In-game the boss renders as an EXPLODED CLOUD OF DISCONNECTED TRIANGLES (shards radiating from a center), not a coherent model.** Find and fix the rendering bug so the mesh renders as the solid hulking golem it actually is.

NOTE: a prior session wasted hours treating this as a darkness/lighting/aesthetic problem and "fixing" it with weld/smooth/rigid-weights/different-source-mesh/emission/key-light. NONE of that touched the bug, because the bug is in the runtime mesh reconstruction, not the data or the lighting. Do not go down the lighting path. Zoom in on screenshots (crop+upscale the boss region) before judging anything; full-frame dim shots hid the shatter and led to false "it's fixed now" conclusions.

Repo: `/Users/tbrack/Documents/Projects/FTK` (read its `CLAUDE.md`: subagent-first; net35/Mono only; never commit game DLLs; no em dashes; never `rm`, use Trash). Machine: Apple M5, macOS, no Unity editor, Blender 5.1.2, Python venv `.venv-3dgen` (UnityPy/numpy/scipy/trimesh).

## THE DECISIVE FACT (this is where to start)
The shipped/deployed glb `ai-model-gen/mudwretch_rigged.glb` is VERIFIED CORRECT by directly parsing it in Python (decode POSITION/indices/JOINTS_0/WEIGHTS_0 from the BIN chunk):
- 21662 vertices, POSITION bbox `[-2.26, -0.03, -0.78] .. [2.26, 2.62, 1.15]` (a clean upright figure, Y-up, ~2.65 tall).
- 12000 triangles; every index in range (`max index 21661 < 21662`).
- **`JOINTS_0` unique values = `[0]` and `WEIGHTS_0` = `(1, 0, 0, 0)` for EVERY vertex** -> pure rigid weighting, all verts bound 100% to joint slot 0 (`skins[0].joints[0]` -> node name `Root_M`). 37 named joints present.

With pure rigid weights (all verts share ONE bone -> ONE transform) a SkinnedMeshRenderer cannot scatter the mesh; at worst the whole thing rigidly translates/rotates. So the shatter is NOT skinning math, NOT bind poses, NOT the rig, NOT the data, NOT lighting. **It is the C# loader's reconstruction of the mesh, or a Unity-2017.2 runtime-skinned-mesh requirement that the loader violates.** It has been wrong for every mesh variant tried (the original t-pose mesh shattered identically); the rigid weights were added later and it STILL shatters, which is the proof.

## Where the bug is
`FTKModFramework/Core/RuntimeGltfMeshLoader.cs` (the loader). It: splits the GLB (12-byte header + JSON chunk + BIN chunk), hand-parses the JSON, decodes accessors from the BIN (POSITION VEC3 f32, NORMAL VEC3 f32, TEXCOORD_0 VEC2 f32 with V flipped, JOINTS_0 VEC4 u16, WEIGHTS_0 VEC4 f32, indices SCALAR u32), maps each vertex's glb joint-slot -> bone NAME (`nodes[skins[0].joints[slot]].name`) -> live `smr.bones` index, builds `BoneWeight[]`, builds `bindposes` from the glb `inverseBindMatrices` remapped to runtime bone order, then:
```
mesh.vertices = positions; mesh.normals = normals; mesh.uv = uvs;
mesh.triangles = triangles; mesh.boneWeights = boneWeights; mesh.bindposes = bindposes;
... ; smr.sharedMesh = mesh;
```
The loader's own diagnostics confirm it reads NAMES and bind poses correctly (logs `dropped slots=0`, and `probe Root_M/Head_M/Wrist_L: live==glb` byte-identical). So the corruption is in the per-vertex geometry/weight reconstruction or the Mesh assembly, not the name/bindpose logic.

## Prime hypotheses (test, do not guess-and-relaunch)
1. **The C# decodes POSITION or the index buffer wrong for this glb** -> scattered verts or triangles that span the mesh -> shatter AT REST. Check `ReadVec3`/`ReadScalarIndices`/`AccessorView`: bufferView byteOffset handling, the u32 index path, endianness, off-by-one, BIN-chunk start offset after the (space-padded) JSON chunk. Re-decode the glb in Python EXACTLY as the C# does and rasterize the rest mesh (POSITION+indices) to confirm the glb itself is coherent (it is, per the bbox above) and isolate the divergence to the C#.
2. **The JOINTS_0/WEIGHTS_0 decode is misaligned** so the runtime gets VARYING (garbage) per-vertex bones instead of all-Root -> animation scatters it. (`ReadVec4U16` reads `count*4` u16 from the accessor's bufferView offset; verify the offset and that the JOINTS accessor count == POSITION count.) This is the single most likely cause of "shatter despite a rigid glb".
3. **A Unity-2017.2 runtime-skinned-mesh requirement is violated.** Replacing `smr.sharedMesh` with a Mesh built at runtime that carries its own `boneWeights`/`bindposes`: confirm Unity actually skins it. Possible gotchas: `mesh.boneWeights.Length` must equal `vertexCount`; `mesh.bindposes.Length` must match `smr.bones.Length` AND order; the assignment ORDER (vertices before triangles before boneWeights/bindposes); whether `mesh.RecalculateBounds()`/`bounds` matters; whether the SMR needs `smr.sharedMesh` reassigned or `bones`/`rootBone` refreshed. Use `game-decompile-analyst` to check how the vanilla code builds/sets skinned meshes, and `csharp-harmony-engineer` for the fix.

## The fastest discriminating experiments (instrument, do not eyeball)
- **Bypass skinning:** in the loader, temporarily DO NOT set `mesh.boneWeights`/`mesh.bindposes`. If the boss then renders as a STATIC (un-animated) but COHERENT golem -> the geometry/decode is fine and the bug is in the bone-weight/bindpose path. If it STILL shatters -> the POSITION/index decode is wrong. This one test splits the problem in half.
- **Log what Unity actually has:** after building the Mesh, `Plugin.Log` `mesh.vertexCount`, `mesh.boneWeights.Length`, `mesh.bindposes.Length`, `smr.bones.Length`, and a few `mesh.vertices[i]` + `mesh.boneWeights[i].boneIndex0/weight0`. Confirm they match the glb (all weight0==1, boneIndex0==runtime Root index, positions sane).
- **Build the same mesh two ways:** compare the runtime-built Mesh against the same `.glb` loaded as a real AssetBundle (or against a Python-rebuilt mesh) to see whether the runtime construction specifically is at fault.
- Consider whether `IndexFormat` / 16-bit indices interacts (21662 verts < 65535, so 16-bit is fine; but confirm `mesh.triangles` doesn't silently truncate or mis-set).

## Build / deploy / run / verify (and the gotchas that wasted the last session)
- Build: `cd FTKModFramework && dotnet build -c Release`; copy `bin/Release/net35/FTKModFramework.dll` to `<game>/BepInEx/plugins/`; copy the glb+png to `<game>/BepInEx/plugins/FTKModFramework_content/models/`. `<game>` = `~/Library/Application Support/Steam/steamapps/common/For The King`.
- A DLL change OR a fresh boss spawn requires a RELAUNCH (the dungeon session persists; the loader builds per spawn but in-session re-runs do not reset the dungeon).
- Launch WITH BepInEx + the agent bridge (launching the raw `FTK.app/.../MacOS/FTK` skips the doorstop and BepInEx never loads): `: > "<game>/BepInEx/LogOutput.log"; (cd "<game>"; FTK_AGENT_BRIDGE=1 bash "<game>/run_bepinex.sh" > /tmp/ftk.log 2>&1 &)`. Wait for `FTKAgentBridge listening` in `LogOutput.log` (and confirm `SELF-TEST PASS`).
- Drive via the `mcp__ftk-agent` MCP tools (`ftk_observe/ftk_act/ftk_wait_for/ftk_screenshot`; bridge `127.0.0.1:8777`): `start_run {adventure:"HollowMire"}` -> wait ~22s -> `enter_dungeon {dungeon:"FloodedCrypt"}` -> `dungeon_regen` -> `cleared_room` x10 (boss `1112402686` is level1 room4; batched parallel `cleared_room` serialize) -> `dungeon_encounter` -> `dungeon_scroll_complete`.
- SCREENSHOT GOTCHAS: the diorama is only LIT on the PLAYER's first turn (`combat.heroTurnReady==true`). `advance` ENDS the turn -> enemy turn -> the diorama goes BLACK (combat stalls; ~600KB all-black PNG). To clear the re-firing boss-intro dialog WITHOUT ending the turn, use `dismiss_message` (NOT `advance`), then `ftk_screenshot`. **Then CROP+UPSCALE the boss region (~center, upper third) and inspect it** -- the shatter is invisible at full-frame size and led to false "fixed" calls.

## Current state (truth)
- Last commit `4bb3299` (branch `campaign-d1-hollow-mire`): the loader + pipeline + docs (shipped an EARLIER t-pose glb + emission). Build green.
- Uncommitted working tree (builds green): `ai-model-gen/mudwretch_rigged.glb` = the verified-correct TRELLIS rigid mesh; `ai-model-gen/mudwretch_basecolor.png` = trellis basecolor; `FTKModFramework/Core/EnemyVisualPatch.cs` has DEAD-END lighting experiments (low emission + a mis-positioned key light) in the glb-swap path -- IGNORE/REVERT those, they are not the issue; `tools/ai-model-pipeline/05b_rig_numpy.py` has useful `--src/--rigid/--posearms`+weld flags. Boss wired in `FTKModFramework/Content/RealmBossAdventure.cs` (`glbMesh`/`glbTexture`, swampAura/lantern/procedural/hunch/widthBoost all off).
- The rig pipeline (`tools/ai-model-pipeline/04b_extract_troll_skinned.py`, `05b_rig_numpy.py`) and the offline rasterizer are correct and reusable. Game troll mesh `enTroll01` = path_id 3190 in `<game>/FTK.app/Contents/Resources/Data/resources.assets`.

## Definition of done
A CROPPED+ZOOMED in-game agent-harness screenshot in which the Mudwretch Foreman is a COHERENT, connected solid mesh (a recognizable hulking golem), NOT a shard cloud; build green; `SELF-TEST PASS` present; no new ids / no determinism regression. Then re-enable real (multi-bone, non-rigid) skinning if desired, commit, and update `docs/`.
