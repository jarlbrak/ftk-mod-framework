using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;
using StartGameFE;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Owns the mode boundary and resume preflight for the optional single-player party expansion.
    /// Five slots stay unavailable until setup UI and in-run support have both completed their preflight.
    /// </summary>
    internal static class SinglePlayerPartyExpansion
    {
        private sealed class PortraitActionPointSnapshot
        {
            internal uiPortraitHolder Holder;
            internal List<uiPortraitActionPoint> Original;
            internal List<uiPortraitActionPoint> Expanded;
            internal readonly List<uiPortraitActionPoint> Created = new List<uiPortraitActionPoint>();
        }

        private sealed class AttackDistanceSnapshot
        {
            internal DummyAttackSlide Slide;
            internal float[] Original;
            internal float[] Expanded;
        }

        private sealed class GoldMenuSnapshot
        {
            internal uiGoldMenu Menu;
            internal List<uiGoldMenuEntry> Original;
            internal List<uiGoldMenuEntry> OriginalEntries;
            internal List<uiGoldMenuEntry> Expanded;
            internal readonly List<uiGoldMenuEntry> Created = new List<uiGoldMenuEntry>();
        }

        private sealed class PopupMenuSnapshot
        {
            internal uiPopupMenu Menu;
            internal uiPopupMenu.PopupButton GivePopup;
            internal int OriginalPopupCount;
            internal int ExpandedPopupCount;
            internal List<uiPopupMenuButton> ExpandedButtons;
            internal readonly List<uiPopupMenuButton> Created = new List<uiPopupMenuButton>();
        }

        internal const int VanillaPlayerCapacity = 3;
        internal const int ExpandedPlayerCapacity = 5;

        private static bool _setupUiReady;
        private static bool _runtimeReady;
        private static bool _hudReady;
        private static bool _combatReady;
        private static bool _ownsExpandedCapacity;
        private static bool _warnedSupportUnavailable;
        private static bool _setupUiPrepared;
        private static uiCharacterCreateRoot _setupRoot;
        private static SelectScreenCamera _setupCamera;
        private static Transform[] _originalCreateTargetArray;
        private static Transform[] _originalCameraTargetArray;
        private static Transform[] _expandedCreateTargetArray;
        private static Transform[] _expandedCameraTargetArray;
        private static uiQuickPlayerCreate[] _originalQuickCreatePool;
        private static uiQuickPlayerCreate[] _expandedQuickCreatePool;
        private static int[] _originalDefaultClasses;
        private static int[] _expandedDefaultClasses;
        private static Vector2[] _nativeSetupAnchors;
        private static Vector3[] _nativeCameraTargets;
        private static readonly List<Transform> _ownedSetupTargetClones = new List<Transform>();
        private static uiItemMenu _giveButtonMenu;
        private static uiItemMenuButton[] _originalGiveButtons;
        private static uiItemMenuButton[] _expandedGiveButtons;
        private static readonly List<uiItemMenuButton> _ownedGiveButtonClones = new List<uiItemMenuButton>();
        private static uiHudScroller _hudScroller;
        private static float[] _originalHudPositions;
        private static float[] _expandedHudPositions;
        private static readonly List<PortraitActionPointSnapshot> _portraitActionPointSnapshots =
            new List<PortraitActionPointSnapshot>();
        private static readonly List<AttackDistanceSnapshot> _attackDistanceSnapshots =
            new List<AttackDistanceSnapshot>();
        private static readonly List<GoldMenuSnapshot> _goldMenuSnapshots = new List<GoldMenuSnapshot>();
        private static readonly List<PopupMenuSnapshot> _popupMenuSnapshots = new List<PopupMenuSnapshot>();
        private static FTKHub _expandedHub;
        private static GameObject[] _originalDummies;
        private static GameObject[] _expandedDummies;
        private static readonly List<GameObject> _ownedDummyClones = new List<GameObject>();
        private static readonly List<int> _ownedViewIds = new List<int>();
        private static readonly FieldInfo QuickCreatePool = AccessTools.Field(typeof(uiQuickPlayerCreate), "guiQuickPlayerCreates");
        private static readonly FieldInfo GiveButtons = AccessTools.Field(typeof(uiItemMenu), "m_GiveButtons");
        private static readonly FieldInfo GameConfigModes = AccessTools.Field(typeof(GameConfig), "m_GameModes");
        private static readonly FieldInfo GoldMenuCurrentEntries = AccessTools.Field(typeof(uiGoldMenu), "m_CurrentGoldEntries");
        private static readonly FieldInfo GoldMenuStats = AccessTools.Field(typeof(uiGoldMenu), "m_Stats");
        private static readonly FieldInfo GoldMenuCurrentGold = AccessTools.Field(typeof(uiGoldMenu), "m_CurrentGold");
        private static readonly MethodInfo PopupMenuOnClick = AccessTools.Method(typeof(uiPopupMenu), "OnClick",
            new[] { typeof(uiPopupMenu.Action), typeof(int) });
        private static readonly HashSet<uiGoldMenu> _settingExpandedGold = new HashSet<uiGoldMenu>();
        private static readonly HashSet<uiGoldMenu> _initializingGoldMenus = new HashSet<uiGoldMenu>();

        internal static bool SupportsFiveHeroSetup
        {
            get { return _setupUiReady && _runtimeReady && _hudReady && _combatReady; }
        }

        private static bool OwnsExpandedCapacity
        {
            get { return _ownsExpandedCapacity && GameFlowMC.gMaxPlayers == ExpandedPlayerCapacity; }
        }

        // The setup and runtime transactions publish readiness only after all references are valid.
        // Until both transactions exist and pass, this feature remains inert.
        internal static void SetSetupUiReady(bool ready)
        {
            _setupUiReady = ready;
        }

        internal static void SetRuntimeReady(bool ready)
        {
            _runtimeReady = ready;
        }

        internal static void ApplyModeCapacity(GameLogic.GameMode mode)
        {
            bool requested = Plugin.EnableFiveHeroSinglePlayer != null &&
                Plugin.EnableFiveHeroSinglePlayer.Value;
            bool shouldExpand = requested && IsOfflineSinglePlayer(mode) && SupportsFiveHeroSetup;

            if (shouldExpand)
            {
                if (GameFlowMC.gMaxPlayers == VanillaPlayerCapacity)
                {
                    GameFlowMC.gMaxPlayers = ExpandedPlayerCapacity;
                    _ownsExpandedCapacity = true;
                    if (!ApplySetupLayout(ExpandedPlayerCapacity) || !SupportsFiveHeroSetup)
                    {
                        RestoreVanillaCapacity();
                        RestorePresentation();
                    }
                }
                else if (GameFlowMC.gMaxPlayers == ExpandedPlayerCapacity && _ownsExpandedCapacity)
                {
                    if (!ApplySetupLayout(ExpandedPlayerCapacity) || !SupportsFiveHeroSetup)
                    {
                        RestoreVanillaCapacity();
                        RestorePresentation();
                    }
                    return;
                }
                else
                {
                    Plugin.Log.LogWarning("[five-hero] player capacity was changed by another runtime; keeping the current value.");
                    return;
                }
                return;
            }

            RestoreVanillaCapacity();
            RestorePresentation();
            if (requested && mode == GameLogic.GameMode.SinglePlayer && !SupportsFiveHeroSetup &&
                !_warnedSupportUnavailable)
            {
                Plugin.Log.LogWarning("[five-hero] setup or runtime support is unavailable; keeping the native three-hero limit.");
                _warnedSupportUnavailable = true;
            }
            else if (!requested || mode != GameLogic.GameMode.SinglePlayer)
            {
                _warnedSupportUnavailable = false;
            }
        }

        private static void RestoreVanillaCapacity()
        {
            if (_ownsExpandedCapacity && GameFlowMC.gMaxPlayers == ExpandedPlayerCapacity)
                GameFlowMC.gMaxPlayers = VanillaPlayerCapacity;
            _ownsExpandedCapacity = false;
        }

        private static bool IsOfflineSinglePlayer(GameLogic.GameMode mode)
        {
            if (mode != GameLogic.GameMode.SinglePlayer)
                return false;
            uiStartGame title = uiStartGame.Instance;
            if (title == null)
                return PhotonNetwork.offlineMode;
            if (title.m_UseOnlineSinglePlayer)
                return false;
            // Photon reports offlineMode as a connected state, so the title helper alone would reject offline runs.
            return PhotonNetwork.offlineMode || !title.IsPhotonServerConnected;
        }

        private static bool TryGetSelectedGameMode(GameConfig config, out GameLogic.GameMode mode)
        {
            mode = GameLogic.GameMode.Multiplayer;
            if (config == null || config.m_GameType == null || GameConfigModes == null)
                return false;
            GameLogic.GameMode[] modes = GameConfigModes.GetValue(config) as GameLogic.GameMode[];
            int selected = config.m_GameType.value;
            if (modes == null || selected < 0 || selected >= modes.Length)
                return false;
            mode = modes[selected];
            return true;
        }

        private static bool PrepareStartBoundary(GameConfig config)
        {
            uiStartGame title = uiStartGame.Instance;
            bool featureRequested = Plugin.EnableFiveHeroSinglePlayer != null &&
                Plugin.EnableFiveHeroSinglePlayer.Value;
            bool featurePreviouslyOwned = _ownsExpandedCapacity;
            bool managesExpandedCount = featureRequested || featurePreviouslyOwned;
            GameLogic.GameMode mode;
            if (!TryGetSelectedGameMode(config, out mode))
            {
                Plugin.Log.LogWarning("[five-hero] selected game mode could not be read; restoring the native capacity before launch.");
                RestoreVanillaCapacity();
                bool presentationRestored = RestorePresentation();
                bool dummiesRestored = RestoreRuntimeDummies(true);
                if (managesExpandedCount && title != null && title.m_ActualMaxCharCount > VanillaPlayerCapacity)
                {
                    if (title.m_IsResuming)
                    {
                        ShowRuntimeError("The selected game mode could not be checked. The saved run was not loaded.");
                        return false;
                    }
                    title.m_ActualMaxCharCount = VanillaPlayerCapacity;
                }
                if (!presentationRestored || !dummiesRestored)
                {
                    ShowRuntimeError("Expanded single-player state could not be restored. Game setup was stopped safely.");
                    return false;
                }
                if (managesExpandedCount && !OwnsExpandedCapacity &&
                    GameFlowMC.gMaxPlayers != VanillaPlayerCapacity)
                {
                    ShowRuntimeError("Another runtime changed the player capacity. Game setup was stopped safely.");
                    return false;
                }
                return true;
            }

            PrepareForSelectedMode(mode);
            ApplyModeCapacity(mode);
            bool eligibleForExpansion = Plugin.EnableFiveHeroSinglePlayer != null &&
                Plugin.EnableFiveHeroSinglePlayer.Value && IsOfflineSinglePlayer(mode);
            if (eligibleForExpansion && OwnsExpandedCapacity)
                return true;

            bool restoredPresentation = RestorePresentation();
            bool restoredDummies = RestoreRuntimeDummies(true);
            if (!restoredPresentation || !restoredDummies)
            {
                ShowRuntimeError("Expanded single-player state could not be restored. Game setup was stopped safely.");
                return false;
            }

            if (managesExpandedCount && !OwnsExpandedCapacity &&
                GameFlowMC.gMaxPlayers != VanillaPlayerCapacity)
            {
                ShowRuntimeError("Another runtime changed the player capacity. Game setup was stopped safely.");
                return false;
            }
            if (managesExpandedCount && title != null && title.m_ActualMaxCharCount > VanillaPlayerCapacity)
            {
                if (title.m_IsResuming)
                {
                    ShowRuntimeError("Five-hero support is unavailable for this game mode. The saved run was not loaded.");
                    return false;
                }
                title.m_ActualMaxCharCount = VanillaPlayerCapacity;
                ShowRuntimeError("Five-hero support is unavailable for this game mode. This new run will use three heroes.");
            }
            return true;
        }

        private static void PrepareForSelectedMode(GameLogic.GameMode mode)
        {
            try
            {
                bool requested = Plugin.EnableFiveHeroSinglePlayer != null &&
                    Plugin.EnableFiveHeroSinglePlayer.Value;
                if (!requested || !IsOfflineSinglePlayer(mode))
                {
                    SetSetupUiReady(false);
                    SetRuntimeReady(false);
                    _hudReady = false;
                    _combatReady = false;
                    RestorePresentation();
                    RestoreRuntimeDummies(true);
                    return;
                }

                bool dummiesReady = CanPrepareRuntimeDummies();
                bool inputReady = CanPrepareExpandedSetupInput();
                bool runtimeReady = dummiesReady && inputReady;
                bool combatReady = runtimeReady && CanPrepareCombatLayouts();
                bool hudReady = combatReady && TryPrepareHudSupport();
                uiStartGame title = uiStartGame.Instance;
                bool createRootReady = title != null && title.m_CreateCharacterRoot != null;
                bool uiReady = hudReady && createRootReady && TryPrepareSetupUi(title.m_CreateCharacterRoot);
                SetSetupUiReady(uiReady);
                SetRuntimeReady(runtimeReady);
                _hudReady = hudReady;
                _combatReady = combatReady;
                if (!SupportsFiveHeroSetup)
                    Plugin.Log.LogInfo("[five-hero] preflight readiness: dummies=" + dummiesReady +
                        " input=" + inputReady + " combat=" + combatReady + " hud=" + hudReady +
                        " createRoot=" + createRootReady + " camera=" + (SelectScreenCamera.Instance != null));
            }
            catch (Exception error)
            {
                SetSetupUiReady(false);
                SetRuntimeReady(false);
                _hudReady = false;
                _combatReady = false;
                RestoreVanillaCapacity();
                try
                {
                    RestorePresentation();
                    RestoreRuntimeDummies(true);
                }
                catch (Exception cleanupError)
                {
                    Plugin.Log.LogWarning("[five-hero] mode preflight cleanup failed: " + cleanupError.Message);
                }
                Plugin.Log.LogWarning("[five-hero] mode preflight failed: " + error.Message);
            }
        }

        // The opt-in Agent runner bypasses GameConfig.OnStartGame while it drives the
        // asynchronous offline-room flow. Keep that test path on the same preflight and
        // capacity transaction as the normal title-screen route before it selects slots.
        internal static int PrepareForAgentSinglePlayerRun()
        {
            PrepareForSelectedMode(GameLogic.GameMode.SinglePlayer);
            ApplyModeCapacity(GameLogic.GameMode.SinglePlayer);
            return OwnsExpandedCapacity
                ? ExpandedPlayerCapacity
                : Math.Min(VanillaPlayerCapacity, GameFlowMC.gMaxPlayers);
        }

        private static bool TryPrepareSetupUi(uiCharacterCreateRoot root)
        {
            SelectScreenCamera camera = SelectScreenCamera.Instance;
            if (_setupUiPrepared && (root != _setupRoot || camera != _setupCamera) && !RestoreSetupUi())
                return false;
            if (root == null || camera == null || root.m_CreateUITargets == null ||
                camera.m_PlayerTargets == null || root.m_CreateUITargets.Length < VanillaPlayerCapacity ||
                camera.m_PlayerTargets.Length < VanillaPlayerCapacity || QuickCreatePool == null)
                return false;

            bool stillPrepared = _setupUiPrepared && root == _setupRoot && camera == _setupCamera &&
                ReferenceEquals(root.m_CreateUITargets, _expandedCreateTargetArray) &&
                ReferenceEquals(camera.m_PlayerTargets, _expandedCameraTargetArray) &&
                ReferenceEquals(QuickCreatePool.GetValue(null), _expandedQuickCreatePool) &&
                ReferenceEquals(uiQuickPlayerCreate.Default_Classes, _expandedDefaultClasses);
            if (_setupUiPrepared && !stillPrepared && !RestoreSetupUi())
                return false;
            if (stillPrepared)
                return true;

            Transform[] originalCreateTargets = root.m_CreateUITargets;
            Transform[] originalCameraTargets = camera.m_PlayerTargets;
            uiQuickPlayerCreate[] originalPool = QuickCreatePool.GetValue(null) as uiQuickPlayerCreate[];
            int[] originalDefaults = uiQuickPlayerCreate.Default_Classes;
            if (originalPool == null || originalPool.Length < VanillaPlayerCapacity ||
                originalDefaults == null || originalDefaults.Length < VanillaPlayerCapacity)
                return false;
            Transform[] createTargets = root.m_CreateUITargets;
            Transform[] cameraTargets = camera.m_PlayerTargets;
            int[] defaults = null;
            uiQuickPlayerCreate[] pool = null;
            List<Transform> created = new List<Transform>();
            try
            {
                for (int i = 0; i < VanillaPlayerCapacity; i++)
                    if (createTargets[i] == null || cameraTargets[i] == null)
                        return false;

                if (createTargets.Length < ExpandedPlayerCapacity)
                    createTargets = ExtendTargets(createTargets, created, "FiveHeroCreateTarget");
                if (cameraTargets.Length < ExpandedPlayerCapacity)
                    cameraTargets = ExtendTargets(cameraTargets, created, "FiveHeroCameraTarget");

                for (int i = 0; i < ExpandedPlayerCapacity; i++)
                    if (createTargets[i] == null || cameraTargets[i] == null ||
                        createTargets[i].GetComponent<RectTransform>() == null)
                        throw new InvalidOperationException("A required five-hero setup or camera target is invalid.");

                defaults = originalDefaults;
                if (defaults.Length < ExpandedPlayerCapacity)
                {
                    int[] expandedDefaults = new int[ExpandedPlayerCapacity];
                    Array.Copy(defaults, expandedDefaults, defaults.Length);
                    for (int i = defaults.Length; i < expandedDefaults.Length; i++)
                        expandedDefaults[i] = i % VanillaPlayerCapacity;
                    defaults = expandedDefaults;
                }

                pool = originalPool;
                if (pool.Length < ExpandedPlayerCapacity)
                {
                    uiQuickPlayerCreate[] expandedPool = new uiQuickPlayerCreate[ExpandedPlayerCapacity];
                    Array.Copy(pool, expandedPool, pool.Length);
                    pool = expandedPool;
                }

                CaptureNativeSetupLayout(root, camera);
                QuickCreatePool.SetValue(null, pool);
                uiQuickPlayerCreate.Default_Classes = defaults;
                root.m_CreateUITargets = createTargets;
                camera.m_PlayerTargets = cameraTargets;
                _originalCreateTargetArray = originalCreateTargets;
                _originalCameraTargetArray = originalCameraTargets;
                _expandedCreateTargetArray = createTargets;
                _expandedCameraTargetArray = cameraTargets;
                _originalQuickCreatePool = originalPool;
                _expandedQuickCreatePool = pool;
                _originalDefaultClasses = originalDefaults;
                _expandedDefaultClasses = defaults;
                _ownedSetupTargetClones.AddRange(created);
                _setupUiPrepared = true;
                _setupRoot = root;
                _setupCamera = camera;
                return true;
            }
            catch (Exception error)
            {
                if (ReferenceEquals(root.m_CreateUITargets, createTargets))
                    root.m_CreateUITargets = originalCreateTargets;
                if (ReferenceEquals(camera.m_PlayerTargets, cameraTargets))
                    camera.m_PlayerTargets = originalCameraTargets;
                if (ReferenceEquals(QuickCreatePool.GetValue(null), pool))
                    QuickCreatePool.SetValue(null, originalPool);
                if (ReferenceEquals(uiQuickPlayerCreate.Default_Classes, defaults))
                    uiQuickPlayerCreate.Default_Classes = originalDefaults;
                for (int i = 0; i < created.Count; i++)
                    if (created[i] != null) UnityEngine.Object.Destroy(created[i].gameObject);
                _setupRoot = null;
                _setupCamera = null;
                _nativeSetupAnchors = null;
                _nativeCameraTargets = null;
                _ownedSetupTargetClones.Clear();
                Plugin.Log.LogWarning("[five-hero] setup target preflight failed: " + error.Message);
                return false;
            }
        }

        private static Transform[] ExtendTargets(Transform[] source, List<Transform> created, string namePrefix)
        {
            Transform[] expanded = new Transform[ExpandedPlayerCapacity];
            Array.Copy(source, expanded, source.Length);
            int sourceIndex = Math.Min(source.Length, VanillaPlayerCapacity) - 1;
            for (int i = source.Length; i < ExpandedPlayerCapacity; i++)
            {
                Transform original = expanded[sourceIndex];
                if (original == null || original.parent == null)
                    throw new InvalidOperationException("A native setup target has no parent.");
                Transform clone = UnityEngine.Object.Instantiate(original);
                created.Add(clone);
                clone.name = namePrefix + (i + 1);
                clone.SetParent(original.parent, false);
                clone.localPosition = original.localPosition;
                clone.localRotation = original.localRotation;
                clone.localScale = original.localScale;
                expanded[i] = clone;
            }
            for (int i = 0; i < ExpandedPlayerCapacity; i++)
                if (expanded[i] == null)
                    throw new InvalidOperationException("A five-hero setup target is missing.");
            return expanded;
        }

        private static void CaptureNativeSetupLayout(uiCharacterCreateRoot root, SelectScreenCamera camera)
        {
            if (_setupRoot == root && _setupCamera == camera && _nativeSetupAnchors != null &&
                _nativeCameraTargets != null)
                return;
            int anchorCount = Math.Min(ExpandedPlayerCapacity, root.m_CreateUITargets.Length);
            int cameraTargetCount = Math.Min(ExpandedPlayerCapacity, camera.m_PlayerTargets.Length);
            Vector2[] anchors = new Vector2[anchorCount];
            Vector3[] cameraTargets = new Vector3[cameraTargetCount];
            for (int i = 0; i < anchorCount; i++)
            {
                RectTransform rect = root.m_CreateUITargets[i].GetComponent<RectTransform>();
                if (rect == null)
                    throw new InvalidOperationException("A native setup target has no RectTransform.");
                anchors[i] = rect.anchoredPosition;
            }
            for (int i = 0; i < cameraTargetCount; i++)
            {
                cameraTargets[i] = camera.m_PlayerTargets[i].position;
            }
            _setupRoot = root;
            _setupCamera = camera;
            _nativeSetupAnchors = anchors;
            _nativeCameraTargets = cameraTargets;
        }

        private static bool RestoreSetupUi()
        {
            if (!_setupUiPrepared)
                return true;

            try
            {
                bool ownsCreateTargets = _setupRoot != null &&
                    ReferenceEquals(_setupRoot.m_CreateUITargets, _expandedCreateTargetArray);
                bool ownsCameraTargets = _setupCamera != null &&
                    ReferenceEquals(_setupCamera.m_PlayerTargets, _expandedCameraTargetArray);
                bool ownsQuickCreatePool = QuickCreatePool != null &&
                    ReferenceEquals(QuickCreatePool.GetValue(null), _expandedQuickCreatePool);
                bool ownsDefaultClasses = ReferenceEquals(uiQuickPlayerCreate.Default_Classes, _expandedDefaultClasses);

                for (int i = 0; _nativeSetupAnchors != null && i < _nativeSetupAnchors.Length &&
                    _expandedCreateTargetArray != null && i < _expandedCreateTargetArray.Length; i++)
                {
                    if (ownsCreateTargets && _expandedCreateTargetArray[i] != null)
                    {
                        RectTransform rect = _expandedCreateTargetArray[i].GetComponent<RectTransform>();
                        if (rect != null)
                            rect.anchoredPosition = _nativeSetupAnchors[i];
                    }
                }
                for (int i = 0; ownsCameraTargets && _nativeCameraTargets != null &&
                    i < _nativeCameraTargets.Length && _expandedCameraTargetArray != null &&
                    i < _expandedCameraTargetArray.Length; i++)
                    if (_expandedCameraTargetArray[i] != null)
                        _expandedCameraTargetArray[i].position = _nativeCameraTargets[i];
                if (ownsCreateTargets)
                    _setupRoot.m_CreateUITargets = _originalCreateTargetArray;
                if (ownsCameraTargets)
                    _setupCamera.m_PlayerTargets = _originalCameraTargetArray;
                if (ownsQuickCreatePool)
                    QuickCreatePool.SetValue(null, _originalQuickCreatePool);
                if (ownsDefaultClasses)
                    uiQuickPlayerCreate.Default_Classes = _originalDefaultClasses;

                for (int i = 0; i < _ownedSetupTargetClones.Count; i++)
                {
                    Transform clone = _ownedSetupTargetClones[i];
                    if (clone == null)
                        continue;
                    bool referenced = ContainsTransform(_setupRoot == null ? null : _setupRoot.m_CreateUITargets, clone) ||
                        ContainsTransform(_setupCamera == null ? null : _setupCamera.m_PlayerTargets, clone);
                    if (!referenced)
                        UnityEngine.Object.Destroy(clone.gameObject);
                }

                _setupUiPrepared = false;
                _setupRoot = null;
                _setupCamera = null;
                _originalCreateTargetArray = null;
                _originalCameraTargetArray = null;
                _expandedCreateTargetArray = null;
                _expandedCameraTargetArray = null;
                _originalQuickCreatePool = null;
                _expandedQuickCreatePool = null;
                _originalDefaultClasses = null;
                _expandedDefaultClasses = null;
                _nativeSetupAnchors = null;
                _nativeCameraTargets = null;
                _ownedSetupTargetClones.Clear();
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] setup rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool ContainsTransform(Transform[] targets, Transform value)
        {
            if (targets == null)
                return false;
            for (int i = 0; i < targets.Length; i++)
                if (ReferenceEquals(targets[i], value))
                    return true;
            return false;
        }

        private static bool ApplySetupLayout(int partyCapacity)
        {
            try
            {
                uiStartGame title = uiStartGame.Instance;
                uiCharacterCreateRoot root = _setupRoot != null ? _setupRoot :
                    (title == null ? null : title.m_CreateCharacterRoot);
                SelectScreenCamera camera = _setupCamera != null ? _setupCamera : SelectScreenCamera.Instance;
                if (root == null || camera == null || root.m_CreateUITargets == null ||
                    camera.m_PlayerTargets == null || _nativeSetupAnchors == null || _nativeCameraTargets == null)
                {
                    SetSetupUiReady(false);
                    return false;
                }

                int count = Mathf.Clamp(partyCapacity, 1, ExpandedPlayerCapacity);
                if (count <= VanillaPlayerCapacity)
                {
                    for (int i = 0; i < count; i++)
                    {
                        RectTransform rect = root.m_CreateUITargets[i].GetComponent<RectTransform>();
                        if (rect != null) rect.anchoredPosition = _nativeSetupAnchors[i];
                        camera.m_PlayerTargets[i].position = _nativeCameraTargets[i];
                    }
                    return true;
                }

                for (int i = 0; i < count; i++)
                {
                    RectTransform rect = root.m_CreateUITargets[i].GetComponent<RectTransform>();
                    if (rect == null)
                    {
                        SetSetupUiReady(false);
                        return false;
                    }
                    float fraction = (float)i / (float)(count - 1);
                    rect.anchoredPosition = Vector2.Lerp(_nativeSetupAnchors[0], _nativeSetupAnchors[2], fraction);
                    camera.m_PlayerTargets[i].position = Vector3.Lerp(_nativeCameraTargets[0],
                        _nativeCameraTargets[2], fraction);
                }
                return true;
            }
            catch (Exception error)
            {
                SetSetupUiReady(false);
                Plugin.Log.LogWarning("[five-hero] setup layout failed: " + error.Message);
                return false;
            }
        }

        private static bool CanPrepareRuntimeDummies()
        {
            FTKHub hub = FTKHub.Instance;
            if (hub == null || hub.m_Dummies == null || hub.m_Dummies.Length != 6)
                return false;
            for (int i = 0; i < VanillaPlayerCapacity; i++)
            {
                GameObject playerDummy = hub.m_Dummies[i];
                if (playerDummy == null || playerDummy.GetComponent<PhotonView>() == null ||
                    playerDummy.GetComponent<CharacterDummy>() == null)
                    return false;
            }
            for (int i = VanillaPlayerCapacity; i < 6; i++)
                if (hub.m_Dummies[i] == null || hub.m_Dummies[i].GetComponent<EnemyDummy>() == null)
                    return false;
            return true;
        }

        private static bool CanPrepareExpandedSetupInput()
        {
            try
            {
                return ReInput.players != null &&
                    ReInput.players.GetPlayer(VanillaPlayerCapacity - 1) != null;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] setup input preflight failed: " + error.Message);
                return false;
            }
        }

        private static bool TryPrepareHudSupport()
        {
            try
            {
                // These HUD objects are created lazily after the offline room starts. Validate
                // the members used by their guarded Harmony hooks now, and expand any instances
                // that already exist. The hooks prepare later instances before their UI is used.
                FieldInfo hudPositionsField = AccessTools.Field(typeof(uiHudScroller), "m_Positions");
                FieldInfo portraitActionPointsField = AccessTools.Field(typeof(uiPortraitHolder), "m_PortraitActionPoints");
                if (GiveButtons == null || hudPositionsField == null || portraitActionPointsField == null ||
                    GoldMenuCurrentEntries == null || GoldMenuStats == null || GoldMenuCurrentGold == null ||
                    PopupMenuOnClick == null)
                    return false;

                uiItemMenu itemMenu = uiItemMenu.Instance;
                if (itemMenu != null && !EnsureGiveButtonCapacity(itemMenu))
                {
                    RestoreGiveButtonCapacity();
                    return false;
                }

                uiHudScroller scroller = uiHudScroller.Instance;
                if (scroller != null)
                {
                    float[] positions = hudPositionsField.GetValue(scroller) as float[];
                    if (!EnsureHudPositionCapacity(scroller, ref positions))
                    {
                        RestoreHudPositionCapacity();
                        RestoreGiveButtonCapacity();
                        return false;
                    }
                }

                uiPortraitHolderManager manager = uiPortraitHolderManager.Instance;
                if (manager != null && (manager.m_Prefab == null || !EnsurePortraitActionPoints(manager.m_Prefab)))
                {
                    RestoreHudPositionCapacity();
                    RestorePortraitActionPoints();
                    RestoreGiveButtonCapacity();
                    return false;
                }
                return true;
            }
            catch (Exception error)
            {
                RestoreHudPositionCapacity();
                RestorePortraitActionPoints();
                RestoreGiveButtonCapacity();
                Plugin.Log.LogWarning("[five-hero] HUD preflight failed: " + error.Message);
                return false;
            }
        }

        private static bool EnsureGiveButtonCapacity(uiItemMenu menu)
        {
            if (menu == null || GiveButtons == null || menu.m_PortraitButton == null)
                return false;
            try
            {
                uiItemMenuButton[] current = GiveButtons.GetValue(menu) as uiItemMenuButton[];
                if (current == null || current.Length < VanillaPlayerCapacity)
                    return false;
                if (_giveButtonMenu != null && (_giveButtonMenu != menu ||
                    !ReferenceEquals(current, _expandedGiveButtons)))
                {
                    if (!RestoreGiveButtonCapacity())
                        return false;
                    current = GiveButtons.GetValue(menu) as uiItemMenuButton[];
                    if (current == null || current.Length < VanillaPlayerCapacity)
                        return false;
                }
                if (current.Length >= ExpandedPlayerCapacity)
                    return true;

                uiItemMenuButton source = current[0] != null ? current[0] : menu.m_PortraitButton;
                if (source == null)
                    return false;
                Transform parent = menu.m_ButtonRoot != null ? menu.m_ButtonRoot : source.transform.parent;
                if (parent == null)
                    return false;

                uiItemMenuButton[] expanded = new uiItemMenuButton[ExpandedPlayerCapacity];
                Array.Copy(current, expanded, current.Length);
                List<uiItemMenuButton> created = new List<uiItemMenuButton>();
                try
                {
                    for (int i = current.Length; i < ExpandedPlayerCapacity; i++)
                    {
                        uiItemMenuButton clone = UnityEngine.Object.Instantiate(source);
                        created.Add(clone);
                        clone.gameObject.SetActive(false);
                        clone.transform.SetParent(parent, false);
                        clone.transform.localPosition = source.transform.localPosition;
                        clone.transform.localRotation = source.transform.localRotation;
                        clone.transform.localScale = source.transform.localScale;
                        expanded[i] = clone;
                    }
                    GiveButtons.SetValue(menu, expanded);
                    _giveButtonMenu = menu;
                    _originalGiveButtons = current;
                    _expandedGiveButtons = expanded;
                    _ownedGiveButtonClones.AddRange(created);
                    return true;
                }
                catch
                {
                    for (int i = 0; i < created.Count; i++)
                        if (created[i] != null) UnityEngine.Object.Destroy(created[i].gameObject);
                    throw;
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] item-transfer preflight failed: " + error.Message);
                return false;
            }
        }

        private static bool TryGetGoldMenuState(uiGoldMenu menu, out List<uiGoldMenuEntry> entries,
            out CharacterStats stats)
        {
            entries = null;
            stats = null;
            if (menu == null || GoldMenuCurrentEntries == null || GoldMenuStats == null)
                return false;
            try
            {
                entries = GoldMenuCurrentEntries.GetValue(menu) as List<uiGoldMenuEntry>;
                stats = GoldMenuStats.GetValue(menu) as CharacterStats;
                if (entries == null || stats == null || menu.m_GoldCount == null)
                    return false;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] == null || entries[i].m_InputField == null)
                        return false;
                return true;
            }
            catch (Exception error)
            {
                entries = null;
                stats = null;
                Plugin.Log.LogWarning("[five-hero] gold-menu state could not be read: " + error.Message);
                return false;
            }
        }

        private static bool TryApplyExpandedGoldAllocation(uiGoldMenu menu, uiGoldMenuEntry edited, int requestedGold)
        {
            List<uiGoldMenuEntry> entries;
            CharacterStats stats;
            if (!TryGetGoldMenuState(menu, out entries, out stats) || edited == null)
                return false;

            int editedIndex = -1;
            for (int i = 0; i < entries.Count; i++)
                if (ReferenceEquals(entries[i], edited))
                {
                    editedIndex = i;
                    break;
                }
            if (editedIndex < 0)
                return false;

            int gold = Math.Max(0, stats.m_Gold);
            int selectedGold = Mathf.Clamp(requestedGold, 0, gold);
            int remaining = gold - selectedGold;
            _settingExpandedGold.Add(menu);
            try
            {
                // Clamp all recipients against one shared balance and leave the remainder with the sender.
                for (int i = 0; i < entries.Count; i++)
                {
                    uiGoldMenuEntry entry = entries[i];
                    if (i == editedIndex)
                    {
                        string selectedText = selectedGold.ToString();
                        if (entry.m_InputField.text != selectedText)
                            entry.m_InputField.text = selectedText;
                        continue;
                    }

                    int requestedOther;
                    if (!int.TryParse(entry.m_InputField.text, out requestedOther) || requestedOther < 0)
                        requestedOther = 0;
                    int assigned = Math.Min(requestedOther, remaining);
                    string assignedText = assigned.ToString();
                    if (entry.m_InputField.text != assignedText)
                        entry.m_InputField.text = assignedText;
                    remaining -= assigned;
                }

                if (GoldMenuCurrentGold != null)
                    GoldMenuCurrentGold.SetValue(menu, remaining);
                menu.m_GoldCount.text = remaining.ToString();
                return true;
            }
            finally
            {
                _settingExpandedGold.Remove(menu);
            }
        }

        private static bool HasValidExpandedGoldAllocation(uiGoldMenu menu)
        {
            List<uiGoldMenuEntry> entries;
            CharacterStats stats;
            if (!TryGetGoldMenuState(menu, out entries, out stats))
                return false;
            long totalAssigned = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int amount;
                if (!int.TryParse(entries[i].m_InputField.text, out amount) || amount < 0)
                    return false;
                totalAssigned += amount;
            }
            return totalAssigned <= Math.Max(0, stats.m_Gold);
        }

        private static bool EnsureGoldMenuCapacity(uiGoldMenu menu, int partyCount)
        {
            if (menu == null || menu.m_FirstEntry == null || menu.m_GoldEntries == null)
                return false;

            int requiredEntries = Mathf.Clamp(partyCount - 1, 0, ExpandedPlayerCapacity - 1);
            if (requiredEntries == 0)
                return true;

            List<uiGoldMenuEntry> current = menu.m_GoldEntries;
            GoldMenuSnapshot previous = FindGoldMenuSnapshot(menu);
            if (previous != null)
            {
                if (ReferenceEquals(current, previous.Expanded) && current.Count >= requiredEntries)
                    return true;
                if (!RestoreGoldMenuSnapshot(previous))
                    return false;
                _goldMenuSnapshots.Remove(previous);
                current = menu.m_GoldEntries;
            }
            if (current.Count >= requiredEntries)
                return true;
            if (current.Count < VanillaPlayerCapacity - 1)
                return false;

            Transform parent = menu.m_FirstEntry.transform.parent;
            if (parent == null)
                return false;

            List<uiGoldMenuEntry> expanded = new List<uiGoldMenuEntry>(current);
            List<uiGoldMenuEntry> created = new List<uiGoldMenuEntry>();
            try
            {
                while (expanded.Count < requiredEntries)
                {
                    uiGoldMenuEntry source = menu.m_FirstEntry;
                    uiGoldMenuEntry clone = UnityEngine.Object.Instantiate(source);
                    created.Add(clone);
                    clone.transform.SetParent(parent, false);
                    clone.transform.localPosition = source.transform.localPosition;
                    clone.transform.localRotation = source.transform.localRotation;
                    clone.transform.localScale = source.transform.localScale;
                    clone.gameObject.SetActive(false);
                    expanded.Add(clone);
                }

                menu.m_GoldEntries = expanded;
                GoldMenuSnapshot snapshot = new GoldMenuSnapshot();
                snapshot.Menu = menu;
                snapshot.Original = current;
                snapshot.OriginalEntries = new List<uiGoldMenuEntry>(current);
                snapshot.Expanded = expanded;
                snapshot.Created.AddRange(created);
                _goldMenuSnapshots.Add(snapshot);
                return true;
            }
            catch (Exception error)
            {
                if (ReferenceEquals(menu.m_GoldEntries, expanded))
                    menu.m_GoldEntries = current;
                for (int i = 0; i < created.Count; i++)
                    if (created[i] != null) UnityEngine.Object.Destroy(created[i].gameObject);
                Plugin.Log.LogWarning("[five-hero] gold-transfer preflight failed: " + error.Message);
                return false;
            }
        }

        private static GoldMenuSnapshot FindGoldMenuSnapshot(uiGoldMenu menu)
        {
            for (int i = 0; i < _goldMenuSnapshots.Count; i++)
                if (ReferenceEquals(_goldMenuSnapshots[i].Menu, menu))
                    return _goldMenuSnapshots[i];
            return null;
        }

        private static bool RestoreGoldMenuSnapshot(GoldMenuSnapshot snapshot)
        {
            if (snapshot == null)
                return true;

            uiGoldMenu menu = snapshot.Menu;
            if (menu == null)
            {
                for (int i = 0; i < snapshot.Created.Count; i++)
                    if (snapshot.Created[i] != null) UnityEngine.Object.Destroy(snapshot.Created[i].gameObject);
                return true;
            }

            try
            {
                if (!ReferenceEquals(menu.m_GoldEntries, snapshot.Expanded))
                {
                    Plugin.Log.LogWarning("[five-hero] gold-entry list changed outside the framework; owned rows were retained.");
                    return false;
                }

                List<uiGoldMenuEntry> currentExpanded = menu.m_GoldEntries;
                List<uiGoldMenuEntry> restoredRows = new List<uiGoldMenuEntry>();
                for (int i = 0; i < currentExpanded.Count; i++)
                {
                    uiGoldMenuEntry entry = currentExpanded[i];
                    if (!IsOwnedGoldEntry(snapshot, entry) && !ContainsGoldEntry(restoredRows, entry))
                        restoredRows.Add(entry);
                }
                for (int i = 0; i < snapshot.Original.Count; i++)
                {
                    uiGoldMenuEntry entry = snapshot.Original[i];
                    if (!ContainsGoldEntry(snapshot.OriginalEntries, entry) &&
                        !IsOwnedGoldEntry(snapshot, entry) && !ContainsGoldEntry(restoredRows, entry))
                        restoredRows.Add(entry);
                }
                snapshot.Original.Clear();
                snapshot.Original.AddRange(restoredRows);
                menu.m_GoldEntries = snapshot.Original;
                List<uiGoldMenuEntry> currentRows = GoldMenuCurrentEntries == null ? null :
                    GoldMenuCurrentEntries.GetValue(menu) as List<uiGoldMenuEntry>;
                if (currentRows != null)
                    for (int i = snapshot.Created.Count - 1; i >= 0; i--)
                        currentRows.Remove(snapshot.Created[i]);

                for (int i = 0; i < snapshot.Created.Count; i++)
                {
                    uiGoldMenuEntry entry = snapshot.Created[i];
                    if (entry != null && !ContainsGoldEntry(menu.m_GoldEntries, entry) &&
                        !ContainsGoldEntry(currentRows, entry))
                        UnityEngine.Object.Destroy(entry.gameObject);
                }
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] gold-menu rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool ContainsGoldEntry(List<uiGoldMenuEntry> entries, uiGoldMenuEntry value)
        {
            if (entries == null)
                return false;
            for (int i = 0; i < entries.Count; i++)
                if (ReferenceEquals(entries[i], value))
                    return true;
            return false;
        }

        private static bool IsOwnedGoldEntry(GoldMenuSnapshot snapshot, uiGoldMenuEntry entry)
        {
            for (int i = 0; i < snapshot.Created.Count; i++)
                if (ReferenceEquals(snapshot.Created[i], entry))
                    return true;
            return false;
        }

        private static void InvokePopupGive(uiPopupMenu menu, int index)
        {
            try
            {
                PopupMenuOnClick.Invoke(menu, new object[] { uiPopupMenu.Action.Give, index });
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[five-hero] item-transfer popup click failed: " + error.Message);
                ShowRuntimeError("The expanded item-transfer action could not be completed safely.");
            }
        }

        private static bool EnsurePopupGiveCapacity(uiPopupMenu menu, int partyCount)
        {
            if (menu == null || menu.m_Popups == null || menu.m_Buttons == null ||
                menu.m_ButtonPrefab == null || menu.m_DisplayRoot == null || PopupMenuOnClick == null)
                return false;

            int requiredButtons = Mathf.Clamp(partyCount - 1, 0, ExpandedPlayerCapacity - 1);
            if (requiredButtons == 0)
                return true;

            uiPopupMenu.PopupButton givePopup = null;
            for (int i = 0; i < menu.m_Popups.Length; i++)
            {
                uiPopupMenu.PopupButton candidate = menu.m_Popups[i];
                if (candidate == null || candidate.m_Action != uiPopupMenu.Action.Give)
                    continue;
                if (givePopup != null)
                    return false;
                givePopup = candidate;
            }
            if (givePopup == null)
                return false;

            List<uiPopupMenuButton> buttons;
            if (!menu.m_Buttons.TryGetValue(uiPopupMenu.Action.Give, out buttons) || buttons == null)
                return false;

            PopupMenuSnapshot previous = FindPopupMenuSnapshot(menu);
            if (previous != null)
            {
                if (ReferenceEquals(previous.GivePopup, givePopup) &&
                    ReferenceEquals(previous.ExpandedButtons, buttons) && buttons.Count >= requiredButtons)
                    return true;
                if (!RestorePopupMenuSnapshot(previous))
                    return false;
                _popupMenuSnapshots.Remove(previous);
                if (!menu.m_Buttons.TryGetValue(uiPopupMenu.Action.Give, out buttons) || buttons == null)
                    return false;
            }
            if (buttons.Count >= requiredButtons)
                return true;
            if (givePopup.m_Count < buttons.Count)
                return false;

            List<uiPopupMenuButton> created = new List<uiPopupMenuButton>();
            try
            {
                while (buttons.Count < requiredButtons)
                {
                    int index = buttons.Count;
                    uiPopupMenuButton clone = UnityEngine.Object.Instantiate(menu.m_ButtonPrefab, menu.m_DisplayRoot);
                    created.Add(clone);
                    clone.SetString(FTKHub.Localized<Google2u.TextMenu>(givePopup.m_DisplayName));
                    UnityEngine.UI.Button button = clone.GetComponent<UnityEngine.UI.Button>();
                    if (button == null || clone.m_RawImage == null)
                        throw new InvalidOperationException("A native item-transfer popup button is incomplete.");
                    button.onClick.AddListener(delegate
                    {
                        InvokePopupGive(menu, index);
                    });
                    clone.m_RawImage.GetComponent<RectTransform>().sizeDelta = givePopup.m_IconSize;
                    clone.gameObject.SetActive(false);
                    buttons.Add(clone);
                }

                PopupMenuSnapshot snapshot = new PopupMenuSnapshot();
                snapshot.Menu = menu;
                snapshot.GivePopup = givePopup;
                snapshot.OriginalPopupCount = givePopup.m_Count;
                snapshot.ExpandedPopupCount = requiredButtons;
                snapshot.ExpandedButtons = buttons;
                snapshot.Created.AddRange(created);
                givePopup.m_Count = requiredButtons;
                _popupMenuSnapshots.Add(snapshot);
                return true;
            }
            catch (Exception error)
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    buttons.Remove(created[i]);
                    if (created[i] != null) UnityEngine.Object.Destroy(created[i].gameObject);
                }
                Plugin.Log.LogWarning("[five-hero] item-transfer popup preflight failed: " + error.Message);
                return false;
            }
        }

        private static PopupMenuSnapshot FindPopupMenuSnapshot(uiPopupMenu menu)
        {
            for (int i = 0; i < _popupMenuSnapshots.Count; i++)
                if (ReferenceEquals(_popupMenuSnapshots[i].Menu, menu))
                    return _popupMenuSnapshots[i];
            return null;
        }

        private static bool RestorePopupMenuSnapshot(PopupMenuSnapshot snapshot)
        {
            if (snapshot == null)
                return true;

            uiPopupMenu menu = snapshot.Menu;
            if (menu == null)
            {
                for (int i = 0; i < snapshot.Created.Count; i++)
                    if (snapshot.Created[i] != null) UnityEngine.Object.Destroy(snapshot.Created[i].gameObject);
                return true;
            }

            try
            {
                List<uiPopupMenuButton> current;
                if (menu.m_Buttons == null ||
                    !menu.m_Buttons.TryGetValue(uiPopupMenu.Action.Give, out current) ||
                    !ReferenceEquals(current, snapshot.ExpandedButtons))
                {
                    Plugin.Log.LogWarning("[five-hero] item-transfer popup changed outside the framework; owned buttons were retained.");
                    return false;
                }

                bool popupStillOwned = false;
                if (menu.m_Popups != null)
                    for (int i = 0; i < menu.m_Popups.Length; i++)
                        if (ReferenceEquals(menu.m_Popups[i], snapshot.GivePopup))
                        {
                            popupStillOwned = true;
                            break;
                        }
                if (popupStillOwned && snapshot.GivePopup.m_Count != snapshot.ExpandedPopupCount)
                {
                    Plugin.Log.LogWarning("[five-hero] item-transfer popup count changed outside the framework; owned buttons were retained.");
                    return false;
                }

                for (int i = snapshot.Created.Count - 1; i >= 0; i--)
                {
                    uiPopupMenuButton button = snapshot.Created[i];
                    current.Remove(button);
                    if (button != null && !ContainsPopupButton(menu.m_Buttons, button))
                        UnityEngine.Object.Destroy(button.gameObject);
                }
                if (popupStillOwned)
                    snapshot.GivePopup.m_Count = snapshot.OriginalPopupCount;
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] item-transfer popup rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool ContainsPopupButton(Dictionary<uiPopupMenu.Action, List<uiPopupMenuButton>> buttons,
            uiPopupMenuButton value)
        {
            if (buttons == null)
                return false;
            foreach (List<uiPopupMenuButton> list in buttons.Values)
                if (list != null)
                    for (int i = 0; i < list.Count; i++)
                        if (ReferenceEquals(list[i], value))
                            return true;
            return false;
        }

        private static bool RestoreGoldMenuCapacity()
        {
            bool restored = true;
            for (int i = _goldMenuSnapshots.Count - 1; i >= 0; i--)
            {
                GoldMenuSnapshot snapshot = _goldMenuSnapshots[i];
                if (!RestoreGoldMenuSnapshot(snapshot))
                {
                    restored = false;
                    continue;
                }
                _goldMenuSnapshots.RemoveAt(i);
            }
            return restored;
        }

        private static bool RestorePopupMenuCapacity()
        {
            bool restored = true;
            for (int i = _popupMenuSnapshots.Count - 1; i >= 0; i--)
            {
                PopupMenuSnapshot snapshot = _popupMenuSnapshots[i];
                if (!RestorePopupMenuSnapshot(snapshot))
                {
                    restored = false;
                    continue;
                }
                _popupMenuSnapshots.RemoveAt(i);
            }
            return restored;
        }

        private static bool RestoreGiveButtonCapacity()
        {
            if (_expandedGiveButtons == null)
                return true;
            if (_giveButtonMenu == null)
            {
                _giveButtonMenu = null;
                _originalGiveButtons = null;
                _expandedGiveButtons = null;
                _ownedGiveButtonClones.Clear();
                return true;
            }
            try
            {
                uiItemMenuButton[] current = GiveButtons == null ? null :
                    GiveButtons.GetValue(_giveButtonMenu) as uiItemMenuButton[];
                if (ReferenceEquals(current, _expandedGiveButtons))
                {
                    GiveButtons.SetValue(_giveButtonMenu, _originalGiveButtons);
                    current = _originalGiveButtons;
                }
                for (int i = 0; i < _ownedGiveButtonClones.Count; i++)
                {
                    uiItemMenuButton clone = _ownedGiveButtonClones[i];
                    if (clone != null && !ContainsButton(current, clone))
                        UnityEngine.Object.Destroy(clone.gameObject);
                }
                _giveButtonMenu = null;
                _originalGiveButtons = null;
                _expandedGiveButtons = null;
                _ownedGiveButtonClones.Clear();
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] item-transfer rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool RestorePresentation()
        {
            bool setup = RestoreSetupUi();
            bool hud = RestoreHudPositionCapacity();
            bool portraits = RestorePortraitActionPoints();
            bool itemButtons = RestoreGiveButtonCapacity();
            bool goldMenus = RestoreGoldMenuCapacity();
            bool itemPopups = RestorePopupMenuCapacity();
            bool combat = RestoreAttackDistanceSnapshots();
            return setup && hud && portraits && itemButtons && goldMenus && itemPopups && combat;
        }

        private static bool ContainsButton(uiItemMenuButton[] buttons, uiItemMenuButton value)
        {
            if (buttons == null)
                return false;
            for (int i = 0; i < buttons.Length; i++)
                if (ReferenceEquals(buttons[i], value))
                    return true;
            return false;
        }

        private static bool EnsurePortraitActionPoints(uiPortraitHolder holder)
        {
            if (holder == null || holder.m_PortraitActionPoints == null ||
                holder.m_PortraitActionPoints.Count < VanillaPlayerCapacity)
                return false;
            List<uiPortraitActionPoint> points = holder.m_PortraitActionPoints;
            List<uiPortraitActionPoint> created = new List<uiPortraitActionPoint>();
            PortraitActionPointSnapshot previous = FindPortraitSnapshot(holder);
            if (previous != null && ReferenceEquals(points, previous.Expanded))
                return points.Count >= ExpandedPlayerCapacity;
            if (previous != null)
            {
                RestorePortraitSnapshot(previous);
                _portraitActionPointSnapshots.Remove(previous);
                points = holder.m_PortraitActionPoints;
            }
            if (points.Count >= ExpandedPlayerCapacity)
                return true;

            List<uiPortraitActionPoint> expanded = new List<uiPortraitActionPoint>(points);
            try
            {
                for (int i = expanded.Count; i < ExpandedPlayerCapacity; i++)
                {
                    uiPortraitActionPoint source = expanded[i - 1];
                    if (source == null || source.transform.parent == null || source.m_ActionPoint == null ||
                        source.m_Portrait == null || source.m_GraveStone == null)
                        throw new InvalidOperationException("A native portrait action point has no parent.");
                    uiPortraitActionPoint clone = UnityEngine.Object.Instantiate(source);
                    created.Add(clone);
                    clone.name = "FiveHeroPortraitActionPoint" + (i + 1);
                    clone.transform.SetParent(source.transform.parent, false);
                    clone.transform.localRotation = source.transform.localRotation;
                    clone.transform.localScale = source.transform.localScale;
                    Vector3 spacing;
                    if (i >= 2)
                        spacing = expanded[i - 1].transform.localPosition - expanded[i - 2].transform.localPosition;
                    else
                        spacing = Vector3.right * 28f;
                    if (spacing.sqrMagnitude < 1f)
                        spacing = Vector3.right * 28f;
                    clone.transform.localPosition = source.transform.localPosition + spacing;
                    expanded.Add(clone);
                }
                PortraitActionPointSnapshot snapshot = new PortraitActionPointSnapshot();
                snapshot.Holder = holder;
                snapshot.Original = points;
                snapshot.Expanded = expanded;
                snapshot.Created.AddRange(created);
                holder.m_PortraitActionPoints = expanded;
                _portraitActionPointSnapshots.Add(snapshot);
                return expanded.Count >= ExpandedPlayerCapacity;
            }
            catch (Exception error)
            {
                if (ReferenceEquals(holder.m_PortraitActionPoints, expanded))
                    holder.m_PortraitActionPoints = points;
                for (int i = 0; i < created.Count; i++)
                    if (created[i] != null) UnityEngine.Object.Destroy(created[i].gameObject);
                Plugin.Log.LogWarning("[five-hero] portrait preflight failed: " + error.Message);
                return false;
            }
        }

        private static PortraitActionPointSnapshot FindPortraitSnapshot(uiPortraitHolder holder)
        {
            for (int i = 0; i < _portraitActionPointSnapshots.Count; i++)
                if (ReferenceEquals(_portraitActionPointSnapshots[i].Holder, holder))
                    return _portraitActionPointSnapshots[i];
            return null;
        }

        private static void RestorePortraitSnapshot(PortraitActionPointSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            uiPortraitHolder holder = snapshot.Holder;
            if (holder != null && ReferenceEquals(holder.m_PortraitActionPoints, snapshot.Expanded))
                holder.m_PortraitActionPoints = snapshot.Original;
            List<uiPortraitActionPoint> current = holder == null ? null : holder.m_PortraitActionPoints;
            for (int i = 0; i < snapshot.Created.Count; i++)
            {
                uiPortraitActionPoint point = snapshot.Created[i];
                if (point != null && !ContainsPortraitPoint(current, point))
                    UnityEngine.Object.Destroy(point.gameObject);
            }
        }

        private static bool RestorePortraitActionPoints()
        {
            try
            {
                for (int i = _portraitActionPointSnapshots.Count - 1; i >= 0; i--)
                    RestorePortraitSnapshot(_portraitActionPointSnapshots[i]);
                _portraitActionPointSnapshots.Clear();
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] portrait rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool ContainsPortraitPoint(List<uiPortraitActionPoint> points, uiPortraitActionPoint value)
        {
            if (points == null)
                return false;
            for (int i = 0; i < points.Count; i++)
                if (ReferenceEquals(points[i], value))
                    return true;
            return false;
        }

        private static bool CanPrepareCombatLayouts()
        {
            DioramaManager manager = DioramaManager.Instance;
            if (manager == null || manager.m_DioramaTable == null || manager.m_DioramaTable.Count == 0)
                return false;
            HashSet<Diorama> checkedDioramas = new HashSet<Diorama>();
            foreach (Diorama diorama in manager.m_DioramaTable.Values)
            {
                if (diorama == null)
                    return false;
                if (!checkedDioramas.Add(diorama))
                    continue;
                if (diorama.m_LayoutTable == null || diorama.m_LayoutTable.Count == 0)
                    return false;
                foreach (Diorama.Layout layout in diorama.m_LayoutTable.Values)
                {
                    if (layout == null || layout.m_TargetRoot == null)
                        return false;
                    int playerTargets = 0;
                    int enemyTargets = 0;
                    bool hasPlayerMidpoint = false;
                    foreach (Transform target in layout.m_TargetRoot)
                    {
                        if (target == null)
                            return false;
                        if (target.name.Contains("Player"))
                        {
                            playerTargets++;
                            hasPlayerMidpoint |= target.name == "PlayerinBetween";
                            if (target.GetComponent<DummyAttackSlide>() == null)
                                return false;
                        }
                        else if (target.name.Contains("enemy"))
                        {
                            enemyTargets++;
                            if (target.GetComponent<DummyAttackSlide>() == null)
                                return false;
                        }
                    }
                    if (playerTargets < (hasPlayerMidpoint ? ExpandedPlayerCapacity : VanillaPlayerCapacity) ||
                        enemyTargets < VanillaPlayerCapacity)
                        return false;
                }
            }
            return checkedDioramas.Count > 0;
        }

        private static bool ExtendDioramaDistances(Diorama diorama, Diorama.LayoutID layoutId)
        {
            if (diorama == null || diorama.m_LayoutTable == null ||
                !diorama.m_LayoutTable.ContainsKey(layoutId))
                return false;
            Diorama.Layout layout = diorama.m_LayoutTable[layoutId];
            if (layout == null || layout.m_TargetRoot == null)
                return false;
            int snapshotStart = _attackDistanceSnapshots.Count;
            try
            {
                foreach (Transform target in layout.m_TargetRoot)
                {
                    if (target == null)
                        throw new InvalidOperationException("A combat layout contains a missing target.");
                    if (!target.name.Contains("Player") && !target.name.Contains("enemy"))
                        continue;
                    DummyAttackSlide slide = target.GetComponent<DummyAttackSlide>();
                    if (slide == null)
                        throw new InvalidOperationException("A combat target has no distance component.");
                    float[] distances = slide.m_Distances;
                    if (distances != null && distances.Length >= ExpandedPlayerCapacity)
                        continue;

                    AttackDistanceSnapshot previous = FindAttackDistanceSnapshot(slide);
                    if (previous != null)
                    {
                        if (ReferenceEquals(distances, previous.Expanded))
                            continue;
                        _attackDistanceSnapshots.Remove(previous);
                    }

                    float[] expanded = new float[ExpandedPlayerCapacity];
                    if (distances != null)
                        Array.Copy(distances, expanded, Math.Min(distances.Length, expanded.Length));
                    AttackDistanceSnapshot snapshot = new AttackDistanceSnapshot();
                    snapshot.Slide = slide;
                    snapshot.Original = distances;
                    snapshot.Expanded = expanded;
                    _attackDistanceSnapshots.Add(snapshot);
                    slide.m_Distances = expanded;
                }
                return true;
            }
            catch (Exception error)
            {
                RestoreAttackDistanceSnapshotsFrom(snapshotStart);
                Plugin.Log.LogError("[five-hero] combat target-distance preflight failed for layout " + layoutId + ": " + error.Message);
                return false;
            }
        }

        private static AttackDistanceSnapshot FindAttackDistanceSnapshot(DummyAttackSlide slide)
        {
            for (int i = 0; i < _attackDistanceSnapshots.Count; i++)
                if (ReferenceEquals(_attackDistanceSnapshots[i].Slide, slide))
                    return _attackDistanceSnapshots[i];
            return null;
        }

        private static void RestoreAttackDistanceSnapshot(AttackDistanceSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Slide != null &&
                ReferenceEquals(snapshot.Slide.m_Distances, snapshot.Expanded))
                snapshot.Slide.m_Distances = snapshot.Original;
        }

        private static void RestoreAttackDistanceSnapshotsFrom(int startIndex)
        {
            for (int i = _attackDistanceSnapshots.Count - 1; i >= startIndex; i--)
            {
                RestoreAttackDistanceSnapshot(_attackDistanceSnapshots[i]);
                _attackDistanceSnapshots.RemoveAt(i);
            }
        }

        private static bool RestoreAttackDistanceSnapshots()
        {
            try
            {
                RestoreAttackDistanceSnapshotsFrom(0);
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] combat distance rollback failed: " + error.Message);
                return false;
            }
        }

        private static bool ExpandRuntimeDummies()
        {
            if (_expandedHub != null && _expandedDummies != null &&
                ReferenceEquals(_expandedHub.m_Dummies, _expandedDummies))
                return true;
            if (!CanPrepareRuntimeDummies() || PhotonNetwork.player == null)
                return false;

            FTKHub hub = FTKHub.Instance;
            GameObject[] original = hub.m_Dummies;
            GameObject source = original[VanillaPlayerCapacity - 1];
            GameObject staging = new GameObject("FTKMF Five Hero Dummy Staging");
            staging.SetActive(false);
            List<GameObject> clones = new List<GameObject>();
            List<int> viewIds = new List<int>();
            try
            {
                for (int i = VanillaPlayerCapacity; i < ExpandedPlayerCapacity; i++)
                {
                    int viewId = PhotonNetwork.AllocateViewID();
                    if (viewId <= 0 || viewIds.Contains(viewId))
                        throw new InvalidOperationException("Photon did not allocate a unique dummy view ID.");
                    viewIds.Add(viewId);

                    GameObject clone = UnityEngine.Object.Instantiate(source, staging.transform);
                    clones.Add(clone);
                    clone.SetActive(false);
                    clone.name = "Player " + (i + 1) + " Dummy";
                    PhotonView view = clone.GetComponent<PhotonView>();
                    if (view == null)
                        throw new InvalidOperationException("A cloned player dummy has no PhotonView.");
                    view.viewID = viewId;
                    clone.transform.SetParent(source.transform.parent, false);
                    clone.transform.localPosition = source.transform.localPosition;
                    clone.transform.localRotation = source.transform.localRotation;
                    clone.transform.localScale = source.transform.localScale;
                }

                GameObject[] expanded = new GameObject[ExpandedPlayerCapacity + 3];
                Array.Copy(original, 0, expanded, 0, VanillaPlayerCapacity);
                expanded[3] = clones[0];
                expanded[4] = clones[1];
                Array.Copy(original, VanillaPlayerCapacity, expanded, ExpandedPlayerCapacity, 3);
                UnityEngine.Object.Destroy(staging);

                _originalDummies = original;
                _expandedDummies = expanded;
                _expandedHub = hub;
                _ownedDummyClones.Clear();
                _ownedDummyClones.AddRange(clones);
                _ownedViewIds.Clear();
                _ownedViewIds.AddRange(viewIds);
                hub.m_Dummies = expanded;
                return true;
            }
            catch (Exception error)
            {
                UnityEngine.Object.Destroy(staging);
                for (int i = 0; i < clones.Count; i++)
                    if (clones[i] != null) UnityEngine.Object.Destroy(clones[i]);
                for (int i = 0; i < viewIds.Count; i++)
                    PhotonNetwork.UnAllocateViewID(viewIds[i]);
                Plugin.Log.LogWarning("[five-hero] dummy preflight failed: " + error.Message);
                return false;
            }
        }

        private static bool RestoreRuntimeDummies(bool titleBoundary = false)
        {
            if (_expandedHub == null || _originalDummies == null || _expandedDummies == null)
                return true;
            if (!titleBoundary && _expandedHub.m_CharacterOverworlds != null &&
                _expandedHub.m_CharacterOverworlds.Count != 0)
                return false;
            if (!ReferenceEquals(_expandedHub.m_Dummies, _expandedDummies))
            {
                Plugin.Log.LogWarning("[five-hero] dummy pool changed outside the framework; owned clones were retained.");
                return false;
            }

            _expandedHub.m_Dummies = _originalDummies;
            List<GameObject> clones = new List<GameObject>(_ownedDummyClones);
            List<int> ids = new List<int>(_ownedViewIds);
            _expandedHub = null;
            _originalDummies = null;
            _expandedDummies = null;
            _ownedDummyClones.Clear();
            _ownedViewIds.Clear();
            for (int i = 0; i < clones.Count; i++)
                if (clones[i] != null) UnityEngine.Object.Destroy(clones[i]);
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(ReleaseViewIds(ids));
            else
                for (int i = 0; i < ids.Count; i++) PhotonNetwork.UnAllocateViewID(ids[i]);
            return true;
        }

        private static IEnumerator ReleaseViewIds(List<int> ids)
        {
            yield return null;
            for (int i = 0; i < ids.Count; i++)
                PhotonNetwork.UnAllocateViewID(ids[i]);
        }

        private static bool IsExpandedSinglePlayerActive()
        {
            // Keep the runtime support patches active for the whole owned run, even if the config file
            // changes while the game is open. Capacity is released at the title boundary.
            return _ownsExpandedCapacity && GameLogic.Instance != null &&
                IsOfflineSinglePlayer(GameLogic.Instance.m_GameMode);
        }

        private static bool IsFiveHeroPartyActive()
        {
            if (!IsExpandedSinglePlayerActive())
                return false;
            uiStartGame title = uiStartGame.Instance;
            if (title != null && title.m_ActualMaxCharCount > VanillaPlayerCapacity)
                return true;
            FTKHub hub = FTKHub.Instance;
            return hub != null && hub.TotalPlayers > VanillaPlayerCapacity;
        }

        private static int GetExpandedPartyCount()
        {
            uiStartGame title = uiStartGame.Instance;
            int count = title == null ? 0 : title.m_ActualMaxCharCount;
            if (count <= VanillaPlayerCapacity)
            {
                FTKHub hub = FTKHub.Instance;
                count = hub == null ? 0 : hub.TotalPlayers;
            }
            if (count <= VanillaPlayerCapacity)
                count = ExpandedPlayerCapacity;
            return Mathf.Clamp(count, VanillaPlayerCapacity + 1, ExpandedPlayerCapacity);
        }

        private static bool EnsureHudPositionCapacity(uiHudScroller scroller, ref float[] positions)
        {
            if (scroller == null)
                return false;
            try
            {
                FieldInfo field = AccessTools.Field(typeof(uiHudScroller), "m_Positions");
                if (field == null)
                    return false;
                if (positions == null)
                    positions = field.GetValue(scroller) as float[];
                if (positions == null)
                    return false;

                if (_hudScroller != null && (_hudScroller != scroller ||
                    !ReferenceEquals(positions, _expandedHudPositions)))
                {
                    if (!RestoreHudPositionCapacity())
                        return false;
                    positions = field.GetValue(scroller) as float[];
                    if (positions == null)
                        return false;
                }
                if (_hudScroller == scroller && ReferenceEquals(positions, _expandedHudPositions))
                    return positions.Length >= ExpandedPlayerCapacity + 1;

                float[] original = positions;
                float[] expanded = new float[Math.Max(original.Length, ExpandedPlayerCapacity + 1)];
                Array.Copy(original, expanded, original.Length);
                field.SetValue(scroller, expanded);
                _hudScroller = scroller;
                _originalHudPositions = original;
                _expandedHudPositions = expanded;
                positions = expanded;
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] HUD position preflight failed: " + error.Message);
                return false;
            }
        }

        private static bool RestoreHudPositionCapacity()
        {
            if (_hudScroller == null || _expandedHudPositions == null)
                return true;
            try
            {
                FieldInfo field = AccessTools.Field(typeof(uiHudScroller), "m_Positions");
                if (field != null && ReferenceEquals(field.GetValue(_hudScroller), _expandedHudPositions))
                    field.SetValue(_hudScroller, _originalHudPositions);
                _hudScroller = null;
                _originalHudPositions = null;
                _expandedHudPositions = null;
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[five-hero] HUD position rollback failed: " + error.Message);
                return false;
            }
        }

        private static void LayoutFiveHeroHud(uiHudScroller scroller, float[] positions, float hudWidth,
            int partyCount)
        {
            int count = Mathf.Clamp(partyCount, VanillaPlayerCapacity + 1, ExpandedPlayerCapacity);
            if (scroller == null || positions == null || positions.Length < count + 1)
                return;
            float canvasWidth = FTKUI.Instance != null && FTKUI.Instance.m_MainCanvas != null
                ? FTKUI.Instance.m_MainCanvas.rect.width
                : Screen.width;
            float scale = OverworldCamera.Instance != null && OverworldCamera.Instance.Is1610()
                ? scroller.m_16_10_Spacing_Scale
                : 1f;
            float maxStep = Mathf.Max(1f, (canvasWidth - hudWidth) / (count - 1));
            float step = Mathf.Min(hudWidth * scale, maxStep);
            float firstPosition = 0f - ((count - 1) * step * 0.5f);
            for (int target = 1; target <= count; target++)
                positions[target] = firstPosition + ((target - 1) * step);
        }

        private static void RelayoutSetupForCurrentParty()
        {
            if (!IsExpandedSinglePlayerActive() || uiStartGame.Instance == null)
                return;
            int count = uiStartGame.Instance.m_CreateUIs == null ? 0 : uiStartGame.Instance.m_CreateUIs.Count;
            if (count > 0)
                ApplySetupLayout(count);
        }

        private static bool PrefixResume(ResumeBrowser.RunInfo runInfo)
        {
            if (runInfo != null && runInfo.m_GameInfo != null &&
                runInfo.m_GameInfo.m_PlayedWith != null &&
                runInfo.m_GameInfo.m_PlayedWith.Length > VanillaPlayerCapacity &&
                Plugin.EnableFiveHeroSinglePlayer != null && Plugin.EnableFiveHeroSinglePlayer.Value)
                PrepareForSelectedMode(runInfo.m_GameInfo.m_GameType);

            string error;
            if (!ValidateResume(runInfo, out error))
            {
                if (runInfo != null && runInfo.m_GameInfo != null)
                    ApplyModeCapacity(runInfo.m_GameInfo.m_GameType);
                ShowResumeError(error);
                return false;
            }

            GameLogic.GameMode mode = runInfo.m_GameInfo.m_GameType;
            ApplyModeCapacity(mode);
            if (runInfo.m_GameInfo.m_PlayedWith.Length > VanillaPlayerCapacity &&
                !OwnsExpandedCapacity)
            {
                ShowResumeError("Five-hero support is not ready in this framework session. Enable the option after updating the framework, then try again.");
                return false;
            }
            return true;
        }

        private static bool ValidateResume(ResumeBrowser.RunInfo runInfo, out string error)
        {
            error = null;
            GameSerialize.GameInfo info = runInfo == null ? null : runInfo.m_GameInfo;
            int count = info == null || info.m_PlayedWith == null ? 0 : info.m_PlayedWith.Length;
            if (count < 1 || count > ExpandedPlayerCapacity)
            {
                error = "This save has an unsupported hero count. Only parties of one to five heroes can be resumed.";
                return false;
            }

            if (count <= VanillaPlayerCapacity)
                return true;

            if (info.m_GameType != GameLogic.GameMode.SinglePlayer)
            {
                error = "This framework supports expanded parties only in single-player. This save was left unchanged.";
                return false;
            }

            if (Plugin.EnableFiveHeroSinglePlayer == null || !Plugin.EnableFiveHeroSinglePlayer.Value)
            {
                error = "This save has " + count + " heroes. Enable Core/EnableFiveHeroSinglePlayer to resume it.";
                return false;
            }

            if (!SupportsFiveHeroSetup)
            {
                error = "This save has " + count + " heroes, but five-hero setup or runtime support is unavailable. Update the framework before resuming it.";
                return false;
            }

            try
            {
                if (runInfo.m_Filename == null)
                {
                    error = "The selected save has no file path, so its five-hero roster cannot be checked safely.";
                    return false;
                }

                // This deserializes the saved records only. Unlike GameSerialize.Load, it does not begin map or
                // game-state deserialization, so a rejected save cannot leave a partially loaded run.
                GameSerialize save = GameSerialize.GetGameSerialize(runInfo.m_Filename);
                GameSerialize.GameInfo savedInfo = save == null ? null : save.m_GameInfo;
                if (savedInfo == null || savedInfo.m_PlayedWith == null ||
                    savedInfo.m_PlayedWith.Length != count ||
                    savedInfo.m_PlayedWithClass == null || savedInfo.m_PlayedWithClass.Length != count ||
                    savedInfo.m_PlayedWithLevel == null || savedInfo.m_PlayedWithLevel.Length != count ||
                    save.m_PlayerSerialize == null || save.m_PlayerSerialize.Length != count)
                {
                    error = "The saved hero summary and player records do not agree. The run was not loaded.";
                    return false;
                }

                if (savedInfo.m_GameType != GameLogic.GameMode.SinglePlayer)
                {
                    error = "The saved run is not a single-player party. The run was not loaded.";
                    return false;
                }
                for (int i = 0; i < save.m_PlayerSerialize.Length; i++)
                    if (save.m_PlayerSerialize[i] == null)
                    {
                        error = "The saved hero records are incomplete. The run was not loaded.";
                        return false;
                    }
            }
            catch (System.Exception exception)
            {
                error = "The five-hero save could not be checked safely: " + exception.Message;
                return false;
            }

            return true;
        }

        private static void ShowResumeError(string message)
        {
            Plugin.Log.LogWarning("[five-hero] resume stopped: " + message);
            uiStartGame title = uiStartGame.Instance;
            bool displayed = false;
            if (title != null && title.m_GameConfig != null &&
                title.m_GameConfig.gameObject.activeInHierarchy && title.m_GameConfig.m_ErrorText != null)
            {
                title.m_GameConfig.m_ErrorText.text = message;
                displayed = true;
            }
            if (title != null && title.m_ResumeBrowser != null &&
                title.m_ResumeBrowser.gameObject.activeInHierarchy && title.m_ResumeBrowser.m_ErrorText != null)
            {
                title.m_ResumeBrowser.m_ErrorText.text = message;
                displayed = true;
            }
            if (!displayed)
                ShowRuntimeError(message);
        }

        private static void ShowResumeBrowserWithError(string message)
        {
            uiStartGame title = uiStartGame.Instance;
            if (title != null)
                title.ShowResumeBrowser();
            ShowResumeError(message);
        }

        private static bool PrefixUninspectableQuickResume()
        {
            if (Plugin.EnableFiveHeroSinglePlayer == null || !Plugin.EnableFiveHeroSinglePlayer.Value)
                return true;
            ShowResumeBrowserWithError("The last save's party size could not be checked safely. The run was not loaded.");
            return false;
        }

        private static void ShowRuntimeError(string message)
        {
            Plugin.Log.LogWarning("[five-hero] " + message);
            try { uiAlertBox.Construct(message); }
            catch (Exception error) { Plugin.Log.LogWarning("[five-hero] could not show the alert: " + error.Message); }
        }

        [HarmonyPatch(typeof(GameConfig), "OnChangeValueGameType")]
        private static class GameModeCapacityPatch
        {
            private static void Postfix()
            {
                if (GameLogic.Instance == null)
                    return;
                PrepareForSelectedMode(GameLogic.Instance.m_GameMode);
                ApplyModeCapacity(GameLogic.Instance.m_GameMode);
            }
        }

        [HarmonyPatch(typeof(uiStartGame), "OnResumeGame")]
        private static class ResumePreflightPatch
        {
            private static bool Prefix(ResumeBrowser.RunInfo _runInfo)
            {
                return PrefixResume(_runInfo);
            }
        }

        [HarmonyPatch(typeof(MainScreen), "OnResume")]
        private static class MainScreenResumePreflightPatch
        {
            private static bool Prefix()
            {
                string filename = PlayerPrefs.GetString("LastSave", string.Empty);
                if (string.IsNullOrEmpty(filename))
                    return true;
                GameSerialize.GameInfo info;
                try { info = GameSerialize.GetGameInfo(filename); }
                catch (Exception error)
                {
                    Plugin.Log.LogWarning("[five-hero] quick-resume metadata could not be read: " + error.Message);
                    return PrefixUninspectableQuickResume();
                }
                if (info == null || info.m_PlayedWith == null)
                    return PrefixUninspectableQuickResume();

                int count = info.m_PlayedWith.Length;
                if (count <= VanillaPlayerCapacity)
                {
                    ApplyModeCapacity(info.m_GameType);
                    return true;
                }

                string message = null;
                if (count > ExpandedPlayerCapacity)
                {
                    message = "This save has an unsupported hero count. Only parties of one to five heroes can be resumed.";
                }
                else if (info.m_GameType != GameLogic.GameMode.SinglePlayer)
                {
                    message = "This framework supports expanded parties only in single-player. This save was left unchanged.";
                }
                else if (Plugin.EnableFiveHeroSinglePlayer == null || !Plugin.EnableFiveHeroSinglePlayer.Value)
                {
                    message = "This save has " + count + " heroes. Enable Core/EnableFiveHeroSinglePlayer to resume it.";
                }
                else
                {
                    PrepareForSelectedMode(info.m_GameType);
                }

                ApplyModeCapacity(info.m_GameType);
                if (message == null)
                {
                    if (OwnsExpandedCapacity)
                        return true;
                    message = "Five-hero support is not ready in this framework session. Enable the option after updating the framework, then try again.";
                }
                ShowResumeBrowserWithError(message);
                return false;
            }
        }

        [HarmonyPatch(typeof(uiCharacterCreateRoot), "Show")]
        private static class CharacterCreatePreflightPatch
        {
            private static void Prefix(uiCharacterCreateRoot __instance)
            {
                if (!IsExpandedSinglePlayerActive())
                    return;
                bool ready = TryPrepareSetupUi(__instance);
                SetSetupUiReady(ready);
                if (!ready)
                {
                    SetRuntimeReady(false);
                    RestoreVanillaCapacity();
                    if (uiStartGame.Instance != null)
                        uiStartGame.Instance.m_ActualMaxCharCount = VanillaPlayerCapacity;
                    RestorePresentation();
                    ShowRuntimeError("Five-hero setup could not prepare its native UI targets. This run will use three heroes.");
                }
            }
        }

        [HarmonyPatch(typeof(uiStartGame), "ShowCreateCharacter")]
        private static class RuntimeDummyPreflightPatch
        {
            private static bool Prefix()
            {
                if (!IsExpandedSinglePlayerActive())
                    return true;
                if (ExpandRuntimeDummies())
                    return true;

                SetRuntimeReady(false);
                RestoreVanillaCapacity();
                RestorePresentation();
                uiStartGame title = uiStartGame.Instance;
                if (title != null && title.m_IsResuming && title.m_ActualMaxCharCount > VanillaPlayerCapacity)
                {
                    ShowRuntimeError("Five-hero dummy setup failed. The saved run was not loaded. Check the framework log for details.");
                    title.ShowStartPage();
                    return false;
                }

                if (title != null)
                    title.m_ActualMaxCharCount = VanillaPlayerCapacity;
                RestoreSetupUi();
                RestoreGiveButtonCapacity();
                ShowRuntimeError("Five-hero dummy setup failed. This run will use three heroes. Check the framework log for details.");
                return true;
            }
        }

        [HarmonyPatch(typeof(GameConfig), "OnStartGame")]
        private static class StartGameBoundaryPatch
        {
            private static bool Prefix(GameConfig __instance)
            {
                return PrepareStartBoundary(__instance);
            }
        }

        [HarmonyPatch(typeof(uiStartGame), "ShowStartPage")]
        private static class RestoreAtTitlePatch
        {
            private static void Postfix()
            {
                ApplyModeCapacity(GameLogic.GameMode.Multiplayer);
                RestoreRuntimeDummies(true);
                RestorePresentation();
                SetSetupUiReady(false);
                SetRuntimeReady(false);
                _hudReady = false;
                _combatReady = false;
            }
        }

        [HarmonyPatch(typeof(uiStartGame), "RegisterUI")]
        private static class SetupHudLayoutPatch
        {
            private static void Postfix()
            {
                RelayoutSetupForCurrentParty();
            }
        }

        [HarmonyPatch(typeof(uiQuickPlayerCreate), "RemoveCharacterRPC")]
        private static class SetupHudRemoveLayoutPatch
        {
            private static void Postfix()
            {
                RelayoutSetupForCurrentParty();
            }
        }

        [HarmonyPatch(typeof(uiHudScroller), "Init")]
        private static class FiveHeroHudInitPatch
        {
            private static bool Prefix(uiHudScroller __instance, uiPlayerMainHud _playerHud,
                ref int ___m_Index, ref Dictionary<uiPlayerMainHud, int> ___m_TargetIndex,
                ref List<uiPlayerMainHud> ___m_Huds, ref float ___m_HudWidth,
                ref float[] ___m_Positions)
            {
                if (!IsFiveHeroPartyActive())
                    return true;
                if (_playerHud == null || _playerHud.m_Cow == null ||
                    !EnsureHudPositionCapacity(__instance, ref ___m_Positions))
                    return false;
                int target = _playerHud.m_Cow.m_FTKPlayerID.TurnIndex + 1;
                int partyCount = GetExpandedPartyCount();
                if (target < 1 || target > partyCount)
                    return false;
                if (___m_TargetIndex == null || ___m_Huds == null)
                    return false;
                ___m_Index = 0;
                ___m_TargetIndex[_playerHud] = target;
                if (!___m_Huds.Contains(_playerHud))
                    ___m_Huds.Add(_playerHud);
                RectTransform rect = _playerHud.GetComponent<RectTransform>();
                if (rect == null)
                    return false;
                ___m_HudWidth = rect.rect.width;
                LayoutFiveHeroHud(__instance, ___m_Positions, ___m_HudWidth, partyCount);
                Vector3 local = rect.localPosition;
                local.y = 0f - rect.anchoredPosition.y;
                local.x = ___m_Positions[target];
                rect.localPosition = local;
                return false;
            }
        }

        [HarmonyPatch(typeof(uiHudScroller), "UpdateHudPresetPositions")]
        private static class FiveHeroHudPresetPatch
        {
            private static void Postfix(uiHudScroller __instance, float ___m_HudWidth,
                ref float[] ___m_Positions)
            {
                if (!IsFiveHeroPartyActive() ||
                    !EnsureHudPositionCapacity(__instance, ref ___m_Positions))
                    return;
                LayoutFiveHeroHud(__instance, ___m_Positions, ___m_HudWidth, GetExpandedPartyCount());
            }
        }

        [HarmonyPatch(typeof(uiPortraitHolder), "UpdateDisplay")]
        private static class FiveHeroPortraitPatch
        {
            private static bool Prefix(uiPortraitHolder __instance, ref bool __result)
            {
                if (!IsFiveHeroPartyActive())
                    return true;
                if (EnsurePortraitActionPoints(__instance))
                    return true;
                Plugin.Log.LogError("[five-hero] a portrait holder did not expose five action-point slots; skipping its update.");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(uiItemMenu), "Show", new[] { typeof(Vector2), typeof(CharacterOverworld), typeof(uiItemIcon), typeof(bool) })]
        private static class FiveHeroItemTransferPatch
        {
            private static bool Prefix(uiItemMenu __instance)
            {
                if (!IsFiveHeroPartyActive() || EnsureGiveButtonCapacity(__instance))
                    return true;
                ShowRuntimeError("Five-hero item transfer could not prepare the native destination buttons. The item menu was not opened.");
                return false;
            }
        }

        [HarmonyPatch(typeof(uiGoldMenu), "Awake")]
        private static class FiveHeroGoldMenuAwakePatch
        {
            private static void Postfix(uiGoldMenu __instance)
            {
                if (IsFiveHeroPartyActive())
                    EnsureGoldMenuCapacity(__instance, GetExpandedPartyCount());
            }
        }

        [HarmonyPatch(typeof(uiGoldMenu), "Show", new[] { typeof(Vector2), typeof(CharacterOverworld) })]
        private static class FiveHeroGoldTransferPatch
        {
            private static bool Prefix(uiGoldMenu __instance)
            {
                if (!IsFiveHeroPartyActive())
                    return true;
                if (!EnsureGoldMenuCapacity(__instance, GetExpandedPartyCount()))
                {
                    ShowRuntimeError("Five-hero gold transfer could not prepare the native recipient rows. The transfer menu was not opened.");
                    return false;
                }
                _initializingGoldMenus.Add(__instance);
                return true;
            }

            private static void Postfix(uiGoldMenu __instance)
            {
                _initializingGoldMenus.Remove(__instance);
            }

            private static Exception Finalizer(uiGoldMenu __instance, Exception __exception)
            {
                _initializingGoldMenus.Remove(__instance);
                return __exception;
            }

        }

        [HarmonyPatch(typeof(uiGoldMenu), "SetGold")]
        private static class FiveHeroGoldAllocationPatch
        {
            private static bool Prefix(uiGoldMenu __instance, uiGoldMenuEntry __0, int __1)
            {
                if (!IsFiveHeroPartyActive())
                    return true;
                if (_initializingGoldMenus.Contains(__instance))
                    return false;
                if (_settingExpandedGold.Contains(__instance))
                    return false;
                if (TryApplyExpandedGoldAllocation(__instance, __0, __1))
                    return false;
                ShowRuntimeError("Five-hero gold allocation could not be checked safely. No gold was transferred.");
                return false;
            }
        }

        [HarmonyPatch(typeof(uiGoldMenu), "OnButtonGive")]
        private static class FiveHeroGoldGivePatch
        {
            private static bool Prefix(uiGoldMenu __instance)
            {
                if (!IsFiveHeroPartyActive() || HasValidExpandedGoldAllocation(__instance))
                    return true;
                ShowRuntimeError("The gold allocation exceeds the sender's current gold or has invalid values. No gold was transferred.");
                return false;
            }
        }

        [HarmonyPatch(typeof(uiPopupMenu), "Awake")]
        private static class FiveHeroItemPopupAwakePatch
        {
            private static void Postfix(uiPopupMenu __instance)
            {
                if (IsFiveHeroPartyActive())
                    EnsurePopupGiveCapacity(__instance, GetExpandedPartyCount());
            }
        }

        [HarmonyPatch(typeof(uiPopupMenu), "Show", new[] { typeof(Vector2), typeof(CharacterOverworld), typeof(uiItemIcon) })]
        private static class FiveHeroItemPopupPatch
        {
            private static bool Prefix(uiPopupMenu __instance)
            {
                if (!IsFiveHeroPartyActive() || EnsurePopupGiveCapacity(__instance, GetExpandedPartyCount()))
                    return true;
                ShowRuntimeError("Five-hero item transfer could not prepare the native recipient buttons. The item menu was not opened.");
                return false;
            }
        }

        [HarmonyPatch(typeof(Diorama), "_resetTargetQueue")]
        private static class FiveHeroCombatDistancePatch
        {
            private static void Prefix(Diorama __instance, Diorama.LayoutID _layoutID)
            {
                if (IsFiveHeroPartyActive())
                    ExtendDioramaDistances(__instance, _layoutID);
            }
        }

        [HarmonyPatch(typeof(ReInput.PlayerHelper), "GetPlayer", new[] { typeof(int) })]
        private static class ExpandedSetupInputPatch
        {
            private static bool Prefix(int playerId, ref Player __result)
            {
                if (!IsExpandedSinglePlayerActive() || playerId < VanillaPlayerCapacity)
                    return true;
                try
                {
                    Player sharedInput = ReInput.players.GetPlayer(VanillaPlayerCapacity - 1);
                    if (sharedInput != null)
                    {
                        __result = sharedInput;
                        return false;
                    }
                    __result = ReInput.players.GetSystemPlayer();
                }
                catch (Exception error)
                {
                    Plugin.Log.LogWarning("[five-hero] extra hero input lookup failed: " + error.Message);
                    __result = null;
                }
                if (__result == null)
                    Plugin.Log.LogError("[five-hero] no fallback input player is available for an extra hero.");
                return false;
            }
        }

    }
}
