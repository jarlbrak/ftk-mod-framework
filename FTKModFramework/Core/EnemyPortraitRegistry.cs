using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Exact custom-row portrait selection, with no native prefab or camera mutations.</summary>
    internal static class EnemyPortraitRegistry
    {
        internal sealed class Selection
        {
            internal FTK_enemyCombat row;
            internal string path;
            internal CharacterEventListener source;
            internal bool rowClone;
        }
        internal sealed class Scope
        {
            internal OffscreenCamera camera;
            internal Selection previous;
            internal bool hadPrevious;
            internal bool forwardFromRow;
        }
        private static readonly Dictionary<string, Selection> Registered = new Dictionary<string, Selection>(StringComparer.Ordinal);
        private static readonly Dictionary<OffscreenCamera, Selection> Active = new Dictionary<OffscreenCamera, Selection>();
        private static readonly Dictionary<OffscreenCamera, bool> Forward = new Dictionary<OffscreenCamera, bool>();

        internal static bool ValidPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 512 || path.IndexOf('\\') >= 0) return false;
            foreach (char value in path) if (char.IsControl(value)) return false;
            foreach (string part in path.Split('/'))
                if (part.Length == 0 || part == "." || part == "..") return false;
            return true;
        }
        internal static bool Resolve(Transform root, string path, out Transform marker, out string error)
        {
            marker = null; error = null;
            if (root == null || !ValidPath(path)) { error = "a proper child marker path is required"; return false; }
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            int matches = 0;
            foreach (Transform candidate in transforms)
                if (candidate != root && ExplicitEnemyMeshSwap.RelativePath(root, candidate) == path) { marker = candidate; matches++; }
            if (matches != 1) { error = "exact marker path matched " + matches + " transforms"; marker = null; return false; }
            // Native FindTransformRecursive accepts a leaf name, not a path, and otherwise chooses the first match.
            int names = 0; foreach (Transform candidate in transforms) if (candidate.name == marker.name) names++;
            if (names != 1) { error = "marker leaf name is ambiguous (" + names + " matches)"; marker = null; return false; }
            return true;
        }
        private static bool ExactCustomRow(FTK_enemyCombat row)
        {
            int id;
            return row != null && !string.IsNullOrEmpty(row.m_ID)
                && ContentRegistry.TryGetSyntheticId(row.m_ID, out id, typeof(FTK_enemyCombatDB))
                && object.ReferenceEquals(Content.Db<FTK_enemyCombatDB>().GetEntryByInt(id), row);
        }
        internal static bool Register(FTK_enemyCombat row, string markerPath)
        {
            Transform marker; string error = null;
            if (!ExactCustomRow(row)) return Reject("the exact custom enemy DB row is required");
            if (row.m_EnemyAsset == null || !Resolve(row.m_EnemyAsset.transform, markerPath, out marker, out error))
                return Reject(row.m_EnemyAsset == null ? "enemy has no source avatar" : error);
            Registered[row.m_ID] = new Selection { row = row, path = markerPath, source = row.m_EnemyAsset };
            Plugin.Log.LogInfo("[enemy-portrait] registered '" + row.m_ID + "' marker='" + markerPath + "'.");
            return true;
        }
        private static bool Reject(string reason)
        {
            Plugin.Log.LogWarning("SetEnemyPortraitMarker: " + reason + "; registration unchanged."); return false;
        }
        internal static Selection ForRow(FTK_enemyCombat row)
        {
            if (!ExactCustomRow(row) || row.m_EnemyAsset == null) return null;
            Selection marker;
            string path = Registered.TryGetValue(row.m_ID, out marker) && object.ReferenceEquals(marker.row, row)
                && marker.source == row.m_EnemyAsset ? marker.path : null;
            return new Selection { row = row, source = row.m_EnemyAsset, path = path };
        }
        internal static bool Valid(Selection value)
        { return value != null && ExactCustomRow(value.row) && value.source != null && value.source == value.row.m_EnemyAsset; }
        internal static Selection Forwarded(Selection value)
        { return new Selection { row = value.row, source = value.source, path = value.path, rowClone = true }; }
        internal static void ApplyMeshesOnClone(OffscreenCamera camera, CharacterEventListener source)
        {
            Selection value = Current(camera);
            if (!Valid(value) || !value.rowClone || source != value.source) return;
            GameObject target = camera.m_TargetObject;
            CharacterEventListener cel = target == null ? null : target.GetComponent<CharacterEventListener>();
            if (target == null || !target.scene.IsValid() || cel == null || cel.m_OffscreenCamera != camera
                || cel == source || target == source.gameObject) return;
            EnemyVisualPatch.ApplyPortraitMeshes(value.row.m_ID, cel);
        }

        internal static Selection ForAvatar(CharacterEventListener avatar)
        {
            if (avatar == null) return null;
            EnemyDummy dummy = avatar.m_Dummy as EnemyDummy;
            if (dummy == null || dummy.m_EventListener != avatar || dummy.m_EnemyCombat == null || dummy.m_EnemyType != dummy.m_EnemyCombat.m_ID) return null;
            return ForRow(dummy.m_EnemyCombat);
        }
        internal static Scope Begin(OffscreenCamera camera)
        {
            Scope scope = new Scope { camera = camera };
            scope.hadPrevious = Active.TryGetValue(camera, out scope.previous);
            Forward.TryGetValue(camera, out scope.forwardFromRow);
            Forward[camera] = false;
            Active[camera] = null; // Unresolved nested captures must never inherit a different enemy's selection.
            return scope;
        }
        internal static void Set(OffscreenCamera camera, Selection selection, bool forwardFromRow = false)
        { Active[camera] = selection; Forward[camera] = forwardFromRow; }
        internal static Selection Current(OffscreenCamera camera)
        { Selection value; return Active.TryGetValue(camera, out value) ? value : null; }
        internal static void End(Scope scope)
        {
            if (scope == null) return;
            if (scope.hadPrevious) { Active[scope.camera] = scope.previous; Forward[scope.camera] = scope.forwardFromRow; }
            else { Active.Remove(scope.camera); Forward.Remove(scope.camera); }
        }
        internal static void SelectOnClone(OffscreenCamera camera, ref string first, ref string fallback)
        {
            if (first != "PortraitCam") return;
            Selection value = Current(camera);
            if (!Valid(value) || ForRow(value.row).path != value.path) return;
            GameObject target = camera.m_TargetObject;
            CharacterEventListener cel = target == null ? null : target.GetComponent<CharacterEventListener>();
            Transform marker; string error;
            if (target == null || !target.scene.IsValid() || cel == null || cel.m_OffscreenCamera != camera
                || target == value.source.gameObject)
            { Plugin.Log.LogWarning("[enemy-portrait] owned portrait clone unavailable; native camera arguments retained."); return; }
            if (value.path == null)
            {
                if (LegacyKrakenPortrait.TryFrame(camera, value.source, cel, out marker))
                { first = marker.name; fallback = null; }
                return;
            }
            if (!Resolve(target.transform, value.path, out marker, out error))
            { Plugin.Log.LogWarning("[enemy-portrait] '" + value.row.m_ID + "': " + error + "; native camera arguments retained."); return; }
            // Only arguments change. Native SetTargetPosition owns all positioning and cleanup of its clone.
            first = marker.name; fallback = null;
            Plugin.Log.LogInfo("[enemy-portrait] selected '" + value.path + "' for '" + value.row.m_ID
                + "', camera=" + camera.GetInstanceID() + ", clone=" + target.GetInstanceID() + ".");
        }
    }
}
