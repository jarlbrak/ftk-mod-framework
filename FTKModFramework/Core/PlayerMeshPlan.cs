using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTKModFramework.Core
{
    // One immutable registration snapshot. Resolve the complete assembled outfit before the single
    // strict transaction creates its lease; a second transaction would stop at that existing lease.
    internal sealed class PlayerMeshPlan
    {
        private readonly EnemyRendererMesh[] _required;
        private readonly PlayerApparelMesh[] _apparel;

        private PlayerMeshPlan(EnemyRendererMesh[] required, PlayerApparelMesh[] apparel)
        { _required = required; _apparel = apparel; }

        internal int RequiredCount { get { return _required.Length; } }
        internal int ApparelCount { get { return _apparel.Length; } }

        internal static bool TryCreate(PlayerRendererMesh[] required, PlayerApparelMesh[] apparel,
            out PlayerMeshPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (required == null || required.Length == 0)
            { error = "at least one required body renderer assignment is required"; return false; }
            if (apparel == null)
            { error = "conditional apparel array is required (use an empty array for none)"; return false; }
            EnemyRendererMesh[] body = new EnemyRendererMesh[required.Length];
            PlayerApparelMesh[] clothes = new PlayerApparelMesh[apparel.Length];
            List<EnemyRendererMesh> all = new List<EnemyRendererMesh>();
            for (int i = 0; i < required.Length; i++)
            {
                PlayerRendererMesh item = required[i];
                if (item == null) { error = "required renderer assignment is null"; return false; }
                body[i] = new EnemyRendererMesh(item.RendererPath, item.GlbFileName, item.TextureFileName);
                all.Add(body[i]);
            }
            if (!ExplicitEnemyMeshSwap.ValidateAssignments(body, out error)) return false;
            for (int i = 0; i < apparel.Length; i++)
            {
                PlayerApparelMesh item = apparel[i];
                if (item == null || string.IsNullOrEmpty(item.ExpectedNativeMeshName) || item.ExpectedNativeMeshName.Trim().Length == 0)
                { error = "conditional apparel requires an exact native mesh name"; return false; }
                clothes[i] = new PlayerApparelMesh(item.RendererPath, item.ExpectedNativeMeshName,
                    item.GlbFileName, item.TextureFileName);
                all.Add(new EnemyRendererMesh(item.RendererPath, item.GlbFileName, item.TextureFileName));
            }
            // Validate all possible assignments, including duplicate paths across required and conditional sets.
            if (!ExplicitEnemyMeshSwap.ValidateAssignments(all.ToArray(), out error)) return false;
            plan = new PlayerMeshPlan(body, clothes);
            return true;
        }

        internal bool Apply(string identity, CharacterEventListener avatar)
        {
            if (avatar == null) return false;
            // Native identities no longer exist after a successful swap. Retain the completed plan's lease
            // before attempting resolution again, matching existing same-avatar idempotence semantics.
            EnemyMeshResources existing = avatar.GetComponent<EnemyMeshResources>();
            if (existing != null && (!existing.VisualResourcesOnly || !existing.ValidLease()))
                return !existing.VisualResourcesOnly && existing.Applied && existing.EnsureRetained();
            EnemyRendererMesh[] resolved;
            string[] skipped;
            string error;
            if (!TryResolve(avatar.transform, out resolved, out skipped, out error))
            {
                Plugin.Log.LogWarning("[player-mesh] outfit rejected for '" + identity + "': " + error + "; native avatar retained.");
                return false;
            }
            foreach (string path in skipped)
                Plugin.Log.LogInfo("[player-mesh] conditional apparel path not present for '" + identity + "': '" + path +
                    "'; assignment skipped.");
            return ExplicitEnemyMeshSwap.Apply(identity, avatar, resolved, null, true);
        }

        internal bool TryResolve(Transform root, out EnemyRendererMesh[] resolved, out string[] skipped, out string error)
        {
            return TryResolve(root, new EnemyRendererMesh[0], out resolved, out skipped, out error);
        }

        // Equipped garments replace class-default conditional apparel, never required body parts.
        internal bool TryResolve(Transform root, EnemyRendererMesh[] equipment, out EnemyRendererMesh[] resolved,
            out string[] skipped, out string error)
        {
            resolved = null;
            skipped = null;
            error = null;
            if (root == null) { error = "assembled avatar root is required"; return false; }
            HashSet<string> replaced = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyRendererMesh item in equipment) replaced.Add(item.RendererPath);
            Transform[] targets = root.GetComponentsInChildren<Transform>(true);
            List<EnemyRendererMesh> assignments = new List<EnemyRendererMesh>();
            List<string> absent = new List<string>();
            foreach (EnemyRendererMesh item in _required)
            {
                if (replaced.Contains(item.RendererPath))
                { error = "item apparel cannot replace required body path '" + item.RendererPath + "'"; return false; }
                SkinnedMeshRenderer renderer;
                bool missing;
                if (!ResolveTarget(root, targets, item.RendererPath, false, out renderer, out missing, out error)) return false;
                assignments.Add(item);
            }
            foreach (PlayerApparelMesh item in _apparel)
            {
                if (replaced.Contains(item.RendererPath)) continue;
                SkinnedMeshRenderer renderer;
                bool missing;
                if (!ResolveTarget(root, targets, item.RendererPath, true, out renderer, out missing, out error)) return false;
                if (missing) { absent.Add(item.RendererPath); continue; }
                if (!string.Equals(renderer.sharedMesh.name, item.ExpectedNativeMeshName, StringComparison.Ordinal))
                {
                    error = "apparel path '" + item.RendererPath + "' expected native mesh '" + item.ExpectedNativeMeshName +
                        "', found '" + renderer.sharedMesh.name + "'";
                    return false;
                }
                assignments.Add(new EnemyRendererMesh(item.RendererPath, item.GlbFileName, item.TextureFileName));
            }
            assignments.AddRange(equipment);
            resolved = assignments.ToArray();
            skipped = absent.ToArray();
            return true;
        }

        private static bool ResolveTarget(Transform root, Transform[] targets, string path, bool optional,
            out SkinnedMeshRenderer renderer, out bool missing, out string error)
        {
            renderer = null;
            missing = false;
            error = null;
            Transform target = null;
            int matches = 0;
            foreach (Transform candidate in targets)
                if (ExplicitEnemyMeshSwap.RelativePath(root, candidate) == path) { target = candidate; matches++; }
            if (matches == 0 && optional) { missing = true; return true; }
            if (matches != 1)
            { error = "renderer path '" + path + "' matched " + matches + " transforms"; return false; }
            SkinnedMeshRenderer[] renderers = target.GetComponents<SkinnedMeshRenderer>();
            if (renderers.Length != 1 || renderers[0].sharedMesh == null)
            { error = "present path '" + path + "' requires one skinned renderer with a native mesh"; return false; }
            renderer = renderers[0];
            return true;
        }
    }
}
