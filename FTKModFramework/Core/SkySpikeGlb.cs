using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FTKModFramework.Core
{
    // SKY SPIKE ONLY: a rigid, textured GLB reader for generated (Rodin-style) ship models. Not a supported API.
    // RuntimeGltfMeshLoader.LoadStaticGlb reads only meshes[0].primitives[0], ignores node transforms, byteStride
    // and materials, assumes the framework exporter's Unity-space axes and caps a mesh at 65k vertices. Generated
    // GLBs need all of those, and changing that loader's contract would touch production item and enemy models,
    // so this reader stays beside the spike and reuses only the GLB container split.
    //
    // Supported: glTF 2.0 binary; every node in the default scene (matrix or TRS, full hierarchy); every triangle
    // primitive; float POSITION/NORMAL/TEXCOORD_0 (UVs may also be normalized u8/u16); u8/u16/u32 or absent
    // indices; interleaved views (byteStride); embedded PNG/JPEG images (bufferView or data: URI) or files beside
    // the GLB; baseColorFactor/Texture, metallic/roughness factors, emissive, alphaMode MASK. glTF is right-handed
    // (+Y up, +Z front); positions and normals are mirrored on X into Unity's left-handed space and the winding
    // flipped, so the model's glTF front stays +Z. Primitives above 65,000 vertices are split into several meshes
    // (Unity 2017.2 has 16-bit mesh indices only). Rejected: Draco/meshopt/KTX2/WebP extensions, sparse accessors,
    // external .bin buffers, skins and morph targets are ignored (rendered in bind pose).
    internal static class SkySpikeGlb
    {
        const int MaxFileBytes = 256 * 1024 * 1024;
        const int MaxVerticesPerMesh = 65000;
        // A raw generated export (about 755k vertices, 1M triangles) would render a million triangles per boat;
        // refuse it so the caller falls back and the log asks for a decimated export.
        const int MaxTotalVertices = 250000;
        const int MaxNodeDepth = 64;

        internal sealed class Part { public string Name; public Mesh Mesh; public Material Material; }

        internal sealed class Model
        {
            public readonly List<Part> Parts = new List<Part>();
            public readonly List<Material> Materials = new List<Material>();
            public Bounds Bounds;
            public int Vertices, Triangles, Textures, Primitives;
            public readonly List<string> Notes = new List<string>();
            internal readonly List<Object> Owned = new List<Object>();
            internal readonly HashSet<string> AmbiguousNodes = new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> SourceNodes = new HashSet<string>(StringComparer.Ordinal);

            internal void Dispose()
            {
                foreach (Object owned in Owned) if (owned != null) Object.Destroy(owned);
                Owned.Clear();
            }
        }

        // Throws on failure after destroying every object it created.
        internal static Model Load(string path, Shader shader)
        {
            List<Object> created = new List<Object>();
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists) throw new FileNotFoundException("GLB not found", path);
                if (info.Length > MaxFileBytes) throw new InvalidDataException("GLB larger than " + MaxFileBytes + " bytes.");
                string json;
                byte[] bin;
                if (!RuntimeGltfMeshLoader.SplitGlb(File.ReadAllBytes(path), out json, out bin))
                    throw new InvalidDataException("Not a glTF 2.0 binary (.glb) container.");
                Reader reader = new Reader(JObject.Parse(json), bin, Path.GetDirectoryName(path), shader, created);
                Model model = reader.Build();
                model.Owned.AddRange(created);
                return model;
            }
            catch
            {
                foreach (Object o in created) if (o != null) Object.Destroy(o);
                throw;
            }
        }

        sealed class Reader
        {
            readonly JObject _root;
            readonly byte[] _bin;
            readonly string _dir;
            readonly Shader _shader;
            readonly List<Object> _created;
            readonly JArray _accessors, _views, _nodes, _meshes, _materials, _textures, _images;
            readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();
            readonly Dictionary<int, Texture2D> _textureCache = new Dictionary<int, Texture2D>();
            readonly Model _model = new Model();
            bool _hasBounds;

            internal Reader(JObject root, byte[] bin, string dir, Shader shader, List<Object> created)
            {
                _root = root; _bin = bin; _dir = dir; _shader = shader; _created = created;
                _accessors = root["accessors"] as JArray;
                _views = root["bufferViews"] as JArray;
                _nodes = root["nodes"] as JArray;
                _meshes = root["meshes"] as JArray;
                _materials = root["materials"] as JArray;
                _textures = root["textures"] as JArray;
                _images = root["images"] as JArray;
            }

            internal Model Build()
            {
                JArray required = _root["extensionsRequired"] as JArray;
                if (required != null && required.Count > 0)
                    throw new NotSupportedException("GLB requires extensions " + required.ToString(Newtonsoft.Json.Formatting.None) +
                        "; export without Draco/meshopt/KTX2/WebP compression.");
                JArray buffers = _root["buffers"] as JArray;
                if (buffers != null && buffers.Count > 0 && buffers[0]["uri"] != null)
                    throw new NotSupportedException("GLB buffer 0 is external; export a self-contained .glb.");
                if (_meshes == null || _meshes.Count == 0) throw new InvalidDataException("GLB has no meshes.");

                List<int> roots = new List<int>();
                JArray scenes = _root["scenes"] as JArray;
                int sceneIndex = Int(_root["scene"], 0);
                JObject scene = scenes != null && sceneIndex >= 0 && sceneIndex < scenes.Count ? scenes[sceneIndex] as JObject : null;
                JArray sceneNodes = scene != null ? scene["nodes"] as JArray : null;
                if (sceneNodes != null) foreach (JToken n in sceneNodes) roots.Add((int)n);
                if (roots.Count > 0 && _nodes != null)
                {
                    foreach (int n in roots) Walk(n, Matrix4x4.identity, 0);
                }
                else
                {
                    // No scene graph: take meshes as authored.
                    _model.Notes.Add("no scene nodes; meshes used untransformed");
                    for (int m = 0; m < _meshes.Count; m++) EmitMesh(m, Matrix4x4.identity, "mesh" + m);
                }
                if (_model.Parts.Count == 0) throw new InvalidDataException("GLB produced no triangle geometry.");
                return _model;
            }

            void Walk(int index, Matrix4x4 parent, int depth)
            {
                if (depth > MaxNodeDepth || index < 0 || index >= _nodes.Count) return;
                JObject node = _nodes[index] as JObject;
                if (node == null) return;
                Matrix4x4 world = parent * NodeMatrix(node);
                if (node["mesh"] != null) EmitMesh((int)node["mesh"], world, (string)node["name"] ?? ("node" + index));
                JArray children = node["children"] as JArray;
                if (children != null) foreach (JToken c in children) Walk((int)c, world, depth + 1);
            }

            static Matrix4x4 NodeMatrix(JObject node)
            {
                JArray matrix = node["matrix"] as JArray;
                if (matrix != null && matrix.Count == 16)
                {
                    Matrix4x4 m = new Matrix4x4();
                    for (int k = 0; k < 16; k++) m[k] = (float)matrix[k]; // column-major in both glTF and Unity
                    return m;
                }
                Vector3 t = Vec3(node["translation"] as JArray, Vector3.zero);
                Vector3 s = Vec3(node["scale"] as JArray, Vector3.one);
                JArray r = node["rotation"] as JArray;
                Quaternion q = Quaternion.identity;
                if (r != null && r.Count == 4)
                {
                    Vector4 v = new Vector4((float)r[0], (float)r[1], (float)r[2], (float)r[3]);
                    float len = v.magnitude;
                    if (len > 1e-6f) { v /= len; q = new Quaternion(v.x, v.y, v.z, v.w); }
                }
                return Matrix4x4.TRS(t, q, s);
            }

            void EmitMesh(int meshIndex, Matrix4x4 world, string nodeName)
            {
                if (!_model.SourceNodes.Add(nodeName)) _model.AmbiguousNodes.Add(nodeName);
                if (meshIndex < 0 || meshIndex >= _meshes.Count) return;
                JObject mesh = _meshes[meshIndex] as JObject;
                JArray primitives = mesh != null ? mesh["primitives"] as JArray : null;
                if (primitives == null) return;
                Matrix4x4 normalMatrix = world.inverse.transpose;
                bool mirrored = world.determinant < 0f;
                for (int p = 0; p < primitives.Count; p++)
                {
                    JObject prim = primitives[p] as JObject;
                    if (prim == null) continue;
                    if (Int(prim["mode"], 4) != 4) { _model.Notes.Add(nodeName + "/p" + p + ": non-triangle mode skipped"); continue; }
                    JObject ext = prim["extensions"] as JObject;
                    if (ext != null && (ext["KHR_draco_mesh_compression"] != null || ext["EXT_meshopt_compression"] != null))
                        throw new NotSupportedException("Compressed primitive (Draco/meshopt); export uncompressed.");
                    JObject attrs = prim["attributes"] as JObject;
                    if (attrs == null || attrs["POSITION"] == null) continue;

                    Vector3[] positions = ToVec3(ReadFloats((int)attrs["POSITION"], 3));
                    Vector3[] normals = attrs["NORMAL"] != null ? ToVec3(ReadFloats((int)attrs["NORMAL"], 3)) : null;
                    Vector2[] uvs = attrs["TEXCOORD_0"] != null ? ToVec2(ReadFloats((int)attrs["TEXCOORD_0"], 2)) : null;
                    if (normals != null && normals.Length != positions.Length) normals = null;
                    if (uvs != null && uvs.Length != positions.Length) uvs = null;
                    int[] indices;
                    if (prim["indices"] != null) indices = ReadIndices((int)prim["indices"]);
                    else { indices = new int[positions.Length - positions.Length % 3]; for (int i = 0; i < indices.Length; i++) indices[i] = i; }
                    if (indices.Length % 3 != 0) throw new InvalidDataException("Index count is not a multiple of 3.");
                    foreach (int ix in indices) if (ix < 0 || ix >= positions.Length) throw new InvalidDataException("Index out of range.");
                    if (_model.Vertices + positions.Length > MaxTotalVertices)
                        throw new InvalidDataException("Model exceeds " + MaxTotalVertices + " vertices; export a decimated GLB (about 10k to 60k triangles).");

                    // glTF (right-handed) to Unity (left-handed): mirror X, flip winding unless the node already mirrors.
                    for (int i = 0; i < positions.Length; i++)
                    {
                        Vector3 v = world.MultiplyPoint3x4(positions[i]);
                        v.x = -v.x;
                        positions[i] = v;
                        if (!_hasBounds) { _model.Bounds = new Bounds(v, Vector3.zero); _hasBounds = true; }
                        else _model.Bounds.Encapsulate(v);
                    }
                    if (normals != null)
                        for (int i = 0; i < normals.Length; i++)
                        {
                            Vector3 n = normalMatrix.MultiplyVector(normals[i]).normalized;
                            n.x = -n.x;
                            normals[i] = n;
                        }
                    if (uvs != null) for (int i = 0; i < uvs.Length; i++) uvs[i].y = 1f - uvs[i].y;
                    if (!mirrored)
                        for (int i = 0; i < indices.Length; i += 3) { int t = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = t; }

                    Material material = MaterialFor(Int(prim["material"], -1));
                    string name = nodeName + "/p" + p;
                    _model.Primitives++;
                    _model.Vertices += positions.Length;
                    _model.Triangles += indices.Length / 3;
                    if (positions.Length <= MaxVerticesPerMesh) AddPart(name, positions, normals, uvs, indices, material);
                    else Split(name, positions, normals, uvs, indices, material);
                }
            }

            // Splits one large primitive into chunks that each fit 16-bit indices.
            void Split(string name, Vector3[] pos, Vector3[] nrm, Vector2[] uv, int[] idx, Material material)
            {
                Dictionary<int, int> remap = new Dictionary<int, int>();
                List<int> chunkIdx = new List<int>();
                int chunk = 0;
                for (int t = 0; t < idx.Length; t += 3)
                {
                    int missing = 0;
                    for (int k = 0; k < 3; k++) if (!remap.ContainsKey(idx[t + k])) missing++;
                    if (remap.Count + missing > MaxVerticesPerMesh)
                    {
                        FlushChunk(name + "#" + chunk++, pos, nrm, uv, remap, chunkIdx, material);
                        remap.Clear();
                        chunkIdx.Clear();
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        int src = idx[t + k], dst;
                        if (!remap.TryGetValue(src, out dst)) { dst = remap.Count; remap.Add(src, dst); }
                        chunkIdx.Add(dst);
                    }
                }
                if (chunkIdx.Count > 0) FlushChunk(name + "#" + chunk, pos, nrm, uv, remap, chunkIdx, material);
                _model.Notes.Add(name + ": " + pos.Length + " vertices split into " + (chunk + 1) + " meshes");
            }

            void FlushChunk(string name, Vector3[] pos, Vector3[] nrm, Vector2[] uv, Dictionary<int, int> remap, List<int> chunkIdx, Material material)
            {
                Vector3[] p = new Vector3[remap.Count];
                Vector3[] n = nrm != null ? new Vector3[remap.Count] : null;
                Vector2[] u = uv != null ? new Vector2[remap.Count] : null;
                foreach (KeyValuePair<int, int> e in remap)
                {
                    p[e.Value] = pos[e.Key];
                    if (n != null) n[e.Value] = nrm[e.Key];
                    if (u != null) u[e.Value] = uv[e.Key];
                }
                AddPart(name, p, n, u, chunkIdx.ToArray(), material);
            }

            void AddPart(string name, Vector3[] pos, Vector3[] nrm, Vector2[] uv, int[] idx, Material material)
            {
                Mesh mesh = new Mesh();
                _created.Add(mesh);
                mesh.name = "SkySpike " + name;
                mesh.vertices = pos;
                if (uv != null) mesh.uv = uv;
                mesh.triangles = idx;
                if (nrm != null) mesh.normals = nrm; else mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                _model.Parts.Add(new Part { Name = name, Mesh = mesh, Material = material });
            }

            Material MaterialFor(int index)
            {
                Material cached;
                if (_materialCache.TryGetValue(index, out cached)) return cached;
                Material m = new Material(_shader);
                _created.Add(m);
                _model.Materials.Add(m);
                _materialCache[index] = m;
                JObject mat = _materials != null && index >= 0 && index < _materials.Count ? _materials[index] as JObject : null;
                m.name = "SkySpike " + (mat != null ? ((string)mat["name"] ?? "material" + index) : "default");
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Glossiness", 0.2f);
                if (mat == null) return m;

                JObject pbr = mat["pbrMetallicRoughness"] as JObject;
                if (pbr != null)
                {
                    JArray factor = pbr["baseColorFactor"] as JArray;
                    if (factor != null && factor.Count == 4)
                        m.color = new Color((float)factor[0], (float)factor[1], (float)factor[2], (float)factor[3]);
                    Texture2D baseColor = TextureFor(pbr["baseColorTexture"] as JObject);
                    if (baseColor != null) m.mainTexture = baseColor;
                    // Standard reads metallic from R and smoothness from A; glTF packs metal in B, roughness in G.
                    // Without a remap pass, textured metal/roughness uses conservative factors instead.
                    bool packed = pbr["metallicRoughnessTexture"] != null;
                    float metallic = pbr["metallicFactor"] != null ? (float)pbr["metallicFactor"] : 1f;
                    float roughness = pbr["roughnessFactor"] != null ? (float)pbr["roughnessFactor"] : 1f;
                    m.SetFloat("_Metallic", packed ? 0f : Mathf.Clamp01(metallic));
                    m.SetFloat("_Glossiness", packed ? 0.2f : Mathf.Clamp(1f - roughness, 0f, 0.8f));
                }
                JArray emissive = mat["emissiveFactor"] as JArray;
                Texture2D emissiveMap = TextureFor(mat["emissiveTexture"] as JObject);
                if (emissive != null && emissive.Count == 3 && ((float)emissive[0] + (float)emissive[1] + (float)emissive[2]) > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color((float)emissive[0], (float)emissive[1], (float)emissive[2], 1f));
                    if (emissiveMap != null) m.SetTexture("_EmissionMap", emissiveMap);
                }
                string alphaMode = (string)mat["alphaMode"] ?? "OPAQUE";
                if (alphaMode == "MASK")
                {
                    m.SetFloat("_Mode", 1f);
                    m.SetFloat("_Cutoff", mat["alphaCutoff"] != null ? (float)mat["alphaCutoff"] : 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = 2450;
                }
                else if (alphaMode == "BLEND") _model.Notes.Add(m.name + ": alphaMode BLEND rendered opaque");
                if (mat["doubleSided"] != null && (bool)mat["doubleSided"]) _model.Notes.Add(m.name + ": doubleSided ignored (back faces culled)");
                if (mat["normalTexture"] != null) _model.Notes.Add(m.name + ": normal map ignored");
                return m;
            }

            Texture2D TextureFor(JObject info)
            {
                if (info == null || info["index"] == null || _textures == null) return null;
                int textureIndex = (int)info["index"];
                if (textureIndex < 0 || textureIndex >= _textures.Count) return null;
                JObject texture = _textures[textureIndex] as JObject;
                if (texture == null || texture["source"] == null)
                {
                    _model.Notes.Add("texture " + textureIndex + " has no PNG/JPEG source (KTX2/WebP unsupported)");
                    return null;
                }
                int image = (int)texture["source"];
                Texture2D cached;
                if (_textureCache.TryGetValue(image, out cached)) return cached;
                _textureCache[image] = null;
                byte[] bytes = ImageBytes(image);
                if (bytes == null) return null;
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                _created.Add(tex);
                if (!tex.LoadImage(bytes))
                {
                    _model.Notes.Add("image " + image + " failed to decode (PNG/JPEG only)");
                    return null;
                }
                tex.name = "SkySpike image " + image;
                tex.wrapMode = TextureWrapMode.Repeat;
                string previousSampling = tex.filterMode + "/" + tex.anisoLevel + "/" + tex.mipMapBias;
                // Match the authored trilinear sampler and preserve oblique deck detail. Keep mipmaps
                // and a restrained bias so distant map ships do not acquire hard aliasing.
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 8;
                tex.mipMapBias = -0.25f;
                Plugin.Log.LogInfo("[SkySpike] texture '" + tex.name + "': " + tex.width + "x" + tex.height +
                    ", mipmaps " + tex.mipmapCount + ", filter " + tex.filterMode + ", aniso " + tex.anisoLevel +
                    ", mip bias " + tex.mipMapBias + " (import defaults " + previousSampling + "), quality texture limit " +
                    QualitySettings.masterTextureLimit + ", anisotropic filtering " + QualitySettings.anisotropicFiltering + ".");
                _textureCache[image] = tex;
                _model.Textures++;
                if (tex.width > 4096 || tex.height > 4096) _model.Notes.Add(tex.name + " is " + tex.width + "x" + tex.height + " (large)");
                return tex;
            }

            byte[] ImageBytes(int image)
            {
                JObject img = _images != null && image >= 0 && image < _images.Count ? _images[image] as JObject : null;
                if (img == null) return null;
                if (img["bufferView"] != null) return ViewBytes((int)img["bufferView"]);
                string uri = (string)img["uri"];
                if (string.IsNullOrEmpty(uri)) return null;
                int comma = uri.IndexOf(',');
                if (uri.StartsWith("data:", StringComparison.Ordinal) && comma > 0)
                    return Convert.FromBase64String(uri.Substring(comma + 1));
                string file = Path.Combine(_dir, Uri.UnescapeDataString(uri));
                if (File.Exists(file)) return File.ReadAllBytes(file);
                _model.Notes.Add("image " + image + " file '" + uri + "' not found");
                return null;
            }

            byte[] ViewBytes(int viewIndex)
            {
                JObject view = View(viewIndex);
                int offset = Int(view["byteOffset"], 0), length = Int(view["byteLength"], -1);
                if (offset < 0 || length < 0 || (long)offset + length > _bin.Length) throw new InvalidDataException("bufferView exceeds BIN.");
                byte[] result = new byte[length];
                Buffer.BlockCopy(_bin, offset, result, 0, length);
                return result;
            }

            JObject View(int viewIndex)
            {
                JObject view = _views != null && viewIndex >= 0 && viewIndex < _views.Count ? _views[viewIndex] as JObject : null;
                if (view == null) throw new InvalidDataException("Missing bufferView " + viewIndex + ".");
                if (Int(view["buffer"], 0) != 0) throw new NotSupportedException("Only buffer 0 (the GLB BIN chunk) is supported.");
                return view;
            }

            // Reads float or normalized-integer accessors, honoring byteStride.
            float[] ReadFloats(int accessorIndex, int components)
            {
                JObject acc = Accessor(accessorIndex);
                int count = Int(acc["count"], 0);
                int component = Int(acc["componentType"], 0);
                bool normalized = acc["normalized"] != null && (bool)acc["normalized"];
                int size = component == 5126 ? 4 : component == 5123 || component == 5122 ? 2 : component == 5121 || component == 5120 ? 1 : 0;
                if (size == 0 || (component != 5126 && !normalized)) throw new NotSupportedException("Unsupported vertex accessor component " + component + ".");
                int start, stride;
                Locate(acc, count, size * components, out start, out stride);
                float[] result = new float[count * components];
                for (int i = 0; i < count; i++)
                {
                    int p = start + i * stride;
                    for (int c = 0; c < components; c++, p += size)
                    {
                        float v;
                        switch (component)
                        {
                            case 5126: v = BitConverter.ToSingle(_bin, p); break;
                            case 5123: v = BitConverter.ToUInt16(_bin, p) / 65535f; break;
                            case 5122: v = Mathf.Max(BitConverter.ToInt16(_bin, p) / 32767f, -1f); break;
                            case 5121: v = _bin[p] / 255f; break;
                            default: v = Mathf.Max((sbyte)_bin[p] / 127f, -1f); break;
                        }
                        if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Nonfinite vertex data.");
                        result[i * components + c] = v;
                    }
                }
                return result;
            }

            int[] ReadIndices(int accessorIndex)
            {
                JObject acc = Accessor(accessorIndex);
                int count = Int(acc["count"], 0);
                int component = Int(acc["componentType"], 0);
                int size = component == 5125 ? 4 : component == 5123 ? 2 : component == 5121 ? 1 : 0;
                if (size == 0) throw new NotSupportedException("Unsupported index component " + component + ".");
                int start, stride;
                Locate(acc, count, size, out start, out stride);
                int[] result = new int[count];
                for (int i = 0; i < count; i++)
                {
                    int p = start + i * stride;
                    long v = size == 4 ? BitConverter.ToUInt32(_bin, p) : size == 2 ? BitConverter.ToUInt16(_bin, p) : _bin[p];
                    if (v > int.MaxValue) throw new InvalidDataException("Index too large.");
                    result[i] = (int)v;
                }
                return result;
            }

            JObject Accessor(int index)
            {
                JObject acc = _accessors != null && index >= 0 && index < _accessors.Count ? _accessors[index] as JObject : null;
                if (acc == null) throw new InvalidDataException("Missing accessor " + index + ".");
                if (acc["sparse"] != null) throw new NotSupportedException("Sparse accessors are unsupported.");
                if (acc["bufferView"] == null) throw new NotSupportedException("Accessor without bufferView is unsupported.");
                return acc;
            }

            void Locate(JObject acc, int count, int elementSize, out int start, out int stride)
            {
                JObject view = View((int)acc["bufferView"]);
                int viewOffset = Int(view["byteOffset"], 0), viewLength = Int(view["byteLength"], -1);
                stride = Int(view["byteStride"], 0);
                if (stride == 0) stride = elementSize;
                start = viewOffset + Int(acc["byteOffset"], 0);
                if (count <= 0 || stride < elementSize || viewLength < 0) throw new InvalidDataException("Invalid accessor layout.");
                long end = start + (long)stride * (count - 1) + elementSize;
                if (start < viewOffset || end > (long)viewOffset + viewLength || end > _bin.Length)
                    throw new InvalidDataException("Accessor exceeds its bufferView.");
            }

            static Vector3[] ToVec3(float[] f)
            {
                Vector3[] r = new Vector3[f.Length / 3];
                for (int i = 0; i < r.Length; i++) r[i] = new Vector3(f[3 * i], f[3 * i + 1], f[3 * i + 2]);
                return r;
            }

            static Vector2[] ToVec2(float[] f)
            {
                Vector2[] r = new Vector2[f.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = new Vector2(f[2 * i], f[2 * i + 1]);
                return r;
            }

            static Vector3 Vec3(JArray a, Vector3 fallback)
            {
                return a != null && a.Count == 3 ? new Vector3((float)a[0], (float)a[1], (float)a[2]) : fallback;
            }

            static int Int(JToken token, int fallback)
            {
                return token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) ? (int)token : fallback;
            }
        }
    }
}
