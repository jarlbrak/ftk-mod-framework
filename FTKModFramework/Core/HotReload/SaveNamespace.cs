using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using FTKModFramework.Core.Marketplace;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    internal static class SaveNamespace
    {
        internal static bool Requested
        {
            get { return HotReloadBoundary.Enabled && (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" || Environment.GetEnvironmentVariable("FTK_HOT_RELOAD_SAVE_TEST") == "1"); }
        }
        internal static bool Ready { get; private set; }
        internal static string Fingerprint { get; private set; }
        private static string directory;
        private static bool patched;
        private static string gameHash;
        private static ManagedSnapshot current;
        internal static void Initialize(ManagedSnapshot active)
        {
            if (!Requested) return;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(typeof(uiStartGame).Assembly.Location))
                gameHash = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (gameHash != "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8")
                throw new InvalidOperationException("Save namespace requires the audited native assembly.");
            InstallPatches();
            Publish(active);
        }
        internal static string Prepare(ManagedSnapshot snapshot)
        {
            string fingerprint = FingerprintFor(snapshot);
            if (fingerprint != null) SaveSetIdentity.CreateOwnedDirectory(SaveSetIdentity.DirectoryFor(Application.persistentDataPath, fingerprint));
            return fingerprint;
        }
        internal static string FingerprintFor(ManagedSnapshot snapshot)
        {
            if (!Requested) return null;
            if (!patched || gameHash == null) throw new InvalidOperationException("Save namespace has no prepared managed generation.");
            Dictionary<string, string> packages = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PackageDescriptor package in snapshot == null ? new List<PackageDescriptor>() : snapshot.Packages)
                if (package.Enabled) packages.Add(package.ModGuid, package.Sha256);
            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.Ordinal);
            settings.Add("dataContent", Plugin.EnableDataContent.Value.ToString());
            settings.Add("campaignEngine", Plugin.EnableCampaignEngine.Value.ToString());
            settings.Add("behaviorLoading", Plugin.EnableBehaviorLoading.Value.ToString());
            string fingerprint = SaveSetIdentity.Compute(gameHash, Plugin.Version, packages, settings);
            return fingerprint;
        }
        internal static void Publish(ManagedSnapshot snapshot)
        {
            if (!Requested) return;
            string fingerprint = Prepare(snapshot);
            directory = SaveSetIdentity.DirectoryFor(Application.persistentDataPath, fingerprint);
            Fingerprint = fingerprint;
            current = snapshot;
            Ready = true;
            uiStartGame title = uiStartGame.Instance;
            if (title != null)
            {
                title.m_ResumeFilename = string.Empty;
                if (title.m_ResumeBrowser != null)
                {
                    Reflect.Invoke(title.m_ResumeBrowser, "ClearList");
                    title.m_ResumeBrowser.m_RunInfo = null;
                }
                if (title.m_MainScreen != null && title.m_MainScreen.gameObject.activeInHierarchy)
                    title.m_MainScreen.OnPreSetFocus();
            }
        }
        internal static void PinCurrent()
        {
            if (!Requested) return;
            if (!Ready) throw new InvalidOperationException("Save namespace is not ready.");
            SaveSetIdentity.ValidateOwnedDirectory(directory);
            if (current == null) throw new InvalidOperationException("Save library has no durable managed generation.");
            SaveSetIdentity.WritePin(MarketplaceRuntime.StateRoot, Fingerprint, current.GenerationId);
        }
        internal static void ValidateResumePath(string path)
        {
            if (!Requested) return;
            if (!Ready || !SaveSetIdentity.IsSavePath(directory, path, true))
                throw new InvalidOperationException("Resume file is outside the active content save library.");
        }
        private static bool SanitizeLastSave()
        {
            if (!Requested) return true;
            string key = LastSaveKey();
            string value = PlayerPrefs.GetString(key, "");
            if (value.Length != 0 && !SaveSetIdentity.IsSavePath(directory, value, true))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            return GuardScan();
        }
        private static bool GuardScan()
        {
            if (!Requested) return true;
            try
            {
                foreach (string file in Directory.GetFiles(AdventurePathSlash()))
                    if (string.Equals(Path.GetExtension(file), ".run", StringComparison.OrdinalIgnoreCase)) ValidateResumePath(file);
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[save-namespace] Refusing unsafe save scan: " + error.Message);
                uiStartGame title = uiStartGame.Instance;
                if (title != null && title.m_MainScreen != null) title.m_MainScreen.m_ResumeButton.gameObject.SetActive(false);
                return false;
            }
        }
        private static bool GuardResume(StartGameFE.ResumeBrowser.RunInfo _runInfo)
        {
            if (!Requested) return true;
            try { ValidateResumePath(_runInfo == null ? null : _runInfo.m_Filename); PinCurrent(); return true; }
            catch (Exception error) { Plugin.Log.LogError("[save-namespace] " + error.Message); return false; }
        }
        private static bool GuardLoad(uiStartGame __instance)
        {
            if (!Requested) return true;
            try
            {
                ValidateResumePath(Convert.ToString(Reflect.GetField(__instance, "m_LoadFileName")));
                if (!string.IsNullOrEmpty(__instance.m_ResumeFilename)) ValidateResumePath(__instance.m_ResumeFilename);
                PinCurrent(); return true;
            }
            catch (Exception error) { Plugin.Log.LogError("[save-namespace] " + error.Message); return false; }
        }
        internal static string AdventurePathSlash()
        {
            if (!Requested) return uiStartGame.GetSavePathSlash();
            if (!Ready) throw new InvalidOperationException("Save namespace is not ready.");
            SaveSetIdentity.ValidateOwnedDirectory(directory);
            return directory + Path.DirectorySeparatorChar;
        }
        internal static string LastSaveKey()
        {
            if (!Requested) return "LastSave";
            if (!Ready) throw new InvalidOperationException("Save namespace is not ready.");
            return "FTKMF.LastSave." + Fingerprint;
        }
        private static void OldSaveName(ref string __result)
        {
            if (Requested) __result = Path.Combine(AdventurePathSlash(), Path.GetFileName(__result));
        }
        private static void InstallPatches()
        {
            if (patched) return;
            Harmony harmony = new Harmony("com.ftkmf.save-namespace");
            foreach (MethodInfo method in new[] {
                AccessTools.Method(typeof(uiStartGame), "GenerateResumeFilename"),
                AccessTools.Method(typeof(StartGameFE.ResumeBrowser), "RefreshGameList"),
                AccessTools.Method(typeof(StartGameFE.MainScreen), "OnPreSetFocus"),
                AccessTools.Method(typeof(StartGameFE.MainScreen), "OnResume"),
                AccessTools.Method(typeof(GameLogic), "Save", new[] { typeof(bool), typeof(bool) }) })
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(SaveNamespace), "RewritePaths"));
            harmony.Patch(AccessTools.Method(typeof(uiStartGame), "GetSaveFileFullName"), postfix: new HarmonyMethod(typeof(SaveNamespace), "OldSaveName"));
            foreach (string name in new[] { "OnPreSetFocus", "OnResume" })
                harmony.Patch(AccessTools.Method(typeof(StartGameFE.MainScreen), name), prefix: new HarmonyMethod(typeof(SaveNamespace), "SanitizeLastSave"));
            harmony.Patch(AccessTools.Method(typeof(StartGameFE.ResumeBrowser), "RefreshGameList"), prefix: new HarmonyMethod(typeof(SaveNamespace), "GuardScan"));
            harmony.Patch(AccessTools.Method(typeof(uiStartGame), "OnResumeGame"), prefix: new HarmonyMethod(typeof(SaveNamespace), "GuardResume"));
            harmony.Patch(AccessTools.Method(typeof(uiStartGame), "LoadGame"), prefix: new HarmonyMethod(typeof(SaveNamespace), "GuardLoad"));
            patched = true;
        }
        private static IEnumerable<CodeInstruction> RewritePaths(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int paths = 0, keys = 0;
            List<CodeInstruction> result = new List<CodeInstruction>();
            MethodInfo nativePath = AccessTools.Method(typeof(uiStartGame), "GetSavePathSlash");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(nativePath))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(SaveNamespace), "AdventurePathSlash"); paths++;
                }
                if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == "LastSave")
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(SaveNamespace), "LastSaveKey"); keys++;
                }
                result.Add(instruction);
            }
            string name = __originalMethod.Name;
            int expectedPaths = name == "GenerateResumeFilename" || name == "RefreshGameList" || name == "OnPreSetFocus" ? 1 : 0;
            int expectedKeys = name == "OnPreSetFocus" ? 2 : name == "OnResume" || name == "Save" ? 1 : 0;
            if (paths != expectedPaths || keys != expectedKeys) throw new InvalidOperationException("Native save route changed: " + __originalMethod);
            return result;
        }
    }
}
