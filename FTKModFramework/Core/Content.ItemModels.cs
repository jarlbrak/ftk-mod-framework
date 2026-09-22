using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Bind exact rigid renderers on newly instantiated custom equipment, for every wearer.</summary>
        public static bool SetItemMeshesFromGlb(FTK_itembase item, params ItemRendererMesh[] meshes)
        {
            return RegisterItemMeshes(item, meshes, false);
        }

        /// <summary>Bind exact rigid renderers relative to the native loot-display prefab root, independently of equipped geometry.</summary>
        public static bool SetItemDisplayMeshesFromGlb(FTK_itembase item, params ItemRendererMesh[] meshes)
        {
            return RegisterItemMeshes(item, meshes, true);
        }

        private static bool RegisterItemMeshes(FTK_itembase item, ItemRendererMesh[] meshes, bool display)
        {
            int id;
            if (item == null || !ContentRegistry.TryGetSyntheticId(item.m_ID, out id,
                typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return false;
            FTK_itembase registered = item is FTK_weaponStats2
                ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id)
                : (FTK_itembase)Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id);
            if (!object.ReferenceEquals(item, registered)) return false;
            EnemyRendererMesh[] assignments;
            if (meshes == null || meshes.Length == 0) return false;
            assignments = new EnemyRendererMesh[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) return false;
                assignments[i] = EnemyRendererMesh.ForStaticRenderer(meshes[i].RendererPath,
                    meshes[i].GlbFileName, meshes[i].TextureFileName, true);
            }
            string error;
            if (!ExplicitEnemyMeshSwap.ValidateAssignments(assignments, out error))
            {
                Plugin.Log.LogWarning("[item-model] registration rejected: " + error);
                return false;
            }
            if (display) ItemModelRegistry.RegisterDisplay(id, assignments);
            else ItemModelRegistry.Register(id, assignments);
            return true;
        }
    }

    /// <summary>One rigid mesh path relative to the native root selected by the registration API.</summary>
    public sealed class ItemRendererMesh
    {
        public string RendererPath { get; private set; }
        public string GlbFileName { get; private set; }
        public string TextureFileName { get; private set; }
        public ItemRendererMesh(string rendererPath, string glbFileName, string textureFileName)
        {
            RendererPath = rendererPath; GlbFileName = glbFileName; TextureFileName = textureFileName;
        }
    }
}
