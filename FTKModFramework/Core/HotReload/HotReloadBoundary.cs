using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using FTKModFramework.Core.UI;
using FTKModFramework.Core.Marketplace;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FTKModFramework.Core.HotReload
{
    // Admission is restricted to the audited platform and managed package lifecycle.
    internal static class HotReloadBoundary
    {
        internal static bool Requested { get { return Environment.GetEnvironmentVariable("FTK_HOT_RELOAD") == "1" ||
            (Plugin.EnableTitleScreenActivation != null && Plugin.EnableTitleScreenActivation.Value); } }
        internal static bool Enabled { get; private set; }
        internal static string EligibilityNotice { get; private set; }
        internal static bool TestEnvironment
        {
            get
            {
                try
                {
                    if (!Requested || Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" ||
                        Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") != "1") return false;
                    string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
                    string root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
                    DirectoryInfo directory = new DirectoryInfo(root);
                    if (string.IsNullOrEmpty(requested) || Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root ||
                        directory.Parent == null || directory.Parent.Name != "scratch") return false;
                    for (DirectoryInfo ancestor = directory; ancestor != null; ancestor = ancestor.Parent)
                        if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                    return directory.Exists;
                }
                catch { return false; }
            }
        }
        internal static void Initialize()
        {
            if (!Requested) return;
            EligibilityNotice = Eligibility();
            Enabled = EligibilityNotice == null;
            if (!Enabled) Plugin.Log.LogInfo("[hot-reload] Next-launch activation: " + EligibilityNotice);
        }
        private static string Eligibility()
        {
            if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") == "1" && !TestEnvironment)
                return "Invalid protected test environment.";
            if (!TestEnvironment && !Directory.Exists("/System/Library/CoreServices"))
                return "Title-screen activation currently supports the audited macOS build; this platform uses next-launch activation.";
            if (!MarketplaceRuntime.CanDiscover || !MarketplaceRuntime.BootstrapVerified || MarketplaceRuntime.LeaseFailure != null || !MarketplaceRuntimeLease.Acquired)
                return "Marketplace startup did not establish exclusive ownership.";
            if (Plugin.SelfTestsEnabled || !Plugin.EnableDataContent.Value ||
                Plugin.SyntheticContentCount.Value != 0 || Plugin.DiagnosticsEnableGate.Value)
                return "Injected or diagnostic content requires next-launch activation.";
            foreach (KeyValuePair<string, PluginInfo> plugin in Chainloader.PluginInfos)
                if (plugin.Key != Plugin.Guid && !(TestEnvironment &&
                    (plugin.Key == "com.ftkmf.model-test-content" || plugin.Key == "com.ftkmf.runtime-model-test")))
                    return "Unsupported loaded plugin: " + plugin.Key;
            if (Directory.Exists(Plugin.DataContentRootPath))
                foreach (string folder in Directory.GetDirectories(Plugin.DataContentRootPath))
                    if (File.Exists(Path.Combine(folder, "manifest.json"))) return "Manual content requires next-launch activation.";
            string packageReason = HotReloadPackagePolicy.InvalidReason(MarketplaceRuntime.Active);
            if (packageReason != null) return packageReason;
            using (SHA256 hash = SHA256.Create())
            using (FileStream file = File.OpenRead(typeof(GridEditor.TableManager).Assembly.Location))
                if (BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant() !=
                    "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8")
                    return "This game build has no audited hot activation adapter.";
            return null;
        }
        internal static string SaveProtectionReason()
        {
            if (!Enabled) return "Title-screen activation is unavailable.";
            if (SaveNamespace.Requested) return SaveNamespace.Ready ? null : "Compatible adventure save routing is not ready.";
            if (!TestEnvironment) return "Production save routing is not ready.";
            try
            {
                if (!Chainloader.PluginInfos.ContainsKey("com.ftkmf.runtime-model-test") ||
                    !Chainloader.PluginInfos.ContainsKey("com.ftkmf.model-test-content")) return "Protected test plugins are missing.";
                string root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
                string token;
                using (SHA256 hash = SHA256.Create())
                    token = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root))).Replace("-", "").ToLowerInvariant().Substring(0, 16);
                string expected = Path.Combine(Application.persistentDataPath, "save-model-test-" + token);
                if (Path.GetFullPath(uiStartGame.GetSavePath()) != Path.GetFullPath(expected)) return "Native save path is not protected.";
                return null;
            }
            catch (Exception e) { return "Cannot establish save isolation: " + e.Message; }
        }
        internal static bool NavigationLocked { get { return Enabled && (HotReloadCoordinator.Busy || HotReloadCoordinator.Faulted); } }
        internal static string SealReason { get; private set; }
        internal static bool TitleObserved;
        internal static void ObserveTitle()
        {
            if (!Enabled || SealReason != null) return;
            StartGameFE.MainScreen screen = uiScreen.gCurrent as StartGameFE.MainScreen;
            uiStartGame title = UnityEngine.Object.FindObjectOfType<uiStartGame>();
            if (title != null && title.m_MainScreen == screen && screen != null && screen.gameObject.scene.name == "FTK_main" && screen.gameObject.scene.isLoaded &&
                screen.gameObject.activeInHierarchy && screen.m_HasInputFocus &&
                FTKInput.Instance != null && FTKInput.Instance.m_CurrentInputFocus == screen) TitleObserved = true;
        }
        internal static void Seal(string reason) { if (Enabled && SealReason == null) SealReason = reason; }

        internal static string Check()
        {
            if (!Enabled) return EligibilityNotice ?? "Title-screen activation is disabled.";
            if (SealReason != null) return "Content is sealed for this process: " + SealReason;
            // Native SplashScreen loads FTK_main additively and remains the active scene.
            uiStartGame title = UnityEngine.Object.FindObjectOfType<uiStartGame>();
            if (!TitleObserved || title == null || title.gameObject.scene.name != "FTK_main" || !title.gameObject.scene.isLoaded)
                return "Initial title has not settled.";
            StartGameFE.MainScreen main = title.m_MainScreen;
            bool titleFocused = main != null && uiScreen.gCurrent == main && main.m_HasInputFocus &&
                main.gameObject.activeInHierarchy && FTKInput.Instance != null && FTKInput.Instance.m_CurrentInputFocus == main;
            if (main == null || !main.gameObject.scene.isLoaded || main.gameObject.scene.name != "FTK_main" ||
                (!titleFocused && !ModsPanel.HasTitleOwner(main))) return "Return to the initial title screen or its Mods menu.";
            if (FTKInput.Instance.m_WaitingForPopup) return "A native popup is pending.";
            if (!DefinitionState.HasBaseline) return "No pristine database baseline.";
            if (Plugin.SelfTestsEnabled || !Plugin.EnableDataContent.Value ||
                Plugin.SyntheticContentCount.Value != 0 || Plugin.DiagnosticsEnableGate.Value)
                return "Diagnostic content is outside title-screen activation.";
            foreach (KeyValuePair<string, PluginInfo> plugin in Chainloader.PluginInfos)
                if (plugin.Key != Plugin.Guid && !(TestEnvironment && (plugin.Key == "com.ftkmf.model-test-content" || plugin.Key == "com.ftkmf.runtime-model-test")))
                    return "Unsupported loaded plugin: " + plugin.Key;
            if (Directory.Exists(Plugin.DataContentRootPath))
                foreach (string folder in Directory.GetDirectories(Plugin.DataContentRootPath))
                    if (File.Exists(Path.Combine(folder, "manifest.json"))) return "Manual content is outside title-screen activation.";
            string saveProtection = SaveProtectionReason();
            if (saveProtection != null) return saveProtection;
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "+connect_lobby") return "Command-line lobby joins are unsupported.";
            if (uiStartGame.gRestartJoinGame || (!PhotonNetwork.offlineMode && PhotonNetwork.connectionState.ToString() != "Disconnected"))
                return "A network connection or join is pending.";
            if (PhotonNetwork.inRoom || PhotonNetwork.insideLobby) return "Network room or lobby is active.";
            if (!string.IsNullOrEmpty(GameLogic.gAction)) return "Native scene action is pending.";
            if (title == null || title.m_GameStarted || title.m_IsResuming || title.m_MapReady ||
                title.m_JoiningDirect || title.m_JoiningOnlineGame || title.m_IsResumingAutoSave || title.m_ResumeGameInfo != null ||
                uiStartGame.IsWaitingForServerBrowser || title.attmeptingToJoinSever || title.m_CreateUIs.Count != 0)
                return "Native title session is not pristine.";
            if (UnityEngine.Object.FindObjectsOfType<uiQuickPlayerCreate>().Length != 0 ||
                UnityEngine.Object.FindObjectsOfType<CharacterOverworld>().Length != 0)
                return "Character consumers still exist.";
            try { PaladinResourceState.RequireQuiescent(); }
            catch (Exception e) { return e.Message; }
            return null;
        }
    }

    [HarmonyPatch]
    internal static class HotReloadTitleNavigation
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "ShowLoreStore", "OnOptionsMenu", "ShowLanguage", "OnGameExit", "OnJoinGame" })
                yield return AccessTools.Method(typeof(StartGameFE.MainScreen), name);
            yield return AccessTools.Method(typeof(StartGameFE.uiLoreStore), "Show");
            yield return AccessTools.Method(typeof(uiOptionsMenu), "Show");
        }
        private static bool Prefix(MethodBase __originalMethod)
        {
            if (!HotReloadBoundary.Enabled) return true;
            if (HotReloadBoundary.NavigationLocked) return false;
            if (__originalMethod.Name == "OnJoinGame")
            {
                HotReloadCoordinator.Notice = "Multiplayer requires launching with title-screen activation disabled.";
                return false;
            }
            if (__originalMethod.Name == "ShowLoreStore" || __originalMethod.DeclaringType == typeof(StartGameFE.uiLoreStore))
                HotReloadBoundary.Seal("Lore Store retains content references");
            return true;
        }
    }

    [HarmonyPatch(typeof(FTKInput), "SetFocus")]
    internal static class HotReloadFocusGuard
    {
        private static bool Prefix(FTKInputFocus _focus)
        {
            return !HotReloadBoundary.NavigationLocked ||
                (FTKInput.Instance != null && FTKInput.Instance.m_CurrentInputFocus == _focus);
        }
    }

    [HarmonyPatch]
    internal static class HotReloadCloseGuard
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(FTKInput), "Close");
            yield return AccessTools.Method(typeof(FTKInput), "LostFocus");
        }
        private static bool Prefix(FTKInputFocus _focus)
        {
            return !HotReloadBoundary.NavigationLocked ||
                (FTKInput.Instance != null && FTKInput.Instance.m_CurrentInputFocus != _focus);
        }
    }

    [HarmonyPatch(typeof(uiStartGame), "Start")]
    internal static class HotReloadStartupJoinGuard
    {
        private static bool Prefix()
        {
            if (!HotReloadBoundary.Enabled) return true;
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "+connect_lobby")
                {
                    HotReloadBoundary.Seal("Command-line lobby join rejected");
                    return false;
                }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class HotReloadSessionEntry
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "ShowGameConfig", "ShowResumeBrowser", "ShowLobby", "OnResumeGame", "SetLoadGame", "SetLoadMap",
                "ShowCreateCharacterMC", "ShowCreateCharacter", "ConnectToServer", "CreateMap", "StartGame", "TutorialStartGame", "EnterGame", "LoadGame", "GameLobbyJoinRequested", "OnLobbyEnter", "JoinGameDirect", "_OnJoinedLobby", "OnJoinedRoom", "_OnJoinedRoom", "_OnCreatedRoom" })
                yield return AccessTools.Method(typeof(uiStartGame), name);
            yield return AccessTools.Method(typeof(uiCharacterCreateRoot), "CreateUI");
            foreach (string name in new[] { "CreateOnlineRoom", "CreateOfflineRoom", "JoinGame", "LoadGame", "SaveGame", "SaveMap", "RestartGame" })
                yield return AccessTools.Method(typeof(GameLogic), name);
        }
        private static bool Prefix(MethodBase __originalMethod)
        {
            if (ClassPreferences.RecoveryFaulted)
            {
                Plugin.Log.LogError(ClassPreferences.RecoveryNotice);
                return false;
            }
            if (MarketplaceRuntime.LeaseFailure != null) return false;
            if (!HotReloadBoundary.Enabled) return true;
            if (HotReloadCoordinator.Busy || HotReloadCoordinator.Faulted) return false;
            string name = __originalMethod.Name;
            if (HotReloadBoundary.SaveProtectionReason() != null) return false;
            if ((name == "_OnJoinedLobby" || name == "OnJoinedRoom" || name == "_OnJoinedRoom" || name == "_OnCreatedRoom") && !PhotonNetwork.offlineMode)
            {
                HotReloadBoundary.Seal("Unsupported online callback " + name);
                return false;
            }
            HotReloadBoundary.Seal(__originalMethod.DeclaringType.Name + "." + name);
            bool resume = name == "ShowResumeBrowser" || name == "OnResumeGame" || name == "SetLoadGame" || name == "SetLoadMap" || name == "LoadGame";
            if ((resume && !SaveNamespace.Requested) || name == "GameLobbyJoinRequested" || name == "OnLobbyEnter" || name == "JoinGameDirect" || name == "ShowLobby" || name == "ConnectToServer" || name == "CreateOnlineRoom" || name == "JoinGame")
            {
                HotReloadCoordinator.Notice = "This save or online entry is unavailable in title-screen activation mode.";
                Plugin.Log.LogWarning("[hot-reload] " + HotReloadCoordinator.Notice);
                return false;
            }
            try { SaveNamespace.PinCurrent(); }
            catch (Exception error) { Plugin.Log.LogError("[hot-reload] Cannot protect adventure content: " + error.Message); return false; }
            return true;
        }
    }
}
