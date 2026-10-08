using System;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>A rigid helmet model selected by an exact native skinset or registered custom race.</summary>
    public sealed class ItemHeadProfile
    {
        public FTK_skinset.ID NativeSkinset { get; private set; }
        public string RaceGuid { get; private set; }
        public string RaceKey { get; private set; }
        public ItemRendererMesh Model { get; private set; }
        public HeadFaceOcclusion FaceOcclusion { get; private set; }

        private ItemHeadProfile(FTK_skinset.ID skinset, string raceGuid, string raceKey,
            ItemRendererMesh model, HeadFaceOcclusion faceOcclusion)
        { NativeSkinset = skinset; RaceGuid = raceGuid; RaceKey = raceKey; Model = model; FaceOcclusion = faceOcclusion; }

        public static ItemHeadProfile ForNativeSkinset(FTK_skinset.ID skinset, ItemRendererMesh model)
        { return ForNativeSkinset(skinset, model, null); }
        public static ItemHeadProfile ForNativeSkinset(FTK_skinset.ID skinset, ItemRendererMesh model,
            HeadFaceOcclusion faceOcclusion)
        { return new ItemHeadProfile(skinset, null, null, model, faceOcclusion); }

        public static ItemHeadProfile ForCustomRace(string modGuid, string raceKey, ItemRendererMesh model)
        { return ForCustomRace(modGuid, raceKey, model, null); }
        public static ItemHeadProfile ForCustomRace(string modGuid, string raceKey, ItemRendererMesh model,
            HeadFaceOcclusion faceOcclusion)
        { return new ItemHeadProfile(FTK_skinset.ID.None, modGuid, raceKey, model, faceOcclusion); }
    }

    public static partial class Content
    {
        /// <summary>Bind exact per-wearer rigid models to a registered helmet. Unmatched wearers retain itemModels.</summary>
        public static bool SetItemHeadProfilesFromGlb(FTK_items item, params ItemHeadProfile[] profiles)
        {
            int id;
            if (item == null || item.m_ObjectType != FTK_itembase.ObjectType.helmet ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB)) ||
                !object.ReferenceEquals(item, Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id))) return false;
            string error;
            if (!ItemHeadProfileRegistry.Register(id, profiles, out error))
            {
                Plugin.Log.LogWarning("[item-head-profile] registration rejected: " + error);
                return false;
            }
            return true;
        }
    }
}
