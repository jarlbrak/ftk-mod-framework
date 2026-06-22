using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// EDITOR-FREE runtime loader for a SKINNED <c>.glb</c> mesh, keyed to the vanilla game skeleton by BONE NAME.
    /// This is the pure-managed (net35 / Mono 3.5) alternative to <see cref="CustomModelLoader"/>'s AssetBundle path:
    /// a modder ships a single <c>.glb</c> (built outside Unity, in any DCC/Python pipeline) at
    /// <c>&lt;pluginDir&gt;/FTKModFramework_content/models/&lt;file&gt;.glb</c> and we build a runtime <see cref="Mesh"/>
    /// that the EXISTING vanilla skeleton + Animator deform, with NO Unity editor and NO AssetBundle build step.
    ///
    /// THE NON-STANDARD GLB CONTRACT (this loader is paired with the mod's own exporter, NOT a general glTF reader):
    ///   - Vertex POSITIONs are already in Unity mesh-local space: NO axis/handedness conversion on read.
    ///   - The mesh is keyed to the live skeleton by BONE NAME: each per-vertex JOINTS_0 slot indexes
    ///     <c>skins[0].joints</c>, and <c>nodes[joint].name</c> is the troll bone name we remap to the live
    ///     <c>smr.bones[]</c> index. Influences whose bone name is absent on the live rig are dropped and the kept
    ///     weights renormalized (all-dropped falls back to weight 1.0 on bone 0).
    ///   - The bindposes come from the glb's OWN <c>skins[0].inverseBindMatrices</c>, name-remapped to runtime
    ///     <c>smr.bones[]</c> order (matrix for joint slot s lands at runtime bone index <c>slotToRuntime[s]</c>).
    ///     The glb vertices + glb IBM are a self-consistent (vertex-space, bind) pair, so rest pose = identity and
    ///     the vanilla Animator (driving the SAME troll skeleton the glb was rigged against) deforms coherently.
    ///     Reusing the LIVE bindposes (passed as <paramref name="runtimeBindposes"/>) is WRONG when the live mesh's
    ///     bindposes were authored against different vertices than the glb's: that scatters the vertices into an
    ///     exploded triangle cloud. The live bindposes are kept only as a per-slot / wholesale fallback (used when a
    ///     joint name is absent on the live rig, or the glb has no inverseBindMatrices accessor at all).
    ///   - &lt; 65,535 verts, so 16-bit indices are used: we never touch <c>Mesh.indexFormat</c> /
    ///     <c>IndexFormat.UInt32</c> (that API does not exist in Unity 2017.2.2p2).
    ///
    /// WHY THIS IS DETERMINISM- AND SAVE-SAFE: identical to <see cref="CustomModelLoader"/>. A mesh is VISUAL-ONLY;
    /// it never crosses Photon and is never serialized. The only shared state is the enemy enum-int identity
    /// (deterministic via <see cref="IdAllocator"/>). The <c>.glb</c> ships INSIDE the mod, byte-identical on every
    /// co-op client (no per-machine paths, no streaming), so game state stays byte-for-byte identical in co-op.
    ///
    /// NEVER THROWS: the whole load is wrapped; on ANY failure it logs a warning and returns null, so the caller
    /// keeps the original (or procedural) body, exactly matching <see cref="CustomModelLoader"/>'s contract.
    /// </summary>
    internal static class RuntimeGltfMeshLoader
    {
        // GLB container magic numbers (little-endian uint32), per the binary format the mod's exporter writes.
        private const uint GlbMagic = 0x46546C67;   // "glTF"
        private const uint ChunkJson = 0x4E4F534A;  // "JSON"
        private const uint ChunkBin = 0x004E4942;   // "BIN\0"

        // glTF componentType values used by the accessors we read.
        private const int CompFloat32 = 5126;
        private const int CompUint32 = 5125;
        private const int CompUint16 = 5123;

        /// <summary>
        /// Build a runtime skinned <see cref="Mesh"/> from the <c>.glb</c> at
        /// <c>FTKModFramework_content/models/&lt;glbFileName&gt;</c>, name-remapping its joints onto
        /// <paramref name="runtimeBones"/>. Bindposes come from the glb's own <c>inverseBindMatrices</c>
        /// (name-remapped to <paramref name="runtimeBones"/> order); <paramref name="runtimeBindposes"/> (the LIVE
        /// troll bindposes, in <paramref name="runtimeBones"/> order) is kept only as a fallback. Returns the Mesh,
        /// or <c>null</c> (with a warning) on any failure. Never throws.
        /// </summary>
        /// <param name="glbFileName">The .glb file name under FTKModFramework_content/models/.</param>
        /// <param name="runtimeBones">The live SkinnedMeshRenderer.bones[] (the vanilla skeleton).</param>
        /// <param name="runtimeBindposes">The live sharedMesh.bindposes[], captured BEFORE the swap (paired with
        /// <paramref name="runtimeBones"/> by index).</param>
        internal static Mesh LoadSkinnedGlb(string glbFileName, Transform[] runtimeBones, Matrix4x4[] runtimeBindposes)
        {
            string path = null;
            try
            {
                if (string.IsNullOrEmpty(glbFileName))
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: glbFileName is null/empty; skipped.");
                    return null;
                }
                if (runtimeBones == null || runtimeBones.Length == 0)
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: runtimeBones is null/empty for '" + glbFileName +
                        "'; cannot name-remap. Skipped.");
                    return null;
                }
                if (runtimeBindposes == null || runtimeBindposes.Length != runtimeBones.Length)
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: runtimeBindposes (" +
                        (runtimeBindposes == null ? -1 : runtimeBindposes.Length) + ") must pair 1:1 with bones (" +
                        runtimeBones.Length + ") for '" + glbFileName + "'. Skipped.");
                    return null;
                }

                path = CustomModelLoader.ResolveModelPath(glbFileName);
                if (!File.Exists(path))
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: file not found at '" + path +
                        "'. Ship it at FTKModFramework_content/models/" + glbFileName + ".");
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);

                // 1) Split the GLB container into the JSON text + the BIN payload (buffer 0).
                string json;
                byte[] bin;
                if (!SplitGlb(bytes, out json, out bin))
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: '" + glbFileName +
                        "' is not a valid GLB (bad magic / version / chunks).");
                    return null;
                }

                // 2) Parse the JSON (hand-rolled; no JSON library, System.Text.Json forbidden on net35).
                object root = JsonParser.Parse(json);
                JObj rootObj = root as JObj;
                if (rootObj == null)
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: '" + glbFileName + "' JSON root is not an object.");
                    return null;
                }

                // 3) Walk the JSON to the single mesh primitive's accessor indices + the skin's joint bone names.
                GltfDoc doc = new GltfDoc(rootObj, bin);

                int posAcc, normAcc, uvAcc, jointsAcc, weightsAcc, indicesAcc;
                if (!doc.ReadPrimitive(out posAcc, out normAcc, out uvAcc, out jointsAcc, out weightsAcc, out indicesAcc))
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: '" + glbFileName +
                        "' has no usable meshes[0].primitives[0] (POSITION/JOINTS_0/WEIGHTS_0/indices required).");
                    return null;
                }

                // Per-joint-slot bone NAME: nodes[ skins[0].joints[slot] ].name. JOINTS_0 values index this array.
                string[] jointBoneNames = doc.ReadSkinJointNames();
                if (jointBoneNames == null || jointBoneNames.Length == 0)
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: '" + glbFileName +
                        "' has no skins[0].joints; cannot name-remap.");
                    return null;
                }

                // 4) Decode the accessor buffers (tightly packed; one bufferView each; no byteStride).
                Vector3[] positions = doc.ReadVec3(posAcc);
                Vector3[] normals = normAcc >= 0 ? doc.ReadVec3(normAcc) : null;
                Vector2[] uvs = uvAcc >= 0 ? doc.ReadVec2(uvAcc) : null;
                ushort[] joints = doc.ReadVec4U16(jointsAcc);     // 4 per vertex, flattened
                float[] weights = doc.ReadVec4F32(weightsAcc);    // 4 per vertex, flattened
                int[] triangles = doc.ReadScalarIndices(indicesAcc);

                if (positions == null || joints == null || weights == null || triangles == null)
                {
                    Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: '" + glbFileName +
                        "' accessor decode failed (positions/joints/weights/indices null).");
                    return null;
                }

                int vCount = positions.Length;

                // 5) Build the live bone NAME -> runtime index map (smr.bones[i].name -> i).
                Dictionary<string, int> nameToRuntimeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < runtimeBones.Length; i++)
                {
                    Transform b = runtimeBones[i];
                    if (b == null || b.name == null) continue;
                    if (!nameToRuntimeIndex.ContainsKey(b.name)) nameToRuntimeIndex[b.name] = i;
                }

                // Pre-map each glb joint SLOT (0..jointBoneNames.Length-1) to a live runtime bone index (or -1 if
                // that bone name is absent on the live rig). Per-vertex JOINTS_0 values index this slot table.
                int[] slotToRuntime = new int[jointBoneNames.Length];
                int droppedSlots = 0;
                for (int s = 0; s < jointBoneNames.Length; s++)
                {
                    int ri;
                    if (jointBoneNames[s] != null && nameToRuntimeIndex.TryGetValue(jointBoneNames[s], out ri))
                        slotToRuntime[s] = ri;
                    else { slotToRuntime[s] = -1; droppedSlots++; }
                }

                // 6) Remap each vertex's 4 (slot, weight) influences onto live bone indices; drop unmatched ones;
                //    renormalize the kept weights to sum to 1 (all-dropped -> weight 1.0 on bone 0).
                BoneWeight[] boneWeights = BuildBoneWeights(vCount, joints, weights, slotToRuntime);

                // 6b) Build the bindposes the Mesh will use, indexed by RUNTIME bone index (the same index the
                //     boneWeights reference). Use the glb's OWN inverseBindMatrices (authored consistently with the
                //     glb vertices), name-remapped to runtime order. The glb vertices + glb IBM are a self-consistent
                //     (vertex-space, bind) pair, so rest pose = identity; reusing the LIVE bindposes (which pair with a
                //     DIFFERENT mesh's vertices) scatters the vertices, hence the exploded triangle cloud.
                //     Fallback per-slot to runtimeBindposes; whole-array fallback if the glb has no IBM accessor.
                Matrix4x4[] glbIBM = doc.ReadInverseBindMatrices(); // indexed by glb joint slot; null if absent
                Matrix4x4[] bindposes;
                bool usedGlbIbm;
                if (glbIBM != null && glbIBM.Length > 0)
                {
                    bindposes = new Matrix4x4[runtimeBones.Length];
                    for (int i = 0; i < runtimeBones.Length; i++) bindposes[i] = runtimeBindposes[i]; // fallback
                    int slotMax = Math.Min(slotToRuntime.Length, glbIBM.Length);
                    for (int s = 0; s < slotMax; s++)
                    {
                        int r = slotToRuntime[s];
                        if (r >= 0) bindposes[r] = glbIBM[s];
                    }
                    usedGlbIbm = true;
                }
                else
                {
                    // Older glb (no inverseBindMatrices) or decode failure: fall back to the live bindposes.
                    bindposes = runtimeBindposes;
                    usedGlbIbm = false;
                    Plugin.Log.LogInfo("[gltf] LoadSkinnedGlb: '" + glbFileName +
                        "' has no decodable skins[0].inverseBindMatrices; falling back to LIVE runtime bindposes.");
                }

                // 6c) DIAGNOSTICS (cheap LogInfo): dump the runtime bone NAME order so file-vs-runtime permutation is
                //     visible, then for 3 probe bones compare the LIVE bind position vs the GLB bind position. A
                //     bind position is the inverse bindpose's translation column (column 3 of the inverse matrix).
                LogBindposeDiagnostics(glbFileName, runtimeBones, runtimeBindposes, nameToRuntimeIndex,
                    jointBoneNames, glbIBM);

                // 7) Assemble the Mesh. Vertices BEFORE triangles. NO coordinate conversion on positions/normals;
                //    UV V is flipped. Bindposes come from the glb's OWN inverseBindMatrices (name-remapped to runtime
                //    order), falling back to the LIVE troll bindposes per-slot / wholesale.
                //
                // TEMPORARY DIAGNOSTIC INSTRUMENTATION (work item #73, FR-1; to be removed/fenced in #75, FR-5):
                // FTK_DIAG_SKIP_SKIN==1 is the "split the problem in half" discriminator. When set, we SKIP assigning
                // mesh.boneWeights/mesh.bindposes, leaving a STATIC unskinned mesh, to isolate a skinning-rebind bug
                // from a decode bug: if the static mesh renders correctly the decode is sound and the fault is in the
                // skin rebind; if it is still wrong the decode itself is suspect. Read ONCE into a local bool (mirrors
                // the single-env-read idiom of the FTK_BASELINE_STOCK_BODY lever in Content/RealmBossAdventure.cs);
                // distinct name, NEVER reuse FTK_BASELINE_STOCK_BODY. When unset or != "1" the behavior below is
                // BYTE-IDENTICAL to the shipped path (assign boneWeights + bindposes, no diagnostic log).
                bool diagSkipSkin = Environment.GetEnvironmentVariable("FTK_DIAG_SKIP_SKIN") == "1";

                Mesh mesh = new Mesh();
                mesh.name = "ftkmf_glb_" + glbFileName;
                mesh.vertices = positions;                 // set vertices first
                if (normals != null && normals.Length == vCount) mesh.normals = normals;
                if (uvs != null && uvs.Length == vCount)
                {
                    for (int i = 0; i < uvs.Length; i++) uvs[i].y = 1f - uvs[i].y; // V flip
                    mesh.uv = uvs;
                }
                mesh.triangles = triangles;                // then triangles (16-bit indices; never touch indexFormat)
                if (diagSkipSkin)
                {
                    // Skip boneWeights/bindposes: a static unskinned mesh, to isolate rebind vs decode.
                    Plugin.Log.LogInfo("[gltf][DIAG] FTK_DIAG_SKIP_SKIN=1: skipping boneWeights/bindposes " +
                        "(static unskinned mesh) to isolate rebind vs decode.");
                }
                else
                {
                    mesh.boneWeights = boneWeights;
                    mesh.bindposes = bindposes;            // glb IBM (name-remapped) or live-bindpose fallback
                }
                if (normals == null || normals.Length != vCount) mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                int triCount = triangles.Length / 3;
                Plugin.Log.LogInfo("[gltf] LoadSkinnedGlb: built '" + glbFileName + "' (" + vCount + " verts, " +
                    triCount + " tris; 16-bit indices; bindposes=" +
                    (diagSkipSkin ? "DIAG-SKIPPED (static unskinned)" : (usedGlbIbm ? "glb-IBM" : "live-fallback")) +
                    "; joint slots=" + jointBoneNames.Length +
                    ", dropped slots (name not on live rig)=" + droppedSlots + ").");
                return mesh;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[gltf] LoadSkinnedGlb: failed to load '" +
                    (path != null ? path : glbFileName) + "': " + e.Message);
                return null;
            }
        }

        // ---- GLB container ----------------------------------------------------------------------------------

        /// <summary>
        /// Split the 12-byte-header GLB into its JSON-chunk text and BIN-chunk payload (buffer 0). Returns false if
        /// the magic/version is wrong or a required chunk is missing/out of bounds. BIN may be empty (returned as a
        /// zero-length array) only if there is no BIN chunk, which our skinned format never produces.
        /// </summary>
        private static bool SplitGlb(byte[] bytes, out string json, out byte[] bin)
        {
            json = null;
            bin = null;
            if (bytes == null || bytes.Length < 12) return false;

            int p = 0;
            uint magic = ReadU32(bytes, ref p);
            uint version = ReadU32(bytes, ref p);
            ReadU32(bytes, ref p); // totalLength (not trusted; we bound everything against bytes.Length)
            if (magic != GlbMagic || version != 2) return false;

            // Chunk 0 MUST be JSON.
            if (p + 8 > bytes.Length) return false;
            uint jsonLen = ReadU32(bytes, ref p);
            uint jsonType = ReadU32(bytes, ref p);
            if (jsonType != ChunkJson) return false;
            if (p + (long)jsonLen > bytes.Length) return false;
            json = Encoding.UTF8.GetString(bytes, p, (int)jsonLen);
            p += (int)jsonLen;

            // Chunk 1 SHOULD be BIN (our format always emits it). If absent, BIN is empty.
            if (p + 8 <= bytes.Length)
            {
                uint binLen = ReadU32(bytes, ref p);
                uint binType = ReadU32(bytes, ref p);
                if (binType == ChunkBin && p + (long)binLen <= bytes.Length)
                {
                    bin = new byte[binLen];
                    Buffer.BlockCopy(bytes, p, bin, 0, (int)binLen);
                }
            }
            if (bin == null) bin = new byte[0];
            return true;
        }

        private static uint ReadU32(byte[] b, ref int p)
        {
            uint v = (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24));
            p += 4;
            return v;
        }

        // ---- bone-weight remap ------------------------------------------------------------------------------

        /// <summary>
        /// For each vertex, take its 4 (jointSlot, weight) influences, map jointSlot -> live bone index via
        /// <paramref name="slotToRuntime"/> (dropping any -1), renormalize the kept weights to sum to 1 (all dropped
        /// -> weight 1.0 on bone 0), sort the kept influences by weight descending, and fill boneIndex0..3 /
        /// weight0..3. Unused slots are weight 0 on bone 0 (Unity ignores zero-weight influences).
        /// </summary>
        private static BoneWeight[] BuildBoneWeights(int vCount, ushort[] joints, float[] weights, int[] slotToRuntime)
        {
            BoneWeight[] result = new BoneWeight[vCount];
            int slotCount = slotToRuntime.Length;

            // Scratch for the up-to-4 kept influences per vertex.
            int[] keptBone = new int[4];
            float[] keptWeight = new float[4];

            for (int v = 0; v < vCount; v++)
            {
                int baseI = v * 4;
                int kept = 0;
                float sum = 0f;

                for (int k = 0; k < 4; k++)
                {
                    int slot = joints[baseI + k];
                    float w = weights[baseI + k];
                    if (w <= 0f) continue;
                    if (slot < 0 || slot >= slotCount) continue;
                    int bone = slotToRuntime[slot];
                    if (bone < 0) continue; // bone name absent on the live rig: drop this influence

                    keptBone[kept] = bone;
                    keptWeight[kept] = w;
                    sum += w;
                    kept++;
                }

                BoneWeight bw = new BoneWeight();
                if (kept == 0 || sum <= 0f)
                {
                    // All influences dropped (or zero weight): pin fully to bone 0 so the vertex still skins.
                    bw.boneIndex0 = 0; bw.weight0 = 1f;
                    result[v] = bw;
                    continue;
                }

                // Renormalize the kept weights to sum to 1.
                float inv = 1f / sum;
                for (int k = 0; k < kept; k++) keptWeight[k] *= inv;

                // Sort the kept influences by weight descending (simple insertion sort over <=4 items).
                for (int a = 1; a < kept; a++)
                {
                    int bIdx = keptBone[a];
                    float wv = keptWeight[a];
                    int b2 = a - 1;
                    while (b2 >= 0 && keptWeight[b2] < wv)
                    {
                        keptBone[b2 + 1] = keptBone[b2];
                        keptWeight[b2 + 1] = keptWeight[b2];
                        b2--;
                    }
                    keptBone[b2 + 1] = bIdx;
                    keptWeight[b2 + 1] = wv;
                }

                bw.boneIndex0 = keptBone[0]; bw.weight0 = keptWeight[0];
                if (kept > 1) { bw.boneIndex1 = keptBone[1]; bw.weight1 = keptWeight[1]; }
                if (kept > 2) { bw.boneIndex2 = keptBone[2]; bw.weight2 = keptWeight[2]; }
                if (kept > 3) { bw.boneIndex3 = keptBone[3]; bw.weight3 = keptWeight[3]; }
                result[v] = bw;
            }
            return result;
        }

        // ---- diagnostics (temporary, cheap) ----------------------------------------------------------------

        /// <summary>
        /// Emit ONE info line dumping the runtime bone NAME order (so file-vs-runtime permutation is visible), then
        /// for 3 probe bones ("Root_M", "Head_M", "Wrist_L") compare the LIVE bind position vs the GLB bind position.
        /// A bind position is the bindpose's INVERSE translation (column 3 of the inverse matrix): the bone's rest
        /// position in mesh-local space. If live and glb differ, the live bindposes were authored against a different
        /// mesh than the glb vertices, which is exactly what scatters the vertices. Best-effort: any null/missing
        /// probe is logged as "n/a"; this never affects the load.
        /// </summary>
        private static void LogBindposeDiagnostics(string glbFileName, Transform[] runtimeBones,
            Matrix4x4[] runtimeBindposes, Dictionary<string, int> nameToRuntimeIndex, string[] jointBoneNames,
            Matrix4x4[] glbIBM)
        {
            try
            {
                StringBuilder order = new StringBuilder();
                for (int i = 0; i < runtimeBones.Length; i++)
                {
                    if (i > 0) order.Append(',');
                    Transform b = runtimeBones[i];
                    order.Append(b != null && b.name != null ? b.name : "<null>");
                }

                StringBuilder probes = new StringBuilder();
                string[] probeNames = { "Root_M", "Head_M", "Wrist_L" };
                for (int pi = 0; pi < probeNames.Length; pi++)
                {
                    string name = probeNames[pi];
                    probes.Append(" probe ").Append(name).Append(": live=").Append(ProbeLive(name, nameToRuntimeIndex, runtimeBindposes))
                          .Append(" glb=").Append(ProbeGlb(name, jointBoneNames, glbIBM));
                }

                Plugin.Log.LogInfo("[gltf] bindpose-diag '" + glbFileName + "': runtimeBones.Length=" +
                    runtimeBones.Length + " order=[" + order + "];" + probes);
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("[gltf] bindpose-diag '" + glbFileName + "': skipped (" + e.Message + ").");
            }
        }

        /// <summary>LIVE bind position for a probe bone name: inverse(runtimeBindposes[idx]).GetColumn(3), or "n/a".</summary>
        private static string ProbeLive(string name, Dictionary<string, int> nameToRuntimeIndex, Matrix4x4[] runtimeBindposes)
        {
            int idx;
            if (!nameToRuntimeIndex.TryGetValue(name, out idx)) return "n/a";
            if (idx < 0 || idx >= runtimeBindposes.Length) return "n/a";
            Vector4 c = runtimeBindposes[idx].inverse.GetColumn(3);
            return Fmt(c);
        }

        /// <summary>GLB bind position for a probe bone name: inverse(glbIBM[slot]).GetColumn(3), or "n/a".</summary>
        private static string ProbeGlb(string name, string[] jointBoneNames, Matrix4x4[] glbIBM)
        {
            if (glbIBM == null) return "n/a";
            int slot = -1;
            for (int s = 0; s < jointBoneNames.Length; s++)
            {
                if (string.Equals(jointBoneNames[s], name, StringComparison.Ordinal)) { slot = s; break; }
            }
            if (slot < 0 || slot >= glbIBM.Length) return "n/a";
            Vector4 c = glbIBM[slot].inverse.GetColumn(3);
            return Fmt(c);
        }

        private static string Fmt(Vector4 c)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            return "(" + c.x.ToString("0.###", inv) + "," + c.y.ToString("0.###", inv) + "," + c.z.ToString("0.###", inv) + ")";
        }

        // ---- glTF JSON walker (over the hand-rolled parser) -------------------------------------------------

        /// <summary>
        /// Thin reader over the parsed glTF JSON object + the BIN payload. Knows only the tightly-packed,
        /// one-bufferView-each, no-byteStride layout the mod's exporter writes (see the class doc).
        /// </summary>
        private sealed class GltfDoc
        {
            private readonly JObj _root;
            private readonly byte[] _bin;
            private readonly JArr _accessors;
            private readonly JArr _bufferViews;
            private readonly JArr _nodes;
            private readonly JArr _skins;
            private readonly JArr _meshes;

            internal GltfDoc(JObj root, byte[] bin)
            {
                _root = root;
                _bin = bin;
                _accessors = root.GetArr("accessors");
                _bufferViews = root.GetArr("bufferViews");
                _nodes = root.GetArr("nodes");
                _skins = root.GetArr("skins");
                _meshes = root.GetArr("meshes");
            }

            /// <summary>Read meshes[0].primitives[0] attribute + indices accessor indices. Returns false if the
            /// required POSITION/JOINTS_0/WEIGHTS_0/indices are missing.</summary>
            internal bool ReadPrimitive(out int pos, out int norm, out int uv, out int joints, out int weights, out int indices)
            {
                pos = norm = uv = joints = weights = indices = -1;
                if (_meshes == null || _meshes.Count == 0) return false;
                JObj mesh0 = _meshes.GetObj(0);
                if (mesh0 == null) return false;
                JArr prims = mesh0.GetArr("primitives");
                if (prims == null || prims.Count == 0) return false;
                JObj prim0 = prims.GetObj(0);
                if (prim0 == null) return false;

                JObj attrs = prim0.GetObj("attributes");
                if (attrs == null) return false;

                pos = attrs.GetInt("POSITION", -1);
                norm = attrs.GetInt("NORMAL", -1);
                uv = attrs.GetInt("TEXCOORD_0", -1);
                joints = attrs.GetInt("JOINTS_0", -1);
                weights = attrs.GetInt("WEIGHTS_0", -1);
                indices = prim0.GetInt("indices", -1);

                return pos >= 0 && joints >= 0 && weights >= 0 && indices >= 0;
            }

            /// <summary>For each skins[0].joints[k] node index, return nodes[idx].name. The returned array is indexed
            /// by the per-vertex JOINTS_0 slot (0..joints-1).</summary>
            internal string[] ReadSkinJointNames()
            {
                if (_skins == null || _skins.Count == 0 || _nodes == null) return null;
                JObj skin0 = _skins.GetObj(0);
                if (skin0 == null) return null;
                JArr joints = skin0.GetArr("joints");
                if (joints == null || joints.Count == 0) return null;

                string[] names = new string[joints.Count];
                for (int k = 0; k < joints.Count; k++)
                {
                    int nodeIdx = joints.GetInt(k, -1);
                    JObj node = (nodeIdx >= 0 && nodeIdx < _nodes.Count) ? _nodes.GetObj(nodeIdx) : null;
                    names[k] = node != null ? node.GetString("name", null) : null;
                }
                return names;
            }

            /// <summary>
            /// Read skins[0].inverseBindMatrices (a MAT4 f32 accessor, count == joints) as a Unity
            /// <see cref="Matrix4x4"/>[] indexed by the glb joint SLOT (same order as <see cref="ReadSkinJointNames"/>).
            /// glTF stores each MAT4 as 16 floats COLUMN-MAJOR; Unity's Matrix4x4 linear indexer is ALSO column-major
            /// (m[0]=m00, m[1]=m10, ... m[15]=m33), so the 16 floats copy straight across with NO transpose. Returns
            /// null if the skin has no inverseBindMatrices accessor (older glb) or the accessor cannot be decoded.
            /// </summary>
            internal Matrix4x4[] ReadInverseBindMatrices()
            {
                if (_skins == null || _skins.Count == 0) return null;
                JObj skin0 = _skins.GetObj(0);
                if (skin0 == null) return null;
                int accIndex = skin0.GetInt("inverseBindMatrices", -1);
                if (accIndex < 0) return null; // older glb: no IBM authored

                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                if (comp != CompFloat32) return null;
                if (off + (long)count * 64 > _bin.Length) return null; // 16 floats * 4 bytes = 64 per MAT4

                Matrix4x4[] r = new Matrix4x4[count];
                int p = off;
                for (int i = 0; i < count; i++)
                {
                    Matrix4x4 mtx = new Matrix4x4();
                    for (int k = 0; k < 16; k++)
                    {
                        mtx[k] = ReadF32(p); // column-major in == column-major out: no transpose
                        p += 4;
                    }
                    r[i] = mtx;
                }
                return r;
            }

            // ---- accessor decode (tightly packed; one bufferView each; no byteStride) ----

            /// <summary>Resolve an accessor to (byteOffset into BIN, count, componentType). Returns false on miss.</summary>
            private bool AccessorView(int accIndex, out int byteOffset, out int count, out int componentType)
            {
                byteOffset = count = componentType = 0;
                if (_accessors == null || accIndex < 0 || accIndex >= _accessors.Count) return false;
                JObj acc = _accessors.GetObj(accIndex);
                if (acc == null) return false;

                count = acc.GetInt("count", 0);
                componentType = acc.GetInt("componentType", 0);
                int bvIndex = acc.GetInt("bufferView", -1);
                int accByteOffset = acc.GetInt("byteOffset", 0); // accessor-local offset (usually 0)

                if (_bufferViews == null || bvIndex < 0 || bvIndex >= _bufferViews.Count) return false;
                JObj bv = _bufferViews.GetObj(bvIndex);
                if (bv == null) return false;
                int bvOffset = bv.GetInt("byteOffset", 0);

                byteOffset = bvOffset + accByteOffset;
                return count > 0 && byteOffset >= 0;
            }

            internal Vector3[] ReadVec3(int accIndex)
            {
                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                if (comp != CompFloat32) return null;
                if (off + (long)count * 12 > _bin.Length) return null;
                Vector3[] r = new Vector3[count];
                int p = off;
                for (int i = 0; i < count; i++)
                {
                    r[i] = new Vector3(ReadF32(p), ReadF32(p + 4), ReadF32(p + 8));
                    p += 12;
                }
                return r;
            }

            internal Vector2[] ReadVec2(int accIndex)
            {
                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                if (comp != CompFloat32) return null;
                if (off + (long)count * 8 > _bin.Length) return null;
                Vector2[] r = new Vector2[count];
                int p = off;
                for (int i = 0; i < count; i++)
                {
                    r[i] = new Vector2(ReadF32(p), ReadF32(p + 4));
                    p += 8;
                }
                return r;
            }

            /// <summary>VEC4 u16 (JOINTS_0): 4 ushorts per element, flattened to count*4.</summary>
            internal ushort[] ReadVec4U16(int accIndex)
            {
                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                if (comp != CompUint16) return null;
                if (off + (long)count * 8 > _bin.Length) return null;
                ushort[] r = new ushort[count * 4];
                int p = off;
                for (int i = 0; i < count * 4; i++)
                {
                    r[i] = (ushort)(_bin[p] | (_bin[p + 1] << 8));
                    p += 2;
                }
                return r;
            }

            /// <summary>VEC4 f32 (WEIGHTS_0): 4 floats per element, flattened to count*4.</summary>
            internal float[] ReadVec4F32(int accIndex)
            {
                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                if (comp != CompFloat32) return null;
                if (off + (long)count * 16 > _bin.Length) return null;
                float[] r = new float[count * 4];
                int p = off;
                for (int i = 0; i < count * 4; i++)
                {
                    r[i] = ReadF32(p);
                    p += 4;
                }
                return r;
            }

            /// <summary>SCALAR indices as int[] (componentType u32 or u16). &lt; 65,535 verts, so values fit in int
            /// and the Mesh uses 16-bit indices regardless of the source width (we never set indexFormat).</summary>
            internal int[] ReadScalarIndices(int accIndex)
            {
                int off, count, comp;
                if (!AccessorView(accIndex, out off, out count, out comp)) return null;
                int[] r = new int[count];
                int p = off;
                if (comp == CompUint32)
                {
                    if (off + (long)count * 4 > _bin.Length) return null;
                    for (int i = 0; i < count; i++)
                    {
                        r[i] = (int)(uint)(_bin[p] | (_bin[p + 1] << 8) | (_bin[p + 2] << 16) | (_bin[p + 3] << 24));
                        p += 4;
                    }
                    return r;
                }
                if (comp == CompUint16)
                {
                    if (off + (long)count * 2 > _bin.Length) return null;
                    for (int i = 0; i < count; i++)
                    {
                        r[i] = _bin[p] | (_bin[p + 1] << 8);
                        p += 2;
                    }
                    return r;
                }
                return null;
            }

            private float ReadF32(int p)
            {
                // BitConverter.ToSingle exists in net35; the buffer is little-endian and so is the target (x64 Mono).
                return BitConverter.ToSingle(_bin, p);
            }
        }
    }

    // ---- minimal JSON (objects, arrays, strings, numbers, true/false/null) -----------------------------------

    /// <summary>A parsed JSON object: string keys -> values (JObj / JArr / string / double / bool / null).</summary>
    internal sealed class JObj
    {
        private readonly Dictionary<string, object> _m = new Dictionary<string, object>(StringComparer.Ordinal);

        internal void Set(string key, object value) { _m[key] = value; }

        internal JObj GetObj(string key)
        {
            object v;
            return _m.TryGetValue(key, out v) ? v as JObj : null;
        }

        internal JArr GetArr(string key)
        {
            object v;
            return _m.TryGetValue(key, out v) ? v as JArr : null;
        }

        internal int GetInt(string key, int fallback)
        {
            object v;
            if (_m.TryGetValue(key, out v) && v is double) return (int)(double)v;
            return fallback;
        }

        internal string GetString(string key, string fallback)
        {
            object v;
            if (_m.TryGetValue(key, out v) && v is string) return (string)v;
            return fallback;
        }
    }

    /// <summary>A parsed JSON array of values (JObj / JArr / string / double / bool / null).</summary>
    internal sealed class JArr
    {
        private readonly List<object> _l = new List<object>();

        internal void Add(object value) { _l.Add(value); }
        internal int Count { get { return _l.Count; } }

        internal JObj GetObj(int i)
        {
            return (i >= 0 && i < _l.Count) ? _l[i] as JObj : null;
        }

        internal int GetInt(int i, int fallback)
        {
            if (i >= 0 && i < _l.Count && _l[i] is double) return (int)(double)_l[i];
            return fallback;
        }
    }

    /// <summary>
    /// A minimal, internal JSON parser (objects, arrays, strings, numbers, true/false/null). Just enough to read the
    /// glTF JSON chunk: there is NO JSON library available to Core and System.Text.Json is forbidden on net35. Not a
    /// general/strict parser; it assumes the well-formed glTF JSON the mod's exporter writes. Throws on malformed
    /// input (the single caller wraps the whole load in try/catch and returns null on any throw).
    /// </summary>
    internal static class JsonParser
    {
        internal static object Parse(string s)
        {
            int i = 0;
            object v = ParseValue(s, ref i);
            SkipWs(s, ref i);
            return v;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static JObj ParseObject(string s, ref int i)
        {
            JObj obj = new JObj();
            i++; // '{'
            SkipWs(s, ref i);
            if (s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                i++; // ':'
                object val = ParseValue(s, ref i);
                obj.Set(key, val);
                SkipWs(s, ref i);
                char c = s[i++];
                if (c == ',') continue;
                if (c == '}') break;
                throw new FormatException("JSON: expected ',' or '}' at " + (i - 1));
            }
            return obj;
        }

        private static JArr ParseArray(string s, ref int i)
        {
            JArr arr = new JArr();
            i++; // '['
            SkipWs(s, ref i);
            if (s[i] == ']') { i++; return arr; }
            while (true)
            {
                object val = ParseValue(s, ref i);
                arr.Add(val);
                SkipWs(s, ref i);
                char c = s[i++];
                if (c == ',') continue;
                if (c == ']') break;
                throw new FormatException("JSON: expected ',' or ']' at " + (i - 1));
            }
            return arr;
        }

        private static string ParseString(string s, ref int i)
        {
            // s[i] is the opening quote.
            i++;
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            string hex = s.Substring(i, 4);
                            i += 4;
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9')) i++;
                else break;
            }
            string num = s.Substring(start, i - start);
            return double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException("JSON: expected '" + literal + "' at " + i);
            i += literal.Length;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }
    }
}
