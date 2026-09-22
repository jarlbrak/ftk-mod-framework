using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.IO;
using System.Diagnostics;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    internal static partial class PaladinResourceState
    {
        internal static long AssetCaptureMs, AssetWorkerWaitMs, AssetTextureMs;

        // Call only during quiescent publication. Prefabs are inspected, never instantiated or
        // mutated. Texture decoding is the only Unity allocation and its destruction is fenced.
        internal static void PreflightAssets()
        {
            IEnumerator steps = PreflightAssetsSteps();
            while (steps.MoveNext()) Thread.Sleep(1);
        }

        internal static IEnumerator PreflightAssetsSteps()
        {
            AssetCaptureMs = AssetWorkerWaitMs = AssetTextureMs = 0;
            Stopwatch phase = Stopwatch.StartNew();
            List<Action> jobs = new List<Action>();
            if (!TransactionOpen) throw new InvalidOperationException("Asset preflight requires a resource transaction.");
            HashSet<string> rigidModels = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> textures = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> requiredItems = new HashSet<int>();
            foreach (bool display in new[] { false, true })
                foreach (KeyValuePair<int, EnemyRendererMesh[]> plan in ItemModelRegistry.ReloadPlans(display)) requiredItems.Add(plan.Key);
            foreach (KeyValuePair<int, PlayerApparelMesh[]> plan in ItemApparelRegistry.ReloadPlans()) requiredItems.Add(plan.Key);
            Dictionary<int, FTK_itembase> items = PreflightRowIndex.Build(ItemRowsForPreflight(), requiredItems,
                delegate(FTK_itembase row) { return (int)FTK_itembase.GetEnum(row.m_ID); });
            foreach (bool display in new[] { false, true })
                foreach (KeyValuePair<int, EnemyRendererMesh[]> plan in ItemModelRegistry.ReloadPlans(display))
                {
                    FTK_itembase item = items[plan.Key];
                    GameObject root = item.m_Prefab;
                    if (root == null) throw new InvalidOperationException("Item has no native prefab: " + item.m_ID);
                    // Verified FTKHub routing: equipped geometry returns the component subtree;
                    // loot display returns the full prefab root.
                    if (!display)
                    {
                        Component component = item is FTK_weaponStats2 ? (Component)root.GetComponentInChildren<Weapon>() :
                            item.m_ObjectType == FTK_itembase.ObjectType.shield ? (Component)root.GetComponentInChildren<Shield>() :
                            item.m_ObjectType == FTK_itembase.ObjectType.helmet ? (Component)root.GetComponentInChildren<Helmet>() : null;
                        if (component == null) throw new InvalidOperationException("Unsupported or missing held item component: " + item.m_ID);
                        root = component.gameObject;
                    }
                    foreach (EnemyRendererMesh mesh in plan.Value)
                    {
                        Transform target = ExactTarget(root, mesh.RendererPath);
                        MeshRenderer[] renderers = target.GetComponents<MeshRenderer>();
                        MeshFilter[] filters = target.GetComponents<MeshFilter>();
                        if (renderers.Length != 1 || filters.Length != 1 || filters[0].sharedMesh == null ||
                            target.GetComponents<SkinnedMeshRenderer>().Length != 0)
                            throw new InvalidOperationException("Invalid native rigid renderer: " + item.m_ID + "/" + mesh.RendererPath);
                        ValidateMaterial(renderers[0], true);
                        // Every native target is checked, but identical immutable rigid bytes only
                        // need decoding once in this transaction. Skinned contracts remain distinct.
                        string resolved = CustomModelLoader.ResolveModelPath(mesh.GlbFileName);
                        if (rigidModels.Add(resolved))
                            jobs.Add(delegate { RuntimeGltfMeshLoader.PreflightResolved(resolved, null, null); });
                        textures.Add(mesh.TextureFileName);
                    }
                }
            List<string[]> avatars = null;
            foreach (KeyValuePair<int, PlayerApparelMesh[]> plan in ItemApparelRegistry.ReloadPlans())
            {
                if (avatars == null) avatars = AvatarBoneNames();
                FTK_itembase item = items[plan.Key];
                foreach (PlayerApparelMesh mesh in plan.Value)
                {
                    SkinnedMeshRenderer renderer = null;
                    foreach (GameObject prefab in new[] { item.m_WearablePrefab, item.m_WearablePrefabM })
                    {
                        if (prefab == null) throw new InvalidOperationException("Missing native garment: " + item.m_ID);
                        // Native Stitcher flattens every active source renderer onto one clone-named
                        // child. Multiple source renderers would violate the runtime's exact-one rule.
                        SkinnedMeshRenderer[] sources = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
                        string path = StitcherPreflightRules.FlattenedPath(prefab.name, sources.Length);
                        if (path != mesh.RendererPath) continue;
                        SkinnedMeshRenderer candidate = sources[0];
                        if (object.ReferenceEquals(renderer, candidate)) continue;
                        if (renderer != null || candidate.sharedMesh == null || candidate.sharedMesh.name != mesh.ExpectedNativeMeshName)
                            throw new InvalidOperationException("Ambiguous or mismatched native garment: " + mesh.RendererPath);
                        renderer = candidate;
                    }
                    if (renderer == null) throw new InvalidOperationException("Native garment renderer not found: " + mesh.RendererPath);
                    ValidateMaterial(renderer, false);
                    Transform[] sourceBones = renderer.bones ?? new Transform[0];
                    if (sourceBones.Length == 0) throw new InvalidOperationException("Native garment has no source bone palette.");
                    string[] names = new string[sourceBones.Length];
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (sourceBones[i] == null) throw new InvalidOperationException("Native garment contains a null bone.");
                        names[i] = sourceBones[i].name;
                    }
                    foreach (string[] targetNames in avatars)
                    {
                        int[] map = StitcherPreflightRules.MapBones(names, targetNames);
                        for (int i = 0; i < map.Length; i++)
                            // A prefab root changes name on instantiation. A root match is
                            // unsupported even when all the other named bones map exactly.
                            if (map[i] == 0) throw new InvalidOperationException("Garment depends on renamed avatar root; unsupported preflight.");
                    }
                    string resolved = CustomModelLoader.ResolveModelPath(mesh.GlbFileName);
                    // MapBones proved every target has these exact ordinal names. Retaining
                    // this copied source palette avoids reading translated Unity names again.
                    string[] copiedNames = names;
                    Matrix4x4[] copiedBindposes = (Matrix4x4[])renderer.sharedMesh.bindposes.Clone();
                    jobs.Add(delegate { RuntimeGltfMeshLoader.PreflightResolved(resolved, copiedNames, copiedBindposes); });
                    textures.Add(mesh.TextureFileName);
                }
            }
            AssetCaptureMs = phase.ElapsedMilliseconds;
            phase = Stopwatch.StartNew();
            PreflightWorkers workers = new PreflightWorkers(jobs.ToArray());
            while (!workers.Complete) yield return null;
            AssetWorkerWaitMs = phase.ElapsedMilliseconds;
            workers.ThrowIfFailed();
            List<string> orderedTextures = new List<string>(textures);
            orderedTextures.Sort(StringComparer.Ordinal);
            foreach (string texture in orderedTextures)
            {
                phase = Stopwatch.StartNew();
                PreflightTexture(texture);
                AssetTextureMs += phase.ElapsedMilliseconds;
                yield return null;
            }
        }

        private static List<string[]> AvatarBoneNames()
        {
            List<string[]> result = new List<string[]>();
            HashSet<CharacterEventListener> seen = new HashSet<CharacterEventListener>();
            foreach (FTK_playerGameStart player in Content.Db<FTK_playerGameStartDB>().m_Array)
            {
                if (player.m_Skinsets == null) throw new InvalidOperationException("Playable class has no skinset list.");
                foreach (FTK_skinset.ID id in player.m_Skinsets)
                {
                    if (id == FTK_skinset.ID.None) continue;
                    FTK_skinset skin = Content.Db<FTK_skinsetDB>().GetEntry(id);
                    if (skin == null || skin.m_Avatar == null) throw new InvalidOperationException("Playable skinset has no native avatar.");
                    if (!seen.Add(skin.m_Avatar)) continue;
                    // Native Stitcher's recursive catalog includes inactive descendants too.
                    List<Transform> transforms = new List<Transform>();
                    CatalogAvatar(skin.m_Avatar.transform, transforms);
                    string[] names = new string[transforms.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = i == 0 ? "Avatar" : transforms[i].name;
                    result.Add(names);
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("No native wearable target avatars.");
            return result;
        }

        private static void CatalogAvatar(Transform root, List<Transform> transforms)
        {
            transforms.Add(root);
            foreach (Transform child in root) CatalogAvatar(child, transforms);
        }

        private static IEnumerable<FTK_itembase> ItemRowsForPreflight()
        {
            foreach (FTK_items item in Content.Db<FTK_itemsDB>().m_Array) yield return item;
            foreach (FTK_weaponStats2 item in Content.Db<FTK_weaponStats2DB>().m_Array) yield return item;
        }

        private static Transform ExactTarget(GameObject root, string path)
        {
            Transform found = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (ExplicitEnemyMeshSwap.RelativePath(root.transform, candidate) == path)
                {
                    if (found != null) throw new InvalidOperationException("Ambiguous native renderer path: " + path);
                    found = candidate;
                }
            if (found == null) throw new InvalidOperationException("Native renderer path not found: " + path);
            return found;
        }

        private static void ValidateMaterial(Renderer renderer, bool rigid)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0 || materials[0] == null || !materials[0].HasProperty("_MainTex") ||
                (rigid && materials.Length != 1)) throw new InvalidOperationException("Unsupported native material topology.");
            foreach (ScrollingUVs scroller in renderer.GetComponents<ScrollingUVs>())
            {
                if (scroller.materialIndex != 0) throw new InvalidOperationException("Garment requires unsupported material slots.");
                ExplicitScrollingUvs.Validate(scroller, new[] { materials[0] });
            }
        }

        private static void PreflightTexture(string identity)
        {
            if (string.IsNullOrEmpty(identity)) throw new InvalidOperationException("Missing model texture.");
            string path = PackageModelPaths.Resolve(identity);
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidOperationException("Model texture exceeds 16 MiB.");
            byte[] bytes = File.ReadAllBytes(path);
            int width, height;
            PngStructure.Validate(bytes, 16 * 1024 * 1024, 4096, out width, out height);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes) || texture.width != width || texture.height != height)
                    throw new InvalidOperationException("Model PNG decode failed or dimensions differ.");
            }
            finally { DestroyTracked(texture); }
        }
    }
}
