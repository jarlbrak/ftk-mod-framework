using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    internal static class ItemHeadProfileRegistry
    {
        private sealed class Entry
        {
            internal FTK_skinset.ID NativeSkinset;
            internal string RaceGuid, RaceKey;
            internal EnemyRendererMesh Model;
            internal HeadFaceOcclusion Face;
        }

        private static Dictionary<int, Entry[]> items = new Dictionary<int, Entry[]>();
        internal static bool HasProfiles(int id) { return items.ContainsKey(id); }
        internal static int ReloadCount { get { return items.Count; } }
        internal static IEnumerable<KeyValuePair<int, EnemyRendererMesh[]>> ReloadPlans()
        {
            foreach (KeyValuePair<int, Entry[]> pair in items)
            {
                EnemyRendererMesh[] assignments = new EnemyRendererMesh[pair.Value.Length];
                for (int i = 0; i < assignments.Length; i++) assignments[i] = pair.Value[i].Model;
                yield return new KeyValuePair<int, EnemyRendererMesh[]>(pair.Key, assignments);
            }
        }

        internal static Action SuspendForReload()
        {
            Dictionary<int, Entry[]> prior = items;
            items = new Dictionary<int, Entry[]>();
            return delegate { items = prior; };
        }

        internal static bool Register(int id, ItemHeadProfile[] profiles, out string error)
        {
            error = null;
            if (profiles == null || profiles.Length == 0 || profiles.Length > 256)
            { error = "head profiles require 1..256 entries"; return false; }
            string fallbackPath;
            if (!ItemModelRegistry.TryGetHeadFallbackPath(id, out fallbackPath))
            { error = "exactly one rigid itemModels fallback is required"; return false; }
            List<Entry> prepared = new List<Entry>();
            foreach (ItemHeadProfile profile in profiles)
            {
                if (profile == null || profile.Model == null) { error = "profile and model are required"; return false; }
                bool custom = profile.RaceGuid != null || profile.RaceKey != null;
                if (custom ? profile.NativeSkinset != FTK_skinset.ID.None ||
                    string.IsNullOrEmpty(profile.RaceGuid) || string.IsNullOrEmpty(profile.RaceKey) ||
                    profile.RaceGuid.IndexOf(':') >= 0 || profile.RaceKey.IndexOf(':') >= 0
                    : profile.NativeSkinset == FTK_skinset.ID.None)
                { error = "one exact native skinset or race identity is required"; return false; }
                if (!custom && Content.Db<FTK_skinsetDB>().GetEntry(profile.NativeSkinset) == null)
                { error = "native skinset is not registered"; return false; }
                if (!custom && !Enum.IsDefined(typeof(FTK_skinset.ID), profile.NativeSkinset))
                { error = "native selector must be a declared game skinset"; return false; }
                foreach (Entry prior in prepared)
                    if (custom ? prior.RaceGuid == profile.RaceGuid && prior.RaceKey == profile.RaceKey :
                        prior.RaceGuid == null && prior.NativeSkinset == profile.NativeSkinset)
                    { error = "duplicate head profile selector"; return false; }
                ItemRendererMesh model = profile.Model;
                if (model.RendererPath != fallbackPath)
                { error = "profile renderer path must equal itemModels fallback path"; return false; }
                EnemyRendererMesh assignment = EnemyRendererMesh.ForStaticRenderer(model.RendererPath,
                    model.GlbFileName, model.TextureFileName, true, model.Matte);
                if (!ExplicitEnemyMeshSwap.ValidateAssignments(new[] { assignment }, out error)) return false;
                prepared.Add(new Entry { NativeSkinset = profile.NativeSkinset, RaceGuid = profile.RaceGuid,
                    RaceKey = profile.RaceKey, Model = assignment, Face = profile.FaceOcclusion });
            }
            items[id] = prepared.ToArray();
            return true;
        }

        internal static bool TrySelect(int id, FTK_playerGameStart row, FTK_skinset skinset, out EnemyRendererMesh model)
        {
            HeadFaceOcclusion face;
            return TrySelect(id, row, skinset, out model, out face);
        }

        internal static bool TrySelect(int id, FTK_playerGameStart row, FTK_skinset skinset,
            out EnemyRendererMesh model, out HeadFaceOcclusion face)
        {
            model = null; face = null;
            Entry[] profiles;
            if (row == null || skinset == null || !items.TryGetValue(id, out profiles)) return false;
            string guid, key;
            bool custom = PlayerRaceRegistry.TryGetIdentity(row, skinset, out guid, out key);
            Entry native = null;
            foreach (Entry profile in profiles)
            {
                if (custom && profile.RaceGuid == guid && profile.RaceKey == key)
                { model = profile.Model; face = profile.Face; return true; }
                if (!custom && profile.RaceGuid == null &&
                    object.ReferenceEquals(Content.Db<FTK_skinsetDB>().GetEntry(profile.NativeSkinset), skinset))
                    native = profile;
            }
            if (native == null) return false;
            model = native.Model; face = native.Face;
            return true;
        }

        internal static void Apply(CharacterEventListener avatar)
        {
            if (avatar == null || avatar.m_Helmet == null) return;
            CharacterOverworld owner = avatar.m_CharacterOverworld;
            uiQuickPlayerCreate preview = avatar.m_uiQuickPlayerCreate;
            if (owner == null && preview == null) return;
            if (owner != null && owner.m_HideHelmet) return;
            PlayerInventory inventory = owner != null ? owner.m_PlayerInventory : preview.m_PlayerInventory;
            if (inventory == null) return;
            FTK_itembase.ID id = inventory.Get(PlayerInventory.ContainerID.Head).GetOne();
            if (id == FTK_itembase.ID.None || !HasProfiles((int)id)) return;
            FTK_playerGameStart row = owner != null ? owner.GetDBEntry() : preview.GetClassDBEntry();
            FTK_skinset skinset = owner != null ? owner.GetSkinset() : preview.GetSkinset();
            EnemyRendererMesh model;
            if (!TrySelect((int)id, row, skinset, out model))
            {
                ItemModelRegistry.Apply(id, avatar.m_Helmet.gameObject);
                return;
            }
            GameObject helmet = avatar.m_Helmet.gameObject;
            if (!ExplicitEnemyMeshSwap.ApplyToObject("head-profile:" + (int)id, helmet,
                new[] { model }, null, true))
            {
                Plugin.Log.LogWarning("[item-head-profile] selected helmet model rejected for " + id);
                // An incomplete swap rollback may retain a lease. Never start another transaction over it.
                if (helmet.GetComponent<EnemyMeshResources>() == null)
                    ItemModelRegistry.Apply(id, helmet);
            }
        }
    }
}
