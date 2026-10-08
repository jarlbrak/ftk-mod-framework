using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    internal sealed class HeadProfileAvatarState : MonoBehaviour
    {
        [SerializeField]
        internal bool Ready;
        [SerializeField]
        internal Helmet ProcessedHelmet;
        private void OnEnable() { if (Ready) HeadProfileCoordinator.OnHelmetUpdated(GetComponent<CharacterEventListener>()); }
    }

    internal static class HeadProfileCoordinator
    {
        internal static void MarkReady(CharacterEventListener avatar)
        {
            if (avatar == null) return;
            HeadProfileAvatarState state = avatar.GetComponent<HeadProfileAvatarState>() ??
                avatar.gameObject.AddComponent<HeadProfileAvatarState>();
            state.Ready = true;
            OnHelmetUpdated(avatar);
        }

        internal static void OnHelmetUpdated(CharacterEventListener avatar)
        {
            HeadProfileAvatarState state = avatar == null ? null : avatar.GetComponent<HeadProfileAvatarState>();
            if (state == null || !state.Ready) return; // Initial native UpdateHelmet precedes race/body assembly.
            if (!avatar.gameObject.activeInHierarchy || state.ProcessedHelmet == avatar.m_Helmet) return;
            HeadFaceResources.Prune();
            HeadFaceResources old = avatar.GetComponent<HeadFaceResources>();
            if (old != null && !old.Restore())
            { Plugin.Log.LogError("[head-face] prior face references diverged; new helmet profile rejected"); return; }
            if (avatar.m_Helmet == null || !avatar.m_Helmet.gameObject.activeInHierarchy) return;
            CharacterOverworld owner = avatar.m_CharacterOverworld;
            uiQuickPlayerCreate preview = avatar.m_uiQuickPlayerCreate;
            if (owner == null && preview == null) return;
            if (owner != null && owner.m_HideHelmet) return;
            PlayerInventory inventory = owner != null ? owner.m_PlayerInventory : preview.m_PlayerInventory;
            if (inventory == null) return;
            FTK_itembase.ID item = inventory.Get(PlayerInventory.ContainerID.Head).GetOne();
            if (item == FTK_itembase.ID.None || !ItemHeadProfileRegistry.HasProfiles((int)item)) return;
            FTK_playerGameStart row = owner != null ? owner.GetDBEntry() : preview.GetClassDBEntry();
            FTK_skinset skinset = owner != null ? owner.GetSkinset() : preview.GetSkinset();
            EnemyRendererMesh model; HeadFaceOcclusion volume;
            if (!ItemHeadProfileRegistry.TrySelect((int)item, row, skinset, out model, out volume))
            { ItemModelRegistry.Apply(item, avatar.m_Helmet.gameObject); ApplyHair(item, avatar); state.ProcessedHelmet=avatar.m_Helmet; return; }
            if (volume == null)
            { ItemHeadProfileRegistry.Apply(avatar); ApplyHair(item, avatar); state.ProcessedHelmet=avatar.m_Helmet; return; }
            // A freshly assembled avatar may remain invisible until native SetVisible(true).
            try
            {
                Transform body = ExactPath(avatar.transform, volume.BodyPath);
                SkinnedMeshRenderer[] bodyRenderers = body.GetComponents<SkinnedMeshRenderer>();
                if (bodyRenderers.Length == 1 && (!bodyRenderers[0].enabled || !body.gameObject.activeInHierarchy))
                    return;
                if (bodyRenderers.Length != 1) throw new InvalidOperationException("Exact body renderer required");
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[head-face] source body unavailable: " + error.Message);
                ItemModelRegistry.Apply(item, avatar.m_Helmet.gameObject);
                ApplyHair(item, avatar); state.ProcessedHelmet=avatar.m_Helmet;
                return;
            }
            ApplyHair(item, avatar); // Establish native hair visibility before target admission and hide policy.
            try { ApplyClipped(item, avatar, row, skinset, model, volume); }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[head-face] exact profile rejected: " + error.Message);
                if (avatar.m_Helmet.GetComponent<EnemyMeshResources>() == null)
                    ItemModelRegistry.Apply(item, avatar.m_Helmet.gameObject);
            }
            state.ProcessedHelmet=avatar.m_Helmet;
        }

        private static void ApplyHair(FTK_itembase.ID item, CharacterEventListener avatar)
        { ItemModelRegistry.ApplyAttachedHelmetHairVisibility(item, avatar); }

        private static Transform ExactPath(Transform root, string path)
        {
            Transform result = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (ExplicitEnemyMeshSwap.RelativePath(root, candidate) == path)
                { if (result != null) throw new InvalidOperationException("Ambiguous path " + path); result = candidate; }
            if (result == null) throw new InvalidOperationException("Missing path " + path);
            return result;
        }

        private static SkinnedMeshRenderer ExactFace(Transform root, string path)
        {
            Transform target = ExactPath(root, path);
            SkinnedMeshRenderer[] renderers = target.GetComponents<SkinnedMeshRenderer>();
            if (renderers.Length != 1 || renderers[0].sharedMesh == null ||
                !renderers[0].enabled || !renderers[0].gameObject.activeInHierarchy)
                throw new InvalidOperationException("Exact active face renderer required " + path);
            return renderers[0];
        }

        private static Mesh AttestSource(CharacterEventListener avatar, FTK_playerGameStart row,
            FTK_skinset skinset, string path, SkinnedMeshRenderer current)
        {
            PlayerMeshPlan plan = PlayerRaceRegistry.GetPlan(row, skinset);
            if (plan == null)
            {
                if (skinset == null || skinset.m_Avatar == null)
                    throw new InvalidOperationException("Native skinset prefab unavailable");
                Transform sourceTarget = ExactPath(skinset.m_Avatar.transform, path);
                SkinnedMeshRenderer[] sourceRenderers = sourceTarget.GetComponents<SkinnedMeshRenderer>();
                if (sourceRenderers.Length != 1 || sourceRenderers[0].sharedMesh == null)
                    throw new InvalidOperationException("Exact native prefab face source unavailable");
                SkinnedMeshRenderer native = sourceRenderers[0];
                if (current.sharedMesh != native.sharedMesh)
                    throw new InvalidOperationException("Native face source reference changed");
                return native.sharedMesh;
            }
            string registeredModel;
            if (!plan.TryGetRequiredModel(path, out registeredModel) ||
                current.sharedMesh.name != "ftkmf_glb_" + registeredModel)
                throw new InvalidOperationException("Custom face path/model is not registered for this race");
            EnemyMeshResources lease = avatar.GetComponent<EnemyMeshResources>();
            if (lease == null || !lease.OwnsExplicitMesh(current))
                throw new InvalidOperationException("Custom face source is not owned by this avatar's race lease");
            return current.sharedMesh;
        }

        private static void AddClip(CharacterEventListener avatar, FTK_playerGameStart row,
            FTK_skinset skinset, string path, Transform helmetMesh, HeadFaceOcclusion volume,
            bool contained, List<SkinnedMeshRenderer> targets, List<Mesh> originals,
            List<Mesh> outputs)
        {
            SkinnedMeshRenderer target = ExactFace(avatar.transform, path);
            Mesh source = AttestSource(avatar, row, skinset, path, target);
            Mesh output = HeadFaceClipper.Clip(target, helmetMesh, volume, source, contained);
            if (output == null)
            {
                if (contained) throw new InvalidOperationException("Body volume did not select strict head geometry");
                return;
            }
            targets.Add(target); originals.Add(source); outputs.Add(output);
        }

        private static void ApplyClipped(FTK_itembase.ID item, CharacterEventListener avatar,
            FTK_playerGameStart row, FTK_skinset skinset, EnemyRendererMesh model,
            HeadFaceOcclusion volume)
        {
            GameObject helmet = avatar.m_Helmet.gameObject;
            Transform helmetMesh = ExactPath(helmet.transform, model.RendererPath);
            if (helmetMesh.GetComponents<MeshFilter>().Length != 1 ||
                helmetMesh.GetComponents<MeshRenderer>().Length != 1)
                throw new InvalidOperationException("Exact rigid helmet MeshFilter/MeshRenderer required");
            List<SkinnedMeshRenderer> targets = new List<SkinnedMeshRenderer>();
            List<Mesh> originals = new List<Mesh>(), outputs = new List<Mesh>();
            bool committed = false;
            bool attemptedCommit = false;
            HeadFaceResources owner = avatar.GetComponent<HeadFaceResources>() ??
                avatar.gameObject.AddComponent<HeadFaceResources>();
            try
            {
                // Every approved clipped profile uses strict Head_M plus all-corner interior body omission.
                AddClip(avatar,row,skinset,volume.BodyPath,helmetMesh,volume,true,targets,originals,outputs);
                if (volume.UpperHair == HeadHairMode.ClipStrictHead)
                    AddClip(avatar,row,skinset,"hairTop",helmetMesh,volume,false,targets,originals,outputs);
                if (volume.LowerHair == HeadHairMode.ClipStrictHead)
                    AddClip(avatar,row,skinset,"hairBottom",helmetMesh,volume,false,targets,originals,outputs);
                else if (volume.LowerHair == HeadHairMode.HideRenderer)
                {
                    SkinnedMeshRenderer lower = ExactFace(avatar.transform,"hairBottom");
                    Mesh source = AttestSource(avatar,row,skinset,"hairBottom",lower);
                    targets.Add(lower); originals.Add(source); outputs.Add(HeadFaceClipper.EmptyLowerHair(source));
                }
                attemptedCommit = true;
                if (!owner.Commit(targets.ToArray(),originals.ToArray(),outputs.ToArray()))
                    throw new InvalidOperationException("Face commit rejected");
                committed = true;
                if (!ExplicitEnemyMeshSwap.ApplyToObject("head-profile:"+(int)item,helmet,
                    new[] {model},null,true))
                    throw new InvalidOperationException("Rigid helmet model rejected after face preparation");
            }
            catch
            {
                if (committed && !owner.Restore())
                    Plugin.Log.LogError("[head-face] face restore failed; generated meshes retained");
                throw;
            }
            finally
            {
                if (!committed && (!attemptedCommit || !owner.LastCommitAdopted) && !owner.Active)
                    foreach (Mesh mesh in outputs)
                        if (mesh != null) HotReload.PaladinResourceState.DestroyTracked(mesh);
            }
        }
    }
}
