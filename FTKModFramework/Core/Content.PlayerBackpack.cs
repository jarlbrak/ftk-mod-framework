using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Replace rigid renderers relative to each newly created native backpack root for this custom class and skinset.</summary>
        public static bool SetClassBackpackMeshesFromGlb(FTK_playerGameStart classRow, FTK_skinset.ID skinset,
            params PlayerRendererMesh[] meshes)
        {
            return PlayerBackpackRegistry.Register(classRow, skinset, meshes);
        }
    }

    internal static class PlayerBackpackRegistry
    {
        private static readonly Dictionary<int, Dictionary<int, EnemyRendererMesh[]>> Registrations =
            new Dictionary<int, Dictionary<int, EnemyRendererMesh[]>>();

        internal static bool Register(FTK_playerGameStart row, FTK_skinset.ID skinset, PlayerRendererMesh[] meshes)
        {
            int id;
            if (!ExactClass(row, out id) || row.m_Skinsets == null ||
                Array.IndexOf(row.m_Skinsets, skinset) < 0 || Content.Db<FTK_skinsetDB>().GetEntry(skinset) == null ||
                meshes == null || meshes.Length == 0) return false;
            EnemyRendererMesh[] snapshot = new EnemyRendererMesh[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) return false;
                snapshot[i] = EnemyRendererMesh.ForStaticRenderer(meshes[i].RendererPath,
                    meshes[i].GlbFileName, meshes[i].TextureFileName, true);
            }
            string error;
            if (!ExplicitEnemyMeshSwap.ValidateAssignments(snapshot, out error)) return false;
            Dictionary<int, EnemyRendererMesh[]> skins;
            if (!Registrations.TryGetValue(id, out skins))
                Registrations.Add(id, skins = new Dictionary<int, EnemyRendererMesh[]>());
            skins[(int)skinset] = snapshot;
            return true;
        }

        private static bool ExactClass(FTK_playerGameStart row, out int id)
        {
            id = 0;
            return row != null && !string.IsNullOrEmpty(row.m_ID) &&
                ContentRegistry.TryGetSyntheticId(row.m_ID, out id, typeof(FTK_playerGameStartDB)) &&
                object.ReferenceEquals(Content.Db<FTK_playerGameStartDB>().GetEntryByInt(id), row);
        }

        internal static void Apply(FTK_playerGameStart row, FTK_skinset skinset, GameObject backpack)
        {
            int id;
            Dictionary<int, EnemyRendererMesh[]> skins;
            EnemyRendererMesh[] meshes;
            if (backpack == null || skinset == null || !ExactClass(row, out id) ||
                !Registrations.TryGetValue(id, out skins) ||
                !skins.TryGetValue(Content.Db<FTK_skinsetDB>().GetIntFromID(skinset.m_ID), out meshes)) return;
            // The native backpack is an independent, replaceable child. Keeping its lease on that
            // root survives avatar cloning and native death detachment without owning the avatar.
            ExplicitEnemyMeshSwap.ApplyToObject("player-backpack:" + row.m_ID, backpack, meshes, null, true);
        }
    }

    [HarmonyPatch(typeof(CharacterEventListener), "UpdateBackpack", new Type[0])]
    internal static class PlayerBackpackPatch
    {
        private static void Postfix(CharacterEventListener __instance)
        {
            try
            {
                if (__instance == null || __instance.m_Backpack == null) return;
                CharacterOverworld cow = __instance.m_CharacterOverworld;
                uiQuickPlayerCreate preview = __instance.m_uiQuickPlayerCreate;
                if (cow != null)
                    PlayerBackpackRegistry.Apply(cow.GetDBEntry(), cow.GetSkinset(), __instance.m_Backpack.gameObject);
                else if (preview != null)
                    PlayerBackpackRegistry.Apply(preview.GetClassDBEntry(), preview.GetSkinset(), __instance.m_Backpack.gameObject);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[player-backpack] native backpack retained: " + e.Message); }
        }
    }
}
