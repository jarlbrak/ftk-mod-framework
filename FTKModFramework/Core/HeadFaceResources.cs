using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Owns only generated face meshes. Serialized target references remap onto native avatar clones.
    internal sealed class HeadFaceResources : MonoBehaviour
    {
        private sealed class Lease
        {
            internal Mesh[] Meshes;
            internal readonly List<HeadFaceResources> Owners = new List<HeadFaceResources>();
        }
        private static readonly Dictionary<int, Lease> leases = new Dictionary<int, Lease>();
        private static int nextId;
        [SerializeField] private int leaseId;
        [SerializeField] private SkinnedMeshRenderer[] renderers;
        [SerializeField] private Mesh[] originals, replacements;
        [NonSerialized] private bool acquired;
        [NonSerialized] internal bool LastCommitAdopted;

        internal static int ActiveLeaseCount { get { Prune(); return leases.Count; } }
        internal bool HasLease { get { return leaseId != 0; } }
        internal static int ActiveMeshCount
        {
            get
            {
                Prune();
                int count = 0;
                foreach (Lease lease in leases.Values) count += lease.Meshes.Length;
                return count;
            }
        }
        internal bool Active { get { return leaseId != 0 && EnsureRetained(); } }

        internal static void RetainHierarchy(GameObject root)
        {
            if (root == null) return;
            foreach (HeadFaceResources owner in root.GetComponentsInChildren<HeadFaceResources>(true))
                owner.EnsureRetained();
        }

        internal bool EnsureRetained()
        {
            if (leaseId == 0) return false;
            Lease lease;
            if (!leases.TryGetValue(leaseId, out lease)) return false;
            if (acquired) return lease.Owners.Contains(this);
            lease.Owners.Add(this); acquired = true; return true;
        }

        internal bool Restore()
        {
            if (leaseId == 0) return true;
            if (!EnsureRetained() || renderers == null || originals == null || replacements == null ||
                renderers.Length != originals.Length || renderers.Length != replacements.Length) return false;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null || originals[i] == null ||
                    renderers[i].sharedMesh != replacements[i]) return false;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMesh = originals[i];
            }
            Release();
            renderers = null; originals = null; replacements = null;
            return true;
        }

        internal bool Commit(SkinnedMeshRenderer[] targets, Mesh[] sources, Mesh[] outputs)
        {
            LastCommitAdopted = false;
            if (leaseId != 0 || targets == null || sources == null || outputs == null ||
                targets.Length == 0 || targets.Length != sources.Length || targets.Length != outputs.Length ||
                targets.Length > 16) return false;
            List<Mesh> owned = new List<Mesh>();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null || sources[i] == null || targets[i].sharedMesh != sources[i] ||
                    !targets[i].gameObject.activeInHierarchy || !targets[i].enabled || outputs[i] == null) return false;
                for (int j = 0; j < i; j++) if (targets[j] == targets[i]) return false;
                if (outputs[i] == sources[i] || owned.Contains(outputs[i])) return false;
                owned.Add(outputs[i]);
            }
            int id = ++nextId;
            if (id == 0) id = ++nextId;
            Lease lease = new Lease { Meshes = owned.ToArray() };
            leases.Add(id, lease);
            LastCommitAdopted = true;
            leaseId = id; acquired = true; lease.Owners.Add(this);
            renderers = (SkinnedMeshRenderer[])targets.Clone();
            originals = (Mesh[])sources.Clone(); replacements = (Mesh[])outputs.Clone();
            int attempted = 0;
            try
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    attempted++;
                    targets[i].sharedMesh = outputs[i];
                }
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[head-face] commit: " + error.Message);
                bool restored = true;
                for (int i = attempted - 1; i >= 0; i--)
                    try { targets[i].sharedMesh = sources[i]; }
                    catch (Exception rollback) { restored = false; Plugin.Log.LogError("[head-face] rollback: " + rollback.Message); }
                if (restored) Release(); // Incomplete rollback keeps every generated mesh leased.
                return false;
            }
        }

        private void Release()
        {
            if (!acquired || leaseId == 0) return;
            int id = leaseId; leaseId = 0; acquired = false;
            Lease lease;
            if (!leases.TryGetValue(id, out lease)) return;
            lease.Owners.Remove(this);
            DestroyIfUnused(id, lease);
        }
        private static void DestroyIfUnused(int id, Lease lease)
        {
            if (lease.Owners.Count != 0) return;
            // Inactive native clones can miss Awake. Never free a mesh still referenced by any clone.
            SkinnedMeshRenderer[] live = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in live)
                if (renderer != null && renderer.sharedMesh != null)
                    foreach (Mesh mesh in lease.Meshes)
                        if (mesh != null && renderer.sharedMesh == mesh) return;
            leases.Remove(id);
            foreach (Mesh mesh in lease.Meshes) if (mesh != null)
                HotReload.PaladinResourceState.DestroyTracked(mesh);
        }
        internal static void Prune()
        {
            List<int> empty = null;
            foreach (KeyValuePair<int, Lease> pair in leases)
            {
                for (int i = pair.Value.Owners.Count - 1; i >= 0; i--)
                    if (pair.Value.Owners[i] == null) pair.Value.Owners.RemoveAt(i);
                if (pair.Value.Owners.Count == 0)
                { if (empty == null) empty = new List<int>(); empty.Add(pair.Key); }
            }
            if (empty != null) foreach (int id in empty) DestroyIfUnused(id, leases[id]);
        }
        private void Awake() { EnsureRetained(); }
        private void OnDestroy() { Release(); }
    }
}
