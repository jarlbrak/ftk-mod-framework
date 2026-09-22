using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static JObject HotReloadProbe(JObject command)
    {
        Type coordinator = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "FTKModFramework")
                coordinator = assembly.GetType("FTKModFramework.Core.HotReload.HotReloadCoordinator", true);
        if (coordinator == null) throw new InvalidOperationException("Hot reload framework is unavailable.");
        Dictionary<string, object> args = new Dictionary<string, object>();
        string action = Str(command, "action") ?? "status";
        args["action"] = action == "navigation-lock" || action == "force-gc" ? "status" : action;
        if (command["point"] != null) args["point"] = Str(command, "point");
        object result = coordinator.GetMethod("TestBridge", Statics).Invoke(null, new object[] { args });
        JObject report = JObject.FromObject(result);
        if (action == "force-gc")
        {
            Type boundary = coordinator.Assembly.GetType("FTKModFramework.Core.HotReload.HotReloadBoundary", true);
            if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" || Environment.GetEnvironmentVariable("FTK_HOT_RELOAD") != "1" ||
                report.Value<bool>("busy") || report.Value<bool>("faulted") || boundary.GetMethod("Check", Statics).Invoke(null, null) != null)
                throw new InvalidOperationException("Forced GC requires an idle protected initial title.");
            for (int i = 0; i < 2; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            report = JObject.FromObject(coordinator.GetMethod("TestBridge", Statics).Invoke(null, new object[] { args }));
            report["forcedGcRounds"] = 2;
        }
        Type resources = coordinator.Assembly.GetType("FTKModFramework.Core.HotReload.PaladinResourceState", true);
        JObject assetTimings = new JObject();
        foreach (string name in new[] { "AssetCaptureMs", "AssetWorkerWaitMs", "AssetTextureMs" })
            assetTimings[name] = Convert.ToInt64(resources.GetField(name, Statics).GetValue(null));
        // Worker wait includes frame polling; texture time excludes yields between decodes.
        report["assetPreflightTimings"] = assetTimings;
        if (action == "navigation-lock")
        {
            if (!report.Value<bool>("busy") || report.Value<bool>("faulted")) throw new InvalidOperationException("Navigation probe requires a live activation.");
            FTKInputFocus focus = FTKInput.Instance.m_CurrentInputFocus;
            uiScreen screen = uiScreen.gCurrent;
            int objects = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.GameObject)).Length;
            StartGameFE.MainScreen main = uiStartGame.Instance.m_MainScreen;
            main.ShowLoreStore();
            uiStartGame.Instance.m_LoreStore.Show();
            main.OnOptionsMenu(); uiOptionsMenu.Instance.Show();
            main.ShowLanguage(); main.OnJoinGame(); main.OnGameExit();
            FTKInput.Instance.Close(focus); FTKInput.Instance.LostFocus(focus);
            FTKInput.Instance.SetFocus(main);
            if (focus.GetType().FullName == "FTKModFramework.Core.UI.ModsPanel")
                focus.GetType().GetMethod("GoBack", Members).Invoke(focus, null);
            if (FTKInput.Instance.m_CurrentInputFocus != focus || uiScreen.gCurrent != screen || !focus.gameObject.activeInHierarchy ||
                objects != UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.GameObject)).Length)
                throw new InvalidOperationException("Transaction allowed native navigation or content allocation.");
            report["navigationLocked"] = true;
            report["guardedCalls"] = 12;
        }
        Type saveNamespace = coordinator.Assembly.GetType("FTKModFramework.Core.HotReload.SaveNamespace", true);
        report["saveLibraryReady"] = (bool)saveNamespace.GetProperty("Ready", Statics).GetValue(null, null);
        report["saveFingerprint"] = (string)saveNamespace.GetProperty("Fingerprint", Statics).GetValue(null, null);
        if (report.Value<bool>("saveLibraryReady"))
            report["adventureSavePath"] = (string)saveNamespace.GetMethod("AdventurePathSlash", Statics).Invoke(null, null);
        report["legacySavePath"] = uiStartGame.GetSavePathSlash();
        report["rememberedClasses"] = new JArray(UnityEngine.PlayerPrefs.GetInt("Player0class", -1),
            UnityEngine.PlayerPrefs.GetInt("Player1class", -1), UnityEngine.PlayerPrefs.GetInt("Player2class", -1));
        report["suppressedPlatformWrites"] = suppressedPlatformWrites;
        return report;
    }
}
