using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Loads artist-authored 3D assets (mesh / prefab / material / texture) out of a Unity AssetBundle that
    /// a modder ships INSIDE the mod at <c>&lt;pluginDir&gt;/FTKModFramework_content/models/&lt;file&gt;.unity3d</c>.
    /// This is the engine plumbing behind the two public facades in <see cref="Content"/>
    /// (<c>Content.SetEnemyBodyMesh</c> and <c>Content.SetEnemyBodyFromBundle</c>): the Core/Content boundary, with
    /// the modder-facing API in <see cref="Content"/> and the bundle mechanics here.
    ///
    /// WHY THIS IS DETERMINISM- AND SAVE-SAFE:
    ///   - A 3D model is VISUAL-ONLY: the loaded mesh/prefab/material/texture never crosses Photon and is never
    ///     serialized into a save. The only shared state is the enemy enum-int identity (already deterministic via
    ///     <see cref="IdAllocator"/>). So as long as the bundle ships INSIDE the mod (no per-machine paths, no
    ///     streaming) it is byte-identical on every co-op client, exactly like the procedural visual work that
    ///     already shipped (<see cref="EnemyVisualPatch"/> / <see cref="ProceduralCreature"/>).
    ///
    /// AVAILABILITY: <c>AssetBundle.LoadFromFile</c> and <c>AssetBundle.LoadAsset&lt;T&gt;</c> exist in this build's
    /// UnityEngine.CoreModule.dll (Unity 2017.2.2p2); the game itself never calls them, but the managed API is present.
    /// Bundles MUST be built with the SAME Unity version (2017.2.2p2) or they will not load.
    ///
    /// NEVER THROWS: every public entry returns null + logs a clear warning on a miss or failure, so a missing or
    /// broken bundle can never break combat or registration (the caller falls back to the original / procedural body).
    /// </summary>
    internal static class CustomModelLoader
    {
        // The ship subfolder, next to the plugin DLL, that mirrors the existing FTKModFramework_content/ extraction
        // site used by the adventure art (Adventures.RegisterEndGameImage / RegisterUserNpc).
        private const string ContentFolder = "FTKModFramework_content";
        private const string ModelsSubFolder = "models";

        /// <summary>
        /// Resolve the absolute path to a file shipped under
        /// <c>&lt;pluginDir&gt;/FTKModFramework_content/models/&lt;fileName&gt;</c>. Shared so other Core loaders
        /// (e.g. <see cref="RuntimeGltfMeshLoader"/>) resolve the models folder the SAME way as the bundle path,
        /// without duplicating the two ship-folder constants. Never throws.
        /// </summary>
        internal static string ResolveModelPath(string fileName)
        {
            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            return Path.Combine(Path.Combine(Path.Combine(pluginDir, ContentFolder), ModelsSubFolder), fileName);
        }

        // Loaded bundles, keyed by the bundle file name (not the full path). AssetBundle.LoadFromFile on the SAME
        // path twice throws ("The AssetBundle ... can't be loaded because another AssetBundle with the same files
        // is already loaded"), so we load each bundle exactly once and reuse the handle.
        private static readonly Dictionary<string, AssetBundle> _bundles =
            new Dictionary<string, AssetBundle>(StringComparer.Ordinal);

        /// <summary>
        /// Resolve and load (once) the AssetBundle at
        /// <c>&lt;pluginDir&gt;/FTKModFramework_content/models/&lt;bundleFileName&gt;</c>. Caches the handle so the
        /// same bundle is only loaded once (a second <c>LoadFromFile</c> on the same path throws). Returns the loaded
        /// bundle, or <c>null</c> (with a clear warning) if the file is missing or the load fails. Never throws.
        /// </summary>
        internal static AssetBundle LoadBundle(string bundleFileName)
        {
            if (string.IsNullOrEmpty(bundleFileName))
            {
                Plugin.Log.LogWarning("[custom-model] LoadBundle: bundleFileName is null/empty; skipped.");
                return null;
            }

            AssetBundle cached;
            if (_bundles.TryGetValue(bundleFileName, out cached))
                return cached; // may be null (a prior miss is cached so we do not re-warn every spawn)

            string path = null;
            try
            {
                path = ResolveModelPath(bundleFileName);

                if (!File.Exists(path))
                {
                    Plugin.Log.LogWarning("[custom-model] LoadBundle: bundle not found at '" + path +
                        "'. Ship it at " + ContentFolder + "/" + ModelsSubFolder + "/" + bundleFileName + ".");
                    _bundles[bundleFileName] = null; // cache the miss
                    return null;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null)
                {
                    Plugin.Log.LogWarning("[custom-model] LoadBundle: AssetBundle.LoadFromFile returned null for '" +
                        path + "'. Was it built with Unity 2017.2.2p2 (the game's engine)?");
                    _bundles[bundleFileName] = null;
                    return null;
                }

                _bundles[bundleFileName] = bundle;
                Plugin.Log.LogInfo("[custom-model] LoadBundle: loaded '" + bundleFileName + "' from '" + path + "'.");
                return bundle;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[custom-model] LoadBundle: failed to load '" +
                    (path != null ? path : bundleFileName) + "': " + e.Message);
                _bundles[bundleFileName] = null; // cache the failure so we do not retry-and-rethrow each spawn
                return null;
            }
        }

        /// <summary>
        /// Load a <see cref="Mesh"/> named <paramref name="meshName"/> out of <paramref name="bundleFileName"/>.
        /// Returns <c>null</c> (with a clear warning) if the bundle or the asset is missing. Never throws.
        /// </summary>
        internal static Mesh LoadMesh(string bundleFileName, string meshName)
        {
            return LoadAsset<Mesh>(bundleFileName, meshName, "Mesh");
        }

        /// <summary>
        /// Load a <see cref="GameObject"/> prefab named <paramref name="prefabName"/> out of
        /// <paramref name="bundleFileName"/>. Returns <c>null</c> (with a clear warning) if the bundle or the asset is
        /// missing. Never throws. NOTE: the returned object is the bundle's prefab template; the caller must
        /// <c>Instantiate</c> it before use.
        /// </summary>
        internal static GameObject LoadPrefab(string bundleFileName, string prefabName)
        {
            return LoadAsset<GameObject>(bundleFileName, prefabName, "GameObject");
        }

        /// <summary>
        /// Load a <see cref="Material"/> named <paramref name="materialName"/> out of <paramref name="bundleFileName"/>.
        /// Returns <c>null</c> (with a clear warning) on a miss. Never throws.
        /// </summary>
        internal static Material LoadMaterial(string bundleFileName, string materialName)
        {
            return LoadAsset<Material>(bundleFileName, materialName, "Material");
        }

        /// <summary>
        /// Load a <see cref="Texture2D"/> named <paramref name="textureName"/> out of <paramref name="bundleFileName"/>.
        /// Returns <c>null</c> (with a clear warning) on a miss. Never throws.
        /// </summary>
        internal static Texture2D LoadTexture(string bundleFileName, string textureName)
        {
            return LoadAsset<Texture2D>(bundleFileName, textureName, "Texture2D");
        }

        /// <summary>Shared, null-guarded, never-throwing <c>bundle.LoadAsset&lt;T&gt;</c> wrapper.</summary>
        private static T LoadAsset<T>(string bundleFileName, string assetName, string kind)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(assetName))
            {
                Plugin.Log.LogWarning("[custom-model] Load" + kind + ": assetName is null/empty for bundle '" +
                    bundleFileName + "'; skipped.");
                return null;
            }

            AssetBundle bundle = LoadBundle(bundleFileName);
            if (bundle == null) return null; // LoadBundle already logged the reason

            try
            {
                T asset = bundle.LoadAsset<T>(assetName);
                if (asset == null)
                {
                    Plugin.Log.LogWarning("[custom-model] Load" + kind + ": no " + kind + " named '" + assetName +
                        "' in bundle '" + bundleFileName + "'. Available: " +
                        string.Join(", ", bundle.GetAllAssetNames()));
                    return null;
                }
                return asset;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[custom-model] Load" + kind + ": loading '" + assetName + "' from bundle '" +
                    bundleFileName + "' failed: " + e.Message);
                return null;
            }
        }
    }
}
