using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Ownership at the permissive legacy visual call sites, without changing loader selection.</summary>
    internal static class LegacyVisualResources
    {
        internal static void Materials(CharacterEventListener cel, Renderer renderer, Action<Material> prepare,
            UnityEngine.Object extra = null)
        {
            Material[] originals = null;
            Material[] copies = null;
            List<UnityEngine.Object> added = new List<UnityEngine.Object>();
            if (extra != null) added.Add(extra);
            EnemyMeshResources owner = null;
            bool fresh = false;
            EnemyMeshResources.Batch batch = null;
            bool attempted = false;
            try
            {
                originals = renderer.sharedMaterials; copies = new Material[originals.Length];
                owner = cel.GetComponent<EnemyMeshResources>(); fresh = owner == null;
                if (!fresh && !owner.ValidLease()) throw new InvalidOperationException("Invalid existing visual resource owner");
                for (int i = 0; i < copies.Length; i++)
                {
                    if (originals[i] == null) continue;
                    copies[i] = new Material(originals[i]); added.Add(copies[i]); prepare(copies[i]);
                }
                if (added.Count == 0) return;
                ScrollingUVs[] scrollers = renderer.GetComponents<ScrollingUVs>();
                bool supported = true;
                try
                {
                    // The narrow prefix's private-copy contract requires every slot to exist.
                    foreach (Material material in copies) if (material == null) throw new InvalidOperationException("null material slot");
                    foreach (ScrollingUVs scroller in scrollers) ExplicitScrollingUvs.Validate(scroller, copies);
                }
                catch (Exception e)
                {
                    supported = false;
                    if (scrollers.Length > 0) Plugin.Log.LogWarning("[enemy-visual] legacy scroller left native: " + e.Message +
                        "; subsequent native-created materials are outside the framework ownership guarantee");
                }
                if (fresh) owner = cel.gameObject.AddComponent<EnemyMeshResources>();
                batch = owner.Append(added.ToArray(), new Renderer[] { renderer }, fresh);
                attempted = true; renderer.sharedMaterials = copies;
                owner.AddScrollingTarget(renderer, copies, supported ? scrollers : new ScrollingUVs[0]);
                if (!owner.Applied) owner.VisualResourcesOnly = true;
                owner.Applied = true;
            }
            catch
            {
                bool restored = true;
                if (attempted) try { renderer.sharedMaterials = originals; }
                    catch (Exception e) { restored = false; Plugin.Log.LogError("[enemy-visual] material rollback: " + e.Message); }
                if (batch != null) batch.Rollback(restored);
                else
                {
                    if (fresh && owner != null) UnityEngine.Object.Destroy(owner);
                    foreach (UnityEngine.Object item in added) if (item != null) UnityEngine.Object.Destroy(item);
                }
                throw;
            }
        }

        internal static void Mesh(CharacterEventListener cel, SkinnedMeshRenderer renderer, Mesh mesh)
        {
            Mesh original = null;
            Transform[] bones = null;
            EnemyMeshResources owner = null;
            bool fresh = false;
            EnemyMeshResources.Batch batch = null;
            bool attempted = false;
            try
            {
                original = renderer.sharedMesh; bones = renderer.bones;
                owner = cel.GetComponent<EnemyMeshResources>(); fresh = owner == null;
                if (fresh) owner = cel.gameObject.AddComponent<EnemyMeshResources>();
                batch = owner.Append(new UnityEngine.Object[] { mesh }, new Renderer[] { renderer }, fresh);
                attempted = true; renderer.sharedMesh = mesh;
                if (bones != null) renderer.bones = (Transform[])bones.Clone();
                if (!owner.Applied) owner.VisualResourcesOnly = true;
                owner.Applied = true;
            }
            catch
            {
                bool restored = true;
                if (attempted) try { renderer.sharedMesh = original; renderer.bones = bones; }
                    catch (Exception e) { restored = false; Plugin.Log.LogError("[enemy-visual] mesh rollback: " + e.Message); }
                if (batch != null) batch.Rollback(restored);
                else
                {
                    if (fresh && owner != null) UnityEngine.Object.Destroy(owner);
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
                }
                throw;
            }
        }

        internal static void OptionalTexture(string enemyId, CharacterEventListener cel, Renderer renderer, string file)
        {
            if (string.IsNullOrEmpty(file)) return;
            Texture2D texture = null;
            try
            {
                string path = CustomModelLoader.ResolveModelPath(file);
                if (!File.Exists(path)) throw new IOException("not found at '" + path + "'");
                texture = new Texture2D(2, 2);
                // Owned PNGs are sampled by materials only; release the CPU pixel copy after upload.
                if (!texture.LoadImage(File.ReadAllBytes(path), true)) throw new InvalidDataException("PNG decode returned false");
                Texture2D prepared = texture;
                // Materials owns the allocation on entry, including its failure paths.
                texture = null;
                Materials(cel, renderer, delegate(Material material)
                {
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", prepared);
                    else material.mainTexture = prepared;
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", prepared);
                        material.SetColor("_EmissionColor", new Color(0.45f, 0.50f, 0.40f));
                    }
                }, prepared);
            }
            catch (Exception e)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Plugin.Log.LogWarning("[enemy-visual] glb texture load failed for '" + enemyId + "': " +
                    e.Message + "; mesh applied without it; prior material appearance retained unless rollback was incomplete.");
            }
        }
    }
}
