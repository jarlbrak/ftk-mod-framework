using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    [DllImport("/usr/lib/libobjc.A.dylib")] static extern IntPtr objc_getClass(string name);
    [DllImport("/usr/lib/libobjc.A.dylib")] static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern IntPtr SendObjectiveC(IntPtr receiver, IntPtr selector);

    void VerifyHotReloadProfile()
    {
        if (Environment.GetEnvironmentVariable("FTK_HOT_RELOAD") != "1" &&
            Environment.GetEnvironmentVariable("FTK_HOT_RELOAD_CONFIG_TEST") != "1") return;
        // This test-only macOS gate validates the native defaults domain before writing a sentinel.
        IntPtr bundle = SendObjectiveC(objc_getClass("NSBundle"), sel_registerName("mainBundle"));
        IntPtr identifier = SendObjectiveC(bundle, sel_registerName("bundleIdentifier"));
        string actual = Marshal.PtrToStringAnsi(SendObjectiveC(identifier, sel_registerName("UTF8String")));
        if (actual != "com.ftkmf.hotreload.test") throw new InvalidOperationException("Unexpected hot-reload test preferences domain: " + actual);
        string dataPath = Application.persistentDataPath.Replace('\\', '/');
        // Unity uses its bundle-ID fallback when the new company/product directory does not exist.
        if (!dataPath.EndsWith("/FTKModFrameworkHotReload/IsolatedTest", StringComparison.Ordinal) &&
            !dataPath.EndsWith("/com.ftkmf.hotreload.test", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected hot-reload persistent data path: " + Application.persistentDataPath);
        InstallHotReloadPlatformGuard();
        const string key = "FTKHotReloadIsolationSentinel";
        PlayerPrefs.SetString(key, sessionId);
        PlayerPrefs.Save();
        if (PlayerPrefs.GetString(key) != sessionId) throw new InvalidOperationException("Test preferences sentinel did not round trip.");
        File.WriteAllText(Path.Combine(root, "hot-reload-profile.json"), new JObject {
            {"bundleIdentifier", actual}, {"sentinelKey", key}, {"sentinelValue", sessionId},
            {"persistentDataPath", Application.persistentDataPath}, {"savePath", isolatedSavePath}
        }.ToString());
    }

    static int suppressedPlatformWrites;
    static void InstallHotReloadPlatformGuard()
    {
        Type stats = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "Assembly-CSharp-firstpass") stats = assembly.GetType("Steamworks.SteamUserStats", true);
        if (stats == null) throw new InvalidOperationException("Native platform wrappers unavailable.");
        Harmony guard = new Harmony("com.ftkmf.runtime-model-test.hot-reload-platform");
        Type api = stats.Assembly.GetType("Steamworks.SteamAPI", true);
        MethodInfo restart = api.GetMethod("RestartAppIfNecessary", BindingFlags.Public | BindingFlags.Static);
        if (restart == null || restart.ReturnType != typeof(bool)) throw new InvalidOperationException("Steam relaunch guard unavailable.");
        guard.Patch(restart, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SuppressPlatformRestart", Statics)));
        int count = 0;
        foreach (MethodInfo method in stats.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            bool boolean = method.Name == "SetStat" || method.Name == "UpdateAvgRateStat" || method.Name == "SetAchievement" ||
                method.Name == "ClearAchievement" || method.Name == "StoreStats" || method.Name == "IndicateAchievementProgress" || method.Name == "ResetAllStats";
            bool asynchronous = method.Name == "FindOrCreateLeaderboard" || method.Name == "UploadLeaderboardScore" || method.Name == "AttachLeaderboardUGC";
            if (!boolean && !asynchronous) continue;
            if (boolean && method.ReturnType != typeof(bool)) throw new InvalidOperationException("Unexpected platform guard signature.");
            guard.Patch(method, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod(boolean ? "SuppressPlatformBoolean" : "SuppressPlatformAsync", Statics)));
            count++;
        }
        if (count != 11) throw new InvalidOperationException("Incomplete platform write guard: " + count);
    }
    static bool SuppressPlatformBoolean(ref bool __result) { suppressedPlatformWrites++; __result = true; return false; }
    static bool SuppressPlatformAsync() { suppressedPlatformWrites++; return false; }
    static bool SuppressPlatformRestart(ref bool __result) { __result = false; return false; }
}
