using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Binds the legacy <c>enkrakenhead</c> resource prefab only after the native combat clone has its
    /// real CharacterEventListener and weapon Animator controller. This is a route-specific compatibility
    /// layer: it never assigns a controller, invokes gameplay callbacks, or changes the shared Root_M.
    /// </summary>
    [HarmonyPatch(typeof(EnemyDummy), "InitEnemyDummyForCombat", new Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    [HarmonyPriority(Priority.Last)]
    internal static class LegacyKrakenResourceAdapterPatch
    {
        private static void Postfix(EnemyDummy __instance)
        {
            try
            {
                LegacyKrakenResourceAdapter.TryAttach(__instance);
            }
            catch (Exception e)
            {
                // This patch is visual compatibility only. A bad candidate must leave native combat intact.
                Plugin.Log.LogWarning("[kraken-adapter] candidate inspection failed: " + e.Message);
            }
        }
    }

    internal static class LegacyKrakenResourceAdapter
    {
        internal const string ResourcePrefabPath = "enkrakenhead";

        internal static void TryAttach(EnemyDummy dummy)
        {
            if (dummy == null || dummy.m_EnemyCombat == null || dummy.m_EventListener == null) return;

            FTK_enemyCombat enemyRow = dummy.m_EnemyCombat;

            CharacterEventListener resource;
            if (!TryGetExactResource(out resource)) return;

            // Resource identity is intentional. Matching a similarly-shaped prefab is not a route grant.
            if (enemyRow.m_EnemyAsset != resource) return;

            CharacterEventListener target = dummy.m_EventListener;
            if (target.m_Dummy != dummy) return;
            LegacyKrakenResourceAdapterLease existing = target.GetComponent<LegacyKrakenResourceAdapterLease>();
            if (existing != null) return;

            LegacyKrakenResourceAdapterLease lease = target.gameObject.AddComponent<LegacyKrakenResourceAdapterLease>();
            lease.Configure(dummy, target, resource, enemyRow);
        }

        internal static bool TryGetExactResource(out CharacterEventListener resource)
        {
            resource = null;
            GameObject prefab = Resources.Load<GameObject>(ResourcePrefabPath);
            if (prefab == null || prefab.transform.parent != null) return false;

            CharacterEventListener[] listeners = prefab.GetComponentsInChildren<CharacterEventListener>(true);
            if (listeners == null || listeners.Length != 1) return false;

            CharacterEventListener root = prefab.GetComponent<CharacterEventListener>();
            Animator animator = prefab.GetComponent<Animator>();
            if (root == null || listeners[0] != root || animator == null) return false;

            resource = root;
            return true;
        }
    }

    /// <summary>
    /// Per-combat clone lease. It owns only controller-free sampling hierarchies and PlayableGraphs.
    /// Meshes, materials, and textures remain owned by the existing visual-resource lease.
    /// </summary>
    internal sealed class LegacyKrakenResourceAdapterLease : MonoBehaviour
    {
        private const float MatrixTolerance = 0.00001f;
        private const float MinimumScale = 0.00000001f;
        private const int ClipCount = 5;
        private const int BankCount = 2;
        private const string ControllerName = "krakenHeadController";
        private const string RendererPath = "krakenHead";

        private static readonly string[] OldPaths =
        {
            "Root_M/joint1",
            "Root_M/joint1/neck",
            "Root_M/joint1/neck/head",
            "Root_M/joint1/neck/head/topHead",
            "Root_M/joint1/neck/jaw"
        };

        private static readonly string[] ModernPaths =
        {
            "Root_M/base/body",
            "Root_M/base/body/neck",
            "Root_M/base/body/neck/head",
            "Root_M/base/body/neck/head/topHead"
        };

        // The jaw branches from neck. It remains native during appearance and must not pass through
        // ToLocals, which intentionally converts only the linear articulated mapping.
        private static readonly string[] OldMappedPaths =
        {
            "Root_M/joint1",
            "Root_M/joint1/neck",
            "Root_M/joint1/neck/head",
            "Root_M/joint1/neck/head/topHead"
        };

        private static readonly string[] ClipNames =
        {
            "krakenIdle",
            "krakenDamage",
            "krakenDisappear",
            "kraken_appear",
            "krakenAttack"
        };

        // Pinned controller5973 inventory: exact full-path hash, one expected clip, loop flag, speed 1,
        // and state length equal to that clip length. An unlisted state is never sampled optimistically.
        private static readonly NativeStateContract[] StateContracts =
        {
            new NativeStateContract("Base Layer.IDLE", "krakenIdle", true),
            new NativeStateContract("Base Layer.INTRO", "krakenIdle", true),
            new NativeStateContract("Base Layer.DEFEND", "krakenDamage", false),
            new NativeStateContract("Base Layer.ATTACK", "krakenAttack", false),
            new NativeStateContract("Base Layer.VICTORY", "krakenDisappear", false),
            new NativeStateContract("Base Layer.PASSIVE VICTORY", "krakenDisappear", false),
            new NativeStateContract("Base Layer.DAMAGEDHEAVY", "krakenDamage", false),
            new NativeStateContract("Base Layer.DEATH", "krakenDisappear", false),
            new NativeStateContract("Base Layer.ATTACKCRIT", "krakenAttack", false),
            new NativeStateContract("Base Layer.ATTACKPROF", "krakenAttack", false),
            new NativeStateContract("Base Layer.DODGE", "krakenDamage", false),
            new NativeStateContract("Base Layer.DAMAGED", "krakenDamage", false),
            new NativeStateContract("Base Layer.DAMAGED STUN", "krakenDamage", false),
            new NativeStateContract("Base Layer.OverworldAppear", "kraken_appear", false),
            new NativeStateContract("Base Layer.DEATHLIGHT", "krakenDisappear", false)
        };

        private const int AppearanceClip = 3;

        // A combat clone can be destroyed while inactive, in which case Unity may omit its OnDestroy callback.
        // The active framework plugin prunes this list so an owned graph cannot outlive such a clone.
        private static readonly List<LegacyKrakenResourceAdapterLease> Owners =
            new List<LegacyKrakenResourceAdapterLease>();

        private EnemyDummy _owner;
        private FTK_enemyCombat _enemyRow;
        private CharacterEventListener _target;
        private CharacterEventListener _resource;
        private string _enemyId;
        private bool _configured;
        private bool _initialized;
        private bool _disabled;
        private bool _disposed;
        private bool _ownerRegistered;

        private Animator _nativeAnimator;
        private RuntimeAnimatorController _nativeController;
        private Transform _targetTop;
        private Transform _sharedRoot;
        private readonly Transform[] _targets = new Transform[OldPaths.Length];
        private readonly int[] _targetIds = new int[OldPaths.Length];
        private readonly Transform[] _expectedParents = new Transform[OldPaths.Length];

        private SkinnedMeshRenderer _renderer;
        private int _rendererId;
        private Mesh _rendererMesh;
        private Transform[] _rendererBones;
        private Matrix4x4[] _bindposes;

        private readonly Matrix4x4[] _modernRest = new Matrix4x4[ModernPaths.Length];
        private readonly Matrix4x4[] _oldRest = new Matrix4x4[ModernPaths.Length];
        private LocalTrs _jawRest;

        private ClipSampler _modernSampler;
        private ClipSampler _oldSampler;

        internal void Configure(EnemyDummy owner, CharacterEventListener target, CharacterEventListener resource,
            FTK_enemyCombat enemyRow)
        {
            if (_configured) return;
            _configured = true;
            _owner = owner;
            _enemyRow = enemyRow;
            _target = target;
            _resource = resource;
            _enemyId = enemyRow == null ? null : enemyRow.m_ID;
            Owners.Add(this);
            _ownerRegistered = true;
        }

        internal static void PruneDestroyedOwners()
        {
            for (int i = Owners.Count - 1; i >= 0; i--)
            {
                LegacyKrakenResourceAdapterLease owner = Owners[i];
                if (owner != null) continue;
                Owners.RemoveAt(i);
                // A destroyed Unity object still has a managed wrapper, so its fields can release the graph.
                if ((object)owner != null) owner.DisposeOwned();
            }
        }

        private void LateUpdate()
        {
            if (_disabled || _disposed) return;

            try
            {
                if (!_configured) throw new InvalidOperationException("Adapter lease was not configured.");
                if (!_initialized)
                {
                    Initialize();
                    _initialized = true;
                }
                ApplyFrame();
            }
            catch (Exception e)
            {
                DisableForOwner(e);
            }
        }

        private void OnDestroy()
        {
            DisposeOwned();
        }

        private void Initialize()
        {
            ValidateOwnerInvariants();
            if (_target == null || _resource == null)
                throw new InvalidOperationException("Target or resource disappeared before adapter initialization.");

            _targetTop = _target.transform;
            _nativeAnimator = _target.GetComponent<Animator>();
            if (_nativeAnimator == null || !_nativeAnimator.isInitialized)
                throw new InvalidOperationException("Real CEL Animator is not initialized.");

            _nativeController = _nativeAnimator.runtimeAnimatorController;
            if (_nativeController == null || _nativeController.name != ControllerName)
                throw new InvalidOperationException("Expected the real weapon-installed " + ControllerName + " controller.");
            if (_nativeAnimator.layerCount != 1)
                throw new InvalidOperationException("Legacy Kraken adapter requires one native Animator layer.");

            Dictionary<string, AnimationClip> clips = ReadExactClips(_nativeController);
            CaptureTargetTopology();
            CaptureSourceRest();

            FTK_enemyCombat nativeKraken = Content.Db<FTK_enemyCombatDB>().GetEntryByStringID("krakenHead");
            if (nativeKraken == null || nativeKraken.m_EnemyAsset == null)
                throw new InvalidOperationException("Native krakenHead source prefab is unavailable.");

            CharacterEventListener modernSource = nativeKraken.m_EnemyAsset;
            Animator modernAnimator = modernSource.GetComponent<Animator>();
            Animator oldAnimator = _resource.GetComponent<Animator>();
            if (modernAnimator == null || oldAnimator == null)
                throw new InvalidOperationException("Required source Animator is unavailable.");

            _modernSampler = new ClipSampler("modern", modernSource.transform, modernAnimator.avatar, clips);
            _oldSampler = new ClipSampler("old", _resource.transform, oldAnimator.avatar, clips);
        }

        private static Dictionary<string, AnimationClip> ReadExactClips(RuntimeAnimatorController controller)
        {
            AnimationClip[] found = controller.animationClips;
            if (found == null || found.Length != StateContracts.Length)
                throw new InvalidOperationException("Expected exactly fifteen Kraken controller state clip references.");

            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            for (int i = 0; i < found.Length; i++)
            {
                AnimationClip clip = found[i];
                if (clip == null || Array.IndexOf(ClipNames, clip.name) < 0)
                    throw new InvalidOperationException("Unexpected or ambiguous Kraken controller clip.");

                AnimationClip existing;
                if (clips.TryGetValue(clip.name, out existing))
                {
                    if (!ReferenceEquals(existing, clip))
                        throw new InvalidOperationException("Unexpected or ambiguous Kraken controller clip.");
                    continue;
                }
                clips.Add(clip.name, clip);
            }
            if (clips.Count != ClipCount)
                throw new InvalidOperationException("Kraken controller clip set changed.");
            return clips;
        }

        private void CaptureTargetTopology()
        {
            _sharedRoot = ResolveExact(_targetTop, "Root_M");
            if (_sharedRoot == null) throw new InvalidOperationException("Missing target Root_M.");

            for (int i = 0; i < OldPaths.Length; i++)
            {
                Transform target = ResolveExact(_targetTop, OldPaths[i]);
                if (target == null) throw new InvalidOperationException("Missing target palette path " + OldPaths[i] + ".");
                _targets[i] = target;
                _targetIds[i] = target.GetInstanceID();
            }

            _expectedParents[0] = _sharedRoot;
            _expectedParents[1] = _targets[0];
            _expectedParents[2] = _targets[1];
            _expectedParents[3] = _targets[2];
            _expectedParents[4] = _targets[1];
            for (int i = 0; i < _targets.Length; i++)
                if (_targets[i].parent != _expectedParents[i])
                    throw new InvalidOperationException("Unexpected target palette parent at " + OldPaths[i] + ".");

            Transform rendererTransform = ResolveExact(_targetTop, RendererPath);
            if (rendererTransform == null) throw new InvalidOperationException("Missing target renderer path " + RendererPath + ".");
            SkinnedMeshRenderer[] renderers = rendererTransform.GetComponents<SkinnedMeshRenderer>();
            if (renderers.Length != 1 || rendererTransform.GetComponents<MeshRenderer>().Length != 0)
                throw new InvalidOperationException("Expected one skinned renderer at " + RendererPath + ".");

            _renderer = renderers[0];
            _rendererId = _renderer.GetInstanceID();
            _rendererMesh = _renderer.sharedMesh;
            if (_rendererMesh == null) throw new InvalidOperationException("Target renderer has no mesh.");

            _rendererBones = _renderer.bones;
            Matrix4x4[] binds = _rendererMesh.bindposes;
            if (_rendererBones == null || _rendererBones.Length != OldPaths.Length ||
                binds == null || binds.Length != OldPaths.Length)
                throw new InvalidOperationException("Target renderer palette or bind-pose count changed.");

            _bindposes = new Matrix4x4[binds.Length];
            for (int i = 0; i < OldPaths.Length; i++)
            {
                if (_rendererBones[i] != _targets[i])
                    throw new InvalidOperationException("Target renderer palette order changed at " + OldPaths[i] + ".");
                _bindposes[i] = binds[i];
                EnsureFinite(_bindposes[i], "target bind pose");
            }

            ValidateTargetInvariants();
        }

        private void CaptureSourceRest()
        {
            Transform oldTop = _resource.transform;
            Transform modernTop;
            FTK_enemyCombat nativeKraken = Content.Db<FTK_enemyCombatDB>().GetEntryByStringID("krakenHead");
            if (nativeKraken == null || nativeKraken.m_EnemyAsset == null)
                throw new InvalidOperationException("Native krakenHead source prefab is unavailable.");
            modernTop = nativeKraken.m_EnemyAsset.transform;

            for (int i = 0; i < ModernPaths.Length; i++)
            {
                Transform modern = ResolveExact(modernTop, ModernPaths[i]);
                Transform old = ResolveExact(oldTop, OldPaths[i]);
                if (modern == null || old == null)
                    throw new InvalidOperationException("Required source path is unavailable.");
                _modernRest[i] = ModelMatrix(modern, modernTop);
                _oldRest[i] = ModelMatrix(old, oldTop);
                Decompose(_modernRest[i], "modern rest");
                Decompose(_oldRest[i], "old rest");
            }

            Transform jaw = ResolveExact(oldTop, OldPaths[4]);
            if (jaw == null) throw new InvalidOperationException("Old jaw source path is unavailable.");
            _jawRest = ReadLocal(jaw);
            Decompose(_jawRest.Matrix(), "old jaw rest");
        }

        private void ApplyFrame()
        {
            ValidateOwnerInvariants();
            ValidateTargetInvariants();
            ValidateNativeAnimator();

            ClipInput current = CaptureInput(_nativeAnimator.GetCurrentAnimatorStateInfo(0),
                _nativeAnimator.GetCurrentAnimatorClipInfo(0), "current");
            ClipInput next = default(ClipInput);
            bool inTransition = _nativeAnimator.IsInTransition(0);
            if (inTransition)
            {
                next = CaptureInput(_nativeAnimator.GetNextAnimatorStateInfo(0),
                    _nativeAnimator.GetNextAnimatorClipInfo(0), "next");
                if (Math.Abs(current.weight + next.weight - 1f) > MatrixTolerance)
                    throw new InvalidOperationException("Native current/next raw weights do not sum to one.");
            }
            else if (Math.Abs(current.weight - 1f) > MatrixTolerance)
            {
                throw new InvalidOperationException("Native non-transition clip does not have full raw weight.");
            }

            bool currentAppearance = current.clipIndex == AppearanceClip;
            bool nextAppearance = inTransition && next.clipIndex == AppearanceClip;
            if (currentAppearance && nextAppearance)
                throw new InvalidOperationException("Two appearance contributors require a separately approved policy.");

            Matrix4x4[] modernSnapshot = null;
            Matrix4x4[] appearanceSnapshot = null;
            float appearanceWeight = 0f;
            bool preserveNativeJaw = currentAppearance || nextAppearance;

            if (currentAppearance || nextAppearance)
            {
                ClipInput appearance = currentAppearance ? current : next;
                ClipInput main = currentAppearance ? next : current;
                appearanceWeight = appearance.weight;
                if (!IsFinite(appearanceWeight) || appearanceWeight < 0f || appearanceWeight > 1f)
                    throw new InvalidOperationException("Appearance raw weight is invalid.");

                _oldSampler.EvaluateSingle(appearance);
                appearanceSnapshot = SnapshotModels(_oldSampler, OldMappedPaths);

                if (inTransition)
                {
                    _modernSampler.EvaluateSingle(main);
                    modernSnapshot = SnapshotModels(_modernSampler, ModernPaths);
                }
            }
            else
            {
                _modernSampler.Evaluate(current, inTransition ? (ClipInput?)next : null);
                modernSnapshot = SnapshotModels(_modernSampler, ModernPaths);
            }

            // Every source and the native root are copied into value matrices before a target local is written.
            Matrix4x4 actualRoot = ModelMatrix(_sharedRoot, _targetTop);
            Decompose(actualRoot, "actual shared root");
            Matrix4x4[] mainModels = modernSnapshot == null ? null : AdaptModernModels(modernSnapshot);
            LocalTrs[] mainLocals = mainModels == null ? null : ToLocals(mainModels, actualRoot, "main");
            LocalTrs[] appearanceLocals = appearanceSnapshot == null ? null :
                ToLocals(appearanceSnapshot, actualRoot, "appearance");

            LocalTrs[] pending = new LocalTrs[ModernPaths.Length];
            for (int i = 0; i < pending.Length; i++)
            {
                if (mainLocals == null) pending[i] = appearanceLocals[i];
                else if (appearanceLocals == null) pending[i] = mainLocals[i];
                else pending[i] = Blend(mainLocals[i], appearanceLocals[i], appearanceWeight);
                Decompose(pending[i].Matrix(), "pending target local");
            }

            Commit(pending, preserveNativeJaw);
        }

        private void ValidateNativeAnimator()
        {
            if (_nativeAnimator == null || !_nativeAnimator.isInitialized ||
                _nativeAnimator.runtimeAnimatorController != _nativeController ||
                _nativeController == null || _nativeController.name != ControllerName ||
                _nativeAnimator.layerCount != 1)
                throw new InvalidOperationException("Real native Animator identity/controller changed.");
        }

        private ClipInput CaptureInput(AnimatorStateInfo state, AnimatorClipInfo[] clips, string role)
        {
            if (clips == null || clips.Length != 1 || clips[0].clip == null)
                throw new InvalidOperationException("Expected exactly one native " + role + " clip.");

            NativeStateContract contract = FindStateContract(state.fullPathHash);
            if (contract == null)
                throw new InvalidOperationException("Native " + role + " state is not certified for the Kraken adapter.");

            AnimationClip clip = clips[0].clip;
            if (clip.name != contract.clipName)
                throw new InvalidOperationException("Native " + role + " state-to-clip mapping changed.");

            AnimationClip expected = _modernSampler.ClipAt(contract.clipIndex);
            if (expected != clip)
                throw new InvalidOperationException("Native " + role + " clip identity changed.");

            if (!IsFinite(state.normalizedTime) || !IsFinite(state.length) || !IsFinite(state.speed) ||
                !IsFinite(state.speedMultiplier) || !IsFinite(clips[0].weight) || !IsFinite(clip.length) ||
                clip.length <= 0f || clips[0].weight < 0f || clips[0].weight > 1f)
                throw new InvalidOperationException("Native " + role + " clock/weight contract changed.");
            if (state.speed != 1f || state.speedMultiplier != 1f || state.length != clip.length ||
                state.loop != contract.loop || clip.isLooping != contract.loop)
                throw new InvalidOperationException("Native " + role + " certified state clock contract changed.");

            double phase = state.normalizedTime;
            double mappedPhase = state.loop ? phase - Math.Floor(phase) : Math.Max(0d, Math.Min(1d, phase));
            float seconds = (float)(mappedPhase * clip.length);
            if (!IsFinite(seconds))
                throw new InvalidOperationException("Native " + role + " sample time is invalid.");

            ClipInput input = new ClipInput();
            input.clipIndex = contract.clipIndex;
            input.clip = clip;
            input.seconds = seconds;
            input.weight = clips[0].weight;
            return input;
        }

        private Matrix4x4[] SnapshotModels(ClipSampler sampler, string[] paths)
        {
            Matrix4x4[] result = new Matrix4x4[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                Transform target = sampler.Resolve(paths[i]);
                if (target == null) throw new InvalidOperationException("Sampler source path disappeared.");
                result[i] = ModelMatrix(target, sampler.Root.transform);
                Decompose(result[i], "sampler model");
            }
            return result;
        }

        private Matrix4x4[] AdaptModernModels(Matrix4x4[] modernSnapshot)
        {
            Matrix4x4[] result = new Matrix4x4[modernSnapshot.Length];
            for (int i = 0; i < result.Length; i++)
            {
                EnsureFinite(modernSnapshot[i], "modern snapshot");
                Matrix4x4 inverseRest = _modernRest[i].inverse;
                EnsureFinite(inverseRest, "modern rest inverse");
                result[i] = modernSnapshot[i] * inverseRest * _oldRest[i];
                Decompose(result[i], "adapted old model");
            }
            return result;
        }

        private static LocalTrs[] ToLocals(Matrix4x4[] models, Matrix4x4 actualRoot, string role)
        {
            LocalTrs[] result = new LocalTrs[models.Length];
            Matrix4x4 parent = actualRoot;
            for (int i = 0; i < models.Length; i++)
            {
                EnsureFinite(parent, role + " parent model");
                Matrix4x4 inverseParent = parent.inverse;
                EnsureFinite(inverseParent, role + " parent inverse");
                Matrix4x4 local = inverseParent * models[i];
                result[i] = Decompose(local, role + " target local");
                parent = models[i];
            }
            return result;
        }

        private void Commit(LocalTrs[] pending, bool preserveNativeJaw)
        {
            ValidateOwnerInvariants();
            LocalTrs[] rollback = new LocalTrs[OldPaths.Length];
            for (int i = 0; i < rollback.Length; i++) rollback[i] = ReadLocal(_targets[i]);
            LocalTrs rootBefore = ReadLocal(_sharedRoot);
            LocalTrs jawBefore = rollback[4];

            try
            {
                // Parent-to-child order makes each readback unambiguous and never writes Root_M.
                for (int i = 0; i < pending.Length; i++) pending[i].Apply(_targets[i]);
                if (!preserveNativeJaw) _jawRest.Apply(_targets[4]);

                ValidateTargetInvariants();
                if (MatrixDifference(rootBefore.Matrix(), ReadLocal(_sharedRoot).Matrix()) > MatrixTolerance)
                    throw new InvalidOperationException("Shared Root_M was modified.");
                for (int i = 0; i < pending.Length; i++)
                    Near(ReadLocal(_targets[i]).Matrix(), pending[i].Matrix(), "target local readback");
                if (preserveNativeJaw)
                    Near(ReadLocal(_targets[4]).Matrix(), jawBefore.Matrix(), "native appearance jaw preservation");
                else
                    Near(ReadLocal(_targets[4]).Matrix(), _jawRest.Matrix(), "main-state jaw rest");
            }
            catch (Exception commitFailure)
            {
                try
                {
                    Restore(rollback);
                }
                catch (Exception restoreFailure)
                {
                    throw new InvalidOperationException("Adapter commit failed and rollback was incomplete: " +
                        commitFailure.Message, restoreFailure);
                }
                throw;
            }
        }

        private void Restore(LocalTrs[] rollback)
        {
            Exception failure = null;
            for (int i = 0; i < rollback.Length; i++)
            {
                try
                {
                    if (_targets[i] == null)
                        throw new InvalidOperationException("Rollback target disappeared at " + OldPaths[i] + ".");
                    rollback[i].Apply(_targets[i]);
                    Near(ReadLocal(_targets[i]).Matrix(), rollback[i].Matrix(), "rollback local readback");
                }
                catch (Exception e)
                {
                    if (failure == null) failure = e;
                }
            }
            if (failure != null) throw new InvalidOperationException("Adapter rollback did not restore every target.", failure);
        }

        private void ValidateTargetInvariants()
        {
            if (_target == null || _target.transform != _targetTop || _sharedRoot == null ||
                ResolveExact(_targetTop, "Root_M") != _sharedRoot)
                throw new InvalidOperationException("Target root identity changed.");

            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null || _targets[i].GetInstanceID() != _targetIds[i] ||
                    ResolveExact(_targetTop, OldPaths[i]) != _targets[i] ||
                    _targets[i].parent != _expectedParents[i])
                    throw new InvalidOperationException("Target palette identity/parent changed.");
            }

            Transform rendererTransform = ResolveExact(_targetTop, RendererPath);
            if (_renderer == null || _renderer.GetInstanceID() != _rendererId ||
                rendererTransform == null || _renderer.transform != rendererTransform ||
                _renderer.sharedMesh != _rendererMesh)
                throw new InvalidOperationException("Target renderer identity changed.");

            Transform[] bones = _renderer.bones;
            Matrix4x4[] binds = _rendererMesh == null ? null : _rendererMesh.bindposes;
            if (bones == null || binds == null || bones.Length != _rendererBones.Length ||
                binds.Length != _bindposes.Length)
                throw new InvalidOperationException("Target renderer palette/bind pose length changed.");

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != _rendererBones[i] || bones[i] != _targets[i])
                    throw new InvalidOperationException("Target renderer palette order changed.");
                if (MatrixDifference(binds[i], _bindposes[i]) != 0f)
                    throw new InvalidOperationException("Target inverse bind pose changed.");
            }
        }

        private void ValidateOwnerInvariants()
        {
            if (_owner == null || _target == null || _resource == null || _enemyRow == null ||
                _owner.m_EventListener != _target || _target.m_Dummy != _owner ||
                _owner.m_EnemyCombat != _enemyRow ||
                !string.Equals(_enemyRow.m_ID, _enemyId, StringComparison.Ordinal) ||
                _enemyRow.m_EnemyAsset != _resource)
                throw new InvalidOperationException("Native owner/CEL/enemy-row/resource identity changed.");
        }

        private void DisableForOwner(Exception e)
        {
            if (_disabled) return;
            _disabled = true;
            DisposeOwned();
            Plugin.Log.LogWarning("[kraken-adapter] disabled for '" + (_enemyId ?? "unknown") + "': " + e.Message);
            enabled = false;
        }

        private void DisposeOwned()
        {
            if (_disposed) return;
            _disposed = true;

            Exception failure = null;
            try
            {
                if (_modernSampler != null) _modernSampler.Dispose();
            }
            catch (Exception e)
            {
                failure = e;
            }
            try
            {
                if (_oldSampler != null) _oldSampler.Dispose();
            }
            catch (Exception e)
            {
                if (failure == null) failure = e;
            }
            finally
            {
                _modernSampler = null;
                _oldSampler = null;
                UnregisterOwner();
            }

            if (failure != null && Plugin.Log != null)
                Plugin.Log.LogWarning("[kraken-adapter] owned sampler cleanup failed: " + failure.Message);
        }

        private void UnregisterOwner()
        {
            if (!_ownerRegistered) return;
            _ownerRegistered = false;
            for (int i = Owners.Count - 1; i >= 0; i--)
            {
                if (!object.ReferenceEquals(Owners[i], this)) continue;
                Owners.RemoveAt(i);
                break;
            }
        }

        private static Transform ResolveExact(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform current = root;
            string[] segments = path.Split('/');
            for (int segment = 0; segment < segments.Length; segment++)
            {
                Transform found = null;
                int matches = 0;
                for (int child = 0; child < current.childCount; child++)
                {
                    Transform candidate = current.GetChild(child);
                    if (candidate.name != segments[segment]) continue;
                    found = candidate;
                    matches++;
                }
                if (matches != 1) return null;
                current = found;
            }
            return current;
        }

        private static Matrix4x4 ModelMatrix(Transform target, Transform top)
        {
            Matrix4x4 result = Matrix4x4.identity;
            int count = 0;
            while (target != top)
            {
                if (target == null || top == null || ++count > 256)
                    throw new InvalidOperationException("Transform is not beneath its exact model root.");
                result = ReadLocal(target).Matrix() * result;
                target = target.parent;
            }
            EnsureFinite(result, "model matrix");
            return result;
        }

        private static LocalTrs ReadLocal(Transform target)
        {
            if (target == null) throw new InvalidOperationException("Required transform disappeared.");
            LocalTrs result = new LocalTrs();
            result.position = target.localPosition;
            result.rotation = target.localRotation;
            result.scale = target.localScale;
            EnsureFinite(result.Matrix(), "transform local");
            return result;
        }

        private static LocalTrs Blend(LocalTrs from, LocalTrs to, float weight)
        {
            if (!IsFinite(weight) || weight < 0f || weight > 1f)
                throw new InvalidOperationException("Blend weight is invalid.");
            LocalTrs result = new LocalTrs();
            result.position = Vector3.Lerp(from.position, to.position, weight);
            result.rotation = Quaternion.Slerp(from.rotation, to.rotation, weight);
            result.scale = Vector3.Lerp(from.scale, to.scale, weight);
            return result;
        }

        private static LocalTrs Decompose(Matrix4x4 value, string label)
        {
            EnsureFinite(value, label);
            Vector3 x = new Vector3(value.m00, value.m10, value.m20);
            Vector3 y = new Vector3(value.m01, value.m11, value.m21);
            Vector3 z = new Vector3(value.m02, value.m12, value.m22);
            Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (!IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z) ||
                scale.x < MinimumScale || scale.y < MinimumScale || scale.z < MinimumScale)
                throw new InvalidOperationException(label + " has a singular scale.");

            float determinant = Vector3.Dot(Vector3.Cross(x, y), z);
            if (!IsFinite(determinant) || determinant <= 0f)
                throw new InvalidOperationException(label + " has a nonpositive determinant.");

            x /= scale.x;
            y /= scale.y;
            z /= scale.z;
            if (Math.Abs(Vector3.Dot(x, y)) > MatrixTolerance ||
                Math.Abs(Vector3.Dot(x, z)) > MatrixTolerance ||
                Math.Abs(Vector3.Dot(y, z)) > MatrixTolerance)
                throw new InvalidOperationException(label + " is sheared.");

            LocalTrs result = new LocalTrs();
            result.position = new Vector3(value.m03, value.m13, value.m23);
            result.scale = scale;
            result.rotation = Quaternion.LookRotation(z, y);
            Near(value, result.Matrix(), label + " TRS reconstruction");
            return result;
        }

        private static void Near(Matrix4x4 actual, Matrix4x4 expected, string label)
        {
            float difference = MatrixDifference(actual, expected);
            if (difference > MatrixTolerance)
                throw new InvalidOperationException(label + " exceeded " + MatrixTolerance + " (error " + difference + ").");
        }

        private static float MatrixDifference(Matrix4x4 left, Matrix4x4 right)
        {
            EnsureFinite(left, "matrix comparison left");
            EnsureFinite(right, "matrix comparison right");
            float maximum = 0f;
            for (int i = 0; i < 16; i++)
            {
                float difference = Math.Abs(left[i] - right[i]);
                if (!IsFinite(difference)) throw new InvalidOperationException("Matrix comparison is nonfinite.");
                if (difference > maximum) maximum = difference;
            }
            return maximum;
        }

        private static void EnsureFinite(Matrix4x4 value, string label)
        {
            for (int i = 0; i < 16; i++)
                if (!IsFinite(value[i])) throw new InvalidOperationException(label + " is nonfinite.");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private struct ClipInput
        {
            internal int clipIndex;
            internal AnimationClip clip;
            internal float seconds;
            internal float weight;
        }

        private sealed class NativeStateContract
        {
            internal readonly int fullPathHash;
            internal readonly string clipName;
            internal readonly int clipIndex;
            internal readonly bool loop;

            internal NativeStateContract(string fullPath, string expectedClipName, bool expectedLoop)
            {
                fullPathHash = Animator.StringToHash(fullPath);
                clipName = expectedClipName;
                clipIndex = Array.IndexOf(ClipNames, expectedClipName);
                if (clipIndex < 0)
                    throw new InvalidOperationException("Certified Kraken state has an unknown clip.");
                loop = expectedLoop;
            }
        }

        private static NativeStateContract FindStateContract(int fullPathHash)
        {
            for (int i = 0; i < StateContracts.Length; i++)
                if (StateContracts[i].fullPathHash == fullPathHash) return StateContracts[i];
            return null;
        }

        private struct LocalTrs
        {
            internal Vector3 position;
            internal Quaternion rotation;
            internal Vector3 scale;

            internal Matrix4x4 Matrix()
            {
                return Matrix4x4.TRS(position, rotation, scale);
            }

            internal void Apply(Transform target)
            {
                target.localPosition = position;
                target.localRotation = rotation;
                target.localScale = scale;
            }
        }

        /// <summary>
        /// Fixed two-bank sampler. It contains no native controller, no CharacterEventListener, and no
        /// StateMachineBehaviour. Its manual graph only evaluates animation clips at observed native clocks.
        /// </summary>
        private sealed class ClipSampler
        {
            private readonly string _label;
            private readonly Dictionary<string, AnimationClip> _clips;
            private readonly List<SampleNode> _nodes = new List<SampleNode>();
            private readonly AnimationClipPlayable[] _inputs = new AnimationClipPlayable[ClipCount * BankCount];
            private GameObject _root;
            private Animator _animator;
            private PlayableGraph _graph;
            private AnimationMixerPlayable _mixer;
            private bool _disposed;

            internal GameObject Root { get { return _root; } }

            internal ClipSampler(string label, Transform source, Avatar avatar, Dictionary<string, AnimationClip> clips)
            {
                if (source == null || avatar == null || !avatar.isValid || avatar.isHuman)
                    throw new InvalidOperationException("Required " + label + " generic Avatar is unavailable.");
                _label = label;
                _clips = clips;
                try
                {
                    Create(source, avatar);
                }
                catch (Exception createFailure)
                {
                    try
                    {
                        Dispose();
                    }
                    catch (Exception cleanupFailure)
                    {
                        throw new InvalidOperationException("Owned sampler construction failed and cleanup was incomplete: " +
                            createFailure.Message, cleanupFailure);
                    }
                    throw;
                }
            }

            internal AnimationClip ClipAt(int index)
            {
                if (index < 0 || index >= ClipNames.Length) throw new InvalidOperationException("Invalid clip index.");
                AnimationClip clip;
                if (!_clips.TryGetValue(ClipNames[index], out clip) || clip == null)
                    throw new InvalidOperationException("Owned sampler clip disappeared.");
                return clip;
            }

            internal Transform Resolve(string path)
            {
                return ResolveExact(_root == null ? null : _root.transform, path);
            }

            internal void Evaluate(ClipInput current, ClipInput? next)
            {
                ResetToRest();
                ClearInputs();
                SetInput(0, current);
                if (next.HasValue) SetInput(ClipCount, next.Value);
                EvaluateGraph();
            }

            internal void EvaluateSingle(ClipInput input)
            {
                ResetToRest();
                ClearInputs();
                input.weight = 1f;
                SetInput(0, input);
                EvaluateGraph();
            }

            internal void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    if (_graph.IsValid()) _graph.Destroy();
                }
                finally
                {
                    if (_root != null) UnityEngine.Object.Destroy(_root);
                    _root = null;
                    _animator = null;
                }
            }

            private void Create(Transform source, Avatar avatar)
            {
                _root = new GameObject("FTK_KRAKEN_" + _label.ToUpperInvariant() + "_SAMPLER");
                _root.SetActive(false);
                CopyTree(source, _root.transform, _nodes);
                _animator = _root.AddComponent<Animator>();
                _animator.avatar = avatar;
                _animator.runtimeAnimatorController = null;
                _animator.fireEvents = false;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                _graph = PlayableGraph.Create();
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _mixer = AnimationMixerPlayable.Create(_graph, _inputs.Length, false);
                for (int i = 0; i < _inputs.Length; i++)
                {
                    AnimationClip inputClip = ClipAt(i % ClipCount);
                    _inputs[i] = AnimationClipPlayable.Create(_graph, inputClip);
                    _inputs[i].SetSpeed(0d);
                    _inputs[i].SetApplyFootIK(false);
                    if (!_graph.Connect(_inputs[i], 0, _mixer, i))
                        throw new InvalidOperationException("Owned sampler input connection failed.");
                    _inputs[i].SetTime(0d);
                    _mixer.SetInputWeight(i, 0f);
                }

                AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "kraken-" + _label + "-sampler", _animator);
                output.SetSourcePlayable(_mixer);
                output.SetWeight(1f);

                _root.SetActive(true);
                _animator.Rebind();
                _graph.Play();
                _graph.Evaluate(0f);
                Audit();
            }

            private void ResetToRest()
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    SampleNode node = _nodes[i];
                    if (node.transform == null || node.transform.GetInstanceID() != node.id ||
                        node.transform.parent != node.parent)
                        throw new InvalidOperationException("Owned sampler hierarchy changed.");
                    node.rest.Apply(node.transform);
                }
            }

            private void ClearInputs()
            {
                for (int i = 0; i < _inputs.Length; i++)
                {
                    if (!_inputs[i].IsValid()) throw new InvalidOperationException("Owned sampler input disappeared.");
                    _inputs[i].SetTime(0d);
                    _mixer.SetInputWeight(i, 0f);
                }
            }

            private void SetInput(int bankStart, ClipInput input)
            {
                if (input.clipIndex < 0 || input.clipIndex >= ClipCount || input.clip != ClipAt(input.clipIndex) ||
                    !IsFinite(input.seconds) || !IsFinite(input.weight) || input.weight < 0f || input.weight > 1f)
                    throw new InvalidOperationException("Invalid owned sampler input.");
                int slot = bankStart + input.clipIndex;
                _inputs[slot].SetTime(input.seconds);
                _mixer.SetInputWeight(slot, input.weight);
            }

            private void EvaluateGraph()
            {
                Audit();
                _graph.Evaluate(0f);
                Audit();
            }

            private void Audit()
            {
                if (_root == null || !_root.activeInHierarchy || _animator == null || !_animator.enabled ||
                    !_animator.isInitialized || _animator.runtimeAnimatorController != null ||
                    _animator.fireEvents || _animator.applyRootMotion ||
                    _animator.cullingMode != AnimatorCullingMode.AlwaysAnimate ||
                    !_graph.IsValid() || _graph.GetTimeUpdateMode() != DirectorUpdateMode.Manual ||
                    !_mixer.IsValid() || _mixer.GetInputCount() != _inputs.Length ||
                    _graph.GetPlayableCount() != _inputs.Length + 1 || _graph.GetOutputCount() != 1)
                    throw new InvalidOperationException("Owned controller-free sampler contract changed.");

                for (int i = 0; i < _inputs.Length; i++)
                {
                    if (!_inputs[i].IsValid() || _inputs[i].GetAnimationClip() != ClipAt(i % ClipCount) ||
                        _inputs[i].GetSpeed() != 0d || _inputs[i].GetApplyFootIK() ||
                        !_mixer.GetInput(i).GetHandle().Equals(_inputs[i].GetHandle()))
                        throw new InvalidOperationException("Owned sampler input topology changed.");
                }
            }

            private static void CopyTree(Transform source, Transform target, List<SampleNode> nodes)
            {
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;

                SampleNode node = new SampleNode();
                node.transform = target;
                node.id = target.GetInstanceID();
                node.parent = target.parent;
                node.rest = ReadLocal(target);
                nodes.Add(node);

                for (int i = 0; i < source.childCount; i++)
                {
                    Transform child = source.GetChild(i);
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(target, false);
                    CopyTree(child, copy.transform, nodes);
                }
            }

            private sealed class SampleNode
            {
                internal Transform transform;
                internal int id;
                internal Transform parent;
                internal LocalTrs rest;
            }
        }
    }
}
