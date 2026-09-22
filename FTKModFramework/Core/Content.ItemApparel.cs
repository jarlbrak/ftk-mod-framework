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
            List<EnemyRendererMesh> check = new List<EnemyRendererMesh>();
            foreach (PlayerApparelMesh mesh in meshes)
            {
                if (mesh == null || string.IsNullOrEmpty(mesh.ExpectedNativeMeshName) || mesh.ExpectedNativeMeshName.Trim().Length == 0) return RejectApparel(item, "missing expected native mesh name");
                check.Add(new EnemyRendererMesh(mesh.RendererPath, mesh.GlbFileName, mesh.TextureFileName));
            }
            string error;
            if (!ExplicitEnemyMeshSwap.ValidateAssignments(check.ToArray(), out error)) return RejectApparel(item, error);
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
        private static readonly Dictionary<int, PlayerApparelMesh[]> Items = new Dictionary<int, PlayerApparelMesh[]>();
        internal static void Register(int id, PlayerApparelMesh[] meshes) { Items[id] = (PlayerApparelMesh[])meshes.Clone(); }

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
            Transform[] transforms = avatar.GetComponentsInChildren<Transform>(true);
            foreach (PlayerApparelMesh mesh in meshes)
            {
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
