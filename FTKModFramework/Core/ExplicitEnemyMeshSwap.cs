using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Preflight and transactional application for explicitly selected skinned or rigid renderers.</summary>
    internal static class ExplicitEnemyMeshSwap
    {
        internal static bool ValidateAssignments(EnemyRendererMesh[] assignments, out string error)
        {
            error = null;
            if (assignments == null || assignments.Length == 0)
            { error = "at least one renderer assignment is required"; return false; }
            Dictionary<string, bool> paths = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (EnemyRendererMesh assignment in assignments)
            {
                if (assignment == null || !ValidPath(assignment.RendererPath))
                { error = "renderer paths must be exact relative names, or '.' for the root"; return false; }
                if (assignment.RendererKind != EnemyRendererKind.SkinnedMeshRenderer &&
                    assignment.RendererKind != EnemyRendererKind.MeshRenderer)
                { error = "renderer kind must be SkinnedMeshRenderer or MeshRenderer"; return false; }
                if (paths.ContainsKey(assignment.RendererPath))
                { error = "duplicate renderer path '" + assignment.RendererPath + "'"; return false; }
                paths.Add(assignment.RendererPath, true);
                if (!ValidFileName(assignment.GlbFileName) ||
                    (!string.IsNullOrEmpty(assignment.TextureFileName) && !ValidFileName(assignment.TextureFileName)))
                { error = "model files must be relative paths inside the models directory"; return false; }
                EnemyRendererMaterial[] slots = assignment.Slots;
                if (assignment.RendererKind == EnemyRendererKind.MeshRenderer && slots != null)
                { error = "static MeshRenderer assignments do not support native material slot mode"; return false; }
                if (slots != null)
                {
                    if (slots.Length < 2 || slots.Length > 4) { error = "native slot mode requires 2..4 slots"; return false; }
                    bool[] primitives = new bool[slots.Length], native = new bool[slots.Length];
                    foreach (EnemyRendererMaterial slot in slots)
                    {
                        if (slot == null || slot.PrimitiveIndex < 0 || slot.PrimitiveIndex >= slots.Length ||
                            slot.NativeMaterialSlot < 0 || slot.NativeMaterialSlot >= slots.Length ||
                            primitives[slot.PrimitiveIndex] || native[slot.NativeMaterialSlot])
                        { error = "slot descriptors must bijectively cover primitive and native slot indices"; return false; }
                        primitives[slot.PrimitiveIndex] = native[slot.NativeMaterialSlot] = true;
                        if (!string.IsNullOrEmpty(slot.TextureFileName) && !ValidFileName(slot.TextureFileName))
                        { error = "slot textures must be relative model paths"; return false; }
                    }
                }
                try
                {
                    if (slots != null) foreach (EnemyRendererMaterial slot in slots)
                        if (!string.IsNullOrEmpty(slot.TextureFileName)) CustomModelLoader.ResolveModelPath(slot.TextureFileName);
                    CustomModelLoader.ResolveModelPath(assignment.GlbFileName);
                    if (!string.IsNullOrEmpty(assignment.TextureFileName))
                        CustomModelLoader.ResolveModelPath(assignment.TextureFileName);
                }
                catch (Exception e) { error = e.Message; return false; }
            }
            return true;
        }

        private static bool ValidFileName(string name)
        {
            return name != "." && !string.IsNullOrEmpty(name) && !Path.IsPathRooted(name) && ValidPath(name);
        }

        private static bool ValidPath(string path)
        {
            if (path == ".") return true;
            if (string.IsNullOrEmpty(path) || path.IndexOf('\\') >= 0) return false;
            foreach (string segment in path.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..") return false;
            return true;
        }

        internal static string RelativePath(Transform root, Transform child)
        {
            if (root == child) return ".";
            List<string> names = new List<string>();
            while (child != null && child != root)
            {
                if (child.name.IndexOf('/') >= 0 || child.name.IndexOf('\\') >= 0) return null;
                names.Add(child.name);
                child = child.parent;
            }
            if (child != root) return null;
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private sealed class Prepared
        {
            internal Renderer renderer;
            internal SkinnedMeshRenderer skinnedRenderer;
            internal MeshFilter meshFilter;
            internal Mesh mesh;
            internal Mesh originalMesh;
            internal Transform[] bones;
            internal Material[] originalMaterials;
            internal Material[] materials;
            internal Bounds originalBounds;
            internal ScrollingUVs[] scrollers;
        }

        internal static bool Apply(string enemyId, CharacterEventListener cel, EnemyRendererMesh[] assignments,
            Action<Renderer, Material> prepareMaterial = null, bool preserveAuthoredMainPalette = false)
        {
            return ApplyToObject(enemyId, cel == null ? null : cel.gameObject, assignments, prepareMaterial, preserveAuthoredMainPalette);
        }

        internal static bool ApplyToObject(string identity, GameObject root, EnemyRendererMesh[] assignments,
            Action<Renderer, Material> prepareMaterial = null, bool preserveAuthoredMainPalette = false)
        {
            string enemyId = identity;
            string error;
            if (!ValidateAssignments(assignments, out error))
            { Plugin.Log.LogWarning("[enemy-visual] explicit meshes rejected: " + error); return false; }
            if (root == null) return false;
            // Do not allocate or rebind twice on the same spawned clone.
            EnemyMeshResources existing = root.GetComponent<EnemyMeshResources>();
            if (existing != null && (!existing.VisualResourcesOnly || !existing.ValidLease()))
                return !existing.VisualResourcesOnly && existing.Applied && existing.EnsureRetained();
            List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            List<Prepared> prepared = new List<Prepared>();
            EnemyMeshResources lifetime = null;
            EnemyMeshResources.Batch batch = null;
            int attempted = 0;
            try
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (EnemyRendererMesh assignment in assignments)
                {
                    Transform target = null;
                    int pathMatches = 0;
                    foreach (Transform candidate in transforms)
                        if (RelativePath(root.transform, candidate) == assignment.RendererPath)
                        { target = candidate; pathMatches++; }
                    if (pathMatches != 1)
                        throw new InvalidOperationException("renderer path '" + assignment.RendererPath + "' matched " + pathMatches + " transforms");
                    Prepared p = new Prepared();
                    if (assignment.RendererKind == EnemyRendererKind.SkinnedMeshRenderer)
                    {
                        SkinnedMeshRenderer[] matches = target.GetComponents<SkinnedMeshRenderer>();
                        if (matches.Length != 1)
                            throw new InvalidOperationException("renderer path '" + assignment.RendererPath +
                                "' requires exactly one SkinnedMeshRenderer");
                        p.skinnedRenderer = matches[0];
                        p.renderer = p.skinnedRenderer;
                        p.originalMesh = p.skinnedRenderer.sharedMesh;
                        p.bones = p.skinnedRenderer.bones;
                        p.originalMaterials = p.skinnedRenderer.sharedMaterials;
                        p.originalBounds = p.skinnedRenderer.localBounds;
                        if (p.originalMesh == null)
                            throw new InvalidOperationException("target has no reference mesh: " + assignment.RendererPath);
                    }
                    else
                    {
                        MeshRenderer[] renderers = target.GetComponents<MeshRenderer>();
                        MeshFilter[] filters = target.GetComponents<MeshFilter>();
                        if (renderers.Length != 1 || filters.Length != 1 ||
                            target.GetComponents<SkinnedMeshRenderer>().Length != 0)
                            throw new InvalidOperationException("renderer path '" + assignment.RendererPath +
                                "' requires exactly one MeshRenderer, one MeshFilter, and no SkinnedMeshRenderer");
                        p.renderer = renderers[0];
                        p.meshFilter = filters[0];
                        p.originalMesh = p.meshFilter.sharedMesh;
                        p.originalMaterials = p.renderer.sharedMaterials;
                        if (p.originalMesh == null)
                            throw new InvalidOperationException("static target has no reference mesh: " + assignment.RendererPath);
                    }
                    EnemyRendererMaterial[] slots = assignment.Slots;
                    int[] primitiveToSlot = null;
                    if (slots != null)
                    {
                        if (p.originalMaterials.Length != slots.Length)
                            throw new InvalidOperationException("native slot mode must preserve every native material slot");
                        primitiveToSlot = new int[slots.Length];
                        foreach (EnemyRendererMaterial slot in slots) primitiveToSlot[slot.PrimitiveIndex] = slot.NativeMaterialSlot;
                    }
                    p.mesh = p.skinnedRenderer != null
                        ? RuntimeGltfMeshLoader.LoadSkinnedGlb(assignment.GlbFileName, p.bones,
                            p.originalMesh.bindposes, true, primitiveToSlot)
                        : RuntimeGltfMeshLoader.LoadStaticGlb(assignment.GlbFileName, true);
                    if (p.mesh == null) throw new InvalidOperationException("strict GLB preflight failed: " + assignment.GlbFileName);
                    owned.Add(p.mesh);
                    // Enemy callers prepare tint on these private copies before commit, without .materials cloning again.
                    // One GLB primitive has exactly one material. Multiple native slots must not redraw it.
                    if (p.originalMaterials.Length == 0 || p.originalMaterials[0] == null)
                        throw new InvalidOperationException("target requires a usable first material slot");
                    if (p.skinnedRenderer == null && p.originalMaterials.Length != 1)
                        throw new InvalidOperationException("static MeshRenderer target requires exactly one native material slot");
                    p.materials = new Material[slots == null ? 1 : slots.Length];
                    for (int i = 0; i < p.materials.Length; i++)
                    {
                        if (p.originalMaterials[i] == null) throw new InvalidOperationException("native slot has null material");
                        EnemyRendererMaterial option = null;
                        if (slots != null) foreach (EnemyRendererMaterial slot in slots) if (slot.NativeMaterialSlot == i) option = slot;
                        Material material = new Material(p.originalMaterials[i]);
                        p.materials[i] = material;
                        owned.Add(material);
                        ExplicitMaterialOptions.Apply(material, option == null ? assignment.DisableNativeEmission : option.DisableNativeEmission);
                        string textureName = option == null ? assignment.TextureFileName : option.TextureFileName;
                        if (string.IsNullOrEmpty(textureName)) continue;
                        string path = CustomModelLoader.ResolveModelPath(textureName);
                        if (!File.Exists(path)) throw new FileNotFoundException("requested texture missing", path);
                        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        owned.Add(texture);
                        if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("requested texture could not decode: " + textureName);
                        texture.name = "ftkmf_" + textureName;
                        if (!material.HasProperty("_MainTex")) throw new InvalidOperationException("target shader lacks _MainTex");
                        material.SetTexture("_MainTex", texture);
                        if (preserveAuthoredMainPalette) ExplicitMaterialOptions.PreserveMainPalette(material);
                    }
                    if (p.skinnedRenderer != null)
                    {
                        p.scrollers = p.renderer.GetComponents<ScrollingUVs>();
                        foreach (ScrollingUVs scroller in p.scrollers)
                        {
                            // The ordinary constructor also changes material topology.
                            // Never collapse a slot still addressed by a native component.
                            if (slots == null && scroller.materialIndex >= p.materials.Length)
                                throw new InvalidOperationException("single-slot assignment cannot preserve native ScrollingUVs materialIndex " +
                                    scroller.materialIndex + "; use EnemyRendererMesh.WithNativeMaterialSlots");
                            ExplicitScrollingUvs.Validate(scroller, p.materials);
                        }
                    }
                    else if (p.skinnedRenderer == null && p.renderer.GetComponents<ScrollingUVs>().Length != 0)
                        throw new InvalidOperationException("static MeshRenderer target with ScrollingUVs is unsupported");
                    // Settings are prepared on private copies before ANY renderer is changed.
                    if (prepareMaterial != null)
                        foreach (Material material in p.materials) prepareMaterial(p.renderer, material);
                    prepared.Add(p);
                }
                // No renderer state has changed above. Own resources before beginning the commit.
                lifetime = existing ?? root.AddComponent<EnemyMeshResources>();
                List<Renderer> targets = new List<Renderer>();
                foreach (Prepared p in prepared) targets.Add(p.renderer);
                batch = lifetime.Append(owned.ToArray(), targets.ToArray(), existing == null);
                foreach (Prepared p in prepared)
                {
                    attempted++;
                    if (p.skinnedRenderer != null)
                    {
                        p.skinnedRenderer.sharedMesh = p.mesh;
                        p.skinnedRenderer.bones = (Transform[])p.bones.Clone();
                        // Keep the native animated envelope. Mesh bind bounds are not an animation envelope
                        // and cannot safely replace renderer-local bounds across differing root-bone spaces.
                        p.skinnedRenderer.localBounds = p.originalBounds;
                    }
                    else p.meshFilter.sharedMesh = p.mesh;
                    if (p.materials != null) p.renderer.sharedMaterials = p.materials;
                }
                foreach (Prepared p in prepared)
                    if (p.materials != null)
                        lifetime.AddScrollingTarget(p.renderer, p.materials, p.scrollers ?? new ScrollingUVs[0]);
                lifetime.Applied = true;
                lifetime.VisualResourcesOnly = false;
                Plugin.Log.LogInfo("[enemy-visual] explicit mesh swap applied " + prepared.Count + " renderers for '" + enemyId + "'.");
                return true;
            }
            catch (Exception e)
            {
                bool restored = true;
                for (int i = attempted - 1; i >= 0; i--)
                {
                    Prepared p = prepared[i];
                    if (p.renderer == null) continue;
                    try
                    {
                        if (p.skinnedRenderer != null)
                        {
                            p.skinnedRenderer.sharedMesh = p.originalMesh;
                            p.skinnedRenderer.bones = p.bones;
                            p.skinnedRenderer.localBounds = p.originalBounds;
                        }
                        else p.meshFilter.sharedMesh = p.originalMesh;
                        p.renderer.sharedMaterials = p.originalMaterials;
                    }
                    catch (Exception rollback) { restored = false; Plugin.Log.LogError("[enemy-visual] explicit rollback: " + rollback.Message); }
                }
                if (batch != null) batch.Rollback(restored);
                else
                {
                    if (lifetime != null && existing == null) HotReload.PaladinResourceState.DestroyTracked(lifetime);
                    foreach (UnityEngine.Object asset in owned) if (asset != null) HotReload.PaladinResourceState.DestroyTracked(asset);
                }
                Plugin.Log.LogWarning("[enemy-visual] explicit mesh set rejected for '" + enemyId + "': " + e.Message + "; rollback attempted (see any incomplete rollback error).");
                return false;
            }
        }
    }

    /// <summary>Reference-counted ownership for custom resources shared by native avatar cloning.</summary>
    internal sealed class EnemyMeshResources : MonoBehaviour
    {
        private sealed class Lease
        {
            internal UnityEngine.Object[] resources;
            internal int references;
            internal readonly List<EnemyMeshResources> owners = new List<EnemyMeshResources>();
        }
        private static readonly Dictionary<int, Lease> Leases = new Dictionary<int, Lease>();
        internal static int ReloadLeaseCount { get { PruneDestroyedOwners(); return Leases.Count; } }
        internal static int ReloadResourceCount
        {
            get
            {
                PruneDestroyedOwners();
                int count = 0;
                foreach (Lease lease in Leases.Values) count += lease.resources.Length;
                return count;
            }
        }
        private static int _nextLease;
        [SerializeField] private int _leaseId;
        [SerializeField] private Renderer[] _targets;
        [SerializeField] internal bool Applied;
        // Ownership-only tint/legacy assignments are not a completed explicit mesh plan.
        [SerializeField] internal bool VisualResourcesOnly;
        [NonSerialized] private bool _acquired;
        [SerializeField] private Renderer[] _scrollRenderers;
        [SerializeField] private Material[] _scrollMaterials;
        [SerializeField] private int[] _scrollCounts;
        [SerializeField] private ScrollingUVs[] _scrollers;
        [NonSerialized] private Dictionary<Renderer, Material[]> _privateScrolling = new Dictionary<Renderer, Material[]>();

        internal bool ValidLease()
        {
            Lease lease;
            return EnsureRetained() && _acquired && Leases.TryGetValue(_leaseId, out lease) &&
                lease.references > 0 && lease.owners.Contains(this);
        }

        // A synchronous allocation transaction. Callers supply only newly allocated resources;
        // no renderer getter, native asset, or resource inherited from another lease is adopted.
        internal sealed class Batch
        {
            private readonly EnemyMeshResources owner;
            private readonly Lease lease;
            private readonly UnityEngine.Object[] resources, added;
            private readonly Renderer[] targets, scrollRenderers;
            private readonly Material[] scrollMaterials;
            private readonly int[] scrollCounts;
            private readonly ScrollingUVs[] scrollers;
            private readonly Dictionary<Renderer, Material[]> privateScrolling;
            private readonly bool applied, visualOnly, fresh;
            private bool rolledBack;
            internal Batch(EnemyMeshResources owner, UnityEngine.Object[] added, Renderer[] targets, bool fresh)
            {
                this.owner = owner; this.fresh = fresh; this.added = added;
                applied = owner.Applied; visualOnly = owner.VisualResourcesOnly;
                this.targets = owner._targets; scrollRenderers = owner._scrollRenderers;
                scrollMaterials = owner._scrollMaterials; scrollCounts = owner._scrollCounts;
                scrollers = owner._scrollers;
                privateScrolling = owner._privateScrolling == null ? null : new Dictionary<Renderer, Material[]>(owner._privateScrolling);
                // Reject duplicate, null, or previously owned resources BEFORE any ownership change.
                List<UnityEngine.Object> unique = new List<UnityEngine.Object>();
                foreach (UnityEngine.Object item in added)
                {
                    if (item == null || unique.Contains(item)) throw new InvalidOperationException("Invalid new resource batch");
                    foreach (Lease current in Leases.Values)
                        foreach (UnityEngine.Object prior in current.resources)
                            if (object.ReferenceEquals(item, prior)) throw new InvalidOperationException("Cannot adopt an existing lease resource");
                    unique.Add(item);
                }
                if (fresh)
                {
                    owner.SetResources(added); owner.VisualResourcesOnly = true;
                    lease = Leases[owner._leaseId]; resources = new UnityEngine.Object[0];
                }
                else
                {
                    if (!owner.ValidLease()) throw new InvalidOperationException("Missing live resource owner");
                    lease = Leases[owner._leaseId]; resources = lease.resources;
                    List<UnityEngine.Object> combined = new List<UnityEngine.Object>(resources); combined.AddRange(added);
                    lease.resources = combined.ToArray();
                }
                List<Renderer> merged = new List<Renderer>(owner._targets ?? new Renderer[0]);
                foreach (Renderer target in targets) if (!merged.Contains(target)) merged.Add(target);
                owner._targets = merged.ToArray();
            }
            internal void Rollback(bool rendererStateRestored)
            {
                if (rolledBack) return; rolledBack = true;
                owner.Applied = applied; owner.VisualResourcesOnly = fresh || visualOnly;
                if (!rendererStateRestored)
                {
                    Plugin.Log.LogError("[model-mesh] incomplete renderer rollback; new allocations remain leased until final release; plan not committed");
                    return;
                }
                owner._targets = targets; owner._scrollRenderers = scrollRenderers;
                owner._scrollMaterials = scrollMaterials; owner._scrollCounts = scrollCounts;
                owner._scrollers = scrollers; owner._privateScrolling = privateScrolling;
                if (fresh) { owner.Release(); HotReload.PaladinResourceState.DestroyTracked(owner); }
                else
                {
                    lease.resources = resources;
                    foreach (UnityEngine.Object item in added) if (item != null) HotReload.PaladinResourceState.DestroyTracked(item);
                }
            }
        }
        internal Batch Append(UnityEngine.Object[] added, Renderer[] targets, bool fresh)
        { return new Batch(this, added, targets, fresh); }

        internal void AddScrollingTarget(Renderer renderer, Material[] materials, ScrollingUVs[] scrollers)
        {
            if (_privateScrolling == null) _privateScrolling = new Dictionary<Renderer, Material[]>();
            if (_scrollRenderers != null)
            {
                int offset = 0;
                for (int i = 0; i < _scrollRenderers.Length; i++)
                {
                    if (_scrollRenderers[i] == renderer)
                    {
                        List<Material> replacement = new List<Material>(_scrollMaterials);
                        replacement.RemoveRange(offset, _scrollCounts[i]); replacement.InsertRange(offset, materials);
                        _scrollMaterials = replacement.ToArray();
                        _scrollCounts = (int[])_scrollCounts.Clone(); _scrollCounts[i] = materials.Length;
                        List<ScrollingUVs> known = new List<ScrollingUVs>(_scrollers ?? new ScrollingUVs[0]);
                        foreach (ScrollingUVs scroller in scrollers) if (!known.Contains(scroller)) known.Add(scroller);
                        _scrollers = known.ToArray(); _privateScrolling[renderer] = (Material[])materials.Clone(); return;
                    }
                    offset += _scrollCounts[i];
                }
            }
            List<Renderer> rs = new List<Renderer>(_scrollRenderers ?? new Renderer[0]); rs.Add(renderer); _scrollRenderers = rs.ToArray();
            List<int> counts = new List<int>(_scrollCounts ?? new int[0]); counts.Add(materials.Length); _scrollCounts = counts.ToArray();
            List<Material> ms = new List<Material>(_scrollMaterials ?? new Material[0]); ms.AddRange(materials); _scrollMaterials = ms.ToArray();
            List<ScrollingUVs> ss = new List<ScrollingUVs>(_scrollers ?? new ScrollingUVs[0]); ss.AddRange(scrollers); _scrollers = ss.ToArray();
            _privateScrolling[renderer] = (Material[])materials.Clone();
        }

        internal bool OwnsScroller(ScrollingUVs scroller)
        {
            if (!Applied || !_acquired || _scrollers == null) return false;
            foreach (ScrollingUVs current in _scrollers) if (current == scroller) return true;
            return false;
        }

        internal Material[] ScrollingMaterials(Renderer renderer, bool makePrivate)
        { return PrivateMaterials(renderer, makePrivate); }

        // Tint and scrolling share provenance so either path privatizes a clone only once.
        internal Material[] PrivateMaterials(Renderer renderer, bool makePrivate)
        {
            if (!Applied || !EnsureRetained() || !Owns(renderer)) return null;
            if (_privateScrolling == null) _privateScrolling = new Dictionary<Renderer, Material[]>();
            Material[] expected;
            bool initialized = _privateScrolling.TryGetValue(renderer, out expected);
            int materialOffset = -1;
            if (!initialized)
            {
                int offset = 0;
                if (_scrollRenderers == null) return null;
                for (int i = 0; i < _scrollRenderers.Length; i++)
                {
                    if (_scrollRenderers[i] == renderer)
                    { materialOffset = offset; expected = new Material[_scrollCounts[i]]; Array.Copy(_scrollMaterials, offset, expected, 0, expected.Length); break; }
                    offset += _scrollCounts[i];
                }
            }
            Material[] current = renderer.sharedMaterials;
            if (expected == null || current.Length != expected.Length) return null;
            for (int i = 0; i < current.Length; i++) if (current[i] == null || current[i] != expected[i]) return null;
            if (initialized || !makePrivate) return current;
            Lease lease;
            if (!Leases.TryGetValue(_leaseId, out lease)) return null;
            Material[] copies = new Material[current.Length];
            UnityEngine.Object[] priorResources = lease.resources;
            Material[] priorSerialized = _scrollMaterials;
            bool assignmentAttempted = false;
            try
            {
                for (int i = 0; i < copies.Length; i++) copies[i] = new Material(current[i]);
                Material[] serialized = (Material[])priorSerialized.Clone();
                Array.Copy(copies, 0, serialized, materialOffset, copies.Length);
                List<UnityEngine.Object> resources = new List<UnityEngine.Object>(priorResources); resources.AddRange(copies);
                lease.resources = resources.ToArray(); // Own explicit allocations before assigning the renderer.
                assignmentAttempted = true;
                renderer.sharedMaterials = copies;
                // Native clones of this privatized avatar must inherit its current material provenance.
                _scrollMaterials = serialized;
                _privateScrolling.Add(renderer, copies);
                return copies;
            }
            catch
            {
                bool restored = true;
                try { if (assignmentAttempted && renderer != null) renderer.sharedMaterials = current; }
                catch (Exception rollback)
                {
                    restored = false;
                    Plugin.Log.LogError("[model-mesh] incomplete scrolling rollback; explicit copies remain leased until final release: " + rollback.Message);
                }
                _scrollMaterials = priorSerialized;
                _privateScrolling.Remove(renderer); // Unknown assignments cannot authorize the narrow prefix.
                if (restored)
                {
                    lease.resources = priorResources;
                    foreach (Material copy in copies) if (copy != null) HotReload.PaladinResourceState.DestroyTracked(copy);
                }
                // On uncertainty retain the registered batch: live renderers may still reference it.
                throw;
            }
        }

        internal void SetTargets(Renderer[] targets) { _targets = targets; }
        internal bool OwnsExplicitMesh(SkinnedMeshRenderer renderer)
        {
            if(VisualResourcesOnly || !Applied || renderer==null || renderer.sharedMesh==null || !Owns(renderer) || !EnsureRetained())return false;
            Lease lease;if(!Leases.TryGetValue(_leaseId,out lease))return false;
            foreach(UnityEngine.Object resource in lease.resources)if(resource==renderer.sharedMesh)return true;
            return false;
        }
        internal bool Owns(Renderer renderer)
        {
            if (_targets == null) return false;
            foreach (Renderer target in _targets) if (target == renderer) return true;
            return false;
        }
        internal void SetResources(UnityEngine.Object[] resources)
        {
            if (_acquired || _leaseId != 0) throw new InvalidOperationException("Resource owner is already initialized");
            do { _nextLease = _nextLease == int.MaxValue ? 1 : _nextLease + 1; } while (Leases.ContainsKey(_nextLease));
            Lease lease = new Lease();
            lease.resources = resources;
            lease.references = 1;
            lease.owners.Add(this);
            Leases.Add(_nextLease, lease);
            _leaseId = _nextLease;
            _acquired = true;
        }
        internal bool EnsureRetained()
        {
            if (_acquired) return true;
            if (_leaseId == 0) return false;
            Lease lease;
            if (!Leases.TryGetValue(_leaseId, out lease))
            {
                Applied = false;
                Plugin.Log.LogWarning("[model-mesh] missing resource lease " + _leaseId +
                    "; cloned avatar cannot retain its custom assets. No original assets were adopted.");
                return false;
            }
            lease.references++;
            lease.owners.Add(this);
            _acquired = true;
            return true;
        }

        internal static void RetainHierarchy(GameObject root)
        {
            if (root == null) return;
            foreach (EnemyMeshResources owner in root.GetComponentsInChildren<EnemyMeshResources>(true))
                owner.EnsureRetained();
        }

        // Native weapon break recursively detaches individual renderer transforms. They can outlive
        // the weapon root, so acquire an additional reference before that ownership boundary splits.
        internal bool RetainForDetachedRenderer(Renderer renderer)
        {
            if (renderer == null || !Owns(renderer) || !EnsureRetained()) return false;
            if (renderer.gameObject == gameObject) return true;
            EnemyMeshResources fragment = renderer.GetComponent<EnemyMeshResources>();
            if (fragment != null) return fragment._leaseId == _leaseId && fragment.EnsureRetained();
            fragment = renderer.gameObject.AddComponent<EnemyMeshResources>();
            fragment._leaseId = _leaseId;
            fragment._targets = new Renderer[] { renderer };
            fragment.Applied = Applied;
            fragment.VisualResourcesOnly = VisualResourcesOnly;
            return fragment.EnsureRetained();
        }

        internal void Release()
        {
            Applied = false;
            if (!_acquired) return;
            _acquired = false;
            Lease lease;
            int id = _leaseId;
            _leaseId = 0;
            if (!Leases.TryGetValue(id, out lease)) return;
            for (int i = lease.owners.Count - 1; i >= 0; i--)
            {
                if (!object.ReferenceEquals(lease.owners[i], this)) continue;
                lease.owners.RemoveAt(i);
                lease.references--;
                break;
            }
            DestroyIfUnowned(id, lease);
        }

        private static void DestroyIfUnowned(int id, Lease lease)
        {
            if (lease.references > 0) return;
            // Remove before Destroy: lifecycle callbacks cannot reacquire a zero-reference lease.
            Leases.Remove(id);
            foreach (UnityEngine.Object resource in lease.resources)
                if (resource != null) HotReload.PaladinResourceState.DestroyTracked(resource);
        }

        internal static void PruneDestroyedOwners()
        {
            if (Leases.Count == 0) return;
            List<int> empty = null;
            foreach (KeyValuePair<int, Lease> pair in Leases)
            {
                Lease lease = pair.Value;
                for (int i = lease.owners.Count - 1; i >= 0; i--)
                {
                    // Unity native-null detects destroyed components even while their managed wrappers remain.
                    // Never-active clones may receive no OnDestroy, so cleanup cannot depend on their callbacks.
                    if (lease.owners[i] != null) continue;
                    lease.owners.RemoveAt(i);
                    lease.references--;
                }
                if (lease.references != 0) continue;
                if (empty == null) empty = new List<int>();
                empty.Add(pair.Key);
            }
            if (empty != null)
                foreach (int id in empty) DestroyIfUnowned(id, Leases[id]);
        }
        private void Awake() { EnsureRetained(); }
        private void OnDestroy() { Release(); }
    }
}
