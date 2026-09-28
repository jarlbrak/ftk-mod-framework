using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Use a native garment binding and original item-scoped meshes on any wearer.</summary>
        public static bool SetItemApparelMeshesFromGlb(FTK_items item, FTK_skinset.ID femaleBinding,
            FTK_skinset.ID maleBinding, params PlayerApparelMesh[] meshes)
        {
            int id;
            if (item == null || !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB)) ||
                !object.ReferenceEquals(Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id), item) || meshes == null || meshes.Length == 0) return RejectApparel(item, "requires exact registered item and nonempty meshes");
            // Native equip routing selects Body/Foot by ObjectType; both use ObjectSlot.equip.
            if (item.m_ObjectType != FTK_itembase.ObjectType.armor && item.m_ObjectType != FTK_itembase.ObjectType.boots) return RejectApparel(item, "unsupported object type " + item.m_ObjectType);
            FTK_skinset female = Db<FTK_skinsetDB>().GetEntry(femaleBinding), male = Db<FTK_skinsetDB>().GetEntry(maleBinding);
            if (female == null || male == null) return RejectApparel(item, "missing skinset row " + femaleBinding + "/" + maleBinding);
            if (item.m_ObjectType == FTK_itembase.ObjectType.armor && (female.m_Armor == null || male.m_Armor == null)) return RejectApparel(item, "missing native armor prefab " + femaleBinding + "/" + maleBinding);
            GameObject f = item.m_ObjectType == FTK_itembase.ObjectType.armor ? female.m_Armor.gameObject : female.m_Boot;
            GameObject m = item.m_ObjectType == FTK_itembase.ObjectType.armor ? male.m_Armor.gameObject : male.m_Boot;
            if (f == null || m == null) return RejectApparel(item, "missing native garment prefab " + femaleBinding + "/" + maleBinding);
            string error;
            if (!ItemApparelRegistry.ValidateAssignments(meshes, out error)) return RejectApparel(item, error);
            item.m_WearablePrefab = f; item.m_WearablePrefabM = m;
            ItemApparelRegistry.Register(id, meshes);
            return true;
        }
        private static bool RejectApparel(FTK_items item, string reason)
        {
            Plugin.Log.LogWarning("[item-apparel] '" + (item == null ? "<null>" : item.m_ID) + "': " + reason);
            return false;
        }
    }

    internal static class ItemApparelRegistry
    {
        private static Dictionary<int, PlayerApparelMesh[]> Items = new Dictionary<int, PlayerApparelMesh[]>();
        internal static IEnumerable<KeyValuePair<int, PlayerApparelMesh[]>> ReloadPlans() { return Items; }
        internal static int ReloadApparelCount { get { return Items.Count; } }
        internal static Action SuspendForReload()
        {
            Dictionary<int, PlayerApparelMesh[]> old = Items;
            Items = new Dictionary<int, PlayerApparelMesh[]>();
            return delegate { Items = old; };
        }
        internal static void Register(int id, PlayerApparelMesh[] meshes) { Items[id] = (PlayerApparelMesh[])meshes.Clone(); }

        internal static bool ValidateAssignments(PlayerApparelMesh[] meshes, out string error)
        {
            error = null;
            if (meshes == null || meshes.Length == 0) { error = "requires nonempty apparel meshes"; return false; }
            Dictionary<string, HashSet<FTK_playerGameStart.SkinType>> paths =
                new Dictionary<string, HashSet<FTK_playerGameStart.SkinType>>(StringComparer.Ordinal);
            foreach (PlayerApparelMesh mesh in meshes)
            {
                if (mesh == null || string.IsNullOrEmpty(mesh.ExpectedNativeMeshName) || mesh.ExpectedNativeMeshName.Trim().Length == 0)
                { error = "missing expected native mesh name"; return false; }
                if (mesh.NativeSkinType.HasValue && (mesh.NativeSkinType.Value == FTK_playerGameStart.SkinType.None ||
                    !Enum.IsDefined(typeof(FTK_playerGameStart.SkinType), mesh.NativeSkinType.Value)))
                { error = "native skin selector must name a playable appearance"; return false; }
                // Validate every alternative's files without treating a deliberate override as a duplicate path.
                if (!ExplicitEnemyMeshSwap.ValidateAssignments(new[] {
                    new EnemyRendererMesh(mesh.RendererPath, mesh.GlbFileName, mesh.TextureFileName) }, out error)) return false;
                HashSet<FTK_playerGameStart.SkinType> selectors;
                if (!paths.TryGetValue(mesh.RendererPath, out selectors))
                    paths.Add(mesh.RendererPath, selectors = new HashSet<FTK_playerGameStart.SkinType>());
                if (!selectors.Add(mesh.NativeSkinType ?? FTK_playerGameStart.SkinType.None))
                { error = "duplicate apparel path and native skin selector: " + mesh.RendererPath; return false; }
            }
            return true;
        }

        private static FTK_playerGameStart.SkinType NativeSkinType(CharacterEventListener avatar)
        {
            FTK_playerGameStart.SkinType skin = FTK_playerGameStart.SkinType.None;
            FTK_playerGameStart row = null;
            if (avatar.m_CharacterOverworld != null)
            {
                skin = avatar.m_CharacterOverworld.m_SkinType;
                if (skin == FTK_playerGameStart.SkinType.None) row = avatar.m_CharacterOverworld.GetDBEntry();
            }
            else if (avatar.m_uiQuickPlayerCreate != null)
            {
                skin = avatar.m_uiQuickPlayerCreate.m_SkinType;
                if (skin == FTK_playerGameStart.SkinType.None) row = avatar.m_uiQuickPlayerCreate.GetClassDBEntry();
            }
            return row == null ? skin : row.m_DefaultSkinType;
        }

        internal static EnemyRendererMesh[] Resolve(CharacterEventListener avatar)
        {
            PlayerInventory inventory = avatar.m_CharacterOverworld != null ? avatar.m_CharacterOverworld.m_PlayerInventory :
                avatar.m_uiQuickPlayerCreate != null ? avatar.m_uiQuickPlayerCreate.m_PlayerInventory : null;
            if (inventory == null) return new EnemyRendererMesh[0];
            List<EnemyRendererMesh> result = new List<EnemyRendererMesh>();
            MergeItem(avatar, inventory.m_ContainerBody.GetOne(), result);
            MergeItem(avatar, inventory.m_ContainerFoot.GetOne(), result);
            return result.ToArray();
        }

        private static void MergeItem(CharacterEventListener avatar, FTK_itembase.ID id, List<EnemyRendererMesh> result)
        {
            PlayerApparelMesh[] meshes;
            if (!Items.TryGetValue((int)id, out meshes)) return;
            FTK_playerGameStart.SkinType skin = NativeSkinType(avatar);
            Dictionary<string, PlayerApparelMesh> selected = new Dictionary<string, PlayerApparelMesh>(StringComparer.Ordinal);
            List<string> order = new List<string>();
            foreach (PlayerApparelMesh mesh in meshes)
            {
                if (mesh.NativeSkinType.HasValue && mesh.NativeSkinType.Value != skin) continue;
                PlayerApparelMesh prior;
                if (!selected.TryGetValue(mesh.RendererPath, out prior))
                {
                    selected.Add(mesh.RendererPath, mesh);
                    order.Add(mesh.RendererPath);
                }
                else if (mesh.NativeSkinType.HasValue) selected[mesh.RendererPath] = mesh;
            }
            Transform[] transforms = avatar.GetComponentsInChildren<Transform>(true);
            foreach (string path in order)
            {
                PlayerApparelMesh mesh = selected[path];
                Transform target = null;
                foreach (Transform t in transforms)
                    if (ExplicitEnemyMeshSwap.RelativePath(avatar.transform, t) == mesh.RendererPath)
                    {
                        if (target != null) throw new InvalidOperationException("Ambiguous apparel path: " + mesh.RendererPath);
                        target = t;
                    }
                // Male/female alternatives and hidden equipment may legitimately be absent.
                if (target == null) continue;
                SkinnedMeshRenderer[] renderers = target.GetComponents<SkinnedMeshRenderer>();
                if (renderers.Length != 1 || renderers[0].sharedMesh == null || renderers[0].sharedMesh.name != mesh.ExpectedNativeMeshName)
                    throw new InvalidOperationException("Item apparel binding mismatch: " + mesh.RendererPath);
                foreach (EnemyRendererMesh prior in result)
                    if (prior.RendererPath == mesh.RendererPath)
                        throw new InvalidOperationException("Two equipped items target the same apparel path: " + mesh.RendererPath);
                result.Add(new EnemyRendererMesh(mesh.RendererPath, mesh.GlbFileName, mesh.TextureFileName));
            }
        }
    }
}
