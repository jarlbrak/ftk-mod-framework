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
            int classId;
            if (!ContentRegistry.TryGetSyntheticId(classRow.m_ID, out classId, typeof(FTK_playerGameStartDB))) return;
            if (!object.ReferenceEquals(Content.Db<FTK_playerGameStartDB>().GetEntryByInt(classId), classRow)) return;
            Dictionary<int, PlayerMeshPlan> skins;
            if (!Registrations.TryGetValue(classId, out skins)) return;
            int skinId = Content.Db<FTK_skinsetDB>().GetIntFromID(skinset.m_ID);
            PlayerMeshPlan assignments;
            if (!skins.TryGetValue(skinId, out assignments)) return;
            bool applied = assignments.Apply("player:" + classRow.m_ID + ":" + skinset.m_ID, avatar);
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

    [HarmonyPatch(typeof(CharacterDummy), "CreateAvatar", new Type[] { typeof(bool) })]
    internal static class PlayerCombatMeshLeasePatch
    {
        // Runs on success and on a native exception after Instantiate assigned m_EventListener. Retain any
        // cloned resource lease immediately, even for inactive clones whose Awake has not run. Never swallow
        // the native exception. Awake and this finalizer can both run without acquiring a second reference.
        private static Exception Finalizer(CharacterDummy __instance, Exception __exception)
        {
            try
            {
                if (__instance != null && __instance.m_CharacterOverworld != null && __instance.m_EventListener != null)
                {
                    EnemyMeshResources owner = __instance.m_EventListener.GetComponent<EnemyMeshResources>();
                    if (owner != null) owner.EnsureRetained();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[player-mesh] combat lease hook: " + e.Message); }
            return __exception;
        }
    }
}
