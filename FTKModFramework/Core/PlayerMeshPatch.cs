using System;
using System.Collections.Generic;
using HarmonyLib;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>Class-and-skinset scoped visual registrations; no vanilla DB rows or skinsets are modified.</summary>
    internal static class PlayerMeshRegistry
    {
        private static readonly Dictionary<int, Dictionary<int, PlayerMeshPlan>> Registrations =
            new Dictionary<int, Dictionary<int, PlayerMeshPlan>>();

        internal static bool Register(FTK_playerGameStart classRow, FTK_skinset.ID skinset, PlayerRendererMesh[] meshes)
        {
            return Register(classRow, skinset, meshes, new PlayerApparelMesh[0]);
        }

        internal static bool Register(FTK_playerGameStart classRow, FTK_skinset.ID skinset,
            PlayerRendererMesh[] meshes, PlayerApparelMesh[] apparel)
        {
            if (classRow == null || string.IsNullOrEmpty(classRow.m_ID)) return Reject("custom class row is required");
            int classId;
            if (!ContentRegistry.TryGetSyntheticId(classRow.m_ID, out classId, typeof(FTK_playerGameStartDB)))
                return Reject("class must have been registered through Content.AddClass");
            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();
            if (!object.ReferenceEquals(db.GetEntryByInt(classId), classRow))
                return Reject("class argument is not the exact registered DB row");
            bool member = false;
            if (classRow.m_Skinsets != null)
                foreach (FTK_skinset.ID id in classRow.m_Skinsets) if (id == skinset) member = true;
            if (!member || Content.Db<FTK_skinsetDB>().GetEntry(skinset) == null)
                return Reject("skinset must be a valid member of the custom class's m_Skinsets");
            string error;
            PlayerMeshPlan snapshot;
            if (!PlayerMeshPlan.TryCreate(meshes, apparel, out snapshot, out error)) return Reject(error);
            Dictionary<int, PlayerMeshPlan> skins;
            if (!Registrations.TryGetValue(classId, out skins))
            {
                skins = new Dictionary<int, PlayerMeshPlan>();
                Registrations.Add(classId, skins);
            }
            skins[(int)skinset] = snapshot;
            Plugin.Log.LogInfo("[player-mesh] registered " + snapshot.RequiredCount + " required renderers and " +
                snapshot.ApparelCount + " conditional apparel renderers for class '" +
                classRow.m_ID + "', skinset " + skinset + ".");
            return true;
        }

        private static bool Reject(string reason)
        {
            Plugin.Log.LogWarning("SetClassBodyMeshesFromGlb: " + reason + "; registration unchanged.");
            return false;
        }

        internal static void Apply(FTK_playerGameStart classRow, FTK_skinset skinset, CharacterEventListener avatar)
        {
            if (classRow == null || skinset == null || avatar == null) return;
            EnemyMeshResources existing = avatar.GetComponent<EnemyMeshResources>();
            if (existing != null && (!existing.VisualResourcesOnly || !existing.ValidLease()))
            {
                if (!existing.VisualResourcesOnly && existing.Applied) existing.EnsureRetained();
                return;
            }
            PlayerMeshPlan plan = PlayerRaceRegistry.GetPlan(classRow, skinset);
            PlayerMeshPlan racePlan = plan;
            int classId;
            Dictionary<int, PlayerMeshPlan> skins;
            if (plan == null && ContentRegistry.TryGetSyntheticId(classRow.m_ID, out classId, typeof(FTK_playerGameStartDB)) &&
                object.ReferenceEquals(Content.Db<FTK_playerGameStartDB>().GetEntryByInt(classId), classRow) &&
                Registrations.TryGetValue(classId, out skins))
                skins.TryGetValue(Content.Db<FTK_skinsetDB>().GetIntFromID(skinset.m_ID), out plan);
            EnemyRendererMesh[] resolved = ItemApparelRegistry.Resolve(avatar);
            string[] skipped;
            string error;
            if (plan != null && !plan.TryResolve(avatar.transform, resolved, out resolved, out skipped, out error))
            {
                Plugin.Log.LogError("[player-mesh] class outfit rejected: " + error);
                return;
            }
            if (resolved.Length == 0) return;
            Action<UnityEngine.Renderer, UnityEngine.Material> prepare = null;
            if (racePlan != null)
                prepare = delegate(UnityEngine.Renderer renderer, UnityEngine.Material material)
                {
                    if (racePlan.HasTexturedRequiredPath(ExplicitEnemyMeshSwap.RelativePath(avatar.transform, renderer.transform)))
                        ExplicitMaterialOptions.PreservePalette(material);
                };
            bool applied = ExplicitEnemyMeshSwap.Apply("player:" + classRow.m_ID, avatar, resolved, prepare, true);
            Plugin.Log.LogInfo("[player-mesh] " + (applied ? "applied" : "rejected") + " class '" +
                classRow.m_ID + "', skinset '" + skinset.m_ID + "'.");
        }
    }

    // Per-avatar application is intentionally not globally guarded: each native assembly creates a fresh clone.
    [HarmonyPatch(typeof(CharacterEventListener), "CreateAvatar", new Type[] { typeof(CharacterOverworld) })]
    internal static class PlayerOverworldMeshPatch
    {
        private static void Postfix(CharacterOverworld _cow, CharacterEventListener __result)
        {
            try
            {
                if (_cow != null) PlayerMeshRegistry.Apply(_cow.GetDBEntry(), _cow.GetSkinset(), __result);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[player-mesh] overworld hook: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CharacterEventListener), "CreateAvatar", new Type[] { typeof(uiQuickPlayerCreate) })]
    internal static class PlayerPreviewMeshPatch
    {
        private static void Postfix(uiQuickPlayerCreate _uiCreate, CharacterEventListener __result)
        {
            try
            {
                if (_uiCreate != null) PlayerMeshRegistry.Apply(_uiCreate.GetClassDBEntry(), _uiCreate.GetSkinset(), __result);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[player-mesh] preview hook: " + e.Message); }
        }
    }

}
