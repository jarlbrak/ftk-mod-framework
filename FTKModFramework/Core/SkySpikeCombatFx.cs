using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FTKModFramework.Core
{
    // SKY SPIKE combat presentation (env-gated live-test spike, not a supported API): a cloud floor well below
    // the ships in pooled sea-combat dioramas (restored for any non-spike use), and combat ship models split at
    // deck height so the upper works (envelopes, masts) fade when they hide a combatant from the combat camera.
    internal static partial class SkySpike
    {
        // ---- see-through split -------------------------------------------------------------------------------

        sealed class SplitModel
        {
            public ShipModel Model;
            public float Threshold;
            public bool ByMax;
            public int CellsX = 1, CellsY = 1, CellsZ = 1;
            public readonly List<ShipPart> Hull = new List<ShipPart>(), Upper = new List<ShipPart>();
            public int HullTriangles, UpperTriangles;
        }

        static readonly Dictionary<ShipModel, SplitModel> _splits = new Dictionary<ShipModel, SplitModel>();

        static float DeckFractionFor(ShipModel model, ShipTuning tune)
        {
            return tune.CombatDeckFraction >= 0f ? tune.CombatDeckFraction : model.DeckFraction;
        }

        // Model-space height of deck + hullBandClearance, where hull-band sizing cuts (vertices at or below it).
        // Kept apart from the see-through cut so tightening the fade does not resize any ship.
        static float HullThreshold(ShipModel model, ShipTuning tune)
        {
            return model.Bounds.min.y + (DeckFractionFor(model, tune) + tune.HullBandClearance) * model.Bounds.size.y;
        }

        // Model-space height of deck + seeThroughClearance, where the see-through split cuts.
        static float SplitThreshold(ShipModel model, ShipTuning tune)
        {
            return model.Bounds.min.y + (DeckFractionFor(model, tune) + tune.SeeThroughClearance) * model.Bounds.size.y;
        }

        // seeThroughSplit "max" (default): a triangle reaching above deck + clearance at any vertex is UPPER, so a
        // sail or mast that starts at the deck fades whole (railings fade too). "centroid": UPPER only when the
        // triangle's centroid is at or above the cut, which leaves the foot of tall parts opaque in the HULL.
        static SplitModel SplitFor(ShipModel model, ShipTuning tune)
        {
            if (model.Contract != null)
            {
                SplitModel authored;
                if (_splits.TryGetValue(model, out authored)) return authored;
                authored = new SplitModel { Model = model, Threshold = model.Contract.DeckY };
                foreach (ShipPart part in model.Parts)
                {
                    // A deliberate cutaway removes complete authored assemblies. It never cuts deck triangles
                    // or produces translucent balloon fragments over the native attack lanes.
                    if (model.Contract.IsUpperwork(part.Name)) continue;
                    authored.Hull.Add(part);
                    authored.HullTriangles += part.Mesh.triangles.Length / 3;
                }
                if (authored.Hull.Count == 0) throw new InvalidOperationException("Authored cutaway removed the entire ship.");
                _splits[model] = authored;
                return authored;
            }
            float deck = DeckFractionFor(model, tune);
            float threshold = SplitThreshold(model, tune);
            bool byMax = tune.SeeThroughSplit != "centroid";
            int cx = tune.SeeThroughCells[0], cy = tune.SeeThroughCells[1], cz = tune.SeeThroughCells[2];
            SplitModel cached;
            if (_splits.TryGetValue(model, out cached) && Mathf.Abs(cached.Threshold - threshold) < 1e-4f && cached.ByMax == byMax &&
                cached.CellsX == cx && cached.CellsY == cy && cached.CellsZ == cz) return cached;

            SplitModel split = new SplitModel { Model = model, Threshold = threshold, ByMax = byMax, CellsX = cx, CellsY = cy, CellsZ = cz };
            Bounds mb = model.Bounds;
            Vector3 size = new Vector3(Mathf.Max(mb.size.x, 1e-4f), Mathf.Max(mb.size.y - (threshold - mb.min.y), 1e-4f), Mathf.Max(mb.size.z, 1e-4f));
            foreach (ShipPart part in model.Parts)
            {
                Vector3[] v = part.Mesh.vertices;
                Vector3[] n = part.Mesh.normals;
                Vector2[] uv = part.Mesh.uv;
                int[] tris = part.Mesh.triangles;
                List<int> hull = new List<int>();
                // Upper triangles are grouped by the grid cell of their centroid (above the cut), one mesh per cell.
                SortedDictionary<int, List<int>> upper = new SortedDictionary<int, List<int>>();
                int upperCount = 0;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    float y0 = v[tris[t]].y, y1 = v[tris[t + 1]].y, y2 = v[tris[t + 2]].y;
                    bool isUpper = byMax
                        ? Mathf.Max(y0, Mathf.Max(y1, y2)) + part.Pivot.y > threshold
                        : (y0 + y1 + y2) / 3f + part.Pivot.y >= threshold;
                    List<int> into = hull;
                    if (isUpper)
                    {
                        Vector3 c = (v[tris[t]] + v[tris[t + 1]] + v[tris[t + 2]]) / 3f + part.Pivot;
                        int ix = Mathf.Clamp((int)((c.x - mb.min.x) / size.x * cx), 0, cx - 1);
                        int iy = Mathf.Clamp((int)((c.y - threshold) / size.y * cy), 0, cy - 1);
                        int iz = Mathf.Clamp((int)((c.z - mb.min.z) / size.z * cz), 0, cz - 1);
                        int key = (ix * cy + iy) * cz + iz;
                        if (!upper.TryGetValue(key, out into)) { into = new List<int>(); upper.Add(key, into); }
                        upperCount += 3;
                    }
                    into.Add(tris[t]); into.Add(tris[t + 1]); into.Add(tris[t + 2]);
                }
                if (upperCount == 0) { split.Hull.Add(part); split.HullTriangles += tris.Length / 3; continue; }
                if (hull.Count > 0) split.Hull.Add(SubPart(part, "hull", v, n, uv, hull));
                foreach (KeyValuePair<int, List<int>> cell in upper)
                    split.Upper.Add(upper.Count == 1 && hull.Count == 0 ? part : SubPart(part, "upper " + cell.Key, v, n, uv, cell.Value));
                split.HullTriangles += hull.Count / 3;
                split.UpperTriangles += upperCount / 3;
            }
            _splits[model] = split;
            Plugin.Log.LogInfo("[SkySpike] see-through split " + model.Source + " (" + (byMax ? "max vertex" : "centroid") + "): threshold y " +
                F2(threshold) + " (deck " + F2(deck) + " + clearance " + F2(tune.SeeThroughClearance) + " of height " + F2(model.Bounds.size.y) + "), hull " + split.HullTriangles +
                " tris in " + split.Hull.Count + " meshes, upper " + split.UpperTriangles + " tris in " + split.Upper.Count + " meshes (cells " +
                cx + "x" + cy + "x" + cz + ").");
            return split;
        }

        static ShipPart SubPart(ShipPart part, string label, Vector3[] v, Vector3[] n, Vector2[] uv, List<int> tris)
        {
            Dictionary<int, int> remap = new Dictionary<int, int>();
            int[] indices = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                int dst;
                if (!remap.TryGetValue(tris[i], out dst)) { dst = remap.Count; remap.Add(tris[i], dst); }
                indices[i] = dst;
            }
            bool hasNormals = n != null && n.Length == v.Length, hasUv = uv != null && uv.Length == v.Length;
            Vector3[] pv = new Vector3[remap.Count];
            Vector3[] pn = hasNormals ? new Vector3[remap.Count] : null;
            Vector2[] pu = hasUv ? new Vector2[remap.Count] : null;
            foreach (KeyValuePair<int, int> e in remap)
            {
                pv[e.Value] = v[e.Key];
                if (pn != null) pn[e.Value] = n[e.Key];
                if (pu != null) pu[e.Value] = uv[e.Key];
            }
            Mesh mesh = new Mesh();
            mesh.name = part.Mesh.name + " " + label;
            mesh.vertices = pv;
            if (pu != null) mesh.uv = pu;
            mesh.triangles = indices;
            if (pn != null) mesh.normals = pn; else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return new ShipPart { Name = part.Name + " " + label, Mesh = mesh, Material = part.Material, Pivot = part.Pivot };
        }

        static GameObject BuildSplitObject(string name, SplitModel split, int layer, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.SetActive(false);
            root.layer = layer;
            root.transform.SetParent(parent, false);
            List<MeshRenderer> upper = new List<MeshRenderer>();
            List<ShipPart> all = new List<ShipPart>(split.Hull);
            all.AddRange(split.Upper);
            for (int i = 0; i < all.Count; i++)
            {
                ShipPart part = all[i];
                GameObject child = new GameObject(part.Name);
                child.layer = layer;
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = part.Pivot;
                child.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = part.Material;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                if (i >= split.Hull.Count) upper.Add(meshRenderer);
            }
            root.AddComponent<SkySpikeSeeThrough>().Init(split, upper);
            try { SkySpikeRotors.Attach(root, split.Model.Contract); }
            catch { Object.Destroy(root); throw; }
            return root;
        }

        // ---- combat cloud floor --------------------------------------------------------------------------------

        sealed class FloorState
        {
            public readonly Dictionary<Renderer, Material[]> Materials = new Dictionary<Renderer, Material[]>();
            public readonly Dictionary<Renderer, bool> Enabled = new Dictionary<Renderer, bool>();
            public readonly Dictionary<Transform, Vector3> Positions = new Dictionary<Transform, Vector3>();
            public readonly Dictionary<Transform, Vector3> Scales = new Dictionary<Transform, Vector3>();
            public readonly Dictionary<GameObject, bool> Fx = new Dictionary<GameObject, bool>();
            public Material CombatCloud;
            public Texture2D CombatCloudTexture;
            public DioramaBoat Diorama;
            public WaterDistortDiorama PendingWater;
            public MeshFilter CloudMeshFilter;
            public Mesh NativeCloudMesh, CloudMesh;
            public bool Probed, Applied, BackgroundSaved, CloudTilingMeasured;
            public Color Background;
        }

        static readonly Dictionary<int, FloorState> _floors = new Dictionary<int, FloorState>();

        static void FillCombatCloudTexture(Texture2D texture)
        {
            int size = texture.width;
            Color[] pixels = new Color[size * size];
            Color shadow = new Color(0.38f, 0.50f, 0.65f, 1f);
            Color top = new Color(0.88f, 0.92f, 0.96f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float value = 0f, amplitude = 1f, total = 0f;
                    for (int octave = 0; octave < 3; octave++)
                    {
                        value += amplitude * TileablePerlin(x, y, size, 2 << octave, 17.3f + octave * 31.7f);
                        total += amplitude;
                        amplitude *= 0.35f;
                    }
                    float billow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, value / total));
                    pixels[y * size + x] = Color.Lerp(shadow, top, billow);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
        }

        static void FitCombatCloudTiling(FloorState state, Renderer water, int waterCount, HashSet<Transform> moved)
        {
            if (state.CloudTilingMeasured) return;
            state.CloudTilingMeasured = true;
            state.CombatCloud.mainTextureScale = Vector2.one;
            if (waterCount != 1 || water == null || !moved.Contains(water.transform) ||
                Mathf.Abs(Vector3.Dot(water.transform.up, Vector3.up)) <= 0.9999f)
            {
                Plugin.Log.LogInfo("[SkySpike] cloud tiling kept at 1: requires one separate horizontal water surface.");
                return;
            }
            WaterDistortDiorama native = water.GetComponent<WaterDistortDiorama>();
            MeshFilter filter = water.GetComponent<MeshFilter>();
            if (native == null || native.m_Mesh == null || filter == null || filter.sharedMesh == null)
            {
                Plugin.Log.LogInfo("[SkySpike] cloud planar UV skipped: native water mesh unavailable.");
                return;
            }
            // Start replaces the asset binding with its writable instance. An encounter can initialize
            // synchronously on first activation, so finish once after native Start instead of racing it.
            if (filter.sharedMesh == native.m_Mesh)
            {
                state.PendingWater = native;
                Plugin.Log.LogInfo("[SkySpike] cloud planar UV pending native Start on '" + water.name + "'.");
                return;
            }
            ApplyPlanarCloudMesh(state, water, native);
        }

        internal static void OnCloudWaterStarted(WaterDistortDiorama water)
        {
            if (water == null) return;
            foreach (FloorState state in _floors.Values)
            {
                if (state.PendingWater != water) continue;
                state.PendingWater = null;
                if (!state.Applied || state.CombatCloud == null || state.Diorama == null ||
                    !SpikeActive() || !SkySpikeCamera.OwnsArena(state.Diorama)) return;
                Renderer renderer = water.GetComponent<Renderer>();
                if (renderer == null || !state.Materials.ContainsKey(renderer) ||
                    !water.transform.IsChildOf(state.Diorama.transform)) return;
                ApplyPlanarCloudMesh(state, renderer, water);
                return;
            }
        }

        static void ApplyPlanarCloudMesh(FloorState state, Renderer water, WaterDistortDiorama native)
        {
            try
            {
                MeshFilter filter = water.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (state.CloudMesh != null || mesh == null || mesh == native.m_Mesh || !mesh.isReadable ||
                    mesh.vertexCount < 3 || mesh.vertexCount > 250000 || native.m_Mesh == null ||
                    mesh.vertexCount != native.m_Mesh.vertexCount ||
                    Mathf.Abs(Vector3.Dot(water.transform.up, Vector3.up)) <= 0.9999f)
                    throw new InvalidOperationException("water mesh is uninitialized, unreadable or incompatible");
                Vector3[] vertices = mesh.vertices;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (Vector3 point in vertices)
                {
                    if (!SkySpikeAssetContract.Finite(point.x) || !SkySpikeAssetContract.Finite(point.y) ||
                        !SkySpikeAssetContract.Finite(point.z)) throw new InvalidOperationException("nonfinite water position");
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                }
                Vector2[] uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    float x, z;
                    if (!SkySpikeCloudMath.TryPlanarCoordinate(vertices[i].x, min.x, max.x, out x) ||
                        !SkySpikeCloudMath.TryPlanarCoordinate(vertices[i].z, min.z, max.z, out z))
                        throw new InvalidOperationException("degenerate water projection");
                    uv[i] = new Vector2(x, z);
                }
                Bounds bounds = water.bounds;
                float u, v;
                if (!SkySpikeCloudMath.TryTextureScale(bounds.size.x, 0f, 1f, out u) ||
                    !SkySpikeCloudMath.TryTextureScale(bounds.size.z, 0f, 1f, out v))
                    throw new InvalidOperationException("degenerate or extreme water extent");
                int indices = mesh.triangles.Length;
                state.CloudMeshFilter = filter;
                state.NativeCloudMesh = mesh;
                state.CloudMesh = UnityEngine.Object.Instantiate(mesh);
                state.CloudMesh.name = "SkySpike planar cloud water";
                state.CloudMesh.uv = uv;
                if (state.CloudMesh.vertexCount != mesh.vertexCount || state.CloudMesh.triangles.Length != indices ||
                    state.CloudMesh.subMeshCount != mesh.subMeshCount)
                    throw new InvalidOperationException("cloud mesh clone changed native topology counts");
                // Native rendering writes vertices/normals to MeshFilter.mesh, never UVs or topology.
                // Keep the exact native arrays and count so that callback can continue on this clone.
                filter.mesh = state.CloudMesh;
                int assignedMeshId = state.CloudMesh.GetInstanceID();
                // Unity may instantiate an assigned mesh on the first mesh getter. Materialize that
                // boundary now, before native rendering, so we own the instance it will actually use.
                Mesh materialized = filter.mesh;
                if (materialized == null || materialized == mesh || materialized == native.m_Mesh)
                    throw new InvalidOperationException("water mesh getter did not retain a private instance");
                if (materialized != state.CloudMesh)
                {
                    Mesh intermediate = state.CloudMesh;
                    state.CloudMesh = materialized;
                    UnityEngine.Object.Destroy(intermediate);
                }
                if (filter.sharedMesh != state.CloudMesh)
                    throw new InvalidOperationException("water mesh binding changed during materialization");
                state.CombatCloud.mainTextureScale = new Vector2(u, v);
                Plugin.Log.LogInfo("[SkySpike] cloud planar UV on '" + water.name + "': owned mesh '" + state.CloudMesh.name +
                    "', native '" + mesh.name + "', preserved " + mesh.vertexCount + " vertices / " + indices +
                    " indices / " + mesh.subMeshCount + " submeshes; UV XZ normalized 0..1, expanded world size " +
                    Fmt(bounds.size) + ", texture scale " + F2(u) + "," + F2(v) + "; 1 private mesh, native deformation retained; mesh IDs native " +
                    mesh.GetInstanceID() + ", assigned " + assignedMeshId + ", owned " + state.CloudMesh.GetInstanceID() +
                    ", bound " + filter.sharedMesh.GetInstanceID() + ".");
            }
            catch (Exception e)
            {
                ReleaseCloudMesh(state);
                Plugin.Log.LogInfo("[SkySpike] cloud planar UV skipped, native mesh retained: " + e.Message + ".");
            }
        }

        static int ReleaseCloudMesh(FloorState state)
        {
            state.PendingWater = null;
            int restored = 0;
            Mesh bound = state.CloudMeshFilter != null ? state.CloudMeshFilter.sharedMesh : null;
            if (state.CloudMesh != null || state.CloudMeshFilter != null)
                Plugin.Log.LogInfo("[SkySpike] cloud mesh cleanup: filter alive " + (state.CloudMeshFilter != null) +
                    ", native ID " + (state.NativeCloudMesh != null ? state.NativeCloudMesh.GetInstanceID() : 0) +
                    ", owned ID " + (state.CloudMesh != null ? state.CloudMesh.GetInstanceID() : 0) +
                    ", bound ID " + (bound != null ? bound.GetInstanceID() : 0) + ", owned binding " +
                    (state.CloudMesh != null && bound == state.CloudMesh) + ".");
            if (state.CloudMeshFilter != null && state.CloudMesh != null && bound == state.CloudMesh)
            {
                state.CloudMeshFilter.sharedMesh = state.NativeCloudMesh;
                restored = 1;
            }
            if (state.CloudMesh != null) UnityEngine.Object.Destroy(state.CloudMesh);
            state.CloudMesh = null;
            state.NativeCloudMesh = null;
            state.CloudMeshFilter = null;
            return restored;
        }

        static float CombatCloudDrop()
        {
            JObject star = _tuning != null ? _tuning["*"] as JObject : null;
            return star != null ? Num(star, "combatCloudDrop", 6f) : 6f;
        }

        // keelY: the lowest keel of the spike combat ships placed this encounter (NaN when none). The floor drops by
        // combatCloudDrop, or further so the lowered sea top ends 2 units below that keel.
        static void ApplyCloudFloor(DioramaBoat diorama, float keelY)
        {
            if (!SkySpikeAssetContract.Finite(keelY)) { RestoreCloudFloor(diorama); return; }
            try { ApplyCloudFloorCore(diorama, keelY); }
            catch { RestoreCloudFloor(diorama); throw; }
        }

        static void ApplyCloudFloorCore(DioramaBoat diorama, float keelY)
        {
            RefreshTuning();
            EnsureCloudMaterials();
            float configured = CombatCloudDrop();
            float drop = configured;
            FloorState state;
            if (!_floors.TryGetValue(diorama.GetInstanceID(), out state)) { state = new FloorState { Diorama = diorama }; _floors[diorama.GetInstanceID()] = state; }

            List<Renderer> sea = new List<Renderer>();
            List<GameObject> fx = new List<GameObject>();
            FindSea(diorama, sea, fx);
            if (!state.Probed)
            {
                state.Probed = true;
                List<string> names = new List<string>();
                foreach (Renderer r in sea) names.Add(Describe(r));
                List<string> fxNames = new List<string>();
                foreach (GameObject g in fx) fxNames.Add("'" + g.name + "'");
                Renderer bg = diorama.m_Background;
                Plugin.Log.LogInfo("[SkySpike] combat floor probe '" + diorama.name + "': " + sea.Count + " sea renderers [" +
                    string.Join("; ", names.ToArray()) + "], background " + (bg != null ? Describe(bg) + " (native backdrop)" : "none") +
                    ", " + fx.Count + " sea FX [" + string.Join(", ", fxNames.ToArray()) + "].");
            }

            // A transform moves only if every renderer under it is sea or particle FX, so islands, hulls and
            // targets sharing a parent never move; such sea renderers are clouded in place.
            HashSet<Renderer> seaSet = new HashSet<Renderer>(sea);
            HashSet<Transform> moved = new HashSet<Transform>();
            List<Transform> movedOrder = new List<Transform>();
            int clouded = 0, kept = 0, sceneryHidden = 0;
            Renderer tilingWater = null;
            state.Applied = true; // Every subsequent mutation has saved state and must be restorable on failure.
            Renderer backdrop = diorama.m_Background;
            bool authored = SkySpikeCamera.OwnsArena(diorama);
            if (authored && state.CombatCloud == null)
            {
                // A private emission-only surface keeps native sunset tint and hard ship shadows from
                // turning the cloud layer into a pink floor. Own the broad billow texture before filling it
                // so failed generation also rolls back without changing the overworld's shared noise.
                state.CombatCloudTexture = new Texture2D(256, 256, TextureFormat.RGBA32, true);
                state.CombatCloudTexture.name = "SkySpike combat cloud billows";
                state.CombatCloudTexture.wrapMode = TextureWrapMode.Repeat;
                state.CombatCloudTexture.filterMode = FilterMode.Trilinear;
                state.CombatCloudTexture.anisoLevel = 4;
                FillCombatCloudTexture(state.CombatCloudTexture);
                state.CombatCloud = new Material(_cloudSurface);
                state.CombatCloud.name = "SkySpike combat cloud surface";
                state.CombatCloud.mainTexture = state.CombatCloudTexture;
                state.CombatCloud.mainTextureScale = Vector2.one;
                state.CombatCloud.color = Color.black;
                state.CombatCloud.SetFloat("_Glossiness", 0f);
                state.CombatCloud.SetFloat("_SpecularHighlights", 0f);
                state.CombatCloud.SetFloat("_GlossyReflections", 0f);
                state.CombatCloud.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                state.CombatCloud.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                state.CombatCloud.SetTexture("_EmissionMap", state.CombatCloudTexture);
                state.CombatCloud.SetColor("_EmissionColor", Color.white);
            }
            Material surface = authored ? state.CombatCloud : _cloudSurface;
            bool hideBackdrop = authored && backdrop != null;
            if (hideBackdrop)
            {
                // Native lighting colors this backdrop independently of the camera clear color. Its ocean
                // gradient crosses the authored sky views; retain the scene renderer's prior visibility.
                if (!state.Enabled.ContainsKey(backdrop)) state.Enabled[backdrop] = backdrop.enabled;
                backdrop.enabled = false;
            }
            foreach (Renderer r in sea)
            {
                // The arena scenery transaction already owns these flags and restores them independently.
                if (_hiddenOthers.Contains(r)) continue;
                if (!state.Materials.ContainsKey(r)) state.Materials[r] = r.sharedMaterials;
                if (!IsWaterSurface(r, state.Materials[r]))
                {
                    if (!state.Enabled.ContainsKey(r)) state.Enabled[r] = r.enabled;
                    r.enabled = false;
                    sceneryHidden++;
                    continue;
                }
                Material[] slots = new Material[Math.Max(1, state.Materials[r].Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = surface;
                r.sharedMaterials = slots;
                clouded++;
                tilingWater = r;
                Transform t = r.transform;
                if (moved.Contains(t)) continue;
                if (!MovableSea(t, seaSet, diorama)) { kept++; continue; }
                if (!state.Positions.ContainsKey(t)) state.Positions[t] = t.localPosition;
                t.localPosition = state.Positions[t]; // back to vanilla height before measuring and lowering
                if (!state.Scales.ContainsKey(t)) state.Scales[t] = t.localScale;
                t.localScale = state.Scales[t];
                // The authored wide opening exposed the finite water tile. Enlarge only a separately movable,
                // horizontal water surface around its mesh center; never scale a shared diorama/actor parent.
                if (SkySpikeCamera.OwnsArena(diorama) && Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > 0.9999f)
                {
                    Vector3 centerBefore = r.bounds.center;
                    Vector3 original = state.Scales[t];
                    t.localScale = new Vector3(original.x * 4f, original.y, original.z * 4f);
                    Vector3 correction = centerBefore - r.bounds.center;
                    correction.y = 0f;
                    t.position += correction;
                }
                moved.Add(t);
                movedOrder.Add(t);
            }
            if (authored)
                FitCombatCloudTiling(state, tilingWater, clouded, moved);

            // Top of the movable water SURFACE at its vanilla height, from mesh bounds (renderer bounds when there is no
            // mesh). Rocks and props share sea materials (seaDio_props carries matSea and stands about 33 units tall),
            // so only renderers with a water-distort component or a Water material are lowered. The remaining
            // sea scenery stays hidden instead of becoming tall white rocks or a second native enemy vessel.
            bool anyTop = false;
            Bounds top = new Bounds();
            List<string> surfaces = new List<string>();
            foreach (Renderer r in sea)
            {
                Material[] vanilla;
                if (!moved.Contains(r.transform) || !state.Materials.TryGetValue(r, out vanilla) || !IsWaterSurface(r, vanilla)) continue;
                surfaces.Add("'" + r.name + "'");
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Encapsulate(ref top, ref anyTop, mf.sharedMesh.bounds, r.transform.localToWorldMatrix);
                else if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    if (!anyTop) { top = r.bounds; anyTop = true; }
                    else top.Encapsulate(r.bounds);
                }
            }
            if (anyTop && !float.IsNaN(keelY))
            {
                float needed = top.max.y - (keelY - 2f);
                if (needed > drop)
                {
                    drop = needed;
                    Plugin.Log.LogInfo("[SkySpike] combat cloud floor lowered further: lowest keel y " + F2(keelY) + ", sea top y " +
                        F2(top.max.y) + ", combatCloudDrop " + F2(configured) + " -> drop " + F2(drop) + " (sea top at keel - 2).");
                }
            }
            foreach (Transform t in movedOrder) t.position += Vector3.down * drop;
            int lowered = movedOrder.Count;
            foreach (GameObject g in fx)
            {
                if (!state.Fx.ContainsKey(g)) state.Fx[g] = g.activeSelf;
                g.SetActive(false);
            }

            if (!state.BackgroundSaved) { state.Background = diorama.m_BackgroundColor; state.BackgroundSaved = true; }
            diorama.m_BackgroundColor = SkyBlue;
            Camera camera = OverlayCamera.Instance != null ? OverlayCamera.Instance.m_Camera : null;
            string cameraText = "no overlay camera";
            if (camera != null)
            {
                cameraText = "camera '" + camera.name + "' clearFlags " + camera.clearFlags;
                if (camera.clearFlags == CameraClearFlags.SolidColor) camera.backgroundColor = SkyBlue;
                else cameraText += " (background color not used)";
            }
            state.Applied = true;
            Plugin.Log.LogInfo("[SkySpike] combat cloud floor applied on '" + diorama.name + "': " + clouded + " renderers clouded, " +
                lowered + " lowered by " + F2(drop) + " (sea top y " + (anyTop ? F2(top.max.y) + " -> " + F2(top.max.y - drop) +
                " from [" + string.Join(", ", surfaces.ToArray()) + "]" : "unknown, no water surface renderer") +
                ", lowest keel y " + (float.IsNaN(keelY) ? "none" : F2(keelY)) + "), " + kept + " clouded in place (shared transform), " + fx.Count +
                " sea FX hidden, " + sceneryHidden + " non-water sea scenery renderers hidden, native backdrop '" +
                (backdrop != null ? backdrop.name : "none") + "' hidden " + hideBackdrop +
                ", surface '" + surface.name + "' shader '" + surface.shader.name + "', background color " +
                state.Background + " -> sky; " + cameraText + ".");
        }

        internal static void RestoreCloudFloor(DioramaBoat diorama)
        {
            FloorState state;
            if (diorama == null) return;
            if (!_floors.TryGetValue(diorama.GetInstanceID(), out state) || !state.Applied) return;
            bool releasedMesh = state.CloudMesh != null;
            bool cancelledPending = state.PendingWater != null;
            int restoredMeshes = ReleaseCloudMesh(state);
            foreach (KeyValuePair<Renderer, Material[]> e in state.Materials) if (e.Key != null) e.Key.sharedMaterials = e.Value;
            foreach (KeyValuePair<Renderer, bool> e in state.Enabled) if (e.Key != null) e.Key.enabled = e.Value;
            foreach (KeyValuePair<Transform, Vector3> e in state.Positions) if (e.Key != null) e.Key.localPosition = e.Value;
            foreach (KeyValuePair<Transform, Vector3> e in state.Scales) if (e.Key != null) e.Key.localScale = e.Value;
            foreach (KeyValuePair<GameObject, bool> e in state.Fx) if (e.Key != null) e.Key.SetActive(e.Value);
            if (state.BackgroundSaved)
            {
                diorama.m_BackgroundColor = state.Background;
                Camera camera = OverlayCamera.Instance != null ? OverlayCamera.Instance.m_Camera : null;
                EncounterSession session = EncounterSession.Instance;
                if (camera != null && camera.clearFlags == CameraClearFlags.SolidColor &&
                    session != null && session.m_ActiveDiorama == diorama && camera.backgroundColor == SkyBlue)
                    camera.backgroundColor = state.Background;
            }
            bool releasedCloud = state.CombatCloud != null;
            if (releasedCloud) UnityEngine.Object.Destroy(state.CombatCloud);
            bool releasedTexture = state.CombatCloudTexture != null;
            if (releasedTexture) UnityEngine.Object.Destroy(state.CombatCloudTexture);
            _floors.Remove(diorama.GetInstanceID());
            Plugin.Log.LogInfo("[SkySpike] combat cloud floor restored on '" + diorama.name + "' (" +
                state.Materials.Count + " materials, " + state.Enabled.Count + " renderer flags, " + state.Positions.Count + " positions, " +
                state.Scales.Count + " scales, " + state.Fx.Count + " FX; private cloud material released " + releasedCloud +
                ", private cloud texture released " + releasedTexture + "; native mesh bindings restored " + restoredMeshes +
                ", private cloud mesh released " + releasedMesh + ", pending water cancelled " + cancelledPending + ").");
        }

        // Sea: renderers with a water-distort component or an Ocean/Water/Sea material, excluding particles, the
        // vanilla hulls, the target roots, the backdrop and our own objects. FX: objects named fxSea*.
        static void FindSea(DioramaBoat diorama, List<Renderer> sea, List<GameObject> fx)
        {
            HashSet<Transform> excluded = new HashSet<Transform>();
            if (diorama.m_BoatGeos != null) foreach (Transform t in diorama.m_BoatGeos.Values) if (t != null) excluded.Add(t);
            if (diorama.m_LayoutTable != null)
                foreach (Diorama.Layout layout in diorama.m_LayoutTable.Values)
                    if (layout != null && layout.m_TargetRoot != null) excluded.Add(layout.m_TargetRoot);
            foreach (Transform t in diorama.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("fxSea", StringComparison.OrdinalIgnoreCase) && !UnderAny(t.parent, excluded) && !IsSpikeObject(t))
                    fx.Add(t.gameObject);
            }
            foreach (Renderer r in diorama.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r == diorama.m_Background) continue;
                if (IsSpikeObject(r.transform) || UnderAny(r.transform, excluded) || UnderFx(r.transform)) continue;
                bool water = r.GetComponent<WaterDistort>() != null || r.GetComponent<IslandWaterDistort>() != null ||
                    r.GetComponent<WaterDistortDiorama>() != null;
                if (!water)
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null || m == _cloudSurface) { if (m == _cloudSurface) water = true; continue; }
                        string n = m.name;
                        if (n.IndexOf("Ocean", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Sea", StringComparison.OrdinalIgnoreCase) >= 0) { water = true; break; }
                    }
                if (water) sea.Add(r);
            }
        }

        // vanilla: the renderer's materials before the cloud swap (the floor state's saved list).
        static bool IsWaterSurface(Renderer r, Material[] vanilla)
        {
            if (r.GetComponent<WaterDistort>() != null || r.GetComponent<IslandWaterDistort>() != null ||
                r.GetComponent<WaterDistortDiorama>() != null) return true;
            if (vanilla != null)
                foreach (Material m in vanilla)
                    if (m != null && m.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool MovableSea(Transform t, HashSet<Renderer> sea, Diorama diorama)
        {
            if (t == diorama.transform) return false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                if (!sea.Contains(r) && !(r is ParticleSystemRenderer)) return false;
            return true;
        }

        static bool UnderAny(Transform t, HashSet<Transform> roots)
        {
            for (Transform p = t; p != null; p = p.parent) if (roots.Contains(p)) return true;
            return false;
        }

        static bool UnderFx(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (p.name.StartsWith("fxSea", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsSpikeObject(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (p.name.StartsWith("SkySpike", StringComparison.Ordinal)) return true;
            return false;
        }

        static string Describe(Renderer r)
        {
            List<string> mats = new List<string>();
            foreach (Material m in r.sharedMaterials) mats.Add(m != null ? m.name : "null");
            List<string> comps = new List<string>();
            if (r.GetComponent<WaterDistort>() != null) comps.Add("WaterDistort");
            if (r.GetComponent<IslandWaterDistort>() != null) comps.Add("IslandWaterDistort");
            if (r.GetComponent<WaterDistortDiorama>() != null) comps.Add("WaterDistortDiorama");
            MeshFilter mf = r.GetComponent<MeshFilter>();
            bool any = false;
            Bounds b = new Bounds();
            if (mf != null && mf.sharedMesh != null) Encapsulate(ref b, ref any, mf.sharedMesh.bounds, r.transform.localToWorldMatrix);
            return "'" + r.name + "' " + r.GetType().Name + (comps.Count > 0 ? " [" + string.Join(",", comps.ToArray()) + "]" : "") +
                " mats [" + string.Join(",", mats.ToArray()) + "]" + (any ? " bounds center " + Fmt(b.center) + " size " + Fmt(b.size) : "");
        }
    }

    [HarmonyPatch(typeof(WaterDistortDiorama), "Start")]
    internal static class SkySpikeCloudWaterStartPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(WaterDistortDiorama __instance)
        {
            try { SkySpike.OnCloudWaterStarted(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] pending cloud water setup failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(Diorama), "ResetDioramaAssets")]
    internal static class SkySpikeCloudFloorResetPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(Diorama __instance)
        {
            DioramaBoat boat = __instance as DioramaBoat;
            if (boat == null) return;
            try { SkySpike.CleanupCombatPresentation(boat); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] combat presentation reset failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(Diorama), "CleanUpDiorama")]
    internal static class SkySpikeCombatCleanupPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(Diorama __instance)
        {
            DioramaBoat boat = __instance as DioramaBoat;
            if (boat == null) return;
            try { SkySpike.CleanupCombatPresentation(boat); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] combat presentation cleanup failed: " + e); }
        }
    }

    // Save/Exit and confirmed Exit both reload FTK_main here without invoking Diorama.CleanUpDiorama.
    // Release scene-owned bookkeeping while its Unity objects are still alive; never block native restart.
    [HarmonyPatch(typeof(GameLogic), "RestartFadeOutFinish")]
    internal static class SkySpikeRestartCleanupPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Prefix()
        {
            try
            {
                EncounterSession session = EncounterSession.Instance;
                DioramaBoat boat = session != null ? session.m_ActiveDiorama as DioramaBoat : null;
                if (boat != null) SkySpike.CleanupCombatPresentation(boat);
            }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] combat presentation restart cleanup failed: " + e); }
        }
    }

    // Fades each upper-works chunk whose bounds lie between the combat camera and a combatant.
    // Fade materials are owned here; chunks share one copy per source material and carry alpha
    // in a property block. Authored cutaways omit their upperworks instead of using this path.
    internal sealed class SkySpikeSeeThrough : MonoBehaviour
    {
        internal object Split;
        readonly List<MeshRenderer> _upper = new List<MeshRenderer>();
        readonly List<Material> _opaque = new List<Material>();
        readonly List<Material> _fadeFor = new List<Material>();
        readonly Dictionary<Material, Material> _fades = new Dictionary<Material, Material>();
        readonly List<Vector3> _points = new List<Vector3>();
        float[] _alpha = new float[0];
        bool[] _fading = new bool[0];
        MaterialPropertyBlock _block, _clear;
        Diorama _diorama;
        bool _active = true, _failed, _cameraLogged;
        float _minAlpha = 0.2f, _head = 1.2f, _nextLog;
        int _lastFading = -1;

        internal void Init(object split, List<MeshRenderer> upper)
        {
            Split = split;
            foreach (MeshRenderer r in upper)
            {
                Material opaque = r.sharedMaterial;
                Material fade;
                if (!_fades.TryGetValue(opaque, out fade))
                {
                    fade = new Material(opaque);
                    fade.name = opaque.name + " (fade)";
                    fade.SetFloat("_Mode", 2f);
                    fade.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    fade.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    fade.SetInt("_ZWrite", 0);
                    fade.DisableKeyword("_ALPHATEST_ON");
                    fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fade.EnableKeyword("_ALPHABLEND_ON");
                    fade.renderQueue = 3000;
                    _fades.Add(opaque, fade);
                }
                _upper.Add(r);
                _opaque.Add(opaque);
                _fadeFor.Add(fade);
            }
            _alpha = new float[_upper.Count];
            _fading = new bool[_upper.Count];
            for (int i = 0; i < _alpha.Length; i++) _alpha[i] = 1f;
        }

        internal void Configure(Diorama diorama, bool active, float minAlpha, float headHeight)
        {
            _diorama = diorama;
            _active = active;
            _minAlpha = minAlpha;
            _head = headHeight;
            ResetAll();
        }

        void OnEnable() { ResetAll(); }

        void OnDestroy()
        {
            foreach (Material m in _fades.Values) if (m != null) Destroy(m);
        }

        void ResetAll()
        {
            for (int i = 0; i < _upper.Count; i++) { _alpha[i] = 1f; SetOpaque(i); }
            _lastFading = -1;
        }

        void LateUpdate()
        {
            if (!_active || _failed || _upper.Count == 0 || _diorama == null) return;
            try
            {
                Camera camera = OverlayCamera.Instance != null ? OverlayCamera.Instance.m_Camera : null;
                string which = "OverlayCamera.m_Camera";
                if (camera == null || !camera.isActiveAndEnabled) { camera = Camera.main; which = "Camera.main"; }
                if (camera == null) return;
                if (!_cameraLogged)
                {
                    _cameraLogged = true;
                    Plugin.Log.LogInfo("[SkySpike] see-through " + name + ": using " + which + " '" + camera.name + "', " + _upper.Count +
                        " upper chunks, " + _fades.Count + " fade materials.");
                }
                if (_block == null) { _block = new MaterialPropertyBlock(); _clear = new MaterialPropertyBlock(); }

                Vector3 origin = camera.transform.position;
                _points.Clear();
                AddPoints(_diorama.m_PlayerTargets);
                AddPoints(_diorama.m_EnemyTargets);
                float step = Time.unscaledDeltaTime * (1f - _minAlpha) / 0.2f;
                int fading = 0;
                for (int i = 0; i < _upper.Count; i++)
                {
                    MeshRenderer r = _upper[i];
                    if (r == null) continue;
                    bool blocks = Blocks(origin, r.bounds);
                    if (blocks) fading++;
                    _alpha[i] = Mathf.MoveTowards(_alpha[i], blocks ? _minAlpha : 1f, step);
                    if (_alpha[i] >= 0.999f) { SetOpaque(i); continue; }
                    if (!_fading[i]) { r.sharedMaterial = _fadeFor[i]; _fading[i] = true; }
                    Color c = _opaque[i].color;
                    c.a = _alpha[i];
                    _block.SetColor("_Color", c);
                    r.SetPropertyBlock(_block);
                    // seeThroughAlpha 0 hides a chunk outright, in case the build lacks the blend variant.
                    bool hide = _minAlpha <= 0.01f && _alpha[i] < 0.5f;
                    if (r.enabled == hide) r.enabled = !hide;
                }
                if (fading != _lastFading && Time.unscaledTime >= _nextLog)
                {
                    _nextLog = Time.unscaledTime + 1f;
                    _lastFading = fading;
                    Plugin.Log.LogInfo("[SkySpike] see-through " + name + ": " + fading + " of " + _upper.Count + " upper chunks between the camera and " +
                        _points.Count / 3 + " combatants, fading to " + _minAlpha.ToString("0.00") + ".");
                }
            }
            catch (Exception e)
            {
                _failed = true;
                for (int i = 0; i < _upper.Count; i++) SetOpaque(i);
                Plugin.Log.LogError("[SkySpike] see-through " + name + " disabled after an error: " + e);
            }
        }

        void AddPoints(List<Transform> targets)
        {
            if (targets == null) return;
            foreach (Transform t in targets)
            {
                if (t == null) continue;
                _points.Add(t.position + Vector3.up * (_head * 0.15f));
                _points.Add(t.position + Vector3.up * (_head * 0.5f));
                _points.Add(t.position + Vector3.up * _head);
            }
        }

        bool Blocks(Vector3 origin, Bounds bounds)
        {
            foreach (Vector3 point in _points)
            {
                Vector3 delta = point - origin;
                float distance = delta.magnitude;
                if (distance < 1e-3f) continue;
                float hit;
                if (bounds.IntersectRay(new Ray(origin, delta / distance), out hit) && hit < distance - 0.05f) return true;
            }
            return false;
        }

        void SetOpaque(int i)
        {
            MeshRenderer r = _upper[i];
            if (r == null) return;
            if (_fading[i]) { r.sharedMaterial = _opaque[i]; _fading[i] = false; }
            if (_clear != null) r.SetPropertyBlock(_clear);
            r.enabled = true;
        }
    }
}
